using Heddle.Attributes;
using Heddle.Core;
using Heddle.Data;
using Heddle.Exceptions;

namespace Heddle.Extensions
{
    /// <summary>
    /// <para>Terminal branch: renders its body when no earlier branch of the set fired, then clears the set
    /// state (publishes nothing). With no set open it is the "no matching <c>@if</c>" error — <c>HED3003</c>
    /// at compile time where statically visible, <see cref="TemplateProcessingException"/> otherwise.</para>
    /// <para>Its parameter (if any) is evaluated by normal item dispatch and ignored (<c>HED3004</c>).
    /// Stateless — safe for concurrent renders.</para>
    /// </summary>
    [ExtensionName("else")]
    [ScopeChannel]
    public class ElseExtension : AbstractExtension
    {
        internal const string NoMatchingIfMessage = "'@else' has no matching '@if' in this scope.";

        public override ExType InitStart(InitContext initContext, ExType dataType, ExType chainedType, ExType parent)
        {
            return base.InitStart(initContext, parent, chainedType, null);
        }

        public override object ProcessData(in Scope scope)
        {
            if (!scope.TryReadBranch(out var state))
                throw new TemplateProcessingException(NoMatchingIfMessage);

            scope.ClearBranch();
            if (state.Satisfied)
                return string.Empty;

            var parentData = scope.Parent();
            return GetInnerResult(parentData);
        }

        public override void RenderData(in Scope scope)
        {
            if (!scope.TryReadBranch(out var state))
                throw new TemplateProcessingException(NoMatchingIfMessage);

            scope.ClearBranch();
            if (state.Satisfied)
                return;

            var parentData = scope.Parent();
            RenderInnerResult(parentData);
        }
    }
}
