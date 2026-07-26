using System.Collections.Generic;
using System.Linq;
using Heddle.Data;
using Heddle.Strings.Core;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// <para>Golden vectors for the canonical line rule: a line starts at offset 0 or after each <c>'\n'</c>;
    /// <c>'\r'</c> never terminates a line by itself; <c>'\r'</c> adjacent to <c>'\n'</c> belongs to the line
    /// <c>'\n'</c> terminates; offsets and columns count UTF-16 code units. The table (<see cref="LineIndexVectors"/>)
    /// is asserted here against <see cref="LineIndex"/> and against the surface that renders a position to a user,
    /// <see cref="HeddleCompileResult"/>; the generator's <c>LineMapper</c> and the LSP's <c>LineMap</c> assert the
    /// same table in their own suites.</para>
    /// <para>Pre-extraction characterization (before the split-based rule): the leading-<c>'\r'</c> offset bump
    /// made three of the eight documents disagree with the canonical rule — <c>"a\r\n\r\nb"</c>, <c>"a\n\rb"</c>,
    /// and <c>"a\nb\r\n\rc\n"</c> reported positions incorrectly. LF-only, plain CRLF, lone-<c>'\r'</c>, no-EOL and
    /// empty documents were already canonical.</para>
    /// </summary>
    public class LineIndexTests
    {
        public static IEnumerable<object[]> Vectors() => LineIndexVectors.Rows();

        public static IEnumerable<object[]> DocumentNames() => LineIndexVectors.Names();

        [Theory]
        [MemberData(nameof(Vectors))]
        public void LineIndexMapsOffsetsCanonically(string name, string text, int offset, int line, int column)
        {
            var index = new LineIndex(text);
            index.ToOneBased(offset, out var oneLine, out var oneColumn);
            Assert.Equal((line, column + 1), (oneLine, oneColumn));
            index.ToZeroBased(offset, out var zeroLine, out var zeroCharacter);
            Assert.Equal((line - 1, column), (zeroLine, zeroCharacter));
            Assert.Equal(offset, index.LineStart(zeroLine) + zeroCharacter);
        }

        /// <summary>The surface a user reads in <c>CompileResult.ToString()</c> reports the same line and column
        /// as the shared index (1-based line, 0-based column, per <see cref="LinePosition"/>'s shape).</summary>
        [Theory]
        [MemberData(nameof(Vectors))]
        public void CompileResultAgreesWithTheSharedIndex(string name, string text, int offset, int line, int column)
        {
            var result = new HeddleCompileResult(false, text, null);
            result.Errors.Add(new HeddleCompileError { Error = "x", Position = new BlockPosition(offset, 0) });
            var position = result.ErrorList.Single().LinePosition;

            Assert.NotNull(position);
            Assert.Equal((line, column), (position.Line, position.Offset));
        }

        /// <summary>Line lengths exclude the terminating <c>'\n'</c> and include a <c>'\r'</c> that precedes it —
        /// the shape <see cref="HeddleCompileResult"/>'s <c>Split('\n')</c> produced, so the
        /// <c>LineLength</c> a diagnostic carries is unchanged by the extraction.</summary>
        [Theory]
        [MemberData(nameof(DocumentNames))]
        public void LineLengthsMatchTheSplitShape(string name)
        {
            var document = LineIndexVectors.ByName(name);
            var index = new LineIndex(document.Text);
            var split = document.Text.Split('\n');

            Assert.Equal(split.Length, index.LineCount);
            for (var i = 0; i < split.Length; i++)
                Assert.Equal(split[i].Length, index.LineLength(i));
        }

        [Theory]
        [MemberData(nameof(DocumentNames))]
        public void OutOfRangeOffsetsClampIntoTheDocument(string name)
        {
            var document = LineIndexVectors.ByName(name);
            var index = new LineIndex(document.Text);

            index.ToOneBased(-5, out var lowLine, out var lowColumn);
            Assert.Equal((1, 1), (lowLine, lowColumn));

            index.ToOneBased(document.Text.Length + 7, out var highLine, out var highColumn);
            index.ToOneBased(document.Text.Length, out var endLine, out var endColumn);
            Assert.Equal((endLine, endColumn), (highLine, highColumn));
        }

        /// <summary>The lenient inverse the LSP relies on: an over-long character clamps to the line's content
        /// end (never the next line's start), a negative line yields 0, a past-the-end line yields the length.</summary>
        [Fact]
        public void ZeroBasedToOffsetClampsLeniently()
        {
            var index = new LineIndex("a\r\nbb\r\nc");

            Assert.Equal(0, index.ZeroBasedToOffset(-1, 4));
            Assert.Equal(8, index.ZeroBasedToOffset(9, 0));
            Assert.Equal(3, index.ZeroBasedToOffset(1, 0));
            Assert.Equal(4, index.ZeroBasedToOffset(1, 1));
            // "bb\r" is three code units; an over-long character stops at the '\n' rather than crossing it.
            Assert.Equal(6, index.ZeroBasedToOffset(1, 40));
            Assert.Equal(7, index.ZeroBasedToOffset(2, 0));
            Assert.Equal(0, index.ZeroBasedToOffset(0, -3));
        }

        [Fact]
        public void NullDocumentIndexesAsEmpty()
        {
            var index = new LineIndex(null);
            Assert.Equal(1, index.LineCount);
            Assert.Equal(0, index.LineLength(0));
            index.ToOneBased(3, out var line, out var column);
            Assert.Equal((1, 1), (line, column));
        }
    }
}
