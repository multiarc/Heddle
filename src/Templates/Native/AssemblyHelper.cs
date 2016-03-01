using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Microsoft.Dnx.Compilation;
using Microsoft.Extensions.PlatformAbstractions;
using Microsoft.Dnx.Compilation.CSharp;
using Microsoft.CodeAnalysis;

namespace Templates.Native {
    internal static class AssemblyHelper {
        private static readonly ILibraryExporter LibraryManager =
            (ILibraryExporter)CallContextServiceLocator.Locator.ServiceProvider.GetService(typeof(ILibraryExporter));
        private static readonly IApplicationEnvironment Environment =
            (IApplicationEnvironment)
                CallContextServiceLocator.Locator.ServiceProvider.GetService(typeof(IApplicationEnvironment));

        private static List<MetadataReference> _metadataReferences;
#if DOTNET5_4
        private static readonly IAssemblyLoadContextAccessor LoadContextAccessor =
            (IAssemblyLoadContextAccessor)
                CallContextServiceLocator.Locator.ServiceProvider.GetService(typeof (IAssemblyLoadContextAccessor));

        private static readonly Func<object> GetCurrentDomain;
        private static readonly Func<object, Assembly[]> AssembliesGetter;
#else

#endif

        static AssemblyHelper() {
#if DOTNET5_4
            var type = typeof(object).GetTypeInfo().Assembly.GetType("System.AppDomain");
            if (type == null) {
                throw new InvalidOperationException("Cannot find System.AppDomain class in system library, investigate to issue and rewrite assembly list acquire");
            }
            var method = type.GetProperty("CurrentDomain", BindingFlags.Public | BindingFlags.Static).GetGetMethod();
            if (method == null) {
                throw new InvalidOperationException("Cannot find System.AppDomain.CurrentDomain Property get method in Core Mode, investigate to issue and rewrite assembly list acquire");
            }
            GetCurrentDomain = method.CompileStaticAccessor<object>();
            method = type.GetMethod("GetAssemblies", BindingFlags.Public | BindingFlags.Instance);
            AssembliesGetter = method.CompileAccessor<object, Assembly[]>();
#endif
        }

#if DOTNET5_4
        internal static Assembly[] GetAssemblies() {
            var domain = GetCurrentDomain();
            return AssembliesGetter(domain);
        }

        internal static IAssemblyLoadContext GetAssemblyLoadContext()
        {
            return LoadContextAccessor.Default;
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

        private static MetadataReference ConvertMetadataReference(IMetadataReference metadataReference) {
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