using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text;
using Heddle.Precompiled;
using Microsoft.CodeAnalysis;

namespace Heddle.Generator.Observe
{
    /// <summary>
    /// The <b>implementation</b> image behind each reference the compiler was handed as a reference assembly.
    /// <para>A project-to-project reference compiles against the referenced project's generated reference assembly:
    /// metadata with its method bodies thrown away, which the runtime refuses to execute. MSBuild knows both paths —
    /// <c>@(ReferencePath)</c> is the implementation and its <c>ReferenceAssembly</c> metadatum is what
    /// <c>CoreCompile</c> actually passes — and <c>HeddleObserveImplementationPath</c> declares the first for exactly
    /// the references that carry the second. The compilation keeps compiling against the reference assemblies;
    /// observation, which has to <i>run</i> the engine, loads the implementations instead.</para>
    /// <para><b>Matched by assembly identity, never by path or file name.</b> A reference assembly and its
    /// implementation carry the same identity by construction, so identity equality is the proof that substituting
    /// one for the other cannot change which type a metadata name maps onto. Anything whose identity is not exactly
    /// the one the compilation resolved is not substituted at all, which is the declining direction.</para>
    /// <para>RS1035 shapes the reading: <c>System.IO.File</c> is banned in an analyzer and <c>System.IO.FileStream</c>
    /// is not, so a path that cannot be opened contributes nothing rather than being asked about first.</para>
    /// </summary>
    internal sealed class ImplementationReferences
    {
        /// <summary>The empty set — a build that declared nothing, which is every build that resolves no reference
        /// assemblies and every host that does not import the targets.</summary>
        internal static readonly ImplementationReferences None =
            new ImplementationReferences(new Dictionary<AssemblyIdentity, Entry>(), string.Empty);

        private readonly Dictionary<AssemblyIdentity, Entry> _byIdentity;

        private ImplementationReferences(Dictionary<AssemblyIdentity, Entry> byIdentity, string digest)
        {
            _byIdentity = byIdentity;
            Digest = digest;
        }

        /// <summary>One implementation image: where it is, and the module version id read off <i>it</i> rather than
        /// off the reference assembly the compiler holds. The two differ on every rebuild that changes a method body
        /// and nothing else, which is precisely the change a reference assembly is designed not to show.</summary>
        internal readonly struct Entry
        {
            internal Entry(string path, string moduleVersionId)
            {
                Path = path;
                ModuleVersionId = moduleVersionId;
            }

            internal string Path { get; }

            internal string ModuleVersionId { get; }
        }

        /// <summary>Every substituted identity and the module version id substituted for it, as one ordinal-sorted
        /// string. It joins the observation memo's key: an implementation rebuilt behind an unchanged reference
        /// assembly is a different answer, and nothing else in the key would say so.</summary>
        internal string Digest { get; }

        /// <summary>
        /// Reads the declared implementation paths. The declaration is a <c>|</c>-separated list, in whatever order
        /// MSBuild's reference resolution produced it; the paths are sorted ordinally before they are read, so two
        /// builds that resolved the same references reach the same answer whatever order they arrived in.
        /// <para>The separator is <c>|</c> and not the <c>;</c> MSBuild joins items with, because this arrives
        /// through the generated <c>.editorconfig</c> and an editorconfig value ends at the first <c>;</c> — the
        /// rest is a comment. A <c>;</c>-joined list reaches here as its first entry alone.</para>
        /// </summary>
        internal static ImplementationReferences Read(string declared)
        {
            if (string.IsNullOrEmpty(declared))
                return None;

            var paths = new List<string>();
            foreach (var part in declared.Split('|'))
            {
                var path = part.Trim();
                if (path.Length != 0)
                    paths.Add(path);
            }

            if (paths.Count == 0)
                return None;

            paths.Sort(StringComparer.Ordinal);

            var byIdentity = new Dictionary<AssemblyIdentity, Entry>();
            var rows = new List<string>();
            foreach (var path in paths)
            {
                if (!TryReadIdentity(path, out var identity, out var moduleVersionId))
                    continue;

                // First path wins, and the sort above is what makes "first" the same on every machine. Two files
                // claiming one identity is a build that already contradicts itself; picking deterministically is
                // the only thing this layer can honestly do about it.
                if (byIdentity.ContainsKey(identity))
                    continue;

                byIdentity[identity] = new Entry(path, moduleVersionId);
                rows.Add(identity.GetDisplayName() + "|" + moduleVersionId);
            }

            if (byIdentity.Count == 0)
                return None;

            var digest = new StringBuilder();
            foreach (var row in rows)
                digest.Append(row).Append('\n');
            return new ImplementationReferences(byIdentity, ContentHash.HashText(digest.ToString()));
        }

        /// <summary>The implementation declared for an identity the compilation resolved, or <c>false</c> — which
        /// leaves the caller with the reference the compiler gave it and no guess in between.</summary>
        internal bool TryGet(AssemblyIdentity identity, out Entry entry)
        {
            if (identity == null)
            {
                entry = default;
                return false;
            }

            return _byIdentity.TryGetValue(identity, out entry);
        }

        /// <summary>The identity and module version id of the image at <paramref name="path"/>, read from the file
        /// without loading it. Anything that will not open, is not an assembly, or carries no assembly identity
        /// contributes nothing.</summary>
        private static bool TryReadIdentity(string path, out AssemblyIdentity identity, out string moduleVersionId)
        {
            identity = null;
            moduleVersionId = null;
            try
            {
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var metadata = AssemblyMetadata.CreateFromStream(stream, PEStreamOptions.PrefetchMetadata))
                {
                    var modules = metadata.GetModules();
                    if (modules.Length == 0)
                        return false;
                    var module = modules[0];
                    moduleVersionId = module.GetModuleVersionId().ToString("N");
                    identity = IdentityOf(module.GetMetadataReader());
                }
            }
            catch (Exception)
            {
                return false;
            }

            return identity != null && moduleVersionId != null;
        }

        /// <summary>The assembly identity a module's own metadata declares, built rather than parsed so it compares
        /// equal to the identity Roslyn gave the reference assembly under the same rules — key/token, culture,
        /// retargetability and content type all included.</summary>
        private static AssemblyIdentity IdentityOf(MetadataReader reader)
        {
            if (!reader.IsAssembly)
                return null;

            var definition = reader.GetAssemblyDefinition();
            var publicKey = definition.PublicKey.IsNil
                ? ImmutableArray<byte>.Empty
                : ImmutableArray.Create(reader.GetBlobBytes(definition.PublicKey));
            return new AssemblyIdentity(
                reader.GetString(definition.Name),
                definition.Version,
                definition.Culture.IsNil ? null : reader.GetString(definition.Culture),
                publicKey,
                hasPublicKey: !publicKey.IsEmpty,
                isRetargetable: (definition.Flags & System.Reflection.AssemblyFlags.Retargetable) != 0,
                contentType: (System.Reflection.AssemblyContentType)
                    (((int)(definition.Flags & System.Reflection.AssemblyFlags.ContentTypeMask)) >> 9));
        }
    }
}
