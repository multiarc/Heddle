using System;

namespace Heddle.LanguageServices
{
    /// <summary>
    /// <para>Immutable line-start index over one document text (phase 6 D11). All offsets and characters count
    /// UTF-16 code units — exactly LSP's default <c>utf-16</c> position encoding and <c>BlockPosition</c>'s
    /// storage — so mapping is line-splitting only.</para>
    /// <para><c>\n</c> terminates a line; a <c>\r</c> immediately before it belongs to the terminated line
    /// (matching the engine's line splitting).</para>
    /// </summary>
    public sealed class LineMap
    {
        private readonly int[] _lineStarts;
        private readonly int _length;

        internal LineMap(string text)
        {
            if (text == null)
                throw new ArgumentNullException(nameof(text));
            _length = text.Length;
            var starts = new System.Collections.Generic.List<int> { 0 };
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '\n')
                    starts.Add(i + 1);
            }

            _lineStarts = starts.ToArray();
        }

        /// <summary>Number of lines (always at least one).</summary>
        public int LineCount => _lineStarts.Length;

        /// <summary>Maps an absolute UTF-16 offset to a zero-based (line, character) position (clamped in range).</summary>
        public (int Line, int Character) OffsetToPosition(int offset)
        {
            if (offset < 0)
                offset = 0;
            if (offset > _length)
                offset = _length;

            int lo = 0, hi = _lineStarts.Length - 1;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) / 2;
                if (_lineStarts[mid] <= offset)
                    lo = mid;
                else
                    hi = mid - 1;
            }

            return (lo, offset - _lineStarts[lo]);
        }

        /// <summary>Maps a zero-based (line, character) position to an absolute UTF-16 offset (leniently clamped).</summary>
        public int PositionToOffset(int line, int character)
        {
            if (line < 0)
                return 0;
            if (line >= _lineStarts.Length)
                return _length;
            int start = _lineStarts[line];
            // Content end excludes the terminating '\n' so an over-long character clamps to the line, not the
            // next line's start (LSP's lenient interpretation).
            int contentEnd = line + 1 < _lineStarts.Length ? _lineStarts[line + 1] - 1 : _length;
            int offset = start + Math.Max(0, character);
            return offset > contentEnd ? contentEnd : offset;
        }
    }
}
