using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// <para><b>The body-hosting bind must derive its render type, never hard-code <c>Raw</c>.</b>
    /// <c>AllocateBodyExtension</c> writes one <c>PrecompiledRuntime.Bind</c> call per body-hosting extension
    /// call site, and the <c>RenderType</c> it passes is what the runtime turns into <c>DirectRender</c>. It was
    /// hard-coded <c>RenderType.Raw</c> for every such site, which is invisible while only <c>@if</c>,
    /// <c>@for</c> and <c>@list</c> reach it — none of them carries an encoding attribute — and becomes
    /// unescaped markup the moment an <c>[EncodeOutput]</c> extension hosts a body: the dynamic tier escapes
    /// the body's output, the precompiled tier hands it through raw. Same template, same model, two different
    /// documents, and the difference is a cross-site scripting hole on the tier that exists to be faster, not
    /// different.</para>
    /// <para>This suite pins that property on its own, over the engine's own <c>@string</c> — an
    /// <c>[EncodeOutput]</c> <c>AbstractHtmlExtension</c> whose body typing the shared table already carries, so
    /// nothing here depends on any build property. Both halves are
    /// asserted, because either alone can be satisfied by accident: the rendered bytes on both tiers, with the
    /// tier pinned (a fallback is byte-identical by design, so an unpinned render proves nothing), and the
    /// emitted bind itself, which stays readable when a later refactor changes which extension can host a body
    /// at all.</para>
    /// <para>The render type is no longer a literal the build writes: the call site runs the extension's own
    /// <c>InitStart</c> through <c>PrecompiledRuntime.Init</c>, which derives it from the <b>live</b> type's
    /// attributes exactly as <c>HeddleCompiler.InitializeTemplate</c> does. So the source-level half of this guard
    /// asserts that the site goes through that seam and bakes no render type at all — which is a stronger property
    /// than the literal it replaces, because a build that mis-derives it can no longer exist.</para>
    /// </summary>
    public class BodyHostEncodingGuardTests
    {
        private const string Key = "views/body-host-encoding.heddle";

        /// <summary>Body text carrying the three characters an HTML encoder exists to stop.</summary>
        private const string RawBody = "a&b<c>d";

        private const string EncodedBody = "a&amp;b&lt;c&gt;d";

        private const string Template = "@model(){{System.String}}@\\\n@string(this){{" + RawBody + "}}\n";

        /// <summary>The rendered proof. The body is what <c>@string</c> renders when the model is null, and it
        /// must come back entity-encoded on the precompiled tier exactly as it does on the dynamic one.</summary>
        [Fact]
        public void ABodyHostedByAnEncodeOutputExtensionIsEncodedOnThePrecompiledTierToo()
        {
            var gen = DifferentialHarness.Generate(new[] { (Key, Template) });
            Assert.False(gen.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error),
                "Unexpected generator error: " + string.Join("; ", gen.Diagnostics.Select(d => d.ToString())));
            DifferentialHarness.ExpectPrecompiled(gen, Key);

            var (precompiled, dynamic) = DifferentialHarness.Render(Key, Template, typeof(string), null);

            Assert.Equal(dynamic, precompiled);
            Assert.Equal(EncodedBody + "\n", precompiled);
            Assert.DoesNotContain(RawBody, precompiled);
        }

        /// <summary>The emitted-source proof, belt to the render assertion's braces: the body-hosting call site
        /// constructs the extension behind a factory and hands it to <c>PrecompiledRuntime.Init</c>, which runs the
        /// extension's own hook and derives the render type off the live type — so no render type is written here
        /// at all, and there is no literal left to be wrong.</summary>
        [Fact]
        public void TheBodyHostingSiteRunsTheExtensionsOwnHookAndBakesNoRenderType()
        {
            var gen = DifferentialHarness.Generate(new[] { (Key, Template) });
            DifferentialHarness.ExpectPrecompiled(gen, Key);

            var source = string.Join("\n", gen.TemplateSources.Values);
            const string init = "PrecompiledRuntime.Init(";
            var start = source.IndexOf(init, StringComparison.Ordinal);
            Assert.True(start >= 0,
                "No Init-bound site for @string in the generated source:\n" + source);

            var end = source.IndexOf(");", start, StringComparison.Ordinal);
            Assert.True(end > start, "Unterminated Init call in the generated source:\n" + source);
            var allocation = source.Substring(start, end - start);

            Assert.Contains("() => new global::Heddle.Extensions.StringExtension()", allocation);
            Assert.DoesNotContain("RenderType.", allocation);
        }
    }
}
