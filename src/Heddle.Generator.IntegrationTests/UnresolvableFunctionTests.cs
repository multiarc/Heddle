using System.Linq;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// Phase 7 D21 / OQ1 remainder — a function name resolvable from neither the default table nor any referenced
    /// <c>[ExportFunctions]</c> export (a delegate-only registration, not representable in metadata) degrades the
    /// template to a <b>HED7014 fallback marker</b>: a positioned build warning, a null-target function-binding row
    /// in the manifest, and <b>no</b> generated <c>.g.cs</c>. The runtime gauntlet short-circuits the marker to the
    /// dynamic path (README D8/D21).
    /// </summary>
    public class UnresolvableFunctionTests
    {
        private const string ProductType = "Heddle.Generator.IntegrationTests.Fixtures.Product";

        [Fact]
        public void UnresolvableFunctionReportsPositionedHed7014AndNoSource()
        {
            // Line 2 (1-based): "<span>@(mystery(Name))</span>". mystery( starts at column 9 (1-based).
            var t = "@model(){{" + ProductType + "}}@\\\n<span>@(mystery(Name))</span>\n";
            var gen = DifferentialHarness.Generate(new[] { ("views/label-fancy.heddle", t) });

            var hed7014 = gen.Diagnostics.Where(d => d.Id == "HED7014").ToList();
            Assert.Single(hed7014);
            Assert.Equal(DiagnosticSeverity.Warning, hed7014[0].Severity);
            Assert.Contains("mystery", hed7014[0].GetMessage());

            // Positioned at the mystery(...) call: line 2 (0-based line 1), starting at the 'mystery' token.
            var span = hed7014[0].Location.GetLineSpan();
            Assert.Equal(1, span.StartLinePosition.Line);
            Assert.Equal(8, span.StartLinePosition.Character); // 0-based column of 'm' in "@(mystery"

            // No template source generated — only the manifest.
            Assert.Empty(gen.TemplateSources);

            // Manifest carries a marker entry: null entry point, null strategy, null-target function row.
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
