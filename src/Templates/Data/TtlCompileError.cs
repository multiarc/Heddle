using System;
using Templates.Strings.Core;

namespace Templates.Data {
#if !ASPNETCORE50
    [Serializable]
#endif
    public class TtlCompileError {
        public BlockPosition Position { get; set; }

        public string Error { get; set; }

        public Exception Exception { get; set; }
    }
}