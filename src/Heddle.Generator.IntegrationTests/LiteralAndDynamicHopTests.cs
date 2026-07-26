using System.Linq;
using Heddle.Generator.IntegrationTests.Fixtures;
using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// Phase 4 WI7/WI9 differential entries: the lone-surrogate literal that used to be written raw into generated
    /// source (04 F9), and the dynamic member hop that used to bind in the <b>consumer's</b> assembly context
    /// instead of <c>Heddle</c>'s (04 F8 / OQ3).
    /// </summary>
    public class LiteralAndDynamicHopTests
    {
        private const string CartType = "Heddle.Generator.IntegrationTests.Fixtures.Cart";

        [Fact]
        public void LoneSurrogateStringLiteral_IsEscapedInSource_AndRendersIdentically()
        {
            // The template carries the escape; the decoder produces an unpaired code unit; the formatter must put an
            // escape back rather than writing the raw code unit into a .g.cs file.
            var key = "views/lone-surrogate.heddle";
            var content = "@model(){{" + CartType + "}}@\\\n@(\"a\\ud800b\")\n";
            var gen = DifferentialHarness.Generate(new[] { (key, content) });
            DifferentialHarness.ExpectPrecompiled(gen, key);
            var source = string.Join("\n", gen.TemplateSources.Values);
            Assert.Contains("\\ud800", source);
            Assert.DoesNotContain("\uD800", source);

            var (precompiled, dyn) = DifferentialHarness.Render(key, content, typeof(Cart), new Cart());
            Assert.Equal(dyn, precompiled);
        }

        [Fact]
        public void LoneSurrogateCharLiteral_IsEscapedInSource_AndRendersIdentically()
        {
            var key = "views/lone-surrogate-char.heddle";
            var content = "@model(){{" + CartType + "}}@\\\n@('\\ud800')\n";
            var gen = DifferentialHarness.Generate(new[] { (key, content) });
            DifferentialHarness.ExpectPrecompiled(gen, key);
            Assert.Contains("'\\ud800'", string.Join("\n", gen.TemplateSources.Values));

            var (precompiled, dyn) = DifferentialHarness.Render(key, content, typeof(Cart), new Cart());
            Assert.Equal(dyn, precompiled);
        }

        [Fact]
        public void DynamicHop_RoutesThroughTheRuntimeHelper_NotAnInlineDynamicCast()
        {
            // The shape change D11 gates on the schema bump: one binder context for both tiers, chosen once.
            var key = "views/dyn-hop.heddle";
            var content = "@model(){{dynamic}}@\\\n@(Name) @(Nested.Amount)\n";
            var gen = DifferentialHarness.Generate(new[] { (key, content) });
            DifferentialHarness.ExpectPrecompiled(gen, key);
            var source = string.Join("\n", gen.TemplateSources.Values);
            Assert.Contains("global::Heddle.Precompiled.PrecompiledRuntime.DynamicMember(m, \"Name\")", source);
            Assert.Contains(
                "global::Heddle.Precompiled.PrecompiledRuntime.DynamicMember(global::Heddle.Precompiled.PrecompiledRuntime.DynamicMember(m, \"Nested\"), \"Amount\")",
                source);
            Assert.DoesNotContain("(dynamic)", source);
            Assert.Contains("schemaVersion: 5", gen.ManifestSource);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("present")]
        public void DynamicHop_RendersIdenticallyOnBothTiers(string name)
        {
            var key = "views/dyn-hop-render.heddle";
            var content = "@model(){{dynamic}}@\\\n[@(Name)][@(Nested.Amount)]\n";
            var model = name == null
                ? new Cart { Name = null, Nested = null }
                : new Cart { Name = name, Nested = new Nested { Amount = 7 } };
            var (precompiled, dyn) = DifferentialHarness.Render(key, content, typeof(object), model);
            Assert.Equal(dyn, precompiled);
        }
    }
}
