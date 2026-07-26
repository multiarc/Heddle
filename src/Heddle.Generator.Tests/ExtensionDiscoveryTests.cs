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
    /// White-box pins on <see cref="ExtensionBinder"/>: nested-container discovery, AQN formatting with <c>+</c>
    /// and backtick arity, inherited <c>[ExtensionName]</c> precedence, and known-but-unbindable detection.
    /// </summary>
    public class ExtensionDiscoveryTests
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
            var compilation = CSharpCompilation.Create("ExtensionDiscoveryTest",
                new[] { CSharpSyntaxTree.ParseText(source) },
                References,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            return ExtensionBinder.Build(compilation);
        }

        private static readonly string NestedSource = GeneratorHarness.WithAllExtensionsExported(@"
using Heddle.Attributes;
using Heddle.Core;
using Heddle.Data;

namespace Probe
{
    public static class Container
    {
        [ExtensionName(""nestedone"")]
        public sealed class NestedExtension : AbstractExtension
        {
            public override object ProcessData(in Scope scope) => string.Empty;
            public override void RenderData(in Scope scope) { }
        }

        public static class Deeper
        {
            [ExtensionName(""nestedtwo"")]
            public sealed class DeepExtension : AbstractExtension
            {
                public override object ProcessData(in Scope scope) => string.Empty;
                public override void RenderData(in Scope scope) { }
            }
        }
    }
}");

        [Fact]
        public void ScanDescendsIntoNestedContainers()
        {
            var binder = Bind(NestedSource);

            Assert.True(binder.TryResolve("nestedone", out var one));
            Assert.True(binder.TryResolve("nestedtwo", out var two));
            Assert.Equal("Probe.Container+NestedExtension", one.BareTypeName);
            Assert.Equal("Probe.Container+Deeper+DeepExtension", two.BareTypeName);
        }

        [Fact]
        public void NestedIdentityIsSpelledWithPlusOnBothHalvesOfTheManifestRow()
        {
            var binder = Bind(NestedSource);

            Assert.True(binder.TryResolve("nestedone", out var one));
            Assert.Equal("Probe.Container+NestedExtension, ExtensionDiscoveryTest", one.AqnSansVersion);
            Assert.Equal("global::Probe.Container.NestedExtension", one.GlobalName);
        }

        private static readonly string GenericContainerSource = GeneratorHarness.WithAllExtensionsExported(@"
using Heddle.Attributes;
using Heddle.Core;
using Heddle.Data;

namespace Probe
{
    public class GenericHost<T>
    {
        [ExtensionName(""ingeneric"")]
        public sealed class Inner : AbstractExtension
        {
            public override object ProcessData(in Scope scope) => string.Empty;
            public override void RenderData(in Scope scope) { }
        }
    }
}");

        [Fact]
        public void GenericContainerIdentityCarriesTheBacktickArity()
        {
            var binder = Bind(GenericContainerSource);

            Assert.True(binder.TryResolve("ingeneric", out var info));
            Assert.Equal("Probe.GenericHost`1+Inner", info.BareTypeName);
        }

        private static readonly string InheritedNameSource = GeneratorHarness.WithAllExtensionsExported(@"
using Heddle.Attributes;
using Heddle.Core;
using Heddle.Data;

namespace Probe
{
    [ExtensionName(""inh"")]
    public class BaseExtension : AbstractExtension
    {
        public override object ProcessData(in Scope scope) => string.Empty;
        public override void RenderData(in Scope scope) { }
    }

    public sealed class DerivedExtension : BaseExtension
    {
    }
}");

        [Fact]
        public void InheritedExtensionNameRegistersTheSubclassAndTakesTheNameOver()
        {
            var binder = Bind(InheritedNameSource);

            Assert.True(binder.TryResolve("inh", out var info));
            // The runtime reads [ExtensionName] with inherit: true and replaces the incumbent through
            // IsAssignableFrom; the manifest must record the derived type, which is what the live registry holds.
            Assert.Equal("Probe.DerivedExtension", info.BareTypeName);
        }

        private static readonly string ReplaceSource = GeneratorHarness.WithAllExtensionsExported(@"
using Heddle.Attributes;
using Heddle.Core;
using Heddle.Data;

namespace Probe
{
    [ExtensionName(""if"")]
    [ExtensionReplace]
    public sealed class MyIf : AbstractExtension
    {
        public override object ProcessData(in Scope scope) => string.Empty;
        public override void RenderData(in Scope scope) { }
    }
}");

        [Fact]
        public void ExtensionReplaceDisplacesTheEngineBuiltIn()
        {
            var binder = Bind(ReplaceSource);

            Assert.True(binder.TryResolve("if", out var info));
            Assert.Equal("Probe.MyIf", info.BareTypeName);
            Assert.False(info.IsEngineAssembly);
        }

        private static readonly string SubclassOfBuiltInSource = GeneratorHarness.WithAllExtensionsExported(@"
using Heddle.Attributes;
using Heddle.Core;
using Heddle.Data;

namespace Probe
{
    public sealed class MyIf : Heddle.Extensions.IfExtension
    {
    }
}");

        [Fact]
        public void SubclassOfEngineBuiltInTakesTheNameThroughAssignability()
        {
            var binder = Bind(SubclassOfBuiltInSource);

            // A subclass with no declared name takes the inherited attribute and replaces the built-in.
            Assert.True(binder.TryResolve("if", out var info));
            Assert.Equal("Probe.MyIf", info.BareTypeName);
        }

        private static readonly string InterfaceDirectSource = GeneratorHarness.WithAllExtensionsExported(@"
using System;
using Heddle.Attributes;
using Heddle.Core;
using Heddle.Data;
using Heddle.Language;
using Heddle.Runtime;
using Heddle.Strings.Core;

namespace Probe
{
    [ExtensionName(""ifacedirect"")]
    public sealed class DirectExtension : IExtension
    {
        public BlockPosition Position { get; set; }
        public void SetUpRenderType(RenderType renderType) { }
        public ExType InitStart(InitContext initContext, ExType dataType, ExType chainedType, ExType parent) => dataType;
        public void CompleteInit(InitContext initContext) { }
        public object ProcessData(in Scope scope) => string.Empty;
        public void RenderData(in Scope scope) { }
        public void Dispose() { }
    }
}");

        [Fact]
        public void InterfaceDirectImplementorIsDiscoveredButNotBindable()
        {
            var binder = Bind(InterfaceDirectSource);

            // Known but unbindable: discovery follows the runtime, but bindability is separate.
            Assert.False(binder.TryResolve("ifacedirect", out _));
            Assert.True(binder.IsKnownToRuntime("ifacedirect"));
            Assert.True(binder.TryGetUnbindableReason("ifacedirect", out var reason));
            Assert.Contains("IExtension", reason);
        }

        [Fact]
        public void AGenuinelyUnknownNameIsNotKnownToTheRuntimeEither()
        {
            var binder = Bind(InheritedNameSource);

            Assert.False(binder.IsKnownToRuntime("no-such-extension"));
            Assert.False(binder.TryGetUnbindableReason("no-such-extension", out _));
        }

        private static readonly string UnrelatedCollisionSource = GeneratorHarness.WithAllExtensionsExported(@"
using Heddle.Attributes;
using Heddle.Core;
using Heddle.Data;

namespace Probe
{
    [ExtensionName(""clash"")]
    public sealed class AlphaExtension : AbstractExtension
    {
        public override object ProcessData(in Scope scope) => string.Empty;
        public override void RenderData(in Scope scope) { }
    }

    [ExtensionName(""clash"")]
    public sealed class BetaExtension : AbstractExtension
    {
        public override object ProcessData(in Scope scope) => string.Empty;
        public override void RenderData(in Scope scope) { }
    }
}");

        [Fact]
        public void UnrelatedClaimantsDegradeRatherThanBindOrError()
        {
            var binder = Bind(UnrelatedCollisionSource);

            // Degrade rather than silent bind or error; the runtime raises TemplateOverrideException on collision.
            Assert.False(binder.TryResolve("clash", out _));
            Assert.True(binder.IsKnownToRuntime("clash"));
            Assert.True(binder.TryGetUnbindableReason("clash", out var reason));
            Assert.Contains("TemplateOverrideException", reason);
        }

        [Fact]
        public void BranchRoleHasExactlyOneDefinitionInTheRepository()
        {
            // Linked source: both assemblies declare the name, requiring reflection by name to avoid CS0433.
            var runtime = typeof(Heddle.Precompiled.PrecompiledTemplates).Assembly
                .GetType("Heddle.Attributes.BranchRole", throwOnError: true);
            var linked = typeof(ExtensionBinder).Assembly
                .GetType("Heddle.Attributes.BranchRole", throwOnError: true);

            Assert.Equal(runtime.FullName, linked.FullName);
            Assert.Equal(Enum.GetNames(runtime), Enum.GetNames(linked));
            Assert.Equal(
                Enum.GetValues(runtime).Cast<object>().Select(Convert.ToInt32),
                Enum.GetValues(linked).Cast<object>().Select(Convert.ToInt32));
        }
    }
}
