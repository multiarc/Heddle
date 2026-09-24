using System;
using System.Globalization;

namespace Heddle.Precompiled
{
    /// <summary>
    /// <para>The manifest contract's version rules, stated once and compiled into both <c>Heddle</c>
    /// (the registration gate) and the build host (the emitted attribute), so the number the build stamps
    /// and the window the loader accepts cannot drift apart.</para>
    /// <para>The engine version travels as a string on the attribute, so parsing stays at the call site; the
    /// predicates here take parsed <see cref="Version"/>s and are trivially testable on both sides.</para>
    /// </summary>
    public static class PrecompiledSchema
    {
        /// <summary>The schema of the compiled form — the embedded artifact the build host writes and the
        /// runtime loader reads. A marker below it is a 2.x manifest and is refused by registration.</summary>
        public const int CompiledFormSchemaVersion = 3;

        /// <summary>Oldest manifest schema this engine accepts: only artifact-backed markers register, and
        /// every shape below the compiled form's references members no longer in metadata, so accepting one
        /// causes startup faults instead of graceful fallback.</summary>
        public const int MinSupportedSchemaVersion = 3;

        /// <summary>Newest manifest schema this engine accepts. Equal to
        /// <see cref="MinSupportedSchemaVersion"/>: the compiled form is the one shape this engine reads.</summary>
        public const int MaxSupportedSchemaVersion = 3;

        /// <summary>The schema the build host emits. Kept inside the supported window by an invariant test.</summary>
        public const int CurrentSchemaVersion = 3;

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
