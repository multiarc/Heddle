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
        /// An import naming a file that is not there is a document state, not a program fault: the path is being
        /// typed, or the file is mid-rename. The read happens inside the tree walk, which the parser's own guard
        /// does not cover, so it threw out of the parse and took every other diagnostic in the document with it.
        /// </summary>
        [Fact]
        public void AnImportThatCannotBeReadIsReportedAndTheRestOfTheDocumentStillParses()
        {
            var settings = new ParserSettings { RootPath = System.IO.Path.Combine("<none>", "no-such-directory") };

            var context = DocumentParser.Parse(
                "@<<{{missing.heddle}}@\\\n@import(){{legacy.heddle}}@\\\ntail", settings, out var clean);

            Assert.Contains(context.Errors, e => e.DiagnosticId == HeddleDiagnosticIds.ComposeImportUnreadable);
            Assert.Contains(context.Errors, e => e.DiagnosticId == HeddleDiagnosticIds.LegacyImportDirective);
            Assert.Contains("tail", clean);
        }

        /// <summary>
        /// Repetition and depth are not the only ways an import graph grows. A document already parsed and popped is
        /// parsed again the next time it is reached, so a graph where every file imports the next one twice is
        /// acyclic, twenty levels deep, and expands to two million parses with nothing reported — and at the depth
        /// ceiling, to more parses than a machine will ever finish. The total is bounded and the overflow described.
        /// </summary>
        [Fact]
        public void AnAcyclicImportFanOutIsBoundedAndReported()
        {
            const int levels = 20;
            var library = new Dictionary<string, string>();
            for (var i = 0; i < levels; i++)
            {
                var next = "@<<{{f" + (i + 1) + ".heddle}}@\\\n";
                library["f" + i + ".heddle"] = next + next;
            }

            library["f" + levels + ".heddle"] = "leaf";

            var reads = 0;
            var settings = new ParserSettings
            {
                RootPath = "<none>",
                ImportReader = path =>
                {
                    reads++;
                    return library.TryGetValue(path, out var text) ? text : string.Empty;
                }
            };

            var context = DocumentParser.Parse("@<<{{f0.heddle}}@\\\nroot", settings, out _);

            Assert.True(reads <= ParserSettings.MaxImportExpansions,
                "an acyclic import graph must not multiply out; it read " + reads + " documents");
            Assert.Contains(context.Errors, e => e.DiagnosticId == HeddleDiagnosticIds.ComposeImportFanOut);
        }

        /// <summary>The overflow is described once. Every import past the bound is skipped, and a graph that reaches
        /// the bound has thousands of them left — one diagnostic per skip would bury the document's real errors.</summary>
        [Fact]
        public void TheFanOutOverflowIsDescribedOnce()
        {
            const int levels = 20;
            var library = new Dictionary<string, string>();
            for (var i = 0; i < levels; i++)
            {
                var next = "@<<{{f" + (i + 1) + ".heddle}}@\\\n";
                library["f" + i + ".heddle"] = next + next;
            }

            library["f" + levels + ".heddle"] = "leaf";

            var context = Parse("@<<{{f0.heddle}}@\\\nroot", library);

            Assert.Single(context.Errors, e => e.DiagnosticId == HeddleDiagnosticIds.ComposeImportFanOut);
        }

        /// <summary>The bound is per parse, not per settings object: a host reusing one must not find its second
        /// document refused because the first spent the budget.</summary>
        [Fact]
        public void TheFanOutBudgetIsRestoredForEachTopLevelParse()
        {
            var library = new Dictionary<string, string>
            {
                ["lib.heddle"] = "@%<lib>{{l}} :: dynamic%@"
            };
            var settings = new ParserSettings
            {
                RootPath = "<none>",
                ImportReader = path => library.TryGetValue(path, out var text) ? text : string.Empty
            };

            for (var round = 0; round < 3; round++)
            {
                DocumentParser.Parse("@<<{{lib.heddle}}@\\\nroot", settings, out _);
                Assert.Equal(ParserSettings.MaxImportExpansions - 1, settings.ImportExpansionsRemaining);
            }
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

        /// <summary>
        /// Two documents, each importing the other under a <b>different spelling of the other's path</b> — so no raw
        /// spelling ever repeats, and only a normalised key can see the cycle. The sibling test above cannot
        /// distinguish: its documents each import their own spelling, so the raw key repeats at depth two and the
        /// cycle is reported either way.
        /// </summary>
        [Fact]
        public void ACycleWhoseSpellingsNeverRepeatIsStillCaught()
        {
            var library = new Dictionary<string, string>
            {
                ["a.heddle"] = "@<<{{./b.heddle}}@\\\n",
                ["./b.heddle"] = "@<<{{d0/../a.heddle}}@\\\n",
                ["d0/../a.heddle"] = "@<<{{.//b.heddle}}@\\\n",
                [".//b.heddle"] = "@<<{{./d0/../a.heddle}}@\\\n",
                ["./d0/../a.heddle"] = "end"
            };

            var context = Parse("@<<{{a.heddle}}@\\\nroot", library);

            Assert.Contains(context.Errors, e => e.DiagnosticId == HeddleDiagnosticIds.ComposeImportCycle);
        }

        /// <summary>A cycle reached under many spellings must not be described combinatorially. Eight spellings once
        /// produced 863,109 diagnostics at build time; the budget caps the description, never the skipping.</summary>
        [Fact]
        public void CycleReportingIsBoundedHoweverManySpellingsReachIt()
        {
            var spellings = Enumerable.Range(0, 8).Select(i => string.Concat(Enumerable.Repeat("./", i)) + "a.heddle")
                .ToArray();
            var body = string.Concat(spellings.Select(p => "@<<{{" + p + "}}@\\\n"));
            var library = spellings.ToDictionary(p => p, _ => body);

            var context = Parse(body + "root", library);

            Assert.Contains(context.Errors, e => e.DiagnosticId == HeddleDiagnosticIds.ComposeImportCycle);
            // 64 is what this fixture produces with the budget removed, so asserting that bound would have been
            // satisfied by the unguarded behaviour. The budget is 32.
            Assert.True(context.Errors.Count <= ParserSettings.DefaultCycleReportBudget,
                "cycle descriptions must stay bounded; got " + context.Errors.Count);
        }

        /// <summary>
        /// A settings object is reusable, and both the type and the overload taking it are public. The cycle-report
        /// budget lived on it and was never restored, so a host that reused one stopped reporting cycles after the
        /// thirty-second parse — still skipping the import, but silently.
        /// </summary>
        [Fact]
        public void ReusingOneSettingsObjectKeepsReportingCycles()
        {
            var library = new Dictionary<string, string>
            {
                ["a.heddle"] = "@<<{{b.heddle}}@\\\nfrom a",
                ["b.heddle"] = "@<<{{a.heddle}}@\\\nfrom b"
            };
            var settings = new ParserSettings
            {
                RootPath = "<none>",
                ImportReader = path => library.TryGetValue(path, out var text) ? text : string.Empty
            };

            for (var round = 0; round < 40; round++)
            {
                var context = DocumentParser.Parse("@<<{{a.heddle}}@\\\nroot", settings, out _);
                Assert.True(
                    context.Errors.Any(e => e.DiagnosticId == HeddleDiagnosticIds.ComposeImportCycle),
                    "the cycle went unreported on round " + round);
            }
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
