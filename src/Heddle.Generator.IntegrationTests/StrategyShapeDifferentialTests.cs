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
    }
}
