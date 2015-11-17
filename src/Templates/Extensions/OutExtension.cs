using Templates.Attributes;
using Templates.Core;
using Templates.Data;

namespace Templates.Extensions {
    [ExtensionName("out")]
    public class OutExtension : AbstractExtension {
        public override ExType InitStart(InitContext initContext, ExType dataType, ExType chainedType, ExType parent)
        {
            base.InitStart(initContext, chainedType, parent, null);
            return chainedType;
        }

        public override object ProcessData(object data, object chained, object parent)
        {
            if (SubTemplate != null)
            {
                return GetInnerResult(chained, parent);
            }
            return chained;
        }
    }
}