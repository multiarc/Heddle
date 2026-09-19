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
            Assert.Equal("ADA\n", CompiledFormHarness.RenderStrategy(result.Strategy, model));
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
            Assert.Equal("ADA\n", CompiledFormHarness.RenderStrategy(result.Strategy, model));
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
            Assert.Equal("ADA\n", CompiledFormHarness.RenderStrategy(upper, model));
            Assert.Equal("ada\n", CompiledFormHarness.RenderStrategy(lower, model));
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
            // The request shape registers toUpper, so it resolves and materializes; the parameterless
            // default shape cannot bind the name and faults instead of rendering empty, memoized.
            PrecompiledTemplateInfo entry;
            Assert.True(PrecompiledTemplates.TryResolve(row.Name, requestOptions, out entry) && entry != null,
                "TryResolve refused the late-bound row.");
            Assert.Null(entry.Strategy);
            Assert.Equal(PrecompiledFallbackReason.ExtensionInitCompileError,
                entry.MaterializationFaultReason);
        }

        /// <summary>A materialization fault on the registry route: OnFallback fires with the recorded reason,
        /// Fallback policy resolves false, Strict throws PrecompiledMismatchException naming it — the same
        /// shape a gauntlet failure has, so no policy sees a null strategy.</summary>
        [Fact]
        public void MaterializationFaultRaisesOnFallbackAndHonoursThePolicy()
        {
            PrecompiledTemplates.ResetForTests();
            var row = Find("fn-standalone-late-bound.heddle");
            string text = CompiledFormHarness.CorpusText(row.Name);
            var buildOptions = CompiledFormHarness.RowOptions(row.Name, row, TestCorpusIndex.CorpusDir);
            var modelEx = CompiledFormHarness.ModelExFor(row, out _, out _);
            var template = CompiledFormHarness.BuildRecording(text, buildOptions, modelEx, out var context);
            Assert.True(template.CompileResult.Success && context.CompileErrors.Count == 0,
                "Build failed: " + CompiledFormHarness.Summarize(context) + ".");
            var artifact = CompiledFormHarness.ToArtifact(context, row.Name, text, modelEx, buildOptions, row);
            CompiledFormHarness.RegisterImage(
                Precompiled.CompiledForm.CompiledFormWriter.Write(artifact),
                "HeddleTestAsm_DeferredPolicy");
            var events = new System.Collections.Generic.List<PrecompiledFallbackEvent>();
            var saved = PrecompiledTemplates.OnFallback;
            PrecompiledTemplates.OnFallback = e => events.Add(e);
            try
            {
                // The registry knows the name (so the gauntlet's function step passes) but offers no
                // signature the call can bind, so the fault is materialization's, not the gauntlet's.
                var wrongSignature = new Heddle.Runtime.Expressions.FunctionRegistry();
                wrongSignature.Register("toUpper", (Func<int, int, string>)((a, b) => (a + b).ToString()));
                var fallback = CompiledFormHarness.RowOptions(row.Name, row, TestCorpusIndex.CorpusDir);
                fallback.Functions = wrongSignature;
                fallback.PrecompiledMismatchPolicy = PrecompiledMismatchPolicy.Fallback;
                Assert.False(PrecompiledTemplates.TryResolve(row.Name, fallback, out _));
                Assert.Equal(PrecompiledFallbackReason.ExtensionInitCompileError, Assert.Single(events).Reason);

                var strict = CompiledFormHarness.RowOptions(row.Name, row, TestCorpusIndex.CorpusDir);
                strict.Functions = wrongSignature;
                strict.PrecompiledMismatchPolicy = PrecompiledMismatchPolicy.Strict;
                var thrown = Assert.Throws<PrecompiledMismatchException>(
                    () => { PrecompiledTemplates.TryResolve(row.Name, strict, out _); });
                Assert.Equal(PrecompiledFallbackReason.ExtensionInitCompileError, thrown.Reason);
                Assert.Equal(row.Name, thrown.Key);
                Assert.Equal(2, events.Count);
            }
            finally
            {
                PrecompiledTemplates.OnFallback = saved;
            }
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
