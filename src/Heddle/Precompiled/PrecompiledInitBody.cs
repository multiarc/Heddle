using System;

namespace Heddle.Precompiled
{
    /// <summary>
    /// One body a call site hands to the engine's own <c>InitStart</c>: its text before and after the engine's
    /// document shaping, whether it needs a <c>ScopeLocals</c> frame, and the typing the build assumed when it
    /// compiled that text.
    /// <para>A plain extension call has one — the call's own body. A definition invocation has two, because the
    /// engine runs <c>InitStart</c> twice: once on the outer carrier over the caller content, once on the inner
    /// carrier over the definition body.</para>
    /// </summary>
    public sealed class PrecompiledInitBody
    {
        /// <summary>The body text as the parser produced it, which is what the hook sees on entry.
        /// <c>null</c> or empty means the call has no body at all.</summary>
        public string RawText { get; set; }

        /// <summary>The body text after the engine's document shaping — what a real body compile writes back over
        /// <c>InitContext.ParameterTemplate</c>, and the inert inner result of a body that holds no processors.</summary>
        public string ShapedText { get; set; }

        /// <summary>Whether the compiled body needs a fresh <c>ScopeLocals</c> frame per render
        /// (<c>RuntimeDocument.NeedsLocals</c>).</summary>
        public bool NeedsLocals { get; set; }

        /// <summary>The model type the build compiled this body against; <c>null</c> means <c>dynamic</c>. Compared
        /// against the type the hook actually hands the body compile — a disagreement means the emitted casts would
        /// render bytes the engine does not, so the template falls back rather than rendering them.</summary>
        public Type AssumedDataType { get; set; }

        /// <summary>The chained type the build compiled this body against; <c>null</c> means <c>dynamic</c>.
        /// See <see cref="AssumedDataType"/>.</summary>
        public Type AssumedChainedType { get; set; }
    }
}
