using System;
using Templates.Attributes;
using Templates.Exceptions;
using Templates.Helpers;
using Templates.Runtime;

namespace Templates.Extensions {
    [Name ("model")]
    public class ModelExtension: AbstractExtension {
        public override Type InitializeInnerTemplate (string parameter, Type dataType, Type additionalType, CompileContext context)
        {
            if (context == null)
                throw new ArgumentNullException("context");
            if (!string.IsNullOrWhiteSpace(parameter)) {
                try {
                    Type modelType = ReflectionHelper.ResolveType(parameter, context.Namespaces);
                    if (modelType == null)
                        throw new TemplateInitException("Type cannot be determined. Please use Assembly Qualified Name.");

                    context.ModelType = modelType;
                }
                catch (Exception e) {
                    throw new TemplateInitException("Type cannot be determined. Please use Assembly Qualified Name.", e);
                }
            }
            return null;
        }
    }
}