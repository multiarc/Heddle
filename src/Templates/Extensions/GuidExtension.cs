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
        public override object ProcessData(object data, object chained)
        {
            if (!(data is Guid))
                return string.Empty;
            return ((Guid) data).ToString(GetInnerResult(chained, null));
        }
    }
}