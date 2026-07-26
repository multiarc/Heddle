using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Heddle.Helpers;
using Heddle.Native;
using Heddle.Runtime;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// The engine loads no assembly on its own, and only assemblies a host registers offer extension names. An
    /// assembly that merely happens to be in the process must never take a name or a type spelling.
    /// </summary>
    public class AssemblyRegistrationTests
    {
        /// <summary>
        /// An explicit static constructor is what force-loaded the entry assembly's whole reference closure at type
        /// init. <c>beforefieldinit</c> is present exactly when a type declares none, so this reddens if one returns.
        /// </summary>
        [Fact]
        public void AssemblyHelperDoesNoWorkAtTypeInitialization()
        {
            Assert.True(typeof(AssemblyHelper).Attributes.HasFlag(TypeAttributes.BeforeFieldInit),
                "AssemblyHelper declares an explicit static constructor.");
        }

        /// <summary>The dependency-context walk was the only consumer, so the package reference went with it.</summary>
        [Fact]
        public void EngineDoesNotReferenceTheDependencyModelPackage()
        {
            Assert.DoesNotContain("Microsoft.Extensions.DependencyModel",
                typeof(HeddleTemplate).Assembly.GetReferencedAssemblies().Select(name => name.Name));
        }

        /// <summary>
        /// The registration seam end to end: an assembly carrying <c>[ExportExtensions]</c> is compiled and loaded
        /// into the process, then asserted invisible until registered — neither its extension name nor its type
        /// spelling resolves. One <see cref="HeddleTemplate.Register(Assembly)"/> call makes both appear.
        /// </summary>
        [Fact]
        public void LoadedAssemblyOffersNothingUntilRegistered()
        {
            var suffix = Guid.NewGuid().ToString("N").Substring(0, 12);
            var extensionName = "probe" + suffix;
            var typeName = "ProbeExtension" + suffix;
            var probe = CompileAndLoad(ProbeSource(suffix, extensionName, typeName));

            Assert.False(TemplateFactory.Exists(extensionName),
                "an unregistered assembly took an extension name");
            Assert.Throws<InvalidOperationException>(
                () => ReflectionHelper.ResolveType($"ProbeNamespace{suffix}.{typeName}"));

            HeddleTemplate.Register(probe);

            Assert.True(TemplateFactory.Exists(extensionName));
            Assert.NotNull(ReflectionHelper.ResolveType($"ProbeNamespace{suffix}.{typeName}"));
        }

        /// <summary>Registration is repeatable: a host may register in whatever order it establishes precedence.</summary>
        [Fact]
        public void RegisteringTheSameAssemblyTwiceIsIdempotent()
        {
            var assembly = typeof(AssemblyRegistrationTests).Assembly;
            HeddleTemplate.Register(assembly);
            HeddleTemplate.Register(assembly);
        }

        /// <summary>
        /// The behavioural pin, and the one that matters. The sibling tests pin the *mechanism* the old walk used — no
        /// explicit static constructor, no DependencyModel — and a review found that restoring the same discovery
        /// lazily, from <c>GetAssemblies()</c> instead of a type initializer, passed the whole suite. This test states
        /// the behaviour instead: an assembly written to disk and loaded into the default context, which every
        /// observation path therefore sees, must still offer no extension name until the host registers it. Any
        /// reintroduced scan-all reddens here whatever triggers it.
        /// <para>File-backed on purpose: an in-memory probe has no <c>Location</c>, so the observation filter skips it
        /// and the negative assertion would hold for the wrong reason.</para>
        /// </summary>
        [Fact]
        public void AnObservedOnDiskAssemblyOffersNoExtensionNameUntilRegistered()
        {
            var suffix = Guid.NewGuid().ToString("N").Substring(0, 12);
            var extensionName = "ondisk" + suffix;
            var probe = CompileToFileAndLoad(ProbeSource(suffix, extensionName, "OnDiskExtension" + suffix));

            Assert.False(string.IsNullOrEmpty(probe.Location),
                "the probe must be file-backed, or it is skipped by the observation filter and proves nothing");
            Assert.Contains(probe, AssemblyHelper.GetAssemblies());

            Assert.False(TemplateFactory.Exists(extensionName),
                "an observed but unregistered on-disk assembly took an extension name — scan-all discovery is back");

            HeddleTemplate.Register(probe);
            Assert.True(TemplateFactory.Exists(extensionName));
        }

        [Fact]
        public void RegisterRejectsNull()
        {
            Assert.Throws<ArgumentNullException>(() => HeddleTemplate.Register(null));
        }

        private static string ProbeSource(string suffix, string extensionName, string typeName) => $@"
using Heddle.Attributes;
using Heddle.Core;
using Heddle.Data;

[assembly: ExportExtensions(typeof(ProbeNamespace{suffix}.{typeName}))]

namespace ProbeNamespace{suffix}
{{
    [ExtensionName(""{extensionName}"")]
    public class {typeName} : AbstractExtension
    {{
        public override object ProcessData(in Scope scope) => ""probe"";
        public override void RenderData(in Scope scope) => scope.Renderer.Render(""probe"");
    }}
}}";

        /// <summary>Emits the probe to a real file and loads it by path, so it enters the default load context with a
        /// non-empty <see cref="Assembly.Location"/> — the shape the observation path accepts.</summary>
        private static Assembly CompileToFileAndLoad(string source)
        {
            var bytes = Emit(source, out var assemblyName);
            var path = Path.Combine(Path.GetTempPath(), assemblyName + ".dll");
            File.WriteAllBytes(path, bytes);
            return Assembly.LoadFrom(path);
        }

        private static Assembly CompileAndLoad(string source)
        {
            return Assembly.Load(Emit(source, out _));
        }

        private static byte[] Emit(string source, out string assemblyName)
        {
            assemblyName = "Probe" + Guid.NewGuid().ToString("N");
            var compilation = CSharpCompilation.Create(
                assemblyName,
                new[] { CSharpSyntaxTree.ParseText(source) },
                AssemblyHelper.GetApplicationReferences(),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            using var stream = new MemoryStream();
            var result = compilation.Emit(stream);
            Assert.True(result.Success,
                string.Join("\n", result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)
                    .Select(d => d.GetMessage())));

            return stream.ToArray();
        }
    }
}
