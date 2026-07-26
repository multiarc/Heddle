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
    /// The double-render warning HED4002: a by-name call to a definition carrying a default output
    /// (<c>-&gt; chain</c>) warns once per call site, naming the definition and its declaration position;
    /// the default chain's own self-call is exempt; override layering warns in both directions; a derived-name
    /// call does not.
    /// </summary>
    public class DoubleRenderWarningTests
    {
        private const string Fix = "Remove the '->' from the definition, or remove this call.";

        private static HeddleTemplate Compile(string template)
        {
            HeddleTemplate.Configure(typeof(DoubleRenderWarningTests).GetTypeInfo().Assembly);
            var t = new HeddleTemplate(template, new CompileContext(new TemplateOptions(), typeof(object)));
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            return t;
        }

        private static int WarningCount(HeddleTemplate t) =>
            t.Context.CompileWarnings.Count(w => w.DiagnosticId == HeddleDiagnosticIds.DefinitionRendersTwice);

        [Fact]
        public void W01_DefaultOutputAlsoCalledByNameWarnsOnce()
        {
            var t = Compile("@%\n<card> -> ()\n{{CARD}}\n%@\n@card()");
            var warning = Assert.Single(t.Context.CompileWarnings,
                w => w.DiagnosticId == HeddleDiagnosticIds.DefinitionRendersTwice);
            Assert.StartsWith("Definition 'card' (declared at ", warning.Error);
            Assert.EndsWith("has a default output ('->') and is also called by name — it renders twice.", warning.Error);
            Assert.Contains(":", warning.Error); // the embedded declaration BlockPosition (start:length)
            Assert.Equal(Fix, warning.Fix);
            Assert.True(warning.Position.StartIndex > 0, "warning is positioned at the by-name call, not the declaration");
        }

        [Fact]
        public void W02_NoDefaultOutputDoesNotWarn()
        {
            Assert.Equal(0, WarningCount(Compile("@%\n<card>\n{{CARD}}\n%@\n@card()")));
        }

        [Fact]
        public void W03_SelfCallExemptWhenNotCalledByName()
        {
            Assert.Equal(0, WarningCount(Compile("@%\n<card> -> ()\n{{CARD}}\n%@")));
        }

        [Fact]
        public void W04_TwoCallsProduceTwoWarnings()
        {
            Assert.Equal(2, WarningCount(Compile("@%\n<card> -> ()\n{{CARD}}\n%@\n@card()\n@card()")));
        }

        [Fact]
        public void W05_CallBeforeFullOverrideWarns()
        {
            Assert.Equal(1, WarningCount(Compile("@%\n<a> -> ()\n{{A}}\n%@\n@a()\n@%\n<a:a>\n{{A2}}\n%@")));
        }

        [Fact]
        public void W06_CallAfterFullOverrideStillWarns()
        {
            // Base's default chain persists after override.
            Assert.Equal(1, WarningCount(Compile("@%\n<a> -> ()\n{{A}}\n%@\n@%\n<a:a>\n{{A2}}\n%@\n@a()")));
        }

        [Fact]
        public void W07_DerivedNameCallDoesNotWarn()
        {
            Assert.Equal(0, WarningCount(Compile("@%\n<a> -> ()\n{{A}}\n<b:a>\n{{B}}\n%@\n@b()")));
        }

        [Fact]
        public void W08_CorpusScanReportsExactlyTheTwoVcTestHits()
        {
            HeddleTemplate.Configure(typeof(DoubleRenderWarningTests).GetTypeInfo().Assembly);
            var root = Path.GetFullPath("TestTemplate");
            foreach (var path in Directory.GetFiles("TestTemplate", "*.heddle"))
            {
                var name = Path.GetFileName(path);
                int hits = 0;
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
                        // Host setup may fail; warnings collected before the throw still count.
                    }

                    hits = scope.CompileWarnings.Count(w => w.DiagnosticId == HeddleDiagnosticIds.DefinitionRendersTwice);
                }

                int expected = name == "vc-test.heddle" ? 2 : name == "ergo-double-render.heddle" ? 1 : 0;
                Assert.True(expected == hits, $"{name}: expected {expected} HED4002 warning(s), got {hits}");
            }
        }

        [Fact]
        public void ErgoDoubleRenderGolden()
        {
            HeddleTemplate.Configure(typeof(DoubleRenderWarningTests).GetTypeInfo().Assembly);
            var document = File.ReadAllText("TestTemplate/ergo-double-render.heddle").Replace("\r\n", "\n");
            var t = new HeddleTemplate(document, new CompileContext(new TemplateOptions(), typeof(object)));
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            Assert.Equal(1, WarningCount(t));
            var actual = t.Generate(null);
            File.WriteAllText(TestCorpusIndex.WrittenArtifactPath("test-ergo-double-render.html"), actual);
            var expected = File.ReadAllText("TestTemplate/generated-ergo-double-render.html").Replace("\r\n", "\n");
            Assert.Equal(expected, actual);
        }
    }
}
