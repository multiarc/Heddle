using Heddle.Generator.IntegrationTests.Fixtures;
using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>Templates render byte-identically to dynamic engine through the registration → resolver → gauntlet → adapter seam, in both sub-modes (registry-only and file-backed).</summary>
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

        /// <summary>Model-less templates (no <c>@model</c> directive) cross the gauntlet, as corpus processing requires.</summary>
        [Fact]
        public void ModelLessTemplate_CrossesTheGauntlet()
        {
            var (precompiled, dyn) = DifferentialHarness.RenderViaResolver(
                "views/phase0-static.heddle", "<p>hello</p>\n", typeof(object), null);
            Assert.Equal(dyn, precompiled);
        }
    }
}
