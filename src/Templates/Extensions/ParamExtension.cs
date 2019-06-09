using Templates.Attributes;
using Templates.Core;
using Templates.Data;

namespace Templates.Extensions
{

    [ExtensionName("param")]
    public class ParamExtension : AbstractExtension
    {
        public override ExType InitStart(InitContext initContext, ExType dataType, ExType chainedType, ExType parent)
        {
            return dataType;
        }

        public override object ProcessData(in Scope scope)
        {
            return scope.ModelData;
        }

        public override void RenderData(in Scope scope)
        {
        }
    }
}