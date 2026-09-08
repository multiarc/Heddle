using System;
using System.Globalization;

namespace Heddle.Precompiled
{
    /// <summary>
    /// <para>The manifest contract's version rules, stated once and compiled into both <c>Heddle</c>
    /// (the registration gate) and the build host (the emitted attribute). Before this file
    /// the emitted <c>schemaVersion</c> was a literal in the build tier and the accepted window was a pair of
    /// private consts in the runtime — an all-or-nothing hazard, where bumping one side rejects an entire
    /// assembly behind an opt-in callback.</para>
    /// <para>The engine version travels as a string on the attribute, so parsing stays at the call site; the
    /// predicates here take parsed <see cref="Version"/>s and are trivially testable on both sides.</para>
    /// </summary>
    public static class PrecompiledSchema
    {
        /// <summary>The first schema of the compiled form — the embedded artifact the build host writes and the
        /// runtime loader reads. A marker below it is a 2.x manifest and is refused by registration.</summary>
        public const int CompiledFormSchemaVersion = 4;

        /// <summary>Oldest manifest schema this engine accepts. Raised from 1 to 3 in version 2.1 (a declared
        /// binary break): schemas 1–2 reference a <see cref="PrecompiledExtensionBinding"/> constructor no longer in
        /// metadata, so accepting them causes startup faults instead of graceful fallback. Raised to 4 with the
        /// compiled form: only artifact-backed markers register.</summary>
        public const int MinSupportedSchemaVersion = 4;

        /// <summary>Newest manifest schema this engine accepts. Equal to
        /// <see cref="MinSupportedSchemaVersion"/>: schema 4 is the only shape this engine reads, because every
        /// schema below it is a released shape whose IL is unrunnable and no schema above it exists.</summary>
        public const int MaxSupportedSchemaVersion = 4;

        /// <summary>The schema the build host emits. Kept inside the supported window by an invariant test.</summary>
        public const int CurrentSchemaVersion = 4;

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
