using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Heddle.Data;
using Heddle.Language;
using Heddle.Runtime;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// Phase 3 (post-2.0) — the HED2004 HTML-context encoding lint: a bare bodiless <c>@(value)</c> under an
    /// explicitly declared <c>Html</c> profile sitting in an attribute / <c>&lt;script&gt;</c> / URL position
    /// warns once, positioned at the <c>@(</c>, suggesting the matching context encoder (<c>@attr</c> /
    /// <c>@js</c> / <c>@url</c>) or <c>@raw</c>. Never fires off the Html profile (R3 gate), never on a named
    /// encoder / opt-out / bodied carrier (D2), and never changes rendered bytes (the byte-neutrality goldens
    /// and the corpus gate). The matrix rows mirror the phase 3 spec's testing plan one-to-one.
    /// </summary>
    public class HtmlContextLintTests
    {
        public class LintModel
        {
            public string X { get; set; }
            public string FullUrl { get; set; }
            public string Id { get; set; }
            public string Tag { get; set; }
            public string Attrs { get; set; }
            public int Count { get; set; }
            public string Label { get; set; }
        }

        private const string AttributeMessage =
            "A bare '@(...)' output is inside an HTML tag under the Html profile (attribute value or an unquoted/name position); element-text encoding is insufficient there.";

        private const string ScriptMessage =
            "A bare '@(...)' output is inside a <script> block under the Html profile; HTML element-text encoding is wrong for a JavaScript context.";

        private const string UrlMessage =
            "A bare '@(...)' output is in a URL component under the Html profile; element-text encoding does not percent-encode it.";

        private const string AttributeFix =
            "Use '@attr(...)' for the attribute context, or '@raw(...)' if the value is trusted.";

        private const string ScriptFix =
            "Use '@js(...)' for the JavaScript-string context, or '@raw(...)' if the value is trusted.";

        private const string UrlFix =
            "Use '@url(...)' for the URL-component context, or '@raw(...)' if the value is trusted.";

        private static HeddleTemplate Compile(string template, OutputProfile profile)
        {
            HeddleTemplate.Configure(typeof(HtmlContextLintTests).GetTypeInfo().Assembly);
            var t = new HeddleTemplate(template,
                new CompileContext(new TemplateOptions { OutputProfile = profile }, typeof(LintModel)));
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            return t;
        }

        private static List<HeddleCompileWarning> Lints(HeddleTemplate t) =>
            t.Context.CompileWarnings
                .Where(w => w.DiagnosticId == HeddleDiagnosticIds.MissingContextEncoder)
                .OrderBy(w => w.Position.StartIndex)
                .ToList();

        /// <summary>Asserts the exact HED2004 set of a template under Html: (position of the '@(', message, fix)
        /// per expected warning, and — the R3 gate — zero HED2004 for the same template under Text.</summary>
        private static void AssertMatrixRow(string template, params (int Start, string Message, string Fix)[] expected)
        {
            var html = Lints(Compile(template, OutputProfile.Html));
            Assert.Equal(expected.Length, html.Count);
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.Equal(expected[i].Start, html[i].Position.StartIndex);
                Assert.Equal(expected[i].Message, html[i].Error);
                Assert.Equal(expected[i].Fix, html[i].Fix);
            }

            Assert.Empty(Lints(Compile(template, OutputProfile.Text)));
        }

        [Fact] // row 1
        public void AttributeValueWarns()
        {
            AssertMatrixRow("<a title=\"@(X)\">link</a>", (11, AttributeMessage, AttributeFix));
        }

        [Fact] // row 2
        public void CorrectAttrEncoderDoesNotWarn()
        {
            AssertMatrixRow("<a title=\"@attr(X)\">link</a>");
        }

        [Fact] // row 3
        public void ElementTextDoesNotWarn()
        {
            AssertMatrixRow("<p>@(X)</p>");
        }

        [Fact] // row 4
        public void RawOptOutDoesNotWarn()
        {
            AssertMatrixRow("<a title=\"@raw(X)\">link</a>");
        }

        [Fact] // row 5
        public void ScriptBlockWarnsWithJsFix()
        {
            AssertMatrixRow("<script>var n = \"@(X)\";</script>", (18, ScriptMessage, ScriptFix));
        }

        [Fact] // row 6
        public void UrlQueryComponentWarnsWithUrlFix()
        {
            AssertMatrixRow("<a href=\"/s?q=@(X)\">", (15, UrlMessage, UrlFix));
        }

        [Fact] // row 7 — the canonical documented false positive (comment reads as an unclosed tag).
        public void HtmlCommentWarnsAsAttribute()
        {
            AssertMatrixRow("<!-- @(X) -->", (6, AttributeMessage, AttributeFix));
        }

        [Fact] // row 8 — single-hop: the definition body's @(X) has no adjacent attribute literal of its own.
        public void DefinitionBodyIsSingleHopFalseNegative()
        {
            AssertMatrixRow("@%\n<t>\n{{ @(X) }}\n%@\n<a title=\"@t()\">link</a>");
        }

        [Fact] // row 9 — a whole-URL value degrades to @attr (never mis-suggests @url).
        public void WholeUrlValueDegradesToAttribute()
        {
            AssertMatrixRow("<a href=\"@(FullUrl)\">", (10, AttributeMessage, AttributeFix));
        }

        [Fact] // row 10
        public void SingleQuotedAttributeWarns()
        {
            AssertMatrixRow("<a title='@(X)'>", (11, AttributeMessage, AttributeFix));
        }

        [Fact] // row 11 — inside the <script …> start tag itself: the src attribute, not the script body.
        public void ScriptStartTagAttributeWarnsAsAttribute()
        {
            AssertMatrixRow("<script src=\"@(X)\"></script>", (14, AttributeMessage, AttributeFix));
        }

        [Fact] // row 12 — a mid-document flip to Html is honored (D1: the per-block effective profile).
        public void MidDocumentFlipToHtmlIsHonored()
        {
            var t = Compile("@profile(){{html}}<a title=\"@(X)\">link</a>", OutputProfile.Text);
            var warning = Assert.Single(Lints(t));
            Assert.Equal(29, warning.Position.StartIndex);
            Assert.Equal(AttributeMessage, warning.Error);
        }

        [Fact] // row 13 — a flip to Text before the block silences the lint.
        public void MidDocumentFlipToTextSilences()
        {
            var t = Compile("@profile(){{text}}<a title=\"@(X)\">link</a>", OutputProfile.Html);
            Assert.Empty(Lints(t));
        }

        [Fact] // row 14 — a script-prefixed custom element is not a script open (Step 1 boundary check).
        public void ScriptPrefixedCustomElementDoesNotWarn()
        {
            AssertMatrixRow("<script-loader>@(X)</script-loader>");
        }

        [Fact] // row 15 — documented Step-1 false positive: a <script>-shaped literal inside an attribute value.
        public void ScriptLiteralInAttributeValueOverFiresToScript()
        {
            AssertMatrixRow("<p data-x=\"<script>\">@(X)</p>", (22, ScriptMessage, ScriptFix));
        }

        [Fact] // row 16 — documented Step-1 false positive: a raw-text element hides the inert <script>.
        public void RawTextElementScriptOverFiresToScript()
        {
            AssertMatrixRow("<textarea><script></textarea>@(X)", (30, ScriptMessage, ScriptFix));
        }

        [Fact] // row 17 — tag-name / attribute-name injection gets the generic in-tag @attr signal.
        public void TagNameAndAttributeNamePositionsWarnAsAttribute()
        {
            AssertMatrixRow("<@(Tag)>", (2, AttributeMessage, AttributeFix));
            AssertMatrixRow("<div @(Attrs)>", (6, AttributeMessage, AttributeFix));
        }

        [Fact] // row 18 — mixed-quote poisoning under-classifies Url to Attribute (documented limit).
        public void MixedQuotePoisoningUnderClassifiesToAttribute()
        {
            AssertMatrixRow("<a title='it\"s' href=\"/s?q=@(X)\">", (28, AttributeMessage, AttributeFix));
        }

        [Fact] // row 19 — regression witness: an interior '<' in the script body never mis-routes to @attr.
        public void InteriorLessThanInScriptBodyStaysScript()
        {
            AssertMatrixRow("<script>for (var i = 0; i < n; i++) { x = @(X); }</script>",
                (43, ScriptMessage, ScriptFix));
        }

        [Fact] // row 20 — URL path-segment signal (valueSoFar ends with '/').
        public void UrlPathSegmentWarnsWithUrlFix()
        {
            AssertMatrixRow("<img src=\"/img/@(Id)/thumb.png\">", (16, UrlMessage, UrlFix));
        }

        [Fact] // row 21 — regression witness: a '>' inside a sibling @(…) source never skews classification.
        public void SiblingInterpolationSourceIsSkipped()
        {
            AssertMatrixRow("<span data-n=\"@(Count > 0)@(Label)\">",
                (15, AttributeMessage, AttributeFix),
                (27, AttributeMessage, AttributeFix));
        }

        [Fact] // row 22 — quote-aware condition (b): a quoted '>' does not close the <script …> start tag.
        public void QuotedGreaterThanInScriptStartTagStaysAttribute()
        {
            AssertMatrixRow("<script src=\"a>@(X)\">", (16, AttributeMessage, AttributeFix));
        }

        [Fact] // row 23 — quote-aware condition (b) + URL refinement in a later attribute of the same start tag.
        public void QuotedGreaterThanThenUrlAttributeStaysUrl()
        {
            AssertMatrixRow("<script data-x=\"a>b\" src=\"/s?q=@(X)\">", (32, UrlMessage, UrlFix));
        }

        [Fact] // row 24 — close-boundary symmetry: </scriptx> is not an end tag.
        public void NonBoundaryCloseTagDoesNotCountAsClose()
        {
            AssertMatrixRow("<script>x=1</scriptx>@(X)</script>", (22, ScriptMessage, ScriptFix));
        }

        [Fact] // row 25 — documented document-spanning Script false positive (unmatched <script> in a comment).
        public void UnmatchedScriptInCommentOverFiresToScript()
        {
            AssertMatrixRow("<!-- <script> --><p>@(X)</p>", (21, ScriptMessage, ScriptFix));
        }

        [Fact]
        public void FullyCommentedScriptPairIsElementText()
        {
            // A commented <script></script> pair: close > open, so Step 1 does not fire and the
            // trailing element-text block is clean (the D3 counter-example to row 25).
            AssertMatrixRow("<!-- <script></script> --><p>@(X)</p>");
        }

        private static string RenderCorpus(OutputProfile profile, out int lintCount)
        {
            HeddleTemplate.Configure(typeof(HtmlContextLintTests).GetTypeInfo().Assembly);
            var document = File.ReadAllText("TestTemplate/context-lint-corpus.heddle").Replace("\r\n", "\n");
            var t = new HeddleTemplate(document,
                new CompileContext(new TemplateOptions { OutputProfile = profile }, typeof(LintModel)));
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            lintCount = Lints(t).Count;
            var model = new LintModel
            {
                X = "x",
                FullUrl = "https://example.com/a",
                Id = "42",
                Count = 1,
                Label = "lbl"
            };
            return t.Generate(model);
        }

        [Fact] // byte-neutrality golden — many warnings, identical bytes (Html profile).
        public void CorpusRendersByteIdenticalUnderHtml()
        {
            var actual = RenderCorpus(OutputProfile.Html, out var lintCount);
            Assert.True(lintCount > 0, "The corpus fixture is expected to fire HED2004 under Html.");
            File.WriteAllText("TestTemplate/test-context-lint-html.html", actual);
            var expected = File.ReadAllText("TestTemplate/generated-context-lint-html.html").Replace("\r\n", "\n");
            Assert.Equal(expected, actual.Replace("\r\n", "\n"));
        }

        [Fact] // byte-neutrality golden — zero warnings, identical bytes (Text profile, R3 gate).
        public void CorpusRendersByteIdenticalUnderText()
        {
            var actual = RenderCorpus(OutputProfile.Text, out var lintCount);
            Assert.Equal(0, lintCount);
            File.WriteAllText("TestTemplate/test-context-lint-text.html", actual);
            var expected = File.ReadAllText("TestTemplate/generated-context-lint-text.html").Replace("\r\n", "\n");
            Assert.Equal(expected, actual.Replace("\r\n", "\n"));
        }

        [Fact] // corpus-wide R3 gate: off-Html blocks never warn. A fixture that flips to Html mid-document
        // carries an explicit expectation instead of the blanket zero (its post-flip blocks are Html by design).
        public void CorpusScanIsInertUnderTextProfile()
        {
            HeddleTemplate.Configure(typeof(HtmlContextLintTests).GetTypeInfo().Assembly);
            var root = Path.GetFullPath("TestTemplate");
            // Fixtures containing a @profile() flip to Html: their post-flip blocks classify under Html by
            // design (D1); each carries its explicit expected count here.
            var flipFixtures = new Dictionary<string, int>
            {
                // profile-directive.heddle flips to html, but its @(Value)/@raw(Value) sit in element text.
                { "profile-directive.heddle", 0 }
            };
            foreach (var path in Directory.GetFiles("TestTemplate", "*.heddle"))
            {
                var name = Path.GetFileName(path);
                int hits;
                using (var scope = new CompileScope(new CompileContext(
                           new TemplateOptions
                           {
                               OutputProfile = OutputProfile.Text,
                               ExpressionMode = ExpressionMode.FullCSharp,
                               RootPath = root
                           }, typeof(object))))
                {
                    try
                    {
                        var doc = File.ReadAllText(path).Replace("\r\n", "\n");
                        var parseContext = DocumentParser.Parse(doc, scope.CompileContext, out var clean);
                        HeddleCompiler.Compile(clean, scope, parseContext, null);
                    }
                    catch
                    {
                        // A fixture that needs specific host setup may throw; warnings collected before the
                        // throw are still counted (the BraceMisreadWarningTests corpus-scan pattern).
                    }

                    hits = scope.CompileWarnings.Count(w =>
                        w.DiagnosticId == HeddleDiagnosticIds.MissingContextEncoder);
                }

                int expected = flipFixtures.TryGetValue(name, out var explicitCount) ? explicitCount : 0;
                Assert.True(expected == hits, $"{name}: expected {expected} HED2004 warning(s), got {hits}");
            }
        }
    }
}
