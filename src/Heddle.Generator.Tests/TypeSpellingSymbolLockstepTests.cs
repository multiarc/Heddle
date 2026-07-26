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
    /// The <b>symbol-side</b> driver of the type-name corpus, resolving the same spellings under the same imports
    /// and asserting the same outcomes as <c>Heddle.Tests.TypeSpellingLockstepTests</c>. This validates that the
    /// generator reproduces the runtime's type-resolution semantics and verdicts.
    /// <para>The tie probes are declared in this compilation rather than referenced, so the two drivers exercise
    /// their own universes — identical <i>rules</i> must produce identical <i>verdicts</i>, even when the assembly
    /// universes differ.</para>
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
        // Keyword aliases, including the one that used to be a build-tier-only refusal.
        [InlineData("int", "int")]
        [InlineData("string", "string")]
        [InlineData("dynamic", "object")]
        // Generics, arrays, tuples, dotted-nested — none of which the build tier could resolve at all before.
        [InlineData("System.Collections.Generic.List<int>", "System.Collections.Generic.List<int>")]
        [InlineData("int[]", "int[]")]
        [InlineData("System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<int>>",
            "System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<int>>")]
        [InlineData("(int, string)", "(int, string)")]
        // One-element tuples are now resolved on both tiers (previously refused only on the shared parser).
        [InlineData("(int)", "System.ValueTuple<int>")]
        [InlineData("()", "UNRESOLVED")]
        [InlineData(" int ", "int")]
        [InlineData("Probe.Nest.Outer.Inner", "Probe.Nest.Outer.Inner")]
        [InlineData("Probe.Only.UniqueProbe", "Probe.Only.UniqueProbe")]
        // A globally unique short name binds with no import — the runtime's rule, which the build tier lacked.
        [InlineData("UniqueProbe", "Probe.Only.UniqueProbe")]
        [InlineData("NoSuchTypeAnywhere", "UNRESOLVED")]
        public void SpellingsResolveAsTheRuntimeResolvesThem(string spelling, string expected)
        {
            Assert.Equal(expected, Resolve(spelling));
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
            // The build tier now matches the runtime's ambiguity handling: the same input that throws "ambiguous".
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
            // The generator used to append `System` and `System.Collections.Generic` to every lookup. `Uri` is a
            // System type with a unique short name, so it still resolves — by the runtime's uniqueness rule, not by
            // a hard-coded namespace. The distinction is visible on a name the uniqueness rule cannot settle:
            // `TieProbe` has three claimants and no import, and nothing implicit rescues it (asserted above).
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
