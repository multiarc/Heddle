using System;
using System.Collections.Generic;
using System.IO;
using Templates.Attributes;
using Templates.Core;
using Templates.Data;
using Templates.Exceptions;
using Templates.Extensions;
using Templates.Language;
using Templates.Mvc.Extensions;
using Templates.Runtime;

[assembly: ExportExtensions(typeof(ImportMvcExtension))]

namespace Templates.Mvc.Extensions {
    [ExtensionName("import")]
    public class ImportMvcExtension: AbstractExtension
    {
        public override ExType InitStart(InitContext initContext, ExType dataType, ExType chainedType, ExType parent) {
            IEnumerable<string> searched;
            TtlTemplate cached;
            base.InitStart(initContext, dataType, chainedType, parent);
            initContext.ParameterTemplate = GenerateInnerResult(null, null);
            var path = TtlViewEngine.Resolver.Search(initContext.ParameterTemplate, initContext.Context.ControllerName, TemplatePathType.Master,
                out searched, out cached);
            int outputCount = initContext.ParseContext.OutputChains.Length;
            using (var file = File.OpenText(path))
            {
                string document = file.ReadToEnd();
                DocumentParser.Parse(document, initContext.ParseContext, initContext.Context, true);
            }
            if (initContext.ParseContext.OutputChains.Length > outputCount)
                initContext.Context.CompileErrors.Add("The Defenitions template cannot contain output items".ToError(Position));
            return null;
        }

        public override object ProcessData(object data, object chained, object parent, Func<object, object, string> getInnerResult)
        {
            return null;
        }
    }
}