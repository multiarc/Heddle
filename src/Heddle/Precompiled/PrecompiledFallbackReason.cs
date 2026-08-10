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
        ModelTypeMismatch,

        /// <summary>An extension's own compile-time hook, run at registration through
        /// <see cref="PrecompiledRuntime.Init"/>, reported compile errors for one of the template's call sites. The
        /// dynamic tier runs the same hook and would refuse the template too, so this costs the template rather than
        /// the call site — the request compiles dynamically and gets the engine's own errors.
        /// <para>Appended for the same reason as <see cref="ModelTypeMismatch"/>: no existing member's numeric value
        /// moves.</para></summary>
        ExtensionInitCompileError,

        /// <summary>An extension's own compile-time hook handed its body a different model or chained type than the
        /// build assumed when it emitted that body. The emitted casts are typed on the build's assumption, so
        /// rendering them would produce bytes the engine does not; the template falls back instead.</summary>
        ExtensionInitTypingMismatch
    }
}
