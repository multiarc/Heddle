using Heddle.Attributes;
using Heddle.Core;
using Heddle.Data;

namespace Heddle.Extensions
{
    /// <summary>
    /// <para>Continues a branch set: renders its body when no earlier branch of the set fired and its own
    /// condition is truthy, then publishes the updated <see cref="BranchState"/>. When an earlier branch
    /// already fired, it renders nothing and leaves the state unchanged.</para>
    /// <para>With no set open it behaves exactly like <c>@if</c> (starts a set) and draws <c>HED3002</c>
    /// where statically visible. Stateless — safe for concurrent renders.</para>
    /// </summary>
    [ExtensionName("elif")]
    [ExtensionName("elseif")]
    [ScopeChannel]
    [BranchRole(BranchRole.Continuation)]
    public class ElifExtension : AbstractExtension
    {
        public override ExType InitStart(InitContext initContext, ExType dataType, ExType chainedType, ExType parent)
        {
            return base.InitStart(initContext, parent, chainedType, null);
        }

        public override object ProcessData(in Scope scope)
        {
            if (scope.TryReadBranch(out var state) && state.Satisfied)
                return string.Empty;

            bool truthy = BranchCondition.IsTruthy(scope.ModelData);
            scope.PublishBranch(new BranchState(truthy));

            if (truthy)
            {
                var parentData = scope.Parent();
                return GetInnerResult(parentData);
            }

            return string.Empty;
        }

        public override void RenderData(in Scope scope)
        {
            if (scope.TryReadBranch(out var state) && state.Satisfied)
                return;

            bool truthy = BranchCondition.IsTruthy(scope.ModelData);
            scope.PublishBranch(new BranchState(truthy));

            if (truthy)
            {
                var parentData = scope.Parent();
                RenderInnerResult(parentData);
            }
        }
    }
}
