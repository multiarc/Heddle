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

        public override object ProcessData(object data, object chained, object parent, Func<object, object, string> getInnerResult)
        {
            if (data == null)
                return getInnerResult(parent, chained);

            if (!(data is bool))
            {
                return string.Empty;
            }

            if (!(bool) data)
                return getInnerResult(parent, chained);

            return string.Empty;
        }
    }
}