using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Templates.Runtime {
    internal class ExpressionOptions {

        public Type ModelType { get; set; }

        public Type ChainedType { get; set; }

        public string ExtensionName { get; set; }

        public string Expression { get; set; }

        public IEnumerable<string> Namespaces { get; set; }
    }
}