using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Heddle.Generator.Tests
{
    /// <summary>
    /// The emitter answers two questions by building a probe compilation on top of the consumer's own — whether a
    /// <c>@using</c> body is a directive that compiles, and whether an embedded C# expression compiles. Roslyn
    /// requires every tree in a compilation to have been parsed alike and refuses the graft outright otherwise
    /// ("Inconsistent syntax tree features"), so a probe parsed with the defaults throws for any consumer whose
    /// project sets a language version, a preprocessor symbol or a compiler feature. That is not exotic: a plain
    /// <c>dotnet build</c> on the current SDK already passes <c>/features:InterceptorsNamespaces=…</c>, and two
    /// shipped samples stopped building because of it — the emitter reported HED7020 and nothing precompiled.
    /// <para>Every consumer-parse-options case is paired with the same template under the defaults, because a fix
    /// that made the probe stop asking its question would pass the first assertion and fail the second.</para>
    /// </summary>
    public class ConsumerParseOptionsTests
    {
        /// <summary>What a real consumer project carries: the feature string the SDK passes unasked, an explicit
        /// language version, and a preprocessor symbol of the project's own.</summary>
        private static readonly CSharpParseOptions ConsumerOptions = CSharpParseOptions.Default
            .WithLanguageVersion(LanguageVersion.Latest)
            .WithFeatures(new[] { new KeyValuePair<string, string>("InterceptorsNamespaces", ";Some.Generated") })
            .WithPreprocessorSymbols("TRACE", "DEBUG", "NET10_0_OR_GREATER");

        private const string ModelSource =
            "namespace Probe.Models { public class Article { public string Title { get; set; } } }";

        private static readonly Dictionary<string, string> FullCSharp =
            new Dictionary<string, string> { ["build_property.HeddleExpressionMode"] = "FullCSharp" };

        private static string EmittedBody(GeneratorRun run) =>
            run.GeneratedSourceTexts.FirstOrDefault(s => s.Contains("class Body0"));

        private static void AssertPrecompiled(GeneratorRun run)
        {
            var errors = run.GeneratorDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
            Assert.True(errors.Count == 0, string.Join("\n", errors.Select(d => d.ToString())));
            Assert.NotNull(EmittedBody(run));
        }

        /// <summary>The <c>@using</c> probe. The body names a namespace the consumer declares and the template
        /// carries an embedded expression, so the directive is written into the fragment block the expression
        /// compiles under — reaching the probe compilation is the whole point of the shape.</summary>
        [Fact]
        public void AUsingDirectiveIsJudgedInAConsumerCompilationThatSetsParseOptions()
        {
            var run = GeneratorHarness.RunWithSources(
                new[] { ("views/using-options.heddle",
                    "@using(){{Probe.Models}}@\\\n@model(){{Article}}@\\\n[@(Title)]@(@model.Title)\n") },
                new[] { ModelSource },
                FullCSharp,
                parseOptions: ConsumerOptions);

            AssertPrecompiled(run);
            Assert.Contains("using Probe.Models;", EmittedBody(run));
        }

        /// <summary>The same template under the default parse options, which is where the probe already worked —
        /// and the emitted bytes are the same, so the options decide nothing but whether the graft is legal.</summary>
        [Fact]
        public void AUsingDirectiveEmitsTheSameBytesUnderDefaultAndConsumerParseOptions()
        {
            var template = new[]
                { ("views/using-options.heddle", "@using(){{Probe.Models}}@\\\n@model(){{Article}}@\\\n[@(Title)]\n") };

            var underDefaults = GeneratorHarness.RunWithSources(template, new[] { ModelSource });
            var underConsumer = GeneratorHarness.RunWithSources(template, new[] { ModelSource },
                parseOptions: ConsumerOptions);

            AssertPrecompiled(underDefaults);
            AssertPrecompiled(underConsumer);
            Assert.Equal(EmittedBody(underDefaults), EmittedBody(underConsumer));
        }

        /// <summary>The embedded-C# probe, which is a second graft onto the same compilation and breaks on its own:
        /// no <c>@using</c> here, so the directive probe is never reached.</summary>
        [Fact]
        public void AnEmbeddedCSharpExpressionIsTypedInAConsumerCompilationThatSetsParseOptions()
        {
            var run = GeneratorHarness.RunWithSources(
                new[] { ("views/embedded-options.heddle",
                    "@model(){{Probe.Models.Article}}@\\\n[@(@model.Title.Length)]\n") },
                new[] { ModelSource },
                FullCSharp,
                parseOptions: ConsumerOptions);

            AssertPrecompiled(run);
            Assert.Contains("model.Title.Length", EmittedBody(run));
        }

        /// <summary>The near-neighbour that keeps the probe honest: an expression the compiler rejects must still
        /// be refused under consumer parse options. A probe that silently stopped compiling anything would let this
        /// text through into the generated file, where it becomes the consumer's own build error.</summary>
        [Fact]
        public void AnEmbeddedCSharpExpressionThatDoesNotCompileIsStillRefusedUnderConsumerParseOptions()
        {
            var run = GeneratorHarness.RunWithSources(
                new[] { ("views/embedded-bad-options.heddle",
                    "@model(){{Probe.Models.Article}}@\\\n[@(@model.NoSuchMember)]\n") },
                new[] { ModelSource },
                FullCSharp,
                parseOptions: ConsumerOptions);

            var errors = run.GeneratorDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
            Assert.True(errors.Count == 0, string.Join("\n", errors.Select(d => d.ToString())));
            var body = EmittedBody(run);
            if (body != null)
                Assert.DoesNotContain("NoSuchMember", body);
        }
    }
}
