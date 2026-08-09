extern alias generator;
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
    /// The two gates that make engine observation an <b>optimisation</b> rather than a second compiler.
    /// <para><b>Byte identity.</b> The whole corpus is generated twice — once with
    /// <c>HeddleObserveEngine=Off</c> and once with <c>=Strict</c> — and every renderable entry is rendered through
    /// both. The bytes must be identical. Observation may change which tier a <i>read</i> takes; it may never
    /// change a rendered byte, and this is that guarantee written as something that runs.</para>
    /// <para><b>Coverage monotonicity.</b> Every per-template refusal category with observation on must also be
    /// one it had with observation off. Observation is consulted only where the build had no typing of its own, so
    /// this holds by construction — and this test is what makes it stay true when that stops being obvious. It is
    /// pinned per template and per category, so a regression names both rather than reporting a count.</para>
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

        [Theory]
        [MemberData(nameof(Renderable))]
        public void ObservationChangesNoRenderedByte(string name)
        {
            var corpus = TestCorpusIndex.Load(includeFrontEndErrorFixtures: false);
            var target = corpus.FirstOrDefault(t => Path.GetFileName(t.key) == name);
            Assert.False(target.content == null, "Corpus template not found: " + name);

            var blind = RenderThrough(Off, corpus, target.key);
            var observed = RenderThrough(Strict, corpus, target.key);
            Assert.Equal(blind, observed);
        }

        /// <summary>
        /// The refusal categories a template collects with observation on are a <b>subset</b> of the ones it
        /// collects with observation off — never a superset, and never a different set. A template that starts
        /// declining for a class of reason only the observed build reaches is a degrade, and this names it.
        /// </summary>
        [Fact]
        public void ObservationOnlyEverRemovesRefusals()
        {
            var corpus = TestCorpusIndex.Load(includeFrontEndErrorFixtures: false);
            var blind = Categories(Off.Value, corpus);
            var observed = Categories(Strict.Value, corpus);

            var added = observed
                .Where(pair => !blind.TryGetValue(pair.Key, out var had) || !had.IsSupersetOf(pair.Value))
                .Select(pair => pair.Key + ": " + string.Join(", ",
                    pair.Value.Except(blind.TryGetValue(pair.Key, out var seen)
                            ? seen
                            : new SortedSet<string>(StringComparer.Ordinal))
                        .OrderBy(c => c, StringComparer.Ordinal)))
                .OrderBy(line => line, StringComparer.Ordinal)
                .ToList();

            Assert.True(added.Count == 0,
                "Observation ADDED refusal categories, which is a coverage regression — a template it could not " +
                "type must still emit exactly what it emitted before: " + string.Join("; ", added));
        }

        /// <summary>The manifest is the same document either way: observation changes which typing a body carries,
        /// not which templates precompile, so no entry may appear, vanish, or change tier between the two runs.
        /// </summary>
        [Fact]
        public void ObservationChangesNoTemplatesTier()
        {
            var corpus = TestCorpusIndex.Load(includeFrontEndErrorFixtures: false);
            var blind = DifferentialHarness.Generate(corpus, Off.Value, Extra);
            var observed = DifferentialHarness.Generate(corpus, Strict.Value, Extra);

            var drift = new List<string>();
            foreach (var (key, _) in corpus)
            {
                var before = DifferentialHarness.ClassifyInManifest(blind.ManifestSource ?? string.Empty, key);
                var after = DifferentialHarness.ClassifyInManifest(observed.ManifestSource ?? string.Empty, key);
                if (before != after)
                    drift.Add(Path.GetFileName(key) + ": " + before + " -> " + after);
            }

            drift.Sort(StringComparer.Ordinal);
            Assert.True(drift.Count == 0,
                "Observation moved templates between tiers, which it may never do: " + string.Join("; ", drift));
        }

        /// <summary>Under <c>Strict</c> the corpus build must genuinely observe — otherwise the byte-identity gate
        /// above is comparing two identical unobserved runs and proving nothing.</summary>
        [Fact]
        public void TheStrictCorpusRunGenuinelyObserves()
        {
            var corpus = TestCorpusIndex.Load(includeFrontEndErrorFixtures: false);
            var observed = DifferentialHarness.Generate(corpus, Strict.Value, Extra);

            Assert.Empty(observed.Diagnostics.Where(d => d.Id == "HED7034"));
        }

        private static readonly Lazy<Dictionary<string, string>> Off =
            new Lazy<Dictionary<string, string>>(() => Options("Off"));

        private static readonly Lazy<Dictionary<string, string>> Strict =
            new Lazy<Dictionary<string, string>>(() => Options("Strict"));

        private static IReadOnlyList<MetadataReference> Extra => DifferentialHarness.EngineTestModelReferences();

        private static string RenderThrough(Lazy<Dictionary<string, string>> options,
            IReadOnlyList<(string key, string content)> corpus, string targetKey)
        {
            var gen = DifferentialHarness.Generate(corpus, options.Value, Extra);
            var errors = gen.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
            Assert.True(errors.Count == 0, "Generator errors: " + string.Join("\n", errors.Select(e => e.ToString())));

            var entryType = DifferentialHarness.FindEntryTypeByKey(gen.Assembly, targetKey);
            Assert.True(entryType != null, "Generated entry class not found (fell back): " + targetKey);

            var rooted = TestCorpusIndex.CorpusDir;
            if (!rooted.EndsWith("/", StringComparison.Ordinal) && !rooted.EndsWith("\\", StringComparison.Ordinal))
                rooted += Path.DirectorySeparatorChar;
            var runtimeOptions = new TemplateOptions { RootPath = rooted, FileNamePostfix = ".heddle" };
            var root = (IProcessStrategy)entryType
                .GetField("Root", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public |
                                  System.Reflection.BindingFlags.Static)
                .GetValue(null);
            return PrecompiledRuntime.GenerateString(root, null, null, null, runtimeOptions);
        }

        /// <summary>Every HED7031 refusal category the build recorded, by template file name.</summary>
        private static Dictionary<string, SortedSet<string>> Categories(Dictionary<string, string> options,
            IReadOnlyList<(string key, string content)> corpus)
        {
            var gen = DifferentialHarness.Generate(corpus, options, Extra);
            var byTemplate = new Dictionary<string, SortedSet<string>>(StringComparer.Ordinal);
            foreach (var diagnostic in gen.Diagnostics)
            {
                if (diagnostic.Id != Heddle.Data.HeddleDiagnosticIds.BuildTemplateNotPrecompiled)
                    continue;
                var path = diagnostic.Location.GetLineSpan().Path;
                if (string.IsNullOrEmpty(path))
                    continue;
                var name = Path.GetFileName(path);
                if (!byTemplate.TryGetValue(name, out var categories))
                    byTemplate[name] = categories = new SortedSet<string>(StringComparer.Ordinal);
                categories.Add(diagnostic.Properties.TryGetValue(
                    generator::Heddle.Generator.Diagnostics.GeneratorDiagnostics.RefusalCategoryProperty,
                    out var value)
                    ? value
                    : "<none>");
            }

            return byTemplate;
        }
    }
}
