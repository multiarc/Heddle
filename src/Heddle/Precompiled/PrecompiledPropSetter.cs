using Heddle.Data;

namespace Heddle.Precompiled
{
    /// <summary>Dynamic prop-argument evaluator (phase 7 D5, phase 5 D8): invoked against the caller view
    /// (<c>scope.Parent()</c>) so root/chained context stays available while the prop value is computed. Applied by
    /// the engine, never by the emitter — the caller-view rule lives in one place.</summary>
    public delegate object PrecompiledPropEvaluator(in Scope callerView);

    /// <summary>One dynamic prop assignment for a precompiled definition call site (phase 7 D5): the prop's layout
    /// index plus its evaluator. Immutable; instances live in generated <c>static readonly</c> arrays and are handed
    /// to <see cref="PrecompiledRuntime.BindDefinition"/>.</summary>
    public readonly struct PrecompiledPropSetter
    {
        public PrecompiledPropSetter(int index, PrecompiledPropEvaluator evaluate)
        {
            Index = index;
            Evaluate = evaluate;
        }

        /// <summary>The prop's layout index (the same index the runtime backend's layout assigns).</summary>
        public int Index { get; }

        /// <summary>Computes the boxed prop value against the caller view.</summary>
        public PrecompiledPropEvaluator Evaluate { get; }
    }
}
