using System;
using System.Collections.Generic;
using System.IO;
using Templates.Attributes;
using Templates.Data;
using Templates.Exceptions;
using Templates.Extensions;
using Templates.Language;
using Templates.Mvc.Extensions;
using Templates.Runtime;

[assembly: ExportExtensions(typeof(UseMvcExtension))]

namespace Templates.Mvc.Extensions {
    public class UseMvcExtension: ImportExtension {
        public override ExType InitStart(string parameterTemplate, ExType dataType, ExType chainedType, CompileContext context, ParseContext parseContext) {
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