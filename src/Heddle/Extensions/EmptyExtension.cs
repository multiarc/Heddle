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

            if (scope.ModelData != null)
            {
                if (scope.ModelData is string s)
                    return s;
                return scope.ModelData.ToString();
            }

            return string.Empty;
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