using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Heddle.Generator.Emit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Heddle.Generator.Tests
{
    /// <summary>
    /// The generator's extension-discovery scope. The runtime registers only what <c>[assembly: ExportExtensions(...)]</c>
    /// names, but the generator unconditionally scanned every referenced assembly — binding extensions the runtime will
    /// never register, so templates fall back permanently. These tests probe compilations without the attribute.
    /// </summary>
    public class ExportExtensionsScopeTests
    {
        private static readonly IReadOnlyList<MetadataReference> References = BuildReferences();

        private static IReadOnlyList<MetadataReference> BuildReferences()
        {
            var tpa = (string) AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES");
            var refs = tpa.Split(Path.PathSeparator)
                .Where(p => !string.IsNullOrEmpty(p) && File.Exists(p))
                .Where(p => !string.Equals(Path.GetFileNameWithoutExtension(p), "Heddle.Generator",
                    StringComparison.OrdinalIgnoreCase))
                .Select(p => (MetadataReference) MetadataReference.CreateFromFile(p))
                .ToList();
            refs.Add(MetadataReference.CreateFromFile(
                typeof(Heddle.Precompiled.PrecompiledTemplates).Assembly.Location));
            return refs;
        }

        private static ExtensionBinder Bind(string source)
        {
            var compilation = CSharpCompilation.Create("ExportScopeProbe",
                new[] { CSharpSyntaxTree.ParseText(source) }, References,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            return ExtensionBinder.Build(compilation);
        }

        /// <summary>Two extensions in one assembly, so a selective export can name one and not the other.</summary>
        private const string Bodies = @"
namespace Probe
{
    [Heddle.Attributes.ExtensionName(""alpha"")]
    public sealed class AlphaExtension : Heddle.Core.AbstractExtension
    {
        public override object ProcessData(in Heddle.Data.Scope scope) => string.Empty;
        public override void RenderData(in Heddle.Data.Scope scope) { }
    }

    [Heddle.Attributes.ExtensionName(""beta"")]
    public sealed class BetaExtension : Heddle.Core.AbstractExtension
    {
        public override object ProcessData(in Heddle.Data.Scope scope) => string.Empty;
        public override void RenderData(in Heddle.Data.Scope scope) { }
    }
}";

        [Fact]
        public void AnAssemblyWithNoExportAttributeContributesNothing()
        {
            // The runtime never scans this assembly, so binding anything from it produces unresolvable names.
            var binder = Bind(Bodies);

            Assert.False(binder.TryResolve("alpha", out _));
            Assert.False(binder.TryResolve("beta", out _));
            Assert.False(binder.IsKnownToRuntime("alpha"));
            Assert.False(binder.IsKnownToRuntime("beta"));
        }

        [Fact]
        public void ASelectiveExportContributesOnlyTheTypesItNames()
        {
            var binder = Bind(
                "[assembly: Heddle.Attributes.ExportExtensions(typeof(Probe.AlphaExtension))]" + Bodies);

            Assert.True(binder.TryResolve("alpha", out _));
            Assert.False(binder.TryResolve("beta", out _));
            Assert.False(binder.IsKnownToRuntime("beta"));
        }

        [Fact]
        public void SeveralSelectiveExportsAccumulate()
        {
            // The attribute is AllowMultiple, and the runtime iterates every occurrence.
            var binder = Bind(
                "[assembly: Heddle.Attributes.ExportExtensions(typeof(Probe.AlphaExtension))]" +
                "[assembly: Heddle.Attributes.ExportExtensions(typeof(Probe.BetaExtension))]" + Bodies);

            Assert.True(binder.TryResolve("alpha", out _));
            Assert.True(binder.TryResolve("beta", out _));
        }

        [Fact]
        public void TheParameterlessAllFormContributesEveryTypeInTheAssembly()
        {
            var binder = Bind("[assembly: Heddle.Attributes.ExportExtensions]" + Bodies);

            Assert.True(binder.TryResolve("alpha", out _));
            Assert.True(binder.TryResolve("beta", out _));
        }

        [Fact]
        public void AnAllFormShortCircuitsTheAssemblysRemainingAttributes()
        {
            // The runtime breaks on the first All form, so subsequent selective attributes are never read.
            var binder = Bind(
                "[assembly: Heddle.Attributes.ExportExtensions]" +
                "[assembly: Heddle.Attributes.ExportExtensions(typeof(Probe.AlphaExtension))]" + Bodies);

            Assert.True(binder.TryResolve("alpha", out _));
            Assert.True(binder.TryResolve("beta", out _));
        }

        [Fact]
        public void TheEngineAssemblyIsAlwaysInScopeWithoutAnAttribute()
        {
            // LoadBaseExtensions scans the Heddle assembly unconditionally, and Heddle carries no
            // [ExportExtensions] on itself — so the built-ins must stay discoverable in a compilation whose own
            // assembly exports nothing.
            var binder = Bind(Bodies);

            Assert.True(binder.TryResolve("if", out var ifInfo));
            Assert.True(ifInfo.IsEngineAssembly);
            Assert.True(binder.TryResolve("else", out _));
            Assert.True(binder.TryResolve("raw", out _));
        }

        [Fact]
        public void AnExportedTypeThatIsNotAnExtensionIsIgnored()
        {
            // The runtime feeds the named types through the same LoadExtensions predicate, which filters on
            // IExtension + an inherited [ExtensionName]; naming an unrelated type registers nothing.
            var binder = Bind(
                "[assembly: Heddle.Attributes.ExportExtensions(typeof(Probe.NotAnExtension))]" +
                "namespace Probe { public sealed class NotAnExtension { } }");

            Assert.False(binder.IsKnownToRuntime("alpha"));
            // …and the engine's own extensions are unaffected.
            Assert.True(binder.TryResolve("if", out _));
        }

        [Fact]
        public void ANestedExportedTypeIsStillDiscovered()
        {
            // A nested extension the attribute names must stay in scope, and its identity is still the '+'-spelled AQN.
            var binder = Bind(
                "[assembly: Heddle.Attributes.ExportExtensions(typeof(Probe.Container.NestedExtension))]" + @"
namespace Probe
{
    public static class Container
    {
        [Heddle.Attributes.ExtensionName(""nested"")]
        public sealed class NestedExtension : Heddle.Core.AbstractExtension
        {
            public override object ProcessData(in Heddle.Data.Scope scope) => string.Empty;
            public override void RenderData(in Heddle.Data.Scope scope) { }
        }
    }
}");

            Assert.True(binder.TryResolve("nested", out var info));
            Assert.Equal("Probe.Container+NestedExtension", info.BareTypeName);
        }

        [Fact]
        public void AnAllExportStillDescendsIntoNestedContainers()
        {
            var binder = Bind("[assembly: Heddle.Attributes.ExportExtensions]" + @"
namespace Probe
{
    public static class Container
    {
        [Heddle.Attributes.ExtensionName(""nested"")]
        public sealed class NestedExtension : Heddle.Core.AbstractExtension
        {
            public override object ProcessData(in Heddle.Data.Scope scope) => string.Empty;
            public override void RenderData(in Heddle.Data.Scope scope) { }
        }
    }
}");

            Assert.True(binder.TryResolve("nested", out var info));
            Assert.Equal("Probe.Container+NestedExtension", info.BareTypeName);
        }

        [Fact]
        public void AnUnexportedNameIsNotKnownToTheRuntimeSoABodiedCallIsHed7006()
        {
            // The runtime cannot resolve the name, so the build tier raises HED7006 instead of binding it.
            var run = GeneratorHarness.RunWithSources(
                new[] { ("views/unexported.heddle", "@model(){{System.String}}@\\\n@alpha(this){{body}}\n") },
                new[] { Bodies });

            var hed7006 = run.GeneratorDiagnostics.FirstOrDefault(d => d.Id == "HED7006");
            Assert.NotEqual(default, hed7006);
            Assert.Equal(DiagnosticSeverity.Error, hed7006.Severity);
            Assert.Contains("alpha", hed7006.GetMessage());
        }

        [Fact]
        public void AnExportedNameStillBindsEndToEnd()
        {
            var run = GeneratorHarness.RunWithSources(
                new[] { ("views/exported.heddle", "@model(){{System.String}}@\\\n@alpha(this)\n") },
                new[]
                {
                    "[assembly: Heddle.Attributes.ExportExtensions(typeof(Probe.AlphaExtension))]" + Bodies
                });

            Assert.DoesNotContain(run.GeneratorDiagnostics, d => d.Severity == DiagnosticSeverity.Error);
            Assert.Contains(run.GeneratedSourceTexts, t => t.Contains("Probe.AlphaExtension"));
        }
    }
}
