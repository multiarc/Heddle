using System;
using System.Globalization;
using Templates.Attributes;

namespace Templates.Extensions {
    /// <summary>
    /// <para>Condition template</para>
    /// <para>Optional parameter represents string to show if condition is false</para>
    /// </summary>
    [Name ("false")]
    [DataType (typeof (bool))]
    public class FalseExtension: AbstractHtmlExtension {
        protected override object ProcessDataInternal (object value, object chainedResult)
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
            if (!(bool) value)
                return GetInnerResult(chainedResult, null);
            return string.Empty;
        }
    }
}