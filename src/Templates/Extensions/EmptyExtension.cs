using Templates.Attributes;
using Templates.Core;
using Templates.Data;

namespace Templates.Extensions
{
    [ExtensionName("")]
    public class EmptyExtension : AbstractExtension
    {
        public override object ProcessData(ref Scope scope)
        {
            if (InnerExist)
            {
                return GetInnerResult(ref scope);
            }

            if (scope.ModelData != null)
            {
                if (scope.ModelData is string s)
                    return s;
                return scope.ModelData.ToString();
            }

            return string.Empty;
        }

        public override void RenderData(ref Scope scope)
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