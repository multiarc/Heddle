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
        CaseMismatch,

        /// <summary>Registration-time (Q8.30): a template's registered <c>Name</c> could not become a lookup
        /// spelling because another registered template already answers to it — as its key, or as its own name.
        /// Reported once per lost name, never a throw and never a gauntlet failure: the template stays registered
        /// under its key and only the addition is lost, so nothing that resolved before changes meaning. Contrast
        /// a duplicate <em>key</em>, which throws <see cref="PrecompiledRegistrationException"/> because two
        /// templates claiming one registration has no resolvable answer.</summary>
        RegisteredNameUnavailable
    }
}
