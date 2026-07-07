using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.CSharp.RuntimeBinder;
using Microsoft.Extensions.DependencyModel;
using Heddle.Helpers;

namespace Heddle.Native
{
    internal static class AssemblyHelper
    {
#if NETSTANDARD2_0
        private static readonly string NetStandardAssemblyFullName =
            "netstandard, Version=2.0.0.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51";
#endif

        private static volatile DependencyContext _dependencyContext;

        // Phase 9 D4: the assembly registry carries no Microsoft.CodeAnalysis type — the metadata-reference concern
        // moved to RoslynReferenceProvider (reached only past the C#-tier feature switch), so the trimmer can drop
        // the whole Roslyn graph. The cache value is simply the deduped assembly.
        private static readonly ConcurrentDictionary<AssemblyName, Assembly> AssemblyCache =
            new ConcurrentDictionary<AssemblyName, Assembly>(AssemblyNameEqualityComparer.Instance);

        private static readonly List<Assembly> Assemblies;

        private static void WalkReferenceAssemblies(Assembly current)
        {
            if (AssemblyCache.TryAdd(current.GetName(), current))
            {
                lock (Assemblies)
                {
                    Assemblies.Add(current);
                    foreach (var assemblyName in current.GetReferencedAssemblies())
                    {
                        AssemblyLoadSafe(assemblyName, (dependent) =>
                        {
                            if (AssemblyCache.TryAdd(dependent.GetName(), dependent))
                            {
                                Assemblies.Add(dependent);
                                WalkReferenceAssemblies(dependent);
                            }
                        });
                    }
                }
            }
        }

        private static void AssemblyLoadSafe(AssemblyName assemblyName, Action<Assembly> continuation)
        {
            try
            {
                var asm = Assembly.Load(assemblyName);
                continuation?.Invoke(asm);
            }
            catch (FileNotFoundException)
            {
            }
            catch (ReflectionTypeLoadException)
            {
            }
            catch (FileLoadException)
            {
            }
            catch (BadImageFormatException)
            {
                //native or wrong-bitness assembly, skip load issues
            }
        }

        public static IReadOnlyList<Assembly> GetAssemblies()
        {
            return Assemblies;
        }

        private static readonly List<Assembly> ModelAssemblies = new List<Assembly>();
        private static readonly List<AssemblyName> ModelNames = new List<AssemblyName>();

        /// <summary>
        /// Phase 6 D14/D3: adds the workspace model assemblies to the static assembly list so engine
        /// type resolution (<see cref="ReflectionHelper.ResolveType(string, ICollection{string})"/>) can see their
        /// types, invalidates the type caches and reconfigures the name maps. The registration is tracked so
        /// <see cref="UnregisterModelAssemblies"/> can remove exactly these entries on reload — otherwise the
        /// static caches would pin a collectible model <c>AssemblyLoadContext</c> forever (the reload-leak root).
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

                InvalidateTypeCaches();
            }

            ReflectionHelper.Reconfigure();
        }

        /// <summary>
        /// Phase 6 D14/D3: removes every assembly registered by <see cref="RegisterModelAssemblies"/> from the
        /// static lists and cache, invalidates the type caches and reconfigures — clearing the engine-side
        /// references so a collectible model context can actually collect after <c>Unload()</c>. C#-tier metadata
        /// references are held only in a weak per-assembly cache (RoslynReferenceProvider) and need no eviction here.
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

                InvalidateTypeCaches();
            }

            ReflectionHelper.Reconfigure();
        }

        private static void InvalidateTypeCaches()
        {
            _allTypes = null;
            _allTypesByCustomAttribute = null;
            _exportedTypesByCustomAttribute = null;
            AssemblyExportedTypes.Clear();
        }

        private static readonly object LockObj = new object();

        public static IReadOnlyList<Type> GetAllTypes()
        {
            if (_allTypes == null)
            {
                lock (LockObj)
                {
                    if (_allTypes == null)
                    {
                        var result = new List<Type>();

                        foreach (var assembly in GetAssemblies())
                        {
                            try
                            {
                                foreach (var type in assembly.GetTypes())
                                {
                                    result.Add(type);
                                    if (type.IsPublic)
                                    {
                                        AssemblyExportedTypes.AddOrUpdate(assembly.GetName(), () => new List<Type> {type}, list => { list.Add(type); });
                                    }
                                }
                            }
                            catch
                            {
                                //skip type load exceptions
                            }
                        }

                        _allTypes = result;
                    }
                }
            }

            return _allTypes;
        }

        public static IReadOnlyList<Type> GetAssemblyExportedTypes(AssemblyName assemblyName)
        {
            if (_allTypes == null)
            {
                //ensure init
                GetAllTypes();
            }

            return AssemblyExportedTypes.GetValueOrDefault(assemblyName);
        }

        public static IEnumerable<Type> GetAssemblyExportedTypes()
        {
            if (_allTypes == null)
            {
                //ensure init
                GetAllTypes();
            }

            return AssemblyExportedTypes.SelectMany(ex => ex.Value);
        }

        public static IReadOnlyList<TypeWithCustomAttributeValues> GetTypesByCustomAttribute<T>()
            where T : Attribute
        {
            return GetTypesByCustomAttribute(typeof(T));
        }

        public static IReadOnlyList<TypeWithCustomAttributeValues> GetTypesByCustomAttribute(Type customAttributeType)
        {
            if (_allTypesByCustomAttribute == null)
            {
                lock (LockObj)
                {
                    if (_allTypesByCustomAttribute == null)
                    {
                        var result = new Dictionary<Type, List<TypeWithCustomAttributeValues>>(TypeEqualityComparer.Instance);

                        foreach (var type in GetAllTypes())
                        {
                            var customAttributes = type.GetCustomAttributes(true);

                            foreach (var customAttribute in customAttributes.GroupBy(c => c.GetType()))
                            {
                                result.AddOrUpdate(customAttribute.Key, () => new List<TypeWithCustomAttributeValues>
                                {
                                    new TypeWithCustomAttributeValues
                                    {
                                        Type = type,
                                        AttributeType = customAttribute.Key,
                                        AttributeValues = customAttribute.ToArray()
                                    }
                                }, list =>
                                {
                                    list.Add(new TypeWithCustomAttributeValues
                                    {
                                        Type = type,
                                        AttributeType = customAttribute.Key,
                                        AttributeValues = customAttribute.ToArray()
                                    });
                                });
                            }
                        }

                        _allTypesByCustomAttribute = result;
                    }
                }
            }

            return _allTypesByCustomAttribute.GetValueOrDefault(customAttributeType);
        }

        public static IReadOnlyList<TypeWithCustomAttributeValues> GetExportedTypesByCustomAttribute(Type customAttributeType)
        {
            if (_exportedTypesByCustomAttribute == null)
            {
                lock (LockObj)
                {
                    if (_exportedTypesByCustomAttribute == null)
                    {
                        var result = new Dictionary<Type, List<TypeWithCustomAttributeValues>>(TypeEqualityComparer.Instance);

                        foreach (var type in GetAssemblyExportedTypes())
                        {
                            var customAttributes = type.GetCustomAttributes(true);

                            foreach (var customAttribute in customAttributes.GroupBy(c => c.GetType()))
                            {
                                result.AddOrUpdate(customAttribute.Key, () => new List<TypeWithCustomAttributeValues>
                                {
                                    new TypeWithCustomAttributeValues
                                    {
                                        Type = type,
                                        AttributeType = customAttribute.Key,
                                        AttributeValues = customAttribute.ToArray()
                                    }
                                }, list =>
                                {
                                    list.Add(new TypeWithCustomAttributeValues
                                    {
                                        Type = type,
                                        AttributeType = customAttribute.Key,
                                        AttributeValues = customAttribute.ToArray()
                                    });
                                });
                            }
                        }

                        _exportedTypesByCustomAttribute = result;
                    }
                }
            }

            return _exportedTypesByCustomAttribute.GetValueOrDefault(customAttributeType);
        }

        public class TypeWithCustomAttributeValues
        {
            public Type Type { get; set; }

            public object[] AttributeValues { get; set; }

            public Type AttributeType { get; set; }
        }

        private static volatile List<Type> _allTypes;

        private static volatile Dictionary<Type, List<TypeWithCustomAttributeValues>> _allTypesByCustomAttribute;

        private static volatile Dictionary<Type, List<TypeWithCustomAttributeValues>> _exportedTypesByCustomAttribute;

        private static readonly Dictionary<AssemblyName, List<Type>> AssemblyExportedTypes = new Dictionary<AssemblyName, List<Type>>(AssemblyNameEqualityComparer.Instance);

        private static Assembly _applicationAssembly;

        static AssemblyHelper()
        {
            var mainAppAssembly = Assembly.GetEntryAssembly();

            Assemblies = new List<Assembly>();

            if (mainAppAssembly != null)
            {
                _applicationAssembly = mainAppAssembly;
                _dependencyContext = DependencyContext.Load(_applicationAssembly);
                WalkReferenceAssemblies(_applicationAssembly);
            }

            WalkReferenceAssemblies(typeof(DynamicAttribute).GetTypeInfo().Assembly);
#if NETSTANDARD2_0
            //without this reference, net48 target doesn't work from CodeAnalysis context
            AssemblyLoadSafe(new AssemblyName(NetStandardAssemblyFullName), WalkReferenceAssemblies);
#endif
            WalkReferenceAssemblies(typeof(CSharpArgumentInfo).GetTypeInfo().Assembly);
            EnsureApplicationAssembliesWalked();
        }

        private static bool _configured;

        public static void Configure(Assembly startupAssembly)
        {
            if (!_configured)
            {
                _dependencyContext = DependencyContext.Load(startupAssembly);
                _applicationAssembly = startupAssembly;
                WalkReferenceAssemblies(startupAssembly);
                EnsureApplicationAssembliesWalked();
                _configured = true;
                ReflectionHelper.Reconfigure();
            }
        }

        /// <summary>Walks the app's dependency-context default assemblies into the registry (the non-Roslyn half of
        /// the former <c>GetApplicationReferences</c> warm-up). Roslyn-free, so it stays on the reachable path.</summary>
        private static void EnsureApplicationAssembliesWalked()
        {
            if (_dependencyContext != null && !_configured)
            {
                foreach (var name in _dependencyContext.GetDefaultAssemblyNames())
                {
                    if (!AssemblyCache.ContainsKey(name))
                    {
                        AssemblyLoadSafe(name, WalkReferenceAssemblies);
                    }
                }
            }
        }

        /// <summary>Phase 9 D4: the sole Roslyn-typed member of the assembly-helper surface. Called only from the
        /// C#-tier compile paths behind the <c>Heddle.CSharpTierEnabled</c> switch, so a trimmed publish that turns
        /// the switch off makes this (and <see cref="RoslynReferenceProvider"/>) dead — the whole
        /// <c>Microsoft.CodeAnalysis</c> graph drops out of the bundle.</summary>
        internal static List<Microsoft.CodeAnalysis.MetadataReference> GetApplicationReferences()
        {
            EnsureApplicationAssembliesWalked();
            return RoslynReferenceProvider.Build(Assemblies);
        }

        private static readonly ConcurrentDictionary<string, AssemblyName> AssemblyNameCache = new ConcurrentDictionary<string, AssemblyName>();

        public static AssemblyName GetAssemblyName(string assemblyName)
        {
            return AssemblyNameCache.GetOrAdd(assemblyName, name => GetAssemblies().Where(asm => asm.GetName().Name == assemblyName).Select(asm => asm.GetName()).FirstOrDefault());
        }
    }
}
