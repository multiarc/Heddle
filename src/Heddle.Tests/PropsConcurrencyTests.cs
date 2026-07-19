using System;
using System.Reflection;
using System.Threading.Tasks;
using Heddle.Data;
using Heddle.Runtime;
using Heddle.Runtime.Parameters;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// The standards' concurrency proof plus the frozen-array immutability/zero-allocation proof: parallel
    /// renders of one compiled template with opposite dynamic prop values stay strictly per-thread, and the
    /// all-constant binder returns the shared frozen array with zero per-invocation allocation.
    /// </summary>
    public class PropsConcurrencyTests
    {
        private static HeddleTemplate Compile(string document, Type modelType)
        {
            HeddleTemplate.Configure(typeof(PropsConcurrencyTests).GetTypeInfo().Assembly);
            var t = new HeddleTemplate(document, new CompileContext(new TemplateOptions(), modelType));
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            return t;
        }

        private static long Measure(Action action)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            long before = GC.GetAllocatedBytesForCurrentThread();
            action();
            return GC.GetAllocatedBytesForCurrentThread() - before;
        }

        [Fact]
        public void ParallelRendersWithDynamicPropsAreIsolated()
        {
            // The named argument reads a caller-scope member (D8), so it is dynamic → the binder clones per
            // invocation. Opposite values per thread must never cross-contaminate.
            var t = Compile(
                "@% <label(text: string = \"def\")>{{[@(text)]}} :: PropArticle %@\n@label(Article, text: style)",
                typeof(PropRoot));

            Parallel.For(0, 4000, i =>
            {
                var value = (i % 2 == 0) ? "EVEN" : "ODD";
                var model = new PropRoot { Article = new PropArticle(), style = value };
                Assert.Equal("[" + value + "]", t.Generate(model).Trim());
            });
        }

        [Fact]
        public void ParallelRendersWithAllConstantPropsShareTheFrozenArray()
        {
            var t = Compile("@% <label(text: string = \"const\")>{{[@(text)]}} :: PropArticle %@\n@label(Article)",
                typeof(PropRoot));

            Parallel.For(0, 4000,
                i => Assert.Equal("[const]", t.Generate(new PropRoot { Article = new PropArticle() }).Trim()));
        }

        [Fact]
        public void AllConstantBindIsZeroAllocation()
        {
            var frozen = new object[] { "a", false };
            var binder = new PropsBinder(frozen, Array.Empty<PropsBinder.DynamicSlot>());
            for (int i = 0; i < 100; i++)
                binder.Bind(Scope.Null); // warm up

            long alloc = Measure(() =>
            {
                for (int i = 0; i < 10000; i++)
                {
                    var props = binder.Bind(Scope.Null);
                    Assert.Same(frozen, props);
                }
            });

            Assert.Equal(0, alloc);
        }

        [Fact]
        public void DynamicBindAllocatesAClonePerInvocation()
        {
            var frozen = new object[] { "a", null };
            var plan = new[] { new PropsBinder.DynamicSlot(1, new ConstantParameter("x"), null) };
            var binder = new PropsBinder(frozen, plan);
            for (int i = 0; i < 100; i++)
                binder.Bind(Scope.Null); // warm up

            long alloc = Measure(() =>
            {
                for (int i = 0; i < 10000; i++)
                    binder.Bind(Scope.Null);
            });

            // A 2-slot object[] clone is 40 B on x64 — the documented per-invocation cost; must be non-zero.
            Assert.True(alloc >= 10000 * 40, $"expected ≥ 400000 B of clones, measured {alloc} B");
        }
    }
}
