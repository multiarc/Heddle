using System.Collections.Generic;

namespace Heddle.Data
{
    /// <summary>
    /// <para>The one line-start index behind every Heddle position surface: the
    /// generator's <c>#line</c> mapper, the language server's LSP positions, and the <see cref="LinePosition"/>
    /// <see cref="HeddleCompileResult"/> attaches to a diagnostic. Before this type they were three indexes with
    /// two different <c>'\r'</c> rules, so one offset could render as three different columns.</para>
    /// <para><b>The canonical rule.</b> A line starts at offset 0 and after each <c>'\n'</c>. A <c>'\r'</c> is
    /// never a line terminator by itself, and a <c>'\r'</c> adjacent to a <c>'\n'</c> belongs to the line that
    /// <c>'\n'</c> terminates — so a CRLF document has exactly the lines its LF twin has, and a classic-Mac
    /// <c>'\r'</c>-only document is one line. Offsets, columns and line lengths count UTF-16 code units, matching
    /// LSP's default <c>utf-16</c> position encoding, <c>BlockPosition</c>'s storage, and Roslyn's
    /// <c>SourceText</c> line semantics for the <c>#line</c> consumer.</para>
    /// <para>Kept in <c>Data/</c> beside <see cref="LinePosition"/>, dependency-free and netstandard2.0, so it
    /// links into the source generator as-is.</para>
    /// </summary>
    internal sealed class LineIndex
    {
        private readonly int[] _lineStarts;

        /// <summary>Builds the index over a document; <c>null</c> indexes as the empty document (one line).</summary>
        public LineIndex(string text)
        {
            text = text ?? string.Empty;
            Length = text.Length;
            var starts = new List<int> { 0 };
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '\n')
                    starts.Add(i + 1);
            }

            _lineStarts = starts.ToArray();
        }

        /// <summary>The indexed document's length in UTF-16 code units.</summary>
        public int Length { get; }

        /// <summary>Number of lines; always at least one.</summary>
        public int LineCount => _lineStarts.Length;

        /// <summary>Start offset of a zero-based line (clamped into range).</summary>
        public int LineStart(int line)
        {
            if (line < 0)
                return 0;
            return line >= _lineStarts.Length ? Length : _lineStarts[line];
        }

        /// <summary>Length of a zero-based line excluding its terminating <c>'\n'</c> — so a CRLF line's length
        /// counts its trailing <c>'\r'</c>, exactly as <c>Split('\n')</c> reports it.</summary>
        public int LineLength(int line)
        {
            if (line < 0 || line >= _lineStarts.Length)
                return 0;
            var end = line + 1 < _lineStarts.Length ? _lineStarts[line + 1] - 1 : Length;
            return end - _lineStarts[line];
        }

        /// <summary>Zero-based line containing an offset (clamped into range).</summary>
        public int LineOf(int offset)
        {
            if (offset < 0)
                offset = 0;
            if (offset > Length)
                offset = Length;

            // Greatest line-start <= offset.
            int lo = 0, hi = _lineStarts.Length - 1;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) / 2;
                if (_lineStarts[mid] <= offset)
                    lo = mid;
                else
                    hi = mid - 1;
            }

            return lo;
        }

        /// <summary>Maps an offset to a zero-based <c>(line, character)</c> — the LSP convention.</summary>
        public void ToZeroBased(int offset, out int line, out int character)
        {
            line = LineOf(offset);
            if (offset < 0)
                offset = 0;
            if (offset > Length)
                offset = Length;
            character = offset - _lineStarts[line];
        }

        /// <summary>Maps an offset to a one-based <c>(line, column)</c> — the <c>#line</c> convention.</summary>
        public void ToOneBased(int offset, out int line, out int column)
        {
            ToZeroBased(offset, out var zeroLine, out var zeroCharacter);
            line = zeroLine + 1;
            column = zeroCharacter + 1;
        }

        /// <summary>Maps a zero-based <c>(line, character)</c> back to an offset, leniently clamped: an over-long
        /// character stops at the line's content end rather than crossing into the next line (LSP's
        /// interpretation), a negative line yields 0, and a past-the-end line yields the document length.</summary>
        public int ZeroBasedToOffset(int line, int character)
        {
            if (line < 0)
                return 0;
            if (line >= _lineStarts.Length)
                return Length;
            var start = _lineStarts[line];
            var contentEnd = line + 1 < _lineStarts.Length ? _lineStarts[line + 1] - 1 : Length;
            var offset = start + (character > 0 ? character : 0);
            return offset > contentEnd ? contentEnd : offset;
        }
    }
}
