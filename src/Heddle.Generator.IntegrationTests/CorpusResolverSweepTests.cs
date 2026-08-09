using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Heddle.Data;
using Heddle.Precompiled;
using Heddle.Runtime.Expressions;
using Heddle.TestCorpus;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// Integration suite proving every precompiled corpus template resolves through the PrecompiledGauntlet.
    /// Every entry crosses gauntlet validation with Strict mismatch policy and FallbackGuard protecting against
    /// unintended precompiled→dynamic fallback.
    /// </summary>
    [Collection("PrecompiledRegistry")]
    public class CorpusResolverSweepTests : PrecompiledRegistryTestBase
    {
        /// <summary>
        /// The host registrations the corpus's late-bound entries name. <c>fn-late-bound.heddle</c> calls a
        /// function no metadata carries, so it precompiles to a site that binds at first render — and the gauntlet
        /// deliberately moves the request to the dynamic tier where the live registry cannot serve the name. A
        /// deployment that registers it is the one the entry is built for, and it is the only configuration under
        /// which "every precompiled entry crosses the gauntlet" is a claim about the entry rather than about the
        /// harness's own empty registry.
        /// </summary>
        private static void RegisterCorpusHostFunctions(TemplateOptions options)
        {
            var registry = new FunctionRegistry();
            registry.Register("mystery", new Func<string, string>(value => "[" + value + "]"));
            options.Functions = registry;
        }

        /// <summary>The corpus minus front-end-error fixtures; read from the intent table.</summary>
        private static List<(string key, string content)> Corpus() =>
            TestCorpusIndex.Load(includeFrontEndErrorFixtures: false);

        /// <summary>Entries declared as Standalone (model-less, both backends render) and precompile.</summary>
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

        /// <summary>Standalone templates render identically when served by the resolver in registry-only mode.</summary>
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

            // What the loop below actually iterates. The count above pins the targets going in, which is not
            // the same claim: a sweep that returned fewer results than it was given would still leave every
            // assertion that ran passing, on a quietly smaller set.
            Assert.Equal(targets.Count, swept.Count);

            foreach (var result in swept)
                Assert.True(result.Dynamic == result.Precompiled,
                    "Resolver-served precompiled output diverged from the dynamic reference for " + result.Key);
        }

        /// <summary>Standalone templates render identically through the resolver with file-backed disk staging and hash checks.</summary>
        [Fact]
        public void ModelLessCorpusTemplatesRenderIdenticallyThroughTheResolver_FileBacked()
        {
            var corpus = Corpus();
            var names = new HashSet<string>(StandaloneRenderable(), StringComparer.Ordinal);
            var targets = Targets(corpus, k => names.Contains(Path.GetFileName(k)));
            Assert.Equal(names.Count, targets.Count);

            var swept = DifferentialHarness.SweepViaResolver(corpus, targets, TestCorpusIndex.CorpusDir,
                fileBacked: true, renderDynamicReference: true, globalOptions: null,
                extraReferences: DifferentialHarness.EngineTestModelReferences());

            // As above. This test had neither count, so emptying the target set left it passing without
            // rendering anything at all — its twin caught the same mutation on the first line of the body.
            Assert.Equal(targets.Count, swept.Count);

            foreach (var result in swept)
                Assert.True(result.Dynamic == result.Precompiled,
                    "Resolver-served precompiled output diverged from the dynamic reference for " + result.Key);
        }

        /// <summary>At least one Standalone entry carries a declared BOM to exercise HashFile's decode-then-hash path.</summary>
        [Fact]
        public void AtLeastOneStandaloneEntryCarriesADeclaredBom()
        {
            var swept = new HashSet<string>(StandaloneRenderable(), StringComparer.Ordinal);
            var bomInSweep = CorpusIntent.BomNames().Where(swept.Contains).ToList();
            Assert.True(bomInSweep.Count > 0,
                "No BOM-bearing corpus entry is in the file-backed sweep, so PrecompiledGauntlet.HashFile's " +
                "decode-then-hash BOM path is not exercised by any " +
                "test. Declared Bom entries: " + string.Join(", ", CorpusIntent.BomNames()));
        }

        /// <summary>
        /// Every precompiled corpus entry crosses the gauntlet with zero fallback; set equality vs. intent table
        /// reports file-by-file drift rather than edit-one-digit counts.
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

            var standalone = new HashSet<string>(StandaloneRenderable(), StringComparer.Ordinal);
            var keys = new HashSet<string>(precompiledKeys, StringComparer.Ordinal);
            var targets = corpus.Where(t => keys.Contains(t.key))
                .Select(t => new DifferentialHarness.ResolverTarget(t.key, t.content, typeof(object), null,
                    render: standalone.Contains(Path.GetFileName(t.key))))
                .ToList();

            var swept = DifferentialHarness.SweepViaResolver(corpus, targets, TestCorpusIndex.CorpusDir,
                fileBacked: false, renderDynamicReference: false, globalOptions: null, extraReferences: extra,
                configureOptions: RegisterCorpusHostFunctions);

            Assert.Equal(precompiledKeys.Count, swept.Count);
            foreach (var key in precompiledKeys)
            {
                Assert.True(PrecompiledTemplates.TryGet(key, out var entry), "Not registered: " + key);
                Assert.True(entry.IsPrecompiled, "Marker entry swept as precompiled: " + key);
            }
        }
    }
}
