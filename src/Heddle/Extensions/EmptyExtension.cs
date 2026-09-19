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

            // The data path returns the same text RenderData writes: a body that is processed rather
            // than rendered (a definition's caller content, a chained call) concatenates these results
            // as strings, so a boxed non-string here would drop out of the output.
            if (scope.ModelData == null)
                return string.Empty;
            if (scope.ModelData is string s)
                return s;
            return scope.ModelData.ToString();
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