using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Heddle.Helpers;
#if !NETSTANDARD2_0
using System.Runtime.Loader;
#endif

namespace Heddle.Native
{
    /// <summary>
    /// The assembly set engine type resolution and the C# tier see. The engine loads nothing: it observes what the
    /// host has already loaded from disk into the default context, and takes anything else — in-memory assemblies,
    /// collectible contexts, extension providers — only by explicit registration.
    /// </summary>
    internal static class AssemblyHelper
    {
        private static readonly ConcurrentDictionary<AssemblyName, Assembly> AssemblyCache =
            new ConcurrentDictionary<AssemblyName, Assembly>(AssemblyNameEqualityComparer.Instance);

        private static readonly List<Assembly> Assemblies = new List<Assembly>();

        private static readonly List<Assembly> ModelAssemblies = new List<Assembly>();
        private static readonly List<AssemblyName> ModelNames = new List<AssemblyName>();

        /// <summary>
        /// The observed and registered assemblies. Callers that enumerate must hold the returned list's monitor.
        /// </summary>
        public static IReadOnlyList<Assembly> GetAssemblies()
        {
            ObserveLoadedAssemblies();
            return Assemblies;
        }

        /// <summary>
        /// Adds assemblies the host has loaded into the default context and can name on disk. Assemblies without a
        /// file location (emitted expression assemblies, streams) and assemblies in a collectible or custom context
        /// are skipped: observing those would pin a context the host expects to unload, and the engine's own emitted
        /// assemblies would accumulate in the type maps.
        /// </summary>
        private static void ObserveLoadedAssemblies()
        {
            var loaded = AppDomain.CurrentDomain.GetAssemblies();
            lock (Assemblies)
            {
                foreach (var assembly in loaded)
                {
                    if (!IsObservable(assembly))
                        continue;
                    if (AssemblyCache.TryAdd(assembly.GetName(), assembly))
                        Assemblies.Add(assembly);
                }
            }
        }

        private static bool IsObservable(Assembly assembly)
        {
            if (assembly == null || assembly.IsDynamic || string.IsNullOrEmpty(assembly.Location))
                return false;
#if !NETSTANDARD2_0
            var context = AssemblyLoadContext.GetLoadContext(assembly);
            if (context != null && context != AssemblyLoadContext.Default)
                return false;
#endif
            return true;
        }

        /// <summary>
        /// Registers an assembly the engine would not otherwise see, and offers its assembly-level
        /// <c>[ExportExtensions]</c> to the extension registry. Idempotent per assembly; repeatable.
        /// </summary>
        public static void Register(Assembly assembly)
        {
            if (assembly == null)
                throw new ArgumentNullException(nameof(assembly));

            lock (Assemblies)
            {
                if (AssemblyCache.TryAdd(assembly.GetName(), assembly))
                    Assemblies.Add(assembly);
            }

            Runtime.TemplateFactory.RegisterExportedExtensions(assembly);
            ReflectionHelper.Reconfigure();
        }

        /// <summary>
        /// Adds workspace model assemblies so engine type resolution
        /// (<see cref="ReflectionHelper.ResolveType(string, ICollection{string})"/>) can see their types. The
        /// registration is tracked so <see cref="UnregisterModelAssemblies"/> can remove exactly these entries on
        /// reload — otherwise the static caches would pin a collectible model <c>AssemblyLoadContext</c> forever.
        /// </summary>
        public static void RegisterModelAssemblies(IReadOnlyList<Assembly> assemblies)
        {
            if (assemblies == null)
                throw new ArgumentNullException(nameof(assemblies));

            lock (Assemblies)
            {
                foreach (var assembly in assemblies)
                {
                    if (assembly == null)
                        continue;
                    var name = assembly.GetName();
                    if (!AssemblyCache.TryAdd(name, assembly))
                        continue;
                    Assemblies.Add(assembly);
                    ModelAssemblies.Add(assembly);
                    ModelNames.Add(name);
                }
            }

            ReflectionHelper.Reconfigure();
        }

        /// <summary>
        /// Removes every assembly registered by <see cref="RegisterModelAssemblies"/> and reconfigures, so a
        /// collectible model context can actually collect after <c>Unload()</c>. C#-tier metadata references are held
        /// only in a weak per-assembly cache and need no eviction here.
        /// </summary>
        public static void UnregisterModelAssemblies()
        {
            lock (Assemblies)
            {
                foreach (var assembly in ModelAssemblies)
                    Assemblies.Remove(assembly);
                foreach (var name in ModelNames)
                    AssemblyCache.TryRemove(name, out _);
                ModelAssemblies.Clear();
                ModelNames.Clear();
            }

            ReflectionHelper.Reconfigure();
        }

        /// <summary>Sole Roslyn-typed member; called only from C#-tier compile paths behind
        /// <c>Heddle.CSharpTierEnabled</c> switch, so trimmed publishes with the switch off make this dead.</summary>
        internal static List<Microsoft.CodeAnalysis.MetadataReference> GetApplicationReferences()
        {
            ObserveLoadedAssemblies();
            lock (Assemblies)
            {
                return RoslynReferenceProvider.Build(Assemblies.ToArray());
            }
        }

        private static readonly ConcurrentDictionary<string, AssemblyName> AssemblyNameCache =
            new ConcurrentDictionary<string, AssemblyName>();

        public static AssemblyName GetAssemblyName(string assemblyName)
        {
            if (AssemblyNameCache.TryGetValue(assemblyName, out var cached))
                return cached;

            AssemblyName resolved;
            var assemblies = GetAssemblies();
            lock (assemblies)
            {
                resolved = assemblies.Select(asm => asm.GetName())
                    .FirstOrDefault(name => name.Name == assemblyName);
            }

            // Not cached when unresolved: the assembly may be loaded later.
            return resolved == null ? null : AssemblyNameCache.GetOrAdd(assemblyName, resolved);
        }
    }
}
