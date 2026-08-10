using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Heddle.Data;
using Heddle.Runtime;
using Heddle.Runtime.Expressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// A public <c>[ExportFunctions]</c> container nested inside an <c>internal</c> one. The runtime's registry spells
    /// "public container" the way reflection does — <c>IsPublic || IsNestedPublic</c>, which is one question about the
    /// type's own declared accessibility — so it registers such a container's functions and renders them. The build
    /// tier asked a different question, walking the containing chain and demanding public at every level, and reported
    /// the container as ineligible at <c>Location.None</c>: an error with no template position, which fails the
    /// <b>whole compilation</b> rather than one template, and takes every other export in the same assembly down with
    /// it.
    /// <para>Whether generated code may <em>spell</em> the container is a second and separate question, and it is
    /// asked where the call is written. From a referenced assembly an internal outer type is <c>CS0122</c> here, so
    /// the call degrades to the engine — one template, silently, instead of the build.</para>
    /// </summary>
    public class NestedExportContainerTests
    {
        private const string ProbeSource = @"
[assembly: Heddle.Attributes.ExportFunctions(typeof(Probe.Exports.InternalOuter.NestedFunctions))]
[assembly: Heddle.Attributes.ExportFunctions(typeof(Probe.Exports.PublicFunctions))]

namespace Probe.Exports
{
    internal static class InternalOuter
    {
        public static class NestedFunctions
        {
            public static string NestedEcho(string value) => ""<"" + value + "">"";
        }
    }

    public static class PublicFunctions
    {
        public static string PlainEcho(string value) => ""|"" + value + ""|"";
    }
}";

        /// <summary>Built once per run and left on disk for the process lifetime — it is mapped as a metadata
        /// reference for as long as any test in the class is running.</summary>
        private static readonly Lazy<string> Probe = new Lazy<string>(() =>
        {
            var name = "HeddleProbeNestedExports" + Guid.NewGuid().ToString("N");
            var compilation = CSharpCompilation.Create(name,
                new[] { CSharpSyntaxTree.ParseText(ProbeSource) },
                DifferentialHarness.BaseReferences,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            var path = Path.Combine(Path.GetTempPath(), name + ".dll");
            var emit = compilation.Emit(path);
            Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
            AppDomain.CurrentDomain.ProcessExit += (_, __) =>
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
            };
            return path;
        });

        private static MetadataReference[] ProbeReference() =>
            new[] { (MetadataReference) MetadataReference.CreateFromFile(Probe.Value) };

        private static TemplateOptions EngineOptions()
        {
            var registry = new FunctionRegistry();
            registry.RegisterFrom(Assembly.LoadFrom(Probe.Value));
            return new TemplateOptions { Functions = registry };
        }

        /// <summary>The measurement the build tier's diagnostic contradicted: <c>RegisterFrom</c> does not throw on
        /// this assembly, and both containers' functions are registered.</summary>
        [Fact]
        public void TheRuntimeRegistryAcceptsAPublicContainerNestedInAnInternalOne()
        {
            var registry = new FunctionRegistry();
            registry.RegisterFrom(Assembly.LoadFrom(Probe.Value));

            Assert.True(registry.Contains("nestedecho"));
            Assert.True(registry.Contains("plainecho"));
        }

        /// <summary>No build error anywhere, and the call goes to the engine because this assembly may not name the
        /// container — not because the container was called ineligible.</summary>
        [Fact]
        public void ACallToANestedPublicContainerDegradesWithoutFailingTheBuild()
        {
            const string key = "views/nested-export.heddle";
            const string template = "@(nestedecho(\"x\"))\n";
            var gen = DifferentialHarness.Generate(new[] { (key, template) }, extraReferences: ProbeReference());

            Assert.Empty(gen.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
            DifferentialHarness.ExpectDegrade(gen, key);

            var engine = new HeddleTemplate(template, new CompileContext(EngineOptions(), ExType.Dynamic));
            Assert.True(engine.CompileResult.Success, engine.CompileResult.ToString());
            Assert.Equal("&lt;x&gt;\n", engine.Generate(null));
        }

        /// <summary>The collateral, and the reason the severity was what it was: an ordinary public container in the
        /// <b>same</b> assembly precompiles and renders. Before, the ineligible-container error at
        /// <c>Location.None</c> failed the compilation, so this template produced nothing either.</summary>
        [Fact]
        public void APublicContainerInTheSameAssemblyStillPrecompilesAndRenders()
        {
            const string key = "views/nested-export-neighbour.heddle";
            const string template = "@(plainecho(\"x\"))\n";

            var (precompiled, dyn) = DifferentialHarness.Render(key, template, null, null,
                runtimeOptions: EngineOptions(), extraReferences: ProbeReference());
            Assert.Equal("|x|\n", dyn);
            Assert.Equal(dyn, precompiled);
        }
    }
}
