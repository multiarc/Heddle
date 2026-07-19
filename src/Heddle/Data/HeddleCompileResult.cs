using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Heddle.Language;

namespace Heddle.Data
{

    public sealed class HeddleCompileResult
    {
        private readonly List<LinePosition> _positions = new List<LinePosition>();
        private readonly int[] _lineOffsetsSearch;


        public HeddleCompileResult(bool success, string document, ParseContext context)
        {
            Success = success;
            Errors = new List<HeddleCompileError>();
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

        internal List<HeddleCompileError> Errors { get; }

        public IReadOnlyCollection<HeddleCompileError> ErrorList => new ReadOnlyCollection<HeddleCompileError>(FillUpLines());

        public override string ToString()
        {
            return ErrorList.Aggregate("", (s, error) => $"{s}{error.ToString()}\r\n");
        }

        private IList<HeddleCompileError> FillUpLines()
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