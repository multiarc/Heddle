using System.Collections.Generic;
using System.Linq;
using Heddle.Data;
using Heddle.Language;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>An imported document is parsed into the error list of the document that imports it. When the
    /// import has a syntax error it is parsed a second time, in the exact mode, and the errors of the first attempt
    /// are dropped for the second's — which has to mean the import's own first attempt and nothing else. Dropping
    /// the whole list took the importer's diagnostics with it, left the mark the import's errors are attributed
    /// from beyond the end of the list, and could leave the list clean enough for the broken import to be walked
    /// as if it had parsed.</summary>
    public class ImportedSyntaxErrorTests
    {
        private const string Importer = "@%<x:nosuch>{{X}}%@@<<{{broken.heddle}}tail";

        private static ParseContext Parse(string document, bool editor, params string[] files)
        {
            var library = new Dictionary<string, string>();
            for (int i = 0; i + 1 < files.Length; i += 2)
                library[files[i]] = files[i + 1];
            var settings = new ParserSettings
            {
                RootPath = string.Empty,
                ProvideLanguageFeatures = editor,
                ImportIdentifier = path => path,
                ImportReader = path => library.First(pair => path.EndsWith(pair.Key)).Value
            };
            return DocumentParser.Parse(document, settings, out _);
        }

        private static string Describe(ParseContext context)
        {
            return string.Join(" | ", context.Errors.Select(e =>
                e.DiagnosticId + "@" + e.Position.StartIndex + "+" + e.Position.Length +
                (e.ImportOrigin == null ? string.Empty : " from " + e.ImportOrigin.Path)));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ASyntaxErrorInAnImportLeavesTheImportersDiagnosticsInPlace(bool editor)
        {
            var alone = Parse("@%<x:nosuch>{{X}}%@tail", editor);
            Assert.True(alone.Errors.Count > 0, "the importer is expected to carry a diagnostic of its own.");

            var context = Parse(Importer, editor, "broken.heddle", "text @list(Items {{ x }}");

            Assert.True(context.Errors.Count > alone.Errors.Count, Describe(context));
            for (int i = 0; i < alone.Errors.Count; i++)
            {
                Assert.Equal(alone.Errors[i].DiagnosticId, context.Errors[i].DiagnosticId);
                Assert.Equal(alone.Errors[i].Position, context.Errors[i].Position);
                Assert.Null(context.Errors[i].ImportOrigin);
            }

            Assert.Contains(context.Errors, e => e.DiagnosticId == HeddleDiagnosticIds.SyntaxError);
        }

        /// <summary>In the editor each diagnostic of an import carries where it came from, and the definitions
        /// of an import that did not parse are not the importer's.</summary>
        [Fact]
        public void TheEditorAttributesEveryImportedErrorAndTakesNothingFromABrokenImport()
        {
            var context = Parse("@%<x:nosuch>{{X}}%@@<<{{one.heddle}}@<<{{two.heddle}}tail", true,
                "one.heddle", "@%<fromone>{{1}}%@ @list(Items {{ x }}",
                "two.heddle", "@%<fromtwo>{{2}}%@ @(1)}}");

            var own = context.Errors.Where(e => e.ImportOrigin == null).ToList();
            var one = context.Errors.Where(e => e.ImportOrigin != null && e.ImportOrigin.Path.EndsWith("one.heddle")).ToList();
            var two = context.Errors.Where(e => e.ImportOrigin != null && e.ImportOrigin.Path.EndsWith("two.heddle")).ToList();
            Assert.True(own.Count > 0 && one.Count > 0 && two.Count > 0, Describe(context));
            Assert.Equal(context.Errors.Count, own.Count + one.Count + two.Count);

            // In document order: the importer's own, then the first import's, then the second's.
            Assert.Equal(own.Concat(one).Concat(two).ToList(), context.Errors);
            Assert.False(context.DefenitionExists("fromone"), "a broken import's definitions were taken.");
            Assert.False(context.DefenitionExists("fromtwo"), "a broken import's definitions were taken.");
        }

        /// <summary>The editor's diagnostics for one broken document come in the order a single exact parse
        /// reports them — the tokenizer's among the parser's, by where they are — not the tokenizer's first.</summary>
        [Fact]
        public void TheEditorReportsTokenizerErrorsInTheirPlaceAmongTheParsers()
        {
            var context = Parse("@list(Items {{ x }} @(1 ` 2) @(", true);
            Assert.True(context.Errors.Count >= 2, Describe(context));
            var starts = context.Errors.Select(e => e.Position.StartIndex).ToList();
            Assert.True(starts.SequenceEqual(starts.OrderBy(s => s)), Describe(context));
        }
    }
}
