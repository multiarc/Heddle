using System;
using Templates.Attributes;
using Templates.Core.CompilerServices;

namespace Templates.Extensions {
    [Name ("import")]
    [Name ("using")]
    public class ImportExtension: AbstractExtension {
        public override Type InitializeInnerTemplate (string parameter, Type dataType, Type additionalType, CompileContext context)
        {
            if (context == null)
                throw new ArgumentNullException("context");

            if (!string.IsNullOrWhiteSpace(parameter))
                context.ImportNamespace(parameter);
            return null;
        }
    }
}