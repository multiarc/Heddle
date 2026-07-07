using Heddle.LanguageServices;
using Xunit;

namespace Heddle.LanguageServices.Tests
{
    /// <summary>
    /// The phase 6 D11 position-mapping torture set: UTF-16 offset ↔ (line, character) round-trips against
    /// hand-computed expectations across LF and CRLF, with <c>\r</c> belonging to the terminated line.
    /// </summary>
    public class PositionMappingTests
    {
        [Fact]
        public void LfLineStarts()
        {
            var map = new LineMap("ab\ncd\nef");
            Assert.Equal(3, map.LineCount);
            Assert.Equal((0, 0), map.OffsetToPosition(0));
            Assert.Equal((0, 2), map.OffsetToPosition(2));  // the '\n'
            Assert.Equal((1, 0), map.OffsetToPosition(3));  // 'c'
            Assert.Equal((2, 1), map.OffsetToPosition(7));  // 'f'
        }

        [Fact]
        public void CrlfCarriageReturnBelongsToTerminatedLine()
        {
            var map = new LineMap("ab\r\ncd");
            Assert.Equal(2, map.LineCount);
            Assert.Equal((0, 2), map.OffsetToPosition(2)); // '\r' — still line 0
            Assert.Equal((0, 3), map.OffsetToPosition(3)); // '\n'
            Assert.Equal((1, 0), map.OffsetToPosition(4)); // 'c'
        }

        [Fact]
        public void RoundTripsBothWays()
        {
            var text = "one\ntwo\r\nthree";
            var map = new LineMap(text);
            for (int offset = 0; offset <= text.Length; offset++)
            {
                var (line, ch) = map.OffsetToPosition(offset);
                Assert.Equal(offset, map.PositionToOffset(line, ch));
            }
        }

        [Fact]
        public void PositionPastLineEndClamps()
        {
            var map = new LineMap("abc\ndef");
            Assert.Equal(3, map.PositionToOffset(0, 99)); // clamps to the '\n' at end of line 0
            Assert.Equal(7, map.PositionToOffset(99, 0)); // line past the document → document end
        }
    }
}
