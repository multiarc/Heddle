using System.Linq;
using Heddle.Data;
using Heddle.Language;
using Heddle.Runtime;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// The body model-typing table, now data (was scattered prose). This suite pins observable typing (run-tier conformance);
    /// the emitter's branches use the same rows (build-tier conformance).
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

        // Each row predicts observable output; a changed row makes the test red.

        private const string PersonHeader = "@model(){{Heddle.Tests.BodyModelRuleTableTests+Person}}@\\\n";

        /// <summary>Renders or returns <c>null</c> on compile failure (e.g., reading a nonexistent member).</summary>
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

        /// <summary>Body column prediction: Parent-typed bodies bind the enclosing model; others fail.
        /// Probe reads Name (only on Person) to discriminate @list from @for/@branch rows.</summary>
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

        /// <summary>Chained column prediction: Int32Index makes @out() splice the iteration index;
        /// None means @out() splices nothing.</summary>
        [Fact]
        public void TheChainedColumnPredictsWhatOutSplicesInsideAForBody()
        {
            Assert.True(BodyModelRules.TryGet("for", out _, out var chained));
            var expected = chained == ChainedModelSource.Int32Index ? "012" : string.Empty;
            Assert.Equal(expected, Render(PersonHeader + "@for(3){{@out()}}", new Person { Name = "Ada" }));
        }

        /// <summary>Branch rows' Chained column: None predicts empty @out().</summary>
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

        /// <summary>Observed typing: @if reads enclosing model (Parent); @list reads element (ElementOfData).</summary>
        [Fact]
        public void ObservedTypingMatchesTheParentAndElementRows()
        {
            var model = new Person { Name = "Ada", Scores = new[] { 7, 9 } };

            Assert.Equal("Ada", Render("@model(){{" + typeof(Person).FullName + "}}@\\\n@if(Name){{@(Name)}}", model));
            Assert.Equal("79", Render("@model(){{" + typeof(Person).FullName + "}}@\\\n@list(Scores){{@(this)}}", model));
        }

        /// <summary>@for chained row: body sees enclosing model, @out() splices iteration index.</summary>
        [Fact]
        public void ObservedForTypingMatchesTheParentPlusIndexRow()
        {
            var model = new Person { Name = "Ada" };
            Assert.Equal("Ada0Ada1Ada2",
                Render("@model(){{" + typeof(Person).FullName + "}}@\\\n@for(3){{@(Name)@out()}}", model));
        }
    }
}
