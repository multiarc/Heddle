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
using Templates.Extensions;
using Templates.Language;
using Templates.Runtime;

namespace Templates.Mvc {
    [Name("using")]
    public class UseMvcExtension: UseExtension {
        public override Type InitStart(string parameterTemplate, Type dataType, Type chainedType, CompileContext context, ParseContext parseContext) {
            IEnumerable<string> searched;
            TtlTemplate cached;
            var path = TtlViewEngine.Resolver.Search(parameterTemplate, context.ControllerName, TemplatePathType.Master,
                out searched, out cached);
            int outputCount = parseContext.OutputChains.Length;
            using (var file = File.OpenText(path))
            {
                string document = file.ReadToEnd();
                DocumentParser.Parse(document, parseContext);
            }
            if (parseContext.OutputChains.Length > outputCount)
                throw new TemplateParseException("The Defenitions template cannot contain output items");
            return null;
        }
    }
}
