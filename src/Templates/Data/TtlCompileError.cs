using System;
using Templates.Strings.Core;

namespace Templates.Data {
#if !DOTNET5_4
    [Serializable]
#endif
    public class TtlCompileError {
        public BlockPosition Position { get; set; }

        public string Error { get; set; }

        public Exception Exception { get; set; }

        public override string ToString()
        {
            return $"[{Position}]{Error}\r\n{Exception}";
        }
    }
}