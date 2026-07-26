namespace Heddle.Precompiled
{
    /// <summary>Why a precompiled template was not used. The two schema/engine registration-time
    /// reasons (<see cref="SchemaVersionUnsupported"/>, <see cref="EngineVersionIncompatible"/>) fire once per ignored
    /// manifest and never throw under <c>Strict</c>; the per-request reasons drive the mismatch policy.
    /// <para><b>Every member is classified by carrier</b>: a registration-time reason — the two above plus
    /// <see cref="RegisteredNameUnavailable"/> — is reported through
    /// <see cref="PrecompiledFallbackEvent.ForAssembly"/> and names an assembly; every other reason is reported
    /// through <see cref="PrecompiledFallbackEvent.ForTemplate"/> and names one template. Adding a member here
    /// without classifying it there makes it unraisable, deliberately.</para></summary>
    public enum PrecompiledFallbackReason
    {
        SchemaVersionUnsupported,
        EngineVersionIncompatible,

        /// <summary>Gauntlet step 0 — a fallback-marker entry.</summary>
        UnsupportedFunction,

        OptionsMismatch,
        ExtensionBindingMismatch,

        /// <summary>Gauntlet step 3 — a function binding no longer matches the target recorded at build.</summary>
        FunctionBindingMismatch,

        StaleContent,
        StaleImport,

        /// <summary>Informational shadow-index hit on a lookup miss — never a gauntlet failure.</summary>
        CaseMismatch,

        /// <summary>Registration-time: a template's registered <c>Name</c> could not
        /// become a lookup spelling — because another registered template already answers to it (as its key, or as
        /// its own name), or because the name is not a spelling the shared <see cref="TemplateKey"/> rule accepts at
        /// all. The two causes share one reason and one id because from the host's side they are one situation: a
        /// name it expected to resolve does not.
        /// Reported once per lost name, never a throw and never a gauntlet failure: the template stays registered
        /// under its key and only the addition is lost, so nothing that resolved before changes meaning. Contrast
        /// a duplicate <em>key</em>, which throws <see cref="PrecompiledRegistrationException"/> because two
        /// templates claiming one registration has no resolvable answer.</summary>
        RegisteredNameUnavailable
    }
}
