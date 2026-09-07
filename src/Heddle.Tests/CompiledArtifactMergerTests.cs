using System;
using Heddle.Precompiled;
using Heddle.Precompiled.CompiledForm;
using Heddle.Runtime;
using Heddle.TestCorpus;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>The build host compiles one template per record and merges them into one assembly
    /// artifact; this suite proves the merged artifact renders every row byte-identically. It mirrors
    /// <see cref="CompiledFormHarness"/>'s per-row pipeline but registers one merged image for two
    /// rows, so a dropped index rebase fails as a render divergence, not a silent pass.
    /// Serialized — the registry is process-global static state.</summary>
    [Collection("PrecompiledRegistrySerial")]
    public class CompiledArtifactMergerTests : IDisposable
    {
        private readonly Action<PrecompiledFallbackEvent> _savedCallback;

        public CompiledArtifactMergerTests()
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
        public void MergedTwoRowArtifactRendersBothRowsByteIdentically()
        {
            PrecompiledTemplates.ResetForTests();
            string rootPath = TestCorpusIndex.CorpusDir;
            var first = Find("at-escape.heddle");
            var second = Find("brace-misread.heddle");
            var merged = MergeSingles(new[] { first, second }, rootPath);
            var back = CompiledFormReader.Read(CompiledFormWriter.Write(merged));
            Assert.Equal(2, back.Templates.Count);
            CompiledFormHarness.RegisterImage(CompiledFormWriter.Write(back),
                "HeddleTestAsm_Merged" + Guid.NewGuid().ToString("N"));
            foreach (var row in new[] { first, second })
            {
                var requestOptions = CompiledFormHarness.RequestOptions(row, rootPath);
                PrecompiledTemplateInfo entry;
                Assert.True(PrecompiledTemplates.TryResolve(row.Name, requestOptions, out entry) &&
                    entry != null, "TryResolve refused merged row " + row.Name + ".");
                var strategy = entry.GetStrategy(requestOptions);
                Assert.NotNull(strategy);
                CompiledFormHarness.AssertThreeSinkParity(row, strategy, rootPath);
            }
        }

        [Fact]
        public void MergedArtifactRebasesEveryIndexTable()
        {
            PrecompiledTemplates.ResetForTests();
            string rootPath = TestCorpusIndex.CorpusDir;
            var merged = MergeSingles(new[] { Find("at-escape.heddle"), Find("brace-misread.heddle") },
                rootPath);
            Assert.Equal(2, merged.Templates.Count);
            foreach (var row in merged.Templates)
            {
                Assert.InRange(row.RootDocumentRef, 0, merged.Documents.Count - 1);
                foreach (int definition in row.DefinitionRefs)
                    Assert.InRange(definition, 0, merged.Definitions.Count - 1);
                foreach (int extension in row.ExtensionRefs)
                    Assert.InRange(extension, 0, merged.Extensions.Count - 1);
                foreach (int function in row.FunctionRefs)
                    Assert.InRange(function, 0, merged.Functions.Count - 1);
            }

            for (int i = 0; i < merged.Sites.Count; i++)
            {
                var site = merged.Sites[i];
                Assert.InRange(site.TemplateIndex, 0, merged.Templates.Count - 1);
                int bound;
                switch (site.Kind)
                {
                    case CompiledSiteKind.MemberAccessor: bound = merged.Members.Count; break;
                    case CompiledSiteKind.NativeExpression:
                    case CompiledSiteKind.LateBoundCall: bound = merged.Expressions.Count; break;
                    case CompiledSiteKind.EmbeddedCSharp: bound = merged.CSharpSites.Count; break;
                    default: bound = merged.Documents.Count; break;
                }

                Assert.InRange(site.PayloadRef, 0, bound - 1);
            }
        }

        private static CompiledArtifact MergeSingles(CorpusIntentRow[] rows, string rootPath)
        {
            var parts = new CompiledArtifact[rows.Length];
            for (int i = 0; i < rows.Length; i++)
            {
                var row = rows[i];
                string text = CompiledFormHarness.CorpusText(row.Name);
                var buildOptions = CompiledFormHarness.RowOptions(row.Name, row, rootPath);
                Type modelType;
                object model;
                var modelEx = CompiledFormHarness.ModelExFor(row, out modelType, out model);
                CompileContext context;
                var template = CompiledFormHarness.BuildRecording(text, buildOptions, modelEx,
                    out context);
                Assert.True(template.CompileResult.Success && context.CompileErrors.Count == 0,
                    "Build recorded errors for " + row.Name + ": " +
                    CompiledFormHarness.Summarize(context) + ".");
                parts[i] = CompiledFormHarness.ToArtifact(context, row.Name, text, modelEx,
                    buildOptions, row);
            }

            return CompiledArtifactMerger.Merge(parts);
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
