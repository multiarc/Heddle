using System;
using System.Globalization;
using Templates.Attributes;
using Templates.Core;
using Templates.Data;

namespace Templates.Extensions
{
    /// <summary>
    /// <para>Integer Template</para>
    /// <para>Optional parameter represents default value if data name is not set in template or the object value is null</para>
    /// </summary>
    [ExtensionName("int")]
    [DataType(typeof(int))]
    [DataType(typeof(long))]
    [EncodeOutput]
    public class IntegerExtension : AbstractHtmlExtension
    {
        public override ExType InitStart(InitContext initContext, ExType dataType, ExType chainedType, ExType parent)
        {
            return base.InitStart(initContext, parent, chainedType, null);
        }

        protected override object ProcessDataInternal(in Scope scope)
        {
            var model = scope.ModelData;
            if (model == null)
                return string.Empty;
            var parentScope = scope.Parent();
            var format = GetInnerResult(parentScope);

            if (model is int data)
            {
                if (!string.IsNullOrEmpty(format))
                    return data.ToString(format, CultureInfo.InvariantCulture);
                return data.ToString(CultureInfo.InvariantCulture);
            }

            if (model is long longData)
            {
                if (!string.IsNullOrEmpty(format))
                    return longData.ToString(format, CultureInfo.InvariantCulture);
                return longData.ToString(CultureInfo.InvariantCulture);
            }

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

            if (!string.IsNullOrEmpty(format))
                return ((long) model).ToString(format, CultureInfo.InvariantCulture);
            return ((long) model).ToString(CultureInfo.InvariantCulture);
        }

        protected override void RenderDataInternal(in Scope scope)
        {
            var model = scope.ModelData;
            if (model == null)
                return;

            var parentScope = scope.Parent();
            var format = GetInnerResult(parentScope);

            if (model is int data)
            {
                scope.Renderer.Render(!string.IsNullOrEmpty(format) ? data.ToString(format, CultureInfo.InvariantCulture) : data.ToString(CultureInfo.InvariantCulture));
                return;
            }

            if (model is long longData)
            {
                scope.Renderer.Render(!string.IsNullOrEmpty(format)
                    ? longData.ToString(format, CultureInfo.InvariantCulture)
                    : longData.ToString(CultureInfo.InvariantCulture));
                return;
            }

            try
            {
                longData = (long) Convert.ChangeType(model, typeof(long), CultureInfo.InvariantCulture);
            }
            catch (InvalidCastException)
            {
                return;
            }
            catch (OverflowException)
            {
                return;
            }
            catch (FormatException)
            {
                return;
            }

            scope.Renderer.Render(!string.IsNullOrEmpty(format)
                ? longData.ToString(format, CultureInfo.InvariantCulture)
                : longData.ToString(CultureInfo.InvariantCulture));
        }
    }
}