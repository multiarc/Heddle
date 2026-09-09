using Heddle.Attributes;
using Heddle.Core;
using Heddle.Data;

namespace Heddle.Extensions
{
    [ExtensionName("")]
    [ExtensionName("raw")]
    public class EmptyExtension : AbstractExtension
    {
        public override object ProcessData(in Scope scope)
        {
            if (InnerExist)
            {
                return GetInnerResult(scope);
            }

            // The data path carries the value typed: stringifying here collapsed every chained
            // non-string value to String, tripping the Debug return-type guard on precisely typed
            // inner items (function-call results) while rendering the same bytes. Rendering stays
            // in RenderData, which stringifies at the sink.
            return scope.ModelData ?? string.Empty;
        }

        public override void RenderData(in Scope scope)
        {
            if (InnerExist)
            {
                RenderInnerResult(scope);
                return;
            }

            if (scope.ModelData != null)
            {
                if (scope.ModelData is string s)
                {
                    scope.Renderer.Render(s);
                }
                else
                {
                    scope.Renderer.Render(scope.ModelData.ToString());
                }
            }
        }
    }
}