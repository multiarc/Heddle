using Templates.Attributes;
using Templates.Core;

namespace Templates.Extensions {
    [ExtensionName("")]
    public class EmptyExtension: AbstractExtension {
        public override object ProcessData(object data, object chained)
        {
            if (SubTemplate != null) {
                return GetInnerResult(data, chained);
            }
            if (data != null) {
                var s = data as string;
                if (s != null)
                    return s;
                return data.ToString();
            }
            return string.Empty;
        }
    }
}
