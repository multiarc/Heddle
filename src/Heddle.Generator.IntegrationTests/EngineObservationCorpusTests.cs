using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Heddle.Data;
using Heddle.Precompiled;
using Heddle.Runtime;
using Heddle.TestCorpus;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// What engine observation may and may not change, now that a body's typing comes from a real engine compile
    /// and from nowhere else.
    /// <para><b>The reference is the engine, not the other mode.</b> These gates used to compare an
    /// <c>HeddleObserveEngine=Off</c> build against a <c>=Strict</c> one and demand the two agree on bytes, on
    /// tiers and on refusal categories. That premise held while observation was an optimisation layered over a
    /// table of built-in names: the blind build already typed the engine's own body-hosting extensions, so the two
    /// runs covered the same ground. The table is gone, so <c>Off</c> is a genuinely reduced mode — it types a body
    /// only where an attribute declares it — and demanding tier equality asserts something the architecture no
    /// longer claims.</para>
    /// <para>What has not moved is the project's one hard contract: <b>the precompiled tier renders what the engine
    /// renders</b>. So each mode's precompiled output is compared against the dynamic engine's own bytes rather
    /// than against the other mode's. That is strictly stronger than the comparison it replaces — two modes that
    /// were wrong the same way used to pass — and it is what caught a nested branch set rendering an exception on
    /// the blind tier.</para>
    /// <para>What is left of monotonicity is per <b>template</b> rather than per refusal category: observation may
    /// never take a template off the precompiled tier. It may remove a refusal and thereby reveal a later one the
    /// blind build never reached — <c>recursion.heddle</c> refuses <c>HookBehavior</c> blind and <c>HostSetup</c>
    /// observed, and it is the same refused template either way — so the category-level subset it used to assert is
    /// not a property of anything.</para>
    /// </summary>
    public class EngineObservationCorpusTests
    {
        private static readonly string ObserveDirectory = CreateObserveDirectory();

        private static string CreateObserveDirectory()
        {
            var path = Path.Combine(Path.GetTempPath(), "heddle-observe-corpus", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }

        private static Dictionary<string, string> Options(string mode) => new Dictionary<string, string>
        {
            ["build_property." + HeddleBuildOptions.ObserveEngineProperty] = mode,
            ["build_property." + HeddleBuildOptions.ObserveIntermediatePathProperty] = ObserveDirectory
        };

        /// <summary>Everything the corpus can render without a model, which is the set the render-parity gate
        /// already uses — read from the intent table rather than restated, so a template added to one gate joins
        /// this one too.</summary>
        public static IEnumerable<object[]> Renderable() =>
            CorpusIntent.Rows
                .Where(r => r.Bound && r.Render != CorpusRender.ResolveOnly)
                .Select(r => r.Name)
                .OrderBy(n => n, StringComparer.Ordinal)
                .Select(n => new object[] { n });

        /// <summary>
        /// Each mode's precompiled bytes are the engine's bytes.
        /// <para>The observed build must carry an entry class for every template the intent table declares
        /// precompiling — that table describes a default build, which observes — and its render must match the
        /// engine's. The blind build is allowed to have no entry class at all, which is the reduced coverage
        /// <c>Off</c> now means; where it does carry one, that one renders the engine's bytes too.</para>
        /// </summary>
        [Theory]
        [MemberData(nameof(Renderable))]
        public void EachModesPrecompiledOutputIsTheEnginesOutput(string name)
        {
            var corpus = TestCorpusIndex.Load(includeFrontEndErrorFixtures: false);
            var target = corpus.FirstOrDefault(t => Path.GetFileName(t.key) == name);
            Assert.False(target.content == null, "Corpus template not found: " + name);

            var reference = RenderThroughEngine(target.content);

            var observedEntry = EntryType(Observed.Value, target.key);
            Assert.True(observedEntry != null,
                "The intent table declares '" + name + "' precompiling, and a default build observes, so the " +
                "observed build must carry an entry class for it — it fell back instead.");
            Assert.Equal(reference, RenderThroughEntry(observedEntry));

            var blindEntry = EntryType(Blind.Value, target.key);
            if (blindEntry != null)
                Assert.Equal(reference, RenderThroughEntry(blindEntry));
        }

        /// <summary>
        /// Observation never takes a template off the precompiled tier. It may put one on — that is the whole
        /// point of reading the engine instead of guessing — so drift is asserted in one direction only, and the
        /// direction it is asserted in names the template.
        /// </summary>
        [Fact]
        public void ObservationNeverTakesATemplateOffThePrecompiledTier()
        {
            var corpus = TestCorpusIndex.Load(includeFrontEndErrorFixtures: false);

            var lost = new List<string>();
            foreach (var (key, _) in corpus)
            {
                var before = DifferentialHarness.ClassifyInManifest(Blind.Value.ManifestSource ?? string.Empty, key);
                var after = DifferentialHarness.ClassifyInManifest(Observed.Value.ManifestSource ?? string.Empty, key);
                if (before == DifferentialHarness.ManifestState.Precompiled &&
                    after != DifferentialHarness.ManifestState.Precompiled)
                    lost.Add(Path.GetFileName(key) + ": " + before + " -> " + after);
            }

            lost.Sort(StringComparer.Ordinal);
            Assert.True(lost.Count == 0,
                "Observation moved templates OFF the precompiled tier, which it may never do — a build that reads " +
                "the engine cannot know less than one that does not: " + string.Join("; ", lost));
        }

        /// <summary>
        /// A template the blind build precompiles is still precompiled observed, stated through the other carrier:
        /// the <c>HED7031</c> refusals. The set of refused templates observed is a subset of the set refused blind.
        /// <para><b>Per template, not per category.</b> A refusal is taken at the first thing that refuses, so
        /// removing an early one reveals whatever stood behind it: <c>recursion.heddle</c> and
        /// <c>dynamic-recursion.heddle</c> refuse <c>HookBehavior</c> blind — a body under an unread hook — and
        /// <c>HostSetup</c> observed, which is the embedded C# in them that the blind build never walked far enough
        /// to see. Neither template precompiles either way. The category subset the old gate asserted was a
        /// property of the name table, not of observation.</para>
        /// </summary>
        [Fact]
        public void ObservationOnlyEverRemovesRefusals()
        {
            var blind = Refused(Blind.Value);
            var observed = Refused(Observed.Value);

            var added = observed.Where(name => !blind.Contains(name)).OrderBy(n => n, StringComparer.Ordinal)
                .ToList();

            Assert.True(added.Count == 0,
                "Observation refused templates the blind build precompiled, which is a coverage regression: " +
                string.Join("; ", added));
        }

        /// <summary>
        /// The two runs are genuinely different runs. Every gate above compares a blind build against an observed
        /// one; if the two ever became the same build, all of them would keep passing while measuring nothing. At
        /// least one corpus template has to precompile observed and not blind, and the failure names the set so a
        /// change that legitimately closes the gap says which templates it closed it for.
        /// </summary>
        [Fact]
        public void ObservationWidensCoverageOnThisCorpus()
        {
            var corpus = TestCorpusIndex.Load(includeFrontEndErrorFixtures: false);

            var gained = new List<string>();
            foreach (var (key, _) in corpus)
            {
                var before = DifferentialHarness.ClassifyInManifest(Blind.Value.ManifestSource ?? string.Empty, key);
                var after = DifferentialHarness.ClassifyInManifest(Observed.Value.ManifestSource ?? string.Empty, key);
                if (before != DifferentialHarness.ManifestState.Precompiled &&
                    after == DifferentialHarness.ManifestState.Precompiled)
                    gained.Add(Path.GetFileName(key));
            }

            Assert.True(gained.Count != 0,
                "No corpus template precompiles observed that does not precompile blind, so the two runs this " +
                "suite compares are the same run and the comparisons prove nothing.");
        }

        /// <summary>Under <c>Strict</c> the corpus build must genuinely observe — otherwise the byte gate above is
        /// comparing an unobserved run against the engine and calling it observation.</summary>
        [Fact]
        public void TheStrictCorpusRunGenuinelyObserves()
        {
            Assert.Empty(Observed.Value.Diagnostics.Where(d => d.Id == "HED7034"));
        }

        /// <summary>Every template name the build refused to precompile (<c>HED7031</c>), whatever category it
        /// gave.</summary>
        private static HashSet<string> Refused(DifferentialHarness.GenResult gen)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var diagnostic in gen.Diagnostics)
            {
                if (diagnostic.Id != HeddleDiagnosticIds.BuildTemplateNotPrecompiled)
                    continue;
                var path = diagnostic.Location.GetLineSpan().Path;
                if (!string.IsNullOrEmpty(path))
                    names.Add(Path.GetFileName(path));
            }

            return names;
        }

        private static readonly Lazy<DifferentialHarness.GenResult> Blind =
            new Lazy<DifferentialHarness.GenResult>(() => GenerateCorpus("Off"));

        private static readonly Lazy<DifferentialHarness.GenResult> Observed =
            new Lazy<DifferentialHarness.GenResult>(() => GenerateCorpus("Strict"));

        /// <summary>One whole-corpus generation per mode for the whole suite. Every gate here reads both runs, and
        /// regenerating the corpus per theory case was the same two builds over and over.</summary>
        private static DifferentialHarness.GenResult GenerateCorpus(string mode)
        {
            var corpus = TestCorpusIndex.Load(includeFrontEndErrorFixtures: false);
            var gen = DifferentialHarness.Generate(corpus, Options(mode),
                DifferentialHarness.EngineTestModelReferences());
            var errors = gen.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
            Assert.True(errors.Count == 0,
                "Generator errors under " + mode + ": " + string.Join("\n", errors.Select(e => e.ToString())));
            return gen;
        }

        private static Type EntryType(DifferentialHarness.GenResult gen, string targetKey) =>
            DifferentialHarness.FindEntryTypeByKey(gen.Assembly, targetKey);

        /// <summary>The corpus-rooted options both tiers render under: imports resolve out of the corpus directory
        /// exactly as <c>CorpusRenderParityTests</c> resolves them.</summary>
        private static TemplateOptions CorpusOptions()
        {
            var rooted = TestCorpusIndex.CorpusDir;
            if (!rooted.EndsWith("/", StringComparison.Ordinal) && !rooted.EndsWith("\\", StringComparison.Ordinal))
                rooted += Path.DirectorySeparatorChar;
            return new TemplateOptions { RootPath = rooted, FileNamePostfix = ".heddle" };
        }

        private static string RenderThroughEntry(Type entryType)
        {
            var root = (IProcessStrategy)entryType
                .GetField("Root", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public |
                                  System.Reflection.BindingFlags.Static)
                .GetValue(null);
            return PrecompiledRuntime.GenerateString(root, null, null, null, CorpusOptions());
        }

        /// <summary>The reference bytes: the same template through the dynamic engine, model-less and typed
        /// dynamic the way the generated root types it.</summary>
        private static string RenderThroughEngine(string content)
        {
            var template = new HeddleTemplate(content, new CompileContext(CorpusOptions(), ExType.Dynamic));
            Assert.True(template.CompileResult.Success, "Dynamic compile failed: " + template.CompileResult);
            return template.Generate(null);
        }
    }
}
