using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Templates.Attributes;
using Templates.Core;
using Templates.Data;
using Templates.Exceptions;
using Templates.Language;
using Templates.Runtime;

namespace Templates.Extensions {
    [Name("using")]
    public class UseExtension:AbstractExtension {
        public override Type InitStart(string parameterTemplate, Type dataType, Type chainedType, CompileContext context,
            ParseContext parseContext)
        {
            FileReader reader = new FileReader(new TemplateOptions(context.Options)
            {
                TemplateName = parameterTemplate
            });
            string document = reader.ReadEntireFile();
            int outputCount = parseContext.OutputChains.Length;
            DocumentParser.Parse(document, parseContext);
            if (parseContext.OutputChains.Length > outputCount)
                throw new TemplateParseException("The Defenitions template cannot contain output items");
            return null;
        }

        public override object ProcessData(object value, object chainedResult)
        {
            return null;
        }
    }
}
