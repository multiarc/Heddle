using System;
using Templates.Strings.Core;

namespace Templates.Data {
    public class TtlCompileError {
        public LinePosition LinePosition { get; internal set; }

        public BlockPosition Position { get; set; }

        public string Error { get; set; }

        public Exception Exception { get; set; }

        public override string ToString()
        {
            if (LinePosition != null)
            {
                return $"[{LinePosition}]{Error}\r\n{Exception}";
            }
            return $"[{Position}]{Error}\r\n{Exception}";
        }

        public string ToString(int line, int position, int length)
        {
            return $"[{line},{position}:{length}]{Error}\r\n{Exception}";
        }
    }
}