using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Heddle.Generator.Tests
{
    /// <summary>
    /// Validates emitter dispatch logic for name collisions between exported functions and registered extensions.
    /// Complements <c>CallTargetRulesTests</c> which isolates the classifier.
    /// </summary>
    public class CallTargetAdoptionTests
    {
        private const string CollidingExport =
            "[assembly: Heddle.Attributes.ExportFunctions(typeof(Probe.Colliding))]\n" +
            "namespace Probe { public static class Colliding { public static string Raw(string s) => \"FUNCTION:\" + s; } }";

        /// <summary>Collision: exported function <c>raw</c> vs registered <c>RawExtension</c>.
        /// Validates that emitter (like runtime) binds the extension.</summary>
        [Fact]
        public void AnExportedFunctionDoesNotStealARegisteredExtensionName()
        {
            var run = GeneratorHarness.RunWithSources(
                new[] { ("views/collide.heddle", "@model(){{System.String}}@\\\nx@raw(this)y\n") },
                new[] { CollidingExport });

            Assert.DoesNotContain(run.GeneratorDiagnostics, d => d.Severity == DiagnosticSeverity.Error);
            var source = run.GeneratedSourceTexts.FirstOrDefault(s => s.Contains("class Body0"));
            Assert.NotNull(source);

            Assert.Contains("Heddle.Extensions.EmptyExtension", source);   // the type behind @raw
            Assert.DoesNotContain("Probe.Colliding", source);
        }

        /// <summary>Binding the extension is the right answer, and the build tier now says out loud that it
        /// shadowed a function of the same name — the runtime's own diagnostic, at the call. Without the export
        /// there is no collision and nothing is said.</summary>
        [Fact]
        public void TheShadowedFunctionIsReportedAtTheCall()
        {
            const string template = "@model(){{System.String}}@\\\nx@raw(this)y\n";

            var shadowed = GeneratorHarness.RunWithSources(
                new[] { ("views/collide.heddle", template) }, new[] { CollidingExport }).GeneratorDiagnostics;

            var warning = Assert.Single(shadowed, d => d.Id == "HED1016");
            Assert.Equal(DiagnosticSeverity.Warning, warning.Severity);
            Assert.Equal(template.IndexOf("raw(this)", StringComparison.Ordinal),
                warning.Location.SourceSpan.Start);
            Assert.Contains("resolve to the extension", warning.GetMessage(), StringComparison.Ordinal);

            var noCollision = GeneratorHarness.RunWithSources(
                new[] { ("views/collide.heddle", template) },
                new[] { "namespace Probe { public static class Alone { } }" }).GeneratorDiagnostics;
            Assert.DoesNotContain(noCollision, d => d.Id == "HED1016");
        }
    }
}
