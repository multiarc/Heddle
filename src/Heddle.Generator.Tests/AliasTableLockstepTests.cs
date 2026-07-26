extern alias gen;
using System;
using System.Linq;
using Heddle.Generator.Binding;
using Xunit;

namespace Heddle.Generator.Tests
{
    /// <summary>
    /// Generator plan phase 6 D7 — the build tier's alias adapter stays keyed in lockstep with the shared table.
    /// <c>SymbolTypeResolver</c> must map <see cref="Microsoft.CodeAnalysis.SpecialType"/> rather than
    /// <see cref="Type"/>, so it cannot share the table's values; sharing the <b>keys</b> is what stops
    /// "what a template may write", "what the build tier binds" and "what an error message displays" from
    /// drifting three ways, as they already had.
    /// </summary>
    public class AliasTableLockstepTests
    {
        [Fact]
        public void TheSymbolAdapterKeysAreExactlyTheSharedAliases()
        {
            var shared = gen::Heddle.Helpers.CSharpTypeNames.AliasNames
                .OrderBy(a => a, StringComparer.Ordinal)
                .ToList();
            var symbolSide = SymbolTypeResolver.Keywords.Keys
                .OrderBy(a => a, StringComparer.Ordinal)
                .ToList();

            Assert.Equal(shared, symbolSide);
        }

        /// <summary>Phase 3 (Q3.5) — the third arm: the two projections must agree on <b>what each alias means</b>,
        /// not merely on the key set. <c>dynamic</c> is the row that made the difference: it used to be a named
        /// exclusion on the symbol side, so a template writing <c>:: dynamic</c> bound on the run tier (the shared
        /// table maps it to <c>typeof(object)</c>) and not on the build tier. "Match the runtime exactly" leaves no
        /// room for a build-tier-only refusal, so it now maps to <c>System.Object</c> — the same type.</summary>
        [Fact]
        public void EveryAliasMeansTheSameTypeOnBothTiers()
        {
            foreach (var alias in gen::Heddle.Helpers.CSharpTypeNames.AliasNames)
            {
                Assert.True(gen::Heddle.Helpers.CSharpTypeNames.Aliases.TryGetValue(alias, out var clrType));
                Assert.True(SymbolTypeResolver.Keywords.TryGetValue(alias, out var special));
                Assert.Equal(clrType.FullName, SpecialTypeFullName(special));
            }
        }

        [Fact]
        public void DynamicIsNoLongerAnExclusion()
        {
            var dynamicAlias = gen::Heddle.Helpers.CSharpTypeNames.DynamicAlias;
            Assert.Contains(dynamicAlias, gen::Heddle.Helpers.CSharpTypeNames.AliasNames);
            Assert.Contains(dynamicAlias, SymbolTypeResolver.Keywords.Keys);
            Assert.Equal(Microsoft.CodeAnalysis.SpecialType.System_Object,
                SymbolTypeResolver.Keywords[dynamicAlias]);
            Assert.Equal(typeof(object), gen::Heddle.Helpers.CSharpTypeNames.Aliases[dynamicAlias]);
        }

        /// <summary>Phase 6 second pass — the fourth arm, closing the boundary between this table and phase 4's
        /// <c>NumericKind</c> lattice. An alias is either numeric on <b>both</b> the reflection side
        /// (<c>NumericTable.FromClrType</c>) and the Roslyn side (<c>SymbolFacts.ToNumericKind</c>), or on neither,
        /// and the two must name the same kind. Adding <c>nint</c>/<c>nuint</c> to the alias table without adding
        /// the kind — or the reverse — is what this goes red on.</summary>
        [Fact]
        public void EveryAliasClassifiesToTheSameNumericKindOnBothTiers()
        {
            foreach (var alias in gen::Heddle.Helpers.CSharpTypeNames.AliasNames)
            {
                var clrKind = gen::Heddle.Language.Expressions.NumericTable.FromClrType(
                    gen::Heddle.Helpers.CSharpTypeNames.Aliases[alias]);
                var symbolKind = SymbolFacts.ToNumericKind(SymbolTypeResolver.Keywords[alias]);
                Assert.True(clrKind == symbolKind,
                    $"Alias '{alias}' classifies as {clrKind} from its CLR type and {symbolKind} from its " +
                    "SpecialType. The alias table and the NumericKind lattice must move together.");
            }
        }

        /// <summary>The other direction: every non-<c>None</c> <c>NumericKind</c> is reachable from some alias. A
        /// kind with no alias would be a primitive the lattice promotes over but no template can spell — exactly the
        /// half-landed <c>nint</c> the arm above cannot see.</summary>
        [Fact]
        public void EveryNumericKindIsSpellableAsAnAlias()
        {
            var reachable = gen::Heddle.Helpers.CSharpTypeNames.AliasNames
                .Select(a => gen::Heddle.Language.Expressions.NumericTable.FromClrType(
                    gen::Heddle.Helpers.CSharpTypeNames.Aliases[a]))
                .Where(k => k != gen::Heddle.Language.Expressions.NumericKind.None)
                .Distinct()
                .ToList();

            var all = Enum.GetValues(typeof(gen::Heddle.Language.Expressions.NumericKind))
                .Cast<gen::Heddle.Language.Expressions.NumericKind>()
                .Where(k => k != gen::Heddle.Language.Expressions.NumericKind.None)
                .ToList();

            Assert.Equal(all.OrderBy(k => k).ToList(), reachable.OrderBy(k => k).ToList());
        }

        private static string SpecialTypeFullName(Microsoft.CodeAnalysis.SpecialType special)
        {
            // SpecialType member names are the metadata names with '_' for '.' — System_Int32 → System.Int32.
            return special.ToString().Replace('_', '.');
        }
    }
}
