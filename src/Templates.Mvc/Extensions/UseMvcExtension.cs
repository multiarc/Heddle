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
            initContext.ParameterTemplate = GetInnerResult(Scope.Null);
            var path = TtlViewEngine.Resolver.Search(initContext.ParameterTemplate, initContext.CompileScope.CompileContext.ControllerName, TemplatePathType.Master,
                out searched, out cached);
            int outputCount = initContext.ParseContext.OutputChains.Length;
            using (var file = File.OpenText(path))
            {
                string document = file.ReadToEnd();
                DocumentParser.Parse(document, initContext.ParseContext, initContext.CompileScope.CompileContext, true);
            }
            if (initContext.ParseContext.OutputChains.Length > outputCount)
                initContext.CompileScope.CompileErrors.Add("The Defenitions template cannot contain output items".ToError(Position));
            return null;
        }

        public override object ProcessData(Scope scope)
        {
            return null;
        }
    }
}