using Templates.Attributes;
using Templates.Core;
using Templates.Data;

namespace Templates.Extensions
{
    [ExtensionName("swap")]
    public class SwapModelsExtension : AbstractExtension
    {
        public override ExType InitStart(InitContext initContext, ExType dataType, ExType chainedType)
        {
            return base.InitStart(initContext, chainedType, dataType);
        }

        public override object ProcessData(object data, object chained)
        {
            return GetInnerResult(chained, data);
        }
    }
}