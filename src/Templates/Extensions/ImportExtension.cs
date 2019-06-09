using System.IO;
using Templates.Attributes;
using Templates.Core;
using Templates.Data;
using Templates.Language;

namespace Templates.Extensions {
    [ExtensionName("import")]
    public class ImportExtension:AbstractExtension {
        public override ExType InitStart(InitContext initContext, ExType dataType, ExType chainedType, ExType parent)
        {
            base.InitStart(initContext, dataType, chainedType, parent);
            var nullScope = Scope.Null;
            initContext.ParameterTemplate = GetInnerResult(nullScope);
            //int outputCount = initContext.ParseContext.OutputChains.Count;
            using (var file = File.OpenText(Path.Combine(initContext.CompileScope.Options.RootPath, initContext.ParameterTemplate)))
            {
                var document = file.ReadToEnd();
                DocumentParser.Parse(document, initContext.ParseContext, initContext.CompileScope.CompileContext/*, true*/);
            }
            return null;
        }

        public override object ProcessData(in Scope scope)
        {
            return null;
        }

        public override void RenderData(in Scope scope)
        {
        }
    }
}
