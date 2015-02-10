using System;
using System.Globalization;
using Templates.Attributes;

namespace Templates.Extensions {
    /// <summary>
    /// <para>Date template</para>
    /// <para>Optional parameter represents date formatting</para>
    /// <para>For Example:</para>
    /// <para>
    ///     <code>
    ///         &lt;%&lt;date&gt;BirthDate[yyyy-MM-dd]%&gt;
    ///     </code>
    /// </para>
    /// </summary>
    [Name ("date")]
    [DataType (typeof (DateTime))]
    [AdditionalDataType (typeof (object))]
    [DirectRender]
    public class DateExtension: AbstractExtension {
        protected override object ProcessDataInternal (object value, object additionalValue)
        {
            string dateFormat = GetInnerResult(additionalValue);
            if (string.IsNullOrEmpty(dateFormat))
                dateFormat = "d";
            if (value == null || !(value is DateTime))
                return string.Empty;
            return ((DateTime) value).ToString(dateFormat, CultureInfo.InvariantCulture);
        }
    }
}