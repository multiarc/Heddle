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

        public override object ProcessData(ref Scope scope)
        {
            if (!InnerExist)
                return scope.ChainedData;

            var innerScope = scope.Model(scope.ChainedData, scope.ParentModelData);
            return GetInnerResult(ref innerScope);
        }

        public override void RenderData(ref Scope scope)
        {
            if (!InnerExist)
                return;

            var innerScope = scope.Model(scope.ChainedData, scope.ParentModelData);
            RenderInnerResult(ref innerScope);
        }
    }
}