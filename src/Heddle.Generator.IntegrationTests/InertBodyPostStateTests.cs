using System;
using System.Linq;
using Heddle;
using Heddle.Data;
using Heddle.Runtime;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// Post-state 2 of <c>AbstractExtension.InitSubTemplate</c> on the build tier: a call whose body text
    /// <b>compiles to no processors</b> installs no strategy at all, so the hook's <c>InnerExist</c> is false and
    /// the shaped body text is the inert <c>_innerResult</c> — which is exactly what the engine leaves behind when
    /// <c>RuntimeDocument.Empty</c> makes it discard the document it just built. The emitter handed every bodied
    /// call a real <c>BodyN</c> strategy, so <c>@raw(){{mid}}</c> rendered its body on this tier and its model on
    /// the other.
    /// <para>Emptiness is not "the body text holds no <c>@</c>": <c>HeddleCompiler.CompileBody</c> adds a document
    /// element only for a chain whose composed <c>returnTypeChainedPrevious</c> is non-null, so a body of nothing
    /// but directives and a body whose only chain composes to nothing are empty too. Each way of being empty has
    /// its own case below.</para>
    /// <para>Every case is tier-pinned: fallback is byte-identical by design, so an unpinned comparison would
    /// prove nothing about the tier that produced the bytes.</para>
    /// </summary>
    public class InertBodyPostStateTests
    {
        private static string RenderDynamic(string content, object model)
        {
            var t = new HeddleTemplate(content, new CompileContext(new TemplateOptions(), new ExType(typeof(string))));
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            return t.Generate(model);
        }

        private static void AssertBytes(string key, string body, object value, string expected)
        {
            var template = "@model(){{System.String}}@\\\n" + body + "\n";
            var gen = DifferentialHarness.Generate(new[] { (key, template) });
            Assert.False(gen.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error),
                "Unexpected generator error: " + string.Join("; ", gen.Diagnostics.Select(d => d.ToString())));
            DifferentialHarness.ExpectPrecompiled(gen, key);

            var (pre, dyn) = DifferentialHarness.Render(key, template, typeof(string), value);
            Assert.Equal(dyn, pre);
            Assert.Equal(expected, pre);
            Assert.Equal(expected, RenderDynamic(template, value));
        }

        /// <summary>The reproducer: a static-only body on the raw carrier is inert, so the carrier renders its
        /// model. It rendered the body text on this tier before post-state 2 was emitted.</summary>
        [Fact]
        public void AStaticOnlyBodyOnTheRawCarrierRendersTheModel()
            => AssertBytes("views/inert-raw.heddle", "<x>@raw(){{mid}}</x>", "Hi", "<x>Hi</x>\n");

        /// <summary>The unnamed carrier's own form of the same shape — the call the emitter used to refuse
        /// outright (<c>RefusalCategory.ChainCarrier</c>, "bodied unnamed carrier"), for this byte and no other
        /// reason.</summary>
        [Fact]
        public void AStaticOnlyBodyOnTheUnnamedCarrierRendersTheModel()
            => AssertBytes("views/inert-unnamed.heddle", "<x>@(){{mid}}</x>", "Hi", "<x>Hi</x>\n");

        /// <summary>A bodied unnamed carrier whose body does hold a processor keeps post-state 3: the carrier
        /// rescopes and renders the body, which is what makes the retirement a capability rather than a
        /// blanket.</summary>
        [Fact]
        public void ALiveBodyOnTheUnnamedCarrierStillRendersTheBody()
            => AssertBytes("views/live-unnamed.heddle", "<x>@(){{[@()]}}</x>", "Hi", "<x>[Hi]</x>\n");

        /// <summary>A body of nothing but a directive is empty too: shaping removes the block and the compiler's
        /// document-building loop adds no element for a chain whose composed return type is null.</summary>
        [Fact]
        public void ABodyOfNothingButDirectivesIsInert()
            => AssertBytes("views/inert-directives.heddle", "<x>@raw(){{@profile(){{text}}}}</x>", "Hi",
                "<x>Hi</x>\n");

        /// <summary>A body whose only content is a <b>chain</b> that composes to nothing: <c>@note</c> is a custom
        /// <c>[ZeroOutput]</c> extension, so the chain's leftmost item returns null from its hook and the whole
        /// element is dropped even though the item to its right produced a value.</summary>
        [Fact]
        public void ABodyWhoseOnlyChainComposesToNothingIsInert()
            => AssertBytes("views/inert-chain.heddle", "<x>@raw(){{@note():string(this)}}</x>", "Hi",
                "<x>Hi</x>\n");

        /// <summary>Post-state 1 is not post-state 2, and the raw body text is what separates them: a call with no
        /// body at all keeps its own arm.</summary>
        [Fact]
        public void ABodilessCallKeepsPostStateOne()
            => AssertBytes("views/inert-bodiless.heddle", "<x>@raw()</x>", "Hi", "<x>Hi</x>\n");

        /// <summary>The other half of post-state 1: an <b>empty</b> body text. The engine never compiles a
        /// document for it, so the inert inner result is the raw text rather than a shaped one — and this tier
        /// must not collapse the two states into one.</summary>
        [Fact]
        public void AnEmptyBodyTextKeepsPostStateOne()
            => AssertBytes("views/inert-emptybody.heddle", "<x>@raw(){{}}</x>", "Hi", "<x>Hi</x>\n");

        /// <summary>Post-state 3 is untouched: one real call in the body is a document with a processor, so the
        /// strategy is installed and the carrier renders the body.</summary>
        [Fact]
        public void ABodyWithOneRealCallStillInstallsItsStrategy()
            => AssertBytes("views/inert-live.heddle", "<x>@raw(){{[@()]}}</x>", "Hi", "<x>[Hi]</x>\n");

        /// <summary>A pure-literal body on a bodied built-in. <c>@if</c> reads its inner result rather than
        /// <c>InnerExist</c>, so the bytes were already identical — what changes is which of the engine's three
        /// post-states produced them, and the inert inner result has to carry the same text the strategy
        /// wrote.</summary>
        [Fact]
        public void APureLiteralBodyOnABodiedBuiltInStillRendersItsText()
            => AssertBytes("views/inert-if.heddle", "<x>@if(true){{plain}}</x>", "Hi", "<x>plain</x>\n");

        /// <summary>A pure-literal body on a <b>custom</b> extension, read through <c>GetInnerResult</c> with no
        /// <c>InnerExist</c> guard: <c>@bellow</c> steps back to its body when its value is null, so the rendered
        /// bytes are the inert <c>_innerResult</c> itself. This is what pins that the shaped text the site carries
        /// is the text a real body compile would have written back.</summary>
        [Fact]
        public void APureLiteralBodyOnACustomExtensionIsReadFromTheInertInnerResult()
            => AssertBytes("views/inert-bellow.heddle", "<x>@bellow(){{loud}}</x>", null, "<x>LOUD</x>\n");

        /// <summary>A definition body of static text alone. The inner carrier renders its inert inner result where
        /// it used to run a piece-only strategy — the same bytes, now through the engine's own post-state.</summary>
        [Fact]
        public void AStaticOnlyDefinitionBodyRendersThroughItsInertInnerResult()
            => AssertBytes("views/inert-defbody.heddle", "@% <lbl>{{hello}} %@\n<x>@lbl()</x>", "Hi",
                "<x>hello</x>\n");

        /// <summary>Static-only caller content. The outer carrier's inner result is what the callee's
        /// <c>@out()</c> splices, so the definition's own projection reads it back.</summary>
        [Fact]
        public void StaticOnlyCallerContentStillReachesTheCalleesProjection()
            => AssertBytes("views/inert-callercontent.heddle", "@% <box>{{[@out()]}} %@\n<x>@box(){{static}}</x>",
                "Hi", "<x>[static]</x>\n");
    }
}
