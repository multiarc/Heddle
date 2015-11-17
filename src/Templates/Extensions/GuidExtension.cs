using System;
using Templates.Attributes;
using Templates.Core;
using Templates.Data;

namespace Templates.Extensions {
    /// <summary>
    /// <para>Guid Template</para>
    /// <para>Optional string represents GUID formatting</para>
    /// </summary>
    [ExtensionName ("guid")]
    [DataType (typeof (Guid))]
    public class GuidExtension: AbstractExtension {
        public override ExType InitStart(InitContext initContext, ExType dataType, ExType chainedType, ExType parent)
        {
            return base.InitStart(initContext, parent, chainedType, null);
        }

        public override object ProcessData(object data, object chained, object parent)
        {
            if (!(data is Guid))
                return string.Empty;
            return ((Guid) data).ToString(GetInnerResult(parent, chained));
        }
    }
}