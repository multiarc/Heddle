using System;
using Templates.Attributes;
using Templates.Core;
using Templates.Data;

namespace Templates.Extensions {
    [Name ("using")]
    public class UsingExtension: AbstractExtension {
        public override ExType InitStart(InitContext initContext, ExType dataType, ExType chainedType)
        {
            if (initContext.Context == null)
                throw new ArgumentNullException(nameof(initContext.Context));
            base.InitStart(initContext, dataType, chainedType);
            initContext.ParameterTemplate = GetInnerResult(null, null);
            if (!string.IsNullOrWhiteSpace(initContext.ParameterTemplate))
                initContext.Context.ImportNamespace(initContext.ParameterTemplate);
            return null;
        }

        public override object ProcessData(object data, object chained)
        {
            return null;
        }
    }
}