using System;

namespace Heddle.Precompiled
{
    /// <summary>The assembly-level discovery marker the generator emits — one per assembly (phase 7 D6, the
    /// <c>RazorCompiledItemAttribute</c> model). A single reflection touch during
    /// <see cref="PrecompiledTemplates.Register"/> reads the manifest type and runs the schema/engine gate.
    /// The constructor signature is the stable outermost contract and never changes; evolution happens behind
    /// <see cref="ManifestType"/> (schema-versioned).</summary>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false, Inherited = false)]
    public sealed class HeddleCompiledTemplatesAttribute : Attribute
    {
        public HeddleCompiledTemplatesAttribute(Type manifestType, int schemaVersion, string engineVersion)
        {
            ManifestType = manifestType;
            SchemaVersion = schemaVersion;
            EngineVersion = engineVersion;
        }

        public Type ManifestType { get; }

        public int SchemaVersion { get; }

        /// <summary>The referenced <c>Heddle</c> assembly version the generator saw (<c>major.minor.patch</c>).</summary>
        public string EngineVersion { get; }
    }
}
