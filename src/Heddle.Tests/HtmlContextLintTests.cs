using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Heddle.Data;
using Heddle.Language;
using Heddle.Runtime;
using Xunit;
using Heddle.TestCorpus;

namespace Heddle.Tests
{
    /// <summary>
    /// The HED2004 HTML-context encoding lint: warns when a bare <c>@(value)</c> under Html profile sits in
    /// attribute / script / URL position; suggests appropriate context encoder or <c>@raw</c>. Never changes
    /// rendered bytes or fires outside Html profile.
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

        /// <summary>Asserts HED2004 warnings (position, message, fix) under Html, and zero warnings under Text.</summary>
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

        [Fact]
        public void AttributeValueWarns()
        {
            AssertMatrixRow("<a title=\"@(X)\">link</a>", (11, AttributeMessage, AttributeFix));
        }

        [Fact]
        public void CorrectAttrEncoderDoesNotWarn()
        {
            AssertMatrixRow("<a title=\"@attr(X)\">link</a>");
        }

        [Fact]
        public void ElementTextDoesNotWarn()
        {
            AssertMatrixRow("<p>@(X)</p>");
        }

        [Fact]
        public void RawOptOutDoesNotWarn()
        {
            AssertMatrixRow("<a title=\"@raw(X)\">link</a>");
        }

        [Fact]
        public void ScriptBlockWarnsWithJsFix()
        {
            AssertMatrixRow("<script>var n = \"@(X)\";</script>", (18, ScriptMessage, ScriptFix));
        }

        [Fact]
        public void UrlQueryComponentWarnsWithUrlFix()
        {
            AssertMatrixRow("<a href=\"/s?q=@(X)\">", (15, UrlMessage, UrlFix));
        }

        [Fact]
        // Canonical documented false positive: comment reads as unclosed tag.
        public void HtmlCommentWarnsAsAttribute()
        {
            AssertMatrixRow("<!-- @(X) -->", (6, AttributeMessage, AttributeFix));
        }

        [Fact]
        // Single-hop: definition body has no adjacent attribute literal.
        public void DefinitionBodyIsSingleHopFalseNegative()
        {
            AssertMatrixRow("@%\n<t>\n{{ @(X) }}\n%@\n<a title=\"@t()\">link</a>");
        }

        [Fact]
        // Whole-URL value degrades to @attr, never @url.
        public void WholeUrlValueDegradesToAttribute()
        {
            AssertMatrixRow("<a href=\"@(FullUrl)\">", (10, AttributeMessage, AttributeFix));
        }

        [Fact]
        public void SingleQuotedAttributeWarns()
        {
            AssertMatrixRow("<a title='@(X)'>", (11, AttributeMessage, AttributeFix));
        }

        [Fact]
        // Inside <script> start tag (src attribute, not body).
        public void ScriptStartTagAttributeWarnsAsAttribute()
        {
            AssertMatrixRow("<script src=\"@(X)\"></script>", (14, AttributeMessage, AttributeFix));
        }

        [Fact]
        // Mid-document flip to Html is honored.
        public void MidDocumentFlipToHtmlIsHonored()
        {
            var t = Compile("@profile(){{html}}<a title=\"@(X)\">link</a>", OutputProfile.Text);
            var warning = Assert.Single(Lints(t));
            Assert.Equal(29, warning.Position.StartIndex);
            Assert.Equal(AttributeMessage, warning.Error);
        }

        [Fact]
        // Flip to Text before the block silences the lint.
        public void MidDocumentFlipToTextSilences()
        {
            var t = Compile("@profile(){{text}}<a title=\"@(X)\">link</a>", OutputProfile.Html);
            Assert.Empty(Lints(t));
        }

        [Fact]
        // Script-prefixed custom element is not a script open.
        public void ScriptPrefixedCustomElementDoesNotWarn()
        {
            AssertMatrixRow("<script-loader>@(X)</script-loader>");
        }

        [Fact]
        // Documented Step-1 false positive: <script>-shaped literal in attribute value.
        public void ScriptLiteralInAttributeValueOverFiresToScript()
        {
            AssertMatrixRow("<p data-x=\"<script>\">@(X)</p>", (22, ScriptMessage, ScriptFix));
        }

        [Fact]
        // Documented Step-1 false positive: raw-text element hides inert <script>.
        public void RawTextElementScriptOverFiresToScript()
        {
            AssertMatrixRow("<textarea><script></textarea>@(X)", (30, ScriptMessage, ScriptFix));
        }

        [Fact]
        // Tag-name / attribute-name injection gets generic in-tag @attr signal.
        public void TagNameAndAttributeNamePositionsWarnAsAttribute()
        {
            AssertMatrixRow("<@(Tag)>", (2, AttributeMessage, AttributeFix));
            AssertMatrixRow("<div @(Attrs)>", (6, AttributeMessage, AttributeFix));
        }

        [Fact]
        // Mixed-quote poisoning under-classifies URL to Attribute (documented limit).
        public void MixedQuotePoisoningUnderClassifiesToAttribute()
        {
            AssertMatrixRow("<a title='it\"s' href=\"/s?q=@(X)\">", (28, AttributeMessage, AttributeFix));
        }

        [Fact]
        // Regression witness: interior '<' in script body never mis-routes to @attr.
        public void InteriorLessThanInScriptBodyStaysScript()
        {
            AssertMatrixRow("<script>for (var i = 0; i < n; i++) { x = @(X); }</script>",
                (43, ScriptMessage, ScriptFix));
        }

        [Fact]
        // URL path-segment signal (valueSoFar ends with '/').
        public void UrlPathSegmentWarnsWithUrlFix()
        {
            AssertMatrixRow("<img src=\"/img/@(Id)/thumb.png\">", (16, UrlMessage, UrlFix));
        }

        [Fact]
        // Regression witness: '>' in sibling @(…) source never skews classification.
        public void SiblingInterpolationSourceIsSkipped()
        {
            AssertMatrixRow("<span data-n=\"@(Count > 0)@(Label)\">",
                (15, AttributeMessage, AttributeFix),
                (27, AttributeMessage, AttributeFix));
        }

        [Fact]
        // Quoted '>' does not close <script> start tag.
        public void QuotedGreaterThanInScriptStartTagStaysAttribute()
        {
            AssertMatrixRow("<script src=\"a>@(X)\">", (16, AttributeMessage, AttributeFix));
        }

        [Fact]
        // Quoted '>' then URL refinement in later attribute.
        public void QuotedGreaterThanThenUrlAttributeStaysUrl()
        {
            AssertMatrixRow("<script data-x=\"a>b\" src=\"/s?q=@(X)\">", (32, UrlMessage, UrlFix));
        }

        [Fact]
        // </scriptx> is not an end tag (boundary symmetry).
        public void NonBoundaryCloseTagDoesNotCountAsClose()
        {
            AssertMatrixRow("<script>x=1</scriptx>@(X)</script>", (22, ScriptMessage, ScriptFix));
        }

        [Fact]
        // Documented false positive: unmatched <script> in comment affects document-spanning state.
        public void UnmatchedScriptInCommentOverFiresToScript()
        {
            AssertMatrixRow("<!-- <script> --><p>@(X)</p>", (21, ScriptMessage, ScriptFix));
        }

        [Fact]
        public void FullyCommentedScriptPairIsElementText()
        {
            // Commented pair: close > open, so Step 1 doesn't fire.
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
            File.WriteAllText(TestCorpusIndex.WrittenArtifactPath("test-context-lint-html.html"), actual);
            var expected = File.ReadAllText("TestTemplate/generated-context-lint-html.html").Replace("\r\n", "\n");
            Assert.Equal(expected, actual.Replace("\r\n", "\n"));
        }

        [Fact] // byte-neutrality golden — zero warnings, identical bytes (Text profile).
        public void CorpusRendersByteIdenticalUnderText()
        {
            var actual = RenderCorpus(OutputProfile.Text, out var lintCount);
            Assert.Equal(0, lintCount);
            File.WriteAllText(TestCorpusIndex.WrittenArtifactPath("test-context-lint-text.html"), actual);
            var expected = File.ReadAllText("TestTemplate/generated-context-lint-text.html").Replace("\r\n", "\n");
            Assert.Equal(expected, actual.Replace("\r\n", "\n"));
        }

        [Fact]
        public void CorpusScanIsInertUnderTextProfile()
        {
            HeddleTemplate.Configure(typeof(HtmlContextLintTests).GetTypeInfo().Assembly);
            var root = Path.GetFullPath("TestTemplate");
            // Fixtures containing a @profile() flip to Html: their post-flip blocks classify under Html by
            // design (D1); each carries its explicit expected count here.
            var flipFixtures = new Dictionary<string, int>
            {
                { "profile-directive.heddle", 0 } // Flips to Html but @(Value)/@raw(Value) in element text.
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
                        // Some fixtures need host setup; count warnings collected before the throw.
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
