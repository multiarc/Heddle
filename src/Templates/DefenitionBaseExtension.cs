using System;
using Templates.Attributes;
using Templates.Data;
using Templates.Runtime;

namespace Templates {
    internal class DefenitionBaseExtension : AbstractHtmlExtension {
        protected override object ProcessDataInternal(object value, object chainedResult)
        {
            return GetInnerResult(value, chainedResult);
        }
    }
}