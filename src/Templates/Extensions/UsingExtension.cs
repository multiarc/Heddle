using System;
using Templates.Attributes;
using Templates.Core;
using Templates.Data;

namespace Templates.Extensions {
    [ExtensionName ("using")]
    public class UsingExtension: AbstractExtension {
        public override ExType InitStart(InitContext initContext, ExType dataType, ExType chainedType, ExType parent)
        {
            if (initContext.Context == null)
                throw new ArgumentNullException(nameof(initContext.Context));
            base.InitStart(initContext, dataType, chainedType, parent);
            initContext.ParameterTemplate = GenerateInnerResult(null, null);
            if (!string.IsNullOrWhiteSpace(initContext.ParameterTemplate))
                initContext.Context.ImportNamespace(initContext.ParameterTemplate);
            return null;
        }

        public override object ProcessData(object data, object chained, object parent, Func<object, object, string> getInnerResult)
        {
            return null;
        }
    }
}