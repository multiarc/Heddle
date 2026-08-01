using System;
using System.Linq;
using Heddle.Data;
using Heddle.Generator.IntegrationTests.Fixtures;
using Heddle.Runtime;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// <c>@model</c> spellings the build tier accepted and the engine does not. The directive's argument is free
    /// text — unlike a <c>::</c>, prop or slot type, whose name the grammar reads as an identifier — so it is the one
    /// place a spelling reaches a resolver exactly as the author typed it, and the one place the two resolvers can
    /// disagree about what a spelling means.
    /// </summary>
    public class ModelTypeSpellingTests
    {
        private const string ArticleType = "Heddle.Generator.IntegrationTests.Fixtures.Article";

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
        /// A nullable suffix, which neither tier's grammar has. The build side had a prelude of its own that stripped
        /// the <c>?</c> and lifted the value type, so it bound a strategy and rendered <c>[True]</c> for a template
        /// the engine turns down on every path it has — the mismatch policy, a changed file, a direct
        /// <c>HeddleTemplate</c>, the language server, the tool. Both halves are asserted here: the engine refuses,
        /// and the build says so in its own words at the directive rather than binding anything.
        /// </summary>
        [Theory]
        [InlineData("keyword", "int?", "@(HasValue)\n")]
        [InlineData("clr-name", "System.Int32?", "@(HasValue)\n")]
        [InlineData("reference", "System.String?", "@(Length)\n")]
        [InlineData("fixture", ArticleType + "?", "@(Title)\n")]
        public void ANullableSuffixOnTheModelDirectiveIsRefusedOnBothTiers(string name, string spelling, string body)
        {
            var key = "views/nullable-model-" + name + ".heddle";
            var template = "@model(){{" + spelling + "}}@\\\n" + body;
            var gen = DifferentialHarness.Generate(new[] { (key, template) });

            var hed7007 = Assert.Single(gen.Diagnostics.Where(d => d.Id == "HED7007"));
            Assert.Equal(DiagnosticSeverity.Error, hed7007.Severity);
            Assert.Contains(spelling, hed7007.GetMessage());
            Assert.Contains(key, hed7007.Location.GetLineSpan().Path);

            Assert.False(EngineCompiles(template, typeof(object)));
        }

        /// <summary>The cost control: the spelling that does mean a lifted value type is in the shared grammar, means
        /// the same thing on both tiers, and still precompiles.</summary>
        [Fact]
        public void TheNullableSpellingBothGrammarsDoHaveStillPrecompiles()
        {
            const string key = "views/nullable-model-constructed.heddle";
            const string template = "@model(){{System.Nullable<System.Int32>}}@\\\n[@(HasValue)]\n";

            var (precompiled, dyn) = DifferentialHarness.Render(key, template, typeof(int?), 5);
            Assert.Equal(dyn, precompiled);
            Assert.Equal("[True]\n", dyn);
        }

        /// <summary>
        /// A dotted spelling whose namespace segments are wrong. The gate before HED7007 asked only whether some type
        /// somewhere carried the last segment, so an unrelated <c>Article</c> answered for
        /// <c>Nope.Nope.Article</c>: no diagnostic, and the spelling went on verbatim into the entry point's
        /// parameter type — four CS0246 against a <c>.g.cs</c>, attributed to a <c>.heddle</c> file, with nothing of
        /// Heddle's own to say which name was wrong.
        /// </summary>
        [Theory]
        [InlineData("wrong-namespace", "Nope.Nope.Article")]
        [InlineData("one-segment-wrong", "Heddle.Generator.IntegrationTests.Nope.Article")]
        public void ADottedModelSpellingThatBindsNowhereIsReported(string name, string spelling)
        {
            var key = "views/dotted-model-" + name + ".heddle";
            var template = "@model(){{" + spelling + "}}@\\\n@(Title)\n";
            var gen = DifferentialHarness.Generate(new[] { (key, template) });

            var hed7007 = Assert.Single(gen.Diagnostics.Where(d => d.Id == "HED7007"));
            Assert.Equal(DiagnosticSeverity.Error, hed7007.Severity);
            Assert.Contains(spelling, hed7007.GetMessage());
            Assert.Contains(key, hed7007.Location.GetLineSpan().Path);
        }

        /// <summary>The cost control: the fully-qualified spelling binds, precompiles and raises nothing.</summary>
        [Fact]
        public void ADottedModelSpellingThatBindsIsLeftAlone()
        {
            const string key = "views/dotted-model-ok-full.heddle";
            const string template = "@model(){{" + ArticleType + "}}@\\\n[@(Title)]\n";
            var gen = DifferentialHarness.Generate(new[] { (key, template) });

            Assert.Empty(gen.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

            var (precompiled, dyn) = DifferentialHarness.Render(key, template, typeof(Article),
                new Article { Title = "T" });
            Assert.Equal(dyn, precompiled);
            Assert.Equal("[T]\n", dyn);
        }

        /// <summary>
        /// A spelling that resolves to no symbol and is not called a typo either — the tail of a real namespace is
        /// the shape an import completes, and the runtime binds what is <em>loaded</em> rather than what this
        /// compilation references, so reporting it would turn a template that renders into a build error. What the
        /// build must still not do is <b>write</b> it: the spelling went verbatim into the entry point's parameter
        /// type, and the consumer's compiler answered CS0246 (no such namespace) or CS0305 (a generic name with no
        /// arity) against a <c>.g.cs</c> they cannot edit.
        /// <para>The body is static text on purpose. Asked with <c>[@(Title)]</c> in it, every one of these passed
        /// off a member-path degrade that had nothing to do with the spelling, and the generated file that could not
        /// compile was never built at all.</para>
        /// <para>Both halves are asserted per row: nothing is reported — <see cref="DifferentialHarness.Generate"/>
        /// compiles the generated sources and throws if they do not build — and the template goes to the tier that
        /// can still bind the name. The fully-qualified neighbour below is what keeps this from being a blanket
        /// refusal of dotted spellings.</para>
        /// </summary>
        [Theory]
        [InlineData("namespace-tail", "IntegrationTests.Fixtures.Article")]
        [InlineData("shorter-tail", "Fixtures.Article")]
        // A real generic type named without its arity: the name exists, and no C# spelling of it does.
        [InlineData("no-arity", "System.Collections.Generic.List")]
        public void AModelSpellingThatResolvesToNoSymbolIsNeverWrittenIntoTheEntryPoint(string name, string spelling)
        {
            var key = "views/unwritable-model-" + name + ".heddle";
            var template = "@model(){{" + spelling + "}}@\\\nstatic text\n";
            var gen = DifferentialHarness.Generate(new[] { (key, template) });

            Assert.Empty(gen.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
            Assert.DoesNotContain(gen.Diagnostics, d => d.Id == "HED7007");
            DifferentialHarness.ExpectDegrade(gen, key);

            Assert.False(EngineCompiles(template, typeof(object)));
        }

        /// <summary>The cost control for the rule above: the same body under a spelling that does resolve still
        /// precompiles, so refusing to write an unresolved name is not a refusal to write a dotted one.</summary>
        [Fact]
        public void AModelSpellingThatResolvesStillPrecompilesWithTheSameBody()
        {
            const string key = "views/unwritable-model-neighbour.heddle";
            const string template = "@model(){{" + ArticleType + "}}@\\\nstatic text\n";
            var gen = DifferentialHarness.Generate(new[] { (key, template) });

            Assert.Empty(gen.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
            DifferentialHarness.ExpectPrecompiled(gen, key);
        }

        /// <summary>
        /// A model type with <b>no namespace</b>. The runtime keys its full-name index on
        /// <c>type.Namespace + "." + shortName</c> and <c>Type.Namespace</c> is null for such a type, so its full
        /// name carries a leading dot — <c>.GlobalNamespaceModel</c> is a spelling the engine resolves and
        /// <c>GlobalNamespaceModel.Inner</c> is one it does not, because the nested alias is keyed under the leading
        /// dot too. Dropping that dot on the build side diverged in both directions at once.
        /// </summary>
        [Theory]
        [InlineData("global-leading-dot", ".GlobalNamespaceModel", "[@(Name)]\n")]
        [InlineData("global-bare", "GlobalNamespaceModel", "[@(Name)]\n")]
        public void ANamespacelessModelSpellingResolvesTheWayTheEngineResolvesIt(string name, string spelling,
            string body)
        {
            var key = "views/global-model-" + name + ".heddle";
            var template = "@model(){{" + spelling + "}}@\\\n" + body;

            var (precompiled, dyn) = DifferentialHarness.Render(key, template, typeof(GlobalNamespaceModel),
                new GlobalNamespaceModel { Name = "ab" });
            Assert.Equal("[ab]\n", dyn);
            Assert.Equal(dyn, precompiled);
        }

        /// <summary>The nested half of the same rule, where the dot decides the opposite way: the engine indexes the
        /// dotted alias of a namespace-less nested type under <c>.Outer.Inner</c>, so the undotted spelling answers
        /// to nothing on either tier and the dotted one answers on both.</summary>
        [Fact]
        public void ANamespacelessNestedSpellingWithoutItsLeadingDotIsRefusedOnBothTiers()
        {
            const string key = "views/global-model-nested-bare.heddle";
            const string template = "@model(){{GlobalNamespaceModel.Inner}}@\\\n[@(Tag)]\n";
            var gen = DifferentialHarness.Generate(new[] { (key, template) });

            Assert.Single(gen.Diagnostics.Where(d => d.Id == "HED7007"));
            Assert.False(EngineCompiles(template, typeof(GlobalNamespaceModel.Inner)));
        }

        /// <summary>And with the leading dot both tiers bind it, which is what keeps the row above a rule about the
        /// dot rather than a refusal of nested spellings.</summary>
        [Fact]
        public void ANamespacelessNestedSpellingWithItsLeadingDotBindsOnBothTiers()
        {
            const string key = "views/global-model-nested-dotted.heddle";
            const string template = "@model(){{.GlobalNamespaceModel.Inner}}@\\\n[@(Tag)]\n";

            var (precompiled, dyn) = DifferentialHarness.Render(key, template, typeof(GlobalNamespaceModel.Inner),
                new GlobalNamespaceModel.Inner { Tag = "ab" });
            Assert.Equal("[ab]\n", dyn);
            Assert.Equal(dyn, precompiled);
        }

        /// <summary>
        /// An assembly-qualified spelling. The runtime hands one straight to <c>Type.GetType</c>, retrying the CLR
        /// name one <c>.</c>-to-<c>+</c> conversion at a time because the template lexer rejects <c>+</c>; the build
        /// tier had no arm for a comma at all, so a spelling the engine binds left the whole template on the dynamic
        /// tier with nothing said about it.
        /// </summary>
        [Fact]
        public void AnAssemblyQualifiedModelSpellingBindsOnBothTiers()
        {
            const string key = "views/aqn-model.heddle";
            var assembly = typeof(Article).Assembly.GetName().Name;
            var template = "@model(){{" + ArticleType + ", " + assembly + "}}@\\\n[@(Title)]\n";

            var (precompiled, dyn) = DifferentialHarness.Render(key, template, typeof(Article),
                new Article { Title = "T" });
            Assert.Equal("[T]\n", dyn);
            Assert.Equal(dyn, precompiled);
        }

        /// <summary>The same, for a <b>nested</b> type in the global namespace: the two halves of the rule — the
        /// dotted alias standing for the CLR's <c>+</c>, and the missing namespace — meet in one spelling.</summary>
        [Fact]
        public void AnAssemblyQualifiedNestedNamespacelessSpellingBindsOnBothTiers()
        {
            const string key = "views/aqn-model-nested.heddle";
            var assembly = typeof(GlobalNamespaceModel).Assembly.GetName().Name;
            var template = "@model(){{GlobalNamespaceModel.Inner, " + assembly + "}}@\\\n[@(Tag)]\n";

            var (precompiled, dyn) = DifferentialHarness.Render(key, template, typeof(GlobalNamespaceModel.Inner),
                new GlobalNamespaceModel.Inner { Tag = "ab" });
            Assert.Equal("[ab]\n", dyn);
            Assert.Equal(dyn, precompiled);
        }

        /// <summary>
        /// A stated <c>Version</c> the assembly does not carry. The engine goes to <c>Type.GetType</c>, whose
        /// default load context matches an already-loaded assembly by simple name and treats the rest of the
        /// identity as advice — so it resolves, strong-named or not. The build tier compared the version exactly and
        /// quietly left the template on the dynamic tier, which is what a host bumping an assembly version without
        /// editing its templates would have got.
        /// </summary>
        [Theory]
        [InlineData("0.0.0.1")]
        public void AStatedVersionBehindTheAssemblysOwnBindsOnBothTiers(string version)
        {
            var key = "views/aqn-model-version-" + version + ".heddle";
            var assembly = typeof(Article).Assembly.GetName().Name;
            var template = "@model(){{" + ArticleType + ", " + assembly + ", Version=" + version +
                           "}}@\\\n[@(Title)]\n";

            var (precompiled, dyn) = DifferentialHarness.Render(key, template, typeof(Article),
                new Article { Title = "T" });
            Assert.Equal("[T]\n", dyn);
            Assert.Equal(dyn, precompiled);
        }

        /// <summary>
        /// The limit of the arm above, and the near-neighbour that keeps it from reading as "the version is
        /// ignored". A version <b>ahead</b> of the one the assembly carries is not advice the loader can take: the
        /// default context satisfies a request from an already-loaded assembly only when the loaded version is at
        /// least the requested one, so it falls through to probing and finds the same too-old file. The engine
        /// therefore refuses, and the build tier has to refuse with it. It does not: it ignores the stated version
        /// and precompiles, so the same template renders on the build tier and throws on the dynamic one.
        /// <para>The engine half of this was masked until the suites moved to a runner that hosts the test assembly
        /// as its own entry point. Under the old host the request was satisfied from the already-loaded assembly
        /// whatever version was asked for, so both tiers appeared to agree.</para>
        /// </summary>
        [Fact(Skip = "known defect — generator: an assembly-qualified model version ahead of the assembly's own is " +
                     "ignored at build time and refused by the engine, so the tiers disagree; un-skip with that fix")]
        public void AStatedVersionAheadOfTheAssemblysOwnBindsOnNeitherTier()
        {
            const string key = "views/aqn-model-version-ahead.heddle";
            var assembly = typeof(Article).Assembly.GetName().Name;
            var template = "@model(){{" + ArticleType + ", " + assembly + ", Version=99.0.0.0}}@\\\n[@(Title)]\n";

            Assert.False(EngineCompiles(template, typeof(Article)));
            DifferentialHarness.ExpectDegrade(DifferentialHarness.Generate(new[] { (key, template) }), key);
        }

        /// <summary>The cost control for the arm above: an assembly-qualified spelling naming an assembly that is
        /// not there binds on neither tier, so the arm is a lookup and not a way of ignoring the qualifier.</summary>
        [Fact]
        public void AnAssemblyQualifiedSpellingNamingAnAbsentAssemblyBindsOnNeitherTier()
        {
            const string key = "views/aqn-model-wrong-assembly.heddle";
            var template = "@model(){{" + ArticleType + ", Zork.Nope}}@\\\nstatic text\n";
            var gen = DifferentialHarness.Generate(new[] { (key, template) });

            DifferentialHarness.ExpectDegrade(gen, key);
            Assert.False(EngineCompiles(template, typeof(object)));
        }
    }
}
