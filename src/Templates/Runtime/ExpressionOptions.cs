using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Templates.Data;

namespace Templates.Runtime {
    internal class ExpressionOptions {

        public ExType ModelType { get; set; }

        public ExType ChainedType { get; set; }

        public string ExtensionName { get; set; }

        public string Expression { get; set; }

        public IEnumerable<string> Namespaces { get; set; }
    }
}