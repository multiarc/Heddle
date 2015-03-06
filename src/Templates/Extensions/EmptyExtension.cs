using Templates.Attributes;
using Templates.Core;

namespace Templates.Extensions {
    [Name("")]
    [EncodeOutput]
    public class EmptyExtension: AbstractExtension {
        public override object ProcessData(object value, object chainedResult)
        {
            if (SubTemplate != null) {
                return GetInnerResult(value, chainedResult);
            }
            if (value != null) {
                var s = value as string;
                if (s != null)
                    return s;
                return value.ToString();
            }
            return string.Empty;
        }
    }
}
