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
    /// Differential coverage for a built-in <c>@out()</c> that carries its own body. The generator refuses to
    /// precompile a bodied <c>@out</c> (<c>TemplateEmitter.BuildOutCall</c>: <c>!string.IsNullOrEmpty(ParameterTemplate)</c>
    /// → reason "bodied @out" → <c>null</c>), so any template containing one <b>falls back to the dynamic tier</b>. This
    /// pins that documented tier fallback (no precompiled strategy is emitted) — which is what keeps the precompiled and
    /// runtime backends in lockstep for the <c>@out</c> double-render fix (both render through the dynamic engine) — and
    /// asserts the runtime renders the corrected output: because a non-slot <c>@out</c> is a value emitter, a static-only
    /// (no dynamic <c>@</c> content) body is inert, so <c>@out</c> emits the chained value alone, never the chained value
    /// AND the inert static body.
    /// </summary>
    public class OutStaticBodyFallbackTests
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
        // @for threads the index on the chained channel; the inert static @out() body must NOT also render (was "[0D][1D][2D]").
        [InlineData("@for(3){{[@out(){{D}}]}}", "x", "[0][1][2]\n")]
        // A top-level static-body @out with no chained value: the inert body is dropped (was "[BODY]").
        [InlineData("[@out(){{BODY}}]", "x", "[]\n")]
        public void BodiedOut_FallsBackToDynamicTier_AndRendersCorrectly(string body, string value, string expected)
        {
            var template = "@model(){{System.String}}@\\\n" + body + "\n";

            var gen = DifferentialHarness.Generate(new[] { ("views/bodied-out.heddle", template) });
            Assert.False(gen.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error),
                "Unexpected generator error: " + string.Join("; ", gen.Diagnostics.Select(d => d.ToString())));

            // Documented tier fallback: a bodied @out does not precompile — no bound strategy for it.
            Assert.False(IsPrecompiled(gen.ManifestSource ?? string.Empty, "views/bodied-out.heddle"),
                "Expected a dynamic-tier fallback (bodied @out), but the template precompiled.");

            // The runtime backend renders the corrected output: no chained-value-plus-inert-body double-render.
            Assert.Equal(expected, RenderDynamic(template, value));
        }
    }
}
