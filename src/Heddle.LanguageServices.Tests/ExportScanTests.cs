using System.Linq;
using Heddle.LanguageServices;
using Xunit;

namespace Heddle.LanguageServices.Tests
{
    /// <summary>
    /// The phase 6 D23/D24 one-shot export scan: the corpus <c>[ExportExtensions]</c> extension enters the real
    /// registry (offered as an extension name), the <c>[ExportFunctions]</c> container populates the workspace
    /// registry, the scan is one-shot per process, and a bare workspace stays on the default registry.
    /// </summary>
    public class ExportScanTests
    {
        [Fact]
        public void ScanRegistersExtensionIntoTemplateFactory()
        {
            using var service = CorpusFixture.NewTypedService();
            service.Analyze("doc.heddle", "@§".Replace("§", ""), 1);
            var labels = service.GetCompletions("doc.heddle", 1).Items.Select(i => i.Label);
            Assert.Contains("badge", labels);
        }

        [Fact]
        public void ScanRegistersFunctionExportIntoWorkspaceRegistry()
        {
            using var service = CorpusFixture.NewTypedService();
            Assert.NotNull(service.Functions);
            Assert.True(service.Functions.Contains("titlecase"));
        }

        [Fact]
        public void TypelessWorkspaceKeepsDefaultRegistry()
        {
            using var service = CorpusFixture.NewTypelessService();
            // No exports → Functions null (= FunctionRegistry.Default — bare-host parity).
            Assert.Null(service.Functions);
        }

        [Fact]
        public void ScanIsOneShotPerProcess()
        {
            using (var first = CorpusFixture.NewTypedService())
            {
                Assert.True(ExtensionRegistrar.HasScanned);
            }

            using (var second = CorpusFixture.NewTypedService())
            {
                // A second workspace does not rescan; the latch is already set.
                Assert.True(ExtensionRegistrar.HasScanned);
                Assert.True(second.Functions.Contains("titlecase")); // re-applied from retained handles
            }
        }
    }
}
