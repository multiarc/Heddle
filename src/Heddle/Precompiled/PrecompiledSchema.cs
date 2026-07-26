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
        /// <summary>
        /// <para>Oldest manifest schema this engine accepts. <b>Raised 1 → 3 in 2.1 (Q8.2, corrected), the one
        /// narrowing this constant has ever had</b> — and a genuine, declared binary break rather than a gate
        /// catching up with one that had already happened.</para>
        /// <para><b>The released facts, verified against the <c>v2.0.0</c> tag.</b> The shipped generator emitted
        /// <c>schemaVersion: 2</c>; the shipped engine accepted <c>Min = 1, Max = 2</c> (as two private consts —
        /// this file did not exist yet); the shipped <see cref="PrecompiledExtensionBinding"/> had a real
        /// <b>two-argument</b> <c>.ctor(string, string)</c> and the shipped generator emitted two-argument calls. So
        /// <b>schemas 1 and 2 are the only schemas that have ever shipped</b>, and everything numbered above 2 is
        /// unreleased work-in-progress.</para>
        /// <para><b>What the break is.</b> The prop-layout fingerprint landed on
        /// <see cref="PrecompiledExtensionBinding"/> as an <em>optional third constructor parameter</em> rather than
        /// as a real overload, so the two-argument <c>.ctor(string, string)</c> that every <b>released</b>
        /// (schema 1–2) manifest's IL calls no longer exists in metadata. With <c>Min = 1</c> the gate would
        /// <em>accept</em> exactly those manifests, and the fault would land as a
        /// <see cref="MissingMethodException"/> from <c>Activator.CreateInstance</c>/<c>GetTemplates</c> inside
        /// <c>PrecompiledTemplates.Register</c> — a host-startup crash, not the clean per-manifest fallback the
        /// taxonomy promises. Advertising a support window the metadata cannot honour is worse than declaring the
        /// break: <c>3</c> excludes exactly the faulting set (1–2 were built against the two-argument constructor,
        /// 3 against the three-argument one), so a 2.0-precompiled assembly degrades to the dynamic path with one
        /// <c>HED7102</c> callback.</para>
        /// <para><b>Why the numbers collapsed.</b> The unreleased line had reached 5 across three separate bumps
        /// (dynamic-member routing at 3, the prop-layout row at 4, the per-carrier <c>BindDefinition</c> overload at
        /// 5). None of them was ever observable by a user, so carrying three increments would advertise a migration
        /// history that never existed and would leave this window claiming to support manifest shapes no generator
        /// ever emitted. They are collapsed into <b>one</b> increment past the released <c>2</c>: schema
        /// <b>3</b> carries all of it, plus the registered name (Q8.30) and the <c>#line</c> path form (Q8.31).</para>
        /// <para>Consequence, declared at 2.1 with no compatibility shim: a project precompiled by a 2.0.x
        /// generator must be rebuilt to stay precompiled. Demonstrated — not asserted — by
        /// <c>OldSchemaManifestRejectionTests</c>, which builds a manifest whose IL genuinely names the absent
        /// constructor and shows the clean rejection at each released schema and the fault at <c>Min</c>.</para>
        /// </summary>
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
        /// (phase 4 D11 / OQ3). The routing depends on a runtime API that must exist at render time, so it is gated
        /// on the version rather than emitted unconditionally: an older engine sees a schema it does not accept,
        /// rejects the assembly at registration with
        /// <c>PrecompiledFallbackReason.SchemaVersionUnsupported</c>, and falls back to runtime compilation —
        /// instead of faulting with a <c>MissingMethodException</c> mid-render.
        /// </summary>
        public const int DynamicMemberRoutingSchemaVersion = 3;

        /// <summary>
        /// The schema at which an extension binding row may carry a <c>PropLayoutFingerprint</c> (phase 3 OQ4).
        /// Purely additive <em>within</em> schema 3: the gauntlet's layout check is vacuous when the value is
        /// absent, so a parameter-less extension's row omits it entirely and still passes. The constant exists so
        /// "which schema introduced the row" has one answer both tiers read.
        /// </summary>
        public const int PropLayoutFingerprintSchemaVersion = 3;

        /// <summary>
        /// The schema at which a generated definition call site binds through the <b>per-carrier</b>
        /// <c>PrecompiledRuntime.BindDefinition(… bodyNeedsLocals, callerContentNeedsLocals …)</c> overload
        /// (phase 1 D2) instead of the single-flag one. Like
        /// <see cref="DynamicMemberRoutingSchemaVersion"/> this depends on a runtime API that must exist where the
        /// generated assembly runs, so the version — not silence — is what an older engine rejects the assembly on
        /// (<c>PrecompiledFallbackReason.SchemaVersionUnsupported</c>).
        /// </summary>
        public const int PerCarrierLocalsSchemaVersion = 3;

        /// <summary>
        /// The schema at which a template row may carry its <c>PrecompiledTemplateInfo.RegisteredName</c> — the
        /// <c>Name</c> item metadatum the runtime registry answers to (Q8.30). Additive in the
        /// <see cref="PropLayoutFingerprintSchemaVersion"/> sense: a template that declares no name omits the value
        /// and nothing consults it.
        /// </summary>
        public const int RegisteredNameSchemaVersion = 3;

        /// <summary>
        /// The schema at which a template row records which form its generated <c>#line</c> file names are in
        /// (<c>PrecompiledTemplateInfo.LinePathForm</c>, Q8.31). Vacuous when absent: the value reads back as
        /// <c>PrecompiledLinePathForm.Unspecified</c>, which claims nothing.
        /// </summary>
        public const int LinePathFormSchemaVersion = 3;

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
