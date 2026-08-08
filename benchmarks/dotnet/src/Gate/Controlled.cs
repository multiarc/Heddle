using System;
using System.Text;
using Heddle.Benchmarks.Dotnet.Corpus;

namespace Heddle.Benchmarks.Dotnet.Gate
{
    /// <summary>
    /// Controlled-track byte gate and the encoded-suite security floor.
    /// Per cell: render once, normalize (N1–N5), N3b-strip BOTH the
    /// candidate and the oracle, UTF-8-encode, and compare byte sequences.
    ///
    /// Failure carries the contract's full surface — workload, engine, both byte lengths,
    /// first-diff index, and a +/-40-character excerpt from each side — because a gate that only
    /// says "mismatch" costs an hour of bisecting a 55 KB string.
    /// </summary>
    public static class Controlled
    {
        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        /// <summary>
        /// Byte-compares one controlled cell against its corpus entry. Throws
        /// <see cref="GateFailure"/> on any non-whitespace divergence; returns silently on pass.
        /// </summary>
        public static void AssertCell(string engine, string workload, string output)
        {
            var entry = GoldenCorpus.Load(workload);
            var cell = $"{engine} {workload}";

            var normalized = Normalize.Apply(output, entry.Suite, cell);
            var candidate = Utf8NoBom.GetBytes(Normalize.StripWhitespace(normalized));
            var oracle = Utf8NoBom.GetBytes(Normalize.StripWhitespace(entry.Text));

            var n = Math.Min(candidate.Length, oracle.Length);
            var i = 0;
            while (i < n && candidate[i] == oracle[i]) i++;
            if (i != candidate.Length || i != oracle.Length)
                throw new GateFailure(
                    $"[FAIL] {cell}: length exp {oracle.Length} / act {candidate.Length}; first diff at {i}\n" +
                    $"    expected: ...{ExcerptAround(oracle, i)}...\n" +
                    $"    actual:   ...{ExcerptAround(candidate, i)}...");

            if (entry.Suite == "encoded")
                AssertSecurityFloor(engine, workload, output, entry.Text);
        }

        /// <summary>
        /// Encoded-suite security floor (contract rule 5, defence in depth). The raw substring
        /// <c>&lt;script&gt;alert(</c> occurs zero times in the UN-normalized output, and the
        /// escaped form occurs exactly the corpus-derived number of times.
        ///
        /// This is deliberately independent of the byte gate rather than implied by it: the byte
        /// gate proves the output matches an oracle, and this proves the oracle itself was never a
        /// vehicle for unescaped script. A twin that passed the byte gate against a corrupted
        /// oracle would still fail here.
        /// </summary>
        public static void AssertSecurityFloor(string engine, string workload, string rawOutput, string oracleText)
        {
            var rawHits = Normalize.CountOccurrences(rawOutput, "<script>alert(");
            if (rawHits != 0)
                throw new GateFailure(
                    $"[FAIL] {engine} {workload} security: raw \"<script>alert(\" found {rawHits} times (expected 0)");

            const string escaped = "&lt;script&gt;alert(";
            var expected = Normalize.CountOccurrences(oracleText, escaped);
            var actual = Normalize.CountOccurrences(Normalize.CanonicalizeEntities(rawOutput), escaped);
            if (actual != expected)
                throw new GateFailure(
                    $"[FAIL] {engine} {workload} security: escaped \"&lt;script&gt;alert(\" found {actual} times (expected {expected})");
        }

        private static string ExcerptAround(byte[] bytes, int index)
        {
            var from = Math.Max(0, index - 40);
            var to = Math.Min(bytes.Length, index + 40);
            return Utf8NoBom.GetString(bytes, from, to - from).Replace("\n", "\\n");
        }
    }
}
