using System.Linq;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Heddle.Generator.Tests
{
    /// <summary>
    /// The build-tier half of the shared call-target classifier. The emitter once tried the function tier BEFORE
    /// the extension binder, so a host-exported function sharing a name with a registered extension bound as a
    /// function at build and as the extension at run. <c>CallTargetRulesTests</c> pins the shared classifier in
    /// isolation and the run tier, but nothing exercised the emitter's dispatch over a real collision. This does,
    /// over a synthetic compilation so the collision cannot leak into the shared function fixtures.
    /// </summary>
    public class CallTargetAdoptionTests
    {
        private const string CollidingExport =
            "[assembly: Heddle.Attributes.ExportFunctions(typeof(Probe.Colliding))]\n" +
            "namespace Probe { public static class Colliding { public static string Raw(string s) => \"FUNCTION:\" + s; } }";

        /// <summary>A host exports a function named <c>raw</c> — the name <c>RawExtension</c> already claims. The
        /// runtime resolves it to the extension (the classifier's extension-wins arm, pinned run-tier by
        /// <c>CallTargetRulesTests.ExtensionBeatsFunction</c>); the emitter must reach the same answer, i.e. bind
        /// <c>RawExtension</c> and never emit a call into the exported container.</summary>
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
