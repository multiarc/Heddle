using System;
using System.Globalization;
using Templates.Attributes;
using Templates.Core;

namespace Templates.Extensions {
    /// <summary>
    /// <para>Time Template</para>
    /// <para>Optional Parameter represents string to format</para>
    /// </summary>
    [Name ("time")]
    [DataType (typeof (DateTime))]
    [EncodeOutput]
    public class TimeExtension: AbstractHtmlExtension {
        protected override object ProcessDataInternal (object value, object chainedResult)
        {
            string dateFormat = GetInnerResult(chainedResult, null);
            if (string.IsNullOrEmpty(dateFormat))
                dateFormat = "t";
            if (!(value is DateTime))
                return string.Empty;
            return ((DateTime) value).ToString(dateFormat, CultureInfo.InvariantCulture);
        }
    }
}