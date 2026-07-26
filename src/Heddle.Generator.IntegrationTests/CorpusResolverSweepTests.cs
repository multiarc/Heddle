using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Heddle.Precompiled;
using Heddle.TestCorpus;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// The posture carrier. The direct-invoke suites prove the emitter; this suite proves the <b>tier</b>: every
    /// golden-corpus template that precompiles is registered into the process-global registry and resolved through
    /// a real <see cref="Heddle.Runtime.TemplateResolver"/>, so the render crosses <c>ConsultPrecompiled</c> →
    /// <c>TryResolve</c> → the full <c>PrecompiledGauntlet</c> before any byte is produced — with
    /// <see cref="Heddle.Data.PrecompiledMismatchPolicy.Strict"/> on the request and a <see cref="FallbackGuard"/>
    /// around the sweep, so an unintended precompiled→dynamic fallback cannot pass.
    /// <para>The seam this closes is the one the research found unguarded: real generator output never met the
    /// gauntlet in any test, which is how content-hash drifts and nested/generic assembly-qualified-name drifts
    /// shipped.</para>
    /// <para>Three things changed and each deleted a defect rather than covering one. (1) The corpus is
    /// Content-copied into this project's own output, so the assembly-path rewrite and <c>../../..</c> climb —
    /// plus the three "corpus was not found for this TFM" asserts bolted on top of them — are gone. (2) The
    /// byte-compared set is no longer nine hand-listed names: it is every entry the intent table declares
    /// <see cref="CorpusRender.Standalone"/>, which is <b>32</b> of the 40 precompiling entries. (3) The
    /// <c>precompiledKeys.Count == 40</c> literal is set equality against the table.</para>
    /// </summary>
    [Collection("PrecompiledRegistry")]
    public class CorpusResolverSweepTests : PrecompiledRegistryTestBase
    {
        /// <summary>The corpus minus the deliberate front-end-error fixtures, so the rest generates cleanly (imports
        /// still resolve from what remains). Read from the intent table rather than from a fourth hand-copied
        /// <c>HashSet</c> of fixture names.</summary>
        private static List<(string key, string content)> Corpus() =>
            TestCorpusIndex.Load(includeFrontEndErrorFixtures: false);

        /// <summary>
        /// The entries the sweep byte-compares: declared <see cref="CorpusRender.Standalone"/> <b>and</b> declared to
        /// precompile. Standalone means "model-less, and both backends render it" — measured, not assumed.
        /// <para>This replaces a nine-name hand-list. The nine were not wrong, they were just the ones somebody had
        /// got around to; nothing said the other twenty-three were unchecked, and nothing would have said so if a
        /// tenth had quietly stopped rendering.</para>
        /// </summary>
        private static IReadOnlyList<string> StandaloneRenderable() =>
            CorpusIntent.Rows
                .Where(r => r.Tier == CorpusTier.Precompiles && r.Render == CorpusRender.Standalone)
                .Select(r => r.Name)
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToList();

        private static List<DifferentialHarness.ResolverTarget> Targets(
            IReadOnlyList<(string key, string content)> corpus, Func<string, bool> select, bool render = true) =>
            corpus.Where(t => select(t.key))
                .Select(t => new DifferentialHarness.ResolverTarget(t.key, t.content, typeof(object), null, render))
                .ToList();

        /// <summary>
        /// Success criterion 2, byte half: every corpus template declared <c>Standalone</c> renders byte-identically
        /// when the <b>resolver</b> serves it from the registry — registry-only mode, so a fallback could not even
        /// fake the output (the resolver root does not exist, leaving the dynamic re-compile nothing to read).
        /// </summary>
        [Fact]
        public void ModelLessCorpusTemplatesRenderIdenticallyThroughTheResolver()
        {
            var corpus = Corpus();
            var names = new HashSet<string>(StandaloneRenderable(), StringComparer.Ordinal);
            var targets = Targets(corpus, k => names.Contains(Path.GetFileName(k)));
            Assert.Equal(names.Count, targets.Count);

            var swept = DifferentialHarness.SweepViaResolver(corpus, targets, TestCorpusIndex.CorpusDir,
                fileBacked: false, renderDynamicReference: true, globalOptions: null,
                extraReferences: DifferentialHarness.EngineTestModelReferences());

            foreach (var result in swept)
                Assert.True(result.Dynamic == result.Precompiled,
                    "Resolver-served precompiled output diverged from the dynamic reference for " + result.Key);
        }

        /// <summary>
        /// Success criterion 2, file-backed half (the mitigation for "false confidence from registry-only mode"):
        /// the same sweep with the corpus staged on disk and <c>EnableFileChangeCheck</c> on, so
        /// <c>PrecompiledGauntlet.CheckStaleness</c>/<c>HashFile</c> runs against the generator-emitted content hashes
        /// for the template <b>and</b> each of its imports. This is the only path on which content-hash rule drift
        /// can ever be caught by a test.
        /// <para>The staged tree now reproduces each entry's <b>declared encoding</b>, so BOM-bearing entries reach
        /// <c>HashFile</c> as BOM-bearing files. Until then <c>StageCorpus</c> wrote everything BOM-free, which meant
        /// the one sub-mode written to exercise <c>HashFile</c>'s decode-then-hash BOM path never once staged a BOM.
        /// <see cref="AtLeastOneStandaloneEntryCarriesADeclaredBom"/> keeps that true.</para>
        /// </summary>
        [Fact]
        public void ModelLessCorpusTemplatesRenderIdenticallyThroughTheResolver_FileBacked()
        {
            var corpus = Corpus();
            var names = new HashSet<string>(StandaloneRenderable(), StringComparer.Ordinal);
            var targets = Targets(corpus, k => names.Contains(Path.GetFileName(k)));

            var swept = DifferentialHarness.SweepViaResolver(corpus, targets, TestCorpusIndex.CorpusDir,
                fileBacked: true, renderDynamicReference: true, globalOptions: null,
                extraReferences: DifferentialHarness.EngineTestModelReferences());

            foreach (var result in swept)
                Assert.True(result.Dynamic == result.Precompiled,
                    "Resolver-served precompiled output diverged from the dynamic reference for " + result.Key);
        }

        /// <summary>
        /// The file-backed pass above is only evidence if a BOM'd entry is actually in it. Making that a standing
        /// assertion rather than a fact someone once checked: at least one entry that the file-backed sweep stages
        /// and crosses <c>HashFile</c> with is declared <c>Bom = true</c>.
        /// </summary>
        [Fact]
        public void AtLeastOneStandaloneEntryCarriesADeclaredBom()
        {
            var swept = new HashSet<string>(StandaloneRenderable(), StringComparer.Ordinal);
            var bomInSweep = CorpusIntent.BomNames().Where(swept.Contains).ToList();
            Assert.True(bomInSweep.Count > 0,
                "No BOM-bearing corpus entry is in the file-backed sweep, so PrecompiledGauntlet.HashFile's " +
                "decode-then-hash BOM path — the shape phase 5's F1 fix was written for — is not exercised by any " +
                "test. Declared Bom entries: " + string.Join(", ", CorpusIntent.BomNames()));
        }

        /// <summary>
        /// Success criterion 2, coverage half: <b>every</b> corpus entry the manifest reports as precompiled is
        /// resolved through the gauntlet with zero fallback events, and the observed precompiled set is pinned by
        /// <b>set equality against the intent table</b>, reported as a symmetric difference naming the drifting files.
        /// <para>An exact count is rubber-stampable — a change of classification is made green by editing one digit,
        /// and the commit looks the same either way. Set equality cannot be: making it green requires naming the file
        /// whose classification changed and writing why in its row. It is also a strictly better message:
        /// "props-inherit.heddle stopped precompiling" rather than "expected 40, got 39".</para>
        /// <para>The sweep is symmetric on purpose. A deliberate-degrade template that quietly <i>starts</i>
        /// precompiling reddens this gate exactly as loudly as one that stops — which is the mitigation for the
        /// migration risk that a <c>DegradesToMarker</c> shape silently joins the precompiled set.</para>
        /// </summary>
        [Fact]
        public void EveryPrecompiledCorpusEntryCrossesTheGauntlet()
        {
            CorpusDifferentialTests.AssertIntentIsTotal();

            var corpus = Corpus();
            var extra = DifferentialHarness.EngineTestModelReferences();
            var gen = DifferentialHarness.Generate(corpus, globalOptions: null, extraReferences: extra);
            Assert.False(gen.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error));

            var precompiledKeys = corpus
                .Where(t => DifferentialHarness.ClassifyInManifest(gen.ManifestSource, t.key) ==
                            DifferentialHarness.ManifestState.Precompiled)
                .Select(t => t.key)
                .ToList();

            var declared = CorpusIntent.NamesWithTier(CorpusTier.Precompiles);
            var observed = precompiledKeys.Select(Path.GetFileName).ToList();
            Assert.True(new HashSet<string>(observed, StringComparer.Ordinal).SetEquals(declared),
                CorpusIntent.Describe("The precompiled set", declared, observed));

            // Per-entry render flag: Declared-Standalone entries are rendered; the rest resolve only — which still
            // proves they crossed the gauntlet, because the gauntlet's verdict lands at TryResolve, before a byte is
            // produced. That is what a bare @else continuation, which no tier can render standalone, actually needs.
            var standalone = new HashSet<string>(StandaloneRenderable(), StringComparer.Ordinal);
            var keys = new HashSet<string>(precompiledKeys, StringComparer.Ordinal);
            var targets = corpus.Where(t => keys.Contains(t.key))
                .Select(t => new DifferentialHarness.ResolverTarget(t.key, t.content, typeof(object), null,
                    render: standalone.Contains(Path.GetFileName(t.key))))
                .ToList();

            var swept = DifferentialHarness.SweepViaResolver(corpus, targets, TestCorpusIndex.CorpusDir,
                fileBacked: false, renderDynamicReference: false, globalOptions: null, extraReferences: extra);

            Assert.Equal(precompiledKeys.Count, swept.Count);
            // Every entry was served by the precompiled adapter (SweepViaResolver asserts that per target) and the
            // sweep-wide FallbackGuard saw no undeclared event (it verifies before returning). Re-assert the registry
            // really carried them, so a future refactor cannot make this test vacuous.
            foreach (var key in precompiledKeys)
            {
                Assert.True(PrecompiledTemplates.TryGet(key, out var entry), "Not registered: " + key);
                Assert.True(entry.IsPrecompiled, "Marker entry swept as precompiled: " + key);
            }
        }
    }
}
