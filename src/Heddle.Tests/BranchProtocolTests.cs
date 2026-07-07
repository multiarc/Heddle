using System.Reflection;
using Heddle;
using Heddle.Data;
using Heddle.Exceptions;
using Heddle.Runtime;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// The normative branch protocol state machine (phase 3 D8, rows P01–P21) plus the D9 runtime orphan
    /// column, observed through the public channel: each row renders the winning branch body and a trailing
    /// <c>@branchreader()</c> reports the post-state (<c>SAT</c>/<c>UNSAT</c>/<c>NONE</c>). Also: truthiness
    /// for every value category (null, bool, non-bool, phase 1 typed-bool), the <c>@ifnot</c>+<c>@else</c>
    /// inversion, chained / nested-chain composition, and the custom-publisher-satisfies-set row (criterion 7).
    /// </summary>
    public class BranchProtocolTests
    {
        public class M { public bool A { get; set; } public bool B { get; set; } public int Count { get; set; } public string S { get; set; } public object O { get; set; } }

        private static string Render(string template, M model)
        {
            HeddleTemplate.Configure(typeof(BranchProtocolTests).GetTypeInfo().Assembly);
            BranchTestExtensions.Register();
            var t = new HeddleTemplate(template, new CompileContext(typeof(M)));
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            return t.Generate(model);
        }

        // --- P01/P02: @if starts a set; publishes Satisfied = truthy(c) ---

        [Fact]
        public void P01_IfTruePublishesSatisfiedAndRendersBody()
        {
            Assert.Equal("IFSAT", Render("@if(A){{IF}}@branchreader()", new M { A = true }));
        }

        [Fact]
        public void P02_IfFalsePublishesUnsatisfiedAndRendersNothing()
        {
            Assert.Equal("UNSAT", Render("@if(A){{IF}}@branchreader()", new M { A = false }));
        }

        // --- P07/P08: @ifnot inverts the predicate ---

        [Fact]
        public void P07_IfNotTrueConditionIsUnsatisfied()
        {
            Assert.Equal("UNSAT", Render("@ifnot(A){{IN}}@branchreader()", new M { A = true }));
        }

        [Fact]
        public void P08_IfNotFalseConditionIsSatisfiedAndRenders()
        {
            Assert.Equal("INSAT", Render("@ifnot(A){{IN}}@branchreader()", new M { A = false }));
        }

        // --- P03–P06: a new @if always overwrites the set ---

        [Fact]
        public void P03_P05_SecondIfTrueOverwritesToSatisfied()
        {
            // pre-state false (first @if false), then a true @if.
            Assert.Equal("XSAT", Render("@if(A){{Y}}@if(B){{X}}@branchreader()", new M { A = false, B = true }));
            // pre-state true (first @if true), then a true @if.
            Assert.Equal("YXSAT", Render("@if(A){{Y}}@if(B){{X}}@branchreader()", new M { A = true, B = true }));
        }

        [Fact]
        public void P04_P06_SecondIfFalseOverwritesToUnsatisfied()
        {
            Assert.Equal("UNSAT", Render("@if(A){{Y}}@if(B){{X}}@branchreader()", new M { A = false, B = false }));
            Assert.Equal("YUNSAT", Render("@if(A){{Y}}@if(B){{X}}@branchreader()", new M { A = true, B = false }));
        }

        // --- P13/P14: @elif with no set acts exactly like @if (warns HED3002, tested in the compiler suite) ---

        [Fact]
        public void P13_OrphanElifTrueActsAsIf()
        {
            Assert.Equal("ELSAT", Render("@elif(A){{EL}}@branchreader()", new M { A = true }));
        }

        [Fact]
        public void P14_OrphanElifFalseActsAsIf()
        {
            Assert.Equal("UNSAT", Render("@elif(A){{EL}}@branchreader()", new M { A = false }));
        }

        // --- P15/P16: @elif with Satisfied==false evaluates and may fire ---

        [Fact]
        public void P15_ElifAfterUnsatisfiedTrueFires()
        {
            Assert.Equal("ELSAT", Render("@if(A){{IF}}@elif(B){{EL}}@branchreader()", new M { A = false, B = true }));
        }

        [Fact]
        public void P16_ElifAfterUnsatisfiedFalseStaysUnsatisfied()
        {
            Assert.Equal("UNSAT", Render("@if(A){{IF}}@elif(B){{EL}}@branchreader()", new M { A = false, B = false }));
        }

        // --- P17/P18: @elif with Satisfied==true renders nothing, leaves state unchanged (no write) ---

        [Fact]
        public void P17_ElifAfterSatisfiedTrueRendersNothingKeepsSat()
        {
            Assert.Equal("IFSAT", Render("@if(A){{IF}}@elif(B){{EL}}@branchreader()", new M { A = true, B = true }));
        }

        [Fact]
        public void P18_ElifAfterSatisfiedFalseRendersNothingKeepsSat()
        {
            Assert.Equal("IFSAT", Render("@if(A){{IF}}@elif(B){{EL}}@branchreader()", new M { A = true, B = false }));
        }

        // --- P20/P21: @else is terminal, clears the state (post-state NONE) ---

        [Fact]
        public void P20_ElseAfterUnsatisfiedRendersBodyAndClears()
        {
            Assert.Equal("ELNONE", Render("@if(A){{IF}}@else(){{EL}}@branchreader()", new M { A = false }));
        }

        [Fact]
        public void P21_ElseAfterSatisfiedRendersNothingAndClears()
        {
            Assert.Equal("IFNONE", Render("@if(A){{IF}}@else(){{EL}}@branchreader()", new M { A = true }));
        }

        // --- @ifnot + @else inversion ---

        [Fact]
        public void IfNotThenElseRespondsToIfNotSatisfaction()
        {
            Assert.Equal("x", Render("@ifnot(A){{x}}@else(){{y}}", new M { A = false }));
            Assert.Equal("y", Render("@ifnot(A){{x}}@else(){{y}}", new M { A = true }));
        }

        // --- Truthiness categories ---

        [Fact]
        public void TruthinessNullIsFalse()
        {
            Assert.Equal("UNSAT", Render("@if(O){{X}}@branchreader()", new M { O = null }));
        }

        [Fact]
        public void TruthinessNonBoolNonNullIsTrue()
        {
            Assert.Equal("XSAT", Render("@if(O){{X}}@branchreader()", new M { O = "anything" }));
        }

        [Fact]
        public void TruthinessTypedBoolNativeExpression()
        {
            // Phase 1 native tier: the condition is a bool-typed native expression.
            Assert.Equal("XSAT", Render("@if(Count > 3){{X}}@branchreader()", new M { Count = 5 }));
            Assert.Equal("UNSAT", Render("@if(Count > 3){{X}}@branchreader()", new M { Count = 1 }));
        }

        // --- Composition rows (N1 chained / N3 nested chain parameter) ---

        [Fact]
        public void ChainedBranchPublishesIntoEnclosingFrame()
        {
            // @if used as a chain item publishes into the enclosing body frame; a sibling reader sees it.
            // Phase 5 D13: the leftmost @out(A) (A ignored) is now HED5012 — use the bodiless @out().
            Assert.Equal("SAT", Render("@out():if(){{}}@branchreader()", new M { A = true }));
        }

        [Fact]
        public void NestedChainParameterBranchPublishes()
        {
            // A branch participant inside a nested chain parameter publishes into the enclosing frame.
            // Phase 5 D13: @out(branchdriver()) is now HED5012; the unnamed carrier runs the same nested chain
            // parameter (branchdriver renders empty, so the output is unchanged).
            Assert.Equal("SAT", Render("@(branchdriver())@branchreader()", new M { A = true }));
        }

        // --- Criterion 7: a custom publisher satisfies a set so a following @else stays silent ---

        [Fact]
        public void CustomPublisherSatisfiesSetSoElseIsSilent()
        {
            Assert.Equal("", Render("@branchdriver()@else(){{ELSE}}", new M()));
        }

        [Fact]
        public void CustomPublisherUnsatisfiedLetsElseRender()
        {
            // @if(false) leaves the set unsatisfied; @else then renders.
            Assert.Equal("ELSE", Render("@if(A){{IF}}@else(){{ELSE}}", new M { A = false }));
        }

        // --- P19 runtime column: orphan @else on a composition path throws (scan cannot see it) ---

        [Fact]
        public void RuntimeOrphanElseThrowsTemplateProcessingException()
        {
            HeddleTemplate.Configure(typeof(BranchProtocolTests).GetTypeInfo().Assembly);
            BranchTestExtensions.Register();
            // @else is not the leftmost item — it is a chained composition, invisible to the static scan.
            // Phase 5 D13: the leftmost @out(S) (S ignored) is now HED5012 — use the bodiless @out().
            var t = new HeddleTemplate("@out():else(){{E}}", new CompileContext(typeof(M)));
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            var ex = Assert.Throws<TemplateProcessingException>(() => t.Generate(new M { S = "x" }));
            Assert.Equal("'@else' has no matching '@if' in this scope.", ex.Message);
        }
    }
}
