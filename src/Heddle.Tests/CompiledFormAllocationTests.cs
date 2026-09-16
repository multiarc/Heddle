using System;
using System.Collections.Generic;
using System.IO;
using Heddle.Data;
using Heddle.Precompiled;
using Heddle.Runtime;
using Heddle.TestCorpus;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>Materialization and render allocation (P1-R10, GI-3): one request shape materializes once
    /// no matter how often its strategy is read, and a loaded strategy renders every corpus <c>Compiles</c>
    /// row on all three sinks allocating no more bytes per render than the dynamic tier renders the same
    /// text and model. The spec says "at or below" (GI-3 / P3-R8 amendment, 2026-09-17); the compiled form allocates less on every row measured so
    /// far, so the pin is <c>&lt;=</c> with the pair printed — a row that allocates MORE is a real finding,
    /// never a budget to widen. Serialized — the registry is process-global static state.</summary>
    [Collection("PrecompiledRegistrySerial")]
    public class CompiledFormAllocationTests : IDisposable
    {
        private const int Renders = 200;

        private readonly Action<PrecompiledFallbackEvent> _savedCallback;
        private readonly ITestOutputHelper _output;

        public CompiledFormAllocationTests(ITestOutputHelper output)
        {
            _output = output;
            _savedCallback = PrecompiledTemplates.OnFallback;
            PrecompiledTemplates.ResetForTests();
            CorpusExtensionFixtures.Register();
        }

        public void Dispose()
        {
            PrecompiledTemplates.OnFallback = _savedCallback;
            PrecompiledTemplates.ResetForTests();
        }

        public static IEnumerable<object[]> RenderableCompilesRows()
        {
            foreach (var row in CorpusIntent.Rows)
                if (row.Tier == CorpusTier.Compiles && row.Bound && row.Render != CorpusRender.ResolveOnly &&
                    !CompiledFormHarness.IsUnresolvableRow(row.Name))
                    yield return new object[] { row.Name };
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

        [Theory]
        [MemberData(nameof(RenderableCompilesRows))]
        public void LoadedStrategyAllocatesNoMoreThanTheDynamicTierOnEverySink(string name)
        {
            PrecompiledTemplates.ResetForTests();
            var row = Find(name);
            var result = CompiledFormHarness.RegisterRow(row, TestCorpusIndex.CorpusDir);
            Type modelType;
            object model;
            var modelEx = CompiledFormHarness.ModelExFor(row, out modelType, out model);
            string text = CompiledFormHarness.CorpusText(row.Name);
            var renderOptions = CompiledFormHarness.RequestOptions(row, TestCorpusIndex.CorpusDir);
            var dynamic = new HeddleTemplate(text, new CompileContext(renderOptions, modelEx));
            Assert.True(dynamic.CompileResult.Success, "Dynamic reference failed to compile for " + name + ".");
            // One adapter over the loaded strategy, the way a host holds it: the wrapper is built once,
            // and only Generate is inside the measured window on both tiers.
            var loaded = new HeddleTemplate(result.Strategy);

            var writer = new StringWriter();
            var buffer = new Streaming.TestBufferWriter(64 * 1024);
            var sinks = new[]
            {
                new Sink("string",
                    () => dynamic.Generate(model),
                    () => loaded.Generate(model)),
                new Sink("TextWriter",
                    () => { writer.GetStringBuilder().Clear(); dynamic.Generate(model, writer); },
                    () => { writer.GetStringBuilder().Clear(); loaded.Generate(model, writer); }),
                new Sink("IBufferWriter<byte>",
                    () => { buffer.Clear(); dynamic.Generate(model, buffer); },
                    () => { buffer.Clear(); loaded.Generate(model, buffer); })
            };

            var over = new List<string>();
            foreach (var sink in sinks)
            {
                // Warm both tiers past JIT tiering before the measured window.
                for (int i = 0; i < 20; i++)
                {
                    sink.Dynamic();
                    sink.Loaded();
                }
                long dynamicBytes = Measure(sink.Dynamic) / Renders;
                long loadedBytes = Measure(sink.Loaded) / Renders;
                _output.WriteLine(name + " / " + sink.Name + ": loaded " + loadedBytes + " B/render, dynamic " +
                    dynamicBytes + " B/render");
                if (loadedBytes > dynamicBytes)
                    over.Add(sink.Name + ": loaded " + loadedBytes + " B > dynamic " + dynamicBytes + " B per render");
            }

            Assert.True(over.Count == 0,
                "The loaded strategy for " + name + " allocates more than the dynamic tier: " +
                string.Join("; ", over) + ".");
        }

        private sealed class Sink
        {
            internal Sink(string name, Action dynamic, Action loaded)
            {
                Name = name;
                Dynamic = dynamic;
                Loaded = loaded;
            }

            internal string Name { get; }
            internal Action Dynamic { get; }
            internal Action Loaded { get; }
        }

        private static long Measure(Action render)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < Renders; i++)
                render();
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
