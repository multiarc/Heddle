using System.Linq;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// Validates behavior for unresolvable functions: emits HED7014 warning and null-target manifest entry.
    /// Generated source is omitted; runtime uses dynamic path.
    /// </summary>
    public class UnresolvableFunctionTests
    {
        private const string ProductType = "Heddle.Generator.IntegrationTests.Fixtures.Product";

        [Fact]
        public void UnresolvableFunctionReportsPositionedHed7014AndNoSource()
        {
            var t = "@model(){{" + ProductType + "}}@\\\n<span>@(mystery(Name))</span>\n";
            var gen = DifferentialHarness.Generate(new[] { ("views/label-fancy.heddle", t) });

            var hed7014 = gen.Diagnostics.Where(d => d.Id == "HED7014").ToList();
            Assert.Single(hed7014);
            Assert.Equal(DiagnosticSeverity.Warning, hed7014[0].Severity);
            Assert.Contains("mystery", hed7014[0].GetMessage());

            // Error at mystery(...) call: line 1, column 8 (0-based).
            var span = hed7014[0].Location.GetLineSpan();
            Assert.Equal(1, span.StartLinePosition.Line);
            Assert.Equal(8, span.StartLinePosition.Character);

            Assert.Empty(gen.TemplateSources);

            Assert.NotNull(gen.ManifestSource);
            Assert.Contains("entryPointType: null", gen.ManifestSource);
            Assert.Contains("strategy: null", gen.ManifestSource);
            Assert.Contains("new global::Heddle.Precompiled.PrecompiledFunctionBinding(\"mystery\", null, 0)",
                gen.ManifestSource);
        }

        [Fact]
        public void ResolvableFunctionsAreNotMarked()
        {
            // upper is a default built-in; titlecase/shout are exported by the test assembly — all resolvable.
            var t = "@model(){{" + ProductType + "}}@\\\n<span>@(upper(Name)) @(titlecase(Name)) @(shout(Name))</span>\n";
            var gen = DifferentialHarness.Generate(new[] { ("views/label-ok.heddle", t) });

            Assert.DoesNotContain(gen.Diagnostics, d => d.Id == "HED7014");
            Assert.Single(gen.TemplateSources);
        }
    }
}
