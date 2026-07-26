using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Heddle.Precompiled;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// Phase 0 WI4 (D4) — the posture carrier. The direct-invoke suites prove the emitter; this suite proves the
    /// <b>tier</b>: every golden-corpus template that precompiles is registered into the process-global registry and
    /// resolved through a real <see cref="Heddle.Runtime.TemplateResolver"/>, so the render crosses
    /// <c>ConsultPrecompiled</c> → <c>TryResolve</c> → the full <c>PrecompiledGauntlet</c> before any byte is
    /// produced — with <see cref="Heddle.Data.PrecompiledMismatchPolicy.Strict"/> on the request and a
    /// <see cref="FallbackGuard"/> around the sweep, so an unintended precompiled→dynamic fallback cannot pass.
    /// <para>The seam this closes is the one the research found unguarded: real generator output never met the
    /// gauntlet in any test, which is how the content-hash (05 F1) and nested/generic AQN (03 F1) drifts shipped.</para>
    /// </summary>
    [Collection("PrecompiledRegistry")]
    public class CorpusResolverSweepTests : PrecompiledRegistryTestBase
    {
        // The model-less corpus templates whose render parity CorpusRenderParityTests already pins on the
        // direct-invoke path — swept here through the resolver instead, so the same bytes are proven to come from
        // the precompiled tier as the resolver serves it.
        private static readonly string[] ModelLessParityTemplates =
        {
            "optimized-document.heddle",
            "ergo-double-render.heddle",
            "ergo-import-library.heddle",
            "ergo-import-composition.heddle",
            "branching-partial-parent.heddle",
            "profile-partial-parent.heddle",
            "profile-flagship.heddle",
            "profile-directive.heddle",
            "regr-def-inner-comment.heddle",
        };

        // Diagnostic-fixture templates carry deliberate front-end errors; excluded so the rest of the corpus
        // generates cleanly (imports still resolve from the remaining set). Same set as CorpusRenderParityTests.
        private static readonly HashSet<string> DiagnosticFixtures = new HashSet<string>(StringComparer.Ordinal)
        {
            "ergo-import-broken.heddle", "import-origin-a.heddle", "import-origin-b.heddle",
            "import-origin-broken.heddle", "import-origin-c.heddle",
        };

        private static string HeddleTestsDll()
        {
            var self = typeof(DifferentialHarness).Assembly.Location;
            var candidate = self.Replace("Heddle.Generator.IntegrationTests", "Heddle.Tests");
            return File.Exists(candidate) ? candidate : null;
        }

        private static string CorpusDir()
        {
            var dll = HeddleTestsDll();
            if (dll == null)
                return null;
            var projDir = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(dll), "..", "..", ".."));
            var corpus = Path.Combine(projDir, "TestTemplate");
            return Directory.Exists(corpus) ? corpus : null;
        }

        private static List<(string key, string content)> LoadCorpus(string dir)
        {
            var list = new List<(string, string)>();
            foreach (var path in Directory.EnumerateFiles(dir, "*.heddle", SearchOption.AllDirectories)
                         .OrderBy(p => p, StringComparer.Ordinal))
            {
                var rel = path.Substring(dir.Length).TrimStart('\\', '/').Replace('\\', '/');
                list.Add((rel, File.ReadAllText(path)));
            }

            return list.Where(t => !DiagnosticFixtures.Contains(Path.GetFileName(t.Item1))).ToList();
        }

        private static IReadOnlyList<MetadataReference> Extra()
        {
            var dll = HeddleTestsDll();
            return dll == null
                ? Array.Empty<MetadataReference>()
                : new[] { MetadataReference.CreateFromFile(dll) };
        }

        private static List<DifferentialHarness.ResolverTarget> Targets(
            IReadOnlyList<(string key, string content)> corpus, Func<string, bool> select) =>
            corpus.Where(t => select(t.key))
                .Select(t => new DifferentialHarness.ResolverTarget(t.key, t.content, typeof(object), null))
                .ToList();

        /// <summary>
        /// Success criterion 2, byte half: every model-less corpus template that <see cref="CorpusRenderParityTests"/>
        /// renders on the direct-invoke path renders byte-identically when the <b>resolver</b> serves it from the
        /// registry — registry-only mode, so a fallback could not even fake the output (the resolver root does not
        /// exist, leaving the dynamic re-compile nothing to read).
        /// </summary>
        [Fact]
        public void ModelLessCorpusTemplatesRenderIdenticallyThroughTheResolver()
        {
            var dir = CorpusDir();
            if (dir == null)
                return; // Heddle.Tests corpus for this TFM not built — the full-solution gate builds it.
            var corpus = LoadCorpus(dir);
            var names = new HashSet<string>(ModelLessParityTemplates, StringComparer.Ordinal);
            var targets = Targets(corpus, k => names.Contains(Path.GetFileName(k)));
            Assert.Equal(ModelLessParityTemplates.Length, targets.Count);

            var swept = DifferentialHarness.SweepViaResolver(corpus, targets, dir, fileBacked: false,
                renderDynamicReference: true, globalOptions: null, extraReferences: Extra());

            foreach (var result in swept)
                Assert.True(result.Dynamic == result.Precompiled,
                    "Resolver-served precompiled output diverged from the dynamic reference for " + result.Key);
        }

        /// <summary>
        /// Success criterion 2, file-backed half (D2's second sub-mode / the mitigation for "false confidence from
        /// registry-only mode"): the same sweep with the corpus staged on disk and
        /// <c>EnableFileChangeCheck</c> on, so <c>PrecompiledGauntlet.CheckStaleness</c>/<c>HashFile</c> runs against
        /// the generator-emitted content hashes for the template <b>and</b> each of its imports. This is the only
        /// path on which content-hash rule drift (05 F1) can ever be caught by a test.
        /// </summary>
        [Fact]
        public void ModelLessCorpusTemplatesRenderIdenticallyThroughTheResolver_FileBacked()
        {
            var dir = CorpusDir();
            if (dir == null)
                return;
            var corpus = LoadCorpus(dir);
            var names = new HashSet<string>(ModelLessParityTemplates, StringComparer.Ordinal);
            var targets = Targets(corpus, k => names.Contains(Path.GetFileName(k)));

            var swept = DifferentialHarness.SweepViaResolver(corpus, targets, dir, fileBacked: true,
                renderDynamicReference: true, globalOptions: null, extraReferences: Extra());

            foreach (var result in swept)
                Assert.True(result.Dynamic == result.Precompiled,
                    "Resolver-served precompiled output diverged from the dynamic reference for " + result.Key);
        }

        /// <summary>
        /// Success criterion 2, coverage half: <b>every</b> corpus entry the manifest reports as precompiled — not
        /// only the model-less parity subset — is resolved and rendered through the gauntlet with zero fallback
        /// events. Byte parity for the model-carrying families stays with their own differential suites (D4); what is
        /// asserted here is the invariant the phase exists for: no corpus template is silently served by the dynamic
        /// tier. The precompiled set is read from the manifest rather than hard-coded, so a template that starts
        /// precompiling joins the sweep automatically.
        /// </summary>
        [Fact]
        public void EveryPrecompiledCorpusEntryCrossesTheGauntlet()
        {
            var dir = CorpusDir();
            if (dir == null)
                return;
            var corpus = LoadCorpus(dir);
            var gen = DifferentialHarness.Generate(corpus, globalOptions: null, extraReferences: Extra());
            Assert.False(gen.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error));

            var precompiledKeys = corpus
                .Where(t => DifferentialHarness.ClassifyInManifest(gen.ManifestSource, t.key) ==
                            DifferentialHarness.ManifestState.Precompiled)
                .Select(t => t.key)
                .ToList();
            Assert.True(precompiledKeys.Count >= 25,
                "Expected the corpus's precompiled set (~34 templates); got " + precompiledKeys.Count);

            var targets = Targets(corpus, k => precompiledKeys.Contains(k));
            // Resolve-only: the gauntlet's verdict lands at TryResolve, before a byte is rendered, and a handful of
            // corpus entries are import fragments (a bare @else continuation) that no tier can render standalone.
            var swept = DifferentialHarness.SweepViaResolver(corpus, targets, dir, fileBacked: false,
                renderDynamicReference: false, render: false, globalOptions: null, extraReferences: Extra());

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
