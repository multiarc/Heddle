using System;
using System.IO;

namespace Heddle.Precompiled
{
    /// <summary>Thrown by <see cref="PrecompiledTemplates.Register"/> when a staged key ordinally equals an
    /// already-registered key. Registration is transactional — nothing from the staged manifest was
    /// published when this throws. v1 ships no replace flag; the message names the reserved future mechanism.</summary>
    public class PrecompiledRegistrationException : Exception
    {
        public PrecompiledRegistrationException(string key, string existingAssemblyName, string newAssemblyName)
            : base(BuildMessage(key, existingAssemblyName, newAssemblyName))
        {
            Key = key;
            ExistingAssemblyName = existingAssemblyName;
            NewAssemblyName = newAssemblyName;
        }

        /// <summary>A marker below <see cref="PrecompiledSchema.CompiledFormSchemaVersion"/> was registered.
        /// Key and ExistingAssemblyName are null; NewAssemblyName is the rejected assembly.</summary>
        public PrecompiledRegistrationException(string assemblyName, int schemaVersion)
            : base(BuildMessage(assemblyName, schemaVersion))
        {
            NewAssemblyName = assemblyName;
            SchemaVersion = schemaVersion;
        }

        /// <summary>The artifact the marker names is malformed (bad magic, unreadable schema, a required
        /// section missing, sections overlapping or duplicated, a value past the end). The reader's
        /// <see cref="InvalidDataException"/> is the inner exception; the assembly is named here.</summary>
        public PrecompiledRegistrationException(string assemblyName, InvalidDataException malformed)
            : base(BuildMessage(assemblyName, malformed), malformed)
        {
            NewAssemblyName = assemblyName;
        }

        /// <summary>The colliding, normalized key.</summary>
        public string Key { get; }

        /// <summary>The assembly whose entry is already registered.</summary>
        public string ExistingAssemblyName { get; }

        /// <summary>The assembly whose registration was rejected.</summary>
        public string NewAssemblyName { get; }

        /// <summary>The rejected marker's schema; 0 for the duplicate-key constructor.</summary>
        public int SchemaVersion { get; }

        private static string BuildMessage(string key, string existingAssemblyName, string newAssemblyName)
        {
            return
                $"Precompiled template key '{key}' from assembly '{newAssemblyName}' is already registered by " +
                $"assembly '{existingAssemblyName}'. Duplicate keys across precompiled manifests are not supported; " +
                "an explicit replacement marker (a future `Replace` flag on the manifest entry, mirroring " +
                "`[ExtensionReplace]`) is not yet available. Rename one template, give it an explicit `Key` " +
                "metadata, or exclude it from pre-compilation with `<HeddleTemplate Remove=\"…\" />`.";
        }

        private static string BuildMessage(string assemblyName, InvalidDataException malformed)
        {
            return
                $"Assembly '{assemblyName}' carries a malformed compiled-form artifact: " +
                (malformed != null ? malformed.Message : "no detail") + " Rebuild the assembly with the Heddle.Build package.";
        }

        private static string BuildMessage(string assemblyName, int schemaVersion)
        {
            return
                $"Assembly '{assemblyName}' carries a Heddle 2.x precompiled manifest (schema {schemaVersion}); " +
                "this engine reads compiled-form artifacts only. Rebuild the assembly with the Heddle.Build package.";
        }
    }
}
