using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Heddle.Benchmarks.Dotnet.Corpus
{
    /// <summary>
    /// Read-only access to the golden corpus at <c>benchmarks/dotnet/GoldenCorpus/</c> (ledger E8;
    /// the corpus moved there with the harness). Every entry's SHA-256 and byte length are verified
    /// against <c>manifest.json</c> before its bytes are used, so a corrupted checkout fails with a
    /// distinct message rather than as a mystifying gate diff.
    ///
    /// This mirrors the loader every other ecosystem already ships —
    /// <c>benchmarks/rust/src/corpus.rs</c>, <c>benchmarks/js/src/gate/corpus.mjs</c>,
    /// <c>benchmarks/go/internal/corpus/gate.go</c> — down to the workload ordering and the
    /// failure shapes. It is deliberately a sixth independent implementation rather than shared
    /// code: the corpus is the one artifact all six harnesses agree on, and agreement is only
    /// evidence when the implementations are independent.
    /// </summary>
    public static class GoldenCorpus
    {
        /// <summary>The eight workloads in protocol order (Phase 1 workloads.md §The set at a glance).</summary>
        public static readonly IReadOnlyList<(string Id, string Suite)> Workloads = new[]
        {
            ("composed-page", "raw"),
            ("trivial-substitution", "raw"),
            ("large-loop", "raw"),
            ("mixed-page", "raw"),
            ("conditional-heavy", "raw"),
            ("fragment-heavy", "raw"),
            ("fortunes-encoded", "encoded"),
            ("encoded-loop", "encoded"),
        };

        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        private static string _directory;
        private static Manifest _manifest;
        private static readonly Dictionary<string, Entry> Cache = new Dictionary<string, Entry>(StringComparer.Ordinal);

        /// <summary>One loaded corpus entry: the oracle text plus the bytes it was decoded from.</summary>
        public sealed class Entry
        {
            public string Id { get; init; }
            public string Suite { get; init; }
            public string Text { get; init; }
            public byte[] Bytes { get; init; }
        }

        public sealed class ManifestEntry
        {
            public string Workload { get; set; }
            public string Suite { get; set; }
            public string File { get; set; }
            public int ByteLength { get; set; }
            public string Hash { get; set; }
            public string GeneratingCommit { get; set; }
            public string GeneratedUtc { get; set; }
        }

        public sealed class Manifest
        {
            public string Generator { get; set; }
            public List<ManifestEntry> Entries { get; set; } = new List<ManifestEntry>();

            /// <summary>The exported model fixtures (ledger E20) — hash-recorded like the goldens.
            /// Defaults to empty so a manifest predating the section still loads.</summary>
            public List<ManifestEntry> Fixtures { get; set; } = new List<ManifestEntry>();
        }

        /// <summary>
        /// The corpus directory. Resolved by walking up from the running assembly until a directory
        /// containing <c>benchmarks/dotnet/GoldenCorpus/manifest.json</c> is found, so the gate works
        /// from any working directory — `dotnet run` from the project, BenchmarkDotNet's generated
        /// child process (which runs from a nested artifacts path), and a bare `dotnet exec` all
        /// resolve identically. Overridable with the <c>HEDDLE_CORPUS</c> environment variable, the
        /// same escape hatch the JVM harness exposes as <c>-Dheddle.corpus</c>.
        /// </summary>
        public static string Directory()
        {
            if (_directory != null) return _directory;

            var overridePath = Environment.GetEnvironmentVariable("HEDDLE_CORPUS");
            if (!string.IsNullOrWhiteSpace(overridePath))
            {
                if (!System.IO.Directory.Exists(overridePath))
                    throw new CorpusException($"Corpus not found at {overridePath} (from HEDDLE_CORPUS).");
                return _directory = overridePath;
            }

            var probe = new DirectoryInfo(AppContext.BaseDirectory);
            while (probe != null)
            {
                var candidate = Path.Combine(probe.FullName, "benchmarks", "dotnet", "GoldenCorpus");
                if (File.Exists(Path.Combine(candidate, "manifest.json")))
                    return _directory = candidate;
                probe = probe.Parent;
            }

            throw new CorpusException(
                "Corpus not found: walked up from " + AppContext.BaseDirectory +
                " looking for benchmarks/dotnet/GoldenCorpus/manifest.json (set HEDDLE_CORPUS=...).");
        }

        /// <summary>Parsed <c>manifest.json</c>, loaded once.</summary>
        public static Manifest LoadManifest()
        {
            if (_manifest != null) return _manifest;
            var path = Path.Combine(Directory(), "manifest.json");
            if (!File.Exists(path))
                throw new CorpusException($"manifest.json not found under {Directory()} — run export-corpus first.");
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            _manifest = JsonSerializer.Deserialize<Manifest>(File.ReadAllBytes(path), options)
                        ?? throw new CorpusException($"manifest.json at {path} did not parse as an object.");
            return _manifest;
        }

        /// <summary>
        /// Loads one entry, verifying SHA-256 and byte length against the manifest first. The
        /// stored form is UTF-8 with no BOM and no trailing newline.
        /// </summary>
        public static Entry Load(string workloadId)
        {
            if (Cache.TryGetValue(workloadId, out var cached)) return cached;

            var file = Path.Combine(Directory(), workloadId + ".golden.html");
            if (!File.Exists(file))
                throw new CorpusException(
                    $"corpus entry {workloadId} not found under benchmarks/dotnet/GoldenCorpus/ — run export-corpus first.");

            var manifestEntry = LoadManifest().Entries
                                    .FirstOrDefault(e => string.Equals(e.Workload, workloadId, StringComparison.Ordinal))
                                ?? throw new CorpusException(
                                    $"corpus entry {workloadId} has no manifest.json entry under benchmarks/dotnet/GoldenCorpus/.");

            var bytes = File.ReadAllBytes(file);
            var hash = "sha256:" + Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            if (bytes.Length != manifestEntry.ByteLength || !string.Equals(hash, manifestEntry.Hash, StringComparison.Ordinal))
                throw new CorpusException(
                    $"corpus entry {workloadId} is corrupted: GoldenCorpus/{workloadId}.golden.html has {hash} " +
                    $"({bytes.Length} bytes), manifest says {manifestEntry.Hash} ({manifestEntry.ByteLength} bytes).");

            var entry = new Entry
            {
                Id = workloadId,
                Suite = manifestEntry.Suite,
                Text = Utf8NoBom.GetString(bytes),
                Bytes = bytes,
            };
            Cache[workloadId] = entry;
            return entry;
        }

        /// <summary>The raw <c>&lt;id&gt;.verify.json</c> text, for the idiomatic verifier.</summary>
        public static string LoadVerifyJson(string workloadId)
        {
            var path = Path.Combine(Directory(), workloadId + ".verify.json");
            if (!File.Exists(path))
                throw new CorpusException(
                    $"verifier definition for {workloadId} not found under benchmarks/dotnet/GoldenCorpus/ — run export-corpus first.");
            return File.ReadAllText(path, Utf8NoBom);
        }

        /// <summary>Verifies every entry's hash and length. Returns the number of entries checked.</summary>
        public static int VerifyAll()
        {
            var manifest = LoadManifest();
            foreach (var e in manifest.Entries) Load(e.Workload);
            return manifest.Entries.Count;
        }
    }

    /// <summary>A structural assumption about the corpus did not hold.</summary>
    public sealed class CorpusException : Exception
    {
        public CorpusException(string message) : base(message) { }
    }
}
