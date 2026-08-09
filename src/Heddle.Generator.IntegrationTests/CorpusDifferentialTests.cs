using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Heddle.TestCorpus;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// Whole-corpus gate: pins whether each template precompiles, degrades to HED7014 marker, falls back safely,
    /// or is a front-end error fixture. Generated .g.cs must compile (compile-safety). Classification via
    /// <see cref="CorpusIntent"/>, asserted as set equality (symmetric difference) to prevent silent drifts.
    /// </summary>
    public class CorpusDifferentialTests
    {
        [Fact]
        public void CorpusClassificationIsPinnedAndPrecompiledCodeCompiles()
        {
            var templates = TestCorpusIndex.Load();
            AssertIntentIsTotal();
            var extra = DifferentialHarness.EngineTestModelReferences();

            // First pass: find templates with front-end errors (so @<< imports resolve).
            var all = DifferentialHarness.Generate(templates, globalOptions: null, extraReferences: extra);
            var frontEndErrors = new HashSet<string>(all.Diagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .Select(d => Path.GetFileName(d.Location.GetLineSpan().Path))
                .Where(p => !string.IsNullOrEmpty(p)), StringComparer.Ordinal);

            var declaredFrontEndErrors = CorpusIntent.NamesWithTier(CorpusTier.FrontEndError);
            Assert.True(frontEndErrors.SetEquals(declaredFrontEndErrors),
                CorpusIntent.Describe("The FrontEndError set", declaredFrontEndErrors, frontEndErrors));

            // Second pass: generated code must compile (compile-safety).
            var clean = templates.Where(t => !frontEndErrors.Contains(Path.GetFileName(t.key))).ToList();
            var cleanGen = DifferentialHarness.Generate(clean, globalOptions: null, extraReferences: extra);
            Assert.False(cleanGen.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error),
                "Unexpected generator errors over the non-fixture corpus.");
            Assert.NotNull(cleanGen.Assembly);   // Generate throws if the generated .g.cs fails to compile.

            var precompiled = new SortedSet<string>(StringComparer.Ordinal);
            var markers = new SortedSet<string>(StringComparer.Ordinal);
            var fallbacks = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var (key, _) in clean)
            {
                var name = Path.GetFileName(key);
                switch (DifferentialHarness.ClassifyInManifest(cleanGen.ManifestSource ?? string.Empty, key))
                {
                    case DifferentialHarness.ManifestState.Precompiled: precompiled.Add(name); break;
                    case DifferentialHarness.ManifestState.Marker: markers.Add(name); break;
                    default: fallbacks.Add(name); break;
                }
            }

            var declaredPrecompiled = CorpusIntent.NamesWithTier(CorpusTier.Precompiles);
            Assert.True(precompiled.SetEquals(declaredPrecompiled),
                CorpusIntent.Describe("The precompiled set", declaredPrecompiled, precompiled));

            var declaredMarkers = CorpusIntent.NamesWithTier(CorpusTier.DegradesToMarker);
            Assert.True(markers.SetEquals(declaredMarkers),
                CorpusIntent.Describe("The HED7014 marker set", declaredMarkers, markers));

            var declaredFallbacks = CorpusIntent.NamesWithTier(CorpusTier.FallsBackSafely);
            Assert.True(fallbacks.SetEquals(declaredFallbacks),
                CorpusIntent.Describe("The safe-fallback set", declaredFallbacks, fallbacks));
        }

        /// <summary>
        /// Two bidirectional completeness gates: no template without a row, no row without a template. Neither
        /// can drift silently — contributing a template requires declaring its purpose.
        /// </summary>
        internal static void AssertIntentIsTotal()
        {
            var onDisk = TestCorpusIndex.Names();
            var declared = CorpusIntent.DeclaredNames();
            Assert.True(new HashSet<string>(onDisk, StringComparer.Ordinal).SetEquals(declared),
                CorpusIntent.Describe("The corpus intent table", declared, onDisk));

            var blank = CorpusIntent.Rows.Where(r => string.IsNullOrWhiteSpace(r.Why)).Select(r => r.Name).ToList();
            Assert.True(blank.Count == 0,
                "Every corpus intent row needs a non-empty Why — a row without a reason is a rubber stamp. Missing: "
                + string.Join(", ", blank));
        }
    }
}
