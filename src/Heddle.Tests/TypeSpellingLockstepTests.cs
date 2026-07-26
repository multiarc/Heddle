using System;
using System.Collections.Generic;
using Heddle.Helpers;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>Two types with the same short name in two different namespaces — the short-name tie Q3.5's ruling
    /// is about. Both namespaces can be imported at once, which is what used to make the runtime pick the first in
    /// assembly-scan order.</summary>
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
    /// Phase 3 (F8 / Q3.5) — the runtime half of the shared type-name corpus. Its twin in
    /// <c>Heddle.Generator.Tests</c> resolves the same spellings symbolically and asserts the same outcomes.
    /// <para>The <b>ruling</b> pinned here: the short-name arm's order-dependent silent pick was judged defective
    /// (an assembly-load-order-sensitive result the build tier cannot match by construction, and a contradiction of
    /// this file's own <c>RegisterType</c> comment), and both tiers were fixed in lockstep per OQ5's escape clause.
    /// A tie the imports settle to exactly one namespace still resolves; a tie two imports both claim is now the
    /// same "ambigous" error the dotted arms have always raised.</para>
    /// </summary>
    public class TypeSpellingLockstepTests
    {
        /// <summary>The shared name corpus: spelling, imports, and the expected outcome. The generator-side driver
        /// reads the same rows.</summary>
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
            // Q8.3: a one-element tuple IS legal — `(int)` is ValueTuple<int>. The shared parser required two
            // elements when it was written, so the build tier refused a spelling the run tier accepted; folding the
            // runtime onto the parser is what surfaced it. An empty element is still rejected.
            yield return new object[] { "(int)", new string[0], "System.ValueTuple`1[System.Int32]" };
            yield return new object[] { "()", new string[0], "UNRESOLVED" };
            // Surrounding whitespace on a top-level spelling is tolerated by the shared parser (it trims every
            // recursion), where the runtime's own dispatch used to throw. A widening, and the two tiers now agree.
            yield return new object[] { " int ", new string[0], "System.Int32" };

            // The short-name tie. One import settles it; two do not.
            yield return new object[] { "TieProbe", new[] { "Heddle.Tests.TieAlpha" },
                "Heddle.Tests.TieAlpha.TieProbe" };
            yield return new object[] { "TieProbe", new[] { "Heddle.Tests.TieBeta" },
                "Heddle.Tests.TieBeta.TieProbe" };
            yield return new object[] { "TieProbe", new[] { "Heddle.Tests.TieAlpha", "Heddle.Tests.TieBeta" },
                "AMBIGUOUS" };
            yield return new object[] { "TieProbe", new string[0], "UNRESOLVED" };

            // The implicit-namespace row, corrected against the runtime while implementing: `List` binds with NO
            // imports on both tiers — not through an implicit `System.Collections.Generic`, but because the short
            // name `List`1` is globally unique in the type universe, and a unique short name resolves outright.
            // That is precisely the rule the generator used to lack: it tried the @using list and then two
            // hard-coded namespaces. Sharing the rule is what makes this row agree; the hard-coded namespaces are
            // gone, and the row below pins that a name needing them and nothing else binds on neither tier.
            yield return new object[] { "List<int>", new string[0],
                "System.Collections.Generic.List`1[System.Int32]" };
            // Ambiguous under BOTH tiers' rule, and previously resolvable on the build tier alone through the
            // implicit `System` namespace: two imported namespaces claim the short name.
            yield return new object[] { "TieProbe", new[] { "Heddle.Tests", "Heddle.Tests.TieAlpha" }, "AMBIGUOUS" };

            yield return new object[] { "NoSuchTypeAnywhere", new string[0], "UNRESOLVED" };
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
            // The behaviour change itself, stated once: this used to return TieAlpha.TieProbe or
            // TieBeta.TieProbe depending on the order Assembly.GetTypes() happened to yield them.
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
