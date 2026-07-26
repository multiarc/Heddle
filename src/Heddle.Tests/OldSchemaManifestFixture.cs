using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Heddle.Tests
{
    /// <summary>
    /// Builds a precompiled manifest assembly with old (schema 1–2) metadata using a reference facade.
    /// This ensures the fixture references pre-break surfaces rather than binding silently to current constructors.
    /// Built at test time, not checked in, so the construction is auditable.
    /// </summary>
    internal static class OldSchemaManifestFixture
    {
        /// <summary>The released (schema 1–2) slice of the precompiled surface a manifest touches. Deliberately tiny: only the
        /// attribute, the manifest interface, the info type as a bare name, and the two-argument extension binding
        /// whose disappearance is the break.</summary>
        private const string FacadeSource = @"
using System;
using System.Collections.Generic;

namespace Heddle.Precompiled
{
    public sealed class PrecompiledTemplateInfo { }

    public interface IHeddleTemplateManifest
    {
        IReadOnlyList<PrecompiledTemplateInfo> GetTemplates();
    }

    public readonly struct PrecompiledExtensionBinding
    {
        public PrecompiledExtensionBinding(string name, string extensionTypeName)
        {
            Name = name;
            ExtensionTypeName = extensionTypeName;
        }

        public string Name { get; }
        public string ExtensionTypeName { get; }
    }

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
        public string EngineVersion { get; }
    }
}
";

        /// <summary>What the released (schema 1–2) generator emitted: a manifest whose <c>GetTemplates</c> constructs
        /// an extension binding through the two-argument constructor.</summary>
        private const string ManifestSourceTemplate = @"
using System;
using System.Collections.Generic;
using Heddle.Precompiled;

[assembly: HeddleCompiledTemplates(typeof(OldGenerated.__HeddleManifest), {SCHEMA}, ""{ENGINE}"")]

namespace OldGenerated
{
    internal sealed class __HeddleManifest : IHeddleTemplateManifest
    {
        public IReadOnlyList<PrecompiledTemplateInfo> GetTemplates()
        {
            // The whole point: a call to .ctor(string, string), which today's metadata does not carry.
            var binding = new PrecompiledExtensionBinding(""oldext"", ""Some.Ext, SomeAsm"");
            GC.KeepAlive(binding.Name);
            return Array.Empty<PrecompiledTemplateInfo>();
        }
    }
}
";

        /// <summary>Emits the fixture assembly's bytes. <paramref name="schemaVersion"/> is what the manifest
        /// <em>declares</em>; the IL is old-schema either way, which is what makes the pair of tests a controlled
        /// comparison across the <c>MinSupportedSchemaVersion</c> boundary.</summary>
        public static byte[] Emit(int schemaVersion)
        {
            var live = typeof(Heddle.Precompiled.PrecompiledTemplates).Assembly.GetName();
            var facade = Compile(live.Name, FacadeSource, Array.Empty<MetadataReference>(), live.Version);

            var engineVersion = Heddle.Precompiled.PrecompiledSchema.FormatEngineVersion(live.Version);
            var manifestSource = ManifestSourceTemplate
                .Replace("{SCHEMA}", schemaVersion.ToString())
                .Replace("{ENGINE}", engineVersion);

            return Compile("HeddleOldSchemaFixture_S" + schemaVersion, manifestSource,
                new[] { (MetadataReference)MetadataReference.CreateFromImage(facade) }, version: null);
        }

        private static byte[] Compile(string assemblyName, string source, MetadataReference[] extra, Version version)
        {
            var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))
                .Split(Path.PathSeparator)
                .Where(p => !string.IsNullOrEmpty(p) && File.Exists(p))
                // The real Heddle must never be visible to either compilation: its presence is what would let the
                // manifest bind to the current three-parameter constructor and turn this fixture into the very
                // substitution it exists to avoid.
                .Where(p => !string.Equals(Path.GetFileNameWithoutExtension(p), "Heddle", StringComparison.OrdinalIgnoreCase))
                .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))
                .Concat(extra)
                .ToList();

            var trees = new[] { CSharpSyntaxTree.ParseText(source) };
            if (version != null)
                trees = trees.Concat(new[]
                {
                    CSharpSyntaxTree.ParseText(
                        "[assembly: System.Reflection.AssemblyVersion(\"" + version + "\")]")
                }).ToArray();

            var options = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                optimizationLevel: OptimizationLevel.Release);

            // Facade must carry the real assembly's public key; public signing (not full) stamps identity without a signature.
            if (version != null)
                options = options.WithCryptoKeyFile(RepoKeyPath()).WithPublicSign(true);

            var compilation = CSharpCompilation.Create(assemblyName, trees, references, options);
            using (var ms = new MemoryStream())
            {
                var result = compilation.Emit(ms);
                if (!result.Success)
                    throw new InvalidOperationException(assemblyName + " fixture compilation failed:\n" +
                        string.Join("\n", result.Diagnostics
                            .Where(d => d.Severity == DiagnosticSeverity.Error)
                            .Select(d => d.ToString())));
                return ms.ToArray();
            }
        }

        private static string RepoKeyPath() => PipelineContractTests.FindRepoFile("heddle.snk");

        /// <summary>The break itself, as metadata: the two-argument constructor a released schema 1–2 manifest calls is not
        /// present on the live type. Asserted by the tests, and stated here because every other claim in this file
        /// rests on it.</summary>
        public static bool LiveTypeHasTheTwoArgumentConstructor() =>
            typeof(Heddle.Precompiled.PrecompiledExtensionBinding)
                .GetConstructor(new[] { typeof(string), typeof(string) }) != null;

        /// <summary>Loads the fixture. Each call emits a fresh assembly name-collision-free image, because a manifest
        /// assembly cannot be unloaded and <c>Register</c> is idempotent per assembly.</summary>
        public static Assembly Load(byte[] image) => Assembly.Load(image);
    }
}
