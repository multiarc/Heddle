using System;
using System.Globalization;
using Templates.Attributes;
using Templates.Core;
using Templates.Data;

namespace Templates.Extensions {
    /// <summary>
    /// <para>Integer Template</para>
    /// <para>Optional parameter represents default value if data name is not set in template or the object value is null</para>
    /// </summary>
    [ExtensionName ("int")]
    [DataType (typeof (int))]
    [EncodeOutput]
    public class IntegerExtension: AbstractHtmlExtension {
        public override ExType InitStart(InitContext initContext, ExType dataType, ExType chainedType, ExType parent)
        {
            return base.InitStart(initContext, parent, chainedType, null);
        }

        protected override object ProcessDataInternal (object value, object chainedResult, object parent)
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

            string fastStringFormat = GetInnerResult(parent, chainedResult);
            if (!string.IsNullOrEmpty(fastStringFormat))
                return ((int) value).ToString(fastStringFormat, CultureInfo.InvariantCulture);
            return ((int) value).ToString(CultureInfo.InvariantCulture);
        }
    }
}