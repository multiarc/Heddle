using System.Collections.Generic;
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