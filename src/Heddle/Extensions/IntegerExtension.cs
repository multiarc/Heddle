using System;
using System.Globalization;
using Heddle.Attributes;
using Heddle.Core;
using Heddle.Data;

namespace Heddle.Extensions
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

            // Phase 8 D10: format through the one span funnel on net6+ (empty format ≡ "G" ≡ ToString(InvariantCulture));
            // the coercion + its three catch-and-render-nothing handlers are unchanged, and downlevel keeps the
            // string-based form verbatim (ISpanFormattable does not exist there).
            if (model is int data)
            {
#if NET6_0_OR_GREATER
                scope.Renderer.Render(data, format, CultureInfo.InvariantCulture);
#else
                scope.Renderer.Render(!string.IsNullOrEmpty(format) ? data.ToString(format, CultureInfo.InvariantCulture) : data.ToString(CultureInfo.InvariantCulture));
#endif
                return;
            }

            if (model is long longData)
            {
#if NET6_0_OR_GREATER
                scope.Renderer.Render(longData, format, CultureInfo.InvariantCulture);
#else
                scope.Renderer.Render(!string.IsNullOrEmpty(format)
                    ? longData.ToString(format, CultureInfo.InvariantCulture)
                    : longData.ToString(CultureInfo.InvariantCulture));
#endif
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

#if NET6_0_OR_GREATER
            scope.Renderer.Render(longData, format, CultureInfo.InvariantCulture);
#else
            scope.Renderer.Render(!string.IsNullOrEmpty(format)
                ? longData.ToString(format, CultureInfo.InvariantCulture)
                : longData.ToString(CultureInfo.InvariantCulture));
#endif
        }
    }
}