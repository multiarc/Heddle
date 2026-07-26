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
    /// The whole-corpus gate over the shared corpus (<c>src/Heddle.Tests/TestTemplate/**</c>, build-copied into this
    /// project's output by <c>src/TestCorpus/TestCorpus.props</c>). Runs the generator across the entire real corpus
    /// (imports resolved from the same set) with the engine test models referenced and pins, per template, whether it
    /// <b>precompiles</b>, degrades to a <b>HED7014 marker</b>, safely <b>falls back</b> to the dynamic path, or is a
    /// <b>front-end error fixture</b> whose error the generator forwards. Every non-fixture template's generated
    /// <c>.g.cs</c> is required to compile (compile-safety). Byte-for-byte render parity for the supported families is
    /// covered by the family-specific differential suites with representative models.
    /// <para>The classification is no longer two hand-maintained <c>HashSet</c>s and a corpus file count in this file.
    /// It is read from <see cref="CorpusIntent"/>, and every pin is <b>set equality reported as a symmetric
    /// difference</b>. Both replaced pins were counts, and both had already gone wrong in this tree: a <c>&gt;= 25</c>
    /// floor sat against an actual 40 (fifteen templates could stop precompiling in silence), and the <c>&gt;= 40</c>
    /// floor that replaced it here sat against an actual 62 beside a comment claiming "~45". A count is
    /// rubber-stampable — a stage that changes a classification is made green by editing one digit, and the commit
    /// looks identical either way. Set equality cannot be: making it green requires naming the file that moved and
    /// writing down why, in its intent row.</para>
    /// </summary>
    public class CorpusDifferentialTests
    {
        [Fact]
        public void CorpusClassificationIsPinnedAndPrecompiledCodeCompiles()
        {
            // No assembly-path rewrite, no `../../..` climb out of bin/<cfg>/<tfm>, and therefore no
            // "the corpus was not found for this TFM" assert to bolt on top of one. The corpus is Content-copied
            // into this project's own output; a miss throws from the accessor with the path in the message.
            var templates = TestCorpusIndex.Load();

            // Every corpus file has exactly one intent row and vice versa. Asserted here as well as in the engine
            // tier's gate because this suite would otherwise happily classify a template nobody declared.
            AssertIntentIsTotal();

            var extra = DifferentialHarness.EngineTestModelReferences();

            // First pass over the whole corpus (so @<< imports resolve): find the templates whose front-end error the
            // generator forwards, attributed to their file by the diagnostic location.
            var all = DifferentialHarness.Generate(templates, globalOptions: null, extraReferences: extra);
            var frontEndErrors = new HashSet<string>(all.Diagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .Select(d => Path.GetFileName(d.Location.GetLineSpan().Path))
                .Where(p => !string.IsNullOrEmpty(p)), StringComparer.Ordinal);

            var declaredFrontEndErrors = CorpusIntent.NamesWithTier(CorpusTier.FrontEndError);
            Assert.True(frontEndErrors.SetEquals(declaredFrontEndErrors),
                CorpusIntent.Describe("The FrontEndError set", declaredFrontEndErrors, frontEndErrors));

            // Second pass over the rest: no forwarded errors, so the generated code must all compile together
            // (compile-safety for every precompiled corpus template).
            var clean = templates.Where(t => !frontEndErrors.Contains(Path.GetFileName(t.key))).ToList();
            var cleanGen = DifferentialHarness.Generate(clean, globalOptions: null, extraReferences: extra);
            Assert.False(cleanGen.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error),
                "Unexpected generator errors over the non-fixture corpus.");
            Assert.NotNull(cleanGen.Assembly);   // Generate throws if the generated .g.cs fails to compile.

            // Classify each remaining template into the three build-tier buckets.
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

            // Asserted POSITIVELY, not by exclusion, and this is the point of the symmetric difference: a template
            // JOINING the precompiled set reddens the gate exactly as loudly as one leaving it. Before phase 7 this
            // bucket was computed into a SortedSet and then never asserted at all — a dead computation. It is empty
            // today (the phase-7 re-check found every non-precompiling corpus entry is ABSENT from the manifest, not
            // a marker), and an empty set is still a pinned set: the first template that starts emitting a HED7014
            // marker must redden something rather than pass unnoticed.
            var declaredMarkers = CorpusIntent.NamesWithTier(CorpusTier.DegradesToMarker);
            Assert.True(markers.SetEquals(declaredMarkers),
                CorpusIntent.Describe("The HED7014 marker set", declaredMarkers, markers));

            var declaredFallbacks = CorpusIntent.NamesWithTier(CorpusTier.FallsBackSafely);
            Assert.True(fallbacks.SetEquals(declaredFallbacks),
                CorpusIntent.Describe("The safe-fallback set", declaredFallbacks, fallbacks));
        }

        /// <summary>
        /// Two bidirectional completeness gates. Neither direction can drift silently: a template added without a row
        /// fails naming the template, and a row outliving its template fails naming the row. Contributing a template
        /// REQUIRES declaring what it is for.
        /// </summary>
        internal static void AssertIntentIsTotal()
        {
            var onDisk = TestCorpusIndex.Names();
            var declared = CorpusIntent.DeclaredNames();
            Assert.True(new HashSet<string>(onDisk, StringComparer.Ordinal).SetEquals(declared),
                CorpusIntent.Describe("The corpus intent table", declared, onDisk));

            // The one surviving literal (D5). Deliberate, and not a floor: it makes "this stage added N entries" a
            // one-line reviewable diff a reviewer can check against the stage's stated scope, so a stage cannot
            // smuggle extra templates in alongside the ones it claims.
            Assert.Equal(CorpusIntent.DeclaredRowCount, CorpusIntent.Rows.Count);

            var blank = CorpusIntent.Rows.Where(r => string.IsNullOrWhiteSpace(r.Why)).Select(r => r.Name).ToList();
            Assert.True(blank.Count == 0,
                "Every corpus intent row needs a non-empty Why — a row without a reason is a rubber stamp. Missing: "
                + string.Join(", ", blank));
        }
    }
}
