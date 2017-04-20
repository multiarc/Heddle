using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.PlatformAbstractions;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.DependencyModel;
using Templates.Helpers;

#if NETSTANDARD1_6
using System.Runtime.Loader;
#endif

namespace Templates.Native
{
    public static class AssemblyHelper
    {
        private static volatile DependencyContext _dependencyContext;

        private static readonly ConcurrentDictionary<string, Tuple<Assembly, MetadataReference>> AssemblyCache =
            new ConcurrentDictionary<string, Tuple<Assembly, MetadataReference>>(StringComparer.OrdinalIgnoreCase);

        private static readonly List<MetadataReference> MetadataReferences;
        private static readonly List<Assembly> Assemblies;

#if NETSTANDARD1_6

        internal class TemplateLoadContext : AssemblyLoadContext
        {
            protected override Assembly Load(AssemblyName assemblyName)
            {
                return Default.LoadFromAssemblyName(assemblyName);
            }

            public Assembly Load(Stream assembly, Stream assemblySymbols)
            {
                return LoadFromStream(assembly, assemblySymbols);
            }
        }

#endif

        private static void WalkReferenceAssemblies(Assembly current)
        {
            var currentInfo = new Tuple<Assembly, MetadataReference>(current, CreateMetadataFileReference(current));
            if (AssemblyCache.TryAdd(current.FullName, currentInfo))
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
                            var dependentInfo = new Tuple<Assembly, MetadataReference>(dependent, CreateMetadataFileReference(dependent));
                            if (AssemblyCache.TryAdd(dependent.FullName, dependentInfo))
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
                    }
                }
            }
        }

        internal static ICollection<Assembly> GetAssemblies()
        {
            return Assemblies;
        }

        private static Assembly _applicationAssembly;

        static AssemblyHelper()
        {
            var appEnvironment = PlatformServices.Default.Application;
            string applicationName = appEnvironment.ApplicationName ?? "Templates";
            _applicationAssembly = Assembly.Load(new AssemblyName(applicationName));
            _dependencyContext = DependencyContext.Load(_applicationAssembly);
            MetadataReferences = new List<MetadataReference>();
            Assemblies = new List<Assembly>();
            WalkReferenceAssemblies(_applicationAssembly);
            WalkReferenceAssemblies(typeof(System.Runtime.CompilerServices.DynamicAttribute).GetTypeInfo().Assembly);
#if NET461
            WalkReferenceAssemblies(Assembly.Load(new AssemblyName("System.Runtime, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a")));
#endif
            WalkReferenceAssemblies(typeof(Microsoft.CSharp.RuntimeBinder.CSharpArgumentInfo).GetTypeInfo().Assembly);
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

        internal static Stream GetResourceStream(Assembly assembly, string name)
        {
            return assembly.GetManifestResourceStream(name);
        }

        internal static AssemblyName GetAssemblyName(string name)
        {
            return
                GetAssemblies()
                    .Select(assembly => assembly.GetName())
                    .FirstOrDefault(assemblyName => assemblyName.Name == name);
        }

        private static MetadataReference CreateMetadataFileReference(Assembly asm)
        {
            var moduleMetadata = ModuleMetadata.CreateFromFile(asm.Location);
            var metadata = AssemblyMetadata.Create(moduleMetadata);
            return metadata.GetReference(filePath: asm.FullName);
        }

        //private static MetadataReference CreateMetadataFileReference(AssemblyName assemblyName, out Assembly asm)
        //{
        //    asm = Assembly.Load(assemblyName);
        //    var moduleMetadata = ModuleMetadata.CreateFromFile(asm.Location);
        //    var metadata = AssemblyMetadata.Create(moduleMetadata);
        //    return metadata.GetReference(filePath: assemblyName.FullName);
        //}

        //private static string ResolveContentRootPath(string contentRootPath, string basePath)
        //{
        //    if (string.IsNullOrEmpty(contentRootPath))
        //    {
        //        return basePath;
        //    }
        //    if (Path.IsPathRooted(contentRootPath))
        //    {
        //        return contentRootPath;
        //    }
        //    return Path.Combine(Path.GetFullPath(basePath), contentRootPath);
        //}

        public static List<MetadataReference> GetApplicationReferences()
        {
            if (_dependencyContext != null && !_configured)
            {
                foreach (var name in _dependencyContext.GetDefaultAssemblyNames())
                {
                    if (!AssemblyCache.ContainsKey(name.FullName))
                    {
                        var asm = Assembly.Load(name);
                        WalkReferenceAssemblies(asm);
                    }
                }
            }
            return MetadataReferences;
        }

    }
}