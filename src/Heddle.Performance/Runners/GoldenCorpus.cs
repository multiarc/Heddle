using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;

namespace Heddle.Performance.Runners
{
    /// <summary>
    /// The golden oracle corpus (spec D6/D7/D9/D10; normative format in
    /// docs/spec/cross-stack-benchmarks/phase-1-cross-stack-foundation/golden-corpus.md).
    /// Eight workloads, one <c>&lt;id&gt;.golden.html</c> each under
    /// <c>src/Heddle.Performance/GoldenCorpus/</c> containing Heddle's rendered output in the
    /// stored N1–N5 form (<see cref="TwinContent.Normalize"/> = N2/N3/N4; N1 = UTF-8 no BOM,
    /// no trailing newline; N5 is an identity transform on Heddle output) plus a
    /// <c>manifest.json</c> (byte length, SHA-256, generating commit) and one
    /// <c>&lt;id&gt;.verify.json</c> per workload exported from <see cref="IdiomaticChecks"/>.
    /// The contract's N3b whitespace strip is a comparison-time projection and is deliberately
    /// NOT applied at export; <see cref="AssertFresh"/> stays byte-exact on the stored form.
    /// </summary>
    internal static class GoldenCorpus
    {
        /// <summary>The eight-workload registry, ordered by workload number (1–8).</summary>
        internal static readonly (string Id, string Suite, Func<string> RenderHeddle)[] Registry =
        {
            ("composed-page",        "raw",     () => new HeddleTest().Render()),
            ("trivial-substitution", "raw",     () => new SubstitutionHeddleTest().Render()),
            ("large-loop",           "raw",     () => new LoopHeddleTest().Render()),
            ("mixed-page",           "raw",     () => new MixedHeddleTest().Render()),
            ("conditional-heavy",    "raw",     () => new ConditionalHeddleTest().Render()),
            ("fragment-heavy",       "raw",     () => new FragmentHeddleTest().Render()),
            ("fortunes-encoded",     "encoded", () => new FortunesHeddleTest().Render()),
            ("encoded-loop",         "encoded", () => new EncodedLoopHeddleTest().Render()),
        };

        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        // ---- freshness gate (called from every render benchmark's GlobalSetup) ---------------

        /// <summary>
        /// Renders the workload's Heddle oracle live, normalizes to the stored form, and
        /// byte-compares against the committed corpus file (byte-exact — N3b is not applied
        /// here: Heddle's own output vs its own committed oracle). Throws
        /// <see cref="InvalidOperationException"/> on any mismatch so a benchmark can never
        /// time against a stale committed oracle.
        /// </summary>
        public static void AssertFresh(string workloadId)
        {
            var entry = Find(workloadId);
            var file = Path.Combine(CorpusDirectory(), workloadId + ".golden.html");
            if (!File.Exists(file))
                throw new InvalidOperationException(
                    $"Corpus entry '{workloadId}' is stale: GoldenCorpus/{workloadId}.golden.html does not exist; run export-corpus.");

            var live = TwinContent.Normalize(entry.RenderHeddle());
            var liveBytes = Utf8NoBom.GetBytes(live);
            var storedBytes = File.ReadAllBytes(file);
            if (!BytesEqual(liveBytes, storedBytes))
            {
                var stored = Utf8NoBom.GetString(storedBytes);
                throw new InvalidOperationException(
                    $"Corpus entry '{workloadId}' is stale: live Heddle output diverged from GoldenCorpus/{workloadId}.golden.html. {Describe(stored, live)}");
            }
        }

        // ---- export-corpus -------------------------------------------------------------------

        /// <summary>Implements the <c>export-corpus [--allow-dirty]</c> verb. Returns the exit code.</summary>
        public static int Export(bool allowDirty)
        {
            var repoRoot = FindRepoRoot();
            var status = Git(repoRoot, "status --porcelain");
            var dirty = status.Trim().Length > 0;
            if (dirty && !allowDirty)
            {
                Console.WriteLine("export-corpus: working tree is dirty; commit first or pass --allow-dirty.");
                return 1;
            }

            var commit = Git(repoRoot, "rev-parse HEAD").Trim();
            var generatingCommit = dirty ? commit + "+dirty" : commit;
            var generatedUtc = DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

            var dir = CorpusDirectory();
            Directory.CreateDirectory(dir);

            var manifest = new StringBuilder();
            manifest.Append("{\n");
            manifest.Append("  \"$schema\": \"manifest schema v1 (informal; fields below are normative)\",\n");
            manifest.Append("  \"generator\": \"Heddle.Performance export-corpus\",\n");
            manifest.Append("  \"entries\": [\n");

            for (var i = 0; i < Registry.Length; i++)
            {
                var (id, suite, render) = Registry[i];
                var normalized = TwinContent.Normalize(render());
                var bytes = Utf8NoBom.GetBytes(normalized);
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
                manifest.Append(i < Registry.Length - 1 ? "    },\n" : "    }\n");

                // The idiomatic-verifier definition, exported for cross-language consumption.
                var def = IdiomaticChecks.For(id);
                File.WriteAllBytes(Path.Combine(dir, id + ".verify.json"),
                    Utf8NoBom.GetBytes(IdiomaticChecks.ToJson(def)));

                Console.WriteLine($"exported {fileName} ({bytes.Length} bytes, sha256:{hash}) + {id}.verify.json");
            }

            manifest.Append("  ]\n");
            manifest.Append("}\n");
            File.WriteAllBytes(Path.Combine(dir, "manifest.json"), Utf8NoBom.GetBytes(manifest.ToString()));
            Console.WriteLine($"exported manifest.json (generatingCommit {generatingCommit})");
            return 0;
        }

        // ---- verify-corpus -------------------------------------------------------------------

        /// <summary>Implements the <c>verify-corpus</c> verb: freshness + verifier calibration. Returns the exit code.</summary>
        public static int Verify()
        {
            var dir = CorpusDirectory();
            var manifestPath = Path.Combine(dir, "manifest.json");
            if (!File.Exists(manifestPath))
            {
                Console.WriteLine("[FAIL] manifest.json not found; run export-corpus first.");
                return 1;
            }

            var manifest = JObject.Parse(File.ReadAllText(manifestPath));
            var entries = ((JArray)manifest["entries"]).Cast<JObject>()
                .ToDictionary(e => (string)e["workload"], e => e, StringComparer.Ordinal);

            var all = true;

            // 1. Freshness: live render vs committed bytes, byte-exact on the stored form,
            //    plus SHA-256 vs the manifest.
            foreach (var (id, _, render) in Registry)
            {
                var file = Path.Combine(dir, id + ".golden.html");
                if (!File.Exists(file))
                {
                    Console.WriteLine($"[FAIL] {id} freshness: GoldenCorpus/{id}.golden.html missing");
                    all = false;
                    continue;
                }

                var storedBytes = File.ReadAllBytes(file);
                var live = TwinContent.Normalize(render());
                var liveBytes = Utf8NoBom.GetBytes(live);
                if (!BytesEqual(liveBytes, storedBytes))
                {
                    Console.WriteLine($"[FAIL] {id} freshness: {Describe(Utf8NoBom.GetString(storedBytes), live)}");
                    all = false;
                    continue;
                }

                if (!entries.TryGetValue(id, out var entry))
                {
                    Console.WriteLine($"[FAIL] {id} freshness: no manifest entry");
                    all = false;
                    continue;
                }

                var hash = "sha256:" + Sha256Hex(storedBytes);
                if (!string.Equals((string)entry["hash"], hash, StringComparison.Ordinal)
                    || (long)entry["byteLength"] != storedBytes.Length)
                {
                    Console.WriteLine($"[FAIL] {id} freshness: manifest hash/byteLength does not match the committed file ({hash}, {storedBytes.Length} bytes)");
                    all = false;
                    continue;
                }

                Console.WriteLine($"[PASS] {id} freshness ({storedBytes.Length} bytes, {hash})");
            }

            // 2. Verifier calibration: accept the golden, reject each synthesized corruption
            //    with the correct failing check kind (two corruptions per raw workload, three
            //    per encoded workload).
            foreach (var (id, _, _) in Registry)
            {
                var file = Path.Combine(dir, id + ".golden.html");
                if (!File.Exists(file)) continue; // already failed above
                var golden = Utf8NoBom.GetString(File.ReadAllBytes(file));
                var def = IdiomaticChecks.For(id);

                var acceptFailures = IdiomaticChecks.Verify(def, golden);
                if (acceptFailures.Count > 0)
                {
                    Console.WriteLine($"[FAIL] {id} {acceptFailures[0]}");
                    foreach (var f in acceptFailures.Skip(1))
                        Console.WriteLine($"       {id} {f}");
                    all = false;
                }
                else
                {
                    Console.WriteLine($"[PASS] {id} verifier accepts the golden");
                }

                all &= Calibrate(id, def, golden, "removed-row", RemoveFirst(golden, def.RemovedSegment), def.RemovedKind);
                all &= Calibrate(id, def, golden, "reordered", SwapFirst(golden, def.SwapA, def.SwapB), "marker");
                if (def.UnescapeEscaped != null)
                    all &= Calibrate(id, def, golden, "unescaped",
                        ReplaceFirst(golden, def.UnescapeEscaped, def.UnescapeRaw), "forbidden");
            }

            // 3. Dirty-manifest warning (not a failure).
            foreach (var pair in entries)
            {
                var commit = (string)pair.Value["generatingCommit"];
                if (commit != null && commit.EndsWith("+dirty", StringComparison.Ordinal))
                {
                    Console.WriteLine($"[WARN] manifest generatingCommit '{commit}' is dirty; re-export at a clean commit before committing the corpus.");
                    break; // one warning line suffices — the commit is stamped identically per export
                }
            }

            Console.WriteLine(all ? "CORPUS VERIFIED." : "CORPUS VERIFICATION FAILED.");
            return all ? 0 : 1;
        }

        private static bool Calibrate(
            string id, IdiomaticChecks.Definition def, string golden, string corruption, string corrupted, string expectedKind)
        {
            if (corrupted == null || string.Equals(corrupted, golden, StringComparison.Ordinal))
            {
                Console.WriteLine($"[FAIL] {id} calibration: corruption '{corruption}' could not be synthesized (pin not found in golden)");
                return false;
            }

            var failures = IdiomaticChecks.Verify(def, corrupted);
            if (failures.Count == 0)
            {
                Console.WriteLine($"[FAIL] {id} calibration: corruption '{corruption}' was NOT rejected");
                return false;
            }

            var kindToken = "verifier " + expectedKind + ":";
            if (!failures.Any(f => f.StartsWith(kindToken, StringComparison.Ordinal)))
            {
                Console.WriteLine($"[FAIL] {id} calibration: corruption '{corruption}' rejected, but not by the expected '{expectedKind}' check (got: {failures[0]})");
                return false;
            }

            Console.WriteLine($"[PASS] {id} calibration: corruption '{corruption}' rejected ({expectedKind})");
            return true;
        }

        // ---- corruption synthesis (golden-corpus.md §Verification) ---------------------------

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

        /// <summary>Swaps the first occurrence of <paramref name="a"/> with the first occurrence of <paramref name="b"/> after it.</summary>
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

        // ---- infrastructure ------------------------------------------------------------------

        private static (string Id, string Suite, Func<string> RenderHeddle) Find(string workloadId)
        {
            foreach (var entry in Registry)
                if (string.Equals(entry.Id, workloadId, StringComparison.Ordinal))
                    return entry;
            throw new ArgumentException($"Unknown workload id '{workloadId}'.", nameof(workloadId));
        }

        /// <summary>The committed corpus directory, resolved from the repo root (works both under
        /// `dotnet run` in the project directory and under BenchmarkDotNet child processes).</summary>
        internal static string CorpusDirectory()
            => Path.Combine(FindRepoRoot(), "src", "Heddle.Performance", "GoldenCorpus");

        private static string FindRepoRoot()
        {
            foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
            {
                var dir = new DirectoryInfo(start);
                while (dir != null)
                {
                    if (File.Exists(Path.Combine(dir.FullName, "Heddle.sln"))
                        || Directory.Exists(Path.Combine(dir.FullName, ".git")))
                        return dir.FullName;
                    dir = dir.Parent;
                }
            }
            throw new InvalidOperationException(
                "Could not locate the repository root (Heddle.sln/.git) from the current or base directory.");
        }

        private static string Git(string repoRoot, string arguments)
        {
            using (var process = Process.Start(new ProcessStartInfo
            {
                FileName = "git",
                Arguments = arguments,
                WorkingDirectory = repoRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            }))
            {
                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();
                if (process.ExitCode != 0)
                    throw new InvalidOperationException($"'git {arguments}' failed with exit code {process.ExitCode}.");
                return output;
            }
        }

        private static string Sha256Hex(byte[] bytes)
        {
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(bytes);
                var sb = new StringBuilder(hash.Length * 2);
                foreach (var b in hash)
                    sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));
                return sb.ToString();
            }
        }

        private static bool BytesEqual(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            for (var i = 0; i < a.Length; i++)
                if (a[i] != b[i]) return false;
            return true;
        }

        /// <summary>Same excerpt shape as <c>ParityCheck.Describe</c> (spec §Diagnostics).</summary>
        private static string Describe(string expected, string actual)
        {
            if (expected.Length != actual.Length && (expected.Length == 0 || actual.Length == 0))
                return $"expected {expected.Length} chars, got {actual.Length}.";
            var n = Math.Min(expected.Length, actual.Length);
            var i = 0;
            while (i < n && expected[i] == actual[i]) i++;
            var from = Math.Max(0, i - 40);
            string Slice(string s) => s.Substring(from, Math.Min(120, s.Length - from)).Replace("\n", "\\n");
            return $"first diff at index {i} (of exp {expected.Length}/act {actual.Length}).\n" +
                   $"    expected: ...{Slice(expected)}...\n" +
                   $"    actual:   ...{Slice(actual)}...";
        }
    }
}
