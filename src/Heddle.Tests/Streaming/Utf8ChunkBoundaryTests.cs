using System;
using System.Text;
using Heddle.Data;
using Xunit;

namespace Heddle.Tests.Streaming
{
    /// <summary>
    /// Verifies that <see cref="Utf8ScopeRenderer"/> correctly handles surrogate pairs positioned to straddle
    /// 16 KB chunk boundaries; output must match <c>Encoding.UTF8.GetBytes(input)</c> byte-for-byte over both
    /// growable and exact-size writers.
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
            for (int n = ChunkBytes - 8; n <= ChunkBytes + 8; n++)
                AssertByteExact(new string('a', n) + "😀" + new string('b', 40));
        }

        [Fact]
        public void SurrogatePairsAtEveryChunkEdgeAcrossManyChunks()
        {
            // State must not carry across the Render call boundary.
            var sb = new StringBuilder();
            for (int chunk = 1; chunk <= 6; chunk++)
            {
                sb.Append('x', ChunkBytes - 2);
                sb.Append("😀🌍");
            }
            AssertByteExact(sb.ToString());
        }

        [Fact]
        public void ThreeByteScalarsStraddleChunkEdge()
        {
            // CJK characters encode as 3 bytes each; byte boundaries fall at different offsets than ASCII.
            for (int n = 5450; n <= 5480; n++)
                AssertByteExact(new string('中', n) + "😀");
        }

        [Fact]
        public void LoneSurrogateReplacedIdenticallyToOracle()
        {
            // Lone surrogates map to U+FFFD (EF BF BD) via UTF-8 fallback, tested at chunk boundaries.
            AssertByteExact("head" + new string('a', ChunkBytes - 2) + "\uD83D" + new string('b', 100));
            AssertByteExact(new string('a', ChunkBytes + 1) + "\uDC00tail");
            AssertByteExact("\uD83Dmid\uDC00");
        }

        [Fact]
        public void ExactBoundaryLengths()
        {
            AssertByteExact(new string('a', 5461));
            AssertByteExact(new string('a', 5462));
            AssertByteExact(new string('a', ChunkBytes));
            AssertByteExact(new string('a', ChunkBytes) + "😀");
        }
    }
}
