using System;
using System.IO;
using System.Linq;
using Heddle.LanguageServices;
using Xunit;

namespace Heddle.LanguageServices.Tests
{
    /// <summary>
    /// Diagnostics projection scenarios: engine <c>HED*</c> diagnostics are projected with ID + span,
    /// imported diagnostics are re-anchored to the import site, and exported functions are resolved (no false HED1001).
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
            // titlecase is a scanned export registered in the workspace registry, so the engine resolves it.
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

        /// <summary>
        /// A syntax error inside an import must cost the importing document nothing: its own diagnostics stay,
        /// in their order and first, and the import's are attributed to the import and anchored at its site.
        /// The import is parsed twice when it has a syntax error, and the second parse once dropped every
        /// diagnostic collected so far — the importer's included — and with them the mark the attribution
        /// starts from.
        /// </summary>
        [Fact]
        public void ASyntaxErrorInAnImportKeepsTheImportersDiagnosticsAndIsAttributedToTheImport()
        {
            const string own = "@%<x:nosuch>{{X}}%@";
            var alone = Analyze(own + "tail", rootPath: CorpusDir);
            Assert.NotEmpty(alone.Diagnostics);

            var a = Analyze(own + "@<<{{broken-lib.heddle}}tail", rootPath: CorpusDir);

            Assert.True(a.Diagnostics.Count > alone.Diagnostics.Count,
                string.Join(" | ", a.Diagnostics.Select(x => x.Id + "@" + x.Offset + " " + x.ImportedFrom)));
            for (int i = 0; i < alone.Diagnostics.Count; i++)
            {
                Assert.Equal(alone.Diagnostics[i].Id, a.Diagnostics[i].Id);
                Assert.Equal(alone.Diagnostics[i].Offset, a.Diagnostics[i].Offset);
                Assert.Null(a.Diagnostics[i].ImportedFrom);
            }

            foreach (var imported in a.Diagnostics.Skip(alone.Diagnostics.Count))
            {
                Assert.Contains("broken-lib.heddle", imported.ImportedFrom);
                Assert.Equal(own.Length, imported.Offset);
                Assert.Equal(0, imported.Length);
            }
        }

        /// <summary>
        /// A buffer whose import does not exist yet is an ordinary editing state — the path is half typed, or the
        /// file is mid-rename. The read runs inside the parse tree walk, which the analyzer's own guard does not
        /// cover, so it threw straight out of the analysis and the document got no diagnostics at all: not for the
        /// missing import, and not for anything else in it either.
        /// </summary>
        [Fact]
        public void AMissingImportIsReportedInsteadOfEndingTheAnalysis()
        {
            var a = Analyze("@model(){{Corpus.Blog}}\n@<<{{no-such-lib.heddle}}@\\\n@(Nonexistent)",
                rootPath: CorpusDir);

            Assert.Contains(a.Diagnostics, x => x.Id == "HED4009");
            // The rest of the document is still analysed.
            Assert.Contains(a.Diagnostics, x => x.Id == "HED0001");
        }

        // ---- Extension parameters surface transitively through HeddleCompiler.Compile ----

        [Fact]
        public void ParameterDeclaringExtensionCallCompilesCleanInLsp()
        {
            var a = Analyze("@model(){{Corpus.Blog}}\n@gridlsp(Title, columns: 4)");
            Assert.Empty(a.Diagnostics);
        }

        [Fact]
        public void UnknownExtensionParameterProducesHed5001InLsp()
        {
            var a = Analyze("@model(){{Corpus.Blog}}\n@gridlsp(Title, rows: 4)");
            var d = a.Diagnostics.FirstOrDefault(x => x.Id == "HED5001");
            Assert.NotNull(d);
            Assert.Equal(HeddleDiagnosticSeverity.Error, d.Severity);
        }

        [Fact]
        public void NamedArgsOnParameterLessExtensionProduceHed5005InLsp()
        {
            var a = Analyze("@model(){{Corpus.Blog}}\n@badge(Title, p: 1)");
            Assert.Contains(a.Diagnostics, x => x.Id == "HED5005");
        }

        [Fact]
        public void CleanTemplateHasNoDiagnostics()
        {
            var a = Analyze("@model(){{Corpus.Blog}}\n@(Title)");
            Assert.Empty(a.Diagnostics);
        }
    }
}
