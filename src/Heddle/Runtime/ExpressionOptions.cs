using System.Collections.Generic;
using Heddle.Data;
using Heddle.Strings.Core;

namespace Heddle.Runtime {
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