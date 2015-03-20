using Templates.Attributes;
using Templates.Core;
using Templates.Data;
using Templates.Language;
using Templates.Runtime;

namespace Templates.Extensions {

    [Name("param")]
    public class ParamExtension: AbstractExtension {
        public override ExType InitStart(string parameterTemplate, ExType dataType, ExType chainedType, CompileContext context, ParseContext parseContext) {
            return dataType;
        }
        public override object ProcessData(object value, object chainedResult) {
            return value;
        }
    }
}