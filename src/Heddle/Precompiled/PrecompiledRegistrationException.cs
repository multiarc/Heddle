using System;

namespace Heddle.Precompiled
{
    /// <summary>Thrown by <see cref="PrecompiledTemplates.Register"/> when a staged key ordinally equals an
    /// already-registered key (phase 7 D2). Registration is transactional — nothing from the staged manifest was
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

        /// <summary>The colliding, normalized key.</summary>
        public string Key { get; }

        /// <summary>The assembly whose entry is already registered.</summary>
        public string ExistingAssemblyName { get; }

        /// <summary>The assembly whose registration was rejected.</summary>
        public string NewAssemblyName { get; }

        private static string BuildMessage(string key, string existingAssemblyName, string newAssemblyName)
        {
            return
                $"Precompiled template key '{key}' from assembly '{newAssemblyName}' is already registered by " +
                $"assembly '{existingAssemblyName}'. Duplicate keys across precompiled manifests are not supported; " +
                "an explicit replacement marker (a future `Replace` flag on the manifest entry, mirroring " +
                "`[ExtensionReplace]`) is not yet available. Rename one template, give it an explicit `Key` " +
                "metadata, or exclude it from pre-compilation with `<HeddleTemplate Remove=\"…\" />`.";
        }
    }
}
