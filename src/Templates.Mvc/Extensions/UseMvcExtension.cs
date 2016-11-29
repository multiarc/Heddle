using Microsoft.Extensions.DependencyInjection;
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
            var nullScope = Scope.Null;
            initContext.ParameterTemplate = GetInnerResult(ref nullScope);
            var engine = initContext.CompileScope.ServiceProvider.GetRequiredService<TtlViewEngine>();
            var path = engine.Resolver.Search(initContext.ParameterTemplate, initContext.CompileScope.CompileContext.ControllerName, TemplatePathType.Master,
                out searched, out cached);
            int outputCount = initContext.ParseContext.OutputChains.Count;
            using (var file = File.OpenText(path))
            {
                string document = file.ReadToEnd();
                DocumentParser.Parse(document, initContext.ParseContext, initContext.CompileScope.CompileContext, true);
            }
            if (initContext.ParseContext.OutputChains.Count > outputCount)
                initContext.CompileScope.CompileErrors.Add("The Defenitions template cannot contain output items".ToError(Position));
            return null;
        }

        public override object ProcessData(ref Scope scope)
        {
            return null;
        }
    }
}