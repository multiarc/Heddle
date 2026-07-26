using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Heddle.Tests
{
    /// <summary>
    /// <para>Builds a <b>genuine released-schema precompiled manifest assembly</b> — one whose IL references
    /// <c>PrecompiledExtensionBinding..ctor(string, string)</c>, the two-argument constructor the <b>shipped
    /// v2.0.0</b> generator emitted and that <b>no longer exists in metadata</b> since the prop-layout fingerprint
    /// landed as an optional third parameter rather than as a real overload.</para>
    ///
    /// <para><b>Which schemas this is about, verified against the tag.</b> At <c>v2.0.0</c> the generator emitted
    /// <c>schemaVersion: 2</c>, the engine accepted <c>1–2</c>, and the two-argument constructor was real. Schemas 1
    /// and 2 are therefore the <em>only</em> released shapes, and they are what this fixture reproduces. An earlier
    /// version of this doc said "schema 1–3", which was wrong in a way that mattered: schema 3 has never shipped, so
    /// naming it here implied the break's victim set included a shape no generator ever emitted.</para>
    ///
    /// <para><b>Why it is built this way, and what it refuses to do.</b> The obvious way to write an "old manifest"
    /// test is <c>new PrecompiledExtensionBinding("a", "b")</c> — which is exactly what
    /// <c>PropLayoutFingerprintTests.AManifestPredatingTheRowStillPasses</c> does. That call compiles against
    /// <em>today's</em> assembly, so the C# compiler silently binds it to the three-parameter constructor and passes
    /// <c>null</c> for the new one. The resulting IL is a <b>new</b>-schema call wearing an old-schema shape, which is
    /// precisely the substitution that let a binary break ship: the test asserted the compatibility window while
    /// exercising a call the window's oldest members cannot make.</para>
    ///
    /// <para>So the fixture never compiles against the real assembly. It compiles against a <b>reference facade</b>
    /// that declares the pre-break surface — including a real two-argument
    /// <c>PrecompiledExtensionBinding</c> constructor — under the real assembly's identity (name, version, and
    /// public key, taken from the live assembly and signed with the repository key). The emitted fixture therefore
    /// carries an assembly reference and a member reference indistinguishable from what a 2.0 generator emitted, and
    /// at load time that reference resolves to the real <c>Heddle</c>, where the member is gone. Nothing in this file
    /// can accidentally bind to the current constructor, because the current assembly is not among its
    /// references.</para>
    ///
    /// <para>Built at test time rather than checked in as a <c>.dll</c>: a committed binary cannot be re-derived,
    /// reviewed, or re-signed, and this repository checks in no compiled fixtures. The construction is the evidence,
    /// and it is auditable.</para>
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

            // The facade must carry the real assembly's public key, or the fixture's assembly reference records a
            // different identity and will not resolve to it. Public signing, not full signing: it stamps the identity
            // (name/version/public key) without producing a signature, which is all that is needed — the facade is a
            // compile-time reference and is never loaded. Full signing is also unavailable to Roslyn on this platform
            // (CS7027), so public signing is both sufficient and the only portable option.
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
