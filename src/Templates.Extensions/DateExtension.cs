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
    [EncodeOutput]
    public class DateExtension: AbstractHtmlExtension {
        protected override object ProcessDataInternal (object value, object chainedResult)
        {
            string dateFormat = GetInnerResult(chainedResult, null);
            if (string.IsNullOrEmpty(dateFormat))
                dateFormat = "d";
            if (!(value is DateTime))
                return string.Empty;
            return ((DateTime) value).ToString(dateFormat, CultureInfo.InvariantCulture);
        }
    }
}