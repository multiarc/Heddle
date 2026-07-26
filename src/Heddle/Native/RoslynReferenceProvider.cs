using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;

namespace Heddle.Native
{
    /// <summary>
    /// <para>Isolates <c>Microsoft.CodeAnalysis</c> metadata-reference concern. Every member whose signature
    /// mentions a Roslyn type lives here (and in <c>ContextCompilation</c>/<c>CSharpContext</c>), reached only from
    /// the C#-tier compile paths that sit behind the <c>Heddle.CSharpTierEnabled</c> feature switch. When the switch
    /// is trimmed off those call sites are dead code, so the linker removes this whole class together with the rest
    /// of the Roslyn graph — <see cref="AssemblyHelper"/> itself carries no Roslyn-typed member in its reachable
    /// surface.</para>
    /// <para>References are cached per <see cref="Assembly"/> in a <see cref="ConditionalWeakTable{TKey,TValue}"/>
    /// so an unloaded (collectible) model assembly is never pinned — the reload-leak invariant is preserved without
    /// a manual eviction call that would tie this class back into the reachable graph.</para>
    /// </summary>
    internal static class RoslynReferenceProvider
    {
        private static readonly ConditionalWeakTable<Assembly, MetadataReference> Cache =
            new ConditionalWeakTable<Assembly, MetadataReference>();

        /// <summary>Reuses cached per-assembly references; skips dynamic/in-memory assemblies that have no on-disk
        /// location.</summary>
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
#if NETSTANDARD2_0
                lock (Cache)
                {
                    if (Cache.TryGetValue(assembly, out cached))
                        return cached;
                    Cache.Add(assembly, reference);
                }
#else
                reference = Cache.GetValue(assembly, _ => CreateSafe(assembly) ?? reference);
#endif
            }

            return reference;
        }

        private static MetadataReference CreateSafe(Assembly assembly)
        {
            try
            {
                if (string.IsNullOrEmpty(assembly.Location))
                    return null;
                var moduleMetadata = ModuleMetadata.CreateFromFile(assembly.Location);
                var metadata = AssemblyMetadata.Create(moduleMetadata);
                return metadata.GetReference(filePath: assembly.FullName);
            }
            catch
            {
                return null;
            }
        }
    }
}
