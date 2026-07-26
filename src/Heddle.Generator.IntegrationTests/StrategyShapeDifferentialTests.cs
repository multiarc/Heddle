using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// The generated strategy shape and the value-path coercion rail, pinned as differentials because the two sides
    /// cannot share the code: the runtime picks among four strategies (including a full-static short-circuit and a
    /// single-processor fast path), while the generator emits one <c>Render</c>/<c>Execute</c> pair per body. Those are
    /// optimization twins of the same contract, and every byte of every template flows through them.
    /// <para>The contract: a body is a document-ordered alternation of literal pieces and processor calls — head piece,
    /// interleaved processors, tail piece; the render path writes pieces straight to the sink; the value path coerces
    /// every processor result with <c>as string ?? string.Empty</c> and concatenates in document order, with the
    /// empty/single/concat three-case shape.</para>
    /// <para><b>Joint-land rule.</b> <c>OutExtension</c> flags a planned change to the non-string rail. When it ships,
    /// the runtime rail, the emitted <c>Execute</c> shape and the spec text move in ONE landing — and
    /// <see cref="TheEmittedValuePathCarriesThePinnedCoercionRail"/> is the tripwire that landing must consciously edit.
    /// Both tiers currently implement the <c>as string ?? string.Empty</c> rail.</para>
    /// </summary>
    public class StrategyShapeDifferentialTests
    {
        private const string Header = "@model(){{System.String}}@\\\n";

        /// <summary>
        /// A definition whose body splices its caller content with <c>@out()</c>. A definition call's caller content
        /// is the one construct that reaches a body's <c>Execute</c> — i.e. the value path — from the render path on
        /// BOTH tiers (<c>DefinitionBaseExtension.RenderData</c> calls <c>GetInnerResult</c>; the emitter binds the
        /// same body as <c>callerContent:</c>). Every other route into the value path is a <c>:</c> chain, and the
        /// emitter degrades every multi-item chain to the dynamic tier.
        /// </summary>
        private const string WrapDefinition = "@%\n<wrap>{{<w>@out()</w>}}\n%@\n";

        private static string AssertParity(string key, string content, object model = null)
        {
            var gen = DifferentialHarness.Generate(new[] { (key, content) });
            DifferentialHarness.ExpectPrecompiled(gen, key);
            var (precompiled, dyn) = DifferentialHarness.Render(key, content, typeof(string), model ?? "hi");
            Assert.Equal(dyn, precompiled);
            return precompiled;
        }

        /// <summary>strategy-static-only — the runtime's <c>DocumentStrategy</c> short-circuit against the
        /// generated single-piece return.</summary>
        [Fact]
        public void FullStaticBody()
        {
            Assert.Equal("just text", AssertParity("views/strategy-static-only.heddle",
                Header + "just text").Trim());
        }

        /// <summary>The single-part <c>Execute</c> (no <c>string.Concat</c>) against the runtime's single-element strategy.</summary>
        [Fact]
        public void SingleProcessorBody()
        {
            Assert.Equal("hi", AssertParity("views/strategy-single-processor.heddle",
                Header + "@(this)").Trim());
        }

        /// <summary>An empty nested body returns <c>string.Empty</c> on both tiers.</summary>
        [Fact]
        public void EmptyNestedBody()
        {
            AssertParity("views/strategy-empty-body.heddle", Header + "[@if(this){{}}]");
        }

        /// <summary>Head piece / interleaved processors / tail piece ordering, the offset-walk contract itself.</summary>
        [Fact]
        public void HeadInterleavedAndTailOrdering()
        {
            Assert.Equal("A-hi-B-hi-C", AssertParity("views/strategy-alternation.heddle",
                Header + "A-@(this)-B-@(this)-C").Trim());
        }

        /// <summary>Zero-length pieces between adjacent processors must not produce an empty-piece divergence
        /// between <c>GetDocumentPieces</c> and the emitted shape.</summary>
        [Fact]
        public void AdjacentProcessorsWithNoTextBetweenThem()
        {
            Assert.Equal("hihihi", AssertParity("views/strategy-adjacent-processors.heddle",
                Header + "@(this)@(this)@(this)").Trim());
        }

        /// <summary>
        /// <b>The rail tripwire</b>. The value path coerces every processor result with <c>ProcessData(scope) as string ?? string.Empty</c>:
        /// the runtime's strategies do it in <c>RuntimeDocument</c>, the generator emits the identical expression into every <c>Execute</c>.
        /// The coercion is asserted against the emitted source, because it is the emitted <em>shape</em> the joint-land rule constrains.
        /// <para>The companion render-path row below pins the observable asymmetry: a boxed non-string (the <c>@for</c> index a non-slot
        /// <c>@out()</c> returns) is stringified by the render path and dropped by the value path. Both tiers currently agree; when
        /// the rail changes, both of these rows must be edited together.</para>
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
            // The three-case Execute shape: multi-part bodies concatenate in document order.
            Assert.Contains("return string.Concat(", source);
        }

        /// <summary>The render-path companion: a boxed <see cref="int"/> on the chained channel is stringified
        /// identically on both tiers, pinned so the rail change must confront the asymmetry.</summary>
        [Fact]
        public void BoxedNonStringOnTheChainedChannelStringifiesIdenticallyOnBothTiers()
        {
            var rendered = AssertParity("views/strategy-boxed-index.heddle",
                Header + "@for(2){{[@if(this){{@out()}}]}}");
            Assert.Equal("[0][1]", rendered.Trim());
        }

        /// <summary>
        /// <b>The byte-level pin of the rail asymmetry</b>. The two rows above pin the rail as emitted <em>shape</em>
        /// and the render path as bytes; neither drives a boxed non-string through the <em>value</em> path, so the drop
        /// itself was pinned nowhere on either tier.
        /// <para>One template, one <c>@for</c> body, two readers of the <b>same</b> chained value (the loop index, a boxed
        /// <see cref="int"/>): <c>R@if(this){{[@out()]}}</c> reached on the render path (stringifies via <c>OutExtension.RenderData</c>)
        /// and <c>V@wrap(){{[@out()|@if(this){{S}}]}}</c> the caller content of a definition call (evaluated through the body's
        /// <c>Execute</c>, drops via <c>as string ?? string.Empty</c>).</para>
        /// <para>The assertion is the absolute expected bytes <b>and</b> tier equality. Tier equality alone would stay green if
        /// the joint-land rule's landing changed both tiers together — which is exactly when this row must be edited consciously.</para>
        /// </summary>
        [Fact]
        public void BoxedNonStringOnTheValuePathIsDroppedIdenticallyOnBothTiers()
        {
            var rendered = AssertParity("views/strategy-nonstring-value-bytes.heddle",
                Header + WrapDefinition + "@for(2){{R@if(this){{[@out()]}}V@wrap(){{[@out()|@if(this){{S}}]}}}}");
            Assert.Equal("R[0]V<w>[|S]</w>R[1]V<w>[|S]</w>", rendered);
        }

        /// <summary>
        /// The rail's three-case shape drops the non-string in every one of them, on both tiers. Each row lands on a
        /// <em>different</em> one of <c>RuntimeDocument</c>'s strategies, which are byte-equivalent optimizations of
        /// the one rail rather than a second rail. That claim was untested for a non-string value:
        /// <list type="bullet">
        /// <item><c>{{@out()}}</c> — one processor, no pieces: <c>SingleStrategy</c> against the emitted
        /// single-part <c>Execute</c> (no <c>string.Concat</c>).</item>
        /// <item><c>{{@out()@if(this){{S}}}}</c> — two adjacent processors covering the whole body:
        /// <c>OptimizedStrategy</c> against the emitted <c>string.Concat</c>.</item>
        /// <item><c>{{[@out()]}}</c> — processors interleaved with literal pieces: <c>NormalStrategy</c>, whose
        /// <c>?? element.Piece</c> fallback is part of the same rail. A processor element carries a null <c>Piece</c>,
        /// so the fallback must reach <c>string.Empty</c> and not the body text.</item>
        /// </list>
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
