using Heddle.Data;

namespace Heddle.Generator.Emit
{
    /// <summary>Maps an absolute UTF-16 offset in the original <c>.heddle</c> source to a 1-based
    /// <c>(line, column)</c> for the <c>#line</c> mapping rules and the <c>Bind(line, column)</c> source position.
    /// Positions are original-document coordinates — the coordinates the parser records on every
    /// <c>OutputItem</c>/<c>ExprNode</c> before the hidden-channel shift.
    /// A wrapper over the shared <see cref="LineIndex"/> — the line rule itself lives
    /// there, stated once for the build tier, the run tier and the editor.</summary>
    internal sealed class LineMapper
    {
        private readonly LineIndex _lines;

        public LineMapper(string document)
        {
            _lines = new LineIndex(document);
        }

        /// <summary>Returns 1-based (line, column) for an offset, clamped into range.</summary>
        public (int Line, int Column) Map(int offset)
        {
            _lines.ToOneBased(offset, out var line, out var column);
            return (line, column);
        }
    }
}
