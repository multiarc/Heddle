using Heddle.Attributes;
using Heddle.Core;
using Heddle.Data;

namespace Heddle.Extensions
{
    [ExtensionName("swap")]
    public class SwapExtension : AbstractExtension
    {
        public override ExType InitStart(InitContext initContext, ExType dataType, ExType chainedType, ExType parent)
        {
            return base.InitStart(initContext, chainedType, dataType, parent); //-V3066
        }

        public override object ProcessData(in Scope scope)
        {
            var innerScope = scope.Model(scope.ChainedData, scope.ModelData); //-V3066
            return GetInnerResult(innerScope);
        }

        public override void RenderData(in Scope scope)
        {
            var innerScope = scope.Model(scope.ChainedData, scope.ModelData); //-V3066
            RenderInnerResult(innerScope);
        }
    }
}