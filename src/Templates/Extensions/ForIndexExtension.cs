using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Templates.Attributes;
using Templates.Core;
using Templates.Data;
using Templates.Language;
using Templates.Models;
using Templates.Runtime;
using Templates.Strings;

namespace Templates.Extensions
{
    [Name("for")]
    [ChainedType(typeof(ForModel))]
    public class ForIndexExtension : AbstractExtension
    {
        public override ExType InitStart(string parameterTemplate, ExType dataType, ExType chainedType,
            CompileContext context,
            ParseContext parseContext)
        {
            return base.InitStart(parameterTemplate, dataType, typeof (int), context, parseContext);
        }

        public override object ProcessData(object value, object chainedResult)
        {
            if (!(chainedResult is ForModel))
            {
                return string.Empty;
            }
            var model = (ForModel) chainedResult;
            ExStringBuilder builder = new ExStringBuilder();
            for (int i = model.Start ?? 0; i < model.Last; i += model.Step ?? 1)
            {
                builder.Append(GetInnerResult(value, i));
            }
            return builder.ToString();
        }
    }
}