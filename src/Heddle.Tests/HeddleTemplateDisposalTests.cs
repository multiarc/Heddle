using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Heddle.Data;
using Heddle.Runtime;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// The lock-free disposal synchronization on <see cref="HeddleTemplate"/>
    /// (Interlocked <c>_runners</c>, the <c>_disposeAfterComplete</c> fence, and the one-shot CAS-guarded
    /// <c>Teardown</c>). The stress tests are probabilistic regression witnesses: a green run corroborates
    /// but the memory-ordering argument carries the guarantee — no teardown while any render executes, an
    /// idempotent non-blocking <c>Dispose</c>, and a balanced runner count.
    /// </summary>
    public class HeddleTemplateDisposalTests
    {
        public class Doc { public string Name { get; set; } }

        private static void SetField(HeddleTemplate template, string name, object value)
        {
            var field = typeof(HeddleTemplate).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(field);
            field.SetValue(template, value);
        }

        private static int GetRunners(HeddleTemplate template)
        {
            var field = typeof(HeddleTemplate).GetField("_runners", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(field);
            return (int)field.GetValue(template);
        }

        /// <summary>A <see cref="RuntimeDocument"/> whose disposal is observable — the teardown witness.</summary>
        private sealed class CountingRuntimeDocument : RuntimeDocument
        {
            private int _disposeCount;

            public CountingRuntimeDocument()
                : base("x", new DocumentElement[0], new CompileScope(new CompileContext()))
            {
            }

            public int DisposeCount => Volatile.Read(ref _disposeCount);

            protected override void Dispose(bool disposing)
            {
                Interlocked.Increment(ref _disposeCount);
                base.Dispose(disposing);
            }
        }

        /// <summary>A render strategy that blocks on a gate until the test releases it.</summary>
        private sealed class GateStrategy : IProcessStrategy
        {
            public readonly ManualResetEventSlim Gate = new ManualResetEventSlim(false);
            public readonly CountdownEvent Entered;

            public GateStrategy(int expectedRenders)
            {
                Entered = new CountdownEvent(expectedRenders);
            }

            public string Execute(in Scope scope)
            {
                Render(scope);
                return string.Empty;
            }

            public void Render(in Scope scope)
            {
                Entered.Signal();
                Gate.Wait(TimeSpan.FromSeconds(30));
            }
        }

        /// <summary>Builds a template with the counting witness; installs the strategy as the document's own
        /// strategy to match the store-block invariant (<c>_processStrategy == _runtimeDocument.Strategy</c>).</summary>
        private static HeddleTemplate CreateWitnessTemplate(CountingRuntimeDocument document, IProcessStrategy strategy)
        {
            var template = new HeddleTemplate();
            if (strategy != null)
            {
                var backing = typeof(RuntimeDocument).GetField("<Strategy>k__BackingField",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.NotNull(backing);
                backing.SetValue(document, strategy);
            }
            SetField(template, "_runtimeDocument", document);
            SetField(template, "_processStrategy", strategy ?? document.Strategy);
            return template;
        }

        /// <summary>
        /// Stress witness: <c>Dispose()</c> racing many concurrent renders never tears the document down
        /// under an executing render — every render that began completes with untruncated, uncorrupted output,
        /// and any <see cref="ObjectDisposedException"/> comes only from a render that had not begun
        /// (<c>EnterRender</c> throws before the render body starts).
        /// </summary>
        [Fact]
        public void ConcurrentDisposeDuringActiveRendersNeverTearsOrDisposesUnderRender()
        {
            HeddleTemplate.Configure(typeof(HeddleTemplateDisposalTests).GetTypeInfo().Assembly);
            var random = new Random(42);
            for (int iteration = 0; iteration < 200; iteration++)
            {
                var template = new HeddleTemplate("@(Name)", new CompileContext(typeof(Doc)));
                Assert.True(template.CompileResult.Success, template.CompileResult.ToString());
                int disposeAt = random.Next(64);
                Parallel.For(0, 64, i =>
                {
                    if (i == disposeAt)
                    {
                        template.Dispose();
                        return;
                    }
                    var payload = "value-" + i;
                    try
                    {
                        var result = template.Generate(new Doc { Name = payload });
                        Assert.Equal(payload, result);
                    }
                    catch (ObjectDisposedException)
                    {
                        // Legal: the render observed the dispose fence before it began.
                    }
                });
                template.Dispose();
            }
        }

        /// <summary><c>Dispose()</c> twice on an idle instance tears down exactly once and never throws.</summary>
        [Fact]
        public void DisposeIsIdempotent()
        {
            var document = new CountingRuntimeDocument();
            var template = CreateWitnessTemplate(document, null);

            template.Dispose();
            template.Dispose();

            Assert.Equal(1, document.DisposeCount);
        }

        /// <summary>
        /// Deferred teardown: <c>Dispose()</c> during N gate-blocked renders returns immediately without
        /// tearing down; teardown happens exactly once, after the last render exits, and no in-flight render threw.
        /// </summary>
        [Fact]
        public async Task DeferredDisposeRunsExactlyOnceAfterLastRender()
        {
            const int renders = 4;
            var document = new CountingRuntimeDocument();
            var strategy = new GateStrategy(renders);
            var template = CreateWitnessTemplate(document, strategy);

            var tasks = new Task[renders];
            for (int i = 0; i < renders; i++)
            {
                tasks[i] = Task.Run(() => template.Generate(null));
            }
            Assert.True(strategy.Entered.Wait(TimeSpan.FromSeconds(30)), "renders did not enter the strategy");

            template.Dispose();
            Assert.Equal(0, document.DisposeCount);

            strategy.Gate.Set();
            await Task.WhenAny(Task.WhenAll(tasks), Task.Delay(TimeSpan.FromSeconds(30)));
            foreach (var task in tasks)
            {
                // Guard against silent timeout: IsCompleted false means render still blocked.
                Assert.True(task.IsCompleted && task.Exception == null, task.Exception?.ToString());
            }

            Assert.Equal(1, document.DisposeCount);   // exactly once, by the last ExitRender
            template.Dispose();                       // idempotent re-entry after the deferred teardown
            Assert.Equal(1, document.DisposeCount);
        }

        /// <summary>
        /// High-iteration concurrent renders with no dispose leave the Interlocked runner count balanced
        /// at zero (the pre-fix non-atomic <c>++</c>/<c>--</c> could tear), every render succeeds, and a
        /// subsequent clean <c>Dispose</c> takes the immediate path (a later render throws
        /// <see cref="ObjectDisposedException"/>).
        /// </summary>
        [Fact]
        public void ConcurrentRendersWithoutDisposeLeaveCounterBalanced()
        {
            HeddleTemplate.Configure(typeof(HeddleTemplateDisposalTests).GetTypeInfo().Assembly);
            var template = new HeddleTemplate("@(Name)", new CompileContext(typeof(Doc)));
            Assert.True(template.CompileResult.Success, template.CompileResult.ToString());

            Parallel.For(0, 20000, i =>
            {
                var payload = "value-" + (i % 17);
                Assert.Equal(payload, template.Generate(new Doc { Name = payload }));
            });

            Assert.Equal(0, GetRunners(template));
            template.Dispose();
            Assert.Throws<ObjectDisposedException>(() => template.Generate(new Doc { Name = "late" }));
        }
    }
}
