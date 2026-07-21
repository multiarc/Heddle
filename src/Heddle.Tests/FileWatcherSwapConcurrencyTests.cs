using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Heddle.Attributes;
using Heddle.Core;
using Heddle.Data;
using Heddle.Language;
using Heddle.Runtime;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// Phase 1 D5 — the live-document swap under concurrent renders/publishers: coherent old-or-new
    /// output, no use-after-release, exactly-once release of every superseded document, post-Teardown
    /// publishes discarded not resurrected. Modeled on <see cref="BranchConcurrencyTests"/>; the D5
    /// correctness proof carries the guarantee, the green runs corroborate it.
    /// </summary>
    public class FileWatcherSwapConcurrencyTests
    {
        // Per-test watched-file stem: isolates this test from concurrent tests and parallel TFM hosts.
        private readonly string _stem = FileWatcherTestSupport.NewStem();

        private static void AssertQueueDrained(HeddleTemplate template)
        {
            var queue = FileWatcherTestSupport.GetSupersededQueue(template);
            Assert.True(queue == null || queue.IsEmpty,
                "the superseded queue still holds " + (queue?.Count ?? 0) + " document(s) after quiescing");
        }

        /// <summary>The headline stress (test-with): 20k iterations of mostly renders with interleaved
        /// <c>Recompile</c> swaps between two distinguishable contents — every render is exactly OLD or NEW
        /// (never torn/empty), nothing throws <c>ObjectDisposedException</c> (no use-after-release), and the
        /// superseded queue is fully drained once renders quiesce.</summary>
        [Fact]
        public void RecompileSwapInterleavedWithConcurrentRendersIsCoherentAndLeakFree()
        {
            using var template = new HeddleTemplate("OLD", new CompileContext());
            Assert.True(template.CompileResult.Success, template.CompileResult.ToString());

            Parallel.For(0, 20000, i =>
            {
                if (i % 97 == 0)
                {
                    var result = template.Recompile(i % 2 == 0 ? "OLD" : "NEW", new CompileContext());
                    Assert.True(result.Success, result.ToString());
                }
                else
                {
                    var output = template.Generate(null);
                    Assert.True(output == "OLD" || output == "NEW", "torn render output: '" + output + "'");
                }
            });

            // Quiesce: one more render forces the last ExitRender through the _runners == 0 drain.
            var final = template.Generate(null);
            Assert.True(final == "OLD" || final == "NEW", "torn final output: '" + final + "'");
            AssertQueueDrained(template);
        }

        /// <summary>The multi-publisher race the pre-<c>_publishGate</c> design failed (test-with):
        /// several tasks loop <c>Recompile</c> concurrently while renders run. Every output is coherent, no
        /// disposed-document render, and after quiescing + dispose every witness-carrying document was
        /// disposed exactly once (no double-release, no orphan).</summary>
        [Fact]
        public async Task ConcurrentPublishersDoNotTearOrDoubleReleaseUnderRenders()
        {
            HeddleTemplate.Configure(typeof(FileWatcherSwapConcurrencyTests).GetTypeInfo().Assembly);
            DisposalWitnessExtension.Register();
            DisposalWitnessExtension.Reset();

            var template = new HeddleTemplate("OLD@p1witness()", new CompileContext());
            Assert.True(template.CompileResult.Success, template.CompileResult.ToString());

            var tasks = new Task[6];
            for (var p = 0; p < 2; p++)
            {
                var seed = p;
                tasks[p] = Task.Run(() =>
                {
                    for (var i = 0; i < 150; i++)
                    {
                        var content = (i + seed) % 2 == 0 ? "OLD@p1witness()" : "NEW@p1witness()";
                        var result = template.Recompile(content, new CompileContext());
                        Assert.True(result.Success, result.ToString());
                    }
                });
            }
            for (var r = 2; r < 6; r++)
            {
                tasks[r] = Task.Run(() =>
                {
                    for (var i = 0; i < 3000; i++)
                    {
                        var output = template.Generate(null);
                        Assert.True(output == "OLD" || output == "NEW", "torn render output: '" + output + "'");
                    }
                });
            }
            await Task.WhenAll(tasks);

            var final = template.Generate(null);   // quiesce: last ExitRender drains at _runners == 0
            Assert.True(final == "OLD" || final == "NEW", "torn final output: '" + final + "'");
            AssertQueueDrained(template);

            template.Dispose();                    // Teardown releases the live document too
            Assert.Equal(0, DisposalWitnessExtension.DoubleDisposeCount);
            Assert.Equal(DisposalWitnessExtension.CreatedCount, DisposalWitnessExtension.DisposedCount);
        }

        /// <summary>Blocker-2 drain-TOCTOU witness (deterministic interleaving via an in-render publisher):
        /// while a render of doc₁ is in flight, its extension publishes doc₂ (superseding doc₁, whose drain
        /// must defer on <c>_runners</c>) and then a NEW render on another thread snapshots and renders doc₂
        /// to completion. Neither render throws, both outputs match their snapshotted document, and the
        /// deferred doc₁ is released once the first render exits.</summary>
        [Fact]
        public void DrainDoesNotDisposeADocumentANewRenderHolds()
        {
            HeddleTemplate.Configure(typeof(FileWatcherSwapConcurrencyTests).GetTypeInfo().Assembly);
            ReentrantPublishExtension.Register();

            var template = new HeddleTemplate("OLD-@p1reentrant()", new CompileContext());
            Assert.True(template.CompileResult.Success, template.CompileResult.ToString());
            try
            {
                string innerRenderOutput = null;
                ReentrantPublishExtension.OnRender = () =>
                {
                    var result = template.Recompile("NEW", new CompileContext());   // supersede the doc this render holds
                    Assert.True(result.Success, result.ToString());
                    // A fresh render on another thread snapshots the NEW document while the OLD render is
                    // still active (so the publish-drain above deferred on _runners) and completes fully.
                    innerRenderOutput = Task.Run(() => template.Generate(null)).GetAwaiter().GetResult();
                };

                var outerOutput = template.Generate(null);   // holds OLD across the publish

                Assert.Equal("OLD-", outerOutput);           // the held snapshot was never disposed under the render
                Assert.Equal("NEW", innerRenderOutput);
                Assert.Equal("NEW", template.Generate(null));
                AssertQueueDrained(template);                // the deferred OLD document was released on exit
            }
            finally
            {
                ReentrantPublishExtension.OnRender = null;
                template.Dispose();
            }
        }

        /// <summary>Blocker-1 ordering regression (deterministic): a reload triggered from INSIDE a render of
        /// the superseded document leaves the in-flight render's snapshot alive — the snapshot is taken after
        /// <c>EnterRender</c>'s increment, so the drain's <c>_runners</c> gate provably covers it.</summary>
        [Fact]
        public void SnapshotTakenAfterEnterRender()
        {
            HeddleTemplate.Configure(typeof(FileWatcherSwapConcurrencyTests).GetTypeInfo().Assembly);
            ReentrantPublishExtension.Register();

            var template = new HeddleTemplate("A@p1reentrant()B", new CompileContext());
            Assert.True(template.CompileResult.Success, template.CompileResult.ToString());
            try
            {
                ReentrantPublishExtension.OnRender = () =>
                {
                    var result = template.Recompile("SWAPPED", new CompileContext());
                    Assert.True(result.Success, result.ToString());
                };

                // The render that triggered the swap still completes off its own (superseded) snapshot.
                Assert.Equal("AB", template.Generate(null));
                Assert.Equal("SWAPPED", template.Generate(null));
                AssertQueueDrained(template);
            }
            finally
            {
                ReentrantPublishExtension.OnRender = null;
                template.Dispose();
            }
        }

        /// <summary>Blocker-3 post-teardown path (deterministic): a watcher callback landing after
        /// <c>Dispose()</c> compiles, hits the store block's dispose guard, and releases the fresh artifact
        /// instead of publishing onto (resurrecting) the torn-down template.</summary>
        [Fact]
        public void PublishAfterTeardownDisposesNewArtifactAndDoesNotResurrect()
        {
            HeddleTemplate.Configure(typeof(FileWatcherSwapConcurrencyTests).GetTypeInfo().Assembly);
            DisposalWitnessExtension.Register();
            var dir = FileWatcherTestSupport.NewTempDir();
            try
            {
                var path = Path.Combine(dir, _stem + ".heddle");
                File.WriteAllText(path, "LIVE@p1witness()");
                var template = new HeddleTemplate(FileWatcherTestSupport.WatchOptions(dir, _stem));
                Assert.True(template.CompileResult.Success, template.CompileResult.ToString());
                FileWatcherTestSupport.Disarm(template);
                var liveDoc = FileWatcherTestSupport.GetRuntimeDocument(template);
                Assert.NotNull(liveDoc);

                template.Dispose();                                 // _runners == 0 → Teardown runs now
                DisposalWitnessExtension.Reset();

                File.WriteAllText(path, "ZOMBIE@p1witness()");
                FileWatcherTestSupport.InvokeChanged(template, dir, _stem + ".heddle");   // late callback

                // The fresh artifact was disposed by the store block's guard (its witness fired) …
                Assert.Equal(DisposalWitnessExtension.CreatedCount, DisposalWitnessExtension.DisposedCount);
                Assert.True(DisposalWitnessExtension.DisposedCount > 0,
                    "the late publish must have compiled and then released a fresh artifact");
                // … and nothing was resurrected onto the torn-down template.
                Assert.Same(liveDoc, FileWatcherTestSupport.GetRuntimeDocument(template));
                AssertQueueDrained(template);
            }
            finally
            {
                FileWatcherTestSupport.CleanupDir(dir);
            }
        }

        /// <summary>The leak fix (deterministic): one idle reload (no concurrent render) disposes the
        /// superseded document at the publish drain — it is never orphaned as before this phase.</summary>
        [Fact]
        public void SupersededDocumentIsDisposedAfterReloadWhenIdle()
        {
            HeddleTemplate.Configure(typeof(FileWatcherSwapConcurrencyTests).GetTypeInfo().Assembly);
            DisposalWitnessExtension.Register();
            var dir = FileWatcherTestSupport.NewTempDir();
            try
            {
                var path = Path.Combine(dir, _stem + ".heddle");
                File.WriteAllText(path, "ONE@p1witness()");
                using var template = new HeddleTemplate(FileWatcherTestSupport.WatchOptions(dir, _stem));
                Assert.True(template.CompileResult.Success, template.CompileResult.ToString());
                FileWatcherTestSupport.Disarm(template);
                DisposalWitnessExtension.Reset();

                File.WriteAllText(path, "TWO@p1witness()");
                FileWatcherTestSupport.InvokeChanged(template, dir, _stem + ".heddle");   // idle reload
                Assert.True(template.CompileResult.Success, template.CompileResult.ToString());

                Assert.Equal("TWO", template.Generate(null));
                Assert.Equal(1, DisposalWitnessExtension.DisposedCount);   // the ONE document was released …
                AssertQueueDrained(template);                              // … not parked in the queue
                Assert.Equal(0, DisposalWitnessExtension.DoubleDisposeCount);
            }
            finally
            {
                FileWatcherTestSupport.CleanupDir(dir);
            }
        }
    }

    /// <summary>
    /// Phase 1 disposal witness: one instance per compiled document occurrence; counts constructions and
    /// <c>Dispose(true)</c> calls (finalizer passes are ignored so background GC cannot skew the counts),
    /// flagging any instance disposed twice. The exactly-once-release assertions read these counters.
    /// </summary>
    [ExtensionName("p1witness")]
    public class DisposalWitnessExtension : AbstractExtension
    {
        private static readonly object Gate = new object();
        private static bool _registered;
        private static int _created;
        private static int _disposed;
        private static int _doubleDisposed;
        private int _myDisposeCount;

        public static int CreatedCount => Volatile.Read(ref _created);
        public static int DisposedCount => Volatile.Read(ref _disposed);
        public static int DoubleDisposeCount => Volatile.Read(ref _doubleDisposed);

        public DisposalWitnessExtension()
        {
            Interlocked.Increment(ref _created);
        }

        public static void Reset()
        {
            Volatile.Write(ref _created, 0);
            Volatile.Write(ref _disposed, 0);
            Volatile.Write(ref _doubleDisposed, 0);
        }

        public static void Register()
        {
            lock (Gate)
            {
                if (_registered)
                    return;
                if (!TemplateFactory.Exists("p1witness"))
                    TemplateFactory.AddExtensions(new[]
                        { new ExtensionType("p1witness", typeof(DisposalWitnessExtension), false) });
                _registered = true;
            }
        }

        public override ExType InitStart(InitContext initContext, ExType dataType, ExType chainedType, ExType parent)
        {
            base.InitStart(initContext, dataType, chainedType, parent);
            return typeof(string);
        }

        public override object ProcessData(in Scope scope) => string.Empty;

        public override void RenderData(in Scope scope)
        {
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (Interlocked.Exchange(ref _myDisposeCount, 1) == 0)
                    Interlocked.Increment(ref _disposed);
                else
                    Interlocked.Increment(ref _doubleDisposed);
            }
            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// Phase 1 reentrancy hook: on render it runs the test-supplied callback once (e.g. a
    /// <c>Recompile</c> that supersedes the very document this render was invoked from) — the deterministic
    /// driver for the snapshot-after-<c>EnterRender</c> and drain-gate regressions.
    /// </summary>
    [ExtensionName("p1reentrant")]
    public class ReentrantPublishExtension : AbstractExtension
    {
        private static readonly object Gate = new object();
        private static bool _registered;

        /// <summary>Set by a test; invoked (and cleared) on the next render of this extension.</summary>
        public static Action OnRender;

        public static void Register()
        {
            lock (Gate)
            {
                if (_registered)
                    return;
                if (!TemplateFactory.Exists("p1reentrant"))
                    TemplateFactory.AddExtensions(new[]
                        { new ExtensionType("p1reentrant", typeof(ReentrantPublishExtension), false) });
                _registered = true;
            }
        }

        public override ExType InitStart(InitContext initContext, ExType dataType, ExType chainedType, ExType parent)
        {
            base.InitStart(initContext, dataType, chainedType, parent);
            return typeof(string);
        }

        public override object ProcessData(in Scope scope)
        {
            RunCallback();
            return string.Empty;
        }

        public override void RenderData(in Scope scope)
        {
            RunCallback();
        }

        private static void RunCallback()
        {
            var callback = Interlocked.Exchange(ref OnRender, null);
            callback?.Invoke();
        }
    }
}
