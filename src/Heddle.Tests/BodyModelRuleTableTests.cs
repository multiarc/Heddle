using System.Linq;
using Heddle.Data;
using Heddle.Language;
using Heddle.Runtime;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// The body model-typing table. The rule used to live only as prose comments on each emission branch, so a
    /// change to (say) <c>ListExtension</c>'s element-type derivation silently kept the old generator typing. The
    /// table is now data; this suite is the run-tier half of its conformance (the build-tier half asserts the
    /// emitter's branches use the same rows), driven end-to-end through real renders rather than through the
    /// resolvers, so it is the observable typing that is pinned.
    /// </summary>
    public class BodyModelRuleTableTests
    {
        private sealed class Person
        {
            public string Name { get; set; }
            public int[] Scores { get; set; }
            /// <summary>Always null, so a branch on it is falsy — the continuation/terminal probes need one.</summary>
            public string Other { get; set; }
        }

        private static string Render(string template, object model) =>
            new HeddleTemplate(template, new CompileContext(new TemplateOptions(), model?.GetType()))
                .Generate(model);

        [Fact]
        public void EveryPinnedNameIsARegisteredExtension()
        {
            var registered = BodyModelRules.PinnedNames.Where(TemplateFactory.Exists).ToList();
            Assert.Equal(BodyModelRules.PinnedNames.OrderBy(n => n), registered.OrderBy(n => n));
        }

        // The rows are predictions about observable output: each row computes the expected rendered text FROM the row,
        // so a changed row makes the prediction wrong and the test red.

        private const string PersonHeader = "@model(){{Heddle.Tests.BodyModelRuleTableTests+Person}}@\\\n";

        /// <summary>Renders, or returns <c>null</c> when the template does not compile — which is what a body read
        /// of a member the body's model does not have amounts to.</summary>
        private static string TryRender(string template, object model)
        {
            try
            {
                return Render(template, model);
            }
            catch (Exceptions.TemplateCompileException)
            {
                return null;
            }
        }

        /// <summary>The <c>Body</c> column, as a prediction: a body typed by <see cref="BodyModelSource.Parent"/>
        /// can bind the ENCLOSING model's members; a body typed any other way cannot, and the read fails to compile.
        /// Every probe body reads <c>Name</c>, which only the enclosing <see cref="Person"/> has — so the row alone
        /// decides the expected outcome, and <c>@list</c>'s <c>ElementOfData</c> row (element type <c>int</c>, no
        /// <c>Name</c>) is discriminated from the branch/<c>@for</c> rows' <c>Parent</c>.</summary>
        [Theory]
        [InlineData("if", "@if(Name){{@(Name)}}", "Ada")]
        [InlineData("ifnot", "@ifnot(Other){{@(Name)}}", "Ada")]
        [InlineData("elif", "@if(Other){{x}}@elif(Name){{@(Name)}}", "Ada")]
        [InlineData("elseif", "@if(Other){{x}}@elseif(Name){{@(Name)}}", "Ada")]
        [InlineData("else", "@if(Other){{x}}@else(){{@(Name)}}", "Ada")]
        [InlineData("for", "@for(1){{@(Name)}}", "Ada")]
        [InlineData("list", "@list(Scores){{@(Name)}}", "Ada")]
        public void TheBodyColumnPredictsWhetherTheBodySeesTheEnclosingModel(string name, string template,
            string whenParent)
        {
            Assert.True(BodyModelRules.TryGet(name, out var body, out _));
            var expected = body == BodyModelSource.Parent ? whenParent : null;
            Assert.Equal(expected,
                TryRender(PersonHeader + template, new Person { Name = "Ada", Scores = new[] { 7 } }));
        }

        /// <summary>The <c>Chained</c> column, as a prediction: <c>@for</c>'s
        /// <see cref="ChainedModelSource.Int32Index"/> is what makes a non-slot <c>@out()</c> inside its body splice
        /// the boxed iteration index. <see cref="ChainedModelSource.None"/> would mean nothing host-specific on the
        /// chained channel, so <c>@out()</c> would have nothing to splice — which is what this asserts instead when
        /// the row changes. Without this the column had no consumer anywhere: it could be flipped freely.</summary>
        [Fact]
        public void TheChainedColumnPredictsWhatOutSplicesInsideAForBody()
        {
            Assert.True(BodyModelRules.TryGet("for", out _, out var chained));
            var expected = chained == ChainedModelSource.Int32Index ? "012" : string.Empty;
            Assert.Equal(expected, Render(PersonHeader + "@for(3){{@out()}}", new Person { Name = "Ada" }));
        }

        /// <summary>The branch rows' <c>Chained</c> column, same treatment: <c>None</c> predicts that a branch body's
        /// <c>@out()</c> splices nothing.</summary>
        [Theory]
        [InlineData("if")]
        [InlineData("else")]
        public void TheBranchRowsChainedColumnPredictsAnEmptyOut(string name)
        {
            Assert.True(BodyModelRules.TryGet(name, out _, out var chained));
            var template = name == "if" ? "@if(Name){{[@out()]}}" : "@if(Other){{x}}@else(){{[@out()]}}";
            var rendered = Render(PersonHeader + template, new Person { Name = "Ada" });
            Assert.Equal(chained == ChainedModelSource.None ? "[]" : "[0]", rendered);
        }

        [Fact]
        public void ACustomOrUnknownNameDeclaresNoPinnedTyping()
        {
            Assert.False(BodyModelRules.TryGet("yell", out _, out _));
            Assert.False(BodyModelRules.TryGet(null, out _, out _));
        }

        /// <summary>The observed typing, end-to-end: an <c>@if</c> body reads the ENCLOSING model's members
        /// (Parent), while an <c>@list</c> body reads the ELEMENT's (ElementOfData) — the two rows whose confusion
        /// would silently change which member the emitted C# binds.</summary>
        [Fact]
        public void ObservedTypingMatchesTheParentAndElementRows()
        {
            var model = new Person { Name = "Ada", Scores = new[] { 7, 9 } };

            Assert.Equal("Ada", Render("@model(){{" + typeof(Person).FullName + "}}@\\\n@if(Name){{@(Name)}}", model));
            Assert.Equal("79", Render("@model(){{" + typeof(Person).FullName + "}}@\\\n@list(Scores){{@(this)}}", model));
        }

        /// <summary>The <c>@for</c> row's chained half: the body sees the enclosing model, and <c>@out()</c>
        /// splices the boxed iteration index off the chained channel.</summary>
        [Fact]
        public void ObservedForTypingMatchesTheParentPlusIndexRow()
        {
            var model = new Person { Name = "Ada" };
            Assert.Equal("Ada0Ada1Ada2",
                Render("@model(){{" + typeof(Person).FullName + "}}@\\\n@for(3){{@(Name)@out()}}", model));
        }
    }
}
