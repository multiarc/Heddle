using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Templates.Language;
using Templates.Strings;

namespace Templates.Data
{
    public class LinePosition
    {
        public int Offset { get; set; }
        public int Line { get; set; }
        public int LineLength { get; set; }

        public override string ToString()
        {
            return $"{Line},{Offset}:{LineLength}";
        }
    }

    public sealed class TtlCompileResult
    {
        private readonly List<LinePosition> _positions = new List<LinePosition>();
        private readonly int[] _lineOffsetsSearch;


        public TtlCompileResult(bool success, string document, ParseContext context)
        {
            Success = success;
            Errors = new List<TtlCompileError>();
            Document = document;
            Context = context;
            if (document != null)
            {
                var lines = document.Split('\n');
                int offset = 0;
                int lineNumber = 1;
                foreach (var line in lines)
                {
                    if (line.Length > 0 && line[0] == '\r')
                        offset++;
                    _positions.Add(new LinePosition {Line = lineNumber, LineLength = line.Length, Offset = offset});
                    lineNumber++;
                    offset += line.Length + 1;
                }
                _lineOffsetsSearch = _positions.Select(p => p.Offset).ToArray();
            }
        }

        public ParseContext Context { get; }

        public bool Success { get; }

        public string Document { get; }

        internal List<TtlCompileError> Errors { get; }

        public IReadOnlyCollection<TtlCompileError> ErrorList => new ReadOnlyCollection<TtlCompileError>(FillUpLines());

        public override string ToString()
        {
            return ErrorList.Aggregate("", (s, error) => $"{s}{error.ToString()}\r\n");
        }

        private IList<TtlCompileError> FillUpLines()
        {
            if (Document != null)
            {
                foreach (var error in Errors.Where(e => e.LinePosition == null))
                {
                    var index = Array.BinarySearch(_lineOffsetsSearch, error.Position.StartIndex);
                    if (index < 0)
                        index = ~index - 1;
                    var line = _positions[index];
                    error.LinePosition = new LinePosition
                    {
                        Line = line.Line,
                        LineLength = Math.Min(line.LineLength, error.Position.Length),
                        Offset = error.Position.StartIndex - line.Offset
                    };
                }
            }
            return Errors;
        }
    }
}