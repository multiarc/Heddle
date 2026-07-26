using System.Linq;
using Heddle.Data;
using Heddle.Language;
using Heddle.Runtime;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// Generator plan phase 1 WI10 (D12) — the body model-typing table. Until this phase the rule existed only as
    /// prose comments on each emission branch, so a change to (say) <c>ListExtension</c>'s element-type derivation
    /// silently kept the old generator typing. The table is now data; this suite is the run-tier half of its
    /// conformance (the build-tier half asserts the emitter's branches cite the same rows), driven end-to-end
    /// through real renders rather than through the resolvers, so it is the observable typing that is pinned.
    /// </summary>
    public class BodyModelRuleTableTests
    {
        private sealed class Person
        {
            public string Name { get; set; }
            public int[] Scores { get; set; }
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

        [Theory]
        [InlineData("if")]
        [InlineData("ifnot")]
        [InlineData("elif")]
        [InlineData("elseif")]
        [InlineData("else")]
        public void TheBranchTrioIsTypedByTheParentModel(string name)
        {
            Assert.True(BodyModelRules.TryGet(name, out var body, out var chained));
            Assert.Equal(BodyModelSource.Parent, body);
            Assert.Equal(ChainedModelSource.None, chained);
        }

        [Fact]
        public void ForIsParentModelPlusABoxedIndex()
        {
            Assert.True(BodyModelRules.TryGet("for", out var body, out var chained));
            Assert.Equal(BodyModelSource.Parent, body);
            Assert.Equal(ChainedModelSource.Int32Index, chained);
        }

        [Fact]
        public void ListIsTypedByTheElementOfItsData()
        {
            Assert.True(BodyModelRules.TryGet("list", out var body, out var chained));
            Assert.Equal(BodyModelSource.ElementOfData, body);
            Assert.Equal(ChainedModelSource.None, chained);
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
