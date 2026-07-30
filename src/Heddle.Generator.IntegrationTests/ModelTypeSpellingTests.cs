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
    }
}
