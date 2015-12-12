using System;
using Templates.Attributes;
using Templates.Core;
using Templates.Data;

namespace Templates.Extensions {
    [ExtensionName("html")]
    [EncodeOutput]
    public class EmptyHtmlExtension: AbstractHtmlExtension {
        protected override object ProcessDataInternal(Scope scope)
        {
            if (InnerExist)
            {
                return GetInnerResult(scope);
            }
            if (scope.ModelData != null) {
                var s = scope.ModelData as string;
                if (s != null)
                    return s;
                return scope.ModelData.ToString();
            }
            return string.Empty;
        }
    }
}
