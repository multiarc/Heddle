using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Heddle.Data;
using Heddle.Strings.Core;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// Pins the diagnostic-ID plumbing: the null-preserving ToString formats and
    /// the one-to-one match between HeddleDiagnosticIds constants and the Diagnostics table.
    /// </summary>
    public class DiagnosticIdTests
    {
        [Fact]
        public void ToStringOmitsIdWhenNull()
        {
            var error = new HeddleCompileError { Error = "boom", Position = new BlockPosition(0, 0) };
            Assert.Equal("[0:0]boom\r\n", error.ToString());
        }

        [Fact]
        public void ToStringIncludesIdWhenSet()
        {
            var error = new HeddleCompileError
            {
                Error = "boom",
                Position = new BlockPosition(3, 2),
                DiagnosticId = HeddleDiagnosticIds.MethodCallNotAvailable
            };
            Assert.Equal("[3:2]HED1003: boom\r\n", error.ToString());
        }

        [Fact]
        public void ToStringExplicitOverloadIncludesId()
        {
            var error = new HeddleCompileError
            {
                Error = "boom",
                DiagnosticId = HeddleDiagnosticIds.SyntaxError
            };
            Assert.Equal("[1,2:3]HED0003: boom\r\n", error.ToString(1, 2, 3));
        }

        [Fact]
        public void ConstantsMatchTheDiagnosticsTableOneToOne()
        {
            var expected = new HashSet<string>
            {
                "HED0001", "HED0002", "HED0003", "HED0004", "HED0005",
                "HED1001", "HED1002", "HED1003", "HED1004", "HED1005", "HED1006", "HED1007",
                "HED1008", "HED1009", "HED1010", "HED1011", "HED1012", "HED1013", "HED1014",
                "HED1015", "HED1016", "HED1017",
                // HED1018: constant integer/decimal division by zero, refused at compile time by BOTH tiers
                // under this one id -- the engine raises it and the generator forwards it, per the
                // same-fact-same-id rule in the claimed-ID registry.
                "HED1018",
                "HED2001", "HED2002", "HED2003", "HED2004",
                "HED3001", "HED3002", "HED3003", "HED3004", "HED3005",
                "HED4001", "HED4002", "HED4003", "HED4004", "HED4005", "HED4006", "HED4007", "HED4008", "HED4009",
                "HED5001", "HED5002", "HED5003", "HED5004", "HED5005", "HED5006",
                "HED5007", "HED5008", "HED5009", "HED5010", "HED5011", "HED5012",
                "HED5013", "HED5014", "HED5015", "HED5016", "HED5017", "HED5018",
                "HED5019", "HED5020",
                // HED7xxx: generator/engine shared rule cores; ids were Roslyn descriptors but lacked reflectable home.
                "HED7001", "HED7002", "HED7003", "HED7004", "HED7005", "HED7006", "HED7007",
                "HED7008", "HED7009", "HED7010", "HED7011", "HED7012", "HED7013", "HED7014",
                "HED7015", "HED7016", "HED7017", "HED7018", "HED7019", "HED7020", "HED7021", "HED7022",
                "HED7023", "HED7024", "HED7025", "HED7028", "HED7030",
                // HED7031: the emitter declined to precompile and the generator now says so. Before it, the
                // decline was silent -- no source, no manifest row, no diagnostic -- so a template could
                // render dynamically forever while the project believed it was precompiled.
                "HED7031",
                // HED7032: an @model directive and ModelType item metadata resolving to different types.
                "HED7032",
                // HED7033: an extension declaring [PrecompileUnsupported] -- the author's own statement that a
                // static initializer cannot reproduce its hook, costing that call site and not the template.
                "HED7033",
                // HED7034: the build could not observe a real engine compile -- a note under Auto and an error
                // under Strict, never a statement about a template.
                "HED7034",
                "HED7101", "HED7102", "HED7103",
                // HED7104: registered name also answered by another template; runtime id due to cross-assembly collision.
                "HED7104"
            };

            var constants = typeof(HeddleDiagnosticIds)
                .GetFields()
                .Where(f => f.IsLiteral && f.FieldType == typeof(string))
                .Select(f => (string)f.GetRawConstantValue())
                .ToList();

            Assert.Equal(expected.Count, constants.Count);
            Assert.Equal(expected, new HashSet<string>(constants));
            Assert.Equal(constants.Count, constants.Distinct().Count());
        }

        /// <summary>
        /// The claimed-ID registry and <see cref="HeddleDiagnosticIds"/> agree bidirectionally over **every** block a
        /// constant exists in — including `HED7xxx`, which the generator suite also checks from its own side. The only
        /// exclusion is <see cref="RegistryOnly"/>: a claimed id with no public constant. `HED6xxx`/`HED8xxx` are
        /// reserved and claim nothing, so they need no exclusion.
        /// </summary>
        [Fact]
        public void ConstantsAndTheClaimedIdRegistryAgree()
        {
            var deliberatelyUnclaimed = new HashSet<string>(StringComparer.Ordinal);
            var claimed = ClaimedIds(ReadSpec("cross-cutting-decisions.md"), deliberatelyUnclaimed);
            var constants = new HashSet<string>(typeof(HeddleDiagnosticIds)
                .GetFields()
                .Where(f => f.IsLiteral && f.FieldType == typeof(string))
                .Select(f => (string)f.GetRawConstantValue()));

            var unclaimed = constants.Where(id => !claimed.Contains(id)).OrderBy(id => id, StringComparer.Ordinal);
            Assert.True(!unclaimed.Any(),
                "HeddleDiagnosticIds constants missing a claimed-ID registry row: " + string.Join(", ", unclaimed));

            var missing = claimed
                .Where(id => !RegistryOnly.Contains(id) && !constants.Contains(id))
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToList();
            Assert.True(missing.Count == 0,
                "Claimed registry rows with no HeddleDiagnosticIds constant: " + string.Join(", ", missing));

            // The inverse, so an unclaimed row is a gate rather than a note: taking one of those ids without
            // rewriting its row reddens here.
            var takenAnyway = deliberatelyUnclaimed
                .Where(constants.Contains)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToList();
            Assert.True(takenAnyway.Count == 0,
                "Registry rows marked '" + UnclaimedMarker + "' whose id now has a constant: " +
                string.Join(", ", takenAnyway));
            Assert.True(deliberatelyUnclaimed.Count > 0,
                "No '" + UnclaimedMarker + "' rows parsed — the marker or the row shape changed.");
        }

        /// <summary>
        /// Every shipped diagnostic id is named in at least one published document, and where the registry's owner
        /// column names an owning document, the id is named in <b>that</b> one. Before this gate, 16 of 85 shipped ids
        /// appeared in no user-facing page at all — worst of them the native-expression block, whose registry-designated
        /// home named 4 of its 17 — and every existing gate was green, because none of them read published prose.
        /// <para>There is deliberately no exemption set. An empty one would be a concept with no members; whoever finds
        /// an id that genuinely cannot be documented adds the mechanism together with the reason.</para>
        /// </summary>
        [Fact]
        public void EveryShippedIdIsNamedInAPublishedDocument()
        {
            var published = PublishedDocs();
            var constants = new HashSet<string>(typeof(HeddleDiagnosticIds)
                .GetFields()
                .Where(f => f.IsLiteral && f.FieldType == typeof(string))
                .Select(f => (string)f.GetRawConstantValue()));

            var undocumented = constants
                .Where(id => !published.Values.Any(text => NamesId(text, id)))
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToList();
            Assert.True(undocumented.Count == 0,
                "Shipped diagnostic ids named in no published document: " + string.Join(", ", undocumented) +
                ". Document each one in the page the claimed-ID registry names as its owner.");

            var misfiled = new List<string>();
            foreach (var pair in RegistryOwners())
            {
                if (!constants.Contains(pair.Key))
                    continue;
                if (!published.TryGetValue(pair.Value, out var text))
                {
                    misfiled.Add(pair.Key + " → " + pair.Value + " (owning document not found)");
                    continue;
                }

                if (!NamesId(text, pair.Key))
                    misfiled.Add(pair.Key + " → " + pair.Value);
            }

            Assert.True(misfiled.Count == 0,
                "Diagnostic ids absent from the document the registry names as their owner: " +
                string.Join(", ", misfiled.OrderBy(m => m, StringComparer.Ordinal)));
        }

        /// <summary>Whole-token match, so a typo'd id that merely contains a real one ("HED3005x") does not satisfy
        /// the gate for the id it swallowed.</summary>
        private static bool NamesId(string text, string id)
        {
            return Regex.IsMatch(text, @"\bHED" + id.Substring(3) + @"\b");
        }

        /// <summary>Every published page, keyed by file name — <c>docs/*.md</c> only. The spec tree is
        /// excluded on purpose: it is not what a user reads.</summary>
        private static Dictionary<string, string> PublishedDocs([CallerFilePath] string here = null)
        {
            var dir = Path.Combine(RepoRoot(here), "docs");
            var docs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in Directory.GetFiles(dir, "*.md"))
                docs[Path.GetFileName(file)] = File.ReadAllText(file);

            Assert.True(docs.Count > 5, "Published document set looks wrong: " + docs.Count + " files under " + dir);
            return docs;
        }

        /// <summary>Registry id → owning document file name, for the rows whose owner cell links to a published page.
        /// Rows whose owner is prose ("Core engine", "this registry row is the live normative home") have no owning
        /// document and are covered by the any-document leg alone.</summary>
        private static List<KeyValuePair<string, string>> RegistryOwners()
        {
            var markdown = RegistrySection(ReadSpec("cross-cutting-decisions.md"));
            var owners = new List<KeyValuePair<string, string>>();
            foreach (Match match in Regex.Matches(markdown,
                         @"^\| `HED(?<from>\d{4})`(?:[–-]`HED(?<to>\d{4})`)? \| \[[^\]]+\]\((?<link>[^)#]+)",
                         RegexOptions.Multiline))
            {
                var ids = new HashSet<string>(StringComparer.Ordinal);
                AddRange(ids, match);
                var doc = Path.GetFileName(match.Groups["link"].Value);
                foreach (var id in ids)
                    owners.Add(new KeyValuePair<string, string>(id, doc));
            }

            Assert.True(owners.Count > 0, "No owner-linked registry rows parsed — the row shape changed.");
            return owners;
        }

        /// <summary>Claimed IDs not surfaced as public constants (e.g., HED9001, the feature-switch guard).</summary>
        private static readonly HashSet<string> RegistryOnly = new HashSet<string>(StringComparer.Ordinal)
        {
            "HED9001"
        };

        /// <summary>Marks a registry row for an id deliberately left free; avoids a silent whitelist.</summary>
        private const string UnclaimedMarker = "deliberately unclaimed";

        private static void AddRange(HashSet<string> into, Match match)
        {
            var from = int.Parse(match.Groups["from"].Value, CultureInfo.InvariantCulture);
            var to = match.Groups["to"].Success
                ? int.Parse(match.Groups["to"].Value, CultureInfo.InvariantCulture)
                : from;
            for (var i = from; i <= to; i++)
                into.Add("HED" + i.ToString("D4", CultureInfo.InvariantCulture));
        }

        /// <summary>The registry table alone — block reservations elsewhere in the document are not id claims.</summary>
        private static string RegistrySection(string markdown)
        {
            const string heading = "## Claimed diagnostic IDs (registry)";
            var start = markdown.IndexOf(heading, StringComparison.Ordinal);
            Assert.True(start >= 0, "Claimed-ID registry section '" + heading + "' not found.");
            var end = markdown.IndexOf("\n## ", start + heading.Length, StringComparison.Ordinal);
            return end < 0 ? markdown.Substring(start) : markdown.Substring(start, end - start);
        }

        private static HashSet<string> ClaimedIds(string markdown, HashSet<string> unclaimed = null)
        {
            markdown = RegistrySection(markdown);

            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (Match match in Regex.Matches(markdown,
                         @"^\| `HED(?<from>\d{4})`(?:[–-]`HED(?<to>\d{4})`)? \|(?<rest>[^\n]*)",
                         RegexOptions.Multiline))
            {
                if (match.Groups["rest"].Value.IndexOf(UnclaimedMarker, StringComparison.Ordinal) >= 0)
                {
                    if (unclaimed != null)
                        AddRange(unclaimed, match);
                    continue;
                }

                AddRange(ids, match);
            }

            Assert.True(ids.Count > 0,
                "No claimed-ID rows parsed. Expected rows of the form: | `HEDaaaa` | … | or " +
                "| `HEDaaaa`–`HEDbbbb` | … |");
            return ids;
        }

        /// <summary>The repository root, from this test source's own path, so the gates work on every TFM
        /// (including net48) without probing the output directory.</summary>
        private static string RepoRoot(string here)
        {
            return Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here), "..", ".."));
        }

        private static string ReadSpec(string name, [CallerFilePath] string here = null)
        {
            return File.ReadAllText(Path.Combine(RepoRoot(here), "docs", "spec", "common", name));
        }
    }
}
