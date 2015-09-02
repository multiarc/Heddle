using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Dnx.Compilation;
using Microsoft.Dnx.Runtime;
using Microsoft.Dnx.Runtime.Infrastructure;
using Microsoft.Dnx.Compilation.CSharp;
#if DNX451 || DNXCORE50
using Microsoft.CodeAnalysis;
#endif

namespace Templates.Native {
    public static class NativeHelper {
#if DNX451 || DNXCORE50
        private static readonly ILibraryExporter LibraryManager =
            (ILibraryExporter)CallContextServiceLocator.Locator.ServiceProvider.GetService(typeof(ILibraryExporter));
        private static readonly IApplicationEnvironment Environment =
            (IApplicationEnvironment)
                CallContextServiceLocator.Locator.ServiceProvider.GetService(typeof(IApplicationEnvironment));
#endif
#if DNXCORE50
        private static readonly IAssemblyLoadContextAccessor LoadContextAccessor =
            (IAssemblyLoadContextAccessor)
                CallContextServiceLocator.Locator.ServiceProvider.GetService(typeof (IAssemblyLoadContextAccessor));

        private static readonly Func<object> GetCurrentDomain;
        private static readonly Func<object, Assembly[]> AssembliesGetter;
#else
        private static readonly Allocate Allocator;
#endif

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [System.Security.SecurityCritical]  // auto-generated 
        internal static unsafe void MemCpy(char* dmem, char* smem, int charCount) {
            if (charCount > 0)
            {
#if ALIGN_ACCESS
                if ((((int)dmem | (int)smem) & 1) == 0) { 
#endif
                // First Align dmem to a pointer boundary
                if (((int)dmem & 2) != 0) {
                    dmem[0] = smem[0];
                    dmem      += 1;
                    smem      += 1;
                    charCount -= 1;
                }
#if WIN64
                    if ((((int)dmem & 4) != 0) && (charCount >= 2))
                    {
#if IA64
                        if (((int)smem & 2) != 0)
                        { 
                            dmem[0] = smem[0]; 
                            dmem[1] = smem[1];
                        } 
                        else
#endif
                        {
                            ((uint *)dmem)[0] = ((uint *)smem)[0]; 
                        }
                        dmem += 2; 
                        smem += 2; 
                        charCount -= 2;
                    } 
#endif
                // Both x86 and AMD64 perform much faster if all writes are pointer aligned
                // Unaligned reads perform better than 2-byte aligned reads and
                //  better than pointer aligned reads with 16-bit shift and OR operation 
                // So on x86 or AMD64 after aligning dmem to a pointer boundry
                //  we just use standard mechanism 
#if !WIN64
                while (charCount >= 8) {
                    ((uint*)dmem)[0] = ((uint*)smem)[0];
                    ((uint*)dmem)[1] = ((uint*)smem)[1];
                    ((uint*)dmem)[2] = ((uint*)smem)[2];
                    ((uint*)dmem)[3] = ((uint*)smem)[3];
                    dmem      += 8;
                    smem      += 8;
                    charCount -= 8;
                }
                if ((charCount & 4) != 0) {
                    ((uint*)dmem)[0] = ((uint*)smem)[0];
                    ((uint*)dmem)[1] = ((uint*)smem)[1];
                    dmem += 4;
                    smem += 4;
                }
                if ((charCount & 2) != 0) {
                    ((uint*)dmem)[0] = ((uint*)smem)[0];
                    dmem += 2;
                    smem += 2;
                }
#else
#if AMD64
                    while (charCount >= 16) 
                    { 
                        ((ulong *)dmem)[0] = ((ulong *)smem)[0];
                        ((ulong *)dmem)[1] = ((ulong *)smem)[1]; 
                        ((ulong *)dmem)[2] = ((ulong *)smem)[2];
                        ((ulong *)dmem)[3] = ((ulong *)smem)[3];
                        dmem      += 16;
                        smem      += 16; 
                        charCount -= 16;
                    } 
                    if ((charCount & 8) != 0) 
                    {
                        ((ulong *)dmem)[0] = ((ulong *)smem)[0]; 
                        ((ulong *)dmem)[1] = ((ulong *)smem)[1];
                        dmem += 8;
                        smem += 8;
                    } 
                    if ((charCount & 4) != 0)
                    { 
                        ((ulong *)dmem)[0] = ((ulong *)smem)[0]; 
                        dmem += 4;
                        smem += 4; 
                    }
                    if ((charCount & 2) != 0)
                    {
                        ((uint *)dmem)[0] = ((uint *)smem)[0]; 
                        dmem += 2;
                        smem += 2; 
                    } 
#elif IA64
                    // On IA64 we MUST use aligned reads otherwise 
                    // we will fault
                    if (((int)smem & 2) == 0)
                    {
                        // align is 0 or 4 
                        if  (((int)smem & alignConst) == 0)
                        { 
                            while (charCount >= 16) 
                            {
                                ((ulong *)dmem)[0] = ((ulong *)smem)[0]; 
                                ((ulong *)dmem)[1] = ((ulong *)smem)[1];
                                ((ulong *)dmem)[2] = ((ulong *)smem)[2];
                                ((ulong *)dmem)[3] = ((ulong *)smem)[3];
                                dmem      += 16; 
                                smem      += 16;
                                charCount -= 16; 
                            } 

                            if ((charCount & 8) != 0) 
                            {
                                ((ulong *)dmem)[0] = ((ulong *)smem)[0];
                                ((ulong *)dmem)[1] = ((ulong *)smem)[1];
                                dmem += 8; 
                                smem += 8;
                            } 
 
                            if ((charCount & 4) != 0)
                            { 
                                ((ulong *)dmem)[0] = ((ulong *)smem)[0];
                                dmem += 4;
                                smem += 4;
                            } 
                        }
                        else // align is 4 
                        { 
                            while (charCount >= 8)
                            { 
                                ((uint *)dmem)[0] = ((uint *)smem)[0];
                                ((uint *)dmem)[1] = ((uint *)smem)[1];
                                ((uint *)dmem)[2] = ((uint *)smem)[2];
                                ((uint *)dmem)[3] = ((uint *)smem)[3]; 
                                dmem      += 8;
                                smem      += 8; 
                                charCount -= 8; 
                            }
 
                            if ((charCount & 4) != 0)
                            {
                                ((uint *)dmem)[0] = ((uint *)smem)[0];
                                ((uint *)dmem)[1] = ((uint *)smem)[1]; 
                                dmem += 4;
                                smem += 4; 
                            } 
                        }
                        if ((charCount & 2) != 0) 
                        {
                            ((uint *)dmem)[0] = ((uint *)smem)[0];
                            dmem += 2;
                            smem += 2; 
                        }
                    } 
                    else // align is 2 or 6 
                    {
                        while (charCount >= 8) 
                        {
                            dmem[0] = smem[0];
                            dmem[1] = smem[1];
                            dmem[2] = smem[2]; 
                            dmem[3] = smem[3];
                            dmem[4] = smem[4]; 
                            dmem[5] = smem[5]; 
                            dmem[6] = smem[6];
                            dmem[7] = smem[7]; 
                            dmem += 8;
                            smem += 8;
                            charCount -= 8;
                        } 

                        if ((charCount & 4) != 0) 
                        { 
                            dmem[0] = smem[0];
                            dmem[1] = smem[1]; 
                            dmem[2] = smem[2];
                            dmem[3] = smem[3];
                            dmem += 4;
                            smem += 4; 
                        }
                        if ((charCount & 2) != 0) 
                        { 
                            dmem[0] = smem[0];
                            dmem[1] = smem[1]; 
                            dmem += 2;
                            smem += 2;
                        }
                    } 
#endif
#endif
                if ((charCount & 1) != 0) {
                    dmem[0] = smem[0];
                }
#if ALIGN_ACCESS
                }
                else 
                {
                    // This is rare case where at least one of the pointers is only byte aligned. 
                    do { 
                        ((byte *)dmem)[0] = ((byte *)smem)[0];
                        ((byte *)dmem)[1] = ((byte *)smem)[1]; 
                        charCount -= 1;
                        dmem += 1;
                        smem += 1;
                    } 
                    while (charCount > 0);
                } 
#endif
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [System.Security.SecurityCritical]  // auto-generated 
        //Copied implementation of coreclr CompareOrdinal
        internal static unsafe int Equals(char* one, char* two, int lenOne, int lenTwo) {
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

        static NativeHelper() {
#if !DNXCORE50
            var method = typeof(string).GetMethod("FastAllocateString", BindingFlags.Static | BindingFlags.NonPublic);
            Allocator = (Allocate)method.CreateDelegate(typeof(Allocate));
#endif

#if DNXCORE50
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

#if DNXCORE50
        internal static Assembly[] GetAssemblies() {
            var domain = GetCurrentDomain();
            return AssembliesGetter(domain);
        }

        internal static IAssemblyLoadContext GetAssemblyLoadContext()
        {
            return LoadContextAccessor.Default;
        }
#else
        internal static string AllocateString(int len) {
            return Allocator(len);
        }

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

#if DNX451 || DNXCORE50
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
            var references = new List<MetadataReference>();
            var libraryExport = LibraryManager.GetExport(Environment.ApplicationName);
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

#endif

        [System.Security.SecurityCritical]  // auto-generated 
        internal static unsafe int StartsWith(char* data, char* find, int* needleTable, int dataLen, int findLen) {
            if (dataLen >= findLen) {
                int found = 0;
                int currentIndex = findLen - 1;
                int counter = currentIndex;

                while (counter >= 0 && currentIndex < dataLen) {
                    counter = findLen - 1;
                    found = currentIndex;
                    while (counter >= 0 && data[found] == find[counter]) {
                        found--;
                        counter--;
                    }
                    currentIndex += needleTable[(sbyte)data[currentIndex]];
                }
                found++;
                if (found <= dataLen - findLen)
                    return found;
            }
            return -1;
        }

        [System.Security.SecurityCritical]  // auto-generated 
        internal static bool IsWhiteSpace(char c) {
            return c == ' ' || c == '\x00a0' || c == '\x0085' || c >= '\x0009' && c <= '\x000d';
        }
    }
}