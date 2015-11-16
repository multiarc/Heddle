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
        public override ExType InitStart(InitContext initContext, ExType dataType, ExType chainedType)
        {
            return base.InitStart(initContext, chainedType, dataType);
        }

        public override object ProcessData(object data, object chained)
        {
            if (!(data is Guid))
                return string.Empty;
            return ((Guid) data).ToString(GetInnerResult(chained, data));
        }
    }
}