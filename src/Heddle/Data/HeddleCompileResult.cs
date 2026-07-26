using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Heddle.Language;

namespace Heddle.Data
{

    public sealed class HeddleCompileResult
    {
        /// <summary>The shared line rule (phase 6 D6). Replaces this type's own <c>Split('\n')</c> table, whose
        /// leading-<c>'\r'</c> offset bump made <c>"\n\r"</c> sequences and CRLF blank lines report a line and
        /// column the build tier and the editor disagreed with.</summary>
        private readonly LineIndex _lines;


        public HeddleCompileResult(bool success, string document, ParseContext context)
        {
            Success = success;
            Errors = new List<HeddleCompileError>();
            Document = document;
            Context = context;
            if (document != null)
                _lines = new LineIndex(document);
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
                    _lines.ToZeroBased(error.Position.StartIndex, out var line, out var column);
                    error.LinePosition = new LinePosition
                    {
                        Line = line + 1,
                        LineLength = Math.Min(_lines.LineLength(line), error.Position.Length),
                        Offset = column
                    };
                }
            }
            return Errors;
        }
    }
}