using System;
using Templates.Attributes;
using Templates.Core;

namespace Templates.Extensions {
    /// <summary>
    /// <para>Guid Template</para>
    /// <para>Optional string represents GUID formatting</para>
    /// </summary>
    [Name ("guid")]
    [DataType (typeof (Guid))]
    public class GuidExtension: AbstractExtension {
        public override object ProcessData(object value, object chainedResult)
        {
            if (!(value is Guid))
                return string.Empty;
            return ((Guid) value).ToString(GetInnerResult(chainedResult, null));
        }
    }
}