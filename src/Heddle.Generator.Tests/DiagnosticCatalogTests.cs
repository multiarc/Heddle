extern alias gen;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Heddle.Generator.Diagnostics;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Heddle.Generator.Tests
{
    /// <summary>
    /// The code-side half of the descriptor/catalog agreement. Every <c>GeneratorDiagnostics</c> descriptor
    /// must equal its <c>HeddleDiagnosticCatalog</c> row on <c>(Id, Title, DefaultSeverity, MessageFormat)</c>.
    /// After the descriptors became catalog projections this holds by construction; the test is what makes it
    /// stay that way — reintroducing a hand-built descriptor with its own title or severity reds the build
    /// instead of quietly recreating the second registry the catalog exists to remove.
    /// </summary>
    public class DiagnosticCatalogTests
    {
        private static IEnumerable<DiagnosticDescriptor> Descriptors() =>
            typeof(GeneratorDiagnostics)
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.FieldType == typeof(DiagnosticDescriptor))
                .Select(f => (DiagnosticDescriptor)f.GetValue(null));

        [Fact]
        public void EveryDescriptorEqualsItsCatalogRow()
        {
            foreach (var descriptor in Descriptors())
            {
                Assert.True(gen::Heddle.Data.HeddleDiagnosticCatalog.TryGet(descriptor.Id, out var row),
                    "No catalog row for descriptor " + descriptor.Id);
                Assert.Equal(row.Title, descriptor.Title.ToString());
                Assert.Equal(row.MessageFormat, descriptor.MessageFormat.ToString());
                Assert.Equal(
                    row.DefaultSeverity == gen::Heddle.Data.HeddleDiagnosticSeverity.Warning
                        ? DiagnosticSeverity.Warning
                        : DiagnosticSeverity.Error,
                    descriptor.DefaultSeverity);
                Assert.Equal("Heddle.Precompile", descriptor.Category);
                Assert.True(descriptor.IsEnabledByDefault);
            }
        }

        /// <summary>Every <c>HED70xx</c> row the catalog carries has a descriptor: the generator block is the one
        /// the catalog is authoritative <i>from</i>, so a row without a reporter is a stale row.</summary>
        [Fact]
        public void EveryBuildTimeCatalogRowHasADescriptor()
        {
            var reported = new HashSet<string>(Descriptors().Select(d => d.Id));
            var buildRows = gen::Heddle.Data.HeddleDiagnosticCatalog.All
                .Where(r => r.Id.StartsWith("HED70", System.StringComparison.Ordinal))
                .Select(r => r.Id)
                .ToList();

            Assert.NotEmpty(buildRows);
            var orphaned = buildRows.Where(id => !reported.Contains(id)).ToList();
            Assert.True(orphaned.Count == 0, "Catalog HED70xx rows with no descriptor: " + string.Join(", ", orphaned));
        }

        /// <summary>A forwarded diagnostic for a catalogued id keeps the catalog's title, so the id a user sees in
        /// the build log carries the same human name the editor shows for the same fault.</summary>
        [Fact]
        public void ForwardedDescriptorsTakeTheirTitleFromTheCatalog()
        {
            gen::Heddle.Data.HeddleDiagnosticCatalog.TryGet("HED2004", out var row);
            Assert.Equal(row.Title, GeneratorDiagnostics.Forwarded("HED2004", true).Title.ToString());

            // Unknown ids still forward under their own id—identity is preserved.
            Assert.Equal("HED9999", GeneratorDiagnostics.Forwarded("HED9999", true).Id);
        }
    }
}
