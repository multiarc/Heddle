using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Heddle.Data;
using Heddle.Runtime;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// The two tiers do not bind a model type from the same world. The generator resolves it over the compilation's
    /// <b>references</b>; the engine resolves it over the assemblies the process has actually <b>loaded</b>
    /// (<c>AppDomain.CurrentDomain.GetAssemblies()</c>, plus whatever the host registered explicitly). A referenced
    /// assembly is not a loaded one — the CLR loads on first use — so <c>@model(){{X}}</c> can bind at build time and
    /// still fail at run time, and whether it does depends on what else the process happened to touch first.
    /// <para>That is a real property of the engine with a real consequence for hosts, not a test artefact, and this
    /// suite states it outright over a purpose-built assembly that provably is not loaded when the test begins.
    /// Elsewhere the differential harness suppresses it deliberately, by loading every extra reference it is handed;
    /// the last test here pins that line, because a suppression nothing asserts is indistinguishable from a bug.</para>
    /// </summary>
    public class ModelResolutionLoadOrderTests : IDisposable
    {
        private const string Key = "views/late-bound.heddle";
        private const string Template = "@model(){{LateBoundModels.Doc}}@\\\n<p>@(Title)</p>\n";

        /// <summary>An assembly no run of this suite has loaded before: the name carries a fresh GUID, so "not
        /// loaded yet" is a fact rather than an assumption about test ordering.</summary>
        private static string BuildUnloadedModelAssembly(out string assemblyName)
        {
            assemblyName = "HeddleLateBoundModels" + Guid.NewGuid().ToString("N");
            var references = ((string) AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))
                .Split(Path.PathSeparator)
                .Where(p => !string.IsNullOrEmpty(p) && File.Exists(p))
                .Select(p => (MetadataReference) MetadataReference.CreateFromFile(p))
                .ToList();
            var compilation = CSharpCompilation.Create(assemblyName,
                new[]
                {
                    CSharpSyntaxTree.ParseText(
                        "namespace LateBoundModels { public class Doc { public string Title => \"late\"; } }")
                },
                references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            var path = Path.Combine(Path.GetTempPath(), assemblyName + ".dll");
            var emit = compilation.Emit(path);
            Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
            Emitted.Add(path);
            return path;
        }

        /// <summary>Every probe assembly this class wrote, removed when the run ends. The name has to be unique per
        /// run for "not loaded yet" to be a fact rather than a hope, so without this each run leaves another file
        /// behind for good. Best effort: the file is loaded by the time the run finishes, and a platform that locks
        /// a mapped image will refuse — one file then, rather than one per run.</summary>
        private static readonly List<string> Emitted = new List<string>();

        public void Dispose()
        {
            foreach (var path in Emitted)
            {
                try
                {
                    File.Delete(path);
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }

            Emitted.Clear();
        }

        private static Assembly Loaded(string assemblyName) =>
            AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => string.Equals(a.GetName().Name, assemblyName, StringComparison.Ordinal));

        /// <summary>
        /// The whole property in one pass: the engine refuses a model type it cannot see, the generator binds the
        /// same type from a reference alone, and once the assembly is loaded the two agree byte-for-byte. The middle
        /// step is the divergence — a host whose build is green here can still fail at first render.
        /// </summary>
        [Fact]
        public void AModelTypeBindsAtBuildTimeFromAReferenceAndAtRunTimeOnlyOnceItsAssemblyIsLoaded()
        {
            var path = BuildUnloadedModelAssembly(out var assemblyName);
            Assert.Null(Loaded(assemblyName));

            // Run tier, before the load: the engine resolves model types over loaded assemblies, and there is no
            // registration for this one, so the template does not compile at all.
            var beforeLoad = new HeddleTemplate(Template,
                new CompileContext(new TemplateOptions(), ExType.Dynamic));
            Assert.False(beforeLoad.CompileResult.Success);
            Assert.Contains(beforeLoad.CompileResult.Errors, e => e.Error.Contains("LateBoundModels.Doc"));

            // Build tier, at the same moment: a metadata reference is all it needs, so it precompiles the template
            // the engine has just refused.
            var gen = DifferentialHarness.Generate(new[] { (Key, Template) },
                extraReferences: new[] { MetadataReference.CreateFromFile(path) });
            Assert.Empty(gen.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
            DifferentialHarness.ExpectPrecompiled(gen, Key);

            // Run tier, after the load: same template, same model, and now the tiers agree. Nothing about the
            // template changed — only what the process had touched.
            var assembly = Loaded(assemblyName);
            Assert.NotNull(assembly);
            var model = Activator.CreateInstance(assembly.GetType("LateBoundModels.Doc"));

            var afterLoad = new HeddleTemplate(Template, new CompileContext(new TemplateOptions(), ExType.Dynamic));
            Assert.True(afterLoad.CompileResult.Success, afterLoad.CompileResult.ToString());
            var dynamic = afterLoad.Generate(model);

            Assert.Equal("<p>late</p>\n", dynamic);
            Assert.Equal(dynamic, DifferentialHarness.RenderGenerated(gen, Key, model));
        }

        /// <summary>
        /// The harness's own suppression, asserted rather than assumed. <c>DifferentialHarness</c> loads every extra
        /// reference it is handed, so that a corpus differential suite is deciding whether two tiers emit the same
        /// bytes and not whether some earlier test happened to touch a model type first. Without this the load is a
        /// bare side effect in a helper named for something else, and the suites that depend on it would go back to
        /// passing or failing on scheduling.
        /// </summary>
        [Fact]
        public void TheHarnessLoadsExtraReferencesSoDifferentialSuitesDoNotDependOnTestOrder()
        {
            var path = BuildUnloadedModelAssembly(out var assemblyName);
            Assert.Null(Loaded(assemblyName));

            DifferentialHarness.Generate(new[] { (Key, Template) },
                extraReferences: new[] { MetadataReference.CreateFromFile(path) });

            Assert.NotNull(Loaded(assemblyName));
        }
    }
}
