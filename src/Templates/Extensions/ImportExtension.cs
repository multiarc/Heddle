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
            initContext.ParameterTemplate = GenerateInnerResult(null, null);
            int outputCount = initContext.ParseContext.OutputChains.Length;
            using (var file = File.OpenText(Path.Combine(initContext.Context.Options.RootPath, initContext.ParameterTemplate)))
            {
                string document = file.ReadToEnd();
                DocumentParser.Parse(document, initContext.ParseContext, initContext.Context, true);
            }
            if (initContext.ParseContext.OutputChains.Length > outputCount)
                initContext.Context.CompileErrors.Add("The Defenitions template cannot contain output items".ToError(default(BlockPosition)));
            return null;
        }

        public override object ProcessData(object data, object chained, object parent, Func<object, object, string> getInnerResult)
        {
            return null;
        }
    }
}
