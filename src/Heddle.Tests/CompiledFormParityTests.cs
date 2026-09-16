using System;
using System.Collections.Generic;
using Heddle.Precompiled;
using Heddle.Runtime;
using Heddle.TestCorpus;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>Full-corpus parity (P1-W9 exit criterion 1): every intent row builds, round-trips,
    /// registers, validates, materializes and renders byte-identically to the dynamic tier across all
    /// three sinks. Engine-error rows assert the build fails; unresolvable late-bound rows assert both
    /// tiers refuse the same way; resolve-only rows assert materialization without rendering.
    /// Serialized — the registry is process-global static state.</summary>
    [Collection("PrecompiledRegistrySerial")]
    public class CompiledFormParityTests : IDisposable
    {
        private readonly Action<PrecompiledFallbackEvent> _savedCallback;

        public CompiledFormParityTests()
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

        public static IEnumerable<object[]> BoundRows()
        {
            foreach (var row in CorpusIntent.Rows)
                if (row.Bound)
                    yield return new object[] { row.Name };
        }

        public static IEnumerable<object[]> EngineErrorRows()
        {
            foreach (var row in CorpusIntent.Rows)
                if (row.Tier == CorpusTier.EngineError)
                    yield return new object[] { row.Name };
        }

        private static CorpusIntentRow Find(string name)
        {
            foreach (var row in CorpusIntent.Rows)
                if (string.Equals(row.Name, name, StringComparison.Ordinal))
                    return row;
            throw new InvalidOperationException("No intent row names '" + name + "'.");
        }

        [Theory]
        [MemberData(nameof(BoundRows))]
        public void BoundRowRendersByteIdentically(string name)
        {
            // P3-R7: every row runs twice — Heddle.Precompiled.UseGeneratedSites on and off — both
            // byte-identical to the dynamic tier on all three sinks. The "without" arm proves the
            // data path is complete under the table.
            foreach (var useGeneratedSites in new[] { true, false })
            {
                var row = Find(name);
                PrecompiledTemplates.ResetForTests();
                if (CompiledFormHarness.IsUnresolvableRow(name))
                {
                    AssertSymmetricRefusal(row);
                    return;
                }
                // P1-R9: three passes — in-memory, file-backed (written under TestOutput/ and loaded
                // from bytes) and staged (the entry's real encoding on disk, staleness step on) — each
                // rendered under PrecompiledMismatchPolicy.Strict with the fallback sentinel armed.
                using (var guard = FallbackGuard.Install())
                {
                    foreach (var result in CompiledFormHarness.RegisterRowPasses(
                        row, TestCorpusIndex.CorpusDir, useGeneratedSites))
                    {
                        if (row.Render != CorpusRender.ResolveOnly)
                            CompiledFormHarness.AssertThreeSinkParity(row, result.Strategy,
                                TestCorpusIndex.CorpusDir, result.Pass);
                    }
                    guard.Verify();
                }
                CompiledFormHarness.AssertNoPendingRefusals();
                PrecompiledTemplates.ResetForTests();
            }
        }

        [Theory]
        [MemberData(nameof(EngineErrorRows))]
        public void EngineErrorRowFailsTheRecordedBuild(string name)
        {
            var row = Find(name);
            PrecompiledTemplates.ResetForTests();
            string text = CompiledFormHarness.CorpusText(name);
            var options = CompiledFormHarness.RowOptions(name, row, TestCorpusIndex.CorpusDir);
            Type modelType;
            object model;
            var modelEx = CompiledFormHarness.ModelExFor(row, out modelType, out model);
            CompileContext context;
            var template = CompiledFormHarness.BuildRecording(text, options, modelEx, out context);
            Assert.False(template.CompileResult.Success && context.CompileErrors.Count == 0,
                "Expected build errors for " + name + "; it compiled cleanly.");
        }

        private static void AssertSymmetricRefusal(CorpusIntentRow row)
        {
            // Both tiers refuse: the gate reports UnsupportedFunction, the dynamic text compile
            // (no deferral) fails the same call.
            PrecompiledTemplates.ResetForTests();
            string text = CompiledFormHarness.CorpusText(row.Name);
            var buildOptions = CompiledFormHarness.RowOptions(row.Name, row, TestCorpusIndex.CorpusDir);
            Type modelType;
            object model;
            var modelEx = CompiledFormHarness.ModelExFor(row, out modelType, out model);
            CompileContext context;
            var template = CompiledFormHarness.BuildRecording(text, buildOptions, modelEx, out context);
            Assert.True(template.CompileResult.Success && context.CompileErrors.Count == 0,
                "Unresolvable row should defer at build: " + CompiledFormHarness.Summarize(context) + ".");
            var artifact = CompiledFormHarness.ToArtifact(context, row.Name, text, modelEx, buildOptions, row);
            var image = Precompiled.CompiledForm.CompiledFormWriter.Write(artifact);
            CompiledFormHarness.RegisterImage(image, "HeddleTestAsm_ParityRefuse" + row.Name.Replace(".", "_"));
            var requestOptions = CompiledFormHarness.RequestOptions(row, TestCorpusIndex.CorpusDir);
            var report = PrecompiledTemplates.ValidateAll(requestOptions);
            bool unsupported = false;
            foreach (var failure in report.Failures)
                if (failure.Reason == PrecompiledFallbackReason.UnsupportedFunction)
                    unsupported = true;
            Assert.True(unsupported, "Gate should report UnsupportedFunction for " + row.Name + ".");
            PrecompiledTemplateInfo entry;
            Assert.False(
                PrecompiledTemplates.TryResolve(row.Name, requestOptions, out entry) && entry != null,
                "TryResolve should refuse " + row.Name + ".");
            var plain = new HeddleTemplate(text,
                new CompileContext(buildOptions, modelEx));
            Assert.False(plain.CompileResult.Success,
                "Dynamic tier should refuse " + row.Name + " without deferral.");
        }
    }
}
