using System;
using System.Globalization;
using Templates.Attributes;
using Templates.Core;
using Templates.Data;

namespace Templates.Extensions {
    /// <summary>
    /// <para>String Template</para>
    /// <para>Optional parameter represents default value if data name is not set in template or the object value is null</para>
    /// </summary>
    [ExtensionName ("string")]
    [DataType (typeof (string))]
    [EncodeOutput]
    public class StringExtension: AbstractHtmlExtension {
        public override ExType InitStart(InitContext initContext, ExType dataType, ExType chainedType, ExType parent)
        {
            return base.InitStart(initContext, parent, chainedType, null);
        }

        protected override object ProcessDataInternal (object value, object chainedResult, object parent, Func<object, object, string> getInnerResult)
        {
            if (value == null)
                return getInnerResult(parent, chainedResult);
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