using System;
using System.Globalization;
using Templates.Attributes;
using Templates.Core;

namespace Templates.Extensions {
    /// <summary>
    /// <para>Condition template</para>
    /// <para>Optional parameter represents string to show if condition is false</para>
    /// </summary>
    [Name ("else")]
    [DataType (typeof (bool))]
    public class ElseExtension: AbstractExtension {
        public override object ProcessData(object data, object chained)
        {
            if (data == null)
                return string.Empty;
            if (!(data is bool)) {
                try {
                    data = Convert.ChangeType(data, typeof (bool), CultureInfo.InvariantCulture);
                }
                catch (InvalidCastException) {
                    return string.Empty;
                }
                catch (FormatException) {
                    return string.Empty;
                }
            }
            if (!(bool) data)
                return GetInnerResult(chained, null);
            return string.Empty;
        }
    }
}