using System;
using Heddle.Data;
using Heddle.Precompiled;
using Heddle.Runtime;
using Heddle.TestCorpus;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>Late-bound function sites (P1-W9 exit criterion 3): a deferred call renders through the
    /// materializing request's registry, a second registry triggers its own memoized compilation, and
    /// the default-registry strategy fails fast instead of rendering an empty string.
    /// Serialized — the registry is process-global static state.</summary>
    [Collection("PrecompiledRegistrySerial")]
    public class CompiledFormDeferredBindingTests : IDisposable
    {
        private readonly Action<PrecompiledFallbackEvent> _savedCallback;

        public CompiledFormDeferredBindingTests()
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
        public void StandaloneLateBoundCallRendersThroughTheRequestRegistry()
        {
            PrecompiledTemplates.ResetForTests();
            var result = CompiledFormHarness.RegisterRow(Find("fn-standalone-late-bound.heddle"),
                TestCorpusIndex.CorpusDir);
            Type modelType;
            object model;
            CompiledFormHarness.ModelExFor(Find("fn-standalone-late-bound.heddle"), out modelType, out model);
            Assert.Equal("ADA\n", PrecompiledRuntime.GenerateString(result.Strategy, model, null, null));
        }

        [Fact]
        public void TypedConsumerLateBoundCallRendersThroughTheRequestRegistry()
        {
            PrecompiledTemplates.ResetForTests();
            var result = CompiledFormHarness.RegisterRow(Find("fn-typed-consumer-late-bound.heddle"),
                TestCorpusIndex.CorpusDir);
            Type modelType;
            object model;
            CompiledFormHarness.ModelExFor(Find("fn-typed-consumer-late-bound.heddle"), out modelType, out model);
            Assert.Equal("ADA\n", PrecompiledRuntime.GenerateString(result.Strategy, model, null, null));
        }

        [Fact]
        public void EachRegistryMemoizesItsOwnCompilation()
        {
            PrecompiledTemplates.ResetForTests();
            var row = Find("fn-standalone-late-bound.heddle");
            string text = CompiledFormHarness.CorpusText(row.Name);
            var buildOptions = CompiledFormHarness.RowOptions(row.Name, row, TestCorpusIndex.CorpusDir);
            Type modelType;
            object model;
            var modelEx = CompiledFormHarness.ModelExFor(row, out modelType, out model);
            CompileContext context;
            var template = CompiledFormHarness.BuildRecording(text, buildOptions, modelEx, out context);
            Assert.True(template.CompileResult.Success && context.CompileErrors.Count == 0,
                "Build failed: " + CompiledFormHarness.Summarize(context) + ".");
            var artifact = CompiledFormHarness.ToArtifact(context, row.Name, text, modelEx, buildOptions, row);
            CompiledFormHarness.RegisterImage(
                Precompiled.CompiledForm.CompiledFormWriter.Write(artifact),
                "HeddleTestAsm_DeferredMemo");
            var first = CompiledFormHarness.RequestOptions(row, TestCorpusIndex.CorpusDir);
            var second = CompiledFormHarness.RequestOptions(row, TestCorpusIndex.CorpusDir);
            second.Functions = new Heddle.Runtime.Expressions.FunctionRegistry();
            second.Functions.Register("toUpper",
                (Func<string, string>)(s => s == null ? null : s.ToLowerInvariant()));
            PrecompiledTemplateInfo entry;
            Assert.True(PrecompiledTemplates.TryResolve(row.Name, first, out entry) && entry != null,
                "TryResolve refused the late-bound row.");
            var upper = entry.GetStrategy(first);
            var lower = entry.GetStrategy(second);
            Assert.NotNull(upper);
            Assert.NotNull(lower);
            Assert.True(upper == entry.GetStrategy(first), "Same registry should memoize one strategy.");
            Assert.True(!ReferenceEquals(upper, lower), "A different registry should re-compile.");
            Assert.Equal("ADA\n", PrecompiledRuntime.GenerateString(upper, model, null, null));
            Assert.Equal("ada\n", PrecompiledRuntime.GenerateString(lower, model, null, null));
        }

        [Fact]
        public void DefaultRegistryStrategyFailsFastOnTheUnboundName()
        {
            // Without deferral at load there is no silent empty render: the default registry cannot
            // bind toUpper, so the parameterless strategy faults instead of materializing.
            PrecompiledTemplates.ResetForTests();
            var row = Find("fn-standalone-late-bound.heddle");
            string text = CompiledFormHarness.CorpusText(row.Name);
            var buildOptions = CompiledFormHarness.RowOptions(row.Name, row, TestCorpusIndex.CorpusDir);
            Type modelType;
            object model;
            var modelEx = CompiledFormHarness.ModelExFor(row, out modelType, out model);
            CompileContext context;
            var template = CompiledFormHarness.BuildRecording(text, buildOptions, modelEx, out context);
            Assert.True(template.CompileResult.Success && context.CompileErrors.Count == 0,
                "Build failed: " + CompiledFormHarness.Summarize(context) + ".");
            var artifact = CompiledFormHarness.ToArtifact(context, row.Name, text, modelEx, buildOptions, row);
            CompiledFormHarness.RegisterImage(
                Precompiled.CompiledForm.CompiledFormWriter.Write(artifact),
                "HeddleTestAsm_DeferredDefault");
            var requestOptions = CompiledFormHarness.RequestOptions(row, TestCorpusIndex.CorpusDir);
            PrecompiledTemplateInfo entry;
            Assert.True(PrecompiledTemplates.TryResolve(row.Name, requestOptions, out entry) && entry != null,
                "TryResolve refused the late-bound row.");
            Assert.Null(entry.Strategy);
            Assert.Equal(PrecompiledFallbackReason.ExtensionInitCompileError,
                entry.MaterializationFaultReason);
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
