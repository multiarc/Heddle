using System;
using System.Collections.Generic;
using System.Globalization;
using Templates.Attributes;

namespace Templates.Extensions {
    /// <summary>
    /// <para>Money template</para>
    /// <para>Optional parameter contains culture string to format</para>
    /// </summary>
    [Name ("money")]
    [Type (typeof (decimal))]
    [AdditionalType (typeof (object))]
    [DirectRender]
    public class MoneyExtension: AbstractExtension {
        private static readonly Dictionary<string, CultureInfo> CultureCache = new Dictionary<string, CultureInfo>
            (10, StringComparer.OrdinalIgnoreCase);

        private static CultureInfo GetCultureInfo (string locale)
        {
            lock (CultureCache) {
                CultureInfo result;
                if (CultureCache.TryGetValue(locale, out result))
                    return result;
                var cultureInfo = new CultureInfo(locale);
                CultureCache.Add(locale, cultureInfo);
                return cultureInfo;
            }
        }

        protected override object ProcessDataInternal (object value, object additionalValue)
        {
            string locale = GetInnerResult(additionalValue);
            if (value == null)
                return string.Empty;
            if (!(value is decimal)) {
                try {
                    value = Convert.ChangeType(value, typeof (decimal), CultureInfo.InvariantCulture);
                }
                catch (InvalidCastException) {
                    return string.Empty;
                }
                catch (FormatException) {
                    return string.Empty;
                }
            }
            if (!string.IsNullOrWhiteSpace(locale)) {
                CultureInfo localeInfo = GetCultureInfo(locale);
                return ((decimal) value).ToString("c", localeInfo);
            }
            return ((decimal) value).ToString("c", CultureInfo.InvariantCulture);
        }
    }
}