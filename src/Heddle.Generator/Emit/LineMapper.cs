using System.Collections.Generic;

namespace Heddle.Generator.Emit
{
    /// <summary>Maps an absolute UTF-16 offset in the original <c>.heddle</c> source to a 1-based
    /// <c>(line, column)</c> for the <c>#line</c> mapping rules and the <c>Bind(line, column)</c> source position
    /// (phase 7 generated-code #line rules). Positions are original-document coordinates — the coordinates the
    /// parser records on every <c>OutputItem</c>/<c>ExprNode</c> before the hidden-channel shift.</summary>
    internal sealed class LineMapper
    {
        private readonly int[] _lineStarts;
        private readonly int _length;

        public LineMapper(string document)
        {
            _length = document.Length;
            var starts = new List<int> { 0 };
            for (int i = 0; i < document.Length; i++)
            {
                if (document[i] == '\n')
                    starts.Add(i + 1);
            }

            _lineStarts = starts.ToArray();
        }

        /// <summary>Returns 1-based (line, column) for an offset, clamped into range.</summary>
        public (int Line, int Column) Map(int offset)
        {
            if (offset < 0)
                offset = 0;
            if (offset > _length)
                offset = _length;

            // Binary search for the greatest line-start <= offset.
            int lo = 0, hi = _lineStarts.Length - 1, line = 0;
            while (lo <= hi)
            {
                int mid = (lo + hi) / 2;
                if (_lineStarts[mid] <= offset)
                {
                    line = mid;
                    lo = mid + 1;
                }
                else
                {
                    hi = mid - 1;
                }
            }

            return (line + 1, offset - _lineStarts[line] + 1);
        }
    }
}
