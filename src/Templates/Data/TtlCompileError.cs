using System;
using Templates.Strings.Core;

namespace Templates.Data {
    [Serializable]
    public class TtlCompileError {
        public BlockPosition Position { get; set; }

        public string Error { get; set; }

        public Exception Exception { get; set; }
    }
}