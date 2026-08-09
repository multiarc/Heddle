using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Heddle.Precompiled;
using Microsoft.CodeAnalysis;

namespace Heddle.Generator.Observe
{
    /// <summary>
    /// Emits the compilation being built to a real <c>.dll</c> under the intermediate output path, at a
    /// content-addressed filename, so observation has <b>real types</b> for source-declared models and
    /// source-declared extensions rather than shims of them.
    /// <para>Persisting is the only assembly form that works everywhere. A <c>Reflection.Emit</c> assembly is
    /// dynamic and the engine's own reference provider refuses it; a byte-loaded one has no <c>Location</c> and the
    /// provider's in-memory path does not exist on <c>netstandard2.0</c>, which is the compiler host Visual Studio
    /// runs. An assembly with a real location clears both without a single engine change.</para>
    /// <para>The emit runs on the <b>pre-generator</b> compilation, which is what makes it non-circular: models and
    /// extension types are ordinary source, and nothing they declare depends on the code this generator is about to
    /// add. A compilation that does not compile simply fails to emit, and a failed emit is observation being
    /// unavailable — never a build error.</para>
    /// </summary>
    internal static class IntermediateAssembly
    {
        /// <summary>Bumped when the digest's own recipe changes, so an old cache entry can never be read as a new
        /// one.</summary>
        private const string DigestSchema = "heddle-observe-1";

        private static readonly object Gate = new object();

        /// <summary>Digest → the path already produced in this process, so a compiler server pays the emit once per
        /// distinct content rather than once per generation pass.</summary>
        private static readonly Dictionary<string, string> Produced =
            new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>
        /// The digest of everything that decides the emitted image: the sources by content, the references by
        /// module version id, and the shape of the compilation itself. Two builds whose digests agree produce the
        /// same assembly, and two that disagree produce different <i>filenames</i> — which is the property the load
        /// path's immutability rests on.
        /// </summary>
        internal static string Digest(Compilation compilation)
        {
            if (compilation == null)
                throw new ArgumentNullException(nameof(compilation));

            var builder = new StringBuilder();
            builder.Append(DigestSchema).Append('\n');
            builder.Append(compilation.AssemblyName ?? string.Empty).Append('\n');
            builder.Append(compilation.Language).Append('\n');
            builder.Append(compilation.Options.OutputKind).Append('\n');
            builder.Append(compilation.Options.Platform).Append('\n');

            var trees = new List<string>();
            foreach (var tree in compilation.SyntaxTrees)
            {
                string checksum;
                try
                {
                    checksum = ContentHash.ToHex(ToArray(tree.GetText().GetChecksum()));
                }
                catch (Exception)
                {
                    checksum = ContentHash.HashText(tree.ToString());
                }

                trees.Add((tree.FilePath ?? string.Empty) + "|" + checksum);
            }

            trees.Sort(StringComparer.Ordinal);
            foreach (var tree in trees)
                builder.Append(tree).Append('\n');

            var references = new List<string>();
            foreach (var reference in compilation.References)
                references.Add(ReferenceIdentity(reference));
            references.Sort(StringComparer.Ordinal);
            foreach (var reference in references)
                builder.Append(reference).Append('\n');

            return ContentHash.HashText(builder.ToString());
        }

        /// <summary>A reference's identity for the digest: its display path plus the module version id read off the
        /// metadata the compiler already has open. The path alone would be stale the moment a project reference is
        /// rebuilt in place, which is the one way a wrong answer could survive a rebuild.</summary>
        internal static string ReferenceIdentity(MetadataReference reference)
        {
            if (reference == null)
                return string.Empty;

            var display = reference.Display ?? string.Empty;
            var portable = reference as PortableExecutableReference;
            if (portable == null)
                return display + "|compilation";

            try
            {
                var metadata = portable.GetMetadata();
                var assembly = metadata as AssemblyMetadata;
                if (assembly != null)
                {
                    var builder = new StringBuilder(display);
                    foreach (var module in assembly.GetModules())
                        builder.Append('|').Append(module.GetModuleVersionId().ToString("N"));
                    return builder.ToString();
                }

                var single = metadata as ModuleMetadata;
                if (single != null)
                    return display + "|" + single.GetModuleVersionId().ToString("N");
            }
            catch (Exception)
            {
                // A reference whose metadata will not open cannot contribute an mvid; the display path is all the
                // identity there is, and the emit below will fail for the same reason anyway.
            }

            return display + "|no-mvid";
        }

        /// <summary>
        /// The content-addressed path of the emitted intermediate assembly, producing it if this is the first build
        /// to need that exact content.
        /// </summary>
        /// <returns>The path, or <c>null</c> with <paramref name="failure"/> set — always a reason to observe
        /// nothing, never a reason to fail a build.</returns>
        internal static string Produce(Compilation compilation, string directory, out bool emitted,
            out string failure)
        {
            emitted = false;
            failure = null;
            if (string.IsNullOrEmpty(directory))
            {
                failure = "no intermediate output path was supplied to the generator";
                return null;
            }

            var digest = Digest(compilation);
            lock (Gate)
            {
                if (Produced.TryGetValue(digest, out var known))
                    return known;
            }

            var path = Path.Combine(directory, "heddle.observe." + digest + ".dll");
            string emitFailure = null;
            bool wrote;
            var ok = ObserveCache.Ensure(path, stream =>
            {
                var result = compilation.Emit(stream);
                if (!result.Success)
                {
                    emitFailure = "the compilation being built does not compile yet";
                    throw new InvalidOperationException(emitFailure);
                }
            }, out wrote);

            if (!ok)
            {
                failure = emitFailure ?? "the intermediate assembly could not be written to '" + directory + "'";
                return null;
            }

            emitted = wrote;
            lock (Gate)
                Produced[digest] = path;
            return path;
        }

        /// <summary>Test seam: how many distinct contents this process has produced. A second generator run over
        /// identical inputs must not raise it.</summary>
        internal static int ProducedCount
        {
            get
            {
                lock (Gate)
                    return Produced.Count;
            }
        }

        private static byte[] ToArray(System.Collections.Immutable.ImmutableArray<byte> bytes)
        {
            var array = new byte[bytes.Length];
            for (var i = 0; i < bytes.Length; i++)
                array[i] = bytes[i];
            return array;
        }
    }
}
