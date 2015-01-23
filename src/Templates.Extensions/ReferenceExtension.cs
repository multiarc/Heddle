using System;
using Templates.Attributes;
using Templates.Helpers;
using Templates.Runtime;

namespace Templates.Extensions {
    [Name ("reference")]
    public class ReferenceExtension: AbstractExtension {
        public override TypeReference InitializeInnerTemplate (string parameter, Type dataType, Type additionalType, CompileContext context)
        {
            if (context == null)
                throw new ArgumentNullException("context");

            if (!string.IsNullOrWhiteSpace(parameter))
                context.AddReference(parameter);
            return null;
        }
    }
}