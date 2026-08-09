using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Heddle.Generator.Tests
{
    /// <summary>
    /// <c>PrecompiledTemplateInfo.ModelType</c> records the type the generated code was compiled against, and
    /// <c>modelTypeIsAmbient</c> records whether the template itself chose it. The runtime gauntlet reads both, so
    /// each entry shape is pinned here rather than left to whichever one a differential suite happens to exercise:
    /// a check consuming a wrong value is worse than no check.
    /// </summary>
    public class AmbientModelTypeManifestTests
    {
        private const string Path = "Views/Home.heddle";

        private static Dictionary<string, Dictionary<string, string>> Meta(string modelType) =>
            new Dictionary<string, Dictionary<string, string>>
            {
                [Path] = new Dictionary<string, string>
                {
                    ["build_metadata.AdditionalFiles.ModelType"] = modelType
                }
            };

        private static GeneratorRun Run(string template, string metadataModelType = null) =>
            GeneratorHarness.Run(new[] { (Path, template) },
                perFileOptions: metadataModelType == null ? null : Meta(metadataModelType));

        private static string Manifest(GeneratorRun run)
        {
            Assert.DoesNotContain(run.GeneratorDiagnostics, d => d.Severity == DiagnosticSeverity.Error);
            var manifest = run.GeneratedSourceTexts.FirstOrDefault(s => s.Contains("__HeddleManifest"));
            Assert.NotNull(manifest);
            return manifest;
        }

        private static string Entry(GeneratorRun run) =>
            run.GeneratedSourceTexts.First(s => s.Contains("class Home"));

        /// <summary>Nothing types the template: the build compiles it against <c>object</c> and says so, and says
        /// that the choice was its own — the engine would have taken the requesting context's type instead.</summary>
        [Fact]
        public void ADirectivelessUntypedTemplateRecordsObjectAndIsAmbient()
        {
            var run = Run("hello @(Name)\n");

            Assert.Contains("__ModelType = typeof(object)", Entry(run));
            Assert.Contains("modelTypeIsAmbient: true", Manifest(run));
        }

        /// <summary>An <c>@model</c> directive pins the type on both tiers, so the entry records the type and
        /// claims nothing about ambience — the value the wider constructor exists to avoid writing.</summary>
        [Fact]
        public void ADeclaredModelTypeRecordsTheTypeAndIsNotAmbient()
        {
            var run = Run("@model(){{System.Version}}@\\\nMajor: @(Major)\n");

            Assert.Contains("__ModelType = typeof(global::System.Version)", Entry(run));
            Assert.DoesNotContain("modelTypeIsAmbient", Manifest(run));
        }

        /// <summary><c>@model(){{dynamic}}</c> is a declaration like any other: it pins the engine's scope dynamic
        /// whatever the host supplied, so the entry is <b>not</b> ambient even though its recorded type is
        /// <c>object</c> — the one pair a bare "is the model type object?" test would have confused.</summary>
        [Fact]
        public void AnExplicitlyDynamicModelIsDeclaredNotAmbient()
        {
            var run = Run("@model(){{dynamic}}@\\\n@(Name)\n");

            Assert.Contains("__ModelType = typeof(object)", Entry(run));
            Assert.DoesNotContain("modelTypeIsAmbient", Manifest(run));
        }

        /// <summary><c>ModelType</c> item metadata types the build and never reaches the engine, so the entry
        /// records the metadata's type and is ambient — the shape whose two tiers disagree unless the host asks for
        /// exactly that type.</summary>
        [Fact]
        public void MetadataTypingRecordsTheTypeAndIsStillAmbient()
        {
            var run = Run("Major: @(Major)\n", "System.Version");

            Assert.Contains("__ModelType = typeof(global::System.Version)", Entry(run));
            Assert.Contains("modelTypeIsAmbient: true", Manifest(run));
        }

        /// <summary>A HED7014 fallback-marker row carries a model type too, and the same ambience it would have
        /// carried had it precompiled. Nothing reads it — the gauntlet short-circuits a marker before the model-type
        /// step — but a row that lied here would be a trap for whatever reads it next.</summary>
        [Fact]
        public void AMarkerRowRecordsItsModelTypeAndAmbienceToo()
        {
            var run = Run("@(nosuchfn9000(alsonone(Name) + 1))\n");
            var manifest = Manifest(run);

            Assert.Contains("strategy: null", manifest);
            Assert.Contains("modelType: typeof(object)", manifest);
            Assert.Contains("modelTypeIsAmbient: true", manifest);
        }
    }
}
