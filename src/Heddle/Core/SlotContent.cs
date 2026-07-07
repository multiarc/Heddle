using Heddle.Data;

namespace Heddle.Core
{
    /// <summary>
    /// The D11 slot-projection carrier: the outer <see cref="DefinitionBaseExtension"/> (whose funnel renders the
    /// invocation-site caller content) plus the invocation scope captured at the definition call. A slot-mode
    /// <c>@out(expr)</c> renders the caller body through the outer extension under
    /// <c>InvocationScope.Model(value)</c> — model = the slot value, <c>PropsData</c> = the invocation-site frame.
    /// Created once per slot-mode invocation, confined to that render lineage, never cached.
    /// </summary>
    internal sealed class SlotContent
    {
        internal readonly DefinitionBaseExtension Outer;
        internal readonly Scope InvocationScope;

        internal SlotContent(DefinitionBaseExtension outer, in Scope invocationScope)
        {
            Outer = outer;
            InvocationScope = invocationScope;
        }
    }
}
