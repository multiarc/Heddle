using System;
using System.IO;
using System.Linq;
using Heddle.LanguageServices;
using Xunit;

namespace Heddle.LanguageServices.Tests
{
    /// <summary>
    /// The phase 6 diagnostics scenario matrix (success criterion 2): engine <c>HED*</c> diagnostics projected
    /// with ID + span, the D25 import re-anchoring, and the D24 exported-function resolution (no false HED1001).
    /// </summary>
    public class LanguageServiceDiagnosticsTests
    {
        private const string Path = "doc.heddle";
        private static string CorpusDir => System.IO.Path.Combine(AppContext.BaseDirectory, "Corpus");

        private static DocumentAnalysis Analyze(string template, bool typed = true, string rootPath = null)
        {
            var service = typed ? CorpusFixture.NewTypedService(rootPath) : CorpusFixture.NewTypelessService(rootPath);
            try
            {
                return service.Analyze(Path, template, 1);
            }
            finally
            {
                service.Dispose();
            }
        }

        [Fact]
        public void MemberTypoProducesHed0001AtSpan()
        {
            var a = Analyze("@model(){{Corpus.Blog}}\n@(Nonexistent)");
            var d = a.Diagnostics.FirstOrDefault(x => x.Message.Contains("Nonexistent"));
            Assert.NotNull(d);
            Assert.Equal("HED0001", d.Id);
            Assert.Equal(HeddleDiagnosticSeverity.Error, d.Severity);
            Assert.True(d.Length > 0);
        }

        [Fact]
        public void UnclosedBlockProducesHed0003()
        {
            var a = Analyze("@list(x){{ unterminated");
            Assert.Contains(a.Diagnostics, x => x.Id == "HED0003");
        }

        [Fact]
        public void ScannedExportedFunctionCallDrawsNoHed1001()
        {
            // titlecase is a D24-scanned export → registered in the workspace registry, so the engine resolves it.
            var a = Analyze("@model(){{Corpus.Blog}}\n@(titlecase(Title))");
            Assert.DoesNotContain(a.Diagnostics, x => x.Id == "HED1001");
        }

        [Fact]
        public void UnknownFunctionStillDrawsHed1001()
        {
            var a = Analyze("@model(){{Corpus.Blog}}\n@(nosuchfunc(Title))");
            Assert.Contains(a.Diagnostics, x => x.Id == "HED1001");
        }

        [Fact]
        public void ImportedSyntaxErrorReAnchorsToImportSite()
        {
            var a = Analyze("@<<{{broken-lib.heddle}}", rootPath: CorpusDir);
            var d = a.Diagnostics.FirstOrDefault(x => x.ImportedFrom != null);
            Assert.NotNull(d);
            Assert.Contains("broken-lib.heddle", d.ImportedFrom);
            Assert.StartsWith("imported '", d.Message);
            Assert.Equal(0, d.Offset);   // zero-width anchor at the @<< site (document start)
            Assert.Equal(0, d.Length);
        }

        [Fact]
        public void CleanTemplateHasNoDiagnostics()
        {
            var a = Analyze("@model(){{Corpus.Blog}}\n@(Title)");
            Assert.Empty(a.Diagnostics);
        }
    }
}
