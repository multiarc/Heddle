using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Heddle.Generator.Binding;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Heddle.Generator.Tests
{
    /// <summary>
    /// The probe that decides whether a member the symbol model does not show is a typo or something this
    /// compilation is simply not allowed to see. It is what stands between a template the engine renders and a
    /// build-breaking HED7008, so the ways it can answer "no" wrongly are all worth pinning: a nested receiver whose
    /// CLR name is spelled the way source spells it rather than the way metadata does, a name that more than one
    /// reference happens to carry, and a grant that makes the member visible after all.
    /// </summary>
    public class MetadataAccessibilityProbeTests
    {
        private const string ModelSource = @"
namespace Probe.Hidden
{
    public sealed class Outer
    {
        public sealed class Inner
        {
            public string Shown => ""shown"";
            internal string Concealed => ""concealed"";
        }
    }

    public sealed class Flat
    {
        internal string Concealed => ""concealed"";
    }
}";

        private const string ConsumerName = "Probe.Consumer";

        private static readonly IReadOnlyList<MetadataReference> Framework = BuildFramework();

        private static IReadOnlyList<MetadataReference> BuildFramework()
        {
            var tpa = (string) AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES");
            return tpa.Split(Path.PathSeparator)
                .Where(p => !string.IsNullOrEmpty(p) && File.Exists(p))
                .Where(p => !string.Equals(Path.GetFileNameWithoutExtension(p), "Heddle.Generator",
                    StringComparison.OrdinalIgnoreCase))
                .Select(p => (MetadataReference) MetadataReference.CreateFromFile(p))
                .ToList();
        }

        /// <summary>A real compiled assembly, not a compilation reference: only a metadata reference reproduces the
        /// import rule this probe exists for — that a referencing compilation is shown nothing it could not name.</summary>
        private static MetadataReference ModelAssembly(string assemblyName, string source = ModelSource)
        {
            var compilation = CSharpCompilation.Create(assemblyName,
                new[] { CSharpSyntaxTree.ParseText(source) }, Framework,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            using var stream = new MemoryStream();
            var emit = compilation.Emit(stream);
            Assert.True(emit.Success, string.Join("\n", emit.Diagnostics.Where(
                d => d.Severity == DiagnosticSeverity.Error).Select(d => d.ToString())));
            return MetadataReference.CreateFromImage(stream.ToArray());
        }

        private static (SymbolTypeResolver Resolver, Compilation Compilation) Consumer(
            params MetadataReference[] models)
        {
            var references = new List<MetadataReference>(Framework);
            references.AddRange(models);
            var compilation = CSharpCompilation.Create(ConsumerName, Array.Empty<SyntaxTree>(), references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            return (new SymbolTypeResolver(compilation), compilation);
        }

        private static INamedTypeSymbol TypeFrom(Compilation compilation, MetadataReference reference, string name) =>
            ((IAssemblySymbol) compilation.GetAssemblyOrModuleSymbol(reference)).GetTypeByMetadataName(name);

        /// <summary>
        /// The receiver is a nested type, so the name the probe looks up has to be the CLR's — <c>Outer+Inner</c>,
        /// not the <c>Outer.Inner</c> that source and every display string use. Get that wrong and the lookup finds
        /// nothing, which reads exactly like "the member is not there" and reports the template as an error.
        /// </summary>
        [Fact]
        public void AnInternalMemberOnANestedReceiverIsRecognisedAsHidden()
        {
            var model = ModelAssembly("Probe.Models.Nested");
            var (resolver, compilation) = Consumer(model);
            var inner = TypeFrom(compilation, model, "Probe.Hidden.Outer+Inner");
            Assert.NotNull(inner);

            // The premise: the referencing compilation genuinely cannot see it, so the probe is the only thing that
            // can tell this from a typo.
            Assert.Equal(SymbolTypeResolver.PathKind.Failed,
                resolver.ResolvePath(inner, new[] { "Concealed" }).Kind);

            Assert.True(resolver.HiddenByAccessibility(inner, "Concealed"));
            Assert.False(resolver.HiddenByAccessibility(inner, "NotDeclaredAnywhere"));
        }

        /// <summary>
        /// The same type name in two references. The singular lookup answers null on that, which is indistinguishable
        /// from "no such type" — so the member failure went back to an error, at the exact severity the probe was
        /// added to avoid, over a template the engine renders. Two assemblies carrying one name is ordinary in a
        /// large closure; it is a reason to be careful about which type is meant, not evidence of a typo.
        /// </summary>
        [Fact]
        public void AnAmbiguousReceiverTypeNameStillDegrades()
        {
            var first = ModelAssembly("Probe.Models.First");
            var second = ModelAssembly("Probe.Models.Second");
            var (resolver, compilation) = Consumer(first, second);
            var flat = TypeFrom(compilation, first, "Probe.Hidden.Flat");
            Assert.NotNull(flat);
            Assert.Null(compilation.GetTypeByMetadataName("Probe.Hidden.Flat"));   // the ambiguity, stated

            Assert.True(resolver.HiddenByAccessibility(flat, "Concealed"));
            Assert.False(resolver.HiddenByAccessibility(flat, "NotDeclaredAnywhere"));
        }

        /// <summary>
        /// With <c>[InternalsVisibleTo]</c> the member is not hidden from this compilation at all: it is imported
        /// like any other, the path resolves, and the template pre-compiles instead of degrading. The remedy HED7030
        /// recommends has to actually work, and nothing checked that it did.
        /// </summary>
        [Fact]
        public void AGrantedInternalMemberResolvesInsteadOfDegrading()
        {
            var granted = ModelAssembly("Probe.Models.Granted",
                "[assembly: System.Runtime.CompilerServices.InternalsVisibleTo(\"" + ConsumerName + "\")]" +
                ModelSource);
            var (resolver, compilation) = Consumer(granted);
            var flat = TypeFrom(compilation, granted, "Probe.Hidden.Flat");

            var resolution = resolver.ResolvePath(flat, new[] { "Concealed" });
            Assert.Equal(SymbolTypeResolver.PathKind.Resolved, resolution.Kind);
            Assert.Equal(SpecialType.System_String, resolution.ResultType.SpecialType);
        }

        // What is NOT pinned here: how OFTEN the probe runs. It builds a second compilation over the whole reference
        // closure, and it now runs only where a member failure is about to become a diagnostic rather than on every
        // member miss the typed walk produces — including the ones the operand-type estimator makes and discards, and
        // the ones the emitter suppresses outright. That is a cost, and a cost is not an assertion: the answers above
        // are identical either way. Counting the probes would mean a counter in production code that exists for a
        // test to read.

        /// <summary>Without the grant, the same member on the same source is refused — so the test above is measuring
        /// the grant and not something the model would have done anyway.</summary>
        [Fact]
        public void TheSameMemberWithoutTheGrantDoesNotResolve()
        {
            var ungranted = ModelAssembly("Probe.Models.Ungranted");
            var (resolver, compilation) = Consumer(ungranted);
            var flat = TypeFrom(compilation, ungranted, "Probe.Hidden.Flat");

            Assert.Equal(SymbolTypeResolver.PathKind.Failed,
                resolver.ResolvePath(flat, new[] { "Concealed" }).Kind);
            Assert.True(resolver.HiddenByAccessibility(flat, "Concealed"));
        }
    }
}
