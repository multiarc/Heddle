using System;
using System.Globalization;
using Templates.Attributes;
using Templates.Core;
using Templates.Data;

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
    [ExtensionName ("date")]
    [DataType (typeof (DateTime))]
    [EncodeOutput]
    public class DateExtension: AbstractHtmlExtension {
        public override ExType InitStart(InitContext initContext, ExType dataType, ExType chainedType)
        {
            return base.InitStart(initContext, chainedType, dataType);
        }

        protected override object ProcessDataInternal(object value, object chainedResult)
        {
            string dateFormat = GetInnerResult(chainedResult, value);
            if (string.IsNullOrEmpty(dateFormat))
                dateFormat = "d";
            if (!(value is DateTime))
                return string.Empty;
            return ((DateTime) value).ToString(dateFormat, CultureInfo.InvariantCulture);
        }
    }
}