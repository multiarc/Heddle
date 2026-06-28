using Heddle.Attributes;
using Heddle.Core;
using Heddle.Data;

namespace Heddle.Extensions
{
    /// <summary>
    /// <para>Condition Template</para>
    /// <para>Optional parameter represents string to show if condition is true</para>
    /// </summary>
    [ExtensionName("if")]
    public class IfExtension : AbstractExtension
    {
        public override ExType InitStart(InitContext initContext, ExType dataType, ExType chainedType, ExType parent)
        {
            return base.InitStart(initContext, parent, chainedType, null);
        }

        public override object ProcessData(in Scope scope)
        {
            if (scope.ModelData == null)
                return string.Empty;

            if (!(scope.ModelData is bool) || (bool) scope.ModelData)
            {
                var parentData = scope.Parent();
                return GetInnerResult(parentData);
            }

            return string.Empty;
        }

        public override void RenderData(in Scope scope)
        {
            if (scope.ModelData == null)
                return;

            if (scope.ModelData is bool data && !data)
                return;

            var parentData = scope.Parent();
            RenderInnerResult(parentData);
        }
    }
}