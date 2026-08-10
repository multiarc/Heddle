extern alias gen;
using System;
using System.Linq;
using Heddle.Generator.Binding;
using Xunit;

namespace Heddle.Generator.Tests
{
    /// <summary>
    /// The build tier's alias adapter stays keyed in lockstep with the shared table.
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

        /// <summary>The two projections must agree on what each alias means.
        /// <c>dynamic</c> now maps to <c>System.Object</c> on both tiers.</summary>
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

        /// <summary>An alias must classify to the same numeric kind on both the reflection and Roslyn sides.
        /// Adding <c>nint</c>/<c>nuint</c> to the alias table without updating <c>NumericKind</c> will fail this test.</summary>
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

        /// <summary>Every non-<c>None</c> numeric kind must be reachable as an alias.
        /// A kind with no alias is a primitive templates cannot spell.</summary>
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
            return special.ToString().Replace('_', '.');
        }
    }
}
