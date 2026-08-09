namespace Heddle.Precompiled
{
    /// <summary>
    /// How hard the build tries to <b>observe</b> a real engine compile of each template before emitting it
    /// (<c>HeddleObserveEngine</c>).
    /// <para>Observation is a typing optimisation and nothing else: a body whose typing it resolves is emitted with a
    /// direct cast, and a body it cannot resolve is still precompiled through the engine's own zero-allocation
    /// accessors. It can never change a rendered byte, only which tier a read takes.</para>
    /// </summary>
    public enum ObserveMode
    {
        /// <summary>Never observe. The build types what it can from attributes and symbols alone.</summary>
        Off,

        /// <summary>Observe when the build can; any failure is an informational diagnostic and type-agnostic
        /// emission. The default, because a failure costs a tier and never a byte.</summary>
        Auto,

        /// <summary>Observe, and fail the build when it cannot — so a CI leg never silently emits different
        /// sources from a developer machine that could observe.</summary>
        Strict
    }
}
