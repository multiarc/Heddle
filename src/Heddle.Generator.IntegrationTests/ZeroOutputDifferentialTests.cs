using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// Verifies that <c>[ZeroOutput]</c> classification works identically on both the dynamic and precompiled
    /// compilation tiers, matching the runtime's behavioral rule (when a directive's <c>InitStart</c> returns
    /// <c>null</c>, that block is dropped).
    /// </summary>
    public class ZeroOutputDifferentialTests
    {
        private const string Header = "@model(){{System.String}}@\\\n";

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

        /// <summary>Built-in directives classify identically through the binder, maintaining byte-for-byte compatibility.</summary>
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
