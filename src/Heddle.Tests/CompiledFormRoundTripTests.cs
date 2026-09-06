using System;
using System.Linq;
using Heddle.Precompiled;
using Heddle.Precompiled.CompiledForm;
using Heddle.Runtime;
using Heddle.TestCorpus;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>Artifact round-trip (P1-W9 exit criterion 2): write/read/write is byte-deterministic,
    /// refusal sets survive the trip exactly, and every document carries the raw text the loader parses
    /// (empty only for synthesized fragment documents, which the loader never parses).
    /// Serialized — the registry is process-global static state.</summary>
    [Collection("PrecompiledRegistrySerial")]
    public class CompiledFormRoundTripTests : IDisposable
    {
        private readonly Action<PrecompiledFallbackEvent> _savedCallback;

        public CompiledFormRoundTripTests()
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
        public void WriteReadWriteIsByteDeterministic()
        {
            // A blank template, an escape-heavy one, a definition-heavy one and a refusal row: the
            // shapes most likely to wobble across the codec.
            foreach (var name in new[] {
                "at-escape.heddle", "props-defaults.heddle", "ext-site-fallback.heddle", "ctx-encoding.heddle" })
            {
                var row = Find(name);
                string text = CompiledFormHarness.CorpusText(name);
                var options = CompiledFormHarness.RowOptions(name, row, TestCorpusIndex.CorpusDir);
                Type modelType;
                object model;
                var modelEx = CompiledFormHarness.ModelExFor(row, out modelType, out model);
                CompileContext context;
                var template = CompiledFormHarness.BuildRecording(text, options, modelEx, out context);
                Assert.True(template.CompileResult.Success && context.CompileErrors.Count == 0,
                    "Build failed for " + name + ": " + CompiledFormHarness.Summarize(context) + ".");
                var artifact = CompiledFormHarness.ToArtifact(context, name, text, modelEx, options, row);
                byte[] first = CompiledFormWriter.Write(artifact);
                byte[] second = CompiledFormWriter.Write(CompiledFormReader.Read(first));
                Assert.True(first.SequenceEqual(second),
                    "Write/read/write diverged for " + name + " (" + first.Length + " vs " + second.Length + " bytes).");
            }
        }

        [Fact]
        public void EveryParsedDocumentCarriesItsRawText()
        {
            foreach (var row in CorpusIntent.Rows)
            {
                if (!row.Bound || CompiledFormHarness.IsUnresolvableRow(row.Name))
                    continue;
                PrecompiledTemplates.ResetForTests();
                string text = CompiledFormHarness.CorpusText(row.Name);
                var options = CompiledFormHarness.RowOptions(row.Name, row, TestCorpusIndex.CorpusDir);
                Type modelType;
                object model;
                var modelEx = CompiledFormHarness.ModelExFor(row, out modelType, out model);
                CompileContext context;
                var template = CompiledFormHarness.BuildRecording(text, options, modelEx, out context);
                Assert.True(template.CompileResult.Success && context.CompileErrors.Count == 0,
                    "Build failed for " + row.Name + ".");
                var artifact = CompiledFormHarness.ToArtifact(context, row.Name, text, modelEx, options, row);
                foreach (var document in artifact.Documents)
                    Assert.False(string.IsNullOrEmpty(document.RawText) && document.Elements.Count != 0,
                        "Document without raw text carries elements in " + row.Name + ".");
                var back = CompiledFormReader.Read(CompiledFormWriter.Write(artifact));
                for (int i = 0; i < artifact.Documents.Count; i++)
                    Assert.Equal(artifact.Documents[i].RawText, back.Documents[i].RawText);
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
