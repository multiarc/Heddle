using Templates.Attributes;
using Templates.Core;
using Templates.Data;

namespace Templates.Extensions
{
    /// <summary>
    /// <para>Condition template</para>
    /// <para>Optional parameter represents string to show if condition is false</para>
    /// </summary>
    [ExtensionName("ifnot")]
    public class IfNotExtension : AbstractExtension
    {
        public override ExType InitStart(InitContext initContext, ExType dataType, ExType chainedType, ExType parent)
        {
            return base.InitStart(initContext, parent, chainedType, null);
        }

        public override object ProcessData(in Scope scope)
        {
            if (scope.ModelData == null)
            {
                var parentData = scope.Parent();
                return GetInnerResult(parentData);
            }

            if (!(scope.ModelData is bool))
            {
                return string.Empty;
            }

            if (!(bool) scope.ModelData)
            {
                var parentData = scope.Parent();
                return GetInnerResult(parentData);
            }

            return string.Empty;
        }

        public override void RenderData(in Scope scope)
        {
            if (scope.ModelData == null || scope.ModelData is bool data && !data)
            {
                var parentData = scope.Parent();
                RenderInnerResult(parentData);
            }
        }
    }
}