using System;
using Templates.Attributes;

namespace Templates.Extensions {
    /// <summary>
    /// <para>Guid Template</para>
    /// <para>Optional string represents GUID formatting</para>
    /// </summary>
    [Name ("guid")]
    [DataType (typeof (Guid))]
    [AdditionalDataType (typeof (object))]
    [DirectRender]
    public class GuidExtension: AbstractExtension {
        protected override object ProcessDataInternal (object value, object additionalValue)
        {
            if (value == null || !(value is Guid))
                return string.Empty;
            return ((Guid) value).ToString(GetInnerResult(additionalValue));
        }
    }
}