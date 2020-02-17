using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CSharp.RuntimeBinder;
using Microsoft.Extensions.DependencyModel;
using Templates.Helpers;

namespace Templates.Native
{
    internal static class AssemblyHelper
    {
        private static volatile DependencyContext _dependencyContext;

        private static readonly ConcurrentDictionary<AssemblyName, Tuple<Assembly, MetadataReference>> AssemblyCache =
            new ConcurrentDictionary<AssemblyName, Tuple<Assembly, MetadataReference>>(AssemblyNameEqualityComparer.Instance);

        private static readonly List<MetadataReference> MetadataReferences;
        private static readonly List<Assembly> Assemblies;

        private static void WalkReferenceAssemblies(Assembly current)
        {
            var currentInfo = new Tuple<Assembly, MetadataReference>(current, CreateMetadataFileReference(current));
            if (AssemblyCache.TryAdd(current.GetName(), currentInfo))
            {
                lock (Assemblies)
                {
                    Assemblies.Add(currentInfo.Item1);
                    MetadataReferences.Add(currentInfo.Item2);
                    foreach (var assemblyName in current.GetReferencedAssemblies())
                    {
                        try
                        {
                            var dependent = Assembly.Load(assemblyName);
                            var dependentInfo =
                                new Tuple<Assembly, MetadataReference>(dependent,
                                    CreateMetadataFileReference(dependent));
                            if (AssemblyCache.TryAdd(dependent.GetName(), dependentInfo))
                            {
                                MetadataReferences.Add(dependentInfo.Item2);
                                Assemblies.Add(dependentInfo.Item1);
                                WalkReferenceAssemblies(dependent);
                            }
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
                    }
                }
            }
        }

        public static IReadOnlyList<Assembly> GetAssemblies()
        {
            return Assemblies;
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

            MetadataReferences = new List<MetadataReference>();
            Assemblies = new List<Assembly>();

            if (mainAppAssembly != null)
            {
                _applicationAssembly = mainAppAssembly;
                _dependencyContext = DependencyContext.Load(_applicationAssembly);
                WalkReferenceAssemblies(_applicationAssembly);
            }

            WalkReferenceAssemblies(typeof(DynamicAttribute).GetTypeInfo().Assembly);
            WalkReferenceAssemblies(typeof(CSharpArgumentInfo).GetTypeInfo().Assembly);
            GetApplicationReferences();
        }

        private static bool _configured;

        public static void Configure(Assembly startupAssembly)
        {
            if (!_configured)
            {
                _dependencyContext = DependencyContext.Load(startupAssembly);
                _applicationAssembly = startupAssembly;
                WalkReferenceAssemblies(startupAssembly);
                GetApplicationReferences();
                _configured = true;
                ReflectionHelper.Reconfigure();
            }
        }

        private static MetadataReference CreateMetadataFileReference(Assembly asm)
        {
            var moduleMetadata = ModuleMetadata.CreateFromFile(asm.Location);
            var metadata = AssemblyMetadata.Create(moduleMetadata);
            return metadata.GetReference(filePath: asm.FullName);
        }

        internal static List<MetadataReference> GetApplicationReferences()
        {
            if (_dependencyContext != null && !_configured)
            {
                foreach (var name in _dependencyContext.GetDefaultAssemblyNames())
                {
                    if (!AssemblyCache.ContainsKey(name))
                    {
                        try
                        {
                            var asm = Assembly.Load(name);
                            WalkReferenceAssemblies(asm);
                        }
                        catch
                        {
                            //skip load issues
                        }
                    }
                }
            }

            return MetadataReferences;
        }

        private static readonly ConcurrentDictionary<string, AssemblyName> AssemblyNameCache = new ConcurrentDictionary<string, AssemblyName>();

        public static AssemblyName GetAssemblyName(string assemblyName)
        {
            return AssemblyNameCache.GetOrAdd(assemblyName, name => GetAssemblies().Where(asm => asm.GetName().Name == assemblyName).Select(asm => asm.GetName()).FirstOrDefault());
        }
    }
}