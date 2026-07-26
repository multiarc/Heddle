using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// Generated strategy shape and value-path coercion rail; the runtime picks among four strategies while
    /// the generator emits one Render/Execute pair per body, both following the document-ordered alternation
    /// of literal pieces and processor calls. Value path coerces with <c>as string ?? string.Empty</c> per the
    /// joint-land rule: OutExtension-related changes must update this class consciously.
    /// </summary>
    public class StrategyShapeDifferentialTests
    {
        private const string Header = "@model(){{System.String}}@\\\n";

        /// <summary>A definition wrapping caller content with @out(); the one construct reaching Execute from render path.</summary>
        private const string WrapDefinition = "@%\n<wrap>{{<w>@out()</w>}}\n%@\n";

        private static string AssertParity(string key, string content, object model = null)
        {
            var gen = DifferentialHarness.Generate(new[] { (key, content) });
            DifferentialHarness.ExpectPrecompiled(gen, key);
            var (precompiled, dyn) = DifferentialHarness.Render(key, content, typeof(string), model ?? "hi");
            Assert.Equal(dyn, precompiled);
            return precompiled;
        }

        /// <summary>Runtime's DocumentStrategy short-circuit against single-piece return.</summary>
        [Fact]
        public void FullStaticBody()
        {
            Assert.Equal("just text", AssertParity("views/strategy-static-only.heddle",
                Header + "just text").Trim());
        }

        /// <summary>Single-part Execute against runtime's single-element strategy.</summary>
        [Fact]
        public void SingleProcessorBody()
        {
            Assert.Equal("hi", AssertParity("views/strategy-single-processor.heddle",
                Header + "@(this)").Trim());
        }

        /// <summary>Empty nested body returns string.Empty on both tiers.</summary>
        [Fact]
        public void EmptyNestedBody()
        {
            AssertParity("views/strategy-empty-body.heddle", Header + "[@if(this){{}}]");
        }

        /// <summary>Head/interleaved/tail piece ordering and the offset-walk contract.</summary>
        [Fact]
        public void HeadInterleavedAndTailOrdering()
        {
            Assert.Equal("A-hi-B-hi-C", AssertParity("views/strategy-alternation.heddle",
                Header + "A-@(this)-B-@(this)-C").Trim());
        }

        /// <summary>Zero-length pieces between adjacent processors must not diverge from GetDocumentPieces.</summary>
        [Fact]
        public void AdjacentProcessorsWithNoTextBetweenThem()
        {
            Assert.Equal("hihihi", AssertParity("views/strategy-adjacent-processors.heddle",
                Header + "@(this)@(this)@(this)").Trim());
        }

        /// <summary>
        /// Rail tripwire: value path coerces with <c>as string ?? string.Empty</c>, matching runtime's RuntimeDocument.
        /// The companion test pins render-path asymmetry: boxed non-strings are stringified on render, dropped on value path.
        /// </summary>
        [Fact]
        public void TheEmittedValuePathCarriesThePinnedCoercionRail()
        {
            const string key = "views/strategy-nonstring-value.heddle";
            var t = Header + "A-@(this)-B-@(this)-C";

            var gen = DifferentialHarness.Generate(new[] { (key, t) });
            DifferentialHarness.ExpectPrecompiled(gen, key);
            var source = Assert.Single(gen.TemplateSources).Value;

            Assert.Contains(".ProcessData(scope.Model(", source);
            Assert.Contains(") as string ?? string.Empty;", source);
            Assert.Contains("return string.Concat(", source);
        }

        /// <summary>Boxed int on the chained channel stringifies identically on both tiers.</summary>
        [Fact]
        public void BoxedNonStringOnTheChainedChannelStringifiesIdenticallyOnBothTiers()
        {
            var rendered = AssertParity("views/strategy-boxed-index.heddle",
                Header + "@for(2){{[@if(this){{@out()}}]}}");
            Assert.Equal("[0][1]", rendered.Trim());
        }

        /// <summary>
        /// Byte-level pin: one loop index (boxed int), two paths: render stringifies (R@if), value drops (V@wrap).
        /// Asserts absolute bytes AND tier equality so both tiers must be consciously edited together.
        /// </summary>
        [Fact]
        public void BoxedNonStringOnTheValuePathIsDroppedIdenticallyOnBothTiers()
        {
            var rendered = AssertParity("views/strategy-nonstring-value-bytes.heddle",
                Header + WrapDefinition + "@for(2){{R@if(this){{[@out()]}}V@wrap(){{[@out()|@if(this){{S}}]}}}}");
            Assert.Equal("R[0]V<w>[|S]</w>R[1]V<w>[|S]</w>", rendered);
        }

        /// <summary>
        /// Rail three-case shape drops non-strings identically in all RuntimeDocument strategies: single, optimized, normal.
        /// </summary>
        [Theory]
        [InlineData("single", "@out()", "<w></w><w></w>")]
        [InlineData("optimized", "@out()@if(this){{S}}", "<w>S</w><w>S</w>")]
        [InlineData("normal", "[@out()]", "<w>[]</w><w>[]</w>")]
        public void TheValuePathDropsTheNonStringInAllThreeCasesOfTheRail(string shape, string callerContent,
            string expected)
        {
            var rendered = AssertParity("views/strategy-nonstring-value-" + shape + ".heddle",
                Header + WrapDefinition + "@for(2){{@wrap(){{" + callerContent + "}}}}");
            Assert.Equal(expected, rendered);
        }
    }
}
