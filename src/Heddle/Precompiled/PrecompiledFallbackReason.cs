namespace Heddle.Precompiled
{
    /// <summary>Why a precompiled template was not used (phase 7 D6/D8/D21). The two registration-time reasons
    /// (<see cref="SchemaVersionUnsupported"/>, <see cref="EngineVersionIncompatible"/>) fire once per ignored
    /// manifest and never throw under <c>Strict</c>; the per-request reasons drive the mismatch policy.</summary>
    public enum PrecompiledFallbackReason
    {
        SchemaVersionUnsupported,
        EngineVersionIncompatible,

        /// <summary>Gauntlet step 0 — a fallback-marker entry (D21).</summary>
        UnsupportedFunction,

        OptionsMismatch,
        ExtensionBindingMismatch,

        /// <summary>Gauntlet step 3 (D21).</summary>
        FunctionBindingMismatch,

        StaleContent,
        StaleImport,

        /// <summary>Informational shadow-index hit on a lookup miss — never a gauntlet failure.</summary>
        CaseMismatch
    }
}
