using System;
using Templates.Attributes;
using Templates.Language;
using Templates.Runtime;

namespace Templates.Extensions {
    [Name ("import")]
    [Name ("using")]
    public class ImportExtension: AbstractHtmlExtension {
        public override Type InitStart(string parameterTemplate, Type dataType, Type chainedType, CompileContext context, ParseContext parseContext)
        {
            if (context == null)
                throw new ArgumentNullException("context");

            if (!string.IsNullOrWhiteSpace(parameterTemplate))
                context.ImportNamespace(parameterTemplate);
            return null;
        }

        protected override object ProcessDataInternal(object value, object chainedResult)
        {
            return null;
        }
    }
}