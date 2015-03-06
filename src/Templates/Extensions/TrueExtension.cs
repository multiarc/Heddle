using System;
using System.Globalization;
using Templates.Attributes;
using Templates.Core;
using Templates.Language;
using Templates.Runtime;

namespace Templates.Extensions {
    /// <summary>
    /// <para>Condition Template</para>
    /// <para>Optional parameter represents string to show if condition is true</para>
    /// </summary>
    [Name ("if")]
    [DataType (typeof (bool))]
    public class IfExtension: AbstractExtension {
        public override object ProcessData(object value, object chainedResult)
        {
            if (value == null)
                return string.Empty;
            if (!(value is bool)) {
                try {
                    value = Convert.ChangeType(value, typeof (bool), CultureInfo.InvariantCulture);
                }
                catch (InvalidCastException) {
                    return string.Empty;
                }
                catch (FormatException) {
                    return string.Empty;
                }
            }
            if ((bool) value)
                return GetInnerResult(chainedResult, null);
            return string.Empty;
        }

        public override Type InitStart(string parameterTemplate, Type dataType, Type chainedType, CompileContext context, ParseContext parseContext) {
            return base.InitStart(parameterTemplate, chainedType, null, context, parseContext);
        }
    }
}