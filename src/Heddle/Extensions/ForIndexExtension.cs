using Heddle.Attributes;
using Heddle.Core;
using Heddle.Data;
using Heddle.Strings;

namespace Heddle.Extensions
{
    [ExtensionName("for")]
    [DataType(typeof(Heddle.Models.Range))]
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
            if (scope.ModelData is Heddle.Models.Range model)
            {
                start = model.Start;
                last = model.Last;
                step = model.Step;
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

            // C1-R4: one-time probe type-test, then a per-iteration deadline check (the value path accumulates before
            // it reaches the sink, so the deadline is its only bound).
            var probe = scope.Renderer as IBudgetProbe;
            var builder = new ExStringBuilder();
            for (int i = start; i < last; i += step)
            {
                probe?.TickDeadline();
                var parentData = scope.Parent(i);
                builder.Append(GetInnerResult(parentData));
            }

            return builder.ToString();
        }

        public override void RenderData(in Scope scope)
        {
            int start, last, step;
            if (scope.ModelData is Heddle.Models.Range model)
            {
                start = model.Start;
                last = model.Last;
                step = model.Step;
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

            // C1-R4: type-test the held renderer for the budget probe once, before the loop, then enforce the
            // wall-clock deadline per iteration so a zero-output loop (no render op) still terminates.
            var probe = scope.Renderer as IBudgetProbe;
            for (int i = start; i < last; i += step)
            {
                probe?.TickDeadline();
                var parentData = scope.Parent(i);
                RenderInnerResult(parentData);
            }
        }
    }
}