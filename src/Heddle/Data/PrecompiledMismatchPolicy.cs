namespace Heddle.Data
{
    /// <summary>
    /// Controls what happens when a precompiled template entry exists for a lookup but fails the per-request
    /// validation gauntlet (phase 7 D8). A registry <b>miss</b> is unaffected by this policy — strictness
    /// polices divergence, not coverage.
    /// </summary>
    public enum PrecompiledMismatchPolicy
    {
        /// <summary>
        /// Default. On any gauntlet failure the engine falls back to the unchanged dynamic compile path,
        /// raising one <c>OnFallback</c> callback with the reason and a <c>HED7101</c> warning on the
        /// recompile's result. Safe for mixed-mode hosts.
        /// </summary>
        Fallback = 0,

        /// <summary>
        /// An entry that exists but fails the gauntlet throws <c>PrecompiledMismatchException</c> carrying
        /// the key and reason. Use where a dynamic recompile of a diverged template must be impossible.
        /// </summary>
        Strict = 1
    }
}
