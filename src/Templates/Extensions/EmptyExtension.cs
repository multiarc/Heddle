using System;
using Templates.Attributes;
using Templates.Core;

namespace Templates.Extensions {
    [ExtensionName("")]
    public class EmptyExtension: AbstractExtension {
        public override object ProcessData(object data, object chained, object parent, Func<object, object, string> getInnerResult)
        {
            if (InnerExist) {
                return getInnerResult(data, chained);
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
