using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Heddle.Core;
using Heddle.Data;
using Heddle.Exceptions;
using Heddle.Native;
using Heddle.Runtime;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// The extension registry's registration seams: a failed registration must be repeatable, a partially
    /// unloadable assembly must still yield its loadable extensions, and a registration must never mutate the
    /// registry instance a concurrent reader already holds.
    /// </summary>
    public class TemplateFactoryRegistrationTests
    {
        /// <summary>
        /// A registration that throws must be retryable. The scan once marked the assembly as handled before the
        /// work succeeded, so a host that caught the exception and called Register again got a silent no-op and
        /// the assembly's extensions were unreachable for the life of the process. Nothing from the failed call
        /// may be published either — the registry is copy-on-write and a rejected batch publishes nothing.
        /// </summary>
        [Fact]
        public void AFailedExportRegistrationCanBeRetriedRatherThanSilentlySwallowed()
        {
            var suffix = Guid.NewGuid().ToString("N").Substring(0, 12);
            var name = "collide" + suffix;
            var source = $@"
using Heddle.Attributes;
using Heddle.Core;
using Heddle.Data;

[assembly: ExportExtensions(typeof(ProbeNamespace{suffix}.First{suffix}), typeof(ProbeNamespace{suffix}.Second{suffix}))]

namespace ProbeNamespace{suffix}
{{
    [ExtensionName(""{name}"")]
    public class First{suffix} : AbstractExtension
    {{
        public override object ProcessData(in Scope scope) => ""probe"";
        public override void RenderData(in Scope scope) => scope.Renderer.Render(""probe"");
    }}

    [ExtensionName(""{name}"")]
    public class Second{suffix} : AbstractExtension
    {{
        public override object ProcessData(in Scope scope) => ""probe"";
        public override void RenderData(in Scope scope) => scope.Renderer.Render(""probe"");
    }}
}}";
            var assembly = Assembly.Load(Compile(source, AssemblyHelper.GetApplicationReferences()));

            Assert.Throws<TemplateOverrideException>(() => TemplateFactory.RegisterExportedExtensions(assembly));
            Assert.False(TemplateFactory.Exists(name),
                "a rejected registration published part of its batch");
            Assert.Throws<TemplateOverrideException>(() => TemplateFactory.RegisterExportedExtensions(assembly));
        }

        /// <summary>
        /// One type whose base lives in an assembly the runtime cannot resolve must not take the whole scan down:
        /// <c>[ExportExtensions]</c> in its parameterless form reaches every type in the assembly, and a plugin
        /// built against a version the host does not have would otherwise throw out of the host's startup call.
        /// The premise is asserted first so a runtime that starts resolving the dependency fails loudly here
        /// instead of leaving the guard untested.
        /// </summary>
        [Fact]
        public void AnAssemblyWithOneUnloadableTypeStillYieldsItsLoadableExtensions()
        {
            var suffix = Guid.NewGuid().ToString("N").Substring(0, 12);
            var name = "good" + suffix;

            var dependencySource = $@"
namespace DepNamespace{suffix}
{{
    public class DepBase{suffix} {{ }}
}}";
            var dependencyBytes = Compile(dependencySource, AssemblyHelper.GetApplicationReferences());

            var probeReferences = AssemblyHelper.GetApplicationReferences();
            probeReferences.Add(MetadataReference.CreateFromImage(dependencyBytes));
            var probeSource = $@"
using Heddle.Attributes;
using Heddle.Core;
using Heddle.Data;

namespace ProbeNamespace{suffix}
{{
    public class Broken{suffix} : DepNamespace{suffix}.DepBase{suffix} {{ }}

    [ExtensionName(""{name}"")]
    public class Good{suffix} : AbstractExtension
    {{
        public override object ProcessData(in Scope scope) => ""probe"";
        public override void RenderData(in Scope scope) => scope.Renderer.Render(""probe"");
    }}
}}";
            // The dependency is never written to disk and never loaded, so the probe's Broken type cannot
            // resolve its base.
            var assembly = Assembly.Load(Compile(probeSource, probeReferences));

            Assert.Throws<ReflectionTypeLoadException>(() => assembly.GetTypes());

            var found = TemplateFactory.LoadExtensions(assembly).ToList();
            Assert.Contains(found, extension => extension.Name == name);
        }

        /// <summary>
        /// A registration builds a copy and publishes it in one assignment — the dictionary instance a lock-free
        /// reader already holds is never mutated under it. This pins the copy-on-write shape the concurrency fix
        /// rests on; the race itself is not claimed and not raced for —
        /// <see cref="RegistrationConcurrencyTests"/> holds the (non-falsifiable) concurrent guard, and a racing
        /// test that catches the wrong order only by luck is worse than none.
        /// </summary>
        [Fact]
        public void ARegistrationPublishesANewRegistryRatherThanMutatingTheOneReadersHold()
        {
            var name = "cow" + Guid.NewGuid().ToString("N").Substring(0, 12);
            var registryField = typeof(TemplateFactory).GetField("_registry",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(registryField);

            var before = registryField.GetValue(null);
            var beforeSnapshot = (System.Collections.Generic.Dictionary<string, Type>)before;

            TemplateFactory.AddExtensions(new[] { new ExtensionType(name, typeof(LocalProbeExtension), false) });

            Assert.False(beforeSnapshot.ContainsKey(name),
                "the registration mutated the registry instance readers already hold");
            var after = registryField.GetValue(null);
            Assert.NotSame(before, after);
            Assert.True(TemplateFactory.Exists(name));
        }

        private sealed class LocalProbeExtension : AbstractExtension
        {
            public override object ProcessData(in Scope scope) => "probe";
            public override void RenderData(in Scope scope) => scope.Renderer.Render("probe");
        }

        private static byte[] Compile(string source, System.Collections.Generic.List<MetadataReference> references)
        {
            var compilation = CSharpCompilation.Create(
                "Probe" + Guid.NewGuid().ToString("N"),
                new[] { CSharpSyntaxTree.ParseText(source) },
                references,
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
