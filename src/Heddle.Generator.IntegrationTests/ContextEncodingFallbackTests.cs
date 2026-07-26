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
    /// Context encoders (<c>@attr</c>/<c>@js</c>/<c>@url</c>) override compile-time hooks, so templates using them
    /// fall back to the dynamic tier. Pins the fallback (no precompiled strategy) and verifies runtime escaping.
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
        public void ContextEncoder_FallsBackToDynamicTier_AndRendersCorrectly(string call, string value, string expected)
        {
            var template = "@model(){{System.String}}@\\\n" + call + "\n";

            var gen = DifferentialHarness.Generate(new[] { ("views/ctx.heddle", template) });
            Assert.False(gen.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error),
                "Unexpected generator error: " + string.Join("; ", gen.Diagnostics.Select(d => d.ToString())));

            DifferentialHarness.ExpectDegrade(gen, "views/ctx.heddle");
            Assert.Equal(expected + "\n", RenderDynamic(template, value));
        }
    }
}
