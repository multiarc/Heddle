using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Heddle.Benchmarks.Dotnet.Engines;
using Heddle.Benchmarks.Dotnet.Gate;

namespace Heddle.Benchmarks.Dotnet.Corpus
{
    /// <summary>
    /// The two corpus verbs — <c>export-corpus</c> and <c>verify-corpus</c> — that own the committed
    /// oracle under <c>benchmarks/dotnet/GoldenCorpus/</c> (normative format in
    /// docs/spec/cross-stack-benchmarks/phase-1-cross-stack-foundation/golden-corpus.md).
    ///
    /// <para><b>export-corpus</b> renders each workload's Heddle oracle live, applies the contract's
    /// stored-form pipeline (N1–N5; the N3b whitespace strip is a comparison-time projection and is
    /// deliberately never baked in), and writes <c>&lt;id&gt;.golden.html</c>, one
    /// <c>&lt;id&gt;.verify.json</c> per workload, and <c>manifest.json</c>. It refuses a dirty
    /// working tree unless told otherwise, because the manifest stamps the commit the bytes came
    /// from and a stamp that names no reachable tree is worse than no stamp.</para>
    ///
    /// <para><b>verify-corpus</b> proves two independent things. Freshness: a live render still
    /// equals the committed bytes exactly, and the manifest's hash and length still describe the file
    /// on disk. Calibration: the verifier accepts the golden and <em>rejects</em> each synthesized
    /// corruption, by the check kind that corruption is supposed to trip. The second half is what
    /// stops a verifier that has quietly stopped discriminating from passing every idiomatic cell in
    /// six ecosystems.</para>
    ///
    /// <para>The oracle is Heddle's own controlled-track render. This is the one place in the harness
    /// where that is not circular: every other engine is gated <em>against</em> the corpus, and
    /// Heddle's agreement with it is re-proved here from source on every run.</para>
    /// </summary>
    public static class CorpusMaintenance
    {
        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        /// <summary>The name stamped into <c>manifest.json</c>'s <c>generator</c> field.</summary>
        private const string GeneratorName = "benchmarks/dotnet export-corpus";

        /// <summary>Renders one workload's oracle in the stored form.</summary>
        private static string Oracle(string workload, string suite)
        {
            var raw = HeddleEngine.Render("controlled", workload, HeddleEngine.Sink.String);
            return Normalize.Apply(raw, suite, workload + " oracle");
        }

        // ---- export-corpus ---------------------------------------------------------------------

        /// <summary>Implements <c>export-corpus [--allow-dirty]</c>. Returns the process exit code.</summary>
        public static int Export(bool allowDirty)
        {
            var repoRoot = RepoRoot();
            var dirty = Git(repoRoot, "status --porcelain").Trim().Length > 0;
            if (dirty && !allowDirty)
            {
                Console.Error.WriteLine(
                    "export-corpus: the working tree is dirty. Commit first, or pass --allow-dirty — which " +
                    "stamps generatingCommit with '+dirty' and must never be committed.");
                return 1;
            }

            var commit = Git(repoRoot, "rev-parse HEAD").Trim();
            var generatingCommit = dirty ? commit + "+dirty" : commit;
            var generatedUtc = DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

            var dir = ExportDirectory(repoRoot);
            System.IO.Directory.CreateDirectory(dir);

            var manifest = new StringBuilder();
            manifest.Append("{\n");
            manifest.Append("  \"$schema\": \"manifest schema v1 (informal; fields below are normative)\",\n");
            manifest.Append("  \"generator\": \"").Append(GeneratorName).Append("\",\n");
            manifest.Append("  \"entries\": [\n");

            var workloads = GoldenCorpus.Workloads;
            for (var i = 0; i < workloads.Count; i++)
            {
                var (id, suite) = workloads[i];
                var bytes = Utf8NoBom.GetBytes(Oracle(id, suite));
                var fileName = id + ".golden.html";
                File.WriteAllBytes(Path.Combine(dir, fileName), bytes);
                var hash = Sha256Hex(bytes);

                manifest.Append("    {\n");
                manifest.Append($"      \"workload\": \"{id}\",\n");
                manifest.Append($"      \"suite\": \"{suite}\",\n");
                manifest.Append($"      \"file\": \"{fileName}\",\n");
                manifest.Append($"      \"byteLength\": {bytes.Length.ToString(CultureInfo.InvariantCulture)},\n");
                manifest.Append($"      \"hash\": \"sha256:{hash}\",\n");
                manifest.Append($"      \"generatingCommit\": \"{generatingCommit}\",\n");
                manifest.Append($"      \"generatedUtc\": \"{generatedUtc}\"\n");
                manifest.Append(i < workloads.Count - 1 ? "    },\n" : "    }\n");

                var authored = VerifierDefinitions.For(id);
                File.WriteAllBytes(Path.Combine(dir, id + ".verify.json"),
                    Utf8NoBom.GetBytes(VerifierDefinitions.ToJson(authored.Definition)));

                Console.WriteLine($"exported {fileName} ({bytes.Length} bytes, sha256:{hash}) + {id}.verify.json");
            }

            manifest.Append("  ],\n");

            // The fixtures section (ledger E20): model data exported for the non-.NET ports, hashed
            // and recorded exactly like the goldens so a stale copy fails loudly rather than gating
            // five ecosystems against drifted data. Today: composed-page's structured nav.
            var fixtureDir = Path.Combine(dir, "fixtures", "composed-page");
            System.IO.Directory.CreateDirectory(fixtureDir);
            var navBytes = Utf8NoBom.GetBytes(NavFixtureJson());
            File.WriteAllBytes(Path.Combine(fixtureDir, "nav.json"), navBytes);
            var navHash = Sha256Hex(navBytes);

            manifest.Append("  \"fixtures\": [\n");
            manifest.Append("    {\n");
            manifest.Append("      \"workload\": \"composed-page\",\n");
            manifest.Append("      \"file\": \"fixtures/composed-page/nav.json\",\n");
            manifest.Append($"      \"byteLength\": {navBytes.Length.ToString(CultureInfo.InvariantCulture)},\n");
            manifest.Append($"      \"hash\": \"sha256:{navHash}\",\n");
            manifest.Append($"      \"generatingCommit\": \"{generatingCommit}\",\n");
            manifest.Append($"      \"generatedUtc\": \"{generatedUtc}\"\n");
            manifest.Append("    }\n");
            manifest.Append("  ]\n");
            manifest.Append("}\n");
            Console.WriteLine($"exported fixtures/composed-page/nav.json ({navBytes.Length} bytes, sha256:{navHash})");
            File.WriteAllBytes(Path.Combine(dir, "manifest.json"), Utf8NoBom.GetBytes(manifest.ToString()));
            Console.WriteLine($"exported manifest.json (generatingCommit {generatingCommit}) to {dir}");
            Engines.RazorEngine.Shutdown();
            return 0;
        }

        // ---- verify-corpus ---------------------------------------------------------------------

        /// <summary>Implements <c>verify-corpus</c>: freshness, then verifier calibration. Returns the exit code.</summary>
        public static int Verify()
        {
            var dir = GoldenCorpus.Directory();
            var manifestPath = Path.Combine(dir, "manifest.json");
            if (!File.Exists(manifestPath))
            {
                Console.Error.WriteLine("[FAIL] manifest.json not found; run export-corpus first.");
                return 1;
            }

            var entries = GoldenCorpus.LoadManifest().Entries
                .ToDictionary(e => e.Workload, e => e, StringComparer.Ordinal);
            var all = true;

            // 1. Freshness — a live render still equals the committed bytes, byte-exact on the
            //    stored form (N3b is NOT applied: this is Heddle against its own oracle, and a
            //    whitespace change here is a real change to what every other ecosystem gates on).
            foreach (var (id, suite) in GoldenCorpus.Workloads)
            {
                var file = Path.Combine(dir, id + ".golden.html");
                if (!File.Exists(file))
                {
                    Console.Error.WriteLine($"[FAIL] {id} freshness: GoldenCorpus/{id}.golden.html is missing");
                    all = false;
                    continue;
                }

                var storedBytes = File.ReadAllBytes(file);
                var liveBytes = Utf8NoBom.GetBytes(Oracle(id, suite));
                if (!liveBytes.AsSpan().SequenceEqual(storedBytes))
                {
                    Console.Error.WriteLine($"[FAIL] {id} freshness: " +
                        Describe(Utf8NoBom.GetString(storedBytes), Utf8NoBom.GetString(liveBytes)));
                    all = false;
                    continue;
                }

                if (!entries.TryGetValue(id, out var entry))
                {
                    Console.Error.WriteLine($"[FAIL] {id} freshness: no manifest.json entry");
                    all = false;
                    continue;
                }

                var hash = "sha256:" + Sha256Hex(storedBytes);
                if (!string.Equals(entry.Hash, hash, StringComparison.Ordinal) || entry.ByteLength != storedBytes.Length)
                {
                    Console.Error.WriteLine(
                        $"[FAIL] {id} freshness: manifest says {entry.Hash} ({entry.ByteLength} bytes), " +
                        $"file is {hash} ({storedBytes.Length} bytes)");
                    all = false;
                    continue;
                }

                Console.WriteLine($"[PASS] {id} freshness ({storedBytes.Length} bytes, {hash})");
            }

            // 1b. Fixture freshness — the committed nav.json still equals a live serialization of
            //     NavData, and the manifest's fixtures entry still describes the file on disk.
            //     Same discipline as the goldens: five ecosystems read this file as model input.
            {
                var navPath = Path.Combine(dir, "fixtures", "composed-page", "nav.json");
                var fixtureEntry = GoldenCorpus.LoadManifest().Fixtures
                    .FirstOrDefault(f => string.Equals(f.Workload, "composed-page", StringComparison.Ordinal));
                if (!File.Exists(navPath))
                {
                    Console.Error.WriteLine(
                        "[FAIL] composed-page fixture: GoldenCorpus/fixtures/composed-page/nav.json is missing");
                    all = false;
                }
                else if (fixtureEntry == null)
                {
                    Console.Error.WriteLine(
                        "[FAIL] composed-page fixture: manifest.json has no fixtures entry for it");
                    all = false;
                }
                else
                {
                    var storedNav = File.ReadAllBytes(navPath);
                    var liveNav = Utf8NoBom.GetBytes(NavFixtureJson());
                    var navHash = "sha256:" + Sha256Hex(storedNav);
                    if (!liveNav.AsSpan().SequenceEqual(storedNav))
                    {
                        Console.Error.WriteLine("[FAIL] composed-page fixture: nav.json is stale — " +
                            Describe(Utf8NoBom.GetString(storedNav), Utf8NoBom.GetString(liveNav)));
                        all = false;
                    }
                    else if (!string.Equals(fixtureEntry.Hash, navHash, StringComparison.Ordinal)
                             || fixtureEntry.ByteLength != storedNav.Length)
                    {
                        Console.Error.WriteLine(
                            $"[FAIL] composed-page fixture: manifest says {fixtureEntry.Hash} " +
                            $"({fixtureEntry.ByteLength} bytes), file is {navHash} ({storedNav.Length} bytes)");
                        all = false;
                    }
                    else
                    {
                        Console.WriteLine(
                            $"[PASS] composed-page fixture nav.json ({storedNav.Length} bytes, {navHash})");
                    }
                }
            }

            // 2. Calibration — accept the golden, reject each synthesized corruption with the check
            //    kind it is meant to trip. Two corruptions per raw workload, three per encoded one.
            foreach (var (id, _) in GoldenCorpus.Workloads)
            {
                var file = Path.Combine(dir, id + ".golden.html");
                if (!File.Exists(file)) continue; // already reported above

                var golden = Utf8NoBom.GetString(File.ReadAllBytes(file));
                var authored = VerifierDefinitions.For(id);

                var accepted = Verifier.Verify(id, golden);
                if (accepted.Count > 0)
                {
                    foreach (var f in accepted) Console.Error.WriteLine($"[FAIL] {id} verifier rejects the golden: {f}");
                    all = false;
                }
                else
                {
                    Console.WriteLine($"[PASS] {id} verifier accepts the golden");
                }

                all &= Calibrate(id, golden, "removed-row",
                    RemoveFirst(golden, authored.RemovedSegment), authored.RemovedKind);
                all &= Calibrate(id, golden, "reordered",
                    SwapFirst(golden, authored.SwapA, authored.SwapB), "marker");
                if (authored.UnescapeEscaped != null)
                    all &= Calibrate(id, golden, "unescaped",
                        ReplaceFirst(golden, authored.UnescapeEscaped, authored.UnescapeRaw), "forbidden");
            }

            // 3. A dirty manifest is a warning, not a failure: it is legitimate mid-work and only
            //    becomes a problem at commit time.
            var dirtyStamp = entries.Values.FirstOrDefault(
                e => e.GeneratingCommit != null && e.GeneratingCommit.EndsWith("+dirty", StringComparison.Ordinal));
            if (dirtyStamp != null)
                Console.WriteLine($"[WARN] manifest generatingCommit '{dirtyStamp.GeneratingCommit}' is dirty; " +
                                  "re-export at a clean commit before committing the corpus.");

            Console.WriteLine(all ? "CORPUS VERIFIED." : "CORPUS VERIFICATION FAILED.");
            Engines.RazorEngine.Shutdown();
            return all ? 0 : 1;
        }

        /// <summary>
        /// One calibration case. A corruption that cannot be synthesized is a failure in itself: it
        /// means the pin no longer occurs in the golden, so the case has silently stopped testing
        /// anything.
        /// </summary>
        private static bool Calibrate(string id, string golden, string corruption, string corrupted, string expectedKind)
        {
            if (corrupted == null || string.Equals(corrupted, golden, StringComparison.Ordinal))
            {
                Console.Error.WriteLine(
                    $"[FAIL] {id} calibration: corruption '{corruption}' could not be synthesized (pin not found in the golden)");
                return false;
            }

            var failures = Verifier.Verify(id, corrupted);
            if (failures.Count == 0)
            {
                Console.Error.WriteLine($"[FAIL] {id} calibration: corruption '{corruption}' was NOT rejected");
                return false;
            }

            if (!failures.Any(f => f.StartsWith(expectedKind + ":", StringComparison.Ordinal)))
            {
                Console.Error.WriteLine(
                    $"[FAIL] {id} calibration: corruption '{corruption}' was rejected, but not by the expected " +
                    $"'{expectedKind}' check (first failure: {failures[0]})");
                return false;
            }

            Console.WriteLine($"[PASS] {id} calibration: corruption '{corruption}' rejected ({expectedKind})");
            return true;
        }

        // ---- the nav.json fixture ---------------------------------------------------------------

        /// <summary>
        /// The composed-page nav fixture: a JSON serialization of <see cref="Models.NavData"/> —
        /// UTF-8, LF, two-space indent, snake_case keys, properties in declaration order. The five
        /// non-.NET ports load their nav models from this file, so the layout is part of the
        /// committed artifact exactly like the verifier sidecars.
        /// </summary>
        internal static string NavFixtureJson()
        {
            var nav = Models.NavData.Model();
            var sb = new StringBuilder(32 * 1024);

            void Line(int indent, string text) => sb.Append(' ', indent * 2).Append(text).Append('\n');

            string Str(string s)
            {
                // Rule-4 values contain no quote, backslash or control character (asserted by
                // NavData's static constructor), so quoting is the whole escape.
                return "\"" + s + "\"";
            }

            void Links(int indent, System.Collections.Generic.List<Models.NavLink> links)
            {
                if (links.Count == 0) { Line(indent, "\"links\": []"); return; }
                Line(indent, "\"links\": [");
                for (var i = 0; i < links.Count; i++)
                    Line(indent + 1, "{ \"label\": " + Str(links[i].Label) + ", \"href\": " + Str(links[i].Href) + " }"
                                     + (i < links.Count - 1 ? "," : ""));
                Line(indent, "]");
            }

            void Column(int indent, Models.NavColumn column, bool last)
            {
                Line(indent, "{");
                Line(indent + 1, "\"sections\": [");
                for (var i = 0; i < column.Sections.Count; i++)
                {
                    var s = column.Sections[i];
                    Line(indent + 2, "{");
                    Line(indent + 3, "\"title\": " + Str(s.Title) + ",");
                    Line(indent + 3, "\"href\": " + Str(s.Href) + ",");
                    Line(indent + 3, "\"title_linked\": " + (s.TitleLinked ? "true" : "false") + ",");
                    Links(indent + 3, s.Links);
                    Line(indent + 2, "}" + (i < column.Sections.Count - 1 ? "," : ""));
                }
                Line(indent + 1, "]");
                Line(indent, "}" + (last ? "" : ","));
            }

            sb.Append("{\n");
            Line(1, "\"menus\": [");
            for (var m = 0; m < nav.Menus.Count; m++)
            {
                var menu = nav.Menus[m];
                Line(2, "{");
                Line(3, "\"tabs\": [");
                for (var t = 0; t < menu.Tabs.Count; t++)
                {
                    var tab = menu.Tabs[t];
                    Line(4, "{");
                    Line(5, "\"label\": " + Str(tab.Label) + ",");
                    Line(5, "\"href\": " + Str(tab.Href) + ",");
                    Line(5, "\"css\": " + Str(tab.Css) + ",");
                    Line(5, "\"has_dropdown\": " + (tab.HasDropdown ? "true" : "false") + ",");
                    Line(5, "\"dropdown_css\": " + Str(tab.DropdownCss) + ",");
                    Line(5, "\"columns\": [");
                    for (var c = 0; c < tab.Columns.Count; c++)
                        Column(6, tab.Columns[c], c == tab.Columns.Count - 1);
                    Line(5, "]");
                    Line(4, "}" + (t < menu.Tabs.Count - 1 ? "," : ""));
                }
                Line(3, "]");
                Line(2, "}" + (m < nav.Menus.Count - 1 ? "," : ""));
            }
            Line(1, "],");
            Line(1, "\"footer_columns\": [");
            for (var c = 0; c < nav.FooterColumns.Count; c++)
                Column(2, nav.FooterColumns[c], c == nav.FooterColumns.Count - 1);
            Line(1, "]");
            sb.Append("}\n");
            return sb.ToString();
        }

        // ---- corruption synthesis (golden-corpus.md §Verification) -----------------------------

        private static string RemoveFirst(string text, string segment)
        {
            var at = text.IndexOf(segment, StringComparison.Ordinal);
            return at < 0 ? null : text.Remove(at, segment.Length);
        }

        private static string ReplaceFirst(string text, string from, string to)
        {
            var at = text.IndexOf(from, StringComparison.Ordinal);
            return at < 0 ? null : text.Substring(0, at) + to + text.Substring(at + from.Length);
        }

        /// <summary>Swaps the first occurrence of <paramref name="a"/> with the first occurrence of
        /// <paramref name="b"/> that starts after it.</summary>
        private static string SwapFirst(string text, string a, string b)
        {
            var atA = text.IndexOf(a, StringComparison.Ordinal);
            if (atA < 0) return null;
            var atB = text.IndexOf(b, atA + a.Length, StringComparison.Ordinal);
            if (atB < 0) return null;
            return text.Substring(0, atA) + b
                 + text.Substring(atA + a.Length, atB - (atA + a.Length)) + a
                 + text.Substring(atB + b.Length);
        }

        // ---- infrastructure --------------------------------------------------------------------

        /// <summary>
        /// Where <c>export-corpus</c> writes. Unlike <see cref="GoldenCorpus.Directory"/> this cannot
        /// require <c>manifest.json</c> to already exist — the first export creates it — so it
        /// resolves from the repository root instead, still honouring <c>HEDDLE_CORPUS</c> so an
        /// export can be diffed against the committed one without touching the tree.
        /// </summary>
        private static string ExportDirectory(string repoRoot)
        {
            var over = Environment.GetEnvironmentVariable("HEDDLE_CORPUS");
            if (!string.IsNullOrWhiteSpace(over)) return over;
            return Path.Combine(repoRoot, "benchmarks", "dotnet", "GoldenCorpus");
        }

        private static string RepoRoot()
        {
            foreach (var start in new[] { System.IO.Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
            {
                var dir = new DirectoryInfo(start);
                while (dir != null)
                {
                    if (File.Exists(Path.Combine(dir.FullName, "Heddle.sln"))
                        || System.IO.Directory.Exists(Path.Combine(dir.FullName, ".git")))
                        return dir.FullName;
                    dir = dir.Parent;
                }
            }
            throw new CorpusException(
                "could not locate the repository root (Heddle.sln or .git) from the current or base directory.");
        }

        private static string Git(string repoRoot, string arguments)
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "git",
                Arguments = arguments,
                WorkingDirectory = repoRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            }) ?? throw new CorpusException("could not start git.");

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0)
                throw new CorpusException($"'git {arguments}' exited with code {process.ExitCode}.");
            return output;
        }

        private static string Sha256Hex(byte[] bytes)
            => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

        /// <summary>The gate's excerpt shape: first-diff index plus a window from each side.</summary>
        private static string Describe(string expected, string actual)
        {
            if (expected.Length == 0 || actual.Length == 0)
                return $"expected {expected.Length} chars, got {actual.Length}.";
            var n = Math.Min(expected.Length, actual.Length);
            var i = 0;
            while (i < n && expected[i] == actual[i]) i++;
            var from = Math.Max(0, i - 40);
            string Slice(string s) => s.Substring(from, Math.Min(120, s.Length - from)).Replace("\n", "\\n");
            return $"first diff at index {i} (of expected {expected.Length} / actual {actual.Length}).\n" +
                   $"    expected: ...{Slice(expected)}...\n" +
                   $"    actual:   ...{Slice(actual)}...";
        }
    }
}
