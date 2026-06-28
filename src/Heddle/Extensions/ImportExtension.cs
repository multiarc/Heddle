using System.IO;
using Heddle.Attributes;
using Heddle.Core;
using Heddle.Data;
using Heddle.Language;

namespace Heddle.Extensions {
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
