using System.Collections.Generic;
using System.Reflection;
using Heddle;
using Heddle.Data;
using Heddle.Runtime;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// The body-execution entry-point isolation inventory: every path that begins executing a subtemplate
    /// body installs a fresh/cleared frame so branch state never crosses a body boundary, while sibling
    /// composition paths share the enclosing body's frame. A missed entry point would leak state silently,
    /// so every known entry point gets its own case here.
    /// </summary>
    public class BranchIsolationTests
    {
        public class Flag { public bool A { get; set; } public bool B { get; set; } }
        public class ForHolder { public Heddle.Models.Range Range { get; set; } public bool T { get; set; } }

        private static HeddleTemplate Compile(string template, ExType modelType)
        {
            HeddleTemplate.Configure(typeof(BranchIsolationTests).GetTypeInfo().Assembly);
            BranchTestExtensions.Register();
            var t = new HeddleTemplate(template, new CompileContext(modelType));
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            return t;
        }

        [Fact]
        public void RootLevelSetIsIndependentAcrossGenerateCalls()
        {
            var t = Compile("@if(A){{X}}@else(){{Y}}", typeof(Flag));
            Assert.Equal("X", t.Generate(new Flag { A = true }));
            Assert.Equal("Y", t.Generate(new Flag { A = false }));
            Assert.Equal("X", t.Generate(new Flag { A = true }));
            Assert.Equal("Y", t.Generate(new Flag { A = false }));
        }

        [Fact]
        public void FunnelRenderPathInstallsFreshOrClearedFrame()
        {
            // (a) participating body: root has probe (participant) -> root frame; if-body has probe -> fresh frame.
            ScopeProbeExtension.Reset();
            Compile("@probe()@if(A){{@probe()}}", typeof(Flag)).Generate(new Flag { A = true });
            var frames = ScopeProbeExtension.Frames;
            Assert.Equal(2, frames.Count);
            Assert.NotNull(frames[0]);
            Assert.NotNull(frames[1]);
            Assert.NotSame(frames[0], frames[1]); // nested body's frame is fresh, not the parent's

            // (b) non-participating body under a provisioned parent -> cleared (null).
            PlainProbeExtension.Reset();
            Compile("@probe()@if(A){{@plainprobe()}}", typeof(Flag)).Generate(new Flag { A = true });
            var b = PlainProbeExtension.Frames;
            Assert.Single(b);
            Assert.Null(b[0]);

            // (c) non-participating body under a non-provisioned parent -> passthrough (null) fast path.
            PlainProbeExtension.Reset();
            Compile("@(A){{@plainprobe()}}", typeof(Flag)).Generate(new Flag { A = true });
            var c = PlainProbeExtension.Frames;
            Assert.Single(c);
            Assert.Null(c[0]);
        }

        [Fact]
        public void FunnelProcessPathInstallsFreshFrame()
        {
            ScopeProbeExtension.Reset();
            // Run d() as a chain parameter because @out(d()) is now HED5012; same ProcessData funnel path.
            Compile("@%<d>{{@probe()}}%@@(d())", typeof(Flag)).Generate(new Flag());
            var frames = ScopeProbeExtension.Frames;
            Assert.NotEmpty(frames);
            Assert.NotNull(frames[frames.Count - 1]); // participating body -> fresh frame on the process path
        }

        [Fact]
        public void ListIterationResetsBranchState()
        {
            var t = Compile("@list(Items){{@branchreader()@if(V){{F}}}}", typeof(ListReaderModel));
            var model = new ListReaderModel { Items = new List<Cell> { new Cell { V = true }, new Cell { V = true } } };
            // Each iteration starts fresh: reader NONE, then @if publishes SAT and renders F.
            Assert.Equal("NONEFNONEF", t.Generate(model));
        }

        public class Cell { public bool V { get; set; } }
        public class ListReaderModel { public List<Cell> Items { get; set; } }

        [Fact]
        public void ForIterationResetsBranchState()
        {
            var t = Compile("@for(Range){{@branchreader()@if(T){{X}}}}", typeof(ForHolder));
            var model = new ForHolder { Range = new Heddle.Models.Range(0, 2), T = true };
            Assert.Equal("NONEXNONEX", t.Generate(model));
        }

        [Fact]
        public void NestedSetInsideIfBodyIsIndependent()
        {
            var t = Compile("@if(A){{@if(B){{II}}@else(){{IE}}}}@else(){{OE}}", typeof(Flag));
            Assert.Equal("II", t.Generate(new Flag { A = true, B = true }));
            Assert.Equal("IE", t.Generate(new Flag { A = true, B = false }));
            Assert.Equal("OE", t.Generate(new Flag { A = false }));
        }

        [Fact]
        public void SwapBodyGetsFreshFrame()
        {
            // Enclosing set satisfied (A true), then a swap body reads: must be NONE (fresh), not SAT.
            // Use bodiless @out() because @out(A) is now HED5012.
            var t = Compile("@if(A){{}}@out():swap(){{@branchreader()}}", typeof(Flag));
            Assert.Equal("NONE", t.Generate(new Flag { A = true }));
        }

        [Fact]
        public void RescopingContainerBodyGetsFreshFrame()
        {
            var raw = Compile("@if(A){{}}@(A){{@branchreader()}}", typeof(Flag));
            Assert.Equal("NONE", raw.Generate(new Flag { A = true }));

            var html = Compile("@if(A){{}}@html(A){{@branchreader()}}", typeof(Flag));
            Assert.Equal("NONE", html.Generate(new Flag { A = true }));
        }

        [Fact]
        public void FormatBodyCannotReadEnclosingBranchState()
        {
            var t = Compile("@if(A){{}}@date(D){{@branchreader()}}", typeof(DateHolder));
            var outp = t.Generate(new DateHolder { A = true, D = new System.DateTime(2021, 1, 1) });
            Assert.Contains("NONE", outp);
            Assert.DoesNotContain("SAT", outp);
        }

        public class DateHolder { public bool A { get; set; } public System.DateTime D { get; set; } }

        [Fact]
        public void CallerContentSetIsIndependentOfInvocationSiblings()
        {
            HeddleTemplate.Configure(typeof(BranchIsolationTests).GetTypeInfo().Assembly);
            BranchTestExtensions.Register();
            var t = new HeddleTemplate(
                "@model(){{dynamic}}@%<wrap>{{[@out()]}}%@@if(A){{}}@wrap(){{@else(){{E}}}}",
                new CompileContext(typeof(Flag)));
            Assert.False(t.CompileResult.Success);
            Assert.Contains(t.CompileResult.ErrorList, e => e.DiagnosticId == HeddleDiagnosticIds.ElseWithoutIf);
        }

        [Fact]
        public void ParamHasNoBodyExecution()
        {
            // A @param() with a body compiles but never executes the body — no branch participant fires from it.
            var t = Compile("@if(A){{IF}}@else(){{EL}}", typeof(Flag));
            Assert.Equal("EL", t.Generate(new Flag { A = false }));
        }
    }
}
