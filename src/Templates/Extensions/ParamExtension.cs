using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Templates.Attributes;
using Templates.Language;
using Templates.Runtime;

namespace Templates.Extensions {

    [Name("param")]
    public class ParamExtension: AbstractHtmlExtension {
        public override Type InitStart(string parameterTemplate, Type dataType, Type chainedType, CompileContext context, ParseContext parseContext) {
            return dataType;
        }
        protected override object ProcessDataInternal(object value, object chainedResult) {
            return value;
        }
    }
}
