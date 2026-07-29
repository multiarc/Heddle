using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Heddle.Generator.Binding;
using Heddle.Language.Binding;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Heddle.Generator.Tests
{
    /// <summary>
    /// Validates that the generator reproduces the runtime's type-resolution semantics using the same spelling
    /// corpus and assertion outcomes as <c>Heddle.Tests.TypeSpellingLockstepTests</c> in separate compilation universes.
    /// </summary>
    public class TypeSpellingSymbolLockstepTests
    {
        private const string TieSource = @"
namespace Probe { public class TieProbe { } }
namespace Probe.Alpha { public class TieProbe { } }
namespace Probe.Beta { public class TieProbe { } }
namespace Probe.Only { public class UniqueProbe { } }
namespace Probe.Nest { public class Outer { public class Inner { } } }";

        private static readonly SymbolTypeResolver Resolver = BuildResolver();

        private static SymbolTypeResolver BuildResolver()
        {
            var tpa = (string) AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES");
            var references = tpa.Split(Path.PathSeparator)
                .Where(p => !string.IsNullOrEmpty(p) && File.Exists(p))
                .Where(p => !string.Equals(Path.GetFileNameWithoutExtension(p), "Heddle.Generator",
                    StringComparison.OrdinalIgnoreCase))
                .Select(p => (MetadataReference) MetadataReference.CreateFromFile(p))
                .ToList();

            var compilation = CSharpCompilation.Create("TypeSpellingProbe",
                new[] { CSharpSyntaxTree.ParseText(TieSource) }, references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            return new SymbolTypeResolver(compilation);
        }

        private static string Resolve(string spelling, params string[] imports)
        {
            var type = Resolver.ResolveModelType(spelling, imports);
            if (type != null)
                return type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat.WithGlobalNamespaceStyle(
                    SymbolDisplayGlobalNamespaceStyle.Omitted));
            return Resolver.LastFault == TypeSpellingFault.Ambiguous ? "AMBIGUOUS" : "UNRESOLVED";
        }

        [Theory]
        [InlineData("int", "int")]
        [InlineData("string", "string")]
        [InlineData("dynamic", "object")]
        [InlineData("System.Collections.Generic.List<int>", "System.Collections.Generic.List<int>")]
        [InlineData("int[]", "int[]")]
        [InlineData("System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<int>>",
            "System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<int>>")]
        [InlineData("(int, string)", "(int, string)")]
        [InlineData("(int)", "System.ValueTuple<int>")]
        [InlineData("()", "UNRESOLVED")]
        [InlineData(" int ", "int")]
        [InlineData("Probe.Nest.Outer.Inner", "Probe.Nest.Outer.Inner")]
        [InlineData("Probe.Only.UniqueProbe", "Probe.Only.UniqueProbe")]
        [InlineData("UniqueProbe", "Probe.Only.UniqueProbe")]
        [InlineData("NoSuchTypeAnywhere", "UNRESOLVED")]
        // No tier has a nullable suffix. The grammar takes `?` as part of the name, no type answers to it, and both
        // tiers have to say so — this side lifted `int?` to `Nullable<int>` on its own and bound a strategy for a
        // template the engine will not compile on any path.
        [InlineData("int?", "UNRESOLVED")]
        [InlineData("System.Int32?", "UNRESOLVED")]
        [InlineData("System.String?", "UNRESOLVED")]
        [InlineData("Probe.Only.UniqueProbe?", "UNRESOLVED")]
        // The spelling that does mean a lifted value type, and resolves on both tiers.
        [InlineData("System.Nullable<int>", "int?")]
        public void SpellingsResolveAsTheRuntimeResolvesThem(string spelling, string expected)
        {
            Assert.Equal(expected, Resolve(spelling));
        }

        /// <summary>
        /// The last gate before HED7007 calls a <c>@model</c> spelling a typo, asked directly because the templates
        /// that reach it are the ones whose spelling resolves to no symbol — and by then the only thing standing
        /// between a wrong name and the entry point's parameter type is this answer.
        /// <para>Generous where the runtime is: an unqualified name, and a dotted tail an import would complete,
        /// both answer yes. Exact where the runtime is: every segment the author wrote has to be part of a real
        /// name, and a nullable suffix is part of no name on either tier.</para>
        /// </summary>
        [Theory]
        [InlineData("UniqueProbe", true)]
        [InlineData("Probe.Only.UniqueProbe", true)]
        [InlineData("Only.UniqueProbe", true)]
        [InlineData("Nope.Nope.UniqueProbe", false)]
        [InlineData("Probe.Nope.UniqueProbe", false)]
        [InlineData("NoSuchTypeAnywhere", false)]
        [InlineData("int", true)]
        [InlineData("int?", false)]
        [InlineData("UniqueProbe?", false)]
        [InlineData("Probe.Only.UniqueProbe?", false)]
        public void ASpellingIsOnlyKnownWhenEverySegmentOfItIs(string spelling, bool exists)
        {
            Assert.Equal(exists, Resolver.TypeNameExistsAnywhere(spelling));
        }

        [Fact]
        public void ShortNameTieSettledByExactlyOneImportResolves()
        {
            Assert.Equal("Probe.Alpha.TieProbe", Resolve("TieProbe", "Probe.Alpha"));
            Assert.Equal("Probe.Beta.TieProbe", Resolve("TieProbe", "Probe.Beta"));
        }

        [Fact]
        public void ShortNameTieUnsettledByImportsIsTheAmbiguityErrorNotAPick()
        {
            Assert.Equal("AMBIGUOUS", Resolve("TieProbe", "Probe.Alpha", "Probe.Beta"));
            Assert.Equal("AMBIGUOUS", Resolve("TieProbe", "Probe", "Probe.Alpha"));
        }

        [Fact]
        public void ShortNameTieWithNoImportIsUnresolvedNotAPick()
        {
            Assert.Equal("UNRESOLVED", Resolve("TieProbe"));
        }

        [Fact]
        public void ImplicitSystemNamespacesAreGone()
        {
            // Generator no longer implicitly appends System namespaces; Uri resolves by uniqueness rule, TieProbe does not.
            Assert.Equal("System.Uri", Resolve("System.Uri"));
            Assert.Equal("UNRESOLVED", Resolve("TieProbe"));
        }

        [Fact]
        public void AmbiguousModelTypeRedsTheBuildWithHed7023()
        {
            var run = GeneratorHarness.RunWithSources(
                new[]
                {
                    ("views/ambig.heddle",
                        "@using(){{Probe.Alpha}}@\\\n@using(){{Probe.Beta}}@\\\n" +
                        "@model(){{TieProbe}}@\\\nhi\n")
                },
                new[] { TieSource });

            var hed7023 = run.GeneratorDiagnostics.FirstOrDefault(d => d.Id == "HED7023");
            Assert.NotEqual(default, hed7023);
            Assert.Equal(DiagnosticSeverity.Error, hed7023.Severity);
            Assert.Contains("TieProbe", hed7023.GetMessage());
        }
    }
}
