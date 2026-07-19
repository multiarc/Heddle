using System;

namespace Heddle.Runtime
{
    /// <summary>
    /// <para>Phase 9 D4 — the trim-time feature switch that keeps Roslyn (the C# expression tier) out of a trimmed
    /// publish (the browser WASM demo bundle). Reads the standard-library <c>AppContext</c> switch
    /// <c>"Heddle.CSharpTierEnabled"</c>: unset (every ordinary host) reads as <c>true</c>, so behavior is identical
    /// to before this switch existed; a host that sets it to <c>false</c> (or a trimmed publish that adds
    /// <c>&lt;RuntimeHostConfigurationOption Include="Heddle.CSharpTierEnabled" Value="false" Trim="true" /&gt;</c>)
    /// disables the C# tier.</para>
    /// <para>The paired <c>ILLink.Substitutions.xml</c> makes the trimmer replace this getter with a constant
    /// <c>false</c> when the switch is trimmed off, so the guarded Roslyn branches become dead code the linker
    /// removes together with the whole <c>Microsoft.CodeAnalysis.*</c> reference graph.</para>
    /// </summary>
    internal static class HeddleFeatures
    {
        /// <summary>Phase 9 D4 — the stable diagnostic code for the C#-tier-disabled guard (HED9001). Kept internal
        /// so the phase adds no public API surface (the sibling ids in <c>HeddleDiagnosticIds</c> are public, but
        /// this one is a host-capability condition surfaced only through <c>HeddleCompileError.DiagnosticId</c>).</summary>
        internal const string CSharpTierDisabledDiagnosticId = "HED9001";

        /// <summary>
        /// True when the Roslyn C# expression tier is available. Default (switch unset) is <c>true</c> —
        /// unchanged behavior. The trimmer substitutes a constant <c>false</c> for this getter when the
        /// <c>Heddle.CSharpTierEnabled</c> feature is trimmed off.
        /// </summary>
        internal static bool CSharpTierEnabled =>
            !AppContext.TryGetSwitch("Heddle.CSharpTierEnabled", out bool enabled) || enabled;
    }
}
