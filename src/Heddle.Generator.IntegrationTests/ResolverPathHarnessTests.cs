using Heddle.Generator.IntegrationTests.Fixtures;
using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// Phase 0 WI2 (D2) — <see cref="DifferentialHarness.RenderViaResolver"/>'s own coverage: the WI8-shape template
    /// from <see cref="ResolverIntegrationTests"/> renders byte-identically to the dynamic engine through the full
    /// registration → resolver → per-request gauntlet → precompiled-adapter seam, in both sub-modes (registry-only and
    /// file-backed with the staleness check on). This is the seam no test crossed before this phase: the direct-invoke
    /// harness bypasses <c>Register</c>, <c>ConsultPrecompiled</c>, and every <c>PrecompiledGauntlet</c> check.
    /// </summary>
    [Collection("PrecompiledRegistry")]
    public class ResolverPathHarnessTests : PrecompiledRegistryTestBase
    {
        private const string CartType = "Heddle.Generator.IntegrationTests.Fixtures.Cart";

        private const string CartTemplate =
            "@model(){{" + CartType + "}}@\\\n" +
            "Cart @(Name): count @(Count), total @(Price * Quantity).\n" +
            "@if(IsFeatured){{ FEATURED }}\n";

        private static Cart Model() =>
            new Cart { Name = "Basket", Count = 3, Price = 2.5m, Quantity = 4, IsFeatured = true };

        [Fact]
        public void RegistryOnlyMode_RendersThroughTheGauntlet_ByteIdenticalToDynamic()
        {
            var (precompiled, dyn) = DifferentialHarness.RenderViaResolver(
                "views/phase0-registry-only.heddle", CartTemplate, typeof(Cart), Model());
            Assert.Equal(dyn, precompiled);
            Assert.Contains("FEATURED", precompiled);
        }

        [Fact]
        public void FileBackedMode_RendersThroughTheStalenessCheck_ByteIdenticalToDynamic()
        {
            var (precompiled, dyn) = DifferentialHarness.RenderViaResolver(
                "views/phase0-file-backed.heddle", CartTemplate, typeof(Cart), Model(), fileBacked: true);
            Assert.Equal(dyn, precompiled);
        }

        /// <summary>A model-less template with no <c>@model</c> directive still crosses the gauntlet — the sub-mode
        /// that the corpus sweep (WI4) uses for every entry.</summary>
        [Fact]
        public void ModelLessTemplate_CrossesTheGauntlet()
        {
            var (precompiled, dyn) = DifferentialHarness.RenderViaResolver(
                "views/phase0-static.heddle", "<p>hello</p>\n", typeof(object), null);
            Assert.Equal(dyn, precompiled);
        }
    }
}
