using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Heddle.Generator.Tests
{
    internal sealed class TestAdditionalText : AdditionalText
    {
        private readonly SourceText _text;

        public TestAdditionalText(string path, string content)
        {
            Path = path;
            _text = SourceText.From(content, Encoding.UTF8);
        }

        public override string Path { get; }

        public override SourceText GetText(CancellationToken cancellationToken = default) => _text;
    }

    /// <summary>An <see cref="AdditionalText"/> whose content cannot be read — <see cref="GetText"/> returns
    /// <c>null</c>, the signal the generator maps to HED7001 (unreadable template).</summary>
    internal sealed class UnreadableAdditionalText : AdditionalText
    {
        public UnreadableAdditionalText(string path) => Path = path;

        public override string Path { get; }

        public override SourceText GetText(CancellationToken cancellationToken = default) => null;
    }

    internal sealed class TestConfigOptions : AnalyzerConfigOptions
    {
        private readonly Dictionary<string, string> _values;

        public TestConfigOptions(Dictionary<string, string> values) => _values = values;

        public override bool TryGetValue(string key, out string value) => _values.TryGetValue(key, out value);
    }

    internal sealed class TestConfigOptionsProvider : AnalyzerConfigOptionsProvider
    {
        private readonly Dictionary<string, string> _global;
        private readonly Dictionary<string, Dictionary<string, string>> _perFile;

        public TestConfigOptionsProvider(Dictionary<string, string> global,
            Dictionary<string, Dictionary<string, string>> perFile)
        {
            _global = global;
            _perFile = perFile;
        }

        public override AnalyzerConfigOptions GlobalOptions => new TestConfigOptions(_global);

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => new TestConfigOptions(_global);

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) =>
            _perFile.TryGetValue(textFile.Path, out var v)
                ? new TestConfigOptions(v)
                : new TestConfigOptions(new Dictionary<string, string>());
    }

    internal sealed class GeneratorRun
    {
        public GeneratorRun(Compilation output, ImmutableArray<Diagnostic> generatorDiagnostics,
            GeneratorDriverRunResult runResult)
        {
            Output = output;
            GeneratorDiagnostics = generatorDiagnostics;
            RunResult = runResult;
        }

        public Compilation Output { get; }
        public ImmutableArray<Diagnostic> GeneratorDiagnostics { get; }
        public GeneratorDriverRunResult RunResult { get; }

        public IEnumerable<string> GeneratedSourceTexts =>
            RunResult.Results.SelectMany(r => r.GeneratedSources).Select(s => s.SourceText.ToString());

        public ImmutableArray<Diagnostic> OutputDiagnostics => Output.GetDiagnostics();
    }

    internal static class GeneratorHarness
    {
        private static readonly IReadOnlyList<MetadataReference> References = BuildReferences();

        private static IReadOnlyList<MetadataReference> BuildReferences()
        {
            var tpa = (string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES");
            var refs = tpa.Split(Path.PathSeparator)
                .Where(p => !string.IsNullOrEmpty(p) && File.Exists(p))
                .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))
                .ToList();
            refs.Add(MetadataReference.CreateFromFile(
                typeof(Heddle.Precompiled.PrecompiledTemplates).Assembly.Location));
            return refs;
        }

        public static GeneratorRun Run(
            IReadOnlyList<(string path, string content)> templates,
            Dictionary<string, string> globalOptions = null,
            Dictionary<string, Dictionary<string, string>> perFileOptions = null)
        {
            var texts = templates
                .Select(t => (AdditionalText)new TestAdditionalText(t.path, t.content))
                .ToList();
            return RunTexts(texts, globalOptions, perFileOptions);
        }

        public static GeneratorRun RunTexts(
            IReadOnlyList<AdditionalText> texts,
            Dictionary<string, string> globalOptions = null,
            Dictionary<string, Dictionary<string, string>> perFileOptions = null)
        {
            var compilation = CSharpCompilation.Create("HeddleGenTest",
                Array.Empty<SyntaxTree>(),
                References,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            var additionalTexts = texts.ToImmutableArray();

            var optionsProvider = new TestConfigOptionsProvider(
                globalOptions ?? new Dictionary<string, string>(),
                perFileOptions ?? new Dictionary<string, Dictionary<string, string>>());

            var driver = CSharpGeneratorDriver.Create(
                new[] { new HeddleTemplateGenerator().AsSourceGenerator() },
                additionalTexts,
                parseOptions: CSharpParseOptions.Default,
                optionsProvider: optionsProvider);

            var updated = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);
            return new GeneratorRun(output, diagnostics, updated.GetRunResult());
        }

        /// <summary>Runs the generator and returns the driver (D19) for a <c>Verify.SourceGenerators</c> snapshot of
        /// the generated sources and diagnostics.</summary>
        public static GeneratorDriver RunDriver(
            IReadOnlyList<(string path, string content)> templates,
            Dictionary<string, string> globalOptions = null,
            Dictionary<string, Dictionary<string, string>> perFileOptions = null)
        {
            var compilation = CSharpCompilation.Create("HeddleGenTest",
                Array.Empty<SyntaxTree>(),
                References,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            var additionalTexts = templates
                .Select(t => (AdditionalText)new TestAdditionalText(t.path, t.content))
                .ToImmutableArray();

            var optionsProvider = new TestConfigOptionsProvider(
                globalOptions ?? new Dictionary<string, string>(),
                perFileOptions ?? new Dictionary<string, Dictionary<string, string>>());

            GeneratorDriver driver = CSharpGeneratorDriver.Create(
                new[] { new HeddleTemplateGenerator().AsSourceGenerator() },
                additionalTexts,
                parseOptions: CSharpParseOptions.Default,
                optionsProvider: optionsProvider);

            return driver.RunGenerators(compilation);
        }
    }
}
