using Templates.Attributes;
using Templates.Core;
using Templates.Data;

namespace Templates.Extensions
{
    [ExtensionName("html")]
    [EncodeOutput]
    public class EmptyHtmlExtension : AbstractHtmlExtension
    {
        protected override object ProcessDataInternal(ref Scope scope)
        {
            if (InnerExist)
            {
                return GetInnerResult(ref scope);
            }

            if (scope.ModelData == null) 
                return string.Empty;
            
            if (scope.ModelData is string s)
                return s;
            
            return scope.ModelData.ToString();

        }

        protected override void RenderDataInternal(ref Scope scope)
        {
            if (InnerExist)
            {
                RenderInnerResult(ref scope);
                return;
            }

            if (scope.ModelData != null)
            {
                if (scope.ModelData is string s)
                {
                    scope.Render(s);
                }
                else
                {
                    scope.Render(scope.ModelData.ToString());
                }
            }
        }
    }
}