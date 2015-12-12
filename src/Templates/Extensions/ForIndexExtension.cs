using System;
using Templates.Attributes;
using Templates.Core;
using Templates.Data;
using Templates.Models;
using Templates.Strings;

namespace Templates.Extensions
{
    [ExtensionName("for")]
    [ChainedType(typeof(ForModel))]
    public class ForIndexExtension : AbstractExtension
    {
        public override ExType InitStart(InitContext initContext, ExType dataType, ExType chainedType, ExType parent)
        {
            return base.InitStart(initContext, parent, typeof (int), null);
        }

        public override object ProcessData(object data, object chained, object parent, Func<object, object, string> getInnerResult)
        {
            if (!(data is ForModel))
            {
                return string.Empty;
            }
            var model = (ForModel)data;
            ExStringBuilder builder = new ExStringBuilder();
            for (int i = model.Start ?? 0; i < model.Last; i += model.Step ?? 1)
            {
                builder.Append(getInnerResult(parent, i));
            }
            return builder.ToString();
        }
    }
}