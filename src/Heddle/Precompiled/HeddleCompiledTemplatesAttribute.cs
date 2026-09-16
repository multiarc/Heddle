using System;
using System.Diagnostics.CodeAnalysis;

namespace Heddle.Precompiled
{
    /// <summary>The assembly-level discovery marker the build emits — one per assembly, following the
    /// <c>RazorCompiledItemAttribute</c> model. A single reflection touch during
    /// <see cref="PrecompiledTemplates.Register"/> reads the manifest type and runs the schema/engine gate.
    /// The constructor signature is the stable outermost contract and never changes; evolution happens behind
    /// <see cref="ManifestType"/> (schema-versioned).</summary>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false, Inherited = false)]
    public sealed class HeddleCompiledTemplatesAttribute : Attribute
    {
        public HeddleCompiledTemplatesAttribute(
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] Type manifestType,
            int schemaVersion, string engineVersion)
        {
            ManifestType = manifestType;
            SchemaVersion = schemaVersion;
            EngineVersion = engineVersion;
        }

        // P3-R9: the loader instantiates the manifest with Activator.CreateInstance, so the typeof in the
        // generated marker must keep the parameterless constructor through a trimmed publish.
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
        public Type ManifestType { get; }

        public int SchemaVersion { get; }

        /// <summary>The referenced <c>Heddle</c> assembly version the build saw (<c>major.minor.patch</c>).</summary>
        public string EngineVersion { get; }
    }
}
