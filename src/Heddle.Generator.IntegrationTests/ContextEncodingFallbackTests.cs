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
    /// C2-R7 / C2-R8 — the precompile-vs-fallback finding for <c>@attr</c>/<c>@js</c>/<c>@url</c>. Each mirrors
    /// <c>@string</c>'s value-call shape, which means it overrides <c>InitStart</c> (the step-back default body). The
    /// generator refuses to bind engine extensions that override a compile-time hook (it reproduces only the base
    /// behavior), so — exactly like custom branch sets — a template using these built-ins <b>falls back to the
    /// dynamic tier</b> rather than precompiling. This suite pins the documented fallback (no precompiled strategy is
    /// emitted for the template) and asserts the runtime backend renders the escaping correctly.
    /// </summary>
    public class ContextEncodingFallbackTests
    {
        private static bool IsPrecompiled(string manifest, string key)
        {
            var marker = "key: \"" + key + "\"";
            var at = manifest?.IndexOf(marker, StringComparison.Ordinal) ?? -1;
            if (at < 0)
                return false; // no manifest entry -> full dynamic fallback
            var next = manifest.IndexOf("key: \"", at + marker.Length, StringComparison.Ordinal);
            var block = next < 0 ? manifest.Substring(at) : manifest.Substring(at, next - at);
            return !block.Contains("strategy: null"); // strategy: null == marker (not precompiled)
        }

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
        public void ContextEncoder_FallsBackToDynamicTier_AndRendersCorrectly(string call, string value, string expected)
        {
            var template = "@model(){{System.String}}@\\\n" + call + "\n";

            var gen = DifferentialHarness.Generate(new[] { ("views/ctx.heddle", template) });
            Assert.False(gen.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error),
                "Unexpected generator error: " + string.Join("; ", gen.Diagnostics.Select(d => d.ToString())));

            // Documented fallback (C2-R7): the template does not precompile — no bound strategy for it.
            Assert.False(IsPrecompiled(gen.ManifestSource ?? string.Empty, "views/ctx.heddle"),
                "Expected a dynamic-tier fallback (InitStart-overriding value-call), but the template precompiled.");

            // The runtime backend renders the escaping — the fallback is output-safe.
            Assert.Equal(expected + "\n", RenderDynamic(template, value));
        }
    }
}
