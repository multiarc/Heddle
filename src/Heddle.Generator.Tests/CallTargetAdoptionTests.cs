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
    }
}
