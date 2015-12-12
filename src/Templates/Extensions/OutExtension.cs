using System;
using Templates.Attributes;
using Templates.Core;
using Templates.Data;

namespace Templates.Extensions
{
    [ExtensionName("out")]
    public class OutExtension : AbstractExtension
    {
        public override ExType InitStart(InitContext initContext, ExType dataType, ExType chainedType, ExType parent)
        {
            base.InitStart(initContext, chainedType, parent, null);
            return chainedType;
        }

        public override object ProcessData(Scope scope)
        {
            if (InnerExist)
            {
                return GetInnerResult(scope.Model(scope.ChainedData, scope.ParentModelData));
            }
            return scope.ChainedData;
        }
    }
}