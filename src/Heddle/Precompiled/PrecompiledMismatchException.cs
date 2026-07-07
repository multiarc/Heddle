using System;

namespace Heddle.Precompiled
{
    /// <summary>Thrown on a per-request gauntlet failure under
    /// <see cref="Heddle.Data.PrecompiledMismatchPolicy.Strict"/> (phase 7 D8). A registry <b>miss</b> never
    /// throws — strictness polices divergence, not coverage.</summary>
    public class PrecompiledMismatchException : Exception
    {
        public PrecompiledMismatchException(string key, PrecompiledFallbackReason reason, string detail)
            : base($"Precompiled template '{key}' failed validation ({reason}): {detail}.")
        {
            Key = key;
            Reason = reason;
            Detail = detail;
        }

        public string Key { get; }

        public PrecompiledFallbackReason Reason { get; }

        /// <summary>The pinned per-reason detail string (identity-and-metadata.md § reason and detail strings).</summary>
        public string Detail { get; }
    }
}
