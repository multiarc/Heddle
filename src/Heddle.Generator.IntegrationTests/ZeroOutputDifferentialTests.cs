using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// Generator plan phase 1 WI9 (D10 / area 01 F17) — zero-output classification. The runtime's rule is
    /// behavioral (a directive's <c>InitStart</c> returns <c>null</c>, and that is what drops the block); the
    /// generator can only read symbols, so <c>[ZeroOutput]</c> is the declarative twin. Before it, the emitter
    /// classified zero-output by a hard-coded list of the four built-in directive names — so a CUSTOM zero-output
    /// extension had its block removed on the dynamic tier and kept as rendered output on the precompiled one.
    /// </summary>
    public class ZeroOutputDifferentialTests
    {
        private const string Header = "@model(){{System.String}}@\\\n";

        /// <summary>zero-output-custom — a <c>[ZeroOutput]</c> block mid-document is removed on both tiers.</summary>
        [Fact]
        public void CustomZeroOutputBlockIsRemovedOnBothTiers()
        {
            const string key = "views/zero-output-custom.heddle";
            var t = Header + "before@note(){{ignored payload}}after";

            var gen = DifferentialHarness.Generate(new[] { (key, t) });
            DifferentialHarness.ExpectPrecompiled(gen, key);

            var (precompiled, dyn) = DifferentialHarness.Render(key, t, typeof(string), "hi");
            Assert.Equal(dyn, precompiled);
            Assert.DoesNotContain("ignored payload", precompiled);
            Assert.Contains("before", precompiled);
            Assert.Contains("after", precompiled);
        }

        /// <summary>The built-ins classify identically through the binder — the byte-neutrality half of D10.</summary>
        [Fact]
        public void BuiltInDirectivesStillClassifyAsZeroOutput()
        {
            const string key = "views/zero-output-builtin.heddle";
            var t = "@model(){{System.String}}@\\\n@using(){{System.Text}}@\\\nbefore@profile(){{text}}after";

            var gen = DifferentialHarness.Generate(new[] { (key, t) });
            DifferentialHarness.ExpectPrecompiled(gen, key);

            var (precompiled, dyn) = DifferentialHarness.Render(key, t, typeof(string), "hi");
            Assert.Equal(dyn, precompiled);
            Assert.Equal("beforeafter", precompiled.Trim());
        }
    }
}
