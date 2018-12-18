using Templates.Attributes;
using Templates.Core;
using Templates.Data;

namespace Templates.Extensions {
    /// <summary>
    /// <para>Condition Template</para>
    /// <para>Optional parameter represents string to show if condition is true</para>
    /// </summary>
    [ExtensionName ("if")]
    public class IfExtension: AbstractExtension {
        public override ExType InitStart(InitContext initContext, ExType dataType, ExType chainedType, ExType parent)
        {
            return base.InitStart(initContext, parent, chainedType, null);
        }

        public override object ProcessData(ref Scope scope)
        {
            if (scope.ModelData == null)
                return string.Empty;

            if (!(scope.ModelData is bool) || (bool)scope.ModelData)
            {
                var parentData = scope.Parent();
                return GetInnerResult(ref parentData);
            }

            return string.Empty;
        }
    }
}