using System;
using System.Globalization;
using Templates.Attributes;

namespace Templates.Extensions {
    /// <summary>
    /// <para>Time Template</para>
    /// <para>Optional Parameter represents string to format</para>
    /// </summary>
    [Name ("time")]
    [DataType (typeof (DateTime))]
    [AdditionalDataType (typeof (object))]
    [DirectRender]
    public class TimeExtension: AbstractExtension {
        protected override object ProcessDataInternal (object value, object additionalValue)
        {
            string dateFormat = GetInnerResult(additionalValue);
            if (string.IsNullOrEmpty(dateFormat))
                dateFormat = "t";
            if (value == null || !(value is DateTime))
                return string.Empty;
            return ((DateTime) value).ToString(dateFormat, CultureInfo.InvariantCulture);
        }
    }
}