using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.PortableExecutable;
using System.Threading;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Internal;
using Microsoft.Extensions.PlatformAbstractions;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyModel;
using CompilationOptions = Microsoft.Extensions.DependencyModel.CompilationOptions;

#if NETSTANDARD1_5
using System.Runtime.Loader;
#endif

namespace Templates.Native
{
    internal static class AssemblyHelper
    {
        private static volatile DependencyContext _dependencyContext;

        private static readonly ConcurrentDictionary<string, Tuple<Assembly, MetadataReference>> AssemblyCache =
            new ConcurrentDictionary<string, Tuple<Assembly, MetadataReference>>(StringComparer.OrdinalIgnoreCase);

        private static readonly List<MetadataReference> MetadataReferences;

#if NETSTANDARD1_5

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
                            WalkReferenceAssemblies(dependent);
                        }
                    }
                    catch (FileNotFoundException)
                    {
                    }
                }
            }
        }

        internal static ICollection<Assembly> GetAssemblies()
        {
            return AssemblyCache.Values.Select(a => a.Item1).ToArray();
        }

        private static Assembly _applicationAssembly;

        static AssemblyHelper()
        {
#if !NETSTANDARD1_5
            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                Console.WriteLine(args.ExceptionObject.ToString());
            };
#endif

            var appEnvironment = PlatformServices.Default.Application;

            _applicationAssembly = Assembly.Load(new AssemblyName(appEnvironment.ApplicationName));
            _dependencyContext = DependencyContext.Load(_applicationAssembly);
            MetadataReferences = new List<MetadataReference>();
            WalkReferenceAssemblies(_applicationAssembly);
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

        private static string ResolveContentRootPath(string contentRootPath, string basePath)
        {
            if (string.IsNullOrEmpty(contentRootPath))
            {
                return basePath;
            }
            if (Path.IsPathRooted(contentRootPath))
            {
                return contentRootPath;
            }
            return Path.Combine(Path.GetFullPath(basePath), contentRootPath);
        }

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