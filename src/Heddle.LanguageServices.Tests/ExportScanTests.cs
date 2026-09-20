using System.Collections.Generic;
using System.Linq;
using Heddle.LanguageServices;
using Xunit;

namespace Heddle.LanguageServices.Tests
{
    /// <summary>
    /// The one-shot export scan: <c>[ExportExtensions]</c> extensions enter the real registry as offered names,
    /// <c>[ExportFunctions]</c> populates the workspace registry, the scan runs once per process, and a bare
    /// workspace stays on the default registry.
    /// </summary>
    public class ExportScanTests
    {
        [Fact]
        public void ScanRegistersExtensionIntoTemplateFactory()
        {
            using var service = CorpusFixture.NewTypedService();
            service.Analyze("doc.heddle", "@§".Replace("§", ""), 1);
            var labels = service.GetCompletions("doc.heddle", 1).Items.Select(i => i.Label);
            Assert.Contains("badge", labels);
        }

        [Fact]
        public void ScanRegistersFunctionExportIntoWorkspaceRegistry()
        {
            using var service = CorpusFixture.NewTypedService();
            Assert.NotNull(service.Functions);
            Assert.True(service.Functions.Contains("titlecase"));
        }

        [Fact]
        public void TypelessWorkspaceKeepsDefaultRegistry()
        {
            using var service = CorpusFixture.NewTypelessService();
            // No exports → Functions null (= FunctionRegistry.Default — bare-host parity).
            Assert.Null(service.Functions);
        }

        private const string Exporting =
            "[assembly: Heddle.Attributes.ExportFunctions(typeof(B.Fns))] [assembly: Heddle.Attributes.ExportExtensions(typeof(B.ShoutBox))] " +
            "namespace B { public static class Fns { public static string shout(string s) => s; } " +
            "[Heddle.Attributes.ExtensionName(\"shoutbox\")] public class ShoutBox : Heddle.Core.AbstractExtension { " +
            "public override object ProcessData(in Heddle.Data.Scope scope) => null; public override void RenderData(in Heddle.Data.Scope scope) { } } " +
            "public class M { public string Open { get; set; } } }";

        /// <summary>The server starts on whatever workspace it is given first, and the user then edits
        /// <c>assemblies</c>. Pins the regression where the export scan ran once per process: a first workspace
        /// without exports latched it, and the exports of the one configured next were never offered.</summary>
        [Fact]
        public void AWorkspaceConfiguredAfterOneWithoutExportsOffersItsExports()
        {
            using (var workspace = new SynthesizedWorkspace())
            {
                string plain = workspace.Emit("Plain", "namespace A { public class M { public string Open { get; set; } } }");
                string exporting = workspace.Emit("Exp", Exporting, SynthesizedWorkspace.RealEngine, "second");

                Assert.DoesNotContain("shout", workspace.Complete(plain, "@model(){{A.M}} @(§)", new List<string>()));
                Assert.Contains("shout", workspace.Complete(exporting, "@model(){{B.M}} @(§)", new List<string>()));
                Assert.Contains("shoutbox", workspace.Complete(exporting, "@§", new List<string>()));
            }
        }

        /// <summary>And the other way round: what the previous workspace exported is gone with it.</summary>
        [Fact]
        public void ThePreviousWorkspacesExportsDoNotLinger()
        {
            using (var workspace = new SynthesizedWorkspace())
            {
                string exporting = workspace.Emit("Exp", Exporting, SynthesizedWorkspace.RealEngine, "first");
                string plain = workspace.Emit("Plain", "namespace A { public class M { public string Open { get; set; } } }");

                Assert.Contains("shoutbox", workspace.Complete(exporting, "@§", new List<string>()));
                Assert.DoesNotContain("shoutbox", workspace.Complete(plain, "@§", new List<string>()));
                Assert.DoesNotContain("shout", workspace.Complete(plain, "@model(){{A.M}} @(§)", new List<string>()));
                using (var typeless = CorpusFixture.NewTypelessService())
                {
                    typeless.Analyze("doc.heddle", "@", 1);
                    Assert.DoesNotContain("shoutbox", CorpusFixture.Labels(typeless.GetCompletions("doc.heddle", 1)));
                }
            }
        }

        /// <summary>A rebuilt workspace is rescanned on reload: an export added by the rebuild is offered without
        /// restarting the server.</summary>
        [Fact]
        public void AReloadPicksUpAnExportTheRebuildAdded()
        {
            using (var workspace = new SynthesizedWorkspace())
            {
                string path = workspace.Emit("Grow", "namespace A { public class M { public string Open { get; set; } } }");
                using (var service = workspace.Open(path, new List<string>()))
                {
                    service.Analyze("doc.heddle", "@", 1);
                    Assert.DoesNotContain("shoutbox", CorpusFixture.Labels(service.GetCompletions("doc.heddle", 1)));

                    workspace.Emit("Grow", Exporting, SynthesizedWorkspace.RealEngine);
                    service.ReloadModelAssemblies();
                    service.Analyze("doc.heddle", "@", 1);
                    Assert.Contains("shoutbox", CorpusFixture.Labels(service.GetCompletions("doc.heddle", 1)));
                }
            }
        }
    }
}
