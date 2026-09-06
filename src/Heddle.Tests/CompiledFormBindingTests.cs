using System;
using Heddle.Data;
using Heddle.Precompiled;
using Heddle.Runtime;
using Heddle.TestCorpus;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>Gate binding matrix (P1-W9): the checks the gauntlet runs before any strategy is read —
    /// definition calls skip the extension registry, dynamic-attribute members compare by erasure,
    /// unregistered late-bound names report UnsupportedFunction, and stale fingerprints refuse.
    /// Serialized — the registry is process-global static state.</summary>
    [Collection("PrecompiledRegistrySerial")]
    public class CompiledFormBindingTests : IDisposable
    {
        private readonly Action<PrecompiledFallbackEvent> _savedCallback;

        public CompiledFormBindingTests()
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
        public void DefinitionCallsSkipTheExtensionRegistry()
        {
            // props-defaults calls five in-document definitions; none is a registered extension, and
            // the gate must not ask the registry about them.
            PrecompiledTemplates.ResetForTests();
            var result = RegisterBound("props-defaults.heddle");
            Assert.NotNull(result.Strategy);
        }

        [Fact]
        public void DynamicAttributeMembersCompareByErasure()
        {
            // DynamicCategory.SubCategories is ICollection<dynamic>: the build erases the attribute when
            // it records the member type, so the gate compares the erased PropertyType.
            PrecompiledTemplates.ResetForTests();
            var result = RegisterBound("dynamic-recursion.heddle");
            Assert.NotNull(result.Strategy);
        }

        [Fact]
        public void StaleFingerprintRefusesBeforeMaterialization()
        {
            PrecompiledTemplates.ResetForTests();
            var row = Find("ctx-encoding.heddle");
            var result = CompiledFormHarness.RegisterRow(row, TestCorpusIndex.CorpusDir);
            Assert.NotNull(result.Strategy);
            var stale = CompiledFormHarness.RequestOptions(row, TestCorpusIndex.CorpusDir);
            stale.OutputProfile = OutputProfile.Html;
            var report = PrecompiledTemplates.ValidateAll(stale);
            bool mismatch = false;
            foreach (var failure in report.Failures)
                if (failure.Reason == PrecompiledFallbackReason.OptionsMismatch)
                    mismatch = true;
            Assert.True(mismatch, "A stale output profile should fail the options step.");
        }

        [Fact]
        public void FallbackGuardTripsOnAForcedRefusal()
        {
            // The guard's own proof: an unresolvable row raises through the callback it captures.
            PrecompiledTemplates.ResetForTests();
            var row = Find("fn-unresolvable-marker.heddle");
            string text = CompiledFormHarness.CorpusText(row.Name);
            var buildOptions = CompiledFormHarness.RowOptions(row.Name, row, TestCorpusIndex.CorpusDir);
            Type modelType;
            object model;
            var modelEx = CompiledFormHarness.ModelExFor(row, out modelType, out model);
            CompileContext context;
            var template = CompiledFormHarness.BuildRecording(text, buildOptions, modelEx, out context);
            Assert.True(template.CompileResult.Success && context.CompileErrors.Count == 0,
                "Unresolvable row should defer at build.");
            var artifact = CompiledFormHarness.ToArtifact(context, row.Name, text, modelEx, buildOptions, row);
            CompiledFormHarness.RegisterImage(
                Precompiled.CompiledForm.CompiledFormWriter.Write(artifact),
                "HeddleTestAsm_GuardTrip");
            using (var guard = new FallbackGuard())
            {
                var requestOptions = CompiledFormHarness.RequestOptions(row, TestCorpusIndex.CorpusDir);
                PrecompiledTemplateInfo entry;
                Assert.False(
                    PrecompiledTemplates.TryResolve(row.Name, requestOptions, out entry) && entry != null,
                    "TryResolve should refuse the unresolvable row.");
                Assert.True(guard.Events.Count != 0,
                    "The guard captured no fallback event for a refused resolve.");
            }
        }

        [Fact]
        public void FallbackGuardStaysQuietOnACleanRow()
        {
            PrecompiledTemplates.ResetForTests();
            using (var guard = new FallbackGuard())
            {
                var result = CompiledFormHarness.RegisterRow(Find("at-escape.heddle"), TestCorpusIndex.CorpusDir);
                Assert.NotNull(result.Strategy);
                guard.AssertQuiet();
            }
        }

        private static CorpusIntentRow Find(string name)
        {
            foreach (var row in CorpusIntent.Rows)
                if (string.Equals(row.Name, name, StringComparison.Ordinal))
                    return row;
            throw new InvalidOperationException("No intent row names '" + name + "'.");
        }

        private static CompiledFormHarness.RowResult RegisterBound(string name)
        {
            var row = Find(name);
            using (var guard = new FallbackGuard())
            {
                var result = CompiledFormHarness.RegisterRow(row, TestCorpusIndex.CorpusDir);
                guard.AssertQuiet();
                return result;
            }
        }
    }
}
