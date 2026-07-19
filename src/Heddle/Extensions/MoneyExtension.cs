using System;
using System.Collections.Concurrent;
using System.Globalization;
using Heddle.Attributes;
using Heddle.Core;
using Heddle.Data;

namespace Heddle.Extensions
{
    /// <summary>
    /// <para>Money template</para>
    /// <para>Optional parameter contains culture string to format</para>
    /// </summary>
    [ExtensionName("money")]
    [DataType(typeof(decimal))]
    [EncodeOutput]
    public class MoneyExtension : AbstractHtmlExtension
    {
        private static readonly ConcurrentDictionary<string, CultureInfo> CultureCache =
            new ConcurrentDictionary<string, CultureInfo>
                (StringComparer.OrdinalIgnoreCase);

        private static CultureInfo GetCultureInfo(string locale)
        {
            return CultureCache.GetOrAdd(locale, l => new CultureInfo(locale));
        }

        public override ExType InitStart(InitContext initContext, ExType dataType, ExType chainedType, ExType parent)
        {
            return base.InitStart(initContext, parent, chainedType, null);
        }

        protected override object ProcessDataInternal(in Scope scope)
        {
            var parentData = scope.Parent();
            var locale = GetInnerResult(parentData);
            if (scope.ModelData == null)
                return string.Empty;
            CultureInfo localeInfo = null;
            if (!string.IsNullOrWhiteSpace(locale))
            {
                localeInfo = GetCultureInfo(locale);
            }

            if (scope.ModelData is decimal decimalValue)
            {
                if (localeInfo != null)
                    return decimalValue.ToString("c", localeInfo);
                return decimalValue.ToString("c");
            }

            try
            {
                decimalValue = (decimal) Convert.ChangeType(scope.ModelData, typeof(decimal), CultureInfo.InvariantCulture);
                if (localeInfo != null)
                    return decimalValue.ToString("c", localeInfo);
                return decimalValue.ToString("c");
            }
            catch (InvalidCastException)
            {
                return string.Empty;
            }
            catch (FormatException)
            {
                return string.Empty;
            }
        }

        protected override void RenderDataInternal(in Scope scope)
        {
            var parentData = scope.Parent();
            var locale = GetInnerResult(parentData);
            if (scope.ModelData == null)
                return;
            CultureInfo localeInfo = null;
            if (!string.IsNullOrWhiteSpace(locale))
            {
                localeInfo = GetCultureInfo(locale);
            }

            // Phase 8 D10: null provider ≡ thread-current culture in both ToString("c") and decimal.TryFormat (both
            // resolve NumberFormatInfo.CurrentInfo), so the current-culture no-locale branch survives byte-exact.
            if (scope.ModelData is decimal decimalValue)
            {
#if NET6_0_OR_GREATER
                scope.Renderer.Render(decimalValue, "c", localeInfo);
#else
                scope.Renderer.Render(localeInfo != null ? decimalValue.ToString("c", localeInfo) : decimalValue.ToString("c"));
#endif
                return;
            }

            try
            {
                decimalValue = (decimal) Convert.ChangeType(scope.ModelData, typeof(decimal), CultureInfo.InvariantCulture);
#if NET6_0_OR_GREATER
                scope.Renderer.Render(decimalValue, "c", localeInfo);
#else
                scope.Renderer.Render(localeInfo != null ? decimalValue.ToString("c", localeInfo) : decimalValue.ToString("c"));
#endif
            }
            catch (InvalidCastException)
            {
            }
            catch (FormatException)
            {
            }
        }
    }
}