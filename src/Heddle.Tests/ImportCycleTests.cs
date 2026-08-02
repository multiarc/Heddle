using System;
using System.Collections.Generic;
using System.Linq;
using Heddle.Data;
using Heddle.Language;
using Heddle.Precompiled;
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

            // The id is shared with expression and block nesting, which is a different fault with a different
            // remedy; only the message says which of the two a reader is looking at. It also says where the ceiling
            // is — the reader's next question — and that the import was dropped rather than partially expanded.
            var reported = context.Errors
                .Where(e => e.DiagnosticId == HeddleDiagnosticIds.TemplateNestedTooDeeply).ToList();
            Assert.NotEmpty(reported);
            Assert.All(reported, e =>
            {
                Assert.Contains("'@<<' composition imports nest deeper than 64 levels", e.Error);
                Assert.Contains("The import is skipped", e.Error);
            });
        }

        /// <summary>
        /// A host that resolves imports itself — the generator resolves them by template key, from files it was
        /// handed rather than from disk — must have the cycle guard call an import the same document its reader
        /// does. Keying the guard on the file path while the reader keyed on the template key gave one document as
        /// many identities as it had spellings, and the guard walked their permutations before noticing the repeat:
        /// six spellings of one self-importing file were read a thousand times instead of six.
        /// </summary>
        [Fact]
        public void TheCycleGuardFollowsTheReadersOwnNotionOfIdentity()
        {
            var spellings = new[]
            {
                "views/a", "views/a.heddle", "~/views/a.heddle", "/views/a.heddle", "~/views/a", "/views/a"
            };
            var document = string.Concat(spellings.Select(p => "@<<{{" + p + "}}@\\\n"));
            var library = new Dictionary<string, string> { ["views/a.heddle"] = document };
            Func<string, string> identity = path => TemplateKey.TryNormalize(path, out var key) ? key : path;

            var reads = 0;
            var settings = new ParserSettings
            {
                RootPath = string.Empty,
                ImportIdentifier = identity,
                ImportReader = path =>
                {
                    reads++;
                    return library.TryGetValue(identity(path), out var text) ? text : string.Empty;
                }
            };

            var context = DocumentParser.Parse(document, settings, out _);

            Assert.True(reads <= spellings.Length,
                "one document must be read once per import that names it, not once per permutation of its " +
                "spellings; it was read " + reads + " times");
            Assert.Contains(context.Errors, e => e.DiagnosticId == HeddleDiagnosticIds.ComposeImportCycle);
            Assert.DoesNotContain(context.Errors, e => e.DiagnosticId == HeddleDiagnosticIds.ComposeImportFanOut);
        }

        /// <summary>
        /// An import naming a file that is not there is a document state, not a program fault: the path is being
        /// typed, or the file is mid-rename. The read happens inside the tree walk, which the parser's own guard
        /// does not cover, so it threw out of the parse and took every other diagnostic in the document with it.
        /// </summary>
        [Fact]
        public void AnImportThatCannotBeReadIsReportedAndTheRestOfTheDocumentStillParses()
        {
            // A root that is ABSENT, which is the state under test -- not one that is syntactically
            // illegal. The former spelling used "<none>", and .NET Framework's Path.Combine rejects
            // '<' as an illegal path character (ArgumentException) where .NET Core dropped that
            // validation, so the test threw out of its own arrangement on the net48 leg before the
            // parser was ever reached.
            var settings = new ParserSettings
            {
                RootPath = System.IO.Path.Combine("no-such-root", "no-such-directory")
            };

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
        /// <summary>
        /// The bound itself, not merely that some bound is enforced. Asserting only against
        /// <see cref="ParserSettings.MaxImportExpansions"/> agrees with any value it is raised to — the same defect
        /// this file already fixed for the cycle-report budget, and missed for the bound added beside it.
        /// </summary>
        [Fact]
        public void TheFanOutBoundIsTheValueThatWasChosen()
        {
            Assert.Equal(16384, ParserSettings.MaxImportExpansions);
        }

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
                Assert.Equal(ParserSettings.MaxImportExpansions - 1,
                    ImportParseState.Current.ImportExpansionsRemaining);
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
            // satisfied by the unguarded behaviour. The bound is written out rather than read from the engine:
            // comparing against the budget itself made the test agree with any budget, including one raised past
            // what the fixture can produce, at which point it stops observing anything.
            Assert.True(context.Errors.Count <= 32,
                "cycle descriptions must stay bounded; got " + context.Errors.Count);
            Assert.Equal(32, ParserSettings.DefaultCycleReportBudget);
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

        /// <summary>
        /// <see cref="ParserSettings"/> is a public configuration object a host is invited to build once and reuse,
        /// and <see cref="DocumentParser.Parse(string, ParserSettings, out string)"/> takes it. The import stack and
        /// the report budgets lived on it, so two documents parsing at once shared them: each saw the other's
        /// imports as its own and reported cycles and depth limits that were not there, and the cycle message —
        /// built by joining that stack while another thread appended to it — threw straight out of the parse.
        /// </summary>
        [Fact]
        public void OneSettingsObjectServesConcurrentParsesWithoutCrossTalk()
        {
            var library = new Dictionary<string, string>
            {
                ["a.heddle"] = "@<<{{a1.heddle}}@\\\n",
                ["a1.heddle"] = "@<<{{a2.heddle}}@\\\n",
                ["a2.heddle"] = "@%<a>{{A}} :: dynamic%@",
                ["b.heddle"] = "@<<{{b1.heddle}}@\\\n",
                ["b1.heddle"] = "@<<{{b2.heddle}}@\\\n",
                ["b2.heddle"] = "@%<b>{{B}} :: dynamic%@"
            };
            var settings = new ParserSettings
            {
                RootPath = "<none>",
                ImportReader = path => library.TryGetValue(path, out var text) ? text : string.Empty
            };

            var faults = new System.Collections.Concurrent.ConcurrentQueue<string>();
            System.Threading.Tasks.Parallel.For(0, 64, i =>
            {
                var root = i % 2 == 0 ? "a" : "b";
                try
                {
                    var context = DocumentParser.Parse("@<<{{" + root + ".heddle}}@\\\nroot", settings, out _);
                    foreach (var error in context.Errors)
                        faults.Enqueue(error.DiagnosticId + ": " + error.Error);
                }
                catch (Exception e)
                {
                    faults.Enqueue(e.GetType().Name + ": " + e.Message);
                }
            });

            Assert.True(faults.IsEmpty,
                "a shared settings object must not make one parse's imports visible to another; " + faults.Count +
                " faults, first: " + faults.FirstOrDefault());
        }


        /// <summary>
        /// <see cref="ParserSettings.ImportReader"/> is a public seam a host supplies, so it is untrusted input to
        /// the parser. The two idiomatic ways for one to say "no such import" — returning null, and letting a lookup
        /// throw — both escaped the read guard and took the parse down, which is the failure the guard was added to
        /// stop for the disk reader.
        /// </summary>
        [Fact]
        public void AnImportReaderThatReturnsNullIsReportedRatherThanThrown()
        {
            var settings = new ParserSettings { RootPath = "<none>", ImportReader = _ => null };

            var context = DocumentParser.Parse("@<<{{gone.heddle}}@\\\ntail", settings, out _);

            Assert.Contains(context.Errors, e => e.DiagnosticId == HeddleDiagnosticIds.ComposeImportUnreadable);
        }

        [Fact]
        public void AnImportReaderThatThrowsIsReportedRatherThanThrown()
        {
            var library = new Dictionary<string, string>();
            var settings = new ParserSettings { RootPath = "<none>", ImportReader = path => library[path] };

            var context = DocumentParser.Parse("@<<{{gone.heddle}}@\\\ntail", settings, out _);

            Assert.Contains(context.Errors, e => e.DiagnosticId == HeddleDiagnosticIds.ComposeImportUnreadable);
        }


        /// <summary>
        /// A host may resolve an import by parsing — a registry that compiles lazily does exactly that — which
        /// re-enters the parser on the same thread while an import stack is already on it. That inner parse is its
        /// own: it must not see the outer parse's imports as its own and report a cycle that is not there, and it
        /// must hand the outer parse's state back untouched.
        /// <para>The reader that re-enters runs one level down (an import inside an import), because a reader runs
        /// before its own import's name is pushed — a top-level reader sees an empty stack and proves nothing. The
        /// captured depth is asserted so the fixture cannot silently fall back to the shallow shape that made an
        /// earlier version of this test unable to fail.</para>
        /// </summary>
        [Fact]
        public void AParseBegunInsideAnImportReaderIsIndependentOfTheOuterParse()
        {
            var innerLibrary = new Dictionary<string, string> { ["x.heddle"] = "inner body" };
            var innerErrors = new List<string>();
            var importsOnStackWhenReaderRan = -1;

            var settings = new ParserSettings
            {
                RootPath = "<none>",
                ImportReader = path =>
                {
                    if (path == "x.heddle")
                        return "@<<{{y.heddle}}@\\\nmiddle";

                    // The reader for y.heddle runs while x.heddle is being expanded, so the outer parse's import
                    // stack is non-empty here. Resolving by parsing, with a document that imports the same name
                    // the outer parse is expanding.
                    importsOnStackWhenReaderRan = ImportParseState.Current.ActiveImports.Count;
                    var nested = DocumentParser.Parse("@<<{{x.heddle}}@\\\nnested", new ParserSettings
                    {
                        RootPath = "<none>",
                        ImportReader = p => innerLibrary.TryGetValue(p, out var t) ? t : string.Empty
                    }, out _);
                    innerErrors.AddRange(nested.Errors.Select(e => e.DiagnosticId));
                    return "leaf";
                }
            };

            var context = DocumentParser.Parse("@<<{{x.heddle}}@\\\nroot", settings, out _);

            Assert.Equal(1, importsOnStackWhenReaderRan);
            Assert.DoesNotContain(HeddleDiagnosticIds.ComposeImportCycle, innerErrors);
            Assert.Empty(context.Errors);
            Assert.Empty(ImportParseState.Current.ActiveImports);
        }

        /// <summary>
        /// An import path the file system cannot canonicalise. The identity the cycle guard keys on is normally
        /// <c>Path.GetFullPath</c>, which rejects an embedded NUL here and rejects several more characters on .NET
        /// Framework — and the whole parse ran inside that call. A path a reader could not have resolved anyway is
        /// no reason to lose the document's diagnostics along with it; the raw spelling is a worse key, not a
        /// broken one, and it still tells two imports apart.
        /// </summary>
        [Fact]
        public void AnImportPathThatCannotBeCanonicalisedStillParses()
        {
            var library = new Dictionary<string, string> { ["ok.heddle"] = "imported" };
            var context = Parse("@<<{{bad\0name.heddle}}@\\\n@<<{{ok.heddle}}@\\\nroot", library);

            Assert.DoesNotContain(context.Errors, e => e.DiagnosticId == HeddleDiagnosticIds.ComposeImportCycle);
            Assert.DoesNotContain(context.Errors, e => e.DiagnosticId == HeddleDiagnosticIds.TemplateNestedTooDeeply);
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
