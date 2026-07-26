namespace Heddle.Data
{
    /// <summary>
    /// Controls behavior when a precompiled entry exists but fails per-request validation; registry misses are unaffected.
    /// </summary>
    public enum PrecompiledMismatchPolicy
    {
        /// <summary>
        /// Default. Falls back to the dynamic compile path on validation failure, raising <c>OnFallback</c> callback
        /// and <c>HED7101</c> warning on recompile. Safe for mixed-mode hosts.
        /// </summary>
        Fallback = 0,

        /// <summary>
        /// Throws <c>PrecompiledMismatchException</c> on validation failure. For scenarios where dynamic recompile must be impossible.
        /// </summary>
        Strict = 1
    }
}
