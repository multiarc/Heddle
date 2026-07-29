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
