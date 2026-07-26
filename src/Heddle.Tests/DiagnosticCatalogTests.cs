using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Heddle.Data;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// Generator plan phase 6 D12.2/D12.4 — the runtime half of the catalog gates. Diagnostic identity
    /// (id → title, severity, and the message knowledge that genuinely has two consumers) lives in exactly one
    /// code-side table; these keep it bijective with <see cref="HeddleDiagnosticIds"/> and keep every populated
    /// format string well-formed. The descriptor-equality half runs generator-side, where Roslyn types live.
    /// </summary>
    public class DiagnosticCatalogTests
    {
        private static IEnumerable<string> Constants() =>
            typeof(HeddleDiagnosticIds)
                .GetFields()
                .Where(f => f.IsLiteral && f.FieldType == typeof(string))
                .Select(f => (string)f.GetRawConstantValue());

        [Fact]
        public void EveryConstantHasExactlyOneCatalogRowAndViceVersa()
        {
            var constants = new HashSet<string>(Constants(), StringComparer.Ordinal);
            var rows = HeddleDiagnosticCatalog.All.Select(r => r.Id).ToList();

            Assert.Equal(rows.Count, rows.Distinct().Count());
            var uncatalogued = constants.Where(id => !rows.Contains(id)).OrderBy(id => id, StringComparer.Ordinal);
            Assert.True(!uncatalogued.Any(),
                "HeddleDiagnosticIds constants with no catalog row: " + string.Join(", ", uncatalogued));
            var orphaned = rows.Where(id => !constants.Contains(id)).OrderBy(id => id, StringComparer.Ordinal);
            Assert.True(!orphaned.Any(),
                "Catalog rows with no HeddleDiagnosticIds constant: " + string.Join(", ", orphaned));
        }

        [Fact]
        public void EveryRowCarriesATitleAndResolvesByLookup()
        {
            foreach (var row in HeddleDiagnosticCatalog.All)
            {
                Assert.False(string.IsNullOrWhiteSpace(row.Title), row.Id + " has no title");
                Assert.True(HeddleDiagnosticCatalog.TryGet(row.Id, out var looked));
                Assert.Equal(row.Title, looked.Title);
                Assert.Equal(row.DefaultSeverity, looked.DefaultSeverity);
            }

            Assert.False(HeddleDiagnosticCatalog.TryGet("HED9999", out _));
            Assert.False(HeddleDiagnosticCatalog.TryGet(null, out _));
        }

        /// <summary>Where a row carries a format, its placeholders are exactly <c>{0}</c>…<c>{n-1}</c> — no gaps,
        /// no out-of-range index — and it formats without throwing. A wrong index is otherwise a
        /// <c>FormatException</c> at the raise, i.e. at the worst possible moment.</summary>
        [Fact]
        public void PopulatedMessageFormatsAreWellFormed()
        {
            foreach (var row in HeddleDiagnosticCatalog.All.Where(r => r.MessageFormat != null))
            {
                var indexes = Regex.Matches(row.MessageFormat, @"\{(?<n>\d+)(?::[^}]*)?\}")
                    .Cast<Match>()
                    .Select(m => int.Parse(m.Groups["n"].Value, CultureInfo.InvariantCulture))
                    .Distinct()
                    .OrderBy(n => n)
                    .ToList();

                Assert.True(indexes.SequenceEqual(Enumerable.Range(0, indexes.Count)),
                    row.Id + " has non-contiguous message placeholders: " + string.Join(", ", indexes));

                var args = Enumerable.Range(0, indexes.Count).Select(i => (object)("<" + i + ">")).ToArray();
                var formatted = string.Format(CultureInfo.InvariantCulture, row.MessageFormat, args);
                Assert.False(string.IsNullOrWhiteSpace(formatted));
            }
        }

        /// <summary>The <c>MessageFormat</c> column is scoped to rows with a second consumer (Q6.3): the
        /// <c>HED70xx</c> block, whose text the generator's descriptor projection formats. A runtime-raised id's
        /// message has exactly one owner — its raise site — so populating it here would create the second copy
        /// the catalog exists to remove.</summary>
        [Fact]
        public void OnlyRowsWithASecondConsumerCarryMessageProse()
        {
            foreach (var row in HeddleDiagnosticCatalog.All)
            {
                var consumed = row.Id.StartsWith("HED70", StringComparison.Ordinal);
                Assert.True(consumed == (row.MessageFormat != null),
                    row.Id + (consumed
                        ? " is projected to a Roslyn descriptor and must carry its MessageFormat"
                        : " has a single raise site owning its message and must not carry a second copy"));
            }
        }

        /// <summary>The shared <c>[Prop]</c> reserved-name set: the runtime's parse-time def-header check, its
        /// attribute-source twin in <c>PropLayout</c>, and the generator's emitter twin all consult this one
        /// list, so the set cannot differ by tier.</summary>
        [Fact]
        public void ReservedPropNamesAreTheSharedSet()
        {
            Assert.Equal(new[] { "out", "this" }, HeddleDiagnosticCatalog.PropFaults.ReservedNames);
            Assert.True(HeddleDiagnosticCatalog.PropFaults.IsReserved("out"));
            Assert.True(HeddleDiagnosticCatalog.PropFaults.IsReserved("this"));
            Assert.False(HeddleDiagnosticCatalog.PropFaults.IsReserved("Out"));
            Assert.False(HeddleDiagnosticCatalog.PropFaults.IsReserved("title"));
            Assert.False(HeddleDiagnosticCatalog.PropFaults.IsReserved(null));
        }
    }
}
