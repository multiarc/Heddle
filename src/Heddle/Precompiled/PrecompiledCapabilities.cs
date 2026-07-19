using System;

namespace Heddle.Precompiled
{
    /// <summary>Capabilities a precompiled template exposes (phase 7 D15). <c>StringOutput</c> is always set in v1
    /// except on fallback-marker entries (<see cref="PrecompiledTemplateInfo.IsPrecompiled"/> == false, which carry
    /// <see cref="None"/>); <c>Utf8Pieces</c> is added when the <c>"…"u8</c> tier was emitted.</summary>
    [Flags]
    public enum PrecompiledCapabilities
    {
        None = 0,
        StringOutput = 1,
        Utf8Pieces = 2
    }
}
