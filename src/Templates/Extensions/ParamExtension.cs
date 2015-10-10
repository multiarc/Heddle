using Templates.Attributes;
using Templates.Core;
using Templates.Data;

namespace Templates.Extensions {

    [Name("param")]
    public class ParamExtension: AbstractExtension {
        public override ExType InitStart(InitContext initContext, ExType dataType, ExType chainedType)
        {
            return dataType;
        }
        public override object ProcessData(object data, object chained) {
            return data;
        }
    }
}