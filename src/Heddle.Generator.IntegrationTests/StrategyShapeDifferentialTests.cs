using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// Generator plan phase 1 WI12 (D13 / area 01 F2) — the generated strategy shape and the value-path coercion
    /// rail, pinned as differentials because the two sides cannot share the code: the runtime picks among four
    /// strategies (including a full-static short-circuit and a single-processor fast path), while the generator
    /// emits one <c>Render</c>/<c>Execute</c> pair per body. Those are optimization twins of the same contract,
    /// and every byte of every template flows through them.
    /// <para>The contract, restated (the normative text lives in the phase spec): a body is a document-ordered
    /// alternation of literal pieces and processor calls — head piece, interleaved processors, tail piece; the
    /// render path writes pieces straight to the sink; the value path coerces every processor result with
    /// <c>as string ?? string.Empty</c> and concatenates in document order, with the empty/single/concat three-case
    /// shape.</para>
    /// <para><b>Joint-land rule (Q1.2).</b> <c>OutExtension</c> flags a planned change to the non-string rail.
    /// When it ships, the runtime rail, the emitted <c>Execute</c> shape and the spec text move in ONE landing —
    /// and <see cref="TheEmittedValuePathCarriesThePinnedCoercionRail"/> is the tripwire that landing must
    /// consciously edit. Authoring-time verification found no present mismatch: both tiers implement the
    /// <c>as string ?? string.Empty</c> rail today.</para>
    /// </summary>
    public class StrategyShapeDifferentialTests
    {
        private const string Header = "@model(){{System.String}}@\\\n";

        /// <summary>
        /// The Q8.13 vehicle: a definition whose body splices its caller content with <c>@out()</c>. A definition
        /// call's caller content is the one construct that reaches a body's <c>Execute</c> — i.e. the value path —
        /// from the render path on BOTH tiers (<c>DefinitionBaseExtension.RenderData</c> calls
        /// <c>GetInnerResult</c>; the emitter binds the same body as <c>callerContent:</c>). Every other route into
        /// the value path is a <c>:</c> chain, and the emitter degrades every multi-item chain to the dynamic tier,
        /// so a chained fixture would compare the dynamic engine against itself.
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

        /// <summary>strategy-single-processor — the single-part <c>Execute</c> (no <c>string.Concat</c>) against
        /// the runtime's single-element strategy.</summary>
        [Fact]
        public void SingleProcessorBody()
        {
            Assert.Equal("hi", AssertParity("views/strategy-single-processor.heddle",
                Header + "@(this)").Trim());
        }

        /// <summary>strategy-empty-body — an empty nested body returns <c>string.Empty</c> on both tiers.</summary>
        [Fact]
        public void EmptyNestedBody()
        {
            AssertParity("views/strategy-empty-body.heddle", Header + "[@if(this){{}}]");
        }

        /// <summary>strategy-alternation — head piece / interleaved processors / tail piece ordering, i.e. the
        /// offset-walk contract itself.</summary>
        [Fact]
        public void HeadInterleavedAndTailOrdering()
        {
            Assert.Equal("A-hi-B-hi-C", AssertParity("views/strategy-alternation.heddle",
                Header + "A-@(this)-B-@(this)-C").Trim());
        }

        /// <summary>strategy-adjacent-processors — zero-length pieces between adjacent processors must not produce
        /// an empty-piece divergence between <c>GetDocumentPieces</c> and the emitted shape.</summary>
        [Fact]
        public void AdjacentProcessorsWithNoTextBetweenThem()
        {
            Assert.Equal("hihihi", AssertParity("views/strategy-adjacent-processors.heddle",
                Header + "@(this)@(this)@(this)").Trim());
        }

        /// <summary>
        /// strategy-nonstring-value — <b>the rail tripwire</b>. The value path coerces every processor result with
        /// <c>ProcessData(scope) as string ?? string.Empty</c>: the runtime's strategies do it in
        /// <c>RuntimeDocument</c>, the generator emits the identical expression into every <c>Execute</c>. The
        /// coercion is asserted against the emitted source, because it is the emitted <em>shape</em> the joint-land
        /// rule constrains — a rail change that touched only the runtime would leave this text standing.
        /// <para>The companion render-path row below pins the observable asymmetry the rail change is about: a
        /// boxed non-string (the <c>@for</c> index a non-slot <c>@out()</c> returns) is stringified by the render
        /// path and dropped by the value path. Both tiers agree today, which is the "no present mismatch" finding
        /// Q1.2's ruling asked for; when the rail changes, both of these rows must be edited together.</para>
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

        /// <summary>The render-path companion: a boxed <see cref="int"/> on the chained channel is stringified,
        /// identically on both tiers. Pinned so the rail change has to confront the asymmetry rather than
        /// discover it.</summary>
        [Fact]
        public void BoxedNonStringOnTheChainedChannelStringifiesIdenticallyOnBothTiers()
        {
            var rendered = AssertParity("views/strategy-boxed-index.heddle",
                Header + "@for(2){{[@if(this){{@out()}}]}}");
            Assert.Equal("[0][1]", rendered.Trim());
        }

        /// <summary>
        /// strategy-nonstring-value-bytes — <b>the byte-level pin of the normative §4 asymmetry</b> (Q8.13). The two
        /// rows above pin the rail as emitted <em>shape</em> and the render path as bytes; neither drives a boxed
        /// non-string through the <em>value</em> path, so the drop itself — the observable half of §4 — was pinned
        /// nowhere on either tier.
        /// <para>One template, one <c>@for</c> body, two readers of the <b>same</b> chained value (the loop index,
        /// a boxed <see cref="int"/>):</para>
        /// <list type="bullet">
        /// <item><c>R@if(this){{[@out()]}}</c> — reached on the <b>render path</b>, so
        /// <c>OutExtension.RenderData</c> stringifies it: <c>[0]</c>, <c>[1]</c>.</item>
        /// <item><c>V@wrap(){{[@out()|@if(this){{S}}]}}</c> — the caller content of a definition call, which both
        /// tiers evaluate through the body's <c>Execute</c>, so the <b>value path</b>'s
        /// <c>as string ?? string.Empty</c> drops it: <c>[|S]</c>, with nothing where the index was.</item>
        /// </list>
        /// <para>Because the two readers are siblings in one body they see the identical chained object, so the
        /// empty is provably the rail dropping a live non-<see cref="string"/> value and not a null or an unreached
        /// call. The <c>|@if(this){{S}}</c> arm is the control in the other direction: on the same value path, a
        /// <see cref="string"/>-returning processor and the literal pieces around it are carried, so a rail that
        /// dropped everything could not pass either.</para>
        /// <para>The assertion is the absolute expected bytes <b>and</b> tier equality (via
        /// <see cref="AssertParity"/>). Tier equality alone would stay green if the joint-land rule's landing
        /// changed both tiers together — which is exactly the moment this row must be edited consciously.</para>
        /// </summary>
        [Fact]
        public void BoxedNonStringOnTheValuePathIsDroppedIdenticallyOnBothTiers()
        {
            var rendered = AssertParity("views/strategy-nonstring-value-bytes.heddle",
                Header + WrapDefinition + "@for(2){{R@if(this){{[@out()]}}V@wrap(){{[@out()|@if(this){{S}}]}}}}");
            Assert.Equal("R[0]V<w>[|S]</w>R[1]V<w>[|S]</w>", rendered);
        }

        /// <summary>
        /// The rail's three-case shape (§3) drops the non-string in every one of them, on both tiers — and on the
        /// runtime side each row lands on a <em>different</em> one of <c>RuntimeDocument</c>'s strategies, which §3
        /// calls byte-equivalent optimizations of the one rail rather than a second rail. That claim was untested for
        /// a non-string value:
        /// <list type="bullet">
        /// <item><c>{{@out()}}</c> — one processor, no pieces: <c>SingleStrategy</c> against the emitted
        /// single-part <c>Execute</c> (no <c>string.Concat</c>).</item>
        /// <item><c>{{@out()@if(this){{S}}}}</c> — two adjacent processors covering the whole body:
        /// <c>OptimizedStrategy</c> against the emitted <c>string.Concat</c>.</item>
        /// <item><c>{{[@out()]}}</c> — processors interleaved with literal pieces: <c>NormalStrategy</c>, whose
        /// <c>?? element.Piece</c> fallback §3 names as part of the same rail. A processor element carries a null
        /// <c>Piece</c>, so the fallback must reach <c>string.Empty</c> and not the body text.</item>
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
