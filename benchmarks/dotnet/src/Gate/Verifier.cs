using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Heddle.Benchmarks.Dotnet.Corpus;

namespace Heddle.Benchmarks.Dotnet.Gate
{
    /// <summary>
    /// Idiomatic-track functional-equivalence verifier.
    ///
    /// The idiomatic track cannot use the byte gate: templates are authored the way each engine's
    /// own documentation teaches, so their whitespace, attribute ordering and tag shorthands
    /// legitimately differ. What must hold is that the same *content* is produced. The definitions
    /// are the committed <c>&lt;id&gt;.verify.json</c> files, so all six ecosystems check the same
    /// four rules against the same needles.
    ///
    /// Matching semantics, normative: the candidate is normalized (N1–N5), then N3b is applied to
    /// the output AND to every needle before matching. So a needle written with spaces matches
    /// output written without them and vice versa — the check is about content, not layout.
    ///
    ///   values    exact non-overlapping counts
    ///   markers   must all be present, in the listed order
    ///   forbidden must be absent from both the raw and the normalized output
    ///   required  minimum counts
    /// </summary>
    public static class Verifier
    {
        public sealed class ValueCheck
        {
            public string Text { get; set; }
            public int Count { get; set; }
        }

        public sealed class RequiredCheck
        {
            public string Text { get; set; }
            public int MinCount { get; set; }
        }

        public sealed class Definition
        {
            public string Workload { get; set; }
            public string Suite { get; set; }
            public List<ValueCheck> Values { get; set; } = new List<ValueCheck>();
            public List<string> Markers { get; set; } = new List<string>();
            public List<string> Forbidden { get; set; } = new List<string>();
            public List<RequiredCheck> Required { get; set; } = new List<RequiredCheck>();
        }

        private static readonly Dictionary<string, Definition> Cache = new Dictionary<string, Definition>(StringComparer.Ordinal);

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        };

        public static Definition LoadDefinition(string workload)
        {
            if (Cache.TryGetValue(workload, out var cached)) return cached;
            var def = JsonSerializer.Deserialize<Definition>(GoldenCorpus.LoadVerifyJson(workload), JsonOptions)
                      ?? throw new CorpusException($"{workload}.verify.json did not parse as an object.");
            Cache[workload] = def;
            return def;
        }

        /// <summary>
        /// Runs every check for one cell. Returns the list of failures — empty means verified. The
        /// list shape (rather than throw-on-first) is what lets the gate report every problem in a
        /// drifted template in one run instead of one per re-run.
        /// </summary>
        public static IReadOnlyList<string> Verify(string workload, string rawOutput)
        {
            var def = LoadDefinition(workload);
            var entry = GoldenCorpus.Load(workload);
            var failures = new List<string>();

            var normalized = Normalize.Apply(rawOutput, entry.Suite, workload);
            var haystack = Normalize.StripWhitespace(normalized);

            foreach (var v in def.Values)
            {
                var needle = Normalize.StripWhitespace(v.Text);
                var actual = Normalize.CountOccurrences(haystack, needle);
                if (actual != v.Count)
                    failures.Add($"value: {Show(v.Text)} expected {v.Count} occurrence(s), found {actual}");
            }

            // Markers are ordered: each must appear at or after the end of the previous match.
            var cursor = 0;
            foreach (var m in def.Markers)
            {
                var needle = Normalize.StripWhitespace(m);
                var at = haystack.IndexOf(needle, cursor, StringComparison.Ordinal);
                if (at < 0)
                {
                    var anywhere = haystack.IndexOf(needle, StringComparison.Ordinal) >= 0;
                    failures.Add(anywhere
                        ? $"marker: {Show(m)} appears out of order (expected at or after index {cursor})"
                        : $"marker: {Show(m)} not found");
                    continue;
                }
                cursor = at + needle.Length;
            }

            // Forbidden needles are checked against BOTH the raw and the normalized output: an
            // engine that emits a forbidden sequence only pre-normalization has still emitted it.
            var rawStripped = Normalize.StripWhitespace(Normalize.UnifyLineEndings(rawOutput));
            foreach (var f in def.Forbidden)
            {
                var needle = Normalize.StripWhitespace(f);
                var inNormalized = Normalize.CountOccurrences(haystack, needle);
                var inRaw = Normalize.CountOccurrences(rawStripped, needle);
                if (inNormalized > 0 || inRaw > 0)
                    failures.Add($"forbidden: {Show(f)} present ({inRaw} raw, {inNormalized} normalized)");
            }

            foreach (var r in def.Required)
            {
                var needle = Normalize.StripWhitespace(r.Text);
                var actual = Normalize.CountOccurrences(haystack, needle);
                if (actual < r.MinCount)
                    failures.Add($"required: {Show(r.Text)} expected at least {r.MinCount}, found {actual}");
            }

            return failures;
        }

        /// <summary>Throws with every failure listed when the cell does not verify.</summary>
        public static void AssertCell(string engine, string workload, string rawOutput)
        {
            var failures = Verify(workload, rawOutput);
            if (failures.Count == 0) return;
            throw new GateFailure(
                $"[FAIL] {engine} {workload} [idiomatic]: {failures.Count} check(s) failed\n" +
                string.Join("\n", failures.Select(f => "    " + f)));
        }

        private static string Show(string needle)
        {
            var oneLine = needle.Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
            return oneLine.Length <= 60 ? $"\"{oneLine}\"" : $"\"{oneLine.Substring(0, 57)}...\"";
        }
    }
}
