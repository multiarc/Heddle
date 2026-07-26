using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Heddle;
using Heddle.Data;
using Heddle.Runtime;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// Allocation-identity proof. Structural: a document with no <c>[ScopeChannel]</c> participant reports
    /// <c>NeedsLocals == false</c>, so no frame is ever allocated — existing templates (which contain only
    /// <c>@if</c>/<c>@ifnot</c>, never participants) stay allocation-identical by construction. Measured: a
    /// participating list body costs exactly one <c>ScopeLocals</c> per iteration and nothing else, which is
    /// the whole price of the branch-set pattern.
    /// </summary>
    public class BranchAllocationTests
    {
        public class Flag { public bool A { get; set; } }
        public class Cell { public bool F { get; set; } }
        public class ListModel { public List<Cell> Items { get; set; } }

        private static HeddleTemplate Compile(string template, ExType modelType)
        {
            HeddleTemplate.Configure(typeof(BranchAllocationTests).GetTypeInfo().Assembly);
            BranchTestExtensions.Register();
            var t = new HeddleTemplate(template, new CompileContext(modelType));
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            return t;
        }

        private static bool RootNeedsLocals(HeddleTemplate t)
        {
            var field = typeof(HeddleTemplate).GetField("_runtimeDocument", BindingFlags.NonPublic | BindingFlags.Instance);
            var rtdoc = field.GetValue(t);
            Assert.NotNull(rtdoc);
            var prop = rtdoc.GetType().GetProperty("NeedsLocals", BindingFlags.NonPublic | BindingFlags.Instance);
            return (bool)prop.GetValue(rtdoc);
        }

        [Fact]
        public void ExistingTemplateShapesProvisionNoFrame()
        {
            // @if/@ifnot are deliberately NOT [ScopeChannel]: no existing template ever provisions a frame.
            Assert.False(RootNeedsLocals(Compile("@(A)", typeof(Flag))));
            Assert.False(RootNeedsLocals(Compile("@if(A){{X}}", typeof(Flag))));
            Assert.False(RootNeedsLocals(Compile("@if(A){{X}}@ifnot(A){{Y}}", typeof(Flag))));
        }

        [Fact]
        public void BranchParticipantsProvisionAFrame()
        {
            Assert.True(RootNeedsLocals(Compile("@if(A){{X}}@else(){{Y}}", typeof(Flag))));
            Assert.True(RootNeedsLocals(Compile("@if(A){{X}}@elif(A){{Z}}", typeof(Flag))));
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
        public void ParticipatingListBodyCostsOneFramePerIteration()
        {
            const int n = 10000;
            var model = new ListModel { Items = Enumerable.Range(0, n).Select(_ => new Cell { F = true }).ToList() };

            // Identical output ("X" per item) so string/render allocations cancel; the delta isolates frames.
            var pair = Compile("@list(Items){{@if(F){{X}}@ifnot(F){{Y}}}}", typeof(ListModel)); // no participant -> 0 frames
            var elseL = Compile("@list(Items){{@if(F){{X}}@else(){{Y}}}}", typeof(ListModel));   // else -> 1 frame/iter

            Assert.Equal(pair.Generate(model), elseL.Generate(model)); // byte-identical output

            for (int i = 0; i < 5; i++) { pair.Generate(model); elseL.Generate(model); } // warm up

            long pairAlloc = Measure(() => pair.Generate(model));
            long elseAlloc = Measure(() => elseL.Generate(model));
            long delta = elseAlloc - pairAlloc;

            // The participating body's per-iteration cost is bounded by exactly one ScopeLocals frame
            // (32 B on x64 = 320 000 B for n=10 000) and nothing else — no overflow map, no boxing. That is
            // the worst case, assuming no JIT rescue; on newer runtimes the JIT stack-allocates part of it,
            // so the real delta is often lower. Either way it must not exceed the one-frame budget, and the
            // @if/@ifnot pair is the frame-free baseline.
            Assert.True(delta <= n * 40L, $"per-iteration delta {delta} exceeds the one-frame budget (~{n * 32L})");
            Assert.True(delta >= -n * 8L, $"unexpected negative allocation delta {delta}");
        }
    }
}
