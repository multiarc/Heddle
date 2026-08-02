using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Heddle.Generator.Tests
{
    /// <summary>
    /// <c>ModelType</c> item metadata types a template from the project file: with no in-file <c>@model</c>
    /// directive the metadata spelling feeds the same resolution pipeline the directive feeds; with both present
    /// they must name the same resolved type or the build errors (HED7032).
    /// </summary>
    public class ModelTypeMetadataTests
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

        private static GeneratorRun Run(string template, string modelType) =>
            GeneratorHarness.Run(new[] { (Path, template) }, perFileOptions: Meta(modelType));

        private static string EntrySource(GeneratorRun run) =>
            run.GeneratedSourceTexts.FirstOrDefault(s => s.Contains("class Home"));

        [Fact]
        public void MetadataAloneTypesADirectivelessTemplate()
        {
            var run = Run("Major: @(Major)\n", "System.Version");

            Assert.DoesNotContain(run.GeneratorDiagnostics, d => d.Severity == DiagnosticSeverity.Error);
            var source = EntrySource(run);
            Assert.NotNull(source);
            Assert.Contains("public static string Generate(global::System.Version model", source);
            Assert.Contains("__ModelType = typeof(global::System.Version)", source);
        }

        [Fact]
        public void EqualSpellingsInDirectiveAndMetadataAgreeSilently()
        {
            var run = Run("@model(){{System.Version}}@\\\nMajor: @(Major)\n", "System.Version");

            Assert.DoesNotContain(run.GeneratorDiagnostics, d => d.Severity == DiagnosticSeverity.Error);
            Assert.DoesNotContain(run.GeneratorDiagnostics, d => d.Id == "HED7032");
            Assert.Contains("public static string Generate(global::System.Version model", EntrySource(run));
        }

        /// <summary>Different spellings, one symbol: the directive resolves <c>Version</c> through its own
        /// <c>@using</c> import to the very type the metadata fully qualifies. Agreement is about the resolved
        /// type, not the characters.</summary>
        [Fact]
        public void DifferentSpellingsResolvingToTheSameTypeAgreeSilently()
        {
            var run = Run("@using(){{System}}@\\\n@model(){{Version}}@\\\nMajor: @(Major)\n", "System.Version");

            Assert.DoesNotContain(run.GeneratorDiagnostics, d => d.Severity == DiagnosticSeverity.Error);
            Assert.DoesNotContain(run.GeneratorDiagnostics, d => d.Id == "HED7032");
            Assert.Contains("public static string Generate(global::System.Version model", EntrySource(run));
        }

        /// <summary>Two declarations naming two types: the runtime reads only the directive, so the build refuses
        /// to pick one. Positioned at the file start — item metadata has no in-file span.</summary>
        [Fact]
        public void ConflictingDeclarationsAreTheHed7032Error()
        {
            var run = Run("@model(){{System.Version}}@\\\nMajor: @(Major)\n", "System.Text.StringBuilder");

            var conflict = Assert.Single(run.GeneratorDiagnostics, d => d.Id == "HED7032");
            Assert.Equal(DiagnosticSeverity.Error, conflict.Severity);
            Assert.Contains("System.Text.StringBuilder", conflict.GetMessage());
            Assert.Contains("System.Version", conflict.GetMessage());
            Assert.Equal(Path, conflict.Location.GetLineSpan().Path);
            Assert.Equal(0, conflict.Location.SourceSpan.Start);
            Assert.Null(EntrySource(run));
        }

        /// <summary>An explicitly dynamic directive against a typed metadata is the same disagreement: the two
        /// declarations contract different model handling for one template.</summary>
        [Fact]
        public void ADynamicDirectiveAgainstTypedMetadataConflicts()
        {
            var run = Run("@model(){{dynamic}}@\\\nMajor: @(Major)\n", "System.Version");

            var conflict = Assert.Single(run.GeneratorDiagnostics, d => d.Id == "HED7032");
            Assert.Equal(DiagnosticSeverity.Error, conflict.Severity);
        }

        /// <summary>A metadata spelling that names no type anywhere draws the same HED7007 the directive's
        /// non-resolving spelling draws, at the same file-level position the metadata lanes use.</summary>
        [Fact]
        public void ANonResolvingMetadataSpellingBesideADirectiveIsHed7007()
        {
            var run = Run("@model(){{System.Version}}@\\\nMajor: @(Major)\n", "NoSuchTypeAnywhere");

            var missing = Assert.Single(run.GeneratorDiagnostics, d => d.Id == "HED7007");
            Assert.Equal(DiagnosticSeverity.Error, missing.Severity);
            Assert.Contains("NoSuchTypeAnywhere", missing.GetMessage());
            Assert.DoesNotContain(run.GeneratorDiagnostics, d => d.Id == "HED7032");
            Assert.Null(EntrySource(run));
        }

        [Fact]
        public void ANonResolvingMetadataSpellingAloneIsHed7007()
        {
            var run = Run("Major: @(Major)\n", "NoSuchTypeAnywhere");

            var missing = Assert.Single(run.GeneratorDiagnostics, d => d.Id == "HED7007");
            Assert.Contains("NoSuchTypeAnywhere", missing.GetMessage());
            Assert.Equal(0, missing.Location.SourceSpan.Start);
        }

        /// <summary>A dynamic metadata spelling with no directive is the untyped template it asks for — the
        /// entry point takes <c>object</c>, exactly as a <c>@model(){{dynamic}}</c> directive produces.</summary>
        [Fact]
        public void DynamicMetadataAloneLeavesTheTemplateUntyped()
        {
            var run = Run("Hello\n", "dynamic");

            Assert.DoesNotContain(run.GeneratorDiagnostics, d => d.Severity == DiagnosticSeverity.Error);
            Assert.Contains("public static string Generate(object model", EntrySource(run));
        }
    }
}
