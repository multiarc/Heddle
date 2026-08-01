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
namespace Probe.Nest { public class Outer { public class Inner { } } }
// Two hosts of one nested name, so the nested name alone answers to two types and the short-name index therefore
// answers to neither — without the pair, a uniquely-named nested type resolves off the index with no directive at
// all and an arm that reads the directives could never be reached.
namespace Probe.NestAlpha { public class AliasHost { public class AliasNested { } } }
namespace Probe.NestBeta { public class AliasHost { public class AliasNested { } } }
public class GlobalProbe { public class Inner { } }";

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
        /// The <c>@using</c> bodies that bind a name instead of opening a namespace, and the <c>global::</c>
        /// qualifier — resolved here exactly as the runtime resolves them, over this compilation's universe.
        /// <para>Every spelling below is one both tiers refused before the arms existed, so no row can be evidence
        /// that a resolution moved; what each pins is the C# meaning of the directive beside it, and that this side
        /// reaches the same answer the reflection side does.</para>
        /// </summary>
        [Theory]
        // A namespace alias qualifies a type through it, and names no type on its own.
        [InlineData("X.TieProbe", "Probe.Alpha.TieProbe", new[] { "X = Probe.Alpha" })]
        [InlineData("X", "UNRESOLVED", new[] { "X = Probe.Alpha" })]
        // A type alias is the type, and reaches the target's nested types.
        [InlineData("X", "Probe.Alpha.TieProbe", new[] { "X = Probe.Alpha.TieProbe" })]
        [InlineData("X.AliasNested", "Probe.NestAlpha.AliasHost.AliasNested",
            new[] { "X = Probe.NestAlpha.AliasHost" })]
        // An alias to a predefined type — the one target spelling the index does not carry.
        [InlineData("X", "int", new[] { "X = int" })]
        // The arity rewrite happens before the name is resolved, so the alias arm sees `X.List`1`.
        [InlineData("X.List<int>", "System.Collections.Generic.List<int>",
            new[] { "X = System.Collections.Generic" })]
        [InlineData("X.TieProbe", "Probe.Alpha.TieProbe", new[] { "X = global::Probe.Alpha" })]
        // One name, two targets: C# refuses the duplicate, and neither is picked here.
        [InlineData("X", "UNRESOLVED", new[] { "X = Probe.Alpha.TieProbe", "X = Probe.Beta.TieProbe" })]
        // The control for every alias row: the same spelling with no alias declared binds nothing.
        [InlineData("X.TieProbe", "UNRESOLVED", new string[0])]
        [InlineData("X.TieProbe", "UNRESOLVED", new[] { "Probe.Alpha" })]
        // `using static` contributes the target's nested types under their own names; two targets contributing one
        // name is the ambiguity C# reports as CS0104.
        [InlineData("AliasNested", "Probe.NestAlpha.AliasHost.AliasNested",
            new[] { "static Probe.NestAlpha.AliasHost" })]
        [InlineData("AliasNested", "AMBIGUOUS",
            new[] { "static Probe.NestAlpha.AliasHost", "static Probe.NestBeta.AliasHost" })]
        [InlineData("AliasNested", "UNRESOLVED", new string[0])]
        // Importing the host's namespace does not reach into the host, which is what makes the row above a rule
        // about `static` rather than about the namespace being visible.
        [InlineData("AliasNested", "UNRESOLVED", new[] { "Probe.NestAlpha" })]
        // `global::` names the global namespace, consulting no import and no alias.
        [InlineData("global::Probe.Alpha.TieProbe", "Probe.Alpha.TieProbe", new string[0])]
        [InlineData("global::GlobalProbe", "GlobalProbe", new string[0])]
        [InlineData("global::GlobalProbe.Inner", "GlobalProbe.Inner", new string[0])]
        [InlineData("global::System.String[]", "string[]", new string[0])]
        [InlineData("global::System.Collections.Generic.List<int>", "System.Collections.Generic.List<int>",
            new string[0])]
        [InlineData("global::TieProbe", "UNRESOLVED", new[] { "Probe.Alpha" })]
        [InlineData("global::X.TieProbe", "UNRESOLVED", new[] { "X = Probe.Alpha" })]
        // Where a spelling answers to BOTH the index and an alias, the index keeps it. C# decides this the other way
        // — an alias wins over a type reached through an imported namespace — and matching C# here would move a
        // spelling that resolves today onto a different type. Pinned so the deviation is visible.
        [InlineData("TieProbe", "Probe.Alpha.TieProbe", new[] { "Probe.Alpha", "TieProbe = Probe.Beta.TieProbe" })]
        public void ADirectiveThatBindsANameResolvesAsTheRuntimeResolvesIt(string spelling, string expected,
            string[] usings)
        {
            Assert.Equal(expected, Resolve(spelling, usings));
        }

        /// <summary>
        /// The last gate before HED7007 calls a <c>@model</c> spelling a typo, asked directly because the templates
        /// that reach it are the ones whose spelling resolves to no symbol — and by then the only thing standing
        /// between a wrong name and the entry point's parameter type is this answer.
        /// <para>Generous where the runtime is: an unqualified name, and a dotted tail an import would complete,
        /// both answer yes. Exact where the runtime is: every segment the author wrote has to be part of a real
        /// name, and a nullable suffix is part of no name on either tier.</para>
        /// <para>The match is a suffix of the qualified name <b>on a dot</b>, never inside an identifier: the rows
        /// that end mid-name are what separate the two, and every row with a wrong namespace segment answers no
        /// without ever consulting the boundary.</para>
        /// </summary>
        [Theory]
        [InlineData("UniqueProbe", true)]
        [InlineData("Probe.Only.UniqueProbe", true)]
        [InlineData("Only.UniqueProbe", true)]
        [InlineData("Nope.Nope.UniqueProbe", false)]
        [InlineData("Probe.Nope.UniqueProbe", false)]
        [InlineData("NoSuchTypeAnywhere", false)]
        // The tail of an identifier, not of a name: 'UniqueProbe' ends in it and nothing is called it.
        [InlineData("eProbe", false)]
        // A namespace segment, and the tail of both 'TieProbe' and 'UniqueProbe'. A plain suffix test says yes.
        [InlineData("Probe", false)]
        // Every segment is a real one and only the separator between them is not a dot.
        [InlineData("OnlyXUniqueProbe", false)]
        [InlineData("int", true)]
        [InlineData("int?", false)]
        [InlineData("UniqueProbe?", false)]
        [InlineData("Probe.Only.UniqueProbe?", false)]
        // A leading dot: every segment after it is real and the name is fully qualified, so the walk consumes the
        // whole spelling and runs out of namespaces with the dot still unmatched. Answering yes here would let a
        // spelling no tier resolves past the last gate before HED7007.
        [InlineData(".Probe.Only.UniqueProbe", false)]
        [InlineData(".UniqueProbe", false)]
        [InlineData(".int", false)]
        // A dot on both ends, and one segment too many in front of a fully-qualified name.
        [InlineData("..UniqueProbe", false)]
        [InlineData("Extra.Probe.Only.UniqueProbe", false)]
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
