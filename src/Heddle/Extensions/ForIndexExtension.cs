using Heddle.Attributes;
using Heddle.Core;
using Heddle.Data;
using Heddle.Models;
using Heddle.Strings;

namespace Heddle.Extensions
{
    [ExtensionName("for")]
    [DataType(typeof(ForModel))]
    [DataType(typeof(int))]   // @for(5) / @for(Count): iterate 0…n−1
    public class ForIndexExtension : AbstractExtension
    {
        public override ExType InitStart(InitContext initContext, ExType dataType, ExType chainedType, ExType parent)
        {
            return base.InitStart(initContext, parent, typeof(int), null);
        }

        public override object ProcessData(in Scope scope)
        {
            int start, last, step;
            if (scope.ModelData is ForModel model)
            {
                start = model.Start ?? 0;
                last = model.Last;
                step = model.Step ?? 1;
            }
            else if (scope.ModelData is int count)
            {
                start = 0;
                last = count;
                step = 1;
            }
            else
            {
                return string.Empty;
            }

            var builder = new ExStringBuilder();
            for (int i = start; i < last; i += step)
            {
                var parentData = scope.Parent(i);
                builder.Append(GetInnerResult(parentData));
            }

            return builder.ToString();
        }

        public override void RenderData(in Scope scope)
        {
            int start, last, step;
            if (scope.ModelData is ForModel model)
            {
                start = model.Start ?? 0;
                last = model.Last;
                step = model.Step ?? 1;
            }
            else if (scope.ModelData is int count)
            {
                start = 0;
                last = count;
                step = 1;
            }
            else
            {
                return;
            }

            for (int i = start; i < last; i += step)
            {
                var parentData = scope.Parent(i);
                RenderInnerResult(parentData);
            }
        }
    }
}