using System;
using System.Collections.Generic;
using System.IO;

namespace Heddle.Tool.Compile
{
    /// <summary>Writes diagnostics in the canonical MSBuild format the <c>HeddleCompile</c> task
    /// parses without custom code (<c>LogEventsFromTextOutput</c> default):
    /// <c>{file}({line},{col},{endLine},{endCol}): {error|warning|info} HED{nnnn}: {message}</c>.
    /// Positions are 1-based under the engine's <c>\n</c>-only line rule; a diagnostic without a
    /// position prints <c>(1,1)</c>.</summary>
    internal sealed class DiagnosticWriter
    {
        private readonly TextWriter _stdout;

        internal DiagnosticWriter(TextWriter stdout)
        {
            _stdout = stdout;
        }

        internal void Error(string file, string id, string message) =>
            Write(file, 0, 0, "error", id, message);

        internal void Error(string file, string text, int start, int length, string id, string message)
        {
            var map = new LineMap(text);
            map.ToOneBased(start, out int line, out int column);
            map.ToOneBased(start + length, out int endLine, out int endColumn);
            Write(file, line, column, endLine, endColumn, "error", id, message);
        }

        internal void Warning(string file, string id, string message) =>
            Write(file, 0, 0, "warning", id, message);

        internal void Warning(string file, string text, int start, int length, string id, string message)
        {
            var map = new LineMap(text);
            map.ToOneBased(start, out int line, out int column);
            map.ToOneBased(start + length, out int endLine, out int endColumn);
            Write(file, line, column, endLine, endColumn, "warning", id, message);
        }

        internal void Info(string file, string id, string message) =>
            Write(file, 0, 0, "info", id, message);

        private void Write(string file, int line, int column, string severity, string id, string message)
        {
            string position = line <= 0 ? "(1,1)" : "(" + line + "," + column + ")";
            _stdout.WriteLine(file + position + ": " + severity + " " + id + ": " + message);
        }

        private void Write(string file, int line, int column, int endLine, int endColumn,
            string severity, string id, string message)
        {
            _stdout.WriteLine(file + "(" + line + "," + column + "," + endLine + "," + endColumn +
                "): " + severity + " " + id + ": " + message);
        }

        /// <summary>The engine's line rule, restated for the host: a line starts at offset 0 and
        /// after each <c>'\n'</c>; <c>'\r'</c> never starts a line. Offsets count UTF-16 code units.</summary>
        private sealed class LineMap
        {
            private readonly int[] _starts;
            private readonly int _length;

            internal LineMap(string text)
            {
                text = text ?? string.Empty;
                _length = text.Length;
                var starts = new List<int> { 0 };
                for (int i = 0; i < text.Length; i++)
                    if (text[i] == '\n')
                        starts.Add(i + 1);
                _starts = starts.ToArray();
            }

            internal void ToOneBased(int offset, out int line, out int column)
            {
                if (offset < 0)
                    offset = 0;
                if (offset > _length)
                    offset = _length;
                int lo = 0, hi = _starts.Length - 1;
                while (lo < hi)
                {
                    int mid = (lo + hi + 1) / 2;
                    if (_starts[mid] <= offset)
                        lo = mid;
                    else
                        hi = mid - 1;
                }

                line = lo + 1;
                column = offset - _starts[lo] + 1;
            }
        }
    }
}
