using System.Collections.Generic;
using System.Linq;
using Heddle.TestCorpus;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// Pins WHICH refusal took a template off the precompiled tier, not merely that one fired. HED7031 carries the
    /// emitter's recorded reason, and until this suite no test asserted any of them — so a construct could start
    /// degrading for an entirely different refusal and every degrade expectation stayed green.
    /// </summary>
    public class DegradeReasonTests
    {
        private static readonly Dictionary<string, string> FullCSharpBuild =
            new Dictionary<string, string> { ["build_property.HeddleExpressionMode"] = "FullCSharp" };

        /// <summary>The corpus flagship document (a FallsBackSafely row): under the default (Native) build its
        /// first refusal in document order is its embedded C#, which only a FullCSharp build may emit.</summary>
        [Fact]
        public void TheFlagshipCorpusDocumentDegradesForItsEmbeddedCSharp()
        {
            var row = TestCorpusIndex.Load().Single(t => t.key == "template.heddle");
            var gen = DifferentialHarness.Generate(new[] { row },
                extraReferences: DifferentialHarness.EngineTestModelReferences());
            Assert.False(gen.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error),
                "Unexpected generator error: " + string.Join("; ", gen.Diagnostics.Select(d => d.ToString())));

            DifferentialHarness.ExpectDegrade(gen, "template.heddle", "embedded C# outside FullCSharp mode");
        }

        /// <summary>An embedded expression that reads the engine's <c>chained</c> parameter: the emitter cannot
        /// reproduce its runtime type today, so the binder-answered chained/root refusal fires — the specific
        /// refusal the compiled-fragment work retires, pinned here so its retirement is an observable row move
        /// rather than a silent reroute to some other reason.</summary>
        [Fact]
        public void EmbeddedCSharpReadingChainedDegradesForTheChainedRootRefusal()
        {
            const string key = "views/degrade-reason-chained.heddle";
            const string template =
                "@model(){{Heddle.Generator.IntegrationTests.Fixtures.Order}}@(@chained.ToString())";

            var gen = DifferentialHarness.Generate(new[] { (key, template) }, FullCSharpBuild);
            Assert.False(gen.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error),
                "Unexpected generator error: " + string.Join("; ", gen.Diagnostics.Select(d => d.ToString())));

            DifferentialHarness.ExpectDegrade(gen, key, "embedded C# references chained/root");
        }
    }
}
