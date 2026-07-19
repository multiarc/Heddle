using System;
using System.Runtime.CompilerServices;

namespace Heddle.Data
{
    /// <summary>
    /// <para>Per-node render data view. A declared <c>readonly struct</c>: every member is a pure transform,
    /// instances are passed by <c>in</c>, and a <see cref="Scope"/> must not be stored beyond the call that
    /// received it (frames are single-threaded per render lineage).</para>
    /// </summary>
    public readonly struct Scope
    {
        public readonly object ModelData;
        public readonly object ChainedData;
        public readonly object ParentModelData;
        public readonly object CallerData;
        public readonly IScopeRenderer Renderer;
        internal readonly object RootData;
        internal readonly ScopeLocals Locals;

        /// <summary>
        /// <para>Phase 5 (D7) — the per-invocation props carriage of a props-declaring definition body; a shared
        /// frozen array for all-constant call sites (zero alloc) or a per-invocation clone otherwise. <c>null</c>
        /// for every prop-less execution (the common case). Copied unchanged by all transforms; replaced only by
        /// <see cref="WithProps"/>. Never written after construction and never exposed publicly.</para>
        /// </summary>
        internal readonly object[] PropsData;

        /// <summary>
        /// <para>Phase 5 — the projected-content carrier of a slot-declaring definition body (a
        /// <c>SlotContent</c>, typed <see cref="object"/> here to avoid a Core→Data dependency cycle). <c>null</c>
        /// for every non-slot execution. Preserved by all transforms so a slot-mode <c>@out(expr)</c> nested in a
        /// body (e.g. inside <c>@list</c>) can still reach it; installed only by <see cref="WithSlot"/>.</para>
        /// <para>Amends phase 5 D11's "carrier on the chained channel" — the chained channel is overwritten by
        /// looping extensions (<c>@list</c>/<c>@for</c> thread the index there), so it cannot reach an
        /// <c>@out</c> nested in a loop body; a preserved field can (see the amendments ledger).</para>
        /// </summary>
        internal readonly object SlotCarrier;

        /// <summary>
        /// <para>Phase 8 (D2) — the per-invocation bound values of a parameter-declaring extension's <c>[Prop]</c>
        /// layout: the shared frozen array for all-constant call sites (zero alloc) or a per-invocation clone.
        /// <c>null</c> for every parameter-less execution (the common case). Copied unchanged by all transforms;
        /// installed only by <see cref="WithExtensionParameters"/>. Separate from <see cref="PropsData"/> so an
        /// extension's caller content still reads the enclosing definition's props by index.</para>
        /// </summary>
        internal readonly object[] ExtensionParameterValues;

        /// <summary>
        /// <para>Phase 8 (D2) — the immutable per-call-site name→index map for
        /// <see cref="TryGetParameter"/>/<see cref="GetParameter"/>; built once at bind, shared across renders and
        /// threads. <c>null</c> when no parameter frame is active.</para>
        /// </summary>
        internal readonly ExtensionParameterMap ExtensionParameters;

        private const string NoFrameMessage =
            "No local context frame is available here — mark the extension with [ScopeChannel] so the compiler " +
            "provisions one for bodies that contain it.";

        internal Scope(object root, object data, object model, object chained, IScopeRenderer renderer,
            object parent = null, ScopeLocals locals = null, object[] props = null, object slot = null,
            object[] extensionParameterValues = null, ExtensionParameterMap extensionParameters = null)
        {
            RootData = root;
            ModelData = model;
            ChainedData = chained;
            ParentModelData = parent;
            CallerData = data;
            Renderer = renderer;
            Locals = locals;
            PropsData = props;
            SlotCarrier = slot;
            ExtensionParameterValues = extensionParameterValues;
            ExtensionParameters = extensionParameters;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly Scope Parent()
        {
            return new Scope(RootData, CallerData, ParentModelData, ChainedData, Renderer, null, Locals, PropsData, SlotCarrier, ExtensionParameterValues, ExtensionParameters);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly Scope Parent(object chained)
        {
            return new Scope(RootData, CallerData, ParentModelData, chained, Renderer, null, Locals, PropsData, SlotCarrier, ExtensionParameterValues, ExtensionParameters);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly Scope Chain(object chained)
        {
            return new Scope(RootData, CallerData, ModelData, chained, Renderer, ModelData, Locals, PropsData, SlotCarrier, ExtensionParameterValues, ExtensionParameters);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly Scope Model(object model)
        {
            return new Scope(RootData, CallerData, model, ChainedData, Renderer, ModelData, Locals, PropsData, SlotCarrier, ExtensionParameterValues, ExtensionParameters);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly Scope Model(object model, object chained)
        {
            return new Scope(RootData, CallerData, model, chained, Renderer, ModelData, Locals, PropsData, SlotCarrier, ExtensionParameterValues, ExtensionParameters);
        }

        public readonly Scope RenderProxy(IScopeRenderer renderer)
        {
            return new Scope(RootData, CallerData, ModelData, ChainedData, renderer, ParentModelData, Locals, PropsData, SlotCarrier, ExtensionParameterValues, ExtensionParameters);
        }

        /// <summary>
        /// Returns a copy with only the local-context frame replaced (all data fields carried over).
        /// Called only by the frame-installation points (root generate and the body-execution funnel).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal readonly Scope WithLocals(ScopeLocals locals)
        {
            return new Scope(RootData, CallerData, ModelData, ChainedData, Renderer, ParentModelData, locals, PropsData, SlotCarrier, ExtensionParameterValues, ExtensionParameters);
        }

        /// <summary>
        /// Returns a copy with only the props carriage replaced (all other fields carried over). Called only by
        /// the props-installation point (<c>DefinitionBaseExtension</c>). See D7.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal readonly Scope WithProps(object[] props)
        {
            return new Scope(RootData, CallerData, ModelData, ChainedData, Renderer, ParentModelData, Locals, props, SlotCarrier, ExtensionParameterValues, ExtensionParameters);
        }

        /// <summary>
        /// Returns a copy with only the slot carrier replaced (all other fields carried over). Called only by
        /// the slot-installation point (<c>DefinitionBaseExtension</c> in slot mode). See D11.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal readonly Scope WithSlot(object slot)
        {
            return new Scope(RootData, CallerData, ModelData, ChainedData, Renderer, ParentModelData, Locals, PropsData, slot, ExtensionParameterValues, ExtensionParameters);
        }

        /// <summary>
        /// Returns a copy with only the extension-parameter frame replaced (all other fields carried over).
        /// Called only by the parameter-installation point (<c>ExtensionParameterCarrier</c>). Phase 8 D2.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal readonly Scope WithExtensionParameters(object[] values, ExtensionParameterMap map)
        {
            return new Scope(RootData, CallerData, ModelData, ChainedData, Renderer, ParentModelData, Locals, PropsData, SlotCarrier, values, map);
        }

        /// <summary>
        /// <para>Reads a declared extension parameter by name (phase 8). Returns <c>false</c> (never throws) when
        /// the current scope carries no parameter frame or <paramref name="name"/> was not declared by a
        /// <c>[Prop]</c> on the rendering extension.</para>
        /// </summary>
        /// <param name="name">The declared parameter name. Must not be <c>null</c>.</param>
        /// <param name="value">The bound value (possibly <c>null</c> for a null default), or <c>null</c> when the
        /// method returns <c>false</c>.</param>
        /// <returns><c>true</c> when the active frame declares <paramref name="name"/>.</returns>
        public bool TryGetParameter(string name, out object value)
        {
            if (name == null)
                throw new ArgumentNullException(nameof(name));

            value = null;
            if (ExtensionParameters == null || !ExtensionParameters.TryGetIndex(name, out var index))
                return false;

            value = ExtensionParameterValues[index];
            return true;
        }

        /// <summary>
        /// <para>Reads a declared extension parameter by name (phase 8), returning its bound value — which may be
        /// <c>null</c> for a null default. Throws <see cref="ArgumentException"/> when <paramref name="name"/> is
        /// not a declared parameter of the extension whose frame is active — an author bug (the name never matched
        /// a <c>[Prop]</c>). The no-frame case (read outside a parameter-declaring extension's render) is one
        /// instance of "not declared" and throws identically.</para>
        /// </summary>
        /// <param name="name">The declared parameter name. Must not be <c>null</c>.</param>
        /// <returns>The bound value (possibly <c>null</c>).</returns>
        public object GetParameter(string name)
        {
            if (name == null)
                throw new ArgumentNullException(nameof(name));
            if (ExtensionParameters == null || !ExtensionParameters.TryGetIndex(name, out var index))
                throw new ArgumentException(
                    $"'{name}' is not a declared [Prop] parameter of the extension whose frame is active here.",
                    nameof(name));
            return ExtensionParameterValues[index];
        }

        /// <summary>
        /// <para>Publishes a value into the current body's local context frame for later sibling extensions
        /// (in document order) to read. Last write wins. Keys are ordinal, case-sensitive.</para>
        /// <para>The <c>"heddle."</c> prefix is reserved for the engine — only
        /// <see cref="BranchState.ReservedKey"/> is accepted from it, and its value must be a
        /// <see cref="BranchState"/>. <c>null</c> values are legal.</para>
        /// <para>Throws <see cref="InvalidOperationException"/> when no frame exists — mark the publishing
        /// extension with <c>[ScopeChannel]</c> so bodies containing it get a frame.</para>
        /// </summary>
        /// <param name="key">The channel key. Must not be <c>null</c>.</param>
        /// <param name="value">The value to publish. May be <c>null</c>.</param>
        public void Publish(string key, object value)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));

            bool reserved = key.StartsWith("heddle.", StringComparison.Ordinal);
            if (reserved)
            {
                if (!string.Equals(key, BranchState.ReservedKey, StringComparison.Ordinal))
                    throw new ArgumentException(
                        $"The key '{key}' uses the reserved 'heddle.' prefix. Only '{BranchState.ReservedKey}' is accepted.",
                        nameof(key));
                if (!(value is BranchState))
                    throw new ArgumentException(
                        $"Values published under '{BranchState.ReservedKey}' must be a {nameof(BranchState)}.",
                        nameof(value));
            }

            if (Locals == null)
                throw new InvalidOperationException(NoFrameMessage);

            if (reserved)
                Locals.SetBranch((BranchState) value);
            else
                Locals.Set(key, value);
        }

        /// <summary>
        /// <para>Reads a value published earlier in the current body execution. Returns <c>false</c> (never
        /// throws) when the key is absent or no frame exists.</para>
        /// <para>Frames never cross body boundaries: nested bodies, partials, and each <c>@list</c>/<c>@for</c>
        /// iteration start fresh.</para>
        /// </summary>
        /// <param name="key">The channel key. Must not be <c>null</c>.</param>
        /// <param name="value">The published value, or <c>null</c> when the method returns <c>false</c>.</param>
        /// <returns><c>true</c> when a value (possibly <c>null</c>) was published under <paramref name="key"/>.</returns>
        public bool TryRead(string key, out object value)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));

            value = null;
            if (Locals == null)
                return false;

            if (key.StartsWith("heddle.", StringComparison.Ordinal))
            {
                if (string.Equals(key, BranchState.ReservedKey, StringComparison.Ordinal) &&
                    Locals.TryGetBranch(out var state))
                {
                    value = state;
                    return true;
                }

                return false;
            }

            return Locals.TryGet(key, out value);
        }

        /// <summary>Zero-boxing branch-slot write. Silent no-op when no frame exists (the opportunistic
        /// <c>@if</c>/<c>@ifnot</c> path — safe because a frameless body provably has no reader).</summary>
        internal void PublishBranch(in BranchState state)
        {
            Locals?.SetBranch(state);
        }

        /// <summary><c>false</c> when no frame exists or the branch slot is empty; never throws.</summary>
        internal bool TryReadBranch(out BranchState state)
        {
            if (Locals != null)
                return Locals.TryGetBranch(out state);

            state = default;
            return false;
        }

        /// <summary>Empties the branch slot (the terminal <c>@else</c> action); no-op when no frame exists.</summary>
        internal void ClearBranch()
        {
            Locals?.ClearBranch();
        }

        public static readonly Scope Null = new Scope(null, null, null, null, null);
    }
}
