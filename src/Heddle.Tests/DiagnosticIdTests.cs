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
                "HED0001", "HED0002", "HED0003", "HED0004",
                "HED1001", "HED1002", "HED1003", "HED1004", "HED1005", "HED1006", "HED1007",
                "HED1008", "HED1009", "HED1010", "HED1011", "HED1012", "HED1013", "HED1014",
                "HED1015", "HED1016", "HED1017",
                "HED2001", "HED2002", "HED2003", "HED2004",
                "HED3001", "HED3002", "HED3003", "HED3004", "HED3005",
                "HED4001", "HED4002", "HED4003", "HED4004", "HED4005",
                "HED5001", "HED5002", "HED5003", "HED5004", "HED5005", "HED5006",
                "HED5007", "HED5008", "HED5009", "HED5010", "HED5011", "HED5012",
                "HED5013", "HED5014", "HED5015", "HED5016", "HED5017", "HED5018",
                "HED5019", "HED5020",
                // The HED7xxx block gained constants when the generator and engine started sharing rule cores
                // (D12.1) — the ids already shipped, as Roslyn descriptors and PrecompiledFallbackEvent codes;
                // what they lacked was a reflectable home, so nothing could gate them.
                "HED7001", "HED7002", "HED7003", "HED7004", "HED7005", "HED7006", "HED7007",
                "HED7008", "HED7009", "HED7010", "HED7011", "HED7012", "HED7013", "HED7014",
                "HED7015", "HED7016", "HED7017", "HED7018", "HED7019", "HED7020", "HED7021", "HED7022",
                "HED7023", "HED7024", "HED7025", "HED7028",
                "HED7101", "HED7102", "HED7103",
                // A registered Name that another registered template already answers to. A runtime id because
                // the collision spans assemblies — a referenced manifest's rows are IL, not symbol metadata, so the
                // build tier cannot see them (within one compilation the same fault is HED7004).
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
        /// <para>The claimed-ID registry and <see cref="HeddleDiagnosticIds"/> agree in both directions over the
        /// <c>HED0xxx</c>–<c>HED5xxx</c> blocks — no constant outside a claimed row, no claimed runtime row without
        /// a constant. The <c>HED7xxx</c> half is asserted generator-side, where the descriptors live.</para>
        /// <para>Documented exclusions: <c>HED6xxx</c>/<c>HED8xxx</c> are reserved-unclaimed (no four-digit
        /// row), <c>HED9001</c> is deliberately not public surface (the registry row says so), and the
        /// <c>HED7xxx</c> block is the generator/precompiled-runtime block.</para>
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

        /// <summary>Claimed IDs the runtime deliberately does not surface as a public constant, each named here
        /// rather than silently absent. <c>HED9001</c> is the feature-switch guard: its registry row records that
        /// it is an internal id (<c>HeddleFeatures.CSharpTierDisabledDiagnosticId</c>) precisely because that
        /// phase added no public API surface.</summary>
        private static readonly HashSet<string> RegistryOnly = new HashSet<string>(StringComparer.Ordinal)
        {
            "HED9001"
        };

        /// <summary>
        /// Expands the claimed-ID registry table's first cells. Grammar (printed on failure): a row whose first
        /// cell is <c>`HEDaaaa`</c> or <c>`HEDaaaa`–`HEDbbbb`</c>; IDs named in the Owner/Notes cells are
        /// deliberately ignored, so a cross-reference never silently claims an ID.
        /// </summary>
        /// <summary>Marks a registry row recording an id deliberately left free. Such a row is not a claim and must
        /// not demand a constant; making the distinction machine-read avoids a whitelist that grows silently.</summary>
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

        private static HashSet<string> ClaimedIds(string markdown, HashSet<string> unclaimed = null)
        {
            // The registry section only: D1's block-allocation table above it states ranges (HED0001-HED0999)
            // in the same row shape, and a block reservation is not an ID claim.
            const string heading = "## Claimed diagnostic IDs (registry)";
            var start = markdown.IndexOf(heading, StringComparison.Ordinal);
            Assert.True(start >= 0, "Claimed-ID registry section '" + heading + "' not found.");
            var end = markdown.IndexOf("\n## ", start + heading.Length, StringComparison.Ordinal);
            markdown = end < 0 ? markdown.Substring(start) : markdown.Substring(start, end - start);

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

        /// <summary>Locates a spec file from this test source's own path, so the gate works on every TFM
        /// (including net48) without probing the output directory.</summary>
        private static string ReadSpec(string name, [CallerFilePath] string here = null)
        {
            var repo = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here), "..", ".."));
            return File.ReadAllText(Path.Combine(repo, "docs", "spec", "common", name));
        }
    }
}
