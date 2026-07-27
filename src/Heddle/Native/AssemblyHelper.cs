using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using Heddle.Helpers;
#if !NETSTANDARD2_0
using System.Runtime.Loader;
#endif

namespace Heddle.Native
{
    /// <summary>
    /// The assembly set engine type resolution and the C# tier see. The engine loads nothing: it observes what the
    /// host has already loaded into the default context, and takes anything else — a collectible or custom context, an
    /// assembly not yet loaded — only by explicit registration. Extension <b>names</b> are a separate question and come
    /// only from registration, never from observation.
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
        /// Adds assemblies the host has loaded into the default context. Bumps <see cref="Generation"/> when the set
        /// changes, so <see cref="ReflectionHelper"/> can rebuild its name maps instead of holding a snapshot taken
        /// before the assembly existed.
        /// </summary>
        private static void ObserveLoadedAssemblies()
        {
            var loaded = AppDomain.CurrentDomain.GetAssemblies();
            var stamp = StampOf(loaded);
            if (stamp == Volatile.Read(ref _observedStamp))
                return;   // the same assemblies in the same order; the per-assembly work below is not free

            lock (Assemblies)
            {
                var added = false;
                foreach (var assembly in loaded)
                {
                    // Observability first, THEN remember it. Remembering an excluded assembly would hold a strong
                    // reference to a collectible one and pin the very context the exclusion exists to let go.
                    if (!IsObservable(assembly))
                        continue;
                    // Reference identity, because GetName() allocates an AssemblyName per call and this runs on every
                    // type resolution — the already-seen case must not pay for one.
                    if (!Seen.Add(assembly))
                        continue;
                    if (AssemblyCache.TryAdd(assembly.GetName(), assembly))
                    {
                        Assemblies.Add(assembly);
                        added = true;
                    }
                }

                // Generation first, stamp second. A reader that takes the fast path above has seen the new stamp, and
                // therefore also the generation bump that preceded it, so it cannot conclude both "nothing loaded"
                // and "my maps are current" about an assembly that was just added.
                if (added)
                    Interlocked.Increment(ref _generation);
                Volatile.Write(ref _observedStamp, stamp);
            }
        }

        /// <summary>
        /// Identity digest of the loaded set: order-sensitive, allocation-free, and unequal whenever the membership
        /// differs. A plain count cannot do this job — an unloaded collectible context and a newly loaded assembly
        /// cancel out, and the resulting count match told the engine nothing had happened. The assembly that arrived
        /// during that window then stayed invisible, and stayed invisible on retry, until some unrelated later load
        /// happened to disturb the count, which is load order deciding what resolves.
        /// </summary>
        private static int StampOf(Assembly[] loaded)
        {
            var stamp = loaded.Length;
            foreach (var assembly in loaded)
                stamp = unchecked(stamp * 31 + RuntimeHelpers.GetHashCode(assembly));
            return stamp == 0 ? 1 : stamp;   // 0 is the never-observed seed and must not be a reachable digest
        }

        /// <summary>
        /// Excluded, and only these: a dynamic assembly, which has no types worth mapping; an assembly in a
        /// collectible or custom load context, because observing one would pin a context the host expects to unload;
        /// and the engine's <b>own</b> Roslyn-emitted expression assemblies, which would otherwise accumulate in the
        /// type maps one per compiled C# expression.
        /// <para><see cref="Assembly.Location"/> is deliberately <b>not</b> consulted. It is empty for every assembly
        /// in a single-file or WASM publish — the whole application, not an edge case — so filtering on it made the
        /// engine observe nothing at all there, and no host registration could repair type resolution for the
        /// framework assemblies a model type needs.</para>
        /// </summary>
        private static bool IsObservable(Assembly assembly)
        {
            if (assembly == null || assembly.IsDynamic)
                return false;
            if (EngineEmitted.TryGetValue(assembly, out _))
                return false;
#if !NETSTANDARD2_0
            var context = AssemblyLoadContext.GetLoadContext(assembly);
            if (context != null && context != AssemblyLoadContext.Default)
                return false;
#endif
            return true;
        }

        /// <summary>The engine's own emitted expression assemblies, held weakly so tracking them pins nothing.</summary>
        private static readonly ConditionalWeakTable<Assembly, object> EngineEmitted =
            new ConditionalWeakTable<Assembly, object>();

        /// <summary>Records an assembly the engine itself emitted and loaded, so observation skips it.</summary>
        internal static void MarkEngineEmitted(Assembly assembly)
        {
            if (assembly == null)
                return;
#if NETSTANDARD2_0
            lock (EngineEmitted)
            {
                if (!EngineEmitted.TryGetValue(assembly, out _))
                    EngineEmitted.Add(assembly, null);
            }
#else
            EngineEmitted.AddOrUpdate(assembly, null);
#endif
        }

        /// <summary>Every <b>observable</b> assembly already classified, by reference, so the per-pass work skips it.
        /// Holds only assemblies that are in the default context and therefore never unloaded — an excluded
        /// collectible assembly must never land here, or this set becomes the pin. Guarded by the
        /// <see cref="Assemblies"/> monitor.</summary>
        private static readonly HashSet<Assembly> Seen = new HashSet<Assembly>();

        /// <summary>The <see cref="StampOf"/> digest at the last pass; an unchanged digest means nothing to classify.</summary>
        private static int _observedStamp;

        private static int _generation;

        /// <summary>Increments whenever the observed or registered set changes; a cached view of the set is stale when
        /// its stamp differs.</summary>
        internal static int Generation => Volatile.Read(ref _generation);

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
                {
                    Assemblies.Add(assembly);
                    Interlocked.Increment(ref _generation);
                }
            }

            Runtime.TemplateFactory.RegisterExportedExtensions(assembly);
            ReflectionHelper.Reconfigure();
        }

        /// <summary>The former name of <see cref="Register"/>, kept so existing callers keep compiling — the same
        /// courtesy the public <see cref="HeddleTemplate.Configure(Assembly)"/> extends.</summary>
        public static void Configure(Assembly assembly) => Register(assembly);

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
                    Interlocked.Increment(ref _generation);
                }
            }

            ReflectionHelper.Reconfigure();
        }

        /// <summary>
        /// Removes every assembly registered by <see cref="RegisterModelAssemblies"/> and reconfigures, so a
        /// collectible model context can actually collect after <c>Unload()</c>. The C#-tier preparse cache is
        /// dropped too: a cached success carries the expression's result type, which for a workspace model type
        /// belongs to the context being unloaded. C#-tier metadata references need no eviction, but they do decide
        /// <i>when</i> the unload completes — a reference built over an assembly's in-memory metadata holds that
        /// assembly loaded while a caller still has it (see <see cref="RoslynReferenceProvider"/>), so the context
        /// goes once the last compilation using it does.
        /// </summary>
        public static void UnregisterModelAssemblies()
        {
            Runtime.PreparseCache.Clear();
            lock (Assemblies)
            {
                foreach (var assembly in ModelAssemblies)
                    Assemblies.Remove(assembly);
                foreach (var name in ModelNames)
                    AssemblyCache.TryRemove(name, out _);
                ModelAssemblies.Clear();
                ModelNames.Clear();
                Interlocked.Increment(ref _generation);
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
