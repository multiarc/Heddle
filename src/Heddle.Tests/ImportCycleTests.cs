using System.Collections.Generic;
using System.Linq;
using Heddle.Data;
using Heddle.Language;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// An <c>@&lt;&lt;</c> import parses the imported document in place, so a document that imports its way back to
    /// one already being parsed used to recurse until the stack ran out. A <c>StackOverflowException</c> cannot be
    /// caught, so the whole process died — on a template typo, and on anything a user could author. The contract is
    /// that a malformed template becomes an entry in the error list; these hold it to that.
    /// </summary>
    public class ImportCycleTests
    {
        [Fact]
        public void ATwoDocumentImportCycleIsAnErrorNotAStackOverflow()
        {
            var library = new Dictionary<string, string>
            {
                ["a.heddle"] = "@<<{{b.heddle}}@\\\nfrom a",
                ["b.heddle"] = "@<<{{a.heddle}}@\\\nfrom b"
            };

            var context = Parse("@<<{{a.heddle}}@\\\nroot", library);

            Assert.Contains(context.Errors, e => e.DiagnosticId == HeddleDiagnosticIds.ComposeImportCycle);
        }

        [Fact]
        public void ADocumentImportingItselfIsAnErrorNotAStackOverflow()
        {
            var library = new Dictionary<string, string> { ["self.heddle"] = "@<<{{self.heddle}}@\\\nfrom self" };

            var context = Parse("@<<{{self.heddle}}@\\\nroot", library);

            Assert.Contains(context.Errors, e => e.DiagnosticId == HeddleDiagnosticIds.ComposeImportCycle);
        }

        /// <summary>The cycle report names the path that closes it, so the author can see which import to remove
        /// rather than being told only that one exists.</summary>
        [Fact]
        public void TheCycleErrorNamesTheDocumentsInvolved()
        {
            var library = new Dictionary<string, string>
            {
                ["a.heddle"] = "@<<{{b.heddle}}@\\\nfrom a",
                ["b.heddle"] = "@<<{{a.heddle}}@\\\nfrom b"
            };

            var context = Parse("@<<{{a.heddle}}@\\\nroot", library);
            var cycle = context.Errors.First(e => e.DiagnosticId == HeddleDiagnosticIds.ComposeImportCycle);

            Assert.Contains("a.heddle", cycle.Error);
            Assert.Contains("b.heddle", cycle.Error);
        }

        /// <summary>The same document imported twice on separate branches is not a cycle, and must keep working —
        /// a guard that rejected any repeat would break sharing a common library.</summary>
        [Fact]
        public void TheSameImportOnTwoSeparateBranchesIsNotACycle()
        {
            var library = new Dictionary<string, string>
            {
                ["shared.heddle"] = "@%<shared>{{s}} :: dynamic%@",
                ["first.heddle"] = "@<<{{shared.heddle}}@\\\n",
                ["second.heddle"] = "@<<{{shared.heddle}}@\\\n"
            };

            var context = Parse("@<<{{first.heddle}}@\\\n@<<{{second.heddle}}@\\\nroot", library);

            Assert.DoesNotContain(context.Errors, e => e.DiagnosticId == HeddleDiagnosticIds.ComposeImportCycle);
        }

        /// <summary>
        /// Depth is a separate question from repetition, and the cycle guard cannot see it. A chain of thousands of
        /// distinct files contains no cycle at all and still parses itself onto the floor, because every import
        /// parses in place. Five thousand twenty-byte files did exactly that.
        /// </summary>
        [Fact]
        public void AnUnboundedImportChainIsReportedInsteadOfKillingTheProcess()
        {
            const int length = 4000;
            var library = new Dictionary<string, string>();
            for (var i = 0; i < length; i++)
                library["f" + i + ".heddle"] = "@<<{{f" + (i + 1) + ".heddle}}@\\\n";
            library["f" + length + ".heddle"] = "end";

            var context = Parse("@<<{{f0.heddle}}@\\\nroot", library);

            Assert.Contains(context.Errors, e => e.DiagnosticId == HeddleDiagnosticIds.TemplateNestedTooDeeply);
        }

        /// <summary>
        /// One file is one document however its path is spelled. Keying the cycle guard on the raw spelling let
        /// <c>./a.heddle</c> and <c>d/../a.heddle</c> pass as different documents, so a cycle walked straight past
        /// it — and because every unseen spelling pushed another level, the parse explored permutations: eight
        /// spellings produced 863,109 diagnostics at build time.
        /// </summary>
        [Fact]
        public void ACycleIsCaughtHoweverTheImportPathIsSpelled()
        {
            var spellings = new[] { "a.heddle", "./a.heddle", ".//a.heddle", "d0/../a.heddle", "./d0/../a.heddle" };
            var body = string.Concat(spellings.Select(p => "@<<{{" + p + "}}@\\\n"));
            var library = new Dictionary<string, string>();
            foreach (var spelling in spellings)
                library[spelling] = body;

            var context = Parse(body + "root", library);

            Assert.Contains(context.Errors, e => e.DiagnosticId == HeddleDiagnosticIds.ComposeImportCycle);
            Assert.True(context.Errors.Count < 200,
                "cycle reporting must not multiply across spellings; got " + context.Errors.Count);
        }

        private static ParseContext Parse(string document, IReadOnlyDictionary<string, string> library)
        {
            var settings = new ParserSettings
            {
                RootPath = "<none>",
                ImportReader = path => library.TryGetValue(path, out var text) ? text : string.Empty
            };

            return DocumentParser.Parse(document, settings, out _);
        }
    }
}
