using System;
using System.Globalization;

namespace Heddle.Precompiled
{
    /// <summary>
    /// <para>The manifest contract's version rules, stated once and compiled into both <c>Heddle</c>
    /// (the registration gate) and the <c>Heddle.Generator</c> analyzer (the emitted attribute). Before this file
    /// the emitted <c>schemaVersion</c> was a literal in the generator and the accepted window was a pair of private
    /// consts in the runtime — an all-or-nothing hazard, where bumping one side rejects an entire
    /// assembly behind an opt-in callback.</para>
    /// <para>The engine version travels as a string on the attribute, so parsing stays at the call site; the
    /// predicates here take parsed <see cref="Version"/>s and are trivially testable on both sides.</para>
    /// </summary>
    public static class PrecompiledSchema
    {
        /// <summary>Oldest manifest schema this engine accepts. Raised from 1 to 3 in version 2.1 (a declared
        /// binary break): schemas 1–2 reference a <see cref="PrecompiledExtensionBinding"/> constructor no longer in
        /// metadata, so accepting them causes startup faults instead of graceful fallback.</summary>
        public const int MinSupportedSchemaVersion = 3;

        /// <summary>Newest manifest schema this engine accepts. Equal to
        /// <see cref="MinSupportedSchemaVersion"/>: schema 3 is the only shape this engine can both read and have
        /// been emitted by a generator it ships with, because every schema below it is a released shape whose IL is
        /// unrunnable and no schema above it exists.</summary>
        public const int MaxSupportedSchemaVersion = 3;

        /// <summary>The schema the generator emits. Kept inside the supported window by an invariant test.</summary>
        public const int CurrentSchemaVersion = 3;

        /// <summary>
        /// The schema at which generated dynamic member hops route through
        /// <c>PrecompiledRuntime.DynamicMember</c> instead of an inline <c>(dynamic)</c> cast chain
        /// The routing depends on a runtime API that must exist at render time, so it is gated
        /// on the version rather than emitted unconditionally: an older engine sees a schema it does not accept,
        /// rejects the assembly at registration with
        /// <c>PrecompiledFallbackReason.SchemaVersionUnsupported</c>, and falls back to runtime compilation —
        /// instead of faulting with a <c>MissingMethodException</c> mid-render.
        /// </summary>
        public const int DynamicMemberRoutingSchemaVersion = 3;

        /// <summary>
        /// The schema at which an extension binding row may carry a <c>PropLayoutFingerprint</c>.
        /// Purely additive <em>within</em> schema 3: the gauntlet's layout check is vacuous when the value is
        /// absent, so a parameter-less extension's row omits it entirely and still passes. The constant exists so
        /// "which schema introduced the row" has one answer both tiers read.
        /// </summary>
        public const int PropLayoutFingerprintSchemaVersion = 3;

        /// <summary>
        /// The schema at which a generated definition call site binds through the <b>per-carrier</b>
        /// <c>PrecompiledRuntime.BindDefinition(… bodyNeedsLocals, callerContentNeedsLocals …)</c> overload
        /// instead of the single-flag one. Like
        /// <see cref="DynamicMemberRoutingSchemaVersion"/> this depends on a runtime API that must exist where the
        /// generated assembly runs, so the version — not silence — is what an older engine rejects the assembly on
        /// (<c>PrecompiledFallbackReason.SchemaVersionUnsupported</c>).
        /// </summary>
        public const int PerCarrierLocalsSchemaVersion = 3;

        /// <summary>
        /// The schema at which a template row may carry its <c>PrecompiledTemplateInfo.RegisteredName</c> — the
        /// <c>Name</c> item metadatum the runtime registry answers to. Additive in the
        /// <see cref="PropLayoutFingerprintSchemaVersion"/> sense: a template that declares no name omits the value
        /// and nothing consults it.
        /// </summary>
        public const int RegisteredNameSchemaVersion = 3;

        /// <summary>
        /// The schema at which a generated call site may bind a function <b>late</b>, through
        /// <c>PrecompiledFunctionSite</c>. Like <see cref="DynamicMemberRoutingSchemaVersion"/> this names a
        /// runtime API that must exist where the generated assembly runs, so an older engine rejects the whole
        /// assembly at registration (<c>PrecompiledFallbackReason.SchemaVersionUnsupported</c>) rather than
        /// faulting with a <c>MissingMethodException</c> mid-render.
        /// </summary>
        public const int LateBoundFunctionSchemaVersion = 3;

        /// <summary>
        /// The widest call a generated site binds late — the arity list <c>PrecompiledFunctionSite</c> declares
        /// and the gate the emitter applies before emitting one, stated once so the two cannot drift into a
        /// missing-method fault. A wider call keeps the build tier's degrade.
        /// </summary>
        public const int LateBoundFunctionMaxArity = 8;

        /// <summary>Whether the generator may emit a late-bound function site at the schema it is currently
        /// emitting. Structurally unbuildable in the unsafe combination, as the routing gate is.</summary>
        public static bool EmitsLateBoundFunctions =>
            CurrentSchemaVersion >= LateBoundFunctionSchemaVersion;

        /// <summary>
        /// The schema at which a template row records which form its generated <c>#line</c> file names are in
        /// (<c>PrecompiledTemplateInfo.LinePathForm</c>). Vacuous when absent: the value reads back as
        /// <c>PrecompiledLinePathForm.Unspecified</c>, which claims nothing.
        /// </summary>
        public const int LinePathFormSchemaVersion = 3;

        /// <summary>
        /// The schema at which a template row records that its model type is <b>ambient</b> — that the template
        /// declares no <c>@model</c>, so <c>PrecompiledTemplateInfo.ModelType</c> is the build's assumption and the
        /// gauntlet must check it against the request's own model type. Additive in the
        /// <see cref="PropLayoutFingerprintSchemaVersion"/> sense, and deliberately so in the safe direction: a row
        /// that omits the value reads back <c>false</c>, which is exactly how every earlier manifest behaves today.
        /// </summary>
        public const int AmbientModelTypeSchemaVersion = 3;

        /// <summary>Whether the generator may emit the per-carrier <c>BindDefinition</c> overload at the schema it
        /// is currently emitting. Structurally unbuildable in the unsafe combination, exactly as
        /// <see cref="EmitsDynamicMemberRouting"/> is.</summary>
        public static bool EmitsPerCarrierLocals =>
            CurrentSchemaVersion >= PerCarrierLocalsSchemaVersion;

        /// <summary>Whether the generator may emit <c>PrecompiledRuntime.DynamicMember</c> routing at the
        /// schema it is currently emitting. The unsafe combination — routing emitted below its gate — is
        /// structurally unbuildable because both halves read these constants.</summary>
        public static bool EmitsDynamicMemberRouting =>
            CurrentSchemaVersion >= DynamicMemberRoutingSchemaVersion;

        /// <summary>Whether a manifest's declared schema version is inside this engine's accepted window.</summary>
        public static bool IsSupported(int schemaVersion) =>
            schemaVersion >= MinSupportedSchemaVersion && schemaVersion <= MaxSupportedSchemaVersion;

        /// <summary>Formats an engine version for the manifest attribute: <c>major.minor.build</c>, invariant.</summary>
        public static string FormatEngineVersion(Version version)
        {
            if (version == null)
                throw new ArgumentNullException(nameof(version));
            return string.Format(CultureInfo.InvariantCulture, "{0}.{1}.{2}",
                version.Major, version.Minor, Math.Max(0, version.Build));
        }

        /// <summary>The engine-compatibility predicate: same major, and the manifest's engine no newer than the
        /// running one.</summary>
        public static bool IsEngineCompatible(Version manifestVersion, Version runtimeVersion)
        {
            if (manifestVersion == null || runtimeVersion == null)
                return false;
            return manifestVersion.Major == runtimeVersion.Major && manifestVersion <= runtimeVersion;
        }
    }
}
