using System;
using System.Globalization;
using Templates.Attributes;

namespace Templates.Extensions {
    /// <summary>
    /// <para>String Template</para>
    /// <para>Optional parameter represents default value if data name is not set in template or the object value is null</para>
    /// </summary>
    [Name ("string")]
    [DataType (typeof (string))]
    [DirectRender]
    public class StringExtension: AbstractExtension {
        protected override object ProcessDataInternal (object value, object additionalValue)
        {
            if (value == null)
                return GetInnerResult(additionalValue);
            if (!(value is string)) {
                try {
                    value = Convert.ChangeType(value, typeof (string), CultureInfo.InvariantCulture);
                }
                catch (InvalidCastException) {
                    return value.ToString();
                }
            }
            return value;
        }
    }
}