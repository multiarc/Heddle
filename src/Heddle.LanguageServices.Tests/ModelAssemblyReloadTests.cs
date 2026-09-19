using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Heddle.Data;
using Heddle.LanguageServices;
using Xunit;

namespace Heddle.LanguageServices.Tests
{
    /// <summary>
    /// Collectible AssemblyLoadContext reload: load → analyze → reload → the old model context's
    /// <see cref="WeakReference"/> collects within the GC polling loop. The engine's register/unregister seam
    /// drops the static references that would otherwise pin the context.
    /// </summary>
    public class ModelAssemblyReloadTests
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static WeakReference LoadAnalyzeAndReload()
        {
            var service = CorpusFixture.NewTypedService();
            try
            {
                // Analyze a document that resolves a model-ALC type, so the analysis holds ExType→Type→ALC refs.
                service.Analyze("doc.heddle", "@model(){{Corpus.Blog}}\n@(Title)", 1);
                service.ReloadModelAssemblies();
                return service.LastUnloadedModelContext;
            }
            finally
            {
                service.Dispose();
            }
        }

        [Fact]
        public void ReloadCollectsPreviousModelContext()
        {
            AssertCollects(LoadAnalyzeAndReload());
        }

        /// <summary>
        /// The C# tier caches what an expression preparsed to, and the entry carries the expression's result
        /// <see cref="Type"/> — which, for an expression naming a workspace model type, belongs to the collectible
        /// context. The cache is keyed on generated source and evicted only for failures, so one successful C#-tier
        /// analysis pinned the context for the life of the process: unloading it freed nothing, and every reload of
        /// a workspace leaked another copy of its model assemblies.
        /// </summary>
        [Fact]
        public void ReloadCollectsPreviousModelContextAfterACSharpTierAnalysis()
        {
            AssertCollects(LoadAnalyzeCSharpAndReload());
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static WeakReference LoadAnalyzeCSharpAndReload()
        {
            var options = new HeddleLanguageServiceOptions
            {
                AssemblyPaths = new List<string> { CorpusFixture.ModelAssemblyPath },
                ExpressionMode = ExpressionMode.FullCSharp
            };
            var service = new HeddleLanguageService(options);
            try
            {
                // The C# tier's preparse cache stores the expression's result type, here a model-ALC type.
                var analysis = service.Analyze("doc.heddle",
                    "@model(){{Corpus.Blog}}\n@(@ new Corpus.Article())", 1);
                // Naming the model type proves the preparse resolved to it — which is what the cache then holds.
                Assert.True(analysis.CSharpTierUsed, "the C# tier did not run, so nothing was preparsed");
                service.ReloadModelAssemblies();
                return service.LastUnloadedModelContext;
            }
            finally
            {
                service.Dispose();
            }
        }

        private static void AssertCollects(WeakReference weak)
        {
            Assert.NotNull(weak);

            for (int i = 0; i < 10 && weak.IsAlive; i++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }

            Assert.False(weak.IsAlive, "the previous model AssemblyLoadContext should collect after reload");
        }
    }
}
