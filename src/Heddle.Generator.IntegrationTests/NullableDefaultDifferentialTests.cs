using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// The prop-default conversion alignment. The emitter's <c>DefaultConvertible</c> is the HED5009 twin of the
    /// runtime's <c>PropConversion.CanConvertTypes</c>; a row the emitter is missing is a safe over-<em>refusal</em>
    /// (the template falls back rather than mis-renders). These fixtures assert the enabling half: the template
    /// precompiles natively AND its rendered bytes, including the boxed CLR type of the stored default, equal the
    /// dynamic tier's.
    /// </summary>
    public class NullableDefaultDifferentialTests
    {
        private const string Header = "@model(){{System.String}}@\\\n";

        /// <summary>Constant default on a nullable prop must be widened and boxed correctly (int default on long?).</summary>
        [Fact]
        public void NullableWideningDefaultPrecompilesAndReproducesTheBoxedType()
        {
            const string key = "views/props-nullable-widen-default.heddle";
            var t = Header + "@nullableLiftDefault(this)";

            var gen = DifferentialHarness.Generate(new[] { (key, t) });
            DifferentialHarness.ExpectPrecompiled(gen, key);

            var (precompiled, dyn) = DifferentialHarness.Render(key, t, typeof(string), "hi");
            Assert.Equal(dyn, precompiled);
            Assert.Contains("n=5/Int64:hi", precompiled);
        }

        /// <summary>Constant default on nullable prop with matching type (int default on int?).</summary>
        [Fact]
        public void NullableIdentityDefaultPrecompilesAndMatches()
        {
            const string key = "views/props-nullable-identity-default.heddle";
            var t = Header + "@nullableNarrow(this)";

            var gen = DifferentialHarness.Generate(new[] { (key, t) });
            DifferentialHarness.ExpectPrecompiled(gen, key);

            var (precompiled, dyn) = DifferentialHarness.Render(key, t, typeof(string), "hi");
            Assert.Equal(dyn, precompiled);
        }
    }
}
