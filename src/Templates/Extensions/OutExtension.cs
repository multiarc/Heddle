using Templates.Attributes;
using Templates.Core;

namespace Templates.Extensions {
    [Name("out")]
    public class OutExtension : AbstractExtension {
        public override object ProcessData(object value, object chainedResult)
        {
            return chainedResult;
        }
    }
}