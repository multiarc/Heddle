using System;
using System.IO;
using Heddle.Data;
using Heddle.Precompiled;
using Heddle.Precompiled.CompiledForm;
using Heddle.Runtime;
using Heddle.TestCorpus;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>File-backed loading (P1-W9 exit criterion 4): artifact bytes and templates staged on
    /// disk load through file paths — not memory buffers — and render byte-identically, including a
    /// partial child and a composition import resolved from the staged directory.
    /// Serialized — the registry is process-global static state.</summary>
    [Collection("PrecompiledRegistrySerial")]
    public class CompiledFormLinkedSourceTests : IDisposable
    {
        private readonly Action<PrecompiledFallbackEvent> _savedCallback;

        public CompiledFormLinkedSourceTests()
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
        public void StagedBytesAndTemplatesRenderByteIdentically()
        {
            foreach (var name in new[] {
                "at-escape.heddle",
                "trycompile-parity-parent.heddle",
                "ergo-import-composition.heddle" })
                AssertStagedParity(name);
        }

        private static void AssertStagedParity(string name)
        {
            PrecompiledTemplates.ResetForTests();
            var row = Find(name);
            string stage = TestCorpusIndex.WrittenArtifactPath("linked_" + name.Replace(".", "_"));
            Directory.CreateDirectory(stage);
            // Stage every template the row can reach from disk: the row itself plus its partial
            // children and composition imports, all byte-identical to the corpus inputs.
            foreach (var staged in new[] {
                name, "trycompile-parity-child.heddle", "ergo-import-library.heddle" })
            {
                string path = Path.Combine(TestCorpusIndex.CorpusDir, staged);
                if (File.Exists(path))
                    File.Copy(path, Path.Combine(stage, staged), true);
            }
            string text = CompiledFormHarness.Decode(File.ReadAllBytes(Path.Combine(stage, name)));
            var buildOptions = CompiledFormHarness.RowOptions(name, row, stage);
            Type modelType;
            object model;
            var modelEx = CompiledFormHarness.ModelExFor(row, out modelType, out model);
            CompileContext context;
            var template = CompiledFormHarness.BuildRecording(text, buildOptions, modelEx, out context);
            Assert.True(template.CompileResult.Success && context.CompileErrors.Count == 0,
                "Build failed for " + name + ": " + CompiledFormHarness.Summarize(context) + ".");
            var artifact = CompiledFormHarness.ToArtifact(context, name, text, modelEx, buildOptions, row);
            CompiledFormHarness.AssertRefusalsMatch(row, artifact);
            byte[] image = CompiledFormWriter.Write(artifact);
            string imagePath = Path.Combine(stage, name + ".artifact");
            File.WriteAllBytes(imagePath, image);
            // Load from the staged bytes through a fresh read, exactly as a host loading from disk.
            var back = CompiledFormReader.Read(File.ReadAllBytes(imagePath));
            CompiledFormHarness.AssertRefusalsMatch(row, back);
            CompiledFormHarness.RegisterImage(image, "HeddleTestAsm_Linked" + name.Replace(".", "_"));
            var requestOptions = CompiledFormHarness.RequestOptions(row, stage);
            using (var guard = new FallbackGuard())
            {
                var report = PrecompiledTemplates.ValidateAll(requestOptions);
                Assert.True(report.Failures.Count == 0, "Gate refused staged " + name + ": " +
                    CompiledFormHarness.GateDetail(report) + ".");
                PrecompiledTemplateInfo entry;
                Assert.True(PrecompiledTemplates.TryResolve(name, requestOptions, out entry) && entry != null,
                    "TryResolve refused staged " + name + ".");
                var strategy = entry.GetStrategy(requestOptions);
                Assert.NotNull(strategy);
                CompiledFormHarness.AssertThreeSinkParity(Find(name), strategy, stage);
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
    }
}
