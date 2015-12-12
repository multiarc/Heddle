using System;
using System.Globalization;
using Templates.Attributes;
using Templates.Core;
using Templates.Data;

namespace Templates.Extensions {
    /// <summary>
    /// <para>Time Template</para>
    /// <para>Optional Parameter represents string to format</para>
    /// </summary>
    [ExtensionName ("time")]
    [DataType (typeof (DateTime))]
    [EncodeOutput]
    public class TimeExtension: AbstractHtmlExtension {
        public override ExType InitStart(InitContext initContext, ExType dataType, ExType chainedType, ExType parent)
        {
            return base.InitStart(initContext, parent, chainedType, null);
        }


        protected override object ProcessDataInternal (object value, object chainedResult, object parent, Func<object, object, string> getInnerResult)
        {
            string dateFormat = getInnerResult(parent, chainedResult);
            if (string.IsNullOrEmpty(dateFormat))
                dateFormat = "t";
            if (!(value is DateTime))
                return string.Empty;
            return ((DateTime) value).ToString(dateFormat, CultureInfo.InvariantCulture);
        }
    }
}