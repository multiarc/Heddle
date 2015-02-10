using System;
using System.Globalization;
using Templates.Attributes;

namespace Templates.Extensions {
    /// <summary>
    /// <para>Integer Template</para>
    /// <para>Optional parameter represents default value if data name is not set in template or the object value is null</para>
    /// </summary>
    [Name ("int")]
    [DataType (typeof (int))]
    [AdditionalDataType (typeof (object))]
    [DirectRender]
    public class IntegerExtension: AbstractExtension {
        protected override object ProcessDataInternal (object value, object additionalValue)
        {
            if (value == null)
                return string.Empty;
            if (!(value is int)) {
                try {
                    value = Convert.ChangeType(value, typeof (int), CultureInfo.InvariantCulture);
                }
                catch (InvalidCastException) {
                    return string.Empty;
                }
                catch (OverflowException) {
                    return string.Empty;
                }
                catch (FormatException) {
                    return string.Empty;
                }
            }

            string fastStringFormat = GetInnerResult(additionalValue);
            if (!string.IsNullOrEmpty(fastStringFormat))
                return ((int) value).ToString(fastStringFormat, CultureInfo.InvariantCulture);
            return ((int) value).ToString(CultureInfo.InvariantCulture);
        }
    }
}