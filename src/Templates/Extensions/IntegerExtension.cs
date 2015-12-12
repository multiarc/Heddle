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
    [DataType(typeof(long))]
    [EncodeOutput]
    public class IntegerExtension: AbstractHtmlExtension {
        public override ExType InitStart(InitContext initContext, ExType dataType, ExType chainedType, ExType parent)
        {
            return base.InitStart(initContext, parent, chainedType, null);
        }

        protected override object ProcessDataInternal (object value, object chainedResult, object parent, Func<object, object, string> getInnerResult)
        {
            if (value == null)
                return string.Empty;
            var format = getInnerResult(parent, chainedResult);
            if (!(value is long) && !(value is int))
            {
                try
                {
                    value = Convert.ChangeType(value, typeof(long), CultureInfo.InvariantCulture);
                }
                catch (InvalidCastException)
                {
                    return string.Empty;
                }
                catch (OverflowException)
                {
                    return string.Empty;
                }
                catch (FormatException)
                {
                    return string.Empty;
                }
            }
            else if (!(value is int))
            {
                try
                {
                    value = Convert.ChangeType(value, typeof (int), CultureInfo.InvariantCulture);
                }
                catch (InvalidCastException)
                {
                    return string.Empty;
                }
                catch (OverflowException)
                {
                    return string.Empty;
                }
                catch (FormatException)
                {
                    return string.Empty;
                }
            }
            else
            {
                if (!string.IsNullOrEmpty(format))
                    return ((int)value).ToString(format, CultureInfo.InvariantCulture);
                return ((int)value).ToString(CultureInfo.InvariantCulture);
            }
            if (!string.IsNullOrEmpty(format))
                return ((long) value).ToString(format, CultureInfo.InvariantCulture);
            return ((long) value).ToString(CultureInfo.InvariantCulture);
        }
    }
}