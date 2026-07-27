using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;

namespace Heddle.Native
{
    /// <summary>
    /// Isolates Roslyn types for trimming when <c>Heddle.CSharpTierEnabled</c> is off, and caches one
    /// <see cref="MetadataReference"/> per <see cref="Assembly"/>.
    /// <para><b>The cache does not by itself decide when an assembly can go.</b> A reference built over an
    /// assembly's in-memory metadata points at memory the runtime frees when the assembly's load context unloads,
    /// so the assembly is anchored to the reference (see <see cref="Owners"/>): while any caller still holds a
    /// reference, its assembly stays loaded. That defers a collectible context's unload until the last compilation
    /// built against it is finished; it does not prevent one. The engine holds no reference of its own — the
    /// per-assembly cache is keyed weakly and the two tables form a cycle the collector resolves — so an unload
    /// completes as soon as callers let go.</para>
    /// </summary>
    internal static class RoslynReferenceProvider
    {
        private static readonly ConditionalWeakTable<Assembly, MetadataReference> Cache =
            new ConditionalWeakTable<Assembly, MetadataReference>();

        /// <summary>Reuses cached per-assembly references. An assembly with no on-disk location still yields one:
        /// its loaded metadata image is read directly, because in a single-file or WASM publish no assembly has a
        /// location and gating on one left the C# tier with nothing to compile against.</summary>
        internal static List<MetadataReference> Build(IReadOnlyList<Assembly> assemblies)
        {
            var result = new List<MetadataReference>(assemblies.Count);
            foreach (var assembly in assemblies)
            {
                if (assembly == null)
                    continue;
                var reference = GetOrCreate(assembly);
                if (reference != null)
                    result.Add(reference);
            }

            return result;
        }

        private static MetadataReference GetOrCreate(Assembly assembly)
        {
            if (Cache.TryGetValue(assembly, out var cached))
                return cached;

            var reference = CreateSafe(assembly);
            if (reference != null)
            {
                lock (Cache)
                {
                    if (Cache.TryGetValue(assembly, out cached))
                        return cached;
                    Cache.Add(assembly, reference);
                    // The reverse edge, and it is what keeps the reference safe to use. For an assembly with no file
                    // behind it the reference points at metadata the runtime owns, which unloading its load context
                    // frees — and a caller can hold the reference long after the engine has let the assembly go. The
                    // read then lands on freed memory: an AccessViolationException, which cannot be caught, or a
                    // silently corrupt compile. Anchoring the assembly to the reference defers the unload until the
                    // last holder is finished. The two tables form a cycle only the collector needs to understand,
                    // and it does: neither entry keeps the other alive once both are unreachable.
                    Owners.Add(reference, assembly);
                }
            }

            return reference;
        }

        /// <summary>Keeps an assembly alive for exactly as long as a reference built over its metadata is — the
        /// deliberate trade: a deferred unload rather than a read of freed metadata.</summary>
        private static readonly ConditionalWeakTable<MetadataReference, Assembly> Owners =
            new ConditionalWeakTable<MetadataReference, Assembly>();

        private static MetadataReference CreateSafe(Assembly assembly)
        {
            try
            {
                if (assembly.IsDynamic)
                    return null;

                if (!string.IsNullOrEmpty(assembly.Location))
                    return Reference(ModuleMetadata.CreateFromFile(assembly.Location), assembly);

                return FromLoadedImage(assembly);
            }
            catch
            {
                return null;
            }
        }

        private static MetadataReference Reference(ModuleMetadata module, Assembly assembly)
        {
            return AssemblyMetadata.Create(module).GetReference(filePath: assembly.FullName);
        }

#if NETSTANDARD2_0
        /// <summary>.NET Framework has no single-file bundle and no raw-metadata accessor, so a location is the only
        /// way in on this target.</summary>
        private static MetadataReference FromLoadedImage(Assembly assembly) => null;
#else
        /// <summary>
        /// Builds a reference from the metadata the runtime already has mapped, for assemblies that exist only in
        /// memory — every assembly in a single-file or WASM publish, and the byte-loaded model and extension
        /// assemblies the language service hands us.
        /// <para>The blob is owned by the runtime and freed when the assembly's load context unloads, so a reference
        /// over it is only safe while the assembly is loaded. That is what <see cref="Owners"/> guarantees; the
        /// alternative is copying the metadata, which would cost a second image of every in-memory assembly — every
        /// assembly at all in a single-file or WASM publish.</para>
        /// </summary>
        private static unsafe MetadataReference FromLoadedImage(Assembly assembly)
        {
            if (!System.Reflection.Metadata.AssemblyExtensions.TryGetRawMetadata(assembly, out var blob, out var length))
                return null;

            return Reference(ModuleMetadata.CreateFromMetadata((System.IntPtr)blob, length), assembly);
        }
#endif
    }
}
