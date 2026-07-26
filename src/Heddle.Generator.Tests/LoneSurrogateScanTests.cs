extern alias gen;
using System;
using System.Linq;
using System.Text;
using Xunit;

namespace Heddle.Generator.Tests
{
    /// <summary>
    /// Generator plan phase 6 D11 / WI11 — the characterization pin for the lone-surrogate scan, captured against
    /// the pre-fold <c>PieceWriter.IndexOfLoneSurrogate</c> body and re-asserted, vector for vector, against the
    /// folded <c>CSharpEscape</c> core. Every expectation below was measured before the move, so an index that
    /// shifts is a red test rather than a review question.
    /// <para>Vectors are spelled as comma-separated UTF-16 code units on purpose: xUnit serializes
    /// <c>[MemberData]</c> strings through UTF-8, which replaces an unpaired surrogate with U+FFFD and would
    /// quietly turn every interesting vector into a boring one.</para>
    /// </summary>
    public class LoneSurrogateScanTests
    {
        public static TheoryData<string, int> Vectors => new TheoryData<string, int>
        {
            { "", -1 },                       // empty
            { "0061,0062", -1 },              // "ab"
            { "D83D,DE00", -1 },              // a well-formed pair
            { "0061,D83D,DE00,0062", -1 },    // pair between plain chars
            { "D800", 0 },                    // lone high
            { "DC00", 0 },                    // lone low
            { "0061,D800", 1 },               // trailing lone high
            { "D800,0061", 0 },               // lone high before a plain char
            { "0061,0062,DC00,0063", 2 },     // lone low mid-string
            { "D83D,DE00,D800", 2 },          // pair then lone high — the index is past the pair
            { "D800,D800", 0 },               // high followed by high
            { "DC00,D83D,DE00", 0 },          // lone low before a pair
        };

        private static string Decode(string codeUnits) =>
            codeUnits.Length == 0
                ? string.Empty
                : new string(codeUnits.Split(',')
                    .Select(u => (char) Convert.ToInt32(u, 16))
                    .ToArray());

        [Theory]
        [MemberData(nameof(Vectors))]
        public void IndexOfLoneSurrogateMatchesThePin(string codeUnits, int expected)
        {
            Assert.Equal(expected, gen::Heddle.Language.Expressions.CSharpEscape.IndexOfLoneSurrogate(Decode(codeUnits)));
        }

        /// <summary>The bool form is <i>defined</i> as the index form's sign after the fold (D11: "turning today's
        /// coincidence of equivalence into a definition"); the same table proves the two agreed before it.</summary>
        [Theory]
        [MemberData(nameof(Vectors))]
        public void HasLoneSurrogateIsTheSignOfTheIndex(string codeUnits, int expected)
        {
            Assert.Equal(expected >= 0, gen::Heddle.Language.Expressions.CSharpEscape.HasLoneSurrogate(Decode(codeUnits)));
        }

        /// <summary>The index form has always tolerated <c>null</c> (the emitter scans an optional document with
        /// it); after the fold the bool form inherits that tolerance instead of throwing, which is the one
        /// behavioral widening WI11 carries — no caller passed <c>null</c> to it.</summary>
        [Fact]
        public void NullScansAsNoLoneSurrogate()
        {
            Assert.Equal(-1, gen::Heddle.Language.Expressions.CSharpEscape.IndexOfLoneSurrogate(null));
            Assert.False(gen::Heddle.Language.Expressions.CSharpEscape.HasLoneSurrogate(null));
        }

        /// <summary>The escape core and the scan agree on what "well formed" means: a string the scan clears is a
        /// string <c>StringLiteral</c> passes through verbatim (no <c>\uXXXX</c> in its output).</summary>
        [Theory]
        [MemberData(nameof(Vectors))]
        public void AClearedStringCarriesNoUnicodeEscapeInItsLiteral(string codeUnits, int expected)
        {
            if (expected >= 0)
                return;
            var literal = gen::Heddle.Language.Expressions.CSharpEscape.StringLiteral(Decode(codeUnits));
            Assert.DoesNotContain("\\u", literal, StringComparison.Ordinal);
        }
    }
}
