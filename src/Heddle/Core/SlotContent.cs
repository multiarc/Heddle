using Heddle.Data;

namespace Heddle.Core
{
    /// <summary>
    /// The slot-projection carrier: the outer <see cref="DefinitionBaseExtension"/> (whose funnel renders the
    /// invocation-site caller content) plus the invocation scope captured at the definition call. A slot-mode
    /// <c>@out(expr)</c> renders the caller body through the outer extension under
    /// <c>InvocationScope.Model(value)</c> — model = the slot value, <c>PropsData</c> = the invocation-site frame.
    /// Created once per slot-mode invocation, confined to that render lineage, never cached.
    /// <para>The type stays internal; <see cref="ISlotContent"/> is the face an out-of-assembly projection sees,
    /// implemented explicitly so the engine's own funnel keeps reaching <see cref="Outer"/> directly.</para>
    /// </summary>
    internal sealed class SlotContent : ISlotContent
    {
        internal readonly DefinitionBaseExtension Outer;
        internal readonly Scope InvocationScope;

        internal SlotContent(DefinitionBaseExtension outer, in Scope invocationScope)
        {
            Outer = outer;
            InvocationScope = invocationScope;
        }

        Scope ISlotContent.InvocationScope => InvocationScope;

        string ISlotContent.RenderCallerContent(in Scope scope) => Outer.RenderCallerContent(scope);

        void ISlotContent.RenderCallerContentInto(in Scope scope) => Outer.RenderCallerContentInto(scope);
    }
}
