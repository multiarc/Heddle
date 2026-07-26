using System.Linq;
using Heddle.LanguageServices;
using Heddle.Tests;
using Xunit;

namespace Heddle.LanguageServices.Tests
{
    /// <summary>
    /// The editor arm of the diagnostic-projection-equivalence corpus. The same
    /// <see cref="DiagnosticCorpusVectors"/> table the run tier asserts against is asserted here against what a
    /// workspace-configured <c>HeddleLanguageService</c> actually publishes — so the editor and the engine agree
    /// on every diagnostic, and a channel that stopped draining fails here.
    /// <para>The service is constructed with default options to validate default-enabled profile behavior.
    /// The <c>encodingLint</c> fixture is silent under <c>Text</c> and fires under <c>Html</c>, so its row passing
    /// here confirms an unconfigured editor lints exactly as the build of record.</para>
    /// </summary>
    public class DiagnosticProjectionCorpusLspTests
    {
        public static TheoryData<string> Names
        {
            get
            {
                var data = new TheoryData<string>();
                foreach (var c in DiagnosticCorpusVectors.Cases)
                    data.Add(c.Name);
                return data;
            }
        }

        [Theory]
        [MemberData(nameof(Names))]
        public void TheEditorPublishesExactlyTheCorpusEntries(string name)
        {
            var c = DiagnosticCorpusVectors.Cases.First(x => x.Name == name);
            var service = CorpusFixture.NewTypelessService();
            try
            {
                var analysis = service.Analyze("doc.heddle", c.Template, 1);
                var actual = analysis.Diagnostics
                    .Select(d => $"{d.Id ?? "<null>"}/" +
                                 $"{(d.Severity == HeddleDiagnosticSeverity.Warning ? "W" : "E")}@{d.Offset},{d.Length}")
                    .ToList();

                Assert.Equal(c.Entries, actual);
            }
            finally
            {
                service.Dispose();
            }
        }

        /// <summary>Validates the opt-out case: when the workspace configures the <c>text</c> profile,
        /// profile-dependent fixtures go quiet, and we assert the corpus records that state.</summary>
        [Fact]
        public void ConfiguringTheTextProfileYieldsTheCorpusTextRows()
        {
            var c = DiagnosticCorpusVectors.Cases.First(x => x.Name == "encodingLint");
            var options = WorkspaceConfig.Read(null, "{\"outputProfile\":\"text\"}");
            var service = new HeddleLanguageService(options);
            try
            {
                var analysis = service.Analyze("doc.heddle", c.Template, 1);
                Assert.Equal(c.TextProfileEntries, analysis.Diagnostics.Select(d => d.Id).ToArray());
                Assert.NotEqual(c.Entries, c.TextProfileEntries);   // the fixture really is profile-dependent
            }
            finally
            {
                service.Dispose();
            }
        }
    }
}
