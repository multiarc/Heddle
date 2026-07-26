using System.Linq;
using Heddle.Data;
using Heddle.Generator.IntegrationTests.Fixtures;
using Heddle.Runtime;
using Microsoft.CSharp.RuntimeBinder;
using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// Differential entries: the lone-surrogate literal that used to be written raw into generated
    /// source, and the dynamic member hop that used to bind in the <b>consumer's</b> assembly context
    /// instead of <c>Heddle</c>'s.
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
            // The shape change gates on the schema bump: one binder context for both tiers, chosen once.
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
            // Asked of the constant, not of a literal: this test is about the routing shape being gated on the schema
            // that introduced it, never about a particular number. Written as "schemaVersion: 5" it went stale the
            // moment the unreleased schemas 3–5 were collapsed into one.
            Assert.Contains(
                "schemaVersion: " + Heddle.Precompiled.PrecompiledSchema.DynamicMemberRoutingSchemaVersion,
                gen.ManifestSource);
            Assert.True(
                Heddle.Precompiled.PrecompiledSchema.CurrentSchemaVersion >=
                Heddle.Precompiled.PrecompiledSchema.DynamicMemberRoutingSchemaVersion,
                "DynamicMember routing is emitted below the schema that introduced it.");
        }

        /// <summary>
        /// An <b>internal</b> property of a *consumer-assembly* type reached through a dynamic hop. This is a
        /// different accessibility situation from <c>DynamicMemberTests</c>' unit pin over a
        /// <c>Heddle.Tests</c>-internal type, and it is the one the divergence lived in: generated code sitting in a
        /// third assembly.
        /// <para>Formerly the generated <c>(dynamic)</c> cast chain bound in the <i>consumer's</i> context and read
        /// <c>Secret</c> happily while the runtime tier could not see it at all: one tier rendered a value, the other
        /// failed. Both tiers now route through one binder context, so both fail identically. Asserted through the
        /// deferred form because the byte-identical tuple short-circuits on the first backend's throw and would hide
        /// whether the second behaves the same.</para>
        /// </summary>
        [Fact]
        public void InternalPropertyDynamicHop_ReachesTheSameVerdictOnBothTiers()
        {
            const string key = "views/dyn-internal.heddle";
            const string content = "@model(){{dynamic}}@\\\n[@(Secret)]\n";
            var model = new Order { Secret = "s" };
            var (precompiled, dyn) = DifferentialHarness.DeferredWithOptions(key, content, typeof(object), model,
                new TemplateOptions());

            var precompiledFailure = Record.Exception(() => precompiled());
            var dynamicFailure = Record.Exception(() => dyn());

            Assert.NotNull(precompiledFailure);
            Assert.NotNull(dynamicFailure);
            Assert.IsType<RuntimeBinderException>(Unwrap(precompiledFailure));
            Assert.IsType<RuntimeBinderException>(Unwrap(dynamicFailure));

            // And the typed tier still accepts the same getter — a deliberate asymmetry between the typed and dynamic
            // tiers. If a future harmonization changes either half, this row moves with it.
            var typed = new HeddleTemplate("[@(Secret)]",
                new CompileContext(new TemplateOptions(), typeof(Order)));
            Assert.True(typed.CompileResult.Success, typed.CompileResult.ToString());
            Assert.Equal("[s]", typed.Generate(model));
        }

        private static System.Exception Unwrap(System.Exception e) =>
            e is System.Reflection.TargetInvocationException tie && tie.InnerException != null
                ? tie.InnerException
                : e;

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
