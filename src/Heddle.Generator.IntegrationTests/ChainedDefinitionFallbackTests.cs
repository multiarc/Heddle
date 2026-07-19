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
    /// Differential coverage for chaining a value INTO a definition call (<c>@box():producer()</c>). The generator
    /// refuses to precompile any multi-item chain (<c>TemplateEmitter.BuildCall</c>: <c>chain.Chain.Count != 1</c> →
    /// "chained call"), so such a template <b>falls back to the dynamic tier</b>. This pins that documented fallback
    /// (no precompiled strategy is emitted) — which is what keeps the precompiled and runtime backends in lockstep
    /// for the chained-into-definition fix (both render through the dynamic engine) — and asserts the runtime renders
    /// the corrected output: the chained value reaches the definition body's <c>@out()</c>.
    /// </summary>
    public class ChainedDefinitionFallbackTests
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
        // A value chained into a definition: box's @out() emits it.
        [InlineData("@% <box>{{[@out()]}} %@\n@box():string(this)", "Hi", "[Hi]\n")]
        // The documented @heading():emphasis() wrapper chain, valued through a string producer.
        [InlineData("@% <heading>{{<h2>@out()</h2>}} <emphasis>{{<em>@out()</em>}} %@\n@heading():emphasis():string(this)",
            "Hi", "<h2><em>Hi</em></h2>\n")]
        public void ChainedIntoDefinition_FallsBackToDynamicTier_AndRendersCorrectly(string body, string value,
            string expected)
        {
            var template = "@model(){{System.String}}@\\\n" + body + "\n";

            var gen = DifferentialHarness.Generate(new[] { ("views/chained-def.heddle", template) });
            Assert.False(gen.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error),
                "Unexpected generator error: " + string.Join("; ", gen.Diagnostics.Select(d => d.ToString())));

            // Documented fallback: a multi-item chain does not precompile — no bound strategy for it.
            Assert.False(IsPrecompiled(gen.ManifestSource ?? string.Empty, "views/chained-def.heddle"),
                "Expected a dynamic-tier fallback (chained call), but the template precompiled.");

            // The runtime backend renders the corrected output: the chained value reaches the definition's @out().
            Assert.Equal(expected, RenderDynamic(template, value));
        }
    }
}
