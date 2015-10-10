using Templates.Attributes;
using Templates.Core;
using Templates.Data;
using Templates.Models;
using Templates.Strings;

namespace Templates.Extensions
{
    [Name("for")]
    [ChainedType(typeof(ForModel))]
    public class ForIndexExtension : AbstractExtension
    {
        public override ExType InitStart(InitContext initContext, ExType dataType, ExType chainedType)
        {
            return base.InitStart(initContext, dataType, typeof (int));
        }

        public override object ProcessData(object data, object chained)
        {
            if (!(chained is ForModel))
            {
                return string.Empty;
            }
            var model = (ForModel) chained;
            ExStringBuilder builder = new ExStringBuilder();
            for (int i = model.Start ?? 0; i < model.Last; i += model.Step ?? 1)
            {
                builder.Append(GetInnerResult(data, i));
            }
            return builder.ToString();
        }
    }
}