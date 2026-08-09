using Heddle.Data;
using Heddle.Runtime;

namespace Heddle.Core
{
    /// <summary>
    /// The post-state a supplied body installs in place of a compiled one. Its three shapes are exactly the three
    /// post-states of <c>AbstractExtension.InitSubTemplate</c>.
    /// </summary>
    internal struct PrecompiledBodyState
    {
        /// <summary>The body strategy, or <c>null</c> when the body compiled to no processors.</summary>
        internal IProcessStrategy Strategy;

        /// <summary>The value to write to <c>_innerResult</c>; read only when <see cref="AssignInnerResult"/> is set.</summary>
        internal string InnerResult;

        /// <summary>False for the body-with-processors state, where <c>InitStart</c> leaves <c>_innerResult</c> at its
        /// field initializer rather than assigning it.</summary>
        internal bool AssignInnerResult;

        internal bool NeedsLocals;
    }

    /// <summary>
    /// One armed supply of a prebuilt body, consumed by a single <c>AbstractExtension.InitStart</c>.
    /// </summary>
    internal sealed class PrecompiledBodyFrame
    {
        internal PrecompiledBodyFrame(IProcessStrategy strategy, string rawBody, string shapedBody, bool needsLocals)
        {
            Strategy = strategy;
            RawBody = rawBody;
            ShapedBody = shapedBody;
            NeedsLocals = needsLocals;
        }

        internal IProcessStrategy Strategy { get; }

        /// <summary>The call's body text as the parser produced it — what <c>InitStart</c> sees on entry.</summary>
        internal string RawBody { get; }

        /// <summary>The body text after the engine's document shaping — what a real body compile writes back over
        /// <c>InitContext.ParameterTemplate</c>, and the <c>_innerResult</c> of a processor-less body.</summary>
        internal string ShapedBody { get; }

        internal bool NeedsLocals { get; }

        /// <summary>How many times a hook asked this frame for a body. Exactly one is the contract: zero means the
        /// hook never compiled the body it was handed, more than one means it compiled a second document.</summary>
        internal int ConsumeCount { get; set; }

        /// <summary>The model type the hook handed the body compile — the hook's own answer about body typing,
        /// recorded on the first consumption and compared against the build's assumption by the caller.</summary>
        internal ExType ConsumedDataType { get; set; }

        /// <summary>The chained type the hook handed the body compile; see <see cref="ConsumedDataType"/>.</summary>
        internal ExType ConsumedChainedType { get; set; }
    }

    /// <summary>
    /// The one-shot seam that lets a hook run for real without compiling its body.
    /// <para><c>AbstractExtension.InitSubTemplate</c> is <c>private static</c> and reaches
    /// <c>internal HeddleCompiler.Compile</c>, so it is the only door to a body compile in the engine. Arming a frame
    /// closes that door for exactly one <c>InitStart</c>: the hook's own code — <c>ListExtension</c>'s count reader,
    /// <c>OutExtension</c>'s slot mode and its five diagnostics, <c>DefinitionBaseExtension</c>'s recursion limit —
    /// runs unchanged, and where the engine would have compiled the body, the prebuilt strategy is installed instead.
    /// The frame reproduces all three of <c>InitSubTemplate</c>'s post-states field for field.</para>
    /// <para>Thread-static, so a concurrent dynamic compile on another thread never sees an armed frame. The dynamic
    /// path pays one thread-static read per <c>InitStart</c>, which is compile time only — nothing here is on the
    /// render path.</para>
    /// </summary>
    internal static class PrecompiledBodySupply
    {
        [System.ThreadStatic] private static PrecompiledBodyFrame _current;

        /// <summary>Arms <paramref name="frame"/> for the next <c>InitStart</c> on this thread. The caller must
        /// <see cref="Disarm"/> in a <c>finally</c>, and must not arm across a nested document compile.</summary>
        internal static void Arm(PrecompiledBodyFrame frame) => _current = frame;

        /// <summary>Restores the previous state. A frame is never nested, so this clears rather than pops.</summary>
        internal static void Disarm() => _current = null;

        /// <summary>
        /// Supplies the armed body in place of a compile. Returns false when nothing is armed, which is every
        /// dynamic-tier compile.
        /// </summary>
        /// <param name="dataType">The model type the hook is handing the body compile; recorded on the first
        /// consumption.</param>
        /// <param name="chainedType">The chained type the hook is handing the body compile; recorded likewise.</param>
        /// <param name="parameterTemplate">The call's body text, written back exactly as a real body compile writes
        /// it back: unchanged when there is no body, the shaped document when there is one.</param>
        /// <param name="state">The post-state to install.</param>
        internal static bool TryConsume(ExType dataType, ExType chainedType, ref string parameterTemplate,
            out PrecompiledBodyState state)
        {
            var frame = _current;
            if (frame == null)
            {
                state = default(PrecompiledBodyState);
                return false;
            }

            frame.ConsumeCount++;
            if (frame.ConsumeCount > 1)
            {
                // A second body from one hook. Supplying the same strategy twice would install it on whichever
                // document asked last, so hand back the inert no-body state and let the caller — which sees the
                // count — discard the whole instance. Falling through to a real compile is not an option: the
                // synthesized parse context describes this call site, not the document the hook is compiling.
                state = new PrecompiledBodyState
                {
                    Strategy = null,
                    InnerResult = parameterTemplate,
                    AssignInnerResult = true,
                    NeedsLocals = false
                };
                return true;
            }

            frame.ConsumedDataType = dataType;
            frame.ConsumedChainedType = chainedType;

            if (string.IsNullOrEmpty(frame.RawBody))
            {
                // Post-state 1: no body text. InitSubTemplate leaves parameterTemplate alone and yields no document,
                // so InitStart copies the raw text — null or empty, and null is preserved — into _innerResult.
                state = new PrecompiledBodyState
                {
                    Strategy = null,
                    InnerResult = parameterTemplate,
                    AssignInnerResult = true,
                    NeedsLocals = false
                };
                return true;
            }

            // Both remaining post-states compiled a document, so the shaped text replaces the raw text.
            parameterTemplate = frame.ShapedBody;

            if (frame.Strategy == null)
            {
                // Post-state 2: the body compiled but holds no processors (RuntimeDocument.Empty), so the engine
                // discards the document and the shaped text becomes the inert inner result.
                state = new PrecompiledBodyState
                {
                    Strategy = null,
                    InnerResult = frame.ShapedBody,
                    AssignInnerResult = true,
                    NeedsLocals = false
                };
                return true;
            }

            // Post-state 3: a document with processors. _innerResult is never assigned on this path.
            state = new PrecompiledBodyState
            {
                Strategy = frame.Strategy,
                InnerResult = null,
                AssignInnerResult = false,
                NeedsLocals = frame.NeedsLocals
            };
            return true;
        }
    }
}
