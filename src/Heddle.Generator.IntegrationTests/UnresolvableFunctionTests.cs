using System.IO;
using System.Linq;
using Heddle.TestCorpus;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// What is left of the unresolvable-function channel now that a call the build cannot bind is emitted as a
    /// late-bound site instead (<see cref="LateBoundFunctionTests"/>). <c>HED7014</c> still fires, and still
    /// degrades the template to a marker entry, for the calls late binding genuinely cannot serve: the argument
    /// whose static type has no build-time answer, so the outer call cannot be ranked against the type the engine
    /// will rank it against. Generated source is omitted; the runtime uses the dynamic path.
    /// </summary>
    public class UnresolvableFunctionTests
    {
        private const string ProductType = "Heddle.Generator.IntegrationTests.Fixtures.Product";

        /// <summary>The surviving HED7014 shape: the argument is itself a call no metadata carries, so its return
        /// type is the value the build cannot know — and a late-bound site that guessed it would rank the outer
        /// call against a type the engine never saw.</summary>
        [Fact]
        public void UnresolvableFunctionReportsPositionedHed7014AndNoSource()
        {
            var t = "@model(){{" + ProductType + "}}@\\\n<span>@(mystery(other(Name) + \"!\"))</span>\n";
            var gen = DifferentialHarness.Generate(new[] { ("views/label-fancy.heddle", t) });

            var hed7014 = gen.Diagnostics.Where(d => d.Id == "HED7014").ToList();
            Assert.Single(hed7014);
            Assert.Equal(DiagnosticSeverity.Warning, hed7014[0].Severity);
            Assert.Contains("mystery", hed7014[0].GetMessage());

            // Error at mystery(...) call: line 1, column 8 (0-based).
            var span = hed7014[0].Location.GetLineSpan();
            Assert.Equal(1, span.StartLinePosition.Line);
            Assert.Equal(8, span.StartLinePosition.Character);

            Assert.Empty(gen.TemplateSources);

            Assert.NotNull(gen.ManifestSource);
            Assert.Contains("entryPointType: null", gen.ManifestSource);
            Assert.Contains("strategy: null", gen.ManifestSource);
            Assert.Contains("new global::Heddle.Precompiled.PrecompiledFunctionBinding(\"mystery\", null, 0)",
                gen.ManifestSource);
        }

        /// <summary>
        /// The corpus's marker entry, generated from the real file. Without this the marker tier had no member and
        /// the whole-corpus set equality was pinning an empty set — a bucket that had emptied out looked healthy.
        /// </summary>
        [Fact]
        public void TheCorpusMarkerFixtureDegradesToAMarkerEntry()
        {
            const string name = "fn-unresolvable-marker.heddle";
            var corpus = TestCorpusIndex.Load(includeFrontEndErrorFixtures: false);
            var target = corpus.First(t => Path.GetFileName(t.key) == name);

            var gen = DifferentialHarness.Generate(corpus, globalOptions: null,
                extraReferences: DifferentialHarness.EngineTestModelReferences());

            Assert.Equal(DifferentialHarness.ManifestState.Marker,
                DifferentialHarness.ClassifyInManifest(gen.ManifestSource ?? string.Empty, target.key));

            var hed7014 = gen.Diagnostics
                .Where(d => d.Id == "HED7014"
                            && Path.GetFileName(d.Location.GetLineSpan().Path) == name)
                .ToList();
            Assert.Single(hed7014);
            Assert.Equal(DiagnosticSeverity.Warning, hed7014[0].Severity);
            Assert.Contains("mystery", hed7014[0].GetMessage());
        }

        /// <summary>The half that moved: a call whose ARGUMENT the build can type is no longer marked at all —
        /// it precompiles as a late-bound site. Pinned here, beside the shape that still degrades, because what
        /// separates the two is the argument and nothing else.</summary>
        [Fact]
        public void AnUnresolvableFunctionOverATypeableArgumentIsNoLongerMarked()
        {
            var t = "@model(){{" + ProductType + "}}@\\\n<span>@(mystery(Name))</span>\n";
            var gen = DifferentialHarness.Generate(new[] { ("views/label-late.heddle", t) });

            Assert.DoesNotContain(gen.Diagnostics, d => d.Id == "HED7014");
            DifferentialHarness.ExpectPrecompiled(gen, "views/label-late.heddle");
            Assert.Contains("new global::Heddle.Precompiled.PrecompiledFunctionBinding(\"mystery\", null, 0)",
                gen.ManifestSource);
        }

        [Fact]
        public void ResolvableFunctionsAreNotMarked()
        {
            // upper is a default built-in; titlecase/shout are exported by the test assembly — all resolvable.
            var t = "@model(){{" + ProductType + "}}@\\\n<span>@(upper(Name)) @(titlecase(Name)) @(shout(Name))</span>\n";
            var gen = DifferentialHarness.Generate(new[] { ("views/label-ok.heddle", t) });

            Assert.DoesNotContain(gen.Diagnostics, d => d.Id == "HED7014");
            Assert.Single(gen.TemplateSources);
        }
    }
}
