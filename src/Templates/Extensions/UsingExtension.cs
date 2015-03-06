using System;
using Templates.Attributes;
using Templates.Core;
using Templates.Language;
using Templates.Runtime;

namespace Templates.Extensions {
    [Name ("using")]
    public class UsingExtension: AbstractExtension {
        public override Type InitStart(string parameterTemplate, Type dataType, Type chainedType, CompileContext context, ParseContext parseContext)
        {
            if (context == null)
                throw new ArgumentNullException("context");

            if (!string.IsNullOrWhiteSpace(parameterTemplate))
                context.ImportNamespace(parameterTemplate);
            return null;
        }

        public override object ProcessData(object value, object chainedResult)
        {
            return null;
        }
    }
}