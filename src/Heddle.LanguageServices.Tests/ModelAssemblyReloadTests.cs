using System;
using System.Runtime.CompilerServices;
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
            var weak = LoadAnalyzeAndReload();
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
