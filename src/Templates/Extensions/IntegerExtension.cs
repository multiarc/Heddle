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

        protected override object ProcessDataInternal (ref Scope scope)
        {
            var model = scope.ModelData;
            if (model == null)
                return string.Empty;
            var parentScope = scope.Parent();
            var format = GetInnerResult(ref parentScope);
            if (!(model is long) && !(model is int))
            {
                try
                {
                    model = Convert.ChangeType(model, typeof(long), CultureInfo.InvariantCulture);
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
            else if (!(model is int))
            {
                try
                {
                    model = Convert.ChangeType(model, typeof (int), CultureInfo.InvariantCulture);
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
                    return ((int)model).ToString(format, CultureInfo.InvariantCulture);
                return ((int)model).ToString(CultureInfo.InvariantCulture);
            }
            if (!string.IsNullOrEmpty(format))
                return ((long)model).ToString(format, CultureInfo.InvariantCulture);
            return ((long)model).ToString(CultureInfo.InvariantCulture);
        }
    }
}