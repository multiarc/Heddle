using System;
using System.Linq;
using Templates.Attributes;
using Templates.Exceptions;
using Templates.Helpers;
using Templates.Runtime;

namespace Templates.Extensions {
    [Name ("model")]
    public class ModelExtension: AbstractExtension {
        public override Type InitializeInnerTemplate(string parameter, System.Type dataType, System.Type additionalType, DocumentContext context)
        {
            if (context == null)
                throw new ArgumentNullException("context");
            if (!string.IsNullOrWhiteSpace(parameter)) {
                try {
                    System.Type modelType = ReflectionHelper.ResolveType(parameter, context.Namespaces.ToArray());
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