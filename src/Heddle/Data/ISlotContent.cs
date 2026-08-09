namespace Heddle.Data
{
    /// <summary>
    /// <para>The caller content a slot-declaring definition hands to the slot projections inside its body. A
    /// definition invoked as <c>&lt;name(out:: T)&gt;</c> does not pre-render the content its call site passed it;
    /// the engine installs one of these on the scope (<see cref="Scope.SlotCarrier"/>) instead, and each
    /// <c>[SlotProjection]</c> extension in the body renders that content through it — once per projection, against
    /// a model the projection chooses, which is what lets one caller body be rendered per element of a loop.</para>
    /// <para>The projection scope to render against is <see cref="InvocationScope"/> re-modelled on the slot value
    /// (<c>InvocationScope.Model(slotValue)</c>): model = the value the projection was passed, props and caller
    /// frame = the definition's invocation site. That pairing is what makes the caller's content see the value it
    /// was written for while its <c>::</c>-rooted and prop references still resolve where it was written.</para>
    /// <para>Only the engine implements this and only the engine creates instances; an extension consumes one it
    /// finds on the scope. Instances are confined to a single render lineage and must not be stored beyond the
    /// call that received them, exactly as <see cref="Scope"/> must not.</para>
    /// </summary>
    public interface ISlotContent
    {
        /// <summary>
        /// <para>The scope captured where the definition was invoked. Re-model it on the slot value to get the
        /// scope the caller's content is rendered against; do not render against it unchanged, which would show
        /// the caller's content the invocation-site model instead of the projected value.</para>
        /// </summary>
        Scope InvocationScope { get; }

        /// <summary>
        /// <para>Renders the caller's content to a string — the <c>ProcessData</c> half of a projection. Never
        /// returns <c>null</c>; content that renders nothing renders the empty string.</para>
        /// </summary>
        /// <param name="scope">The projection scope, normally <see cref="InvocationScope"/> re-modelled on the
        /// slot value.</param>
        /// <returns>The rendered caller content.</returns>
        string RenderCallerContent(in Scope scope);

        /// <summary>
        /// <para>Renders the caller's content straight into <paramref name="scope"/>'s renderer — the
        /// <c>RenderData</c> half of a projection, and the allocation-free one: prefer it wherever the projection
        /// does not need the content as a value.</para>
        /// </summary>
        /// <param name="scope">The projection scope, normally <see cref="InvocationScope"/> re-modelled on the
        /// slot value.</param>
        void RenderCallerContentInto(in Scope scope);
    }
}
