using System;
using System.Text;
using Heddle.Data;
using Xunit;

namespace Heddle.Tests.Streaming
{
    /// <summary>
    /// Phase 8 WI8 — the adversarial chunk-boundary suite for <see cref="Utf8ScopeRenderer"/> (D5). Surrogate pairs are
    /// positioned to straddle every 16 KB chunk edge; the stateful <c>Encoder.Convert</c> loop must carry the trailing
    /// high surrogate across the boundary so the output equals <c>Encoding.UTF8.GetBytes(input)</c> byte-for-byte.
    /// Run over both a growable and the stingy (exactly-sizeHint) writer.
    /// </summary>
    public class Utf8ChunkBoundaryTests
    {
        private const int ChunkBytes = 16 * 1024;

        private static void AssertByteExact(string input)
        {
            var expected = Encoding.UTF8.GetBytes(input);

            var grow = new TestBufferWriter();
            new Utf8ScopeRenderer(grow).Render(input);
            Assert.Equal(expected, grow.ToArray());

            var stingy = new StingyBufferWriter();
            new Utf8ScopeRenderer(stingy).Render(input);
            Assert.Equal(expected, stingy.ToArray());
        }

        [Fact]
        public void SurrogatePairStraddlesFirstChunkEdge()
        {
            // The emoji (4 UTF-8 bytes = a surrogate pair) sweeps across the 16 384-byte boundary: at some offset its
            // high surrogate is the last unit that fits a chunk and the low surrogate opens the next.
            for (int n = ChunkBytes - 8; n <= ChunkBytes + 8; n++)
                AssertByteExact(new string('a', n) + "😀" + new string('b', 40));
        }

        [Fact]
        public void SurrogatePairsAtEveryChunkEdgeAcrossManyChunks()
        {
            // Emojis placed just before each of several 16 KB boundaries — carry-over must work at every edge, not just
            // the first, and no state may cross the single Render call boundary (flush: true on the final chunk).
            var sb = new StringBuilder();
            for (int chunk = 1; chunk <= 6; chunk++)
            {
                sb.Append('x', ChunkBytes - 2);   // land 2 bytes before the edge
                sb.Append("😀🌍");         // two pairs straddling the edge
            }
            AssertByteExact(sb.ToString());
        }

        [Fact]
        public void ThreeByteScalarsStraddleChunkEdge()
        {
            // 3-byte CJK scalars make the byte boundary fall mid-scalar at different char offsets than ASCII does.
            for (int n = 5450; n <= 5480; n++)   // around the tier boundary in chars (× 3 ≈ 16 KB)
                AssertByteExact(new string('中', n) + "😀");
        }

        [Fact]
        public void LoneSurrogateReplacedIdenticallyToOracle()
        {
            // A lone surrogate (legal in a runtime string) → U+FFFD (EF BF BD) via Encoding.UTF8's default fallback,
            // identically in both tiers and in the oracle. Placed at a chunk edge in the chunked tier.
            AssertByteExact("head" + new string('a', ChunkBytes - 2) + "\uD83D" + new string('b', 100)); // lone high
            AssertByteExact(new string('a', ChunkBytes + 1) + "\uDC00tail");                              // lone low
            AssertByteExact("\uD83Dmid\uDC00");                                                            // both lone
        }

        [Fact]
        public void ExactBoundaryLengths()
        {
            AssertByteExact(new string('a', 5461));   // tier-1 boundary
            AssertByteExact(new string('a', 5462));   // first tier-2 length
            AssertByteExact(new string('a', ChunkBytes));       // exactly one chunk of ASCII
            AssertByteExact(new string('a', ChunkBytes) + "😀"); // one full chunk then a pair
        }
    }
}
