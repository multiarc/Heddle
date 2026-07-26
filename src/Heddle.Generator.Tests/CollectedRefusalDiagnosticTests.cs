using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Heddle.Generator.Tests
{
    /// <summary>
    /// <b>The element walk collects refusals instead of abandoning at the first one.</b> <c>TemplateEmitter.PopulateBody</c>
    /// used to return <c>false</c> the moment <c>BuildCall</c> refused an element, so a template with two provably illegal
    /// calls reported one <c>HED7025</c>; now it records the first reason, skips the refusing element and keeps going, reporting
    /// each refusal at its own span. Sibling elements are independent — they share the same immutable <c>BodyContext</c>, and
    /// nothing a refused element touches can make a later legal element illegal.
    /// <para><b>Refusal still propagates.</b> The walk returns <c>false</c> at the end, so the body is still <c>null</c>, and
    /// <c>Emit</c> still produces no <c>.g.cs</c> and no manifest row. A partial emit — a template compiled with its illegal
    /// elements quietly dropped — would be far worse than one diagnostic at a time.</para>
    /// <para><b>Match principle.</b> The dynamic engine raises the first such error and stops. The build may report multiple
    /// <c>HED7025</c>s the runtime does not raise all at once, but every collected report is one the runtime would raise once the
    /// earlier was fixed.</para>
    /// </summary>
    public class CollectedRefusalDiagnosticTests
    {
        private static Dictionary<string, string> Root(string root) =>
            new Dictionary<string, string> { ["build_property.HeddleTemplateRoot"] = root };

        /// <summary>Two independent sibling elements, each a call the shared overload ranker <i>proves</i> the
        /// runtime will refuse: <c>min(1, 2u)</c> is ambiguous (flat three-member Pareto front) and
        /// <c>min(1, 2, 3)</c> has no applicable overload. Before Q8.19 only the first was reported.</summary>
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

        /// <summary>The companion guard, and the load-bearing half: collecting refusals must not turn into
        /// <b>emitting</b> past them. A template with an unwritable construct produces no generated template source
        /// and no manifest row — exactly as before Q8.19 — because <c>PopulateBody</c> still returns <c>false</c> once
        /// anything refused and <c>Emit</c> still sees a null root.</summary>
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

    /// <summary>A delegate-only function <i>after</i> the first unwritable construct is now reached because collection
    /// continues past refusals. It reaches <c>_unresolvableFunctions</c> and <c>Emit</c> takes the <c>HED7014</c>
    /// fallback-marker arm: the build gains a warning and a <b>marker</b> manifest row (null entry point) where before
    /// there was no row. That is correct — the template really does call a function no metadata can represent, and the
    /// marker is how the registry records "known, deliberately not precompiled". Walk order was the only reason it stayed
    /// hidden. The row is still a marker, never executable code: <c>entryPointType: null</c> is asserted.</summary>
        [Fact]
        public void ADelegateOnlyFunctionAfterTheFirstRefusalNowReachesTheHed7014MarkerArm()
        {
            const string content =
                "@model(){{System.String}}@\\\nfirst: @(min(1, 2u))\nsecond: @(nosuchfn9000(this))\n";

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
