using System;
using System.Globalization;

namespace Heddle.Precompiled
{
    /// <summary>
    /// <para>The manifest contract's version rules (phase 5 D5), stated once and compiled into both <c>Heddle</c>
    /// (the registration gate) and the <c>Heddle.Generator</c> analyzer (the emitted attribute). Before this file
    /// the emitted <c>schemaVersion</c> was a literal in the generator and the accepted window was a pair of private
    /// consts in the runtime — the all-or-nothing hazard of 05 F5, where bumping one side rejects an entire
    /// assembly behind an opt-in callback.</para>
    /// <para>The engine version travels as a string on the attribute, so parsing stays at the call site; the
    /// predicates here take parsed <see cref="Version"/>s and are trivially testable on both sides.</para>
    /// </summary>
    public static class PrecompiledSchema
    {
        /// <summary>Oldest manifest schema this engine accepts (phase 7 generator).</summary>
        public const int MinSupportedSchemaVersion = 1;

        /// <summary>Newest manifest schema this engine accepts (schema 5: the per-carrier
        /// <c>BindDefinition</c> locals overload, phase 1 D2).</summary>
        public const int MaxSupportedSchemaVersion = 5;

        /// <summary>The schema the generator emits. Kept inside the supported window by an invariant test.</summary>
        public const int CurrentSchemaVersion = 5;

        /// <summary>
        /// The schema at which generated dynamic member hops route through
        /// <c>PrecompiledRuntime.DynamicMember</c> instead of an inline <c>(dynamic)</c> cast chain
        /// (phase 4 D11 / OQ3). The routing depends on a runtime API that must exist at render time, so it is gated
        /// on the version rather than emitted unconditionally: an older engine sees a schema it does not accept,
        /// rejects the assembly at registration with
        /// <c>PrecompiledFallbackReason.SchemaVersionUnsupported</c>, and falls back to runtime compilation —
        /// instead of faulting with a <c>MissingMethodException</c> mid-render.
        /// </summary>
        public const int DynamicMemberRoutingSchemaVersion = 3;

        /// <summary>
        /// The schema at which an extension binding row may carry a <c>PropLayoutFingerprint</c> (phase 3 OQ4).
        /// Purely additive: the gauntlet's layout check is vacuous when the value is absent, so manifests emitted
        /// by an older generator pass it and nothing forces re-precompilation. The constant exists so "which
        /// schema introduced the row" has one answer both tiers read.
        /// </summary>
        public const int PropLayoutFingerprintSchemaVersion = 4;

        /// <summary>
        /// The schema at which a generated definition call site binds through the <b>per-carrier</b>
        /// <c>PrecompiledRuntime.BindDefinition(… bodyNeedsLocals, callerContentNeedsLocals …)</c> overload
        /// (phase 1 D2) instead of the single-flag one. Like
        /// <see cref="DynamicMemberRoutingSchemaVersion"/> this depends on a runtime API that must exist where the
        /// generated assembly runs, so the version — not silence — is what an older engine rejects the assembly on
        /// (<c>PrecompiledFallbackReason.SchemaVersionUnsupported</c>).
        /// </summary>
        public const int PerCarrierLocalsSchemaVersion = 5;

        /// <summary>Whether the generator may emit the per-carrier <c>BindDefinition</c> overload at the schema it
        /// is currently emitting (phase 1 D2). Structurally unbuildable in the unsafe combination, exactly as
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
