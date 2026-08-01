using System;
using System.Linq;
using Heddle;
using Heddle.Data;
using Heddle.Runtime;
using Heddle.Tests;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// The build tier's half of the compile-channel warnings. The generator does not run the runtime compiler, so
    /// every one of these is raised by its own walk; what must not differ is what the user is told. This suite is
    /// the only one that can compile the same bytes on both tiers in one process, so the comparison is made
    /// against the run tier's live output rather than against text restated here.
    /// </summary>
    public class CompileChannelDrainTests
    {
        /// <summary>Excluded because the harness passes no template root, not because of the fixture.</summary>
        private const string HarnessRootWarning = "HED7018";

        public static TheoryData<string> ForwardingCases
        {
            get
            {
                var data = new TheoryData<string>();
                foreach (var c in DiagnosticCorpusVectors.Cases.Where(c => c.BuildForwarded.Length > 0))
                    data.Add(c.Name);
                return data;
            }
        }

        private static Diagnostic[] Build(string template) =>
            DifferentialHarness.Generate(new[] { ("views/drain.heddle", template) })
                .Diagnostics
                .Where(d => d.Id != HarnessRootWarning)
                .ToArray();

        /// <summary>Everything the run tier says about the template: the compile warnings, which live on the
        /// compile context, and the errors the result renders.</summary>
        private static System.Collections.Generic.List<HeddleCompileError> Run(string template)
        {
            var options = new TemplateOptions("drain")
            {
                OutputProfile = OutputProfile.Html,
                RootPath = AppContext.BaseDirectory,
                FileNamePostfix = ".heddle"
            };
            var context = new CompileContext(options, ExType.Dynamic);
            var result = new HeddleTemplate(template, context).CompileResult;
            var all = new System.Collections.Generic.List<HeddleCompileError>(context.CompileWarnings);
            all.AddRange(result.ErrorList);
            return all;
        }

        /// <summary>Both texts are produced here, by the two implementations, and compared — a sentence that drifts
        /// on either tier fails instead of being restated in this file and agreeing with itself. Containment rather
        /// than equality because the build surface appends the remediation to the message.</summary>
        [Theory]
        [MemberData(nameof(ForwardingCases))]
        public void AForwardedEntrySaysWhatTheRunTierSays(string name)
        {
            var c = DiagnosticCorpusVectors.Cases.First(x => x.Name == name);
            var runTier = Run(c.Template);

            var checkedAny = false;
            foreach (var reported in Build(c.Template)
                         .Where(d => !d.Id.StartsWith("HED7", StringComparison.Ordinal)))
            {
                var twin = runTier.FirstOrDefault(e => e.DiagnosticId == reported.Id &&
                                                       e.Position.StartIndex == reported.Location.SourceSpan.Start);
                Assert.True(twin != null,
                    $"{name}: the build tier reported {reported.Id} at {reported.Location.SourceSpan.Start}, " +
                    "which the run tier does not raise for the same bytes.");

                var message = reported.GetMessage();
                Assert.Contains(twin.Error, message, StringComparison.Ordinal);
                var fix = (twin as HeddleCompileWarning)?.Fix;
                if (!string.IsNullOrEmpty(fix))
                    Assert.Contains(fix, message, StringComparison.Ordinal);
                checkedAny = true;
            }

            Assert.True(checkedAny, name + ": the corpus declares forwarded entries but none were reported.");
        }

        /// <summary>A warning is advice, not a refusal: the fixtures that now draw one still precompile and still
        /// render the engine's bytes. Without this the drain could "pass" by degrading every template it warns on.
        /// </summary>
        [Theory]
        [InlineData("braceMisread")]
        [InlineData("strippedGap")]
        [InlineData("elseCondition")]
        [InlineData("encodingLint")]
        [InlineData("doubleRender")]
        public void AWarnedTemplateStillPrecompilesAndRendersTheEnginesBytes(string name)
        {
            var c = DiagnosticCorpusVectors.Cases.First(x => x.Name == name);

            var gen = DifferentialHarness.Generate(new[] { ("views/drain.heddle", c.Template) });
            Assert.DoesNotContain(gen.Diagnostics, d => d.Severity == DiagnosticSeverity.Error);
            DifferentialHarness.ExpectPrecompiled(gen, "views/drain.heddle");

            var (precompiled, dynamic) =
                DifferentialHarness.Render("views/drain.heddle", c.Template, typeof(object), null);
            Assert.Equal(dynamic, precompiled);
        }

        /// <summary>The two warnings whose condition needs a typed model, which the shared corpus excludes by
        /// design. Same comparison: the id, the span and the sentence come from the run tier's compile of the same
        /// bytes, and the near neighbour one edit away must stay silent on both tiers.</summary>
        [Theory]
        [InlineData("@model(){{Heddle.Generator.IntegrationTests.Fixtures.Cart}}@(html(Name))", "HED2003",
            "@model(){{Heddle.Generator.IntegrationTests.Fixtures.Cart}}@html(Name)")]
        [InlineData("@% <panel(Name: string = \"P\")>{{@(Name)}} :: Heddle.Generator.IntegrationTests.Fixtures.Cart %@\n@panel(this)",
            "HED5011",
            "@% <panel(Tone: string = \"P\")>{{@(Tone)}} :: Heddle.Generator.IntegrationTests.Fixtures.Cart %@\n@panel(this)")]
        public void ATypedModelWarningIsForwardedWithTheRunTiersSentence(string template, string id,
            string nearNeighbour)
        {
            var runTier = Run(template);
            var twin = Assert.Single(runTier, e => e.DiagnosticId == id);

            var reported = Assert.Single(Build(template), d => d.Id == id);
            Assert.Equal(DiagnosticSeverity.Warning, reported.Severity);
            Assert.Equal(twin.Position.StartIndex, reported.Location.SourceSpan.Start);

            var message = reported.GetMessage();
            Assert.Contains(twin.Error, message, StringComparison.Ordinal);
            Assert.Contains(((HeddleCompileWarning)twin).Fix, message, StringComparison.Ordinal);

            Assert.DoesNotContain(Run(nearNeighbour), e => e.DiagnosticId == id);
            Assert.DoesNotContain(Build(nearNeighbour), d => d.Id == id);
        }

        /// <summary>The orphan terminal is the one entry on the branch-set event stream that is an <b>error</b>:
        /// the engine refuses the template outright. The build tier classifies the same block with the same shared
        /// machine but drained only the warning arm, so it precompiled an entry class for bytes the engine will not
        /// compile. It now takes the error arm too and declines the body, leaving the template on the dynamic tier
        /// where the engine's own error reaches the caller.</summary>
        [Fact]
        public void ATemplateTheEngineRefusesIsNotSilentlyPrecompiled()
        {
            const string template = "@else(){{1}}";

            var refusal = Run(template).Where(e => !(e is HeddleCompileWarning)).ToList();
            Assert.NotEmpty(refusal);

            var gen = DifferentialHarness.Generate(new[] { ("views/drain.heddle", template) });
            var saidSo = gen.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error);
            var precompiled = (gen.ManifestSource ?? string.Empty).Contains("views/drain.heddle");

            Assert.True(saidSo || !precompiled,
                "the engine refuses these bytes, but the build tier precompiled them and reported nothing.");
        }

        /// <summary>The near neighbour of each warned shape: one edit away, and silent on both tiers. A drain that
        /// widened its condition would report here, and a run tier that quietly stopped warning would leave the
        /// paired fixture above with nothing to compare against.</summary>
        [Theory]
        [InlineData("hello { Title } world")]                       // single braces are not the misread shape
        [InlineData("@if(true){{a}}@else(){{b}}")]                  // no gap, no condition on the terminal
        [InlineData("@if(true){{a}} \t @else(){{b}}")]              // whitespace-only gap is stripped silently
        [InlineData("<a>@(1)</a>")]                                 // element text: the default encoder is right
        [InlineData("@profile(){{text}}\n@(1)\nx")]                 // directive before any output
        [InlineData("@%\n<card>\n{{CARD}}\n%@\n@card()")]           // by-name call, but no default output
        [InlineData("@%\n<card> -> ()\n{{CARD}}\n%@")]              // default output, but no by-name call
        public void TheNearNeighbourIsSilentOnBothTiers(string template)
        {
            Assert.DoesNotContain(Run(template),
                e => e is HeddleCompileWarning && !string.IsNullOrEmpty(e.DiagnosticId));

            Assert.DoesNotContain(Build(template),
                d => !d.Id.StartsWith("HED7", StringComparison.Ordinal));
        }
    }
}
