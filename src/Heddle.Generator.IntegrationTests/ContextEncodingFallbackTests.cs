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
    /// The step-back encoders override <c>InitStart</c>, but the override re-types only the DEFAULT BODY, against
    /// the caller's scope. Both halves of that are now emitted: a bodiless call carries exactly the base bind, and
    /// a bodied one compiles its body in the enclosing model's context, which is what the <c>Parent</c> role means.
    /// <para>The bodied case is also where the render type stops being cosmetic. <c>@attr</c> and <c>@string</c>
    /// carry <c>[EncodeOutput]</c>, so a body-hosting bind that hard-coded <c>RenderType.Raw</c> would emit markup
    /// unescaped where the dynamic tier escapes it — a cross-site scripting divergence, produced by the tier that
    /// is supposed to be byte-identical. The render type is derived from the extension's own attributes, and the
    /// markup-bearing rows below are what would catch a regression.</para>
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

        /// <summary>The bodied form, which used to be the degrade this file pinned. The body compiles against the
        /// caller's scope, and the tier is asserted — a byte comparison alone proves nothing here, because a
        /// fallback is byte-identical by design.</summary>
        [Fact]
        public void StepBackEncoderBodiedPrecompilesAndRendersItsBodyIdentically()
        {
            var template = "@model(){{System.String}}@\\\n@attr(this){{fallback}}\n";

            var gen = DifferentialHarness.Generate(new[] { ("views/ctx-bodied.heddle", template) });
            Assert.False(gen.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error),
                "Unexpected generator error: " + string.Join("; ", gen.Diagnostics.Select(d => d.ToString())));
            DifferentialHarness.ExpectPrecompiled(gen, "views/ctx-bodied.heddle");

            // The body is what renders when the value is null; the value is what renders otherwise.
            foreach (var value in new[] { null, "x&y" })
            {
                var (precompiled, dynamic) = DifferentialHarness.Render("views/ctx-bodied.heddle", template,
                    typeof(string), value);
                Assert.Equal(dynamic, precompiled);
                Assert.Equal(RenderDynamic(template, value), precompiled);
            }
        }

        /// <summary><b>The security case.</b> An <c>[EncodeOutput]</c> extension hosting a body must encode its
        /// result on the precompiled tier exactly as it does on the dynamic one. The bind's render type decides
        /// that, and it was hard-coded <c>RenderType.Raw</c> for every body-hosting call — invisible while only
        /// <c>@if</c>, <c>@for</c> and <c>@list</c> reached it, since none of them carries an encoding attribute,
        /// and unescaped output the moment an encoding extension did. Both values matter: the body's own result
        /// (null model) and the call value's (non-null).</summary>
        [Theory]
        // @string routes its own result through the carrier, so the body's markup is escaped on both tiers.
        [InlineData("@string(this)", "a&amp;b&lt;c&gt;\n", "x&amp;y\n")]
        // @attr escapes the call VALUE itself and hands its default body back untouched — a difference between two
        // encoders, reproduced rather than smoothed over.
        [InlineData("@attr(this)", "a&b<c>\n", "x&amp;y\n")]
        public void ABodyHostedByAnEncodingExtensionIsEncodedOnBothTiers(string call, string whenNull,
            string whenValued)
        {
            var template = "@model(){{System.String}}@\\\n" + call + "{{a&b<c>}}\n";

            var gen = DifferentialHarness.Generate(new[] { ("views/ctx-xss.heddle", template) });
            DifferentialHarness.ExpectPrecompiled(gen, "views/ctx-xss.heddle");

            var (onNull, dynamicOnNull) = DifferentialHarness.Render("views/ctx-xss.heddle", template,
                typeof(string), null);
            Assert.Equal(dynamicOnNull, onNull);
            Assert.Equal(whenNull, onNull);

            var (onValue, dynamicOnValue) = DifferentialHarness.Render("views/ctx-xss.heddle", template,
                typeof(string), "x&y");
            Assert.Equal(dynamicOnValue, onValue);
            Assert.Equal(whenValued, onValue);
        }
    }
}
