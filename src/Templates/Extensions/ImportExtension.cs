using System;
using System.IO;
using Templates.Attributes;
using Templates.Core;
using Templates.Data;
using Templates.Exceptions;
using Templates.Language;
using Templates.Strings.Core;

namespace Templates.Extensions {
    [ExtensionName("import")]
    public class ImportExtension:AbstractExtension {
        public override ExType InitStart(InitContext initContext, ExType dataType, ExType chainedType, ExType parent)
        {
            base.InitStart(initContext, dataType, chainedType, parent);
            var nullScope = Scope.Null;
            initContext.ParameterTemplate = GetInnerResult(ref nullScope);
            //int outputCount = initContext.ParseContext.OutputChains.Count;
            using (var file = File.OpenText(Path.Combine(initContext.CompileScope.Options.RootPath, initContext.ParameterTemplate)))
            {
                string document = file.ReadToEnd();
                DocumentParser.Parse(document, initContext.ParseContext, initContext.CompileScope.CompileContext/*, true*/);
            }
            //if (initContext.ParseContext.OutputChains.Count > outputCount)
            //initContext.CompileScope.CompileErrors.Add("The Defenitions template cannot contain output items".ToError(Position));
            return null;
        }

        public override object ProcessData(ref Scope scope)
        {
            return null;
        }
    }
}
