using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Templates.Strings.Core;

namespace Templates.Data {
    [Serializable]
    public class TtlCompileError {
        public BlockPosition Position { get; set; }

        public string Error { get; set; }

        public Exception Exception { get; set; }
    }
}