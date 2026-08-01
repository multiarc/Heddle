using System;
using System.Collections.Generic;
using Heddle.Helpers;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>Two types with the same short name in two different namespaces. Both namespaces can be imported at
    /// once, which is what used to make the runtime pick the first in assembly-scan order.</summary>
    public class TieProbe { }
}

namespace Heddle.Tests.TieAlpha
{
    public class TieProbe { }
}

namespace Heddle.Tests.TieBeta
{
    public class TieProbe { }
}

namespace Heddle.Tests.AliasNestAlpha
{
    /// <summary>Two hosts of one nested name, so a spelling of the nested name alone answers to two types and the
    /// short-name index therefore answers to neither. Without the pair, a uniquely-named nested type resolves off
    /// the index with no import at all and an arm that reads the directives could never be reached.</summary>
    public class AliasHost
    {
        public class AliasNested { }
    }
}

namespace Heddle.Tests.AliasNestBeta
{
    public class AliasHost
    {
        public class AliasNested { }
    }
}

namespace Heddle.Tests
{
    /// <summary>
    /// Runtime half of the shared type-name corpus, resolved symbolically in <c>Heddle.Generator.Tests</c>.
    /// Regression test: short-name ties now error (ambiguous) instead of silent order-dependent picks, matching dotted-form behavior.
    /// </summary>
    public class TypeSpellingLockstepTests
    {
        /// <summary>Shared corpus: spelling, imports, expected outcome (used by generator-side driver too).</summary>
        public static IEnumerable<object[]> Corpus()
        {
            // (spelling, imports, expectation) — expectation is a type full name, "AMBIGUOUS", or "UNRESOLVED".
            yield return new object[] { "int", new string[0], "System.Int32" };
            yield return new object[] { "string", new string[0], "System.String" };
            yield return new object[] { "dynamic", new string[0], "System.Object" };
            yield return new object[] { "System.String", new string[0], "System.String" };
            yield return new object[] { "System.Collections.Generic.List<int>", new string[0],
                "System.Collections.Generic.List`1[System.Int32]" };
            yield return new object[] { "List<int>", new[] { "System.Collections.Generic" },
                "System.Collections.Generic.List`1[System.Int32]" };
            yield return new object[] { "int[]", new string[0], "System.Int32[]" };
            yield return new object[] { "System.String[]", new string[0], "System.String[]" };
            yield return new object[]
            {
                "System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<int>>",
                new string[0],
                "System.Collections.Generic.Dictionary`2[System.String,System.Collections.Generic.List`1[System.Int32]]"
            };
            yield return new object[] { "(int, string)", new string[0],
                "System.ValueTuple`2[System.Int32,System.String]" };
            yield return new object[] { "(int)", new string[0], "System.ValueTuple`1[System.Int32]" };
            yield return new object[] { "()", new string[0], "UNRESOLVED" };
            yield return new object[] { " int ", new string[0], "System.Int32" };
            yield return new object[] { "TieProbe", new[] { "Heddle.Tests.TieAlpha" },
                "Heddle.Tests.TieAlpha.TieProbe" };
            yield return new object[] { "TieProbe", new[] { "Heddle.Tests.TieBeta" },
                "Heddle.Tests.TieBeta.TieProbe" };
            yield return new object[] { "TieProbe", new[] { "Heddle.Tests.TieAlpha", "Heddle.Tests.TieBeta" },
                "AMBIGUOUS" };
            yield return new object[] { "TieProbe", new string[0], "UNRESOLVED" };
            yield return new object[] { "List<int>", new string[0],
                "System.Collections.Generic.List`1[System.Int32]" };
            yield return new object[] { "TieProbe", new[] { "Heddle.Tests", "Heddle.Tests.TieAlpha" }, "AMBIGUOUS" };

            yield return new object[] { "NoSuchTypeAnywhere", new string[0], "UNRESOLVED" };

            // No tier has a nullable suffix. The grammar takes `?` as part of the name, no type answers to it, and
            // both tiers have to say so — the build side lifted `int?` to `Nullable<int>` on its own and bound a
            // strategy for a template the engine will not compile on any path.
            yield return new object[] { "int?", new string[0], "UNRESOLVED" };
            yield return new object[] { "System.Int32?", new string[0], "UNRESOLVED" };
            yield return new object[] { "System.String?", new string[0], "UNRESOLVED" };
            yield return new object[] { "TieProbe?", new[] { "Heddle.Tests.TieAlpha" }, "UNRESOLVED" };
            // The spelling that does mean a lifted value type, and resolves on both tiers.
            yield return new object[] { "System.Nullable<int>", new string[0],
                "System.Nullable`1[System.Int32]" };

            // A @using body that binds a name instead of opening a namespace. Every row below throws today, so
            // nothing here can change what an existing spelling resolves to; what each asserts is the C# meaning of
            // the directive beside it.

            // A namespace alias qualifies a type through it.
            yield return new object[] { "X.TieProbe", new[] { "X = Heddle.Tests.TieAlpha" },
                "Heddle.Tests.TieAlpha.TieProbe" };
            // ...and names no type on its own, because a namespace is not one.
            yield return new object[] { "X", new[] { "X = Heddle.Tests.TieAlpha" }, "UNRESOLVED" };
            // A type alias is the type.
            yield return new object[] { "X", new[] { "X = Heddle.Tests.TieAlpha.TieProbe" },
                "Heddle.Tests.TieAlpha.TieProbe" };
            // And reaches the target's nested types, which C# allows through a type alias as well.
            yield return new object[] { "X.AliasNested", new[] { "X = Heddle.Tests.AliasNestAlpha.AliasHost" },
                "Heddle.Tests.AliasNestAlpha.AliasHost+AliasNested" };
            // An alias to a predefined type — the one target spelling the assembly index does not carry.
            yield return new object[] { "X", new[] { "X = int" }, "System.Int32" };
            // A generic spelling qualified by an alias: the arity rewrite happens before the name is resolved, so
            // the alias arm sees `X.List`1` and the substitution is the same one.
            yield return new object[] { "X.List<int>", new[] { "X = System.Collections.Generic" },
                "System.Collections.Generic.List`1[System.Int32]" };
            // A target that qualifies itself.
            yield return new object[] { "X.TieProbe", new[] { "X = global::Heddle.Tests.TieAlpha" },
                "Heddle.Tests.TieAlpha.TieProbe" };
            // One name, two targets: C# refuses the duplicate, and neither is picked here.
            yield return new object[]
            {
                "X", new[] { "X = Heddle.Tests.TieAlpha.TieProbe", "X = Heddle.Tests.TieBeta.TieProbe" }, "UNRESOLVED"
            };
            // The control for every alias row: the same spelling with no alias declared binds nothing.
            yield return new object[] { "X.TieProbe", new string[0], "UNRESOLVED" };
            yield return new object[] { "X.TieProbe", new[] { "Heddle.Tests.TieAlpha" }, "UNRESOLVED" };

            // `using static` contributes the target's nested types under their own names.
            yield return new object[] { "AliasNested", new[] { "static Heddle.Tests.AliasNestAlpha.AliasHost" },
                "Heddle.Tests.AliasNestAlpha.AliasHost+AliasNested" };
            // Two targets contributing one name is the ambiguity C# reports as CS0104, not a pick.
            yield return new object[]
            {
                "AliasNested",
                new[] { "static Heddle.Tests.AliasNestAlpha.AliasHost", "static Heddle.Tests.AliasNestBeta.AliasHost" },
                "AMBIGUOUS"
            };
            // The control: without the directive the nested name answers to two types and so to neither.
            yield return new object[] { "AliasNested", new string[0], "UNRESOLVED" };
            // A namespace import of the host's namespace does not reach into the host, which is what makes the row
            // above a rule about `static` rather than about the namespace being visible.
            yield return new object[] { "AliasNested", new[] { "Heddle.Tests.AliasNestAlpha" }, "UNRESOLVED" };

            // `global::` names the global namespace, consulting no import and no alias.
            yield return new object[] { "global::Heddle.Tests.TieAlpha.TieProbe", new string[0],
                "Heddle.Tests.TieAlpha.TieProbe" };
            yield return new object[] { "global::GlobalNamespaceProbe", new string[0], "GlobalNamespaceProbe" };
            yield return new object[] { "global::System.String[]", new string[0], "System.String[]" };
            yield return new object[] { "global::System.Collections.Generic.List<int>", new string[0],
                "System.Collections.Generic.List`1[System.Int32]" };
            // The bypass, asserted in both directions: an import that settles the bare spelling settles nothing
            // here, and an alias whose name is the head of the spelling is not consulted either.
            yield return new object[] { "global::TieProbe", new[] { "Heddle.Tests.TieAlpha" }, "UNRESOLVED" };
            yield return new object[] { "global::X.TieProbe", new[] { "X = Heddle.Tests.TieAlpha" }, "UNRESOLVED" };
            yield return new object[] { "global::Heddle.Tests.TieProbe", new string[0], "Heddle.Tests.TieProbe" };

            // Where a spelling answers to BOTH the assembly index and an alias, the index keeps it. C# decides this
            // the other way — an alias wins over a type reached through an imported namespace — and matching C# here
            // would move a spelling that resolves today onto a different type, which is the one thing this rule may
            // not do. Pinned so the deviation is visible rather than incidental.
            yield return new object[]
            {
                "TieProbe", new[] { "Heddle.Tests.TieAlpha", "TieProbe = Heddle.Tests.TieBeta.TieProbe" },
                "Heddle.Tests.TieAlpha.TieProbe"
            };
        }

        [Theory]
        [MemberData(nameof(Corpus))]
        public void ReflectionHelperMatchesTheCorpus(string spelling, string[] imports, string expectation)
        {
            string actual;
            try
            {
                var type = ReflectionHelper.ResolveType(spelling, imports);
                actual = type == null ? "UNRESOLVED" : type.ToString();
            }
            catch (InvalidOperationException e)
            {
                actual = e.Message.Contains("ambigous") ? "AMBIGUOUS" : "UNRESOLVED";
            }

            Assert.Equal(expectation, actual);
        }

        [Fact]
        public void ShortNameTieUnsettledByImportsIsTheAmbiguityErrorNotAPick()
        {
            var ex = Assert.Throws<InvalidOperationException>(() =>
                ReflectionHelper.ResolveType("TieProbe", "Heddle.Tests.TieAlpha", "Heddle.Tests.TieBeta"));
            Assert.Contains("ambigous", ex.Message);
        }

        [Fact]
        public void ShortNameTieSettledByExactlyOneImportStillResolves()
        {
            Assert.Equal(typeof(TieAlpha.TieProbe),
                ReflectionHelper.ResolveType("TieProbe", "Heddle.Tests.TieAlpha"));
        }
    }
}
