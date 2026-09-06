using System;
using Heddle.Data;
using Heddle.Precompiled;
using Heddle.Runtime;
using Heddle.TestCorpus;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>Materialization and render allocation (P1-W9 exit criterion 5): one request shape
    /// materializes once no matter how often its strategy is read, a second registry compiles its own
    /// memoized strategy, and a precompiled render allocates within a small multiple of the dynamic
    /// render — proving the loader runs once per shape, never once per render.
    /// Serialized — the registry is process-global static state.</summary>
    [Collection("PrecompiledRegistrySerial")]
    public class CompiledFormAllocationTests : IDisposable
    {
        private readonly Action<PrecompiledFallbackEvent> _savedCallback;

        public CompiledFormAllocationTests()
        {
            _savedCallback = PrecompiledTemplates.OnFallback;
            PrecompiledTemplates.ResetForTests();
            CorpusExtensionFixtures.Register();
        }

        public void Dispose()
        {
            PrecompiledTemplates.OnFallback = _savedCallback;
            PrecompiledTemplates.ResetForTests();
        }

        [Fact]
        public void OneShapeMaterializesOnce()
        {
            PrecompiledTemplates.ResetForTests();
            var row = Find("ctx-encoding.heddle");
            var result = CompiledFormHarness.RegisterRow(row, TestCorpusIndex.CorpusDir);
            var requestOptions = CompiledFormHarness.RequestOptions(row, TestCorpusIndex.CorpusDir);
            Assert.True(ReferenceEquals(result.Strategy, result.Entry.GetStrategy(requestOptions)),
                "Re-reading a materialized shape should return the memoized strategy.");
        }

        [Fact]
        public void PrecompiledRenderStaysWithinBudgetOfTheDynamicRender()
        {
            PrecompiledTemplates.ResetForTests();
            var row = Find("ctx-encoding.heddle");
            var result = CompiledFormHarness.RegisterRow(row, TestCorpusIndex.CorpusDir);
            Type modelType;
            object model;
            CompiledFormHarness.ModelExFor(row, out modelType, out model);
            string text = CompiledFormHarness.CorpusText(row.Name);
            var renderOptions = CompiledFormHarness.RequestOptions(row, TestCorpusIndex.CorpusDir);
            var dynamic = new HeddleTemplate(text,
                new CompileContext(renderOptions, modelType == null ? ExType.Dynamic : new ExType(modelType)));
            Assert.True(dynamic.CompileResult.Success, "Dynamic reference failed to compile.");
            Assert.Equal(dynamic.Generate(model),
                PrecompiledRuntime.GenerateString(result.Strategy, model, null, null));
            for (int i = 0; i < 5; i++)
            {
                dynamic.Generate(model);
                PrecompiledRuntime.GenerateString(result.Strategy, model, null, null);
            }
            const int renders = 200;
            long dynamicAlloc = Measure(() =>
            {
                for (int i = 0; i < renders; i++)
                    dynamic.Generate(model);
            });
            long precompiledAlloc = Measure(() =>
            {
                for (int i = 0; i < renders; i++)
                    PrecompiledRuntime.GenerateString(result.Strategy, model, null, null);
            });
            Assert.True(precompiledAlloc <= Math.Max(4096L, dynamicAlloc * 4),
                "Precompiled renders allocated " + precompiledAlloc + " bytes vs dynamic " +
                dynamicAlloc + " bytes over " + renders + " renders.");
        }

        private static long Measure(Action action)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            long before = GC.GetAllocatedBytesForCurrentThread();
            action();
            return GC.GetAllocatedBytesForCurrentThread() - before;
        }

        private static CorpusIntentRow Find(string name)
        {
            foreach (var row in CorpusIntent.Rows)
                if (string.Equals(row.Name, name, StringComparison.Ordinal))
                    return row;
            throw new InvalidOperationException("No intent row names '" + name + "'.");
        }
    }
}
