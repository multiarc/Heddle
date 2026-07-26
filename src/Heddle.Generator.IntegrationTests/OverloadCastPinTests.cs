using Heddle.Generator.IntegrationTests.Fixtures;
using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// Phase 4 D10/WI8, added by the phase-4 audit (2026-07-26) to close a **surviving mutant**: deleting
    /// cast-pinning outright — <c>DefaultFunctionBinder</c> emitting every resolved built-in call with no argument
    /// casts at all — reddened **zero** tests across all three suites. D10's whole mechanism ("emit the call with
    /// explicit casts to the chosen overload's parameter types, which pins the consumer's C# compiler to the same
    /// overload by making it an exact match") was therefore unpinned: the ranker's *refusals* were covered by the
    /// overload-tie fixture, but nothing observed that a resolved call carries the pin.
    /// <para>The mutant is <i>currently</i> extensionally harmless for the shipped built-in table — WI10 measured
    /// 0 of 480 argument combinations binding to a different overload under C# betterness than under Heddle's flat
    /// rank — but that is a property of today's first-party signatures, not of the mechanism, and phase 3 routed
    /// host <c>[ExportFunctions]</c> overload sets (arbitrary signatures, where WI10's measurement explicitly does
    /// not carry) through the same <c>ArgumentCasts</c> path. So the pin asserts the emitted text.</para>
    /// </summary>
    public class OverloadCastPinTests
    {
        private const string CartType = "Heddle.Generator.IntegrationTests.Fixtures.Cart";

        private static string Template(string expression) =>
            "@model(){{" + CartType + "}}@\\\nvalue: @(" + expression + ")\n";

        /// <summary>The flat rank picks <c>Min(long, long)</c> for <c>min(1, 2L)</c> — <c>(int, long)</c> ranks
        /// <c>(1, 0)</c> and dominates <c>(double, double)</c>/<c>(decimal, decimal)</c> at <c>(1, 1)</c> — so the
        /// non-exact argument must reach the consumer's compiler already cast to <c>long</c>. Without the cast the
        /// consumer's compiler re-runs its own betterness over the same candidate set; with it, there is no choice
        /// left to make, which is what makes the "by construction" claim true instead of merely asserted.</summary>
        [Fact]
        public void AResolvedCallIsEmittedCastPinnedToTheChosenSignature()
        {
            const string key = "overload/cast-pin.heddle";
            var content = Template("min(1, 2L)");
            var gen = DifferentialHarness.Generate(new[] { (key, content) });
            DifferentialHarness.ExpectPrecompiled(gen, key);
            var source = string.Join("\n", gen.TemplateSources.Values);

            // The non-exact argument carries the winning signature's parameter type; the exact one does not need it.
            Assert.Contains("(long)(1)", source);
            Assert.Contains("PrecompiledFunctions.Min((long)(1), 2L)", source);

            var (precompiled, dyn) = DifferentialHarness.Render(key, content, typeof(Cart), new Cart());
            Assert.Equal(dyn, precompiled);
        }

        /// <summary>An all-exact call needs no pin, and must not grow a redundant one — the negative half, so the
        /// assertion above cannot be satisfied by casting everything unconditionally.</summary>
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

        /// <summary>A widening on both sides: <c>min(Count, Price)</c> is <c>(int, decimal)</c>, so the winning
        /// <c>(decimal, decimal)</c> row pins both arguments — and the rendered bytes are the runtime's, which is
        /// the property the cast is there to preserve when the conversion itself changes the value's type.</summary>
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
