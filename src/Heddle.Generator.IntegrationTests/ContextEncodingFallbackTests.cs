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
    /// The step-back encoders (<c>@string</c>/<c>@attr</c>/<c>@js</c>/<c>@url</c>) override <c>InitStart</c>, but
    /// the override re-types only the DEFAULT BODY — so a BODILESS call carries exactly the base bind, and the
    /// emitter precompiles it from pinned knowledge (<c>ExtensionBinder.Info.HasPinnedStepBackHook</c>). Pins both
    /// halves of that contract: bodiless calls emit and render byte-identically across the tiers (null model
    /// included), and a BODIED call — whose body typing is the hook's business — still degrades to the dynamic tier.
    /// </summary>
    public class ContextEncodingFallbackTests
    {
        private static string RenderDynamic(string content, string model)
        {
            var t = new HeddleTemplate(content, new CompileContext(new TemplateOptions(), new ExType(typeof(string))));
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            return t.Generate(model);
        }

        [Theory]
        [InlineData("@attr(this)", "a&b<c>\"d'", "a&amp;b&lt;c&gt;&quot;d&#39;")]
        [InlineData("@js(this)", "</script>&\"", "\\u003C/script>\\u0026\\\"")]
        [InlineData("@url(this)", "a b&c", "a%20b%26c")]
        // @string carries no [EncodeOutput] of its own, so the default (Html) profile encodes its output.
        [InlineData("@string(this)", "a&b<c>", "a&amp;b&lt;c&gt;")]
        public void StepBackEncoder_Bodiless_PrecompilesByteIdentically(string call, string value, string expected)
        {
            var template = "@model(){{System.String}}@\\\n" + call + "\n";

            var (precompiled, dynamic) = DifferentialHarness.Render("views/ctx.heddle", template,
                typeof(string), value);

            Assert.Equal(dynamic, precompiled);
            Assert.Equal(expected + "\n", precompiled);
        }

        [Theory]
        [InlineData("@attr(this)")]
        [InlineData("@js(this)")]
        [InlineData("@url(this)")]
        [InlineData("@string(this)")]
        public void StepBackEncoder_NullValue_PrecompilesByteIdentically(string call)
        {
            // The null-value arm exercises the encoders' inner-result path (no body: empty), on both tiers.
            var template = "@model(){{System.String}}@\\\n" + call + "\n";

            var (precompiled, dynamic) = DifferentialHarness.Render("views/ctx-null.heddle", template,
                typeof(string), null);

            Assert.Equal(dynamic, precompiled);
            Assert.Equal("\n", precompiled);
        }

        [Fact]
        public void StepBackEncoder_Bodied_StillFallsBackToDynamicTier()
        {
            // The default body is exactly what the hook re-types (it compiles against the CALLER's scope, not the
            // call value) — the emitter has no pinned emission for that, so the bodied form stays a safe degrade.
            var template = "@model(){{System.String}}@\\\n@attr(this){{fallback}}\n";

            var gen = DifferentialHarness.Generate(new[] { ("views/ctx-bodied.heddle", template) });
            Assert.False(gen.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error),
                "Unexpected generator error: " + string.Join("; ", gen.Diagnostics.Select(d => d.ToString())));

            DifferentialHarness.ExpectDegrade(gen, "views/ctx-bodied.heddle");
            // The dynamic tier renders the body (against the caller's scope) when the value is null.
            Assert.Equal("fallback\n", RenderDynamic(template, null));
            Assert.Equal("x&amp;y\n", RenderDynamic(template, "x&y"));
        }
    }
}
