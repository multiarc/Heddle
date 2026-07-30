using Heddle.Generator.IntegrationTests.Fixtures;
using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// A host extension declaring a <c>[Prop]</c> whose type generated code may not spell. A definition's prop
    /// layout has to refuse one — it becomes a body's active layout, where every prop read is emitted through a
    /// cast to the declared type — and the same refusal was extended to extension layouts on the reasoning that
    /// the two are the same kind of thing.
    /// <para>They are not. Nothing on the extension-parameter path writes a prop type: the frozen prototype holds
    /// boxed values, the parameter-name field holds strings, the layout fingerprint is a manifest string, and the
    /// only conversion either the prototype or a dynamic setter emits spells a numeric C# keyword. So the refusal
    /// prevented nothing and cost a template that renders identically on both tiers — which is what these rows
    /// are here to keep saying.</para>
    /// </summary>
    public class ExtensionPropTypeNameTests
    {
        private const string CartType = "Heddle.Generator.IntegrationTests.Fixtures.Cart";

        /// <summary>Every way a prop value reaches the prototype: the declared default, a literal argument, and a
        /// non-constant argument that becomes a dynamic setter. The unnameable prop is present in all three.
        /// </summary>
        [Theory]
        [InlineData("default", "@badged(Name)", "[badge=none/size=2:n]\n")]
        [InlineData("literal", "@badged(Name, size: 7)", "[badge=none/size=7:n]\n")]
        [InlineData("setter", "@badged(Name, size: Count)", "[badge=none/size=4:n]\n")]
        public void AnExtensionPropTypeThisAssemblyCannotNameStillPrecompiles(string name, string call,
            string expected)
        {
            var key = "views/ext-prop-unnameable-" + name + ".heddle";
            var template = "@model(){{" + CartType + "}}@\\\n[" + call + "]\n";

            var (precompiled, dyn) = DifferentialHarness.Render(key, template, typeof(Cart),
                new Cart { Name = "n", Count = 4 });
            Assert.Equal(expected, dyn);
            Assert.Equal(dyn, precompiled);
        }

        /// <summary>
        /// The layout fingerprint travels to the runtime in the manifest, and the runtime recomputes it from the
        /// live extension type before it will serve a precompiled entry. An unnameable slot type has to round-trip
        /// through that string like any other, or the entry precompiles at build time and is thrown away at the
        /// first render — a degrade that no build-time gate would have reported.
        /// </summary>
        [Fact]
        public void TheLayoutFingerprintRoundTripsForAnUnnameablePropType()
        {
            const string key = "views/ext-prop-unnameable-resolver.heddle";
            var template = "@model(){{" + CartType + "}}@\\\n[@badged(Name, size: 7)]\n";

            var (precompiled, dyn) = DifferentialHarness.RenderViaResolver(key, template, typeof(Cart),
                new Cart { Name = "n" });
            Assert.Equal("[badge=none/size=7:n]\n", dyn);
            Assert.Equal(dyn, precompiled);
        }
    }
}
