using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using Templates.Attributes;
using Templates.Core;
using Templates.Data;

namespace Templates.Extensions
{
    /// <summary>
    /// <para>Money template</para>
    /// <para>Optional parameter contains culture string to format</para>
    /// </summary>
    [ExtensionName("money")]
    [DataType(typeof (decimal))]
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

        protected override object ProcessDataInternal(ref Scope scope)
        {
            var parentData = scope.Parent();
            string locale = GetInnerResult(ref parentData);
            if (scope.ModelData == null)
                return string.Empty;
            CultureInfo localeInfo = null;
            if (!string.IsNullOrWhiteSpace(locale))
            {
                localeInfo = GetCultureInfo(locale);
            }
            var decimalValue = scope.ModelData as decimal?;
            if (decimalValue != null)
            {
                if (localeInfo != null)
                    return decimalValue.Value.ToString("c", localeInfo);
                return decimalValue.Value.ToString("c");
            }
            try
            {
                decimalValue = (decimal) Convert.ChangeType(scope.ModelData, typeof (decimal), CultureInfo.InvariantCulture);
                if (localeInfo != null)
                    return decimalValue.Value.ToString("c", localeInfo);
                return decimalValue.Value.ToString("c");
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
    }
}