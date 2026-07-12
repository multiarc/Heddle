using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// Phase 7 WI9 (D20) — the whole-corpus gate over <c>src/Heddle.Tests/TestTemplate/**</c>. Runs the generator
    /// across the entire real corpus (imports resolved from the same set) with the engine test models referenced and
    /// pins, per template, whether it <b>precompiles</b> (a manifest entry with a strategy + compilable generated
    /// code), degrades to a <b>HED7014 marker</b>, safely <b>falls back</b> to the dynamic path, or is a
    /// <b>diagnostic fixture</b> whose front-end error the generator forwards (the import-origin / *-broken fixtures).
    /// The classification is pinned so a regression in either direction is caught, and every non-fixture template's
    /// generated <c>.g.cs</c> is required to compile (compile-safety). Byte-for-byte render parity for the supported
    /// families is covered by the family-specific differential suites with representative models.
    /// </summary>
    public class CorpusDifferentialTests
    {
        // Templates that precompile today (an entry class + compilable generated code). Plain custom/engine
        // extensions ([ExtensionName] types with no InitStart/CompleteInit override — e.g. @raw) now bind directly
        // (D9). Every other non-fixture template falls back safely to the dynamic path (unsupported construct:
        // FullCSharp params, bodied/hook-overriding custom extensions, definition layering/default-output,
        // multi-hop/inherited props, or a Heddle.Tests model member the emitter cannot type — each output-safe).
        private static readonly HashSet<string> ExpectedPrecompiled = new HashSet<string>(StringComparer.Ordinal)
        {
            "branch-import-def.heddle",
            "branch-import-else.heddle",
            "branching-flagship.heddle",
            "branching-interleaved.heddle",
            "branching-list-alternating.heddle",
            "branching-nested.heddle",
            "branching-partial-child.heddle",
            "branching-partial-parent.heddle",
            "ergo-double-render.heddle",
            "ergo-for.heddle",
            "ergo-import-composition.heddle",
            "ergo-import-empty.heddle",
            "ergo-import-library.heddle",
            "ergo-trim-preamble.heddle",
            "import-origin-badmember-lib.heddle",
            "optimized-document.heddle",
            "profile-directive.heddle",
            "profile-flagship.heddle",
            "profile-partial-child.heddle",
            "profile-partial-parent.heddle",
            "profile-resolver-default.heddle",
            "props-abstract-panel.heddle",
            // Compose-nesting regression shim: a standalone top-level @<< compose fixture whose top-level @<<
            // composes the import library. As its own top-level document it precompiles (offset 0).
            "regr-compose-shim.heddle",
            // Hidden-token offset regression fixtures: a single-file definition with an inner-comment body and the
            // import library it is paired with both precompile (no definition layering). The cross-file override
            // page (regr-import-multiline-override) layers an imported definition and so falls back to the dynamic
            // path — pinned by its runtime golden, not here.
            "regr-def-inner-comment.heddle",
            "regr-import-shell.heddle",
            "streaming-large.heddle",     // phase 8 fixture: pure static → precompiles (dynamic tier)
            "streaming-unicode.heddle",   // phase 8 fixture: static + dynamic @(Name)/@(City) → precompiles
        };

        // Diagnostic-testing fixtures whose front-end error the shared parser reports (the generator forwards it as a
        // build error, exactly as the dynamic backend would reject them). Not precompiled and not a silent fallback.
        private static readonly HashSet<string> ExpectedDiagnosticFixtures = new HashSet<string>(StringComparer.Ordinal)
        {
            "ergo-import-broken.heddle",
            "import-origin-a.heddle",
            "import-origin-b.heddle",
            "import-origin-broken.heddle",
            "import-origin-c.heddle",
        };

        private static string CorpusDir()
        {
            var dll = HeddleTestsDll();
            if (dll == null)
                return null;
            var tfmDir = Path.GetDirectoryName(dll);                 // bin/<cfg>/<tfm>
            var projDir = Path.GetFullPath(Path.Combine(tfmDir, "..", "..", ".."));
            var corpus = Path.Combine(projDir, "TestTemplate");
            return Directory.Exists(corpus) ? corpus : null;
        }

        private static string HeddleTestsDll()
        {
            var self = typeof(DifferentialHarness).Assembly.Location;
            var candidate = self.Replace("Heddle.Generator.IntegrationTests", "Heddle.Tests");
            return File.Exists(candidate) ? candidate : null;
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

            return list;
        }

        private static string Classify(string manifest, string key)
        {
            var marker = "key: \"" + key + "\"";
            var at = manifest.IndexOf(marker, StringComparison.Ordinal);
            if (at < 0)
                return "FALLBACK";
            var next = manifest.IndexOf("key: \"", at + marker.Length, StringComparison.Ordinal);
            var block = next < 0 ? manifest.Substring(at) : manifest.Substring(at, next - at);
            return block.Contains("strategy: null") ? "MARKER" : "PRECOMPILED";
        }

        [Fact]
        public void CorpusClassificationIsPinnedAndPrecompiledCodeCompiles()
        {
            var dir = CorpusDir();
            if (dir == null)
                return; // Heddle.Tests.dll for this TFM is not built — the full-solution gate builds it (WI9 requires it).
            var templates = LoadCorpus(dir);
            Assert.True(templates.Count >= 40, "Expected the full TestTemplate corpus (~45 files).");
            var extra = new[] { MetadataReference.CreateFromFile(HeddleTestsDll()) };

            // First pass over the whole corpus (so @<< imports resolve): find the diagnostic-fixture templates whose
            // front-end error the generator forwards, attributed to their file by the diagnostic location.
            var all = DifferentialHarness.Generate(templates, globalOptions: null, extraReferences: extra);
            var fixtures = new HashSet<string>(all.Diagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .Select(d => Path.GetFileName(d.Location.GetLineSpan().Path))
                .Where(p => !string.IsNullOrEmpty(p)), StringComparer.Ordinal);

            Assert.True(ExpectedDiagnosticFixtures.SetEquals(fixtures),
                "Diagnostic-fixture set changed. Expected: [" + string.Join(", ", ExpectedDiagnosticFixtures.OrderBy(x => x)) +
                "] Actual: [" + string.Join(", ", fixtures.OrderBy(x => x)) + "]");

            // Second pass over the non-fixture templates: no forwarded errors, so the generated code must all compile
            // together (compile-safety for every precompiled corpus template).
            var clean = templates.Where(t => !fixtures.Contains(Path.GetFileName(t.key))).ToList();
            var cleanGen = DifferentialHarness.Generate(clean, globalOptions: null, extraReferences: extra);
            Assert.False(cleanGen.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error),
                "Unexpected generator errors over the non-fixture corpus.");
            Assert.NotNull(cleanGen.Assembly);   // Generate throws if the generated .g.cs fails to compile.

            // Classify each non-fixture template and pin the precompile/fallback split.
            var precompiled = new SortedSet<string>(StringComparer.Ordinal);
            var markers = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var (key, _) in clean)
            {
                var state = Classify(cleanGen.ManifestSource ?? string.Empty, key);
                var name = Path.GetFileName(key);
                if (state == "PRECOMPILED")
                    precompiled.Add(name);
                else if (state == "MARKER")
                    markers.Add(name);
            }

            Assert.True(ExpectedPrecompiled.SetEquals(precompiled),
                "Precompiled set changed. Expected: [" + string.Join(", ", ExpectedPrecompiled.OrderBy(x => x)) +
                "] Actual: [" + string.Join(", ", precompiled) + "]");
        }
    }
}
