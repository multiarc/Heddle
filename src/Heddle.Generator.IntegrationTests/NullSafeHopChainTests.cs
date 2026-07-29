using Heddle.Data;
using Heddle.Generator.IntegrationTests.Fixtures;
using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// A member path that walks through a null reference and then reads a member of the value it could not fetch.
    /// The engine substitutes <c>default(T)</c> at the hop that failed and keeps walking, so the read lands on that
    /// default; C#'s <c>?.</c> abandons the whole rest of the chain instead. The two answers are different values,
    /// and for <c>HasValue</c> they are both renderable — the divergence shows up as wrong output, not as an error.
    /// </summary>
    public class NullSafeHopChainTests
    {
        private const string Header =
            "@model(){{Heddle.Generator.IntegrationTests.Fixtures.NullableHopModel}}@\\\n";

        [Theory]
        [InlineData("Inner.Maybe.HasValue")]
        [InlineData("Inner.Maybe.Value")]
        [InlineData("Inner.When.Value.Year")]
        [InlineData("Inner.When.HasValue")]
        public void AHopThroughNullReadsTheMemberOfTheDefaultOnBothTiers(string path)
        {
            const string key = "views/nullable-hop-chain.heddle";
            var template = Header + "@(" + path + ")\n";

            // Two of these paths read `Value` off a default, which throws on the engine tier; the tiers have to agree
            // on that too, so each backend is invoked separately rather than as a tuple that dies on the first one.
            var backends = DifferentialHarness.DeferredWithOptions(
                key, template, typeof(NullableHopModel), new NullableHopModel(), new TemplateOptions());

            var precompiledError = Record.Exception(() => backends.precompiled());
            var dynamicError = Record.Exception(() => backends.dynamic());

            Assert.Equal(dynamicError?.GetType(), precompiledError?.GetType());
            Assert.Equal(dynamicError?.Message, precompiledError?.Message);
            if (dynamicError == null)
                Assert.Equal(backends.dynamic(), backends.precompiled());
        }

        /// <summary>A ref-struct hop has to be spelled against its receiver twice — it has no nullable form to widen
        /// into — and that receiver is itself a null-conditional chain here. Spelling that chain into the read half
        /// re-applies the propagation to a type that cannot carry it, which is a compile error in the generated
        /// source rather than a wrong answer.</summary>
        [Fact]
        public void ARefStructHopBehindAReferenceHopCompiles()
        {
            const string key = "views/nested-ref-struct.heddle";
            const string template =
                "@model(){{Heddle.Generator.IntegrationTests.Fixtures.NestedRefStructModel}}@(Inner.Buf.Length)";
            var model = new NestedRefStructModel { Inner = new RefStructModel() };

            var (precompiled, dyn) = DifferentialHarness.Render(key, template, typeof(NestedRefStructModel), model);

            Assert.Equal(dyn, precompiled);
            Assert.Equal("5", precompiled);
        }

        /// <summary>The same paths with the intermediate reference present: the value is there, so neither tier has
        /// to invent one. This is the half that was already green, and it has to stay that way — a fix that makes
        /// the null case agree by breaking the non-null case has traded one divergence for another.</summary>
        [Theory]
        [InlineData("Inner.Maybe.HasValue")]
        [InlineData("Inner.Maybe.Value")]
        [InlineData("Inner.When.Value.Year")]
        [InlineData("Inner.When.HasValue")]
        public void APresentValueRendersIdenticallyOnBothTiers(string path)
        {
            const string key = "views/nullable-hop-chain-present.heddle";
            var template = Header + "@(" + path + ")\n";
            var model = new NullableHopModel
            {
                Inner = new NullableHolder { Maybe = 41, When = new System.DateTime(2019, 3, 4) }
            };

            var (precompiled, dyn) = DifferentialHarness.Render(key, template, typeof(NullableHopModel), model);

            Assert.Equal(dyn, precompiled);
            Assert.Contains(path == "Inner.Maybe.HasValue" || path == "Inner.When.HasValue" ? "True"
                : path == "Inner.Maybe.Value" ? "41" : "2019", precompiled);
        }
    }
}
