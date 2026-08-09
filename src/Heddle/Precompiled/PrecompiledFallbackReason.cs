namespace Heddle.Precompiled
{
    /// <summary>Why a precompiled template was not used. Every member must be classified by carrier in
    /// <see cref="PrecompiledFallbackEvent"/>: registration-time reasons report via ForAssembly,
    /// per-request reasons via ForTemplate. Unclassified members are unraisable.</summary>
    public enum PrecompiledFallbackReason
    {
        SchemaVersionUnsupported,
        EngineVersionIncompatible,

        /// <summary>Fallback-marker entry.</summary>
        UnsupportedFunction,

        OptionsMismatch,
        ExtensionBindingMismatch,

        /// <summary>Function binding no longer matches the target recorded at build.</summary>
        FunctionBindingMismatch,

        StaleContent,
        StaleImport,

        /// <summary>Informational; never causes gauntlet failure.</summary>
        CaseMismatch,

        /// <summary>Registration-time: a registered <c>Name</c> conflicts with another template's key or name,
        /// or violates <see cref="TemplateKey"/> rules. Never throws; the template stays registered under its key.</summary>
        RegisteredNameUnavailable,

        /// <summary>The entry's <see cref="PrecompiledTemplateInfo.ModelType"/> is not the type the request would
        /// have compiled the template against. Only an entry whose
        /// <see cref="PrecompiledTemplateInfo.ModelTypeIsAmbient"/> is set can raise this: the template declares no
        /// <c>@model</c>, so the build's assumed model type and the host's <c>CompileContext</c> model type are two
        /// independent answers to the same question, and typed code compiled against one of them may not produce the
        /// other's bytes.
        /// <para>Appended rather than grouped with the other per-request reasons so that no existing member's
        /// numeric value moves.</para></summary>
        ModelTypeMismatch
    }
}
