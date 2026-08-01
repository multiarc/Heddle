using System;
using System.Text;

namespace Heddle.Benchmarks.Dotnet.Gate
{
    /// <summary>
    /// Parity contract v2 normalization (normative shape in
    /// docs/spec/cross-stack-benchmarks/phase-1-cross-stack-foundation/parity-contract-v2.md
    /// §normalization-pipeline).
    ///
    /// Whitespace everywhere below is the contract's explicit six-character closed set
    /// { TAB, LF, VT, FF, CR, SPACE } — never <c>char.IsWhiteSpace</c>, never a <c>\s</c> regex
    /// class, and never <c>string.Trim()</c>. All three are Unicode-wide and would strip characters
    /// the contract does not, e.g. U+00A0 NBSP, which is real output a template can legitimately
    /// emit and which must therefore survive to the byte comparison.
    ///
    /// Ported from benchmarks/js/src/gate/normalize.mjs, which is the reference implementation.
    /// </summary>
    public static class Normalize
    {
        /// <summary>The contract's six-character whitespace class.</summary>
        public static bool IsContractWhitespace(char c)
            => c == '\t' || c == '\n' || c == '\v' || c == '\f' || c == '\r' || c == ' ';

        /// <summary>
        /// N1 — well-formedness: the candidate must contain no lone surrogate, i.e. it could not
        /// have been produced by decoding valid UTF-8. A leading U+FEFF (BOM) is deliberately NOT
        /// stripped anywhere in this pipeline: it survives to the byte comparison and fails it.
        /// </summary>
        public static void AssertWellFormed(string text, string context)
        {
            for (var i = 0; i < text.Length; i++)
            {
                var c = text[i];
                if (!char.IsSurrogate(c)) continue;
                var paired = char.IsHighSurrogate(c) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]);
                if (!paired)
                    throw new GateFailure(
                        $"[FAIL] {context}: invalid UTF-8 — candidate output contains a lone surrogate at index {i} (N1)");
                i++; // consume the low surrogate
            }
        }

        /// <summary>
        /// The stored-form pipeline N1–N5: well-formedness (N1), line endings (N2), inter-tag
        /// collapse (N3), edge trim (N4), and — for the encoded suite only — entity
        /// canonicalization (N5). N3b is NOT applied here; it is the comparison-time projection
        /// applied symmetrically to both sides by the gates.
        /// </summary>
        public static string Apply(string text, string suite, string context = "candidate")
        {
            AssertWellFormed(text, context);
            var s = UnifyLineEndings(text);        // N2
            s = CollapseBetweenTags(s);            // N3
            s = TrimEdges(s);                      // N4
            if (suite == "encoded") s = CanonicalizeEntities(s); // N5
            return s;
        }

        /// <summary>N2 — CRLF and lone CR both become LF.</summary>
        public static string UnifyLineEndings(string text)
            => text.Replace("\r\n", "\n").Replace("\r", "\n");

        /// <summary>
        /// N3 — collapse every whitespace run sitting between <c>&gt;</c> and <c>&lt;</c> to
        /// nothing. A single pass suffices: the replacement <c>&gt;&lt;</c> contains no whitespace,
        /// so there is no second fixpoint to reach.
        /// </summary>
        public static string CollapseBetweenTags(string text)
        {
            var sb = new StringBuilder(text.Length);
            var i = 0;
            while (i < text.Length)
            {
                var c = text[i];
                sb.Append(c);
                i++;
                if (c != '>') continue;

                var run = i;
                while (run < text.Length && IsContractWhitespace(text[run])) run++;
                // Only a run that is actually bounded by '<' collapses.
                if (run > i && run < text.Length && text[run] == '<') i = run;
            }
            return sb.ToString();
        }

        /// <summary>N4 — leading and trailing runs of the six-character class only.</summary>
        public static string TrimEdges(string text)
        {
            var start = 0;
            var end = text.Length;
            while (start < end && IsContractWhitespace(text[start])) start++;
            while (end > start && IsContractWhitespace(text[end - 1])) end--;
            return text.Substring(start, end - start);
        }

        /// <summary>
        /// N3b — remove every whitespace run, anywhere, entirely. This is the comparison-time
        /// projection: it is applied to the candidate AND the oracle, so the gate compares
        /// non-whitespace bytes only and whitespace differences can never disqualify a cell.
        /// </summary>
        public static string StripWhitespace(string text)
        {
            var sb = new StringBuilder(text.Length);
            foreach (var c in text)
                if (!IsContractWhitespace(c))
                    sb.Append(c);
            return sb.ToString();
        }

        /// <summary>
        /// N5 — one single left-to-right scan over every recognized spelling of the five
        /// markup-significant characters, rewriting each to its canonical form. Named entities are
        /// case-sensitive; numeric references accept any number of leading zeros; the <c>x</c> and
        /// the hex digits are case-insensitive. Replacement output is never rescanned, so data that
        /// legitimately escaped to <c>&amp;amp;#39;</c> is not double-canonicalized. Any other
        /// entity (<c>&amp;#8482;</c>, <c>&amp;eacute;</c>, <c>&amp;#x60;</c>) is left untouched.
        /// </summary>
        public static string CanonicalizeEntities(string text)
        {
            var sb = new StringBuilder(text.Length);
            var i = 0;
            while (i < text.Length)
            {
                if (text[i] != '&') { sb.Append(text[i]); i++; continue; }

                var canonical = TryScanEntity(text, i, out var consumed);
                if (canonical == null) { sb.Append(text[i]); i++; continue; }

                sb.Append(canonical);
                i += consumed; // advance past the whole match — never rescan the replacement
            }
            return sb.ToString();
        }

        /// <summary>The canonical spelling per codepoint (contract v2 N5 rule 3).</summary>
        private static string CanonicalFor(int codepoint) => codepoint switch
        {
            38 => "&amp;",
            60 => "&lt;",
            62 => "&gt;",
            34 => "&quot;",
            39 => "&#39;",
            _ => null,
        };

        /// <summary>
        /// Scans one entity starting at <paramref name="start"/> (which is known to be '&amp;').
        /// Returns the canonical replacement and how many characters it consumed, or null when the
        /// text at that position is not one of the recognized spellings.
        /// </summary>
        private static string TryScanEntity(string text, int start, out int consumed)
        {
            consumed = 0;
            var semi = text.IndexOf(';', start + 1);
            if (semi < 0) return null;
            var length = semi - start + 1;
            if (length > 12) return null; // longer than any spelling this rule recognizes

            var body = text.Substring(start + 1, semi - start - 1); // between '&' and ';'
            if (body.Length == 0) return null;

            int codepoint;
            if (body[0] == '#')
            {
                var digits = body.Substring(1);
                var hex = digits.Length > 0 && (digits[0] == 'x' || digits[0] == 'X');
                if (hex) digits = digits.Substring(1);
                if (digits.Length == 0) return null;
                foreach (var d in digits)
                {
                    var ok = hex ? Uri.IsHexDigit(d) : d >= '0' && d <= '9';
                    if (!ok) return null;
                }
                if (!int.TryParse(digits,
                        hex ? System.Globalization.NumberStyles.HexNumber : System.Globalization.NumberStyles.None,
                        System.Globalization.CultureInfo.InvariantCulture, out codepoint))
                    return null;
            }
            else
            {
                codepoint = body switch
                {
                    "amp" => 38,
                    "lt" => 60,
                    "gt" => 62,
                    "quot" => 34,
                    "apos" => 39,
                    _ => -1,
                };
                if (codepoint < 0) return null;
            }

            var canonical = CanonicalFor(codepoint);
            if (canonical == null) return null;
            consumed = length;
            return canonical;
        }

        /// <summary>
        /// Non-overlapping ordinal occurrence count. Mirrors the reference implementations in
        /// normalize.mjs, which is this pipeline's reference implementation.
        /// </summary>
        public static int CountOccurrences(string haystack, string needle)
        {
            if (string.IsNullOrEmpty(needle)) return 0;
            var count = 0;
            var index = 0;
            while (true)
            {
                index = haystack.IndexOf(needle, index, StringComparison.Ordinal);
                if (index < 0) return count;
                count++;
                index += needle.Length;
            }
        }
    }

    /// <summary>A gate check failed. The message carries the contract's failure surface.</summary>
    public sealed class GateFailure : Exception
    {
        public GateFailure(string message) : base(message) { }
    }
}
