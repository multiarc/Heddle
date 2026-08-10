using System;
using Heddle.Data;

namespace Heddle.LanguageServices
{
    /// <summary>
    /// <para>Immutable line-start index over one document text. All offsets and characters count
    /// UTF-16 code units — exactly LSP's default <c>utf-16</c> position encoding and <c>BlockPosition</c>'s
    /// storage — so mapping is line-splitting only.</para>
    /// <para>A wrapper over the shared <see cref="LineIndex"/>, which owns the line-splitting rule:
    /// <c>\n</c> terminates a line and a <c>\r</c> adjacent to it belongs to the
    /// terminated line. The engine's <see cref="HeddleCompileResult"/> indexes through the same type, so the
    /// equivalence holds by construction rather than by convention.</para>
    /// </summary>
    public sealed class LineMap
    {
        private readonly LineIndex _lines;

        internal LineMap(string text)
        {
            if (text == null)
                throw new ArgumentNullException(nameof(text));
            _lines = new LineIndex(text);
        }

        /// <summary>Number of lines (always at least one).</summary>
        public int LineCount => _lines.LineCount;

        /// <summary>Maps an absolute UTF-16 offset to a zero-based (line, character) position (clamped in range).</summary>
        public (int Line, int Character) OffsetToPosition(int offset)
        {
            _lines.ToZeroBased(offset, out var line, out var character);
            return (line, character);
        }

        /// <summary>Maps a zero-based (line, character) position to an absolute UTF-16 offset (leniently clamped).</summary>
        public int PositionToOffset(int line, int character) => _lines.ZeroBasedToOffset(line, character);
    }
}
