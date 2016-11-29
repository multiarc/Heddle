using System;
using System.Globalization;
using Templates.Attributes;
using Templates.Core;
using Templates.Data;

namespace Templates.Extensions {
    /// <summary>
    /// <para>Condition template</para>
    /// <para>Optional parameter represents string to show if condition is false</para>
    /// </summary>
    [ExtensionName ("ifnot")]
    public class IfNotExtension: AbstractExtension {
        public override ExType InitStart(InitContext initContext, ExType dataType, ExType chainedType, ExType parent)
        {
            return base.InitStart(initContext, parent, chainedType, null);
        }

        public override object ProcessData(ref Scope scope)
        {
            if (scope.ModelData == null)
            {
                var parentData = scope.Parent();
                return GetInnerResult(ref parentData);
            }

            if (!(scope.ModelData is bool))
            {
                return string.Empty;
            }

            if (!(bool) scope.ModelData)
            {
                var parentData = scope.Parent();
                return GetInnerResult(ref parentData);
            }

            return string.Empty;
        }
    }
}