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
    /// What the member-path memo is allowed to consider "the same question". Its answer carries the resolved type,
    /// which decides the null-safety form, the numeric widening, the formatter and the member name the emitter
    /// writes — so handing one path another path's answer is silently wrong output, not a slow build.
    /// <para>These are resolver-level on purpose. No template reaches either collision today: a duplicated type name
    /// in one namespace is reported as ambiguous before a model type is ever resolved to a symbol, and a path segment
    /// carrying a dot is not spellable in a member path. Both are one edit away from reachable — a hop receiver
    /// becoming a memo key, a second reference arriving in the closure — and the cost of being wrong here is the
    /// worst kind, so the memo is pinned where it can be reached at all.</para>
    /// </summary>
    public class PathMemoizationTests
    {
        private const string DuplicateNameSource = @"
namespace Dup
{
    public sealed class Thing
    {
        public {0} Value => default({0});
    }
}";

        private const string NestedPathSource = @"
namespace Walk
{
    public sealed class Mid
    {
        public int B => 1;
    }

    public sealed class Root
    {
        public Mid A => new Mid();
    }
}";

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

        private static MetadataReference ModelAssembly(string assemblyName, string source)
        {
            var compilation = CSharpCompilation.Create(assemblyName,
                new[] { CSharpSyntaxTree.ParseText(source) }, Framework,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            using var stream = new MemoryStream();
            var emit = compilation.Emit(stream);
            Assert.True(emit.Success, string.Join("\n", emit.Diagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.ToString())));
            return MetadataReference.CreateFromImage(stream.ToArray());
        }

        private static (SymbolTypeResolver Resolver, Compilation Compilation) Consumer(
            params MetadataReference[] models)
        {
            var references = new List<MetadataReference>(Framework);
            references.AddRange(models);
            var compilation = CSharpCompilation.Create("Memo.Consumer", Array.Empty<SyntaxTree>(), references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            return (new SymbolTypeResolver(compilation), compilation);
        }

        private static INamedTypeSymbol TypeFrom(Compilation compilation, MetadataReference reference, string name) =>
            ((IAssemblySymbol) compilation.GetAssemblyOrModuleSymbol(reference)).GetTypeByMetadataName(name);

        /// <summary>
        /// Two references each declaring <c>Dup.Thing</c>. They are different types with different members and one
        /// fully-qualified name, so a memo keyed on that name answers the second walk with the first walk's resolved
        /// type — the emitter then formats a <c>string</c> as an <c>int</c>, or spells a null-conditional against a
        /// value type. Identity is the only key that separates them.
        /// </summary>
        [Fact]
        public void TwoTypesSharingAFullyQualifiedNameGetTheirOwnResolutions()
        {
            var first = ModelAssembly("Memo.Models.First", DuplicateNameSource.Replace("{0}", "string"));
            var second = ModelAssembly("Memo.Models.Second", DuplicateNameSource.Replace("{0}", "int"));
            var (resolver, compilation) = Consumer(first, second);

            var thingA = TypeFrom(compilation, first, "Dup.Thing");
            var thingB = TypeFrom(compilation, second, "Dup.Thing");
            Assert.NotNull(thingA);
            Assert.NotNull(thingB);
            // The premise: one name, two symbols.
            Assert.Equal(thingA.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                thingB.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
            Assert.False(SymbolEqualityComparer.Default.Equals(thingA, thingB));

            Assert.Equal(SpecialType.System_String, resolver.ResolvePath(thingA, new[] { "Value" }).ResultType.SpecialType);
            Assert.Equal(SpecialType.System_Int32, resolver.ResolvePath(thingB, new[] { "Value" }).ResultType.SpecialType);
        }

        /// <summary>
        /// One receiver, two paths that a dot-joined key spells identically: the two-hop <c>A</c> then <c>B</c>, and
        /// the single segment <c>A.B</c>, which is no member at all. The second walk inherited the first's success
        /// and the emitter went on to write a member nothing declares.
        /// </summary>
        [Fact]
        public void ASegmentCarryingADotIsNotTheSameQuestionAsTwoSegments()
        {
            var model = ModelAssembly("Memo.Models.Walk", NestedPathSource);
            var (resolver, compilation) = Consumer(model);
            var root = TypeFrom(compilation, model, "Walk.Root");

            var twoHops = resolver.ResolvePath(root, new[] { "A", "B" });
            Assert.Equal(SymbolTypeResolver.PathKind.Resolved, twoHops.Kind);
            Assert.Equal(SpecialType.System_Int32, twoHops.ResultType.SpecialType);

            Assert.Equal(SymbolTypeResolver.PathKind.Failed, resolver.ResolvePath(root, new[] { "A.B" }).Kind);
        }
    }
}
