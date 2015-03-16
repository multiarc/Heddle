using System;
using System.Collections.Generic;
using System.IO;
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
    [Name("import")]
    public class ImportExtension:AbstractExtension {
        public override ExType InitStart(string parameterTemplate, ExType dataType, ExType chainedType, CompileContext context,
            ParseContext parseContext)
        {
            base.InitStart(parameterTemplate, dataType, chainedType, context, parseContext);
            parameterTemplate = GetInnerResult(null, null);
            int outputCount = parseContext.OutputChains.Length;
            using (var file = File.OpenText(Path.Combine(context.Options.RootPath, parameterTemplate))) {
                string document = file.ReadToEnd();
                DocumentParser.Parse(document, parseContext, true);
            }
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
