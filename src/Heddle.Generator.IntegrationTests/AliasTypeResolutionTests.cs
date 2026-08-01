using System;
using System.Linq;
using Heddle.Data;
using Heddle.Generator.IntegrationTests.Fixtures;
using Heddle.Runtime;
using Microsoft.CodeAnalysis;
using Xunit;
// A using directive imports a namespace's types, never its nested namespaces, so the probe namespaces need naming.
using AliasAlpha = Heddle.Generator.IntegrationTests.Fixtures.AliasNestAlpha;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// A <c>@using</c> body is a C# using-directive header, and two of its three forms bind a name rather than open
    /// a namespace. Both tiers used to collect them into the same list of namespace strings and then look for a
    /// match in it, so a <c>@using</c> alias and a <c>using static</c> bound nothing at all: the directive was legal
    /// and inert, and every spelling that needed it was refused.
    /// <para>Every template here is one both tiers refused before, so no row can be evidence that a resolution
    /// moved. What each pins is that the two tiers now reach the <b>same</b> answer, and that the answer is the one
    /// the C# compiler gives for the same directive.</para>
    /// </summary>
    public class AliasTypeResolutionTests
    {
        private const string Fixtures = "Heddle.Generator.IntegrationTests.Fixtures";
        private const string AliasHostAlpha = Fixtures + ".AliasNestAlpha.AliasHost";
        private const string AliasHostBeta = Fixtures + ".AliasNestBeta.AliasHost";

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
        /// The four spellings a name-binding directive makes reachable, rendered through both tiers.
        /// <para><c>using static</c> is asked of a <b>nested type</b>, which is the only thing it contributes to
        /// type resolution — a static member is a different subsystem and no directive here reaches it. The nested
        /// name is declared twice on purpose: a nested type with a unique short name resolves off the assembly index
        /// with no directive at all, so a probe without the collision would pass whether the arm existed or
        /// not.</para>
        /// </summary>
        [Theory]
        [InlineData("namespace-alias", "A = " + Fixtures, "A.Article")]
        [InlineData("type-alias", "A = " + Fixtures + ".Article", "A")]
        [InlineData("global-qualified", Fixtures, "global::" + Fixtures + ".Article")]
        public void ADirectiveThatBindsANameMakesItsSpellingRenderOnBothTiers(string name, string body,
            string spelling)
        {
            var key = "views/alias-model-" + name + ".heddle";
            var template = "@using(){{" + body + "}}@\\\n@model(){{" + spelling + "}}@\\\n[@(Title)]\n";

            var (precompiled, dyn) = DifferentialHarness.Render(key, template, typeof(Article),
                new Article { Title = "T" });
            Assert.Equal("[T]\n", dyn);
            Assert.Equal(dyn, precompiled);
        }

        [Theory]
        [InlineData("type-alias-nested", "A = " + AliasHostAlpha, "A.AliasNested")]
        [InlineData("static-import-nested", "static " + AliasHostAlpha, "AliasNested")]
        public void ADirectiveThatBindsANestedNameMakesItsSpellingRenderOnBothTiers(string name, string body,
            string spelling)
        {
            var key = "views/alias-model-" + name + ".heddle";
            var template = "@using(){{" + body + "}}@\\\n@model(){{" + spelling + "}}@\\\n[@(Tag)]\n";

            var (precompiled, dyn) = DifferentialHarness.Render(key, template,
                typeof(AliasAlpha.AliasHost.AliasNested),
                new AliasAlpha.AliasHost.AliasNested { Tag = "t" });
            Assert.Equal("[t]\n", dyn);
            Assert.Equal(dyn, precompiled);
        }

        /// <summary>
        /// The <c>global::</c> half that only a namespace-less type can show: the runtime keys such a type's full
        /// name under a leading dot, so <c>GlobalNamespaceModel.Inner</c> answers to nothing on either tier — which
        /// is what a sibling suite pins — while the qualified spelling of the same type binds on both.
        /// </summary>
        [Fact]
        public void TheGlobalQualifierReachesANamespacelessNestedTypeOnBothTiers()
        {
            const string key = "views/alias-model-global-nested.heddle";
            const string template = "@model(){{global::GlobalNamespaceModel.Inner}}@\\\n[@(Tag)]\n";

            var (precompiled, dyn) = DifferentialHarness.Render(key, template, typeof(GlobalNamespaceModel.Inner),
                new GlobalNamespaceModel.Inner { Tag = "g" });
            Assert.Equal("[g]\n", dyn);
            Assert.Equal(dyn, precompiled);
        }

        /// <summary>
        /// Two <c>using static</c> targets contributing one nested name is C#'s CS0104, and both tiers say so: the
        /// runtime raises its "ambigous" error and the build raises <c>HED7023</c> rather than picking one and
        /// emitting typed code off a type the runtime might not choose.
        /// </summary>
        [Fact]
        public void TwoStaticImportsContributingOneNameAreAmbiguousOnBothTiers()
        {
            const string key = "views/alias-model-static-ambiguous.heddle";
            var template = "@using(){{static " + AliasHostAlpha + "}}@\\\n" +
                           "@using(){{static " + AliasHostBeta + "}}@\\\n" +
                           "@model(){{AliasNested}}@\\\n[@(Tag)]\n";
            var gen = DifferentialHarness.Generate(new[] { (key, template) });

            var hed7023 = Assert.Single(gen.Diagnostics.Where(d => d.Id == "HED7023"));
            Assert.Equal(DiagnosticSeverity.Error, hed7023.Severity);
            Assert.Contains("AliasNested", hed7023.GetMessage());

            Assert.False(EngineCompiles(template, typeof(AliasAlpha.AliasHost.AliasNested)));
        }

        /// <summary>
        /// An alias whose target names nothing, and a spelling whose head is an alias nobody declared. Both are
        /// refused on both tiers, which is what keeps the arms above from being a rule that a dotted head is
        /// something to guess at.
        /// </summary>
        [Theory]
        [InlineData("dead-target", "A = Zork.Nope", "A.Article")]
        [InlineData("undeclared-alias", Fixtures, "A.Article")]
        public void AnAliasThatBindsNothingIsRefusedOnBothTiers(string name, string body, string spelling)
        {
            var key = "views/alias-model-" + name + ".heddle";
            var template = "@using(){{" + body + "}}@\\\n@model(){{" + spelling + "}}@\\\nstatic text\n";
            var gen = DifferentialHarness.Generate(new[] { (key, template) });

            var hed7007 = Assert.Single(gen.Diagnostics.Where(d => d.Id == "HED7007"));
            Assert.Equal(DiagnosticSeverity.Error, hed7007.Severity);
            Assert.Contains(spelling, hed7007.GetMessage());

            Assert.False(EngineCompiles(template, typeof(Article)));
        }

        /// <summary>The engine keeps the alias out of the namespace list it matches against, so a directive that
        /// binds a name cannot also act as an import of the text beside the <c>=</c>: the bare spelling the target
        /// namespace would have settled stays unresolved on both tiers.</summary>
        [Fact]
        public void AnAliasIsNotAlsoAnImportOfItsTarget()
        {
            const string key = "views/alias-model-not-an-import.heddle";
            var template = "@using(){{A = " + Fixtures + ".AliasNestAlpha}}@\\\n" +
                           "@model(){{AliasHost}}@\\\nstatic text\n";
            var gen = DifferentialHarness.Generate(new[] { (key, template) });

            Assert.Empty(gen.Diagnostics.Where(d => d.Id == "HED7023"));
            DifferentialHarness.ExpectDegrade(gen, key);
            Assert.False(EngineCompiles(template, typeof(AliasAlpha.AliasHost)));
        }
    }
}
