using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Heddle.TestCorpus;
using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// Render parity: for every corpus template the intent table declares model-less and precompiling, renders the
    /// real <c>TestTemplate/**</c> file through both backends and asserts byte-identical output — the
    /// render-correctness gate the classification-only <see cref="CorpusDifferentialTests"/> does not itself provide.
    /// Imports resolve from the corpus directory on the dynamic side and from the whole-corpus
    /// <c>AdditionalFiles</c> set on the precompiled side.
    /// <para>The set comes from the intent table rather than a hand-written list. It was ten names against
    /// thirty-two eligible templates, with nothing to notice the other twenty-two — a template could be added,
    /// declared standalone and precompiling, and never rendered by this gate at all.</para>
    /// </summary>
    public class CorpusRenderParityTests
    {
        /// <summary>Entries declared model-less (both backends render them without a model) and precompiling.</summary>
        public static IEnumerable<object[]> StandaloneRenderable() =>
            CorpusIntent.Rows
                .Where(r => r.Tier == CorpusTier.Precompiles && r.Render == CorpusRender.Standalone)
                .Select(r => r.Name)
                .OrderBy(n => n, StringComparer.Ordinal)
                .Select(n => new object[] { n });

        [Theory]
        [MemberData(nameof(StandaloneRenderable))]
        public void ModelLessCorpusTemplateRendersIdentically(string name)
        {
            // The corpus is in THIS project's own output directory (TestCorpus.props), so locating it is
            // AppContext.BaseDirectory and nothing else.
            var dir = TestCorpusIndex.CorpusDir;
            // FrontEndError entries carry deliberate parse errors; excluded so the rest of the corpus generates
            // cleanly (imports still resolve from what remains). The set is read from the intent table.
            var corpus = TestCorpusIndex.Load(includeFrontEndErrorFixtures: false);
            var target = corpus.FirstOrDefault(t => Path.GetFileName(t.key) == name);
            Assert.False(target.content == null, "Corpus template not found: " + name);

            var (precompiled, dyn) = DifferentialHarness.RenderInCorpus(
                corpus, target.key, target.content, typeof(object), null, dir,
                extraReferences: DifferentialHarness.EngineTestModelReferences());
            Assert.Equal(dyn, precompiled);
        }

        /// <summary>
        /// The declared set and the corpus on disk agree by name, so a template declared standalone-and-precompiling
        /// but absent — or renamed — is a red build rather than a theory case that quietly stops existing.
        /// </summary>
        [Fact]
        public void EveryDeclaredStandaloneEntryIsInTheCorpus()
        {
            var declared = new HashSet<string>(StandaloneRenderable().Select(row => (string)row[0]),
                StringComparer.Ordinal);
            var present = new HashSet<string>(
                TestCorpusIndex.Load(includeFrontEndErrorFixtures: false).Select(t => Path.GetFileName(t.key)),
                StringComparer.Ordinal);

            Assert.True(declared.Count > 0, "the intent table declares no standalone precompiling entries");
            Assert.Equal(new HashSet<string>(declared.Where(present.Contains), StringComparer.Ordinal), declared);
        }
    }
}
