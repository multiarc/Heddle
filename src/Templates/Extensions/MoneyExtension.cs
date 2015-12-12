using System;
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
        private static volatile Dictionary<string, CultureInfo> _cultureCache = new Dictionary<string, CultureInfo>
            (10, StringComparer.OrdinalIgnoreCase);

        private static CultureInfo GetCultureInfo(string locale)
        {
            CultureInfo result;
            if (_cultureCache.TryGetValue(locale, out result))
                return result;
            var cultureInfo = new CultureInfo(locale);
            lock (_cultureCache)
            {
                Dictionary<string, CultureInfo> newInfos = new Dictionary<string, CultureInfo>(_cultureCache) {{locale, cultureInfo}};
                _cultureCache = newInfos;
            }
            return cultureInfo;
        }

        public override ExType InitStart(InitContext initContext, ExType dataType, ExType chainedType, ExType parent)
        {
            return base.InitStart(initContext, parent, chainedType, null);
        }

        protected override object ProcessDataInternal(Scope scope)
        {
            string locale = GetInnerResult(scope.Parent());
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