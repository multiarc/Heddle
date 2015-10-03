using System.IO;
using Templates.Attributes;
using Templates.Core;
using Templates.Data;
using Templates.Exceptions;
using Templates.Language;
using Templates.Runtime;

namespace Templates.Extensions {
    [Name("import")]
    public class ImportExtension:AbstractExtension {
        public override ExType InitStart(InitContext initContext, ExType dataType, ExType chainedType)
        {
            base.InitStart(initContext, dataType, chainedType);
            initContext.ParameterTemplate = GetInnerResult(null, null);
            int outputCount = initContext.ParseContext.OutputChains.Length;
            using (var file = File.OpenText(Path.Combine(initContext.Context.Options.RootPath, initContext.ParameterTemplate))) {
                string document = file.ReadToEnd();
                DocumentParser.Parse(document, initContext.ParseContext, true);
            }
            if (initContext.ParseContext.OutputChains.Length > outputCount)
                throw new TemplateCompileException("The Defenitions template cannot contain output items".ToError());
            return null;
        }

        public override object ProcessData(object data, object chained)
        {
            return null;
        }
    }
}
