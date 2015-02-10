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
    [AdditionalDataType (typeof (object))]
    public class FalseExtension: AbstractExtension {
        protected override object ProcessDataInternal (object value, object additionalValue)
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
                return GetInnerResult(additionalValue);
            return string.Empty;
        }
    }
}