using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.PortableExecutable;
using System.Threading;
using Microsoft.Extensions.PlatformAbstractions;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.DependencyModel;
#if NETSTANDARD1_5
using System.Runtime.Loader;
#endif

namespace Templates.Native {
    internal static class AssemblyHelper {
        private static readonly DependencyContext DependencyContext = DependencyContext.Default;
        private static readonly ConcurrentDictionary<string, AssemblyMetadata> MetadataFileCache =
            new ConcurrentDictionary<string, AssemblyMetadata>(StringComparer.OrdinalIgnoreCase);

#if NETSTANDARD1_5

        private static readonly ApplicationEnvironment Environment = PlatformServices.Default.Application;

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

        private static readonly HashSet<Assembly> Assemblies;

        private static void WalkReferenceAssemblies(HashSet<Assembly> set, Assembly current)
        {
            if (!set.Contains(current))
            {
                set.Add(current);
                foreach (var assemblyName in current.GetReferencedAssemblies())
                {
                    try
                    { 
                        var dependent = Assembly.Load(assemblyName);
                        WalkReferenceAssemblies(set, dependent);
                    }
                    catch(FileNotFoundException)
                    {
                    }
                }
            }
        }

        static AssemblyHelper()
        {
            Assemblies = new HashSet<Assembly>();
            var appAssembly = Assembly.Load(new AssemblyName(Environment.ApplicationName));
            WalkReferenceAssemblies(Assemblies, appAssembly);
            //var type = typeof(object).GetTypeInfo().Assembly.GetType("System.AppDomain");
            //if (type == null) {
            //    throw new InvalidOperationException("Cannot find System.AppDomain class in system library, investigate to issue and rewrite assembly list acquire");
            //}
            //var method = type.GetProperty("CurrentDomain", BindingFlags.Public | BindingFlags.Static).GetGetMethod();
            //if (method == null) {
            //    throw new InvalidOperationException("Cannot find System.AppDomain.CurrentDomain Property get method in Core Mode, investigate to issue and rewrite assembly list acquire");
            //}
            //GetCurrentDomain = method.CompileStaticAccessor<object>();
            //method = type.GetMethod("GetAssemblies", BindingFlags.NonPublic | BindingFlags.Instance);
            //if (method == null)
            //{
            //    throw new InvalidOperationException($"Cannot find {type.Name}.GetAssemblies() method in Core Mode, investigate to issue and rewrite assembly list acquire\r\n{string.Join("\r\n", type.GetTypeInfo().GetMethods(BindingFlags.Instance | BindingFlags.NonPublic).Select(m => $"{(m.IsPublic ? "public" : "hidebysig")} {(m.IsStatic ? "static":"instance")} {m.Name}:{m.ReturnType.Name}"))}\r\n{string.Join("\r\n", type.GetTypeInfo().GetProperties().Select(m => $"{m.Name}:{m.PropertyType.Name}"))}");
            //}
            //AssembliesGetter = method.CompileAccessor<object, Assembly[]>();
        }

        internal static ICollection<Assembly> GetAssemblies() {
            //var domain = GetCurrentDomain();
            //return AssembliesGetter(domain);
            return Assemblies;
        }

#else

        internal static Assembly[] GetAssemblies() {
            return AppDomain.CurrentDomain.GetAssemblies();
        }
#endif

        internal static AssemblyName GetAssemblyName(string name)
        {
            return
                GetAssemblies()
                    .Select(assembly => assembly.GetName())
                    .FirstOrDefault(assemblyName => assemblyName.Name == name);
        }

        private static MetadataReference CreateMetadataFileReference(string path)
        {
            var metadata = MetadataFileCache.GetOrAdd(path, _ =>
            {
                using (var stream = File.OpenRead(path))
                {
                    var moduleMetadata = ModuleMetadata.CreateFromStream(stream, PEStreamOptions.PrefetchMetadata);
                    return AssemblyMetadata.Create(moduleMetadata);
                }
            });

            return metadata.GetReference(filePath: path);
        }

        public static List<MetadataReference> GetApplicationReferences()
        {
            var metadataReferences = new List<MetadataReference>();
            if (DependencyContext == null)
            {
                // Avoid null ref if the entry point does not have DependencyContext specified.
                return metadataReferences;
            }

            foreach (var library in DependencyContext.CompileLibraries)
            {
                IEnumerable<string> referencePaths;
                try
                {
                    referencePaths = library.ResolveReferencePaths();
                }
                catch (InvalidOperationException)
                {
                    continue;
                }

                metadataReferences.AddRange(referencePaths.Select(CreateMetadataFileReference));
            }

            return metadataReferences;
        }

    }
}