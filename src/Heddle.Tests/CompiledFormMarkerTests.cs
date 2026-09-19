using System;
using System.Linq;
using Heddle.Precompiled;
using Heddle.Precompiled.CompiledForm;
using Heddle.Runtime;
using Heddle.TestCorpus;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>Artifact structural markers (P1-W9): the schema version is the compiled-form schema the
    /// engine reads, the recorded engine version echoes the build input, every document section decodes,
    /// and the content hash matches the bytes the row built from.
    /// Serialized — the registry is process-global static state.</summary>
    [Collection("PrecompiledRegistrySerial")]
    public class CompiledFormMarkerTests : IDisposable
    {
        private readonly Action<PrecompiledFallbackEvent> _savedCallback;

        public CompiledFormMarkerTests()
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
        public void SchemaVersionIsTheCompiledFormSchema()
        {
            Assert.Equal(4, PrecompiledSchema.CompiledFormSchemaVersion);
            Assert.True(PrecompiledSchema.IsSupported(PrecompiledSchema.CompiledFormSchemaVersion),
                "The engine should read the schema it writes.");
        }

        [Fact]
        public void EngineVersionEchoesTheBuildInput()
        {
            const string rowName = "at-escape.heddle";
            var row = Find(rowName);
            string text = CompiledFormHarness.CorpusText(rowName);
            var options = CompiledFormHarness.RowOptions(rowName, row, TestCorpusIndex.CorpusDir);
            Type modelType;
            object model;
            var modelEx = CompiledFormHarness.ModelExFor(row, out modelType, out model);
            CompileContext context;
            var template = CompiledFormHarness.BuildRecording(text, options, modelEx, out context);
            Assert.True(template.CompileResult.Success && context.CompileErrors.Count == 0,
                "Build failed: " + CompiledFormHarness.Summarize(context) + ".");
            const string engineVersion = "9.9.9-test";
            var artifact = context.FormRecord.ToArtifact(engineVersion, "test", rowName,
                ContentHash.HashText(text), "reg-" + rowName, null, modelEx, false, false, "Text",
                row.Mode.ToString(), options.TrimDirectiveLines);
            var back = CompiledFormReader.Read(CompiledFormWriter.Write(artifact));
            Assert.Equal(engineVersion, back.Header.EngineVersion);
            Assert.Equal("reg-" + rowName, back.Templates[0].RegisteredName);
            Assert.Equal(rowName, back.Templates[0].Key);
        }

        [Fact]
        public void ContentHashMatchesTheBuiltBytes()
        {
            const string rowName = "ctx-encoding.heddle";
            var row = Find(rowName);
            string text = CompiledFormHarness.CorpusText(rowName);
            string hash = ContentHash.HashText(text);
            var options = CompiledFormHarness.RowOptions(rowName, row, TestCorpusIndex.CorpusDir);
            Type modelType;
            object model;
            var modelEx = CompiledFormHarness.ModelExFor(row, out modelType, out model);
            CompileContext context;
            var template = CompiledFormHarness.BuildRecording(text, options, modelEx, out context);
            Assert.True(template.CompileResult.Success && context.CompileErrors.Count == 0,
                "Build failed: " + CompiledFormHarness.Summarize(context) + ".");
            var artifact = CompiledFormHarness.ToArtifact(context, rowName, text, modelEx, options, row);
            var back = CompiledFormReader.Read(CompiledFormWriter.Write(artifact));
            Assert.Equal(hash, back.Templates[0].ContentHash);
            Assert.True(back.Documents.Count != 0 && back.Templates.Count != 0,
                "Decoded artifact carries no documents or templates.");
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
