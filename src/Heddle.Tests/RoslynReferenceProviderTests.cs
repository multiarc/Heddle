using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Heddle.Native;
using Xunit;

namespace Heddle.Tests
{
    [CollectionDefinition("ReferenceProviderSerial", DisableParallelization = true)]
    public sealed class ReferenceProviderSerialCollection { }

    /// <summary>
    /// The per-assembly reference cache: one build per assembly, one instance ever handed out. Reference identity
    /// alone cannot pin the first half — a double build whose second result is discarded still returns the cached
    /// instance — so the count comes from the provider's test-only observer. Serialized: any concurrent test that
    /// asks for the application references builds one for every loaded assembly, including this probe, and the
    /// provider legitimately builds twice when two threads miss together — the count is only meaningful alone.
    /// </summary>
    [Collection("ReferenceProviderSerial")]
    public class RoslynReferenceProviderTests
    {
        /// <summary>
        /// A reference is an expensive artifact (the whole metadata image is read); the cache existed before the
        /// fix, but a miss ran the build once outside the cache's factory and once inside it, paying twice for
        /// every first sight of an assembly.
        /// </summary>
        [Fact]
        public void AReferenceIsBuiltOncePerAssemblyAndReusedThereafter()
        {
            var assembly = CompileToFileAndLoad();
            var builds = 0;

            RoslynReferenceProvider.CreateObserver = candidate =>
            {
                if (candidate == assembly)
                    builds++;
            };
            try
            {
                var first = RoslynReferenceProvider.Build(new[] { assembly });
                var second = RoslynReferenceProvider.Build(new[] { assembly });

                var firstReference = Assert.Single(first);
                Assert.Equal(1, builds);
                Assert.Same(firstReference, Assert.Single(second));
                Assert.Equal(1, builds);
            }
            finally
            {
                RoslynReferenceProvider.CreateObserver = null;
            }
        }

        /// <summary>Emitted to a real file and loaded by path, so the reference takes the from-file arm — the one
        /// every target framework supports.</summary>
        private static Assembly CompileToFileAndLoad()
        {
            var assemblyName = "ReferenceProbe" + Guid.NewGuid().ToString("N");
            var compilation = CSharpCompilation.Create(
                assemblyName,
                new[] { CSharpSyntaxTree.ParseText("namespace ReferenceProbe { public class Marker { } }") },
                AssemblyHelper.GetApplicationReferences(),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            using var stream = new MemoryStream();
            var result = compilation.Emit(stream);
            Assert.True(result.Success,
                string.Join("\n", result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)
                    .Select(d => d.GetMessage())));

            return Assembly.LoadFrom(ProbeAssemblyFiles.WriteBesideTestAssembly(stream.ToArray(), assemblyName));
        }
    }
}
