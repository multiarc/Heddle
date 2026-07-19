using System;
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
    /// Phase 2 (post-2.0) WI2/D5–D7 — the HED4005 <c>{{ … }}</c>-in-text misread lint: a bare
    /// <c>{{ Title }}</c> in body text renders literal braces, warns once per occurrence (positioned at the
    /// <c>{{</c>), and suggests <c>@(Title)</c>. Never fires inside a real subtemplate body, a raw region,
    /// a definition body, or an <c>@&lt;&lt;</c> import block; never fires on operator/colon bodies. The
    /// corpus scan asserts zero HED4005 across every pre-existing fixture (D-BC1).
    /// </summary>
    public class BraceMisreadWarningTests
    {
        private static HeddleTemplate Compile(string template, Type modelType = null)
        {
            HeddleTemplate.Configure(typeof(BraceMisreadWarningTests).GetTypeInfo().Assembly);
            return new HeddleTemplate(template,
                new CompileContext(new TemplateOptions(), modelType ?? typeof(object)));
        }

        private static int WarningCount(HeddleTemplate t) =>
            t.Context.CompileWarnings.Count(w => w.DiagnosticId == HeddleDiagnosticIds.LiquidStyleInterpolationMisread);

        [Fact]
        public void SingleIdentifierWarnsOnceAndRendersLiterally()
        {
            var t = Compile("<p>{{ Title }}</p>");
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            Assert.Equal("<p>{{ Title }}</p>", t.Generate(null));
            var warning = Assert.Single(t.Context.CompileWarnings,
                w => w.DiagnosticId == HeddleDiagnosticIds.LiquidStyleInterpolationMisread);
            Assert.Equal(3, warning.Position.StartIndex); // at the '{{'
            Assert.Equal(2, warning.Position.Length);
            Assert.Equal("To output the value, use '@(Title)'.", warning.Fix);
        }

        [Fact]
        public void DottedPathWarns()
        {
            var t = Compile("<p>{{ Author.Name }}</p>");
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            var warning = Assert.Single(t.Context.CompileWarnings,
                w => w.DiagnosticId == HeddleDiagnosticIds.LiquidStyleInterpolationMisread);
            Assert.Equal("To output the value, use '@(Author.Name)'.", warning.Fix);
        }

        [Fact]
        public void ExpressionBodyNoWarn()
        {
            var t = Compile("{{ a + b }}");
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            Assert.Equal(0, WarningCount(t));
            Assert.Equal("{{ a + b }}", t.Generate(null));
        }

        [Fact]
        public void ColonBodyNoWarn()
        {
            var t = Compile("{{ x:y }}");
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            Assert.Equal(0, WarningCount(t));
            Assert.Equal("{{ x:y }}", t.Generate(null));
        }

        private class Article
        {
            public string Title { get; set; }
        }

        private class ArticlesModel
        {
            public Article[] Articles { get; set; }
        }

        [Fact]
        public void RealSubtemplateBodyNoWarn_WithDirective()
        {
            var t = Compile("@list(Articles){{ @(Title) }}", typeof(ArticlesModel));
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            Assert.Equal(0, WarningCount(t));
        }

        [Fact]
        public void RealSubtemplateBodyNoWarn_BareIdentifier()
        {
            // A real '@if(…){{ Title }}' body: the '{{' sits inside the output chain's span — excluded.
            var t = Compile("@if(true){{ Title }}");
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            Assert.Equal(0, WarningCount(t));
        }

        [Fact]
        public void RawRegionNoWarn()
        {
            var t = Compile("@{ {{ Title }} }@");
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            Assert.Equal(0, WarningCount(t));
            Assert.Equal(" {{ Title }} ", t.Generate(null));
        }

        [Fact]
        public void ImportBlockNoWarn()
        {
            // '@<<{{layout.heddle}}' matches the D7 dotted-path regex, but the import block's span lives in
            // DefinitionsBlock.Positions — excluded.
            HeddleTemplate.Configure(typeof(BraceMisreadWarningTests).GetTypeInfo().Assembly);
            var options = new TemplateOptions { RootPath = Path.GetFullPath("TestTemplate") };
            var t = new HeddleTemplate("@<<{{layout.heddle}}", new CompileContext(options, typeof(object)));
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            Assert.Equal(0, WarningCount(t));
        }

        [Fact]
        public void CommentBeforeRealBodyNoWarn()
        {
            // F2 coordinate regression: the comment excises to a clean '@if(true){{ Title }}' whose
            // '{{ Title }}' clean offset falls inside the post-shift chain span — proving the scan runs
            // AFTER ShiftBySkippedTokens (a pre-shift scan false-fires here).
            var t = Compile("@* note *@@if(true){{ Title }}");
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            Assert.Equal(0, WarningCount(t));
        }

        [Fact]
        public void CommentBeforeTextMisreadWarns()
        {
            // F2 coordinate regression: a genuine text-position misread preceded by a comment still fires,
            // positioned at the clean-document '{{' (the comment's excised length precedes it).
            var t = Compile("@* note *@<p>{{ Title }}</p>");
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            Assert.Equal("<p>{{ Title }}</p>", t.Generate(null));
            var warning = Assert.Single(t.Context.CompileWarnings,
                w => w.DiagnosticId == HeddleDiagnosticIds.LiquidStyleInterpolationMisread);
            Assert.Equal(3, warning.Position.StartIndex); // clean-document offset of the '{{'
        }

        [Fact]
        public void TwoOccurrencesWarnTwice()
        {
            var t = Compile("<p>{{ A }}</p><p>{{ B }}</p>");
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            var warnings = t.Context.CompileWarnings
                .Where(w => w.DiagnosticId == HeddleDiagnosticIds.LiquidStyleInterpolationMisread)
                .OrderBy(w => w.Position.StartIndex)
                .ToList();
            Assert.Equal(2, warnings.Count);
            Assert.Equal(3, warnings[0].Position.StartIndex);
            Assert.Equal(17, warnings[1].Position.StartIndex);
        }

        [Fact]
        public void BraceMisreadGolden()
        {
            // The warning never alters output (the ErgoDoubleRenderGolden pattern).
            HeddleTemplate.Configure(typeof(BraceMisreadWarningTests).GetTypeInfo().Assembly);
            var document = File.ReadAllText("TestTemplate/brace-misread.heddle").Replace("\r\n", "\n");
            var t = new HeddleTemplate(document, new CompileContext(new TemplateOptions(), typeof(object)));
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            Assert.Equal(1, WarningCount(t));
            var actual = t.Generate(null);
            File.WriteAllText("TestTemplate/test-brace-misread.html", actual);
            var expected = File.ReadAllText("TestTemplate/generated-brace-misread.html").Replace("\r\n", "\n");
            Assert.Equal(expected, actual);
        }

        [Fact] // corpus scan (D-BC1) — zero HED4005 on every pre-existing fixture
        public void CorpusScanReportsOnlyTheBraceMisreadFixture()
        {
            HeddleTemplate.Configure(typeof(BraceMisreadWarningTests).GetTypeInfo().Assembly);
            var root = Path.GetFullPath("TestTemplate");
            foreach (var path in Directory.GetFiles("TestTemplate", "*.heddle"))
            {
                var name = Path.GetFileName(path);
                int hits;
                using (var scope = new CompileScope(
                    new CompileContext(new TemplateOptions { ExpressionMode = ExpressionMode.FullCSharp, RootPath = root }, typeof(object))))
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
                        // throw are still counted (the DoubleRenderWarningTests.W08 pattern).
                    }

                    hits = scope.CompileWarnings.Count(w => w.DiagnosticId == HeddleDiagnosticIds.LiquidStyleInterpolationMisread);
                }

                int expected = name == "brace-misread.heddle" ? 1 : 0;
                Assert.True(expected == hits, $"{name}: expected {expected} HED4005 warning(s), got {hits}");
            }
        }
    }
}
