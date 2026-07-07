using Heddle.Attributes;
using Heddle.Core;
using Heddle.Data;

namespace Heddle.Extensions
{
    /// <summary>
    /// <para>Condition template</para>
    /// <para>Optional parameter represents string to show if condition is false</para>
    /// </summary>
    [ExtensionName("ifnot")]
    public class IfNotExtension : AbstractExtension
    {
        public override ExType InitStart(InitContext initContext, ExType dataType, ExType chainedType, ExType parent)
        {
            return base.InitStart(initContext, parent, chainedType, null);
        }

        public override object ProcessData(in Scope scope)
        {
            bool satisfied = !BranchCondition.IsTruthy(scope.ModelData);
            scope.PublishBranch(new BranchState(satisfied));

            if (satisfied)
            {
                var parentData = scope.Parent();
                return GetInnerResult(parentData);
            }

            return string.Empty;
        }

        public override void RenderData(in Scope scope)
        {
            bool satisfied = !BranchCondition.IsTruthy(scope.ModelData);
            scope.PublishBranch(new BranchState(satisfied));

            if (satisfied)
            {
                var parentData = scope.Parent();
                RenderInnerResult(parentData);
            }
        }
    }
}