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
        /// The other cost control, and the reason the rule is a <b>suffix</b> match and not a full-name one: a
        /// spelling that names the tail of a real namespace is the shape an import completes, and the runtime binds
        /// what is loaded rather than what this compilation references. Calling that a typo would turn a template
        /// that renders into a build error, which is the direction this gate must never take. It degrades to the
        /// tier that can bind it, without a word.
        /// </summary>
        [Fact]
        public void ADottedModelSpellingThatNamesTheTailOfARealNamespaceIsNotCalledATypo()
        {
            const string key = "views/dotted-model-tail.heddle";
            const string template = "@model(){{IntegrationTests.Fixtures.Article}}@\\\n[@(Title)]\n";
            var gen = DifferentialHarness.Generate(new[] { (key, template) });

            Assert.Empty(gen.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
        }
    }
}
