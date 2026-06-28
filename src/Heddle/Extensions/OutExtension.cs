using Heddle.Attributes;
using Heddle.Core;
using Heddle.Data;

namespace Heddle.Extensions
{
    [ExtensionName("out")]
    public class OutExtension : AbstractExtension
    {
        public override ExType InitStart(InitContext initContext, ExType dataType, ExType chainedType, ExType parent)
        {
            base.InitStart(initContext, chainedType, parent, null);
            return chainedType;
        }

        public override object ProcessData(in Scope scope)
        {
            if (!InnerExist)
                return scope.ChainedData;

            var innerScope = scope.Model(scope.ChainedData, scope.ParentModelData);
            return GetInnerResult(innerScope);
        }

        public override void RenderData(in Scope scope)
        {
            if (!InnerExist)
            {
                scope.Renderer.Render(scope.ChainedData as string);
            }

            var innerScope = scope.Model(scope.ChainedData, scope.ParentModelData);
            RenderInnerResult(innerScope);
        }
    }
}