using System;
using Templates.Attributes;
using Templates.Core;
using Templates.Data;

namespace Templates.Extensions {
    [ExtensionName("")]
    public class EmptyExtension: AbstractExtension {
        public override object ProcessData(ref Scope scope)
        {
            if (InnerExist) {
                return GetInnerResult(ref scope);
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
