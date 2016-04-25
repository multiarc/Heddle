using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Microsoft.Extensions.PlatformAbstractions;
using Microsoft.CodeAnalysis;
using Microsoft.Dnx.Compilation.CSharp;
using Microsoft.Extensions.CompilationAbstractions;

namespace Templates.Native {
    internal static class AssemblyHelper {
        private static readonly ILibraryExporter LibraryManager = CompilationServices.Default.LibraryExporter;
        private static readonly IApplicationEnvironment Environment = PlatformServices.Default.Application;

        private static List<MetadataReference> _metadataReferences;
#if NETSTANDARD1_5
        private static readonly IAssemblyLoadContext LoadContextAccessor = DnxPlatformServices.Default.AssemblyLoadContextAccessor.Default;

        private static readonly Func<object> GetCurrentDomain;
        private static readonly HashSet<Assembly> Assemblies;
        private static readonly Func<object, Assembly[]> AssembliesGetter;

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

        internal static IAssemblyLoadContext GetAssemblyLoadContext()
        {
            return LoadContextAccessor;
        }

#else

        internal static Assembly[] GetAssemblies() {
            return AppDomain.CurrentDomain.GetAssemblies();
        }
#endif

        internal static AssemblyName GetAssemblyName(string name)
        {
            //DnxPlatformServices.Default.AssemblyLoadContextAccessor
            return
                GetAssemblies()
                    .Select(assembly => assembly.GetName())
                    .FirstOrDefault(assemblyName => assemblyName.Name == name);
        }

        private static MetadataReference ConvertMetadataReference(IMetadataReference metadataReference) {
            // ReSharper disable once SuspiciousTypeConversion.Global
            var roslynReference = metadataReference as IRoslynMetadataReference;

            if (roslynReference != null) {
                return roslynReference.MetadataReference;
            }

            var embeddedReference = metadataReference as IMetadataEmbeddedReference;

            if (embeddedReference != null) {
                return MetadataReference.CreateFromImage(embeddedReference.Contents);
            }

            var fileMetadataReference = metadataReference as IMetadataFileReference;

            if (fileMetadataReference != null) {
                return CreateMetadataFileReference(fileMetadataReference.Path);
            }

            var projectReference = metadataReference as IMetadataProjectReference;
            if (projectReference != null) {
                using (var ms = new MemoryStream()) {
                    projectReference.EmitReferenceAssembly(ms);

                    return MetadataReference.CreateFromImage(ms.ToArray());
                }
            }

            throw new NotSupportedException();
        }

        private static MetadataReference CreateMetadataFileReference(string path) {
            return MetadataReference.CreateFromFile(path);
        }

        internal static List<MetadataReference> GetMetadataReferences() {
            if (_metadataReferences == null)
            {
                var references = new List<MetadataReference>();
                var libraryExport = LibraryManager.GetExport(Environment.ApplicationName);
                if (libraryExport?.MetadataReferences?.Count > 0)
                {
                    var roslynReference = libraryExport.MetadataReferences[0] as IRoslynMetadataReference;
                    var compilationReference = roslynReference?.MetadataReference as CompilationReference;
                    if (compilationReference != null)
                    {
                        references.AddRange(compilationReference.Compilation.References);
                        references.Add(roslynReference.MetadataReference);
                        return references;
                    }
                }
                var export = LibraryManager.GetAllExports(Environment.ApplicationName);
                references.AddRange(export.MetadataReferences.Select(ConvertMetadataReference));

                Interlocked.CompareExchange(ref _metadataReferences, references, null);
            }
            return _metadataReferences;
        }

    }
}