using System;
using System.Reflection;
using Heddle;
using Heddle.Data;
using Heddle.Runtime;
using Heddle.Tests.Data;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// Allocation proof for the int-<c>@for</c> loop (phase 4 D1 / criterion): the <c>is int</c> normalization
    /// produces three stack locals and never heap-allocates a <see cref="Heddle.Models.ForModel"/>. Rendered
    /// against the C#-tier <c>ForModel</c> path — which builds one <c>ForModel</c> per render — through the
    /// identical loop body, the int path must allocate no more (the spec's "allocated bytes must not increase"
    /// acceptance). The ForModel arm stays first, so existing templates keep their exact type-test sequence.
    /// </summary>
    public class ForSugarAllocationTests
    {
        private static HeddleTemplate Compile(string template, ExType modelType, bool allowCSharp)
        {
            HeddleTemplate.Configure(typeof(ForSugarAllocationTests).GetTypeInfo().Assembly);
            var t = new HeddleTemplate(template, new CompileContext(new TemplateOptions { AllowCSharp = allowCSharp }, modelType));
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
        public void IntForLoopAllocatesNoMoreThanTheForModelPath()
        {
            var model = new ErgoForData { Count = 250 };
            var intFor = Compile("@for(Count){{<i>@out()</i>}}", typeof(ErgoForData), allowCSharp: false);
            var modelFor = Compile(
                "@using(){{Heddle.Tests.Data}}@using(){{Heddle.Models}}@for(@new ForModel(){ Last = model.Count }){{<i>@out()</i>}}",
                typeof(ErgoForData), allowCSharp: true);

            // Byte-identical output: the int arm behaves exactly as ForModel { Last = n }.
            Assert.Equal(modelFor.Generate(model), intFor.Generate(model));

            for (int i = 0; i < 30; i++) { intFor.Generate(model); modelFor.Generate(model); } // warm up (JIT/tiering)

            long intAlloc = Measure(() => { for (int i = 0; i < 200; i++) intFor.Generate(model); });
            long modelAlloc = Measure(() => { for (int i = 0; i < 200; i++) modelFor.Generate(model); });

            // The int path shares the ForModel path's loop body exactly and additionally avoids the per-render
            // ForModel heap object — so it must never allocate more. A small tolerance absorbs GC noise.
            Assert.True(intAlloc <= modelAlloc + 4096,
                $"int-@for allocated {intAlloc} B, ForModel-@for allocated {modelAlloc} B — the int normalization must not increase allocation.");
        }
    }
}
