using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Heddle.LanguageServices.Tests
{
    /// <summary>A workspace is somebody else's build output, and the server reflects over it. Pins the rule that
    /// nothing found there takes the server down: a workspace built against a <b>newer</b> engine — a type, an
    /// attribute or an attribute constructor this server's engine does not have — costs the one export or
    /// member that needs it, says so in the log, and leaves everything else working. The one thing that never
    /// degrades open is <c>[Hidden]</c>.</summary>
    public class WorkspaceResilienceTests
    {
        private const string NewerEngine =
            "[assembly: System.Reflection.AssemblyVersion(\"99.0.0.0\")]\n" +
            "namespace Heddle.Attributes {\n" +
            "[System.AttributeUsage(System.AttributeTargets.Assembly, AllowMultiple = true)] public class ExportFunctionsAttribute : System.Attribute { public ExportFunctionsAttribute(params System.Type[] c) { } public ExportFunctionsAttribute(string newer, System.Type c) { } }\n" +
            "[System.AttributeUsage(System.AttributeTargets.All)] public class HiddenAttribute : System.Attribute { }\n" +
            "[System.AttributeUsage(System.AttributeTargets.All)] public class FutureAttribute : System.Attribute { } }\n" +
            "namespace Heddle { public class NewThing { public string Z { get; set; } } }";

        private const string Model =
            " namespace W { public class M { public string Open { get; set; } [Heddle.Attributes.Hidden] public string Secret { get; set; } } }";

        [Fact]
        public void AnExportWhoseSignatureNamesATypeTheServersEngineLacksCostsOnlyThatExport()
        {
            using (var workspace = new SynthesizedWorkspace())
            {
                string engine = workspace.Emit("Heddle", NewerEngine);
                string path = workspace.Emit("WsSig",
                    "[assembly: Heddle.Attributes.ExportFunctions(typeof(W.Fns))] namespace W { public static class Fns { " +
                    "public static string shout(string s) => s; public static string viaNew(Heddle.NewThing t) => t.Z; } }" +
                    Model, engine);

                var log = new List<string>();
                var labels = workspace.Complete(path, "@model(){{W.M}} @(§)", log);

                Assert.Contains("Open", labels);
                Assert.DoesNotContain("Secret", labels);
                Assert.Contains("shout", labels);
                Assert.DoesNotContain("vianew", labels);
                Assert.Contains(log, line => line.Contains("viaNew"));
                Assert.Single(log, line => line.Contains("99.0.0.0"));
            }
        }

        [Fact]
        public void AnExportAttributeConstructorTheServersEngineLacksCostsOnlyThatAssemblysExports()
        {
            using (var workspace = new SynthesizedWorkspace())
            {
                string engine = workspace.Emit("Heddle", NewerEngine);
                string path = workspace.Emit("WsCtor",
                    "[assembly: Heddle.Attributes.ExportFunctions(\"x\", typeof(W.Fns))] namespace W { public static class Fns { " +
                    "public static string shout(string s) => s; } }" + Model, engine);

                var log = new List<string>();
                var labels = workspace.Complete(path, "@model(){{W.M}} @(§)", log);

                Assert.Contains("Open", labels);
                Assert.DoesNotContain("Secret", labels);
                Assert.Contains(log, line => line.Contains("WsCtor") && line.Contains("export"));
                Assert.Single(log, line => line.Contains("99.0.0.0"));
            }
        }

        [Fact]
        public void AMemberTypedWithATypeTheServersEngineLacksIsSkippedAndItsNeighboursStay()
        {
            using (var workspace = new SynthesizedWorkspace())
            {
                string engine = workspace.Emit("Heddle", NewerEngine);
                string path = workspace.Emit("WsProp",
                    "namespace W { public class M { public string Open { get; set; } public Heddle.NewThing Thing { get; set; } " +
                    "[Heddle.Attributes.Hidden] public string Secret { get; set; } } }", engine);

                var log = new List<string>();
                var labels = workspace.Complete(path, "@model(){{W.M}} @(§)", log);
                Assert.Contains("Open", labels);
                Assert.DoesNotContain("Thing", labels);
                Assert.DoesNotContain("Secret", labels);
                Assert.Contains(log, line => line.Contains("Thing"));

                Assert.Empty(workspace.Complete(path, "@model(){{W.M}} @(Thing.§)", log));
                Assert.NotNull(workspace.Hover(path, "@model(){{W.M}} @(Op§en)"));
                Assert.Null(workspace.Hover(path, "@model(){{W.M}} @(Th§ing)"));
            }
        }

        /// <summary>An attribute whose type cannot load makes the runtime refuse to list the member's attributes
        /// at all. The member is hidden unless its metadata proves no attribute on it is the hidden one.</summary>
        [Fact]
        public void AnAttributeTheServersEngineLacksNeverOpensAHiddenMember()
        {
            using (var workspace = new SynthesizedWorkspace())
            {
                string engine = workspace.Emit("Heddle", NewerEngine);
                string path = workspace.Emit("WsAttr",
                    "namespace W { public class M { public string Open { get; set; } " +
                    "[Heddle.Attributes.Future] public string Tagged { get; set; } " +
                    "[Heddle.Attributes.Future, Heddle.Attributes.Hidden] public string Both { get; set; } " +
                    "[Heddle.Attributes.Hidden] public string Secret { get; set; } } }", engine);

                var log = new List<string>();
                var labels = workspace.Complete(path, "@model(){{W.M}} @(§)", log);
                Assert.Contains("Open", labels);
                Assert.Contains("Tagged", labels);
                Assert.DoesNotContain("Both", labels);
                Assert.DoesNotContain("Secret", labels);

                var diagnostics = workspace.Diagnose(path, "@model(){{W.M}} @(Both)");
                Assert.Contains(diagnostics, d => d.Id == "HED0001" || d.Message.Contains("Both"));
            }
        }

        /// <summary>A consumer assembly whose name merely begins like the engine's, with a reference that is not
        /// there. Pins the regression where every reference named <c>Heddle…</c> was bound eagerly inside the
        /// load's own catch: the missing one threw, and the whole assembly — its models with it — was dropped
        /// without a word.</summary>
        [Fact]
        public void AConsumerAssemblyNamedLikeTheEngineKeepsItsModelsWhenAReferenceIsAbsent()
        {
            using (var workspace = new SynthesizedWorkspace())
            {
                string models = workspace.Emit("HeddleShop2.Models",
                    "namespace Shop2 { public class Item { public string Sku { get; set; } } }", subdirectory: "elsewhere");
                string web = workspace.Emit("HeddleShop2.Web",
                    "namespace Shop2 { public class Page { public string Title { get; set; } public Item First { get; set; } } }",
                    models);

                var labels = workspace.Complete(web, "@model(){{Shop2.Page}} @(§)", new List<string>());
                Assert.Contains("Title", labels);
            }
        }
    }

    /// <summary>Assemblies compiled on the spot into a directory of their own, as a build output would hold them.</summary>
    internal sealed class SynthesizedWorkspace : IDisposable
    {
        private readonly string _dir;

        internal SynthesizedWorkspace()
        {
            _dir = Path.Combine(Path.GetTempPath(), "heddle-ws-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        internal static string RealEngine => typeof(Heddle.HeddleTemplate).Assembly.Location;

        internal string Emit(string name, string source, string reference = null, string subdirectory = null)
        {
            string dir = subdirectory == null ? _dir : Path.Combine(_dir, subdirectory);
            Directory.CreateDirectory(dir);
            var references = ((string) AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))
                .Split(Path.PathSeparator)
                .Where(p => Path.GetFileName(p).StartsWith("System.", StringComparison.Ordinal) ||
                            Path.GetFileName(p) == "netstandard.dll" || Path.GetFileName(p) == "mscorlib.dll")
                .Select(p => (MetadataReference) MetadataReference.CreateFromFile(p)).ToList();
            if (reference != null)
                references.Add(MetadataReference.CreateFromFile(reference));
            string path = Path.Combine(dir, name + ".dll");
            var emitted = CSharpCompilation.Create(name, new[] { CSharpSyntaxTree.ParseText(source) }, references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)).Emit(path);
            Assert.True(emitted.Success, string.Join("\n", emitted.Diagnostics));
            return path;
        }

        internal HeddleLanguageService Open(string assemblyPath, List<string> log)
        {
            return new HeddleLanguageService(new HeddleLanguageServiceOptions
            {
                AssemblyPaths = new List<string> { assemblyPath }
            }) { LogSink = log.Add };
        }

        internal IReadOnlyList<string> Complete(string assemblyPath, string marked, List<string> log)
        {
            using (var service = Open(assemblyPath, log))
            {
                var (text, offset) = CorpusFixture.At(marked);
                service.Analyze("doc.heddle", text, 1);
                return CorpusFixture.Labels(service.GetCompletions("doc.heddle", offset));
            }
        }

        internal HoverResult Hover(string assemblyPath, string marked)
        {
            using (var service = Open(assemblyPath, new List<string>()))
            {
                var (text, offset) = CorpusFixture.At(marked);
                service.Analyze("doc.heddle", text, 1);
                return service.GetHover("doc.heddle", offset);
            }
        }

        internal IReadOnlyList<HeddleDiagnostic> Diagnose(string assemblyPath, string text)
        {
            using (var service = Open(assemblyPath, new List<string>()))
                return service.Analyze("doc.heddle", text, 1).Diagnostics;
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(_dir, true);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
            }
        }
    }
}
