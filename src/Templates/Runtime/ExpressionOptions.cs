using System.Collections.Generic;
using Templates.Data;
using Templates.Strings.Core;

namespace Templates.Runtime {
    internal class ExpressionOptions {

        public ExType RootModelType { get; set; }

        public ExType ModelType { get; set; }

        public ExType ChainedType { get; set; }

        public string ExtensionName { get; set; }

        public string Expression { get; set; }

        public BlockPosition Position { get; set; }

        public IEnumerable<string> Namespaces { get; set; }
    }
}