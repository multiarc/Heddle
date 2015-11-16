using Templates.Attributes;
using Templates.Core;
using Templates.Data;

namespace Templates.Extensions {
    [Name("out")]
    public class OutExtension : AbstractExtension {
        public override ExType InitStart(InitContext initContext, ExType dataType, ExType chainedType)
        {
            base.InitStart(initContext, chainedType, null);
            return chainedType;
        }

        public override object ProcessData(object data, object chained)
        {
            return chained;
        }
    }
}