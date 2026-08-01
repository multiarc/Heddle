using System;
using Heddle.Data;
using Heddle.Generator.IntegrationTests.Fixtures;
using Heddle.Runtime;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// Where a name-binding <c>@using</c> directive collides with something the resolver already knows, the two
    /// tiers have to break the tie the same way, and the way C# breaks it. These are the collisions the alias suite
    /// does not reach, because every template there binds a name nothing else claims.
    /// </summary>
    public class AliasShadowingOrderTests
    {
        private const string Fixtures = "Heddle.Generator.IntegrationTests.Fixtures";

        private static bool EngineCompiles(string template, Type modelType)
        {
            try
            {
                return new HeddleTemplate(template, new CompileContext(new TemplateOptions(), modelType))
                    .CompileResult.Success;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        /// <summary>
        /// An alias whose name is a C# keyword. The two resolvers consult their keyword tables at opposite ends of
        /// the walk — the engine's alias arm claims the head before the table is reached, the build tier reads its
        /// table before the index that carries aliases — so the orders disagree on paper.
        /// <para>They cannot disagree in practice, and this is the row that says so. Reaching the difference needs
        /// an alias whose name is a keyword, and C# only permits that for <c>dynamic</c>: every other name in the
        /// table is a reserved word, so <c>using int = X;</c> is not a directive any template can carry. One name
        /// is the whole population, and both tiers bind it to the aliased type.</para>
        /// </summary>
        [Fact]
        public void AnAliasNamedAfterAKeywordBindsTheAliasedTypeOnBothTiers()
        {
            const string key = "views/alias-shadows-keyword.heddle";
            var template = "@using(){{dynamic = " + Fixtures + ".Article}}@\\\n@model(){{dynamic}}@\\\n[@(Title)]\n";

            var (precompiled, dyn) = DifferentialHarness.Render(key, template, typeof(Article),
                new Article { Title = "T" });
            Assert.Equal("[T]\n", dyn);
            Assert.Equal(dyn, precompiled);

            // Equal bytes alone would also be satisfied by the build tier declining the template and falling back
            // to the engine, which is a different outcome wearing the same output. The binding is the claim.
            DifferentialHarness.ExpectPrecompiled(DifferentialHarness.Generate(new[] { (key, template) }), key);
        }

        /// <summary>
        /// The near-neighbour that keeps the row above from being satisfied by a blanket refusal: a keyword with no
        /// alias shadowing it still names its primitive on both tiers.
        /// </summary>
        [Fact]
        public void AKeywordWithNoAliasShadowingItStillNamesItsPrimitiveOnBothTiers()
        {
            const string key = "views/keyword-unshadowed.heddle";
            const string template = "@model(){{string}}@\\\n[@()]\n";

            var (precompiled, dyn) = DifferentialHarness.Render(key, template, typeof(string), "T");
            Assert.Equal("[T]\n", dyn);
            Assert.Equal(dyn, precompiled);
        }

        /// <summary>
        /// A type nested inside a generic outer carries its outer's type parameters but declares none of its own.
        /// The engine counts <c>GetGenericArguments().Length</c>, which includes the outer's; the build tier counts
        /// <see cref="INamedTypeSymbol.Arity"/>, which does not. So a spelling naming the nested type with the
        /// outer's argument satisfies one arity check and fails the other, and no diagnostic is raised either way.
        /// </summary>
        [Fact(Skip = "known defect — generator: a type nested in a generic outer is counted with Arity, which " +
                     "excludes the outer's parameters, where the engine counts GetGenericArguments().Length, " +
                     "which includes them, so the build tier degrades on a spelling the engine binds; " +
                     "un-skip with that fix")]
        public void ATypeNestedInAGenericOuterIsCountedTheSameWayOnBothTiers()
        {
            const string key = "views/nested-in-generic-arity.heddle";
            var template = "@model(){{" + Fixtures + ".ArityOuter<int>.ArityInner}}@\\\n[@(Amount)]\n";

            Assert.True(EngineCompiles(template, typeof(object)));
            DifferentialHarness.ExpectPrecompiled(DifferentialHarness.Generate(new[] { (key, template) }), key);
        }

        /// <summary>
        /// Two <c>@using</c> directives that both make a dotted spelling resolvable. C# refuses the spelling as
        /// ambiguous (CS0104) rather than letting the first or last directive win; whichever tier lets declaration
        /// order decide is answering a question the language says has no answer.
        /// </summary>
        [Fact(Skip = "known defect — both tiers: a dotted spelling reachable through two imports is decided by " +
                     "@using declaration order, where C# raises CS0104 and refuses it; un-skip with that fix")]
        public void ADottedSpellingReachableThroughTwoImportsIsRefusedOnBothTiers()
        {
            const string key = "views/dotted-import-ambiguous.heddle";
            var template = "@using(){{" + Fixtures + ".AliasNestAlpha}}@\\\n" +
                           "@using(){{" + Fixtures + ".AliasNestBeta}}@\\\n" +
                           "@model(){{AliasHost.AliasNested}}@\\\n[@(Tag)]\n";

            Assert.False(EngineCompiles(template, typeof(object)));
            DifferentialHarness.ExpectDegrade(DifferentialHarness.Generate(new[] { (key, template) }), key);
        }
    }
}
