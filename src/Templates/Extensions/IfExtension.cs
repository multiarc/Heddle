using System;
using System.Globalization;
using Templates.Attributes;
using Templates.Core;
using Templates.Data;

namespace Templates.Extensions {
    /// <summary>
    /// <para>Condition Template</para>
    /// <para>Optional parameter represents string to show if condition is true</para>
    /// </summary>
    [Name ("if")]
    [DataType (typeof (bool))]
    public class IfExtension: AbstractExtension {
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
            if ((bool) data)
                return GetInnerResult(chained, null);
            return string.Empty;
        }

        public override ExType InitStart(InitContext initContext, ExType dataType, ExType chainedType) {
            return base.InitStart(initContext, chainedType, null);
        }
    }
}