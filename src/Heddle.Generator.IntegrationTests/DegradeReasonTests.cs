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

        /// <summary>The positive form of the retired "embedded C# references chained/root" refusal: the fragment
        /// declares the engine's <c>dynamic chained</c> parameter and the call site passes
        /// <c>scope.ChainedData</c>, so an expression reading the live chained value — the <c>@for</c> index here —
        /// precompiles, with the tier pinned, and renders the engine's bytes through the same DLR call sites the
        /// engine's generated assembly emits.</summary>
        [Fact]
        public void EmbeddedCSharpReadingChainedPrecompilesByteIdentically()
        {
            const string key = "views/degrade-reason-chained.heddle";
            const string template =
                "@model(){{Heddle.Generator.IntegrationTests.Fixtures.Order}}@\\\n" +
                "@for(3){{[@(@chained * 2 + 1)]}}\n";

            // The engine compiles over the assemblies LOADED in this process, and `chained * 2` needs the
            // runtime binder — an input a host rendering dynamic expressions has loaded.
            System.Reflection.Assembly.Load("Microsoft.CSharp");

            var gen = DifferentialHarness.Generate(new[] { (key, template) }, FullCSharpBuild);
            Assert.False(gen.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error),
                "Unexpected generator error: " + string.Join("; ", gen.Diagnostics.Select(d => d.ToString())));
            DifferentialHarness.ExpectPrecompiled(gen, key);

            var (pre, dyn) = DifferentialHarness.Render(key, template,
                typeof(Fixtures.Order), new Fixtures.Order(), FullCSharpBuild,
                new Heddle.Data.TemplateOptions { ExpressionMode = Heddle.Data.ExpressionMode.FullCSharp });
            Assert.Equal("[1][3][5]\n", dyn);
            Assert.Equal(dyn, pre);
        }
    }
}
