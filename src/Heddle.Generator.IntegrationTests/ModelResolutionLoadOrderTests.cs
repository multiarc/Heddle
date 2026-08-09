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

        /// <summary>The template for one probe. The model's namespace carries the probe's own GUID, so the type
        /// each test names can only ever be satisfied by the assembly that test built.</summary>
        private static string TemplateFor(string ns) =>
            "@model(){{" + ns + ".Doc}}@\\\n<p>@(Title)</p>\n";

        /// <summary>An assembly no run of this suite has loaded before, carrying a type no other test can supply:
        /// both the assembly name and the model's namespace carry a fresh GUID.
        /// <para>The namespace has to vary too, not just the assembly name. These tests assert that a model type is
        /// <i>not</i> resolvable before its assembly loads, and the engine resolves a type by name over everything
        /// loaded — so a sibling that had already loaded its own probe under a shared namespace would satisfy the
        /// lookup and quietly turn "the engine cannot see it" into "the engine can". That made the outcome depend on
        /// which test ran first, which held only for as long as the runner's ordering did.</para></summary>
        private static string BuildUnloadedModelAssembly(out string assemblyName, out string modelNamespace)
        {
            var id = Guid.NewGuid().ToString("N");
            assemblyName = "HeddleLateBoundModels" + id;
            modelNamespace = "LateBoundModels" + id;
            return BuildModelAssembly(assemblyName, modelNamespace);
        }

        /// <summary>The same probe with both names supplied, so a caller can build <b>two</b> assemblies declaring
        /// one spelling — which is the only way to reach an ambiguity, and the ambiguity is a property worth
        /// pinning rather than a mistake to avoid.</summary>
        private static string BuildModelAssembly(string assemblyName, string modelNamespace)
        {
            var references = (Heddle.Generator.Tests.HostAssemblies.TrustedOrLoaded())
                .Split(Path.PathSeparator)
                .Where(p => !string.IsNullOrEmpty(p) && File.Exists(p))
                .Select(p => (MetadataReference) MetadataReference.CreateFromFile(p))
                .ToList();
            var compilation = CSharpCompilation.Create(assemblyName,
                new[]
                {
                    CSharpSyntaxTree.ParseText(
                        "namespace " + modelNamespace +
                        " { public class Doc { public string Title => \"late\"; } }")
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
            var path = BuildUnloadedModelAssembly(out var assemblyName, out var ns);
            var template = TemplateFor(ns);
            Assert.Null(Loaded(assemblyName));

            // Run tier, before the load: the engine resolves model types over loaded assemblies, and there is no
            // registration for this one, so the template does not compile at all.
            var beforeLoad = new HeddleTemplate(template,
                new CompileContext(new TemplateOptions(), ExType.Dynamic));
            Assert.False(beforeLoad.CompileResult.Success);
            Assert.Contains(beforeLoad.CompileResult.Errors, e => e.Error.Contains(ns + ".Doc"));

            // Build tier, at the same moment: a metadata reference is all it needs, so it precompiles the template
            // the engine has just refused.
            var gen = DifferentialHarness.Generate(new[] { (Key, template) },
                extraReferences: new[] { MetadataReference.CreateFromFile(path) });
            Assert.Empty(gen.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
            DifferentialHarness.ExpectPrecompiled(gen, Key);

            // Run tier, after the load: same template, same model, and now the tiers agree. Nothing about the
            // template changed — only what the process had touched.
            var assembly = Loaded(assemblyName);
            Assert.NotNull(assembly);
            var model = Activator.CreateInstance(assembly.GetType(ns + ".Doc"));

            var afterLoad = new HeddleTemplate(template, new CompileContext(new TemplateOptions(), ExType.Dynamic));
            Assert.True(afterLoad.CompileResult.Success, afterLoad.CompileResult.ToString());
            var dynamic = afterLoad.Generate(model);

            Assert.Equal("<p>late</p>\n", dynamic);
            Assert.Equal(dynamic, DifferentialHarness.RenderGenerated(gen, Key, model));
        }

        /// <summary>
        /// <b>The negative control.</b> Its sibling above pins the two tiers agreeing in <i>success</i> once the
        /// model assembly is both referenced and loaded. Agreement in success is the easy half: a build that
        /// precompiles nothing and a run tier that renders everything dynamically also agree, byte for byte. The
        /// half that says the design is right is agreement in <b>refusal</b> — configure neither tier, and neither
        /// tier may claim the template.
        /// <para>Configuration is the whole variable. Nothing here is a metadata reference the generator can bind
        /// from and nothing here is loaded into the process, so the build tier resolves no symbol for the spelling
        /// and the run tier resolves no <c>Type</c> for it, and both say so — the generator declining to emit and
        /// reporting <c>HED7007</c>, the engine failing to compile at all.</para>
        /// <para>Between these two tests sits the case the assembly attribute exists to remove: referenced but not
        /// loaded, where the build precompiles a template the engine refuses. That case is pinned above as the
        /// divergence it is; a host that writes <c>[HeddleModelAssembly(typeof(T))]</c> cannot enter it, because the
        /// one declaration is simultaneously the reference the build resolves against and the registration the
        /// engine reads.</para>
        /// </summary>
        [Fact]
        public void WithNeitherTierConfiguredTheTwoTiersAgreeInRefusal()
        {
            BuildUnloadedModelAssembly(out var assemblyName, out var ns);
            var template = TemplateFor(ns);
            Assert.Null(Loaded(assemblyName));

            var run = new HeddleTemplate(template, new CompileContext(new TemplateOptions(), ExType.Dynamic));
            Assert.False(run.CompileResult.Success);
            Assert.Contains(run.CompileResult.Errors, e => e.Error.Contains(ns + ".Doc"));

            // No extraReferences: the generator is handed exactly what an unconfigured project would hand it.
            var gen = DifferentialHarness.Generate(new[] { (Key, template) });
            DifferentialHarness.ExpectDegrade(gen, Key);
            Assert.Contains(gen.Diagnostics,
                d => d.Id == HeddleDiagnosticIds.BuildUnresolvableModelType);

            // And the build tier did not reach for the assembly on its own to close the gap it just reported.
            Assert.Null(Loaded(assemblyName));
        }

        /// <summary>
        /// Ambiguity that configuration <b>creates</b> is not a regression, and this is the shape of the argument:
        /// two assemblies declare one spelling, and both tiers refuse the same input for the same reason — the build
        /// with <c>HED7023</c>, the engine with its "the type name is ambigous" throw. A host that registers both
        /// assemblies gets exactly what a build that references both gets.
        /// <para>Which is why widening what the build can see cannot make a template <i>worse</i> off than the
        /// runtime it has to match: the two tiers were already answering this question the same way, and parity
        /// preserved in the failure is stronger evidence than parity in the success, because a failure has many more
        /// ways to differ.</para>
        /// </summary>
        [Fact]
        public void AmbiguityThatConfigurationCreatesIsRefusedByBothTiersForTheSameInput()
        {
            var id = Guid.NewGuid().ToString("N");
            var ns = "AmbiguousModels" + id;
            var first = BuildModelAssembly("HeddleAmbiguousModelsA" + id, ns);
            var second = BuildModelAssembly("HeddleAmbiguousModelsB" + id, ns);
            var template = TemplateFor(ns);

            // Build tier: two references, one spelling. The harness loads what it is handed, so this also puts both
            // assemblies in the process — which is the run tier's half of the same configuration.
            var gen = DifferentialHarness.Generate(new[] { (Key, template) },
                extraReferences: new[]
                {
                    MetadataReference.CreateFromFile(first), MetadataReference.CreateFromFile(second)
                });

            DifferentialHarness.ExpectDegrade(gen, Key);
            Assert.Contains(gen.Diagnostics, d => d.Id == HeddleDiagnosticIds.BuildAmbiguousTypeName);

            var run = new HeddleTemplate(template, new CompileContext(new TemplateOptions(), ExType.Dynamic));
            Assert.False(run.CompileResult.Success);
            Assert.Contains(run.CompileResult.Errors, e => e.Error.Contains("ambigous"));
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
            var path = BuildUnloadedModelAssembly(out var assemblyName, out var ns);
            Assert.Null(Loaded(assemblyName));

            DifferentialHarness.Generate(new[] { (Key, TemplateFor(ns)) },
                extraReferences: new[] { MetadataReference.CreateFromFile(path) });

            Assert.NotNull(Loaded(assemblyName));
        }
    }
}
