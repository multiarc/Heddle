using Templates.Attributes;
using Templates.Core;
using Templates.Data;

namespace Templates.Extensions
{
    [ExtensionName("html")]
    [EncodeOutput]
    public class EmptyHtmlExtension : AbstractHtmlExtension
    {
        protected override object ProcessDataInternal(in Scope scope)
        {
            if (InnerExist)
            {
                return GetInnerResult(scope);
            }

            if (scope.ModelData == null) 
                return string.Empty;
            
            if (scope.ModelData is string s)
                return s;
            
            return scope.ModelData.ToString();

        }

        protected override void RenderDataInternal(in Scope scope)
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