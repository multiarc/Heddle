using System;
using System.Collections.Generic;
using System.Globalization;
using Templates.Attributes;
using Templates.Core;
using Templates.Language;
using Templates.Runtime;

namespace Templates.Extensions {
    /// <summary>
    /// <para>Money template</para>
    /// <para>Optional parameter contains culture string to format</para>
    /// </summary>
    [Name ("money")]
    [DataType (typeof (decimal))]
    [EncodeOutput]
    public class MoneyExtension: AbstractHtmlExtension {
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

        public override Type InitStart(string parameterTemplate, Type dataType, Type chainedType, CompileContext context,
            ParseContext parseContext)
        {
            return base.InitStart(parameterTemplate, chainedType, null, context, parseContext);
        }

        protected override object ProcessDataInternal (object value, object chainedResult)
        {
            string locale = GetInnerResult(chainedResult, null);
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