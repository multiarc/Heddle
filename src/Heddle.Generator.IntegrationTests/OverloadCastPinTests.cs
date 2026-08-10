using Heddle.Generator.IntegrationTests.Fixtures;
using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// Cast-pinning of resolved calls: emits argument casts to ensure the consumer's compiler chooses the selected overload
    /// (a surviving mutant with no test coverage — critical for arbitrary host <c>[ExportFunctions]</c> signatures).
    /// </summary>
    public class OverloadCastPinTests
    {
        private const string CartType = "Heddle.Generator.IntegrationTests.Fixtures.Cart";

        private static string Template(string expression) =>
            "@model(){{" + CartType + "}}@\\\nvalue: @(" + expression + ")\n";

        /// <summary>
        /// Non-exact arguments must be cast to the selected signature's parameter types; the exact argument does not.
        /// </summary>
        [Fact]
        public void AResolvedCallIsEmittedCastPinnedToTheChosenSignature()
        {
            const string key = "overload/cast-pin.heddle";
            var content = Template("min(1, 2L)");
            var gen = DifferentialHarness.Generate(new[] { (key, content) });
            DifferentialHarness.ExpectPrecompiled(gen, key);
            var source = string.Join("\n", gen.TemplateSources.Values);

            // Non-exact argument cast to parameter type; exact argument remains uncast.
            Assert.Contains("(long)(1)", source);
            Assert.Contains("PrecompiledFunctions.Min((long)(1), 2L)", source);

            var (precompiled, dyn) = DifferentialHarness.Render(key, content, typeof(Cart), new Cart());
            Assert.Equal(dyn, precompiled);
        }

        /// <summary>Exact-match calls have no casts (negative case: proves selective casting).</summary>
        [Fact]
        public void AnAllExactCallCarriesNoCasts()
        {
            const string key = "overload/cast-pin-exact.heddle";
            var content = Template("min(1, 2)");
            var gen = DifferentialHarness.Generate(new[] { (key, content) });
            DifferentialHarness.ExpectPrecompiled(gen, key);
            var source = string.Join("\n", gen.TemplateSources.Values);

            Assert.Contains("PrecompiledFunctions.Min(1, 2)", source);

            var (precompiled, dyn) = DifferentialHarness.Render(key, content, typeof(Cart), new Cart());
            Assert.Equal(dyn, precompiled);
        }

        /// <summary>Both arguments cast when neither is exact (preserves type when conversion changes the value).</summary>
        [Fact]
        public void BothArgumentsArePinnedWhenNeitherIsExact()
        {
            const string key = "overload/cast-pin-both.heddle";
            var content = Template("min(Count, Price)");
            var gen = DifferentialHarness.Generate(new[] { (key, content) });
            DifferentialHarness.ExpectPrecompiled(gen, key);
            var source = string.Join("\n", gen.TemplateSources.Values);
            Assert.Contains("(decimal)(", source);

            var (precompiled, dyn) = DifferentialHarness.Render(key, content, typeof(Cart),
                new Cart { Count = 3, Price = 2.5m });
            Assert.Equal(dyn, precompiled);
        }
    }
}
