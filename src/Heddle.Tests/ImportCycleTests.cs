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
