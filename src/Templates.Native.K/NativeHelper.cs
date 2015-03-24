using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.Framework.Runtime;
using Microsoft.Framework.Runtime.Infrastructure;

namespace Templates.Native {
    public static class NativeHelper
    {
        private static readonly ILibraryManager LibraryManager =
            (ILibraryManager) CallContextServiceLocator.Locator.ServiceProvider.GetService(typeof (ILibraryManager));
        private static readonly IApplicationEnvironment Environment =
            (IApplicationEnvironment)
                CallContextServiceLocator.Locator.ServiceProvider.GetService(typeof (IApplicationEnvironment));
#if ASPNETCORE50
        private static readonly IAssemblyLoadContextAccessor LoadContextAccessor =
            (IAssemblyLoadContextAccessor)
                CallContextServiceLocator.Locator.ServiceProvider.GetService(typeof (IAssemblyLoadContextAccessor));
#endif
        private static readonly Func<int, string> Allocator;
        private static readonly MemcpyDelegate Memcpy;
#if ASPNETCORE50
        private static readonly Func<object> GetCurrentDomain;
        private static readonly Func<object, Assembly[]> AssembliesGetter;
#endif

        public static unsafe void MemCpy(char* dest, char* src, int len)
        {
#if ASPNETCORE50
                Buffer.MemoryCopy(src, dest, len * 2, len * 2);
#else
                Memcpy(dest, src, len);
#endif
        }

        //Copied implementation of coreclr CompareOrdinal
        public static unsafe int Equals(char* one, char* two, int lenOne, int lenTwo)
        {
            if (one == two) {
                return 0;
            }
            char* a = one;
            char* b = two;
            int length = Math.Min(lenOne, lenTwo);
            int diffOffset = -1;

            // unroll the loop
            while (length >= 10) {
                if (*(int*)a != *(int*)b) {
                    diffOffset = 0;
                    break;
                }

                if (*(int*)(a+2) != *(int*)(b+2)) {
                    diffOffset = 2;
                    break;
                }

                if (*(int*)(a+4) != *(int*)(b+4)) {
                    diffOffset = 4;
                    break;
                }

                if (*(int*)(a+6) != *(int*)(b+6)) {
                    diffOffset = 6;
                    break;
                }

                if (*(int*)(a+8) != *(int*)(b+8)) {
                    diffOffset = 8;
                    break;
                }
                a += 10;
                b += 10;
                length -= 10;
            }

            if (diffOffset != -1) {
                // we already see a difference in the unrolled loop above
                a += diffOffset;
                b += diffOffset;
                int order;
                if ((order = (int)*a - (int)*b) != 0) {
                    return order;
                }
                return ((int)*(a+1) - (int)*(b+1));
            }

            // now go back to slower code path and do comparison on 4 bytes one time. 
            // Following code also take advantage of the fact strings will
            // use even numbers of characters (runtime will have a extra zero at the end.) 
            // so even if length is 1 here, we can still do the comparsion. 
            while (length > 0) {
                if (*(int*)a != *(int*)b) {
                    break;
                }
                a += 2;
                b += 2;
                length -= 2;
            }

            if (length > 0) {
                int c;
                // found a different int on above loop
                if ((c = (int)*a - (int)*b) != 0) {
                    return c;
                }
                return ((int)*(a+1) - (int)*(b+1));
            }
            return lenOne - lenTwo;
        }

        static NativeHelper()
        {
            MethodInfo method = typeof (string).GetMethod("FastAllocateString",
                BindingFlags.Static | BindingFlags.NonPublic);
            if (method == null) {
                throw new InvalidOperationException("Cannot find string.FastAllocateString() system method");
            }
            Allocator = method.CompileAccessor<int, string>();
            method = typeof (string).GetMethod("wstrcpy", BindingFlags.Static | BindingFlags.NonPublic);
            if (method == null)
            {
                throw new InvalidOperationException("Cannot find string.wstrcpy() system method");
            }
            Memcpy = (MemcpyDelegate)method.CompileStaticDelegateAccessor<MemcpyDelegate>();
#if ASPNETCORE50
            var type = typeof(object).GetTypeInfo().Assembly.GetType("System.AppDomain");
            if (type == null) {
                throw new InvalidOperationException("Cannot find System.AppDomain class in system library, investigate to issue and rewrite assembly list acquire");
            }
            method = type.GetProperty("CurrentDomain", BindingFlags.Public | BindingFlags.Static).GetGetMethod();
            if (method == null) {
                throw new InvalidOperationException("Cannot find System.AppDomain.CurrentDomain Property get method in Core Mode, investigate to issue and rewrite assembly list acquire");
            }
            GetCurrentDomain = method.CompileStaticAccessor<object>();
            method = type.GetMethod("GetAssemblies", BindingFlags.Public | BindingFlags.Instance);
            AssembliesGetter = method.CompileAccessor<object, Assembly[]>();
#endif
        }

#if ASPNETCORE50
        public static Assembly[] GetAssemblies() {
            var domain = GetCurrentDomain();
            return AssembliesGetter(domain);
        }

        public static IAssemblyLoadContext GetAssemblyLoadContext()
        {
            return LoadContextAccessor.Default;
        }
#endif
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

        private static MetadataReference CreateMetadataFileReference(string path)
        {
            return MetadataReference.CreateFromFile(path);
        }

        public static List<MetadataReference> GetMetadataReferences()
        {
            var references = new List<MetadataReference>();
            var libraryExport = LibraryManager.GetLibraryExport(Environment.ApplicationName);
            if (libraryExport?.MetadataReferences?.Count > 0) {
                var roslynReference = libraryExport.MetadataReferences[0] as IRoslynMetadataReference;
                var compilationReference = roslynReference?.MetadataReference as CompilationReference;
                if (compilationReference != null) {
                    references.AddRange(compilationReference.Compilation.References);
                    references.Add(roslynReference.MetadataReference);
                    return references;
                }
            }
            var export = LibraryManager.GetAllExports(Environment.ApplicationName);
            references.AddRange(export.MetadataReferences.Select(ConvertMetadataReference));

            return references;
        }

        public static string AllocateString(int length)
        {
            return Allocator(length);
        }

        public static unsafe int StartsWith(char* data, char* find, int* needleTable, int dataLen, int findLen)
        {
            if (dataLen >= findLen)
            {
                int found = 0;
                int currentIndex = findLen - 1;
                int counter = currentIndex;

                while (counter >= 0 && currentIndex < dataLen)
                {
                    counter = findLen - 1;
                    found = currentIndex;
                    while (counter >= 0 && data[found] == find[counter])
                    {
                        found--;
                        counter--;
                    }
                    currentIndex += needleTable[(sbyte) data[currentIndex]];
                }
                found++;
                if (found <= dataLen - findLen)
                    return found;
            }
            return -1;
        }

        public static bool IsWhiteSpace(char c)
        {
            return c == ' ' || c == '\x00a0' || c == '\x0085' || c >= '\x0009' && c <= '\x000d';
        }
    }
}