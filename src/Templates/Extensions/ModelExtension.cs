using System;
using System.Linq;
using Templates.Attributes;
using Templates.Core;
using Templates.Data;
using Templates.Exceptions;

namespace Templates.Extensions {
    [ExtensionName ("model")]
    public class ModelExtension: AbstractExtension {
        public override ExType InitStart(InitContext initContext, ExType dataType, ExType chainedType, ExType parent)
        {
            if (initContext.Context == null)
                throw new ArgumentNullException(nameof(initContext.Context));
            base.InitStart(initContext, dataType, chainedType, parent);
            initContext.ParameterTemplate = GetInnerResult(null, null);
            if (!string.IsNullOrWhiteSpace(initContext.ParameterTemplate)) {
                try {
                    ExType modelType = new ExType(initContext.ParameterTemplate, initContext.Context.Namespaces.ToArray());
                    initContext.Context.ModelType = modelType;
                }
                catch (InvalidOperationException e) {
                    initContext.Context.CompileErrors.Add(e.ToError(Position));
                }
            }
            return null;
        }

        public override object ProcessData(object data, object chained, object parent)
        {
            return null;
        }
    }
}