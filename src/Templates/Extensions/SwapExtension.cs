using System;
using Templates.Attributes;
using Templates.Core;
using Templates.Data;

namespace Templates.Extensions
{
    [ExtensionName("swap")]
    public class SwapExtension : AbstractExtension
    {
        public override ExType InitStart(InitContext initContext, ExType dataType, ExType chainedType, ExType parent)
        {
            return base.InitStart(initContext, chainedType, dataType, parent);
        }

        public override object ProcessData(Scope scope)
        {
            return GetInnerResult(scope.Model(scope.ChainedData, scope.ModelData));
        }
    }
}