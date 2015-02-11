using System;
using Templates.Attributes;
using Templates.Helpers;
using Templates.Runtime;

namespace Templates.Extensions {
    [Name ("import")]
    [Name ("using")]
    public class ImportExtension: AbstractExtension {
        public override Type InitializeInnerTemplate(string parameter, System.Type dataType, System.Type additionalType, DocumentContext context)
        {
            if (context == null)
                throw new ArgumentNullException("context");

            if (!string.IsNullOrWhiteSpace(parameter))
                context.ImportNamespace(parameter);
            return null;
        }
    }
}