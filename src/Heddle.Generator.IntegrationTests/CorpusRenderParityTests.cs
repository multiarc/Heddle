using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Heddle.Exceptions;
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
    /// <para>Both halves of the render column are measured, not taken on trust. Reading the table for the set to
    /// render only moves the drift one column left: an entry declared <c>ResolveOnly</c> leaves the gate on its own
    /// say-so, and seven entries declared <c>WithModel</c> sat outside it while rendering identically all along. So
    /// everything not declared <c>ResolveOnly</c> is rendered, and everything declared <c>ResolveOnly</c> is
    /// rendered too — to prove it cannot be.</para>
    /// </summary>
    public class CorpusRenderParityTests
    {
        /// <summary>Precompiling entries the intent table says a shared harness may render without a model —
        /// everything except <see cref="CorpusRender.ResolveOnly"/>.</summary>
        public static IEnumerable<object[]> ModelLessRenderable() =>
            NamesWithRender(r => r != CorpusRender.ResolveOnly).Select(n => new object[] { n });

        /// <summary>Precompiling entries the table excuses from rendering altogether. Empty today — see
        /// <see cref="NoPrecompilingEntryIsCurrentlyExcusedFromRendering"/>.</summary>
        public static IEnumerable<string> DeclaredResolveOnly() =>
            NamesWithRender(r => r == CorpusRender.ResolveOnly);

        private static IEnumerable<string> NamesWithRender(Func<CorpusRender, bool> predicate) =>
            CorpusIntent.Rows
                .Where(r => r.Tier == CorpusTier.Precompiles && predicate(r.Render))
                .Select(r => r.Name)
                .OrderBy(n => n, StringComparer.Ordinal);

        [Theory]
        [MemberData(nameof(ModelLessRenderable))]
        public void ModelLessCorpusTemplateRendersIdentically(string name)
        {
            var (precompiled, dyn) = RenderModelLess(name);
            Assert.Equal(dyn, precompiled);
        }

        /// <summary>
        /// <see cref="CorpusRender.ResolveOnly"/> is the value that removes an entry from byte-parity coverage, so
        /// it is the one that has to be earned rather than declared. Rendering the entry anyway is the only way to
        /// find out: a row that reads <c>ResolveOnly</c> for a template that renders perfectly well has taken it out
        /// of the gate for nothing, and the column drifts one silent row at a time.
        /// </summary>
        /// <para>The column is empty today, and that is a property worth asserting rather than a reason to delete
        /// the check. A template the engine refuses to render is now also one the build tier declines to precompile,
        /// so the two values cannot currently co-occur. They are not mutually exclusive in principle — a template can
        /// precompile and still refuse at render for a model reason rather than a branch one — so the day a row does
        /// appear, the count below reddens and the loop that follows becomes the real gate for it.</para>
        [Fact]
        public void AnEntryDeclaredResolveOnlyGenuinelyDoesNotRender()
        {
            var declared = DeclaredResolveOnly().ToList();

            // The cardinality is the guard. Without it the loop is the whole test, and a loop over an empty
            // self-derived set passes without executing its body — the assertion would be describing nothing.
            Assert.Empty(declared);

            foreach (var name in declared)
            {
                var refusal = Assert.ThrowsAny<Exception>(() => RenderModelLess(name));

                // Not merely "something threw". A harness failure — a missing fixture, a build-time degrade where
                // one was not declared — throws too, and would let a row keep its exemption for a reason that has
                // nothing to do with the template. What earns the exemption is the engine itself refusing to render.
                Assert.IsType<TemplateProcessingException>(refusal);
            }
        }

        /// <summary>
        /// The declared set and the corpus on disk agree by name, so a template declared precompiling but absent —
        /// or renamed — is a red build rather than a theory case that quietly stops existing.
        /// </summary>
        [Fact]
        public void EveryDeclaredPrecompilingEntryIsInTheCorpus()
        {
            var declared = new HashSet<string>(NamesWithRender(_ => true), StringComparer.Ordinal);
            var present = new HashSet<string>(
                TestCorpusIndex.Load(includeFrontEndErrorFixtures: false).Select(t => Path.GetFileName(t.key)),
                StringComparer.Ordinal);

            Assert.True(declared.Count > 0, "the intent table declares no precompiling entries");
            Assert.Equal(new HashSet<string>(declared.Where(present.Contains), StringComparer.Ordinal), declared);
        }

        private static (string precompiled, string dynamic) RenderModelLess(string name)
        {
            // The corpus is in THIS project's own output directory (TestCorpus.props), so locating it is
            // AppContext.BaseDirectory and nothing else.
            var dir = TestCorpusIndex.CorpusDir;
            // FrontEndError entries carry deliberate parse errors; excluded so the rest of the corpus generates
            // cleanly (imports still resolve from what remains). The set is read from the intent table.
            var corpus = TestCorpusIndex.Load(includeFrontEndErrorFixtures: false);
            var target = corpus.FirstOrDefault(t => Path.GetFileName(t.key) == name);
            Assert.False(target.content == null, "Corpus template not found: " + name);

            return DifferentialHarness.RenderInCorpus(
                corpus, target.key, target.content, typeof(object), null, dir,
                extraReferences: DifferentialHarness.EngineTestModelReferences());
        }
    }
}
