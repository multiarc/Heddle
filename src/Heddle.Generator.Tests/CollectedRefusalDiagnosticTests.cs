using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Heddle.Generator.Tests
{
    /// <summary>
    /// Collects all refusals instead of stopping at first, but still prevents emission (no partial templates).
    /// Build-time collection matches runtime behavior: each build report is one the runtime would raise once earlier errors are fixed.
    /// </summary>
    public class CollectedRefusalDiagnosticTests
    {
        private static Dictionary<string, string> Root(string root) =>
            new Dictionary<string, string> { ["build_property.HeddleTemplateRoot"] = root };

        /// <summary>Two calls the ranker proves the runtime will refuse; verifies all refusals are collected.</summary>
        [Fact]
        public void TwoUnwritableCallsInOneTemplateReportTwoDiagnosticsAtTheirOwnSpans()
        {
            const string content =
                "@model(){{System.String}}@\\\nfirst: @(min(1, 2u))\nsecond: @(min(1, 2, 3))\n";

            var run = GeneratorHarness.Run(new[] { ("/repo/app/two-refusals.heddle", content) },
                globalOptions: Root("/repo/app"));

            var reported = run.GeneratorDiagnostics.Where(d => d.Id == "HED7025").ToArray();
            Assert.Equal(2, reported.Length);
            Assert.All(reported, d => Assert.Equal(DiagnosticSeverity.Error, d.Severity));

            var spans = reported.Select(d => d.Location.SourceSpan.Start).Distinct().ToArray();
            Assert.Equal(2, spans.Length);

            var messages = reported.Select(d => d.GetMessage()).ToArray();
            Assert.Contains(messages, m => m.Contains("ambiguous"));
            Assert.Contains(messages, m => m.Contains("(int, int, int)"));
        }

        /// <summary>Guard that collection doesn't cause emission: unwritable constructs still produce no generated source.</summary>
        [Fact]
        public void ATemplateWithAnUnwritableConstructStillEmitsNothingAndRegistersNoManifestRow()
        {
            const string content =
                "@model(){{System.String}}@\\\nfirst: @(min(1, 2u))\nsecond: @(this)\n";

            var run = GeneratorHarness.Run(new[] { ("/repo/app/refused.heddle", content) },
                globalOptions: Root("/repo/app"));

            Assert.Contains(run.GeneratorDiagnostics, d => d.Id == "HED7025");

            Assert.DoesNotContain(run.GeneratedSourceTexts,
                s => s.Contains("second: ") || s.Contains("refused_heddle"));

            foreach (var manifest in run.GeneratedSourceTexts.Where(s => s.Contains("__HeddleManifest")))
                Assert.DoesNotContain("key: \"refused.heddle\"", manifest);
        }

        /// <summary>An un-precompilable function after a refusal reaches the HED7014 marker arm, which walk order
        /// used to hide. The call has to be one late binding cannot serve — its argument is an expression over a
        /// second unknown name, so nothing types it — because a call the build can shape now precompiles.</summary>
        [Fact]
        public void ADelegateOnlyFunctionAfterTheFirstRefusalNowReachesTheHed7014MarkerArm()
        {
            const string content =
                "@model(){{System.String}}@\\\nfirst: @(min(1, 2u))\nsecond: @(nosuchfn9000(alsonone(this) + 1))\n";

            var run = GeneratorHarness.Run(new[] { ("/repo/app/marker-after-refusal.heddle", content) },
                globalOptions: Root("/repo/app"));

            Assert.Single(run.GeneratorDiagnostics.Where(d => d.Id == "HED7025"));
            var hed7014 = Assert.Single(run.GeneratorDiagnostics.Where(d => d.Id == "HED7014"));
            Assert.Equal(DiagnosticSeverity.Warning, hed7014.Severity);
            Assert.Contains("nosuchfn9000", hed7014.GetMessage());

            var manifest = Assert.Single(run.GeneratedSourceTexts, s => s.Contains("__HeddleManifest"));
            Assert.Contains("key: \"marker-after-refusal.heddle\"", manifest);
            Assert.Contains("entryPointType: null", manifest);
        }
    }
}
