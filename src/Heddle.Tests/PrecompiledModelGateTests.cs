using System;
using System.Collections.Generic;
using Heddle.Data;
using Heddle.Exceptions;
using Heddle.Runtime;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// The model-type gate the precompiled adapter used to skip. The dynamic tier refuses a model value its scope
    /// type cannot hold, as a Heddle fault with a message that names both types; the adapter bound a generated
    /// strategy and checked nothing, so the same wrong model reached the generated cast and came back as a raw
    /// <see cref="InvalidCastException"/> — or, for a body that reads no member, rendered a page the dynamic tier
    /// refuses outright. The manifest has always carried the type the generated code was compiled against; these
    /// pin that it is now the gate, and that it gates exactly what the dynamic tier gates and nothing more.
    /// </summary>
    public class PrecompiledModelGateTests
    {
        private sealed class TextStrategy : IProcessStrategy
        {
            public string Execute(in Scope scope) => "x";
            public void Render(in Scope scope) => scope.Renderer.Render("x");
        }

        private class Base { }

        private sealed class Derived : Base { }

        private static HeddleTemplate Adapter(Type modelType) =>
            new HeddleTemplate(new TextStrategy(), modelType: modelType);

        /// <summary>The divergence itself: the two tiers now answer a wrong model with the same fault, carrying the
        /// same sentence. Asserted against the dynamic tier's own message rather than a copy of it, so a reworded
        /// engine message cannot leave the adapter behind.</summary>
        [Fact]
        public void AWrongModelIsRefusedByTheAdapterTheWayTheDynamicTierRefusesIt()
        {
            const string document = "static\n";
            var dynamicTier = new HeddleTemplate(document, new CompileContext(new TemplateOptions(), typeof(Base)));
            var expected = Assert.Throws<TemplateProcessingException>(() => dynamicTier.Generate("wrong"));

            var adapter = Adapter(typeof(Base));
            var actual = Assert.Throws<TemplateProcessingException>(() => adapter.Generate("wrong"));

            Assert.Equal(expected.Message, actual.Message);
            Assert.Contains(typeof(Base).FullName, actual.Message);
            Assert.Contains(typeof(string).FullName, actual.Message);
        }

        /// <summary>A model the entry's type <em>can</em> hold still renders — the gate is instance-of, not
        /// identity, which is the relation the generated cast performs and the one the dynamic tier applies.</summary>
        [Fact]
        public void AnAssignableModelStillRenders()
        {
            Assert.Equal("x", Adapter(typeof(Base)).Generate(new Derived()));
            Assert.Equal("x", Adapter(typeof(Base)).Generate(new Base()));
        }

        /// <summary>A constructed generic is judged by its full identity: <c>List&lt;string&gt;</c> is not a
        /// <c>List&lt;int&gt;</c>, and a tier that compared definitions would serve one for the other.</summary>
        [Fact]
        public void AConstructedGenericIsJudgedByItsTypeArgumentsToo()
        {
            var adapter = Adapter(typeof(List<int>));
            Assert.Equal("x", adapter.Generate(new List<int> { 1 }));
            Assert.Throws<TemplateProcessingException>(() => Adapter(typeof(List<int>)).Generate(new List<string>()));
        }

        /// <summary>An untyped entry records <c>object</c>, which admits every value, and a hand-written manifest may
        /// name no type at all. Neither is a gate, and neither may cost the render path a check.</summary>
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void AnUntypedEntryGatesNothing(bool declaresObject)
        {
            var adapter = Adapter(declaresObject ? typeof(object) : null);
            Assert.Equal("x", adapter.Generate("anything"));
            Assert.Equal("x", adapter.Generate(42));
        }

        /// <summary>A null model stays legal on both tiers: there is no value to judge, and the dynamic tier has
        /// always skipped its own check for one.</summary>
        [Fact]
        public void ANullModelIsStillLegal()
        {
            Assert.Equal("x", Adapter(typeof(Base)).Generate(null));
        }
    }
}
