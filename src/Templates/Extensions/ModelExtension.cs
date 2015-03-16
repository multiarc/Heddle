using System;
using System.Linq;
using Templates.Attributes;
using Templates.Core;
using Templates.Data;
using Templates.Exceptions;
using Templates.Helpers;
using Templates.Language;
using Templates.Runtime;

namespace Templates.Extensions {
    [Name ("model")]
    public class ModelExtension: AbstractExtension {
        public override ExType InitStart(string parameterTemplate, ExType dataType, ExType chainedType, CompileContext context, ParseContext parseContext)
        {
            if (context == null)
                throw new ArgumentNullException("context");
            base.InitStart(parameterTemplate, dataType, chainedType, context, parseContext);
            parameterTemplate = GetInnerResult(null, null);
            if (!string.IsNullOrWhiteSpace(parameterTemplate)) {
                try {
                    ExType modelType = new ExType(parameterTemplate, context.Namespaces.ToArray());
                    context.ModelType = modelType;
                }
                catch (Exception e) {
                    throw new TemplateInitException("Type cannot be determined. Please use Assembly Qualified Name.", e);
                }
            }
            return null;
        }

        public override object ProcessData(object value, object chainedResult)
        {
            return null;
        }
    }
}