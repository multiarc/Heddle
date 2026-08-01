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
        /// <summary>
        /// Adds the assembly-level export declaration a probe compilation needs to be a <b>realistic</b> host assembly.
        /// Since the generator honours <c>[ExportExtensions]</c> — because the runtime does — a probe that declares
        /// extension types and no attribute declares extensions the runtime would never register, and the binder
        /// correctly ignores them. Probes that exist to exercise discovery therefore export everything, which is the
        /// parameterless <c>All</c> form.
        /// <para>The attribute is inserted after the source's <c>using</c> directives (C# requires that) and is declared
        /// here once rather than repeated per probe: a duplicated test input is a duplicate rule one level up.</para>
        /// </summary>
        public static string WithAllExtensionsExported(string source)
        {
            const string attribute = "[assembly: Heddle.Attributes.ExportExtensions]";
            var lines = source.Replace("\r\n", "\n").Split('\n').ToList();

            var insertAt = 0;
            for (var i = 0; i < lines.Count; i++)
                if (lines[i].TrimStart().StartsWith("using ", StringComparison.Ordinal))
                    insertAt = i + 1;

            lines.Insert(insertAt, attribute);
            return string.Join("\n", lines);
        }

        private static readonly IReadOnlyList<MetadataReference> References = BuildReferences();

        private static IReadOnlyList<MetadataReference> BuildReferences()
        {
            var tpa = (string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES");
            var refs = tpa.Split(Path.PathSeparator)
                .Where(p => !string.IsNullOrEmpty(p) && File.Exists(p))
                // Exclude the generator itself; it embeds the runtime types, causing CS0433 if both are referenced.
                .Where(p => !string.Equals(Path.GetFileNameWithoutExtension(p), "Heddle.Generator",
                    StringComparison.OrdinalIgnoreCase))
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

        /// <summary>A run whose compilation carries C# sources — needed by anything that depends on an
        /// assembly-level attribute (<c>[assembly: ExportFunctions(...)]</c>) or on source-declared extension
        /// types, which an empty compilation cannot express.</summary>
        public static GeneratorRun RunWithSources(
            IReadOnlyList<(string path, string content)> templates,
            IReadOnlyList<string> sources,
            Dictionary<string, string> globalOptions = null,
            Dictionary<string, Dictionary<string, string>> perFileOptions = null,
            CSharpParseOptions parseOptions = null)
        {
            var options = parseOptions ?? CSharpParseOptions.Default;
            var trees = sources.Select(src => CSharpSyntaxTree.ParseText(src, options)).ToArray();
            return RunTexts(templates.Select(t => (AdditionalText)new TestAdditionalText(t.path, t.content)).ToList(),
                globalOptions, perFileOptions, syntaxTrees: trees, parseOptions: options);
        }

        /// <summary>A compilation in which the <c>Heddle</c> assembly is not visible among
        /// <c>ReferencedAssemblySymbols</c> — the aliased/embedded/ILMerged shape. The generated manifest is not
        /// compiled here (it cannot be, without the runtime types); only the generator's own output and diagnostics
        /// are under test.</summary>
        public static GeneratorRun RunWithoutHeddleReference(
            IReadOnlyList<(string path, string content)> templates,
            Dictionary<string, string> globalOptions = null,
            Dictionary<string, Dictionary<string, string>> perFileOptions = null)
        {
            var references = References
                .Where(r => !(r.Display ?? string.Empty).EndsWith("Heddle.dll", StringComparison.OrdinalIgnoreCase))
                .ToList();
            return RunTexts(templates.Select(t => (AdditionalText)new TestAdditionalText(t.path, t.content)).ToList(),
                globalOptions, perFileOptions, references);
        }

        public static GeneratorRun RunTexts(
            IReadOnlyList<AdditionalText> texts,
            Dictionary<string, string> globalOptions = null,
            Dictionary<string, Dictionary<string, string>> perFileOptions = null,
            IReadOnlyList<MetadataReference> references = null,
            IReadOnlyList<SyntaxTree> syntaxTrees = null,
            CSharpParseOptions parseOptions = null)
        {
            var compilation = CSharpCompilation.Create("HeddleGenTest",
                syntaxTrees ?? (IEnumerable<SyntaxTree>) Array.Empty<SyntaxTree>(),
                references ?? References,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            var additionalTexts = texts.ToImmutableArray();

            var optionsProvider = new TestConfigOptionsProvider(
                globalOptions ?? new Dictionary<string, string>(),
                perFileOptions ?? new Dictionary<string, Dictionary<string, string>>());

            var driver = CSharpGeneratorDriver.Create(
                new[] { new HeddleTemplateGenerator().AsSourceGenerator() },
                additionalTexts,
                // The driver parses generated sources into this same compilation, so it has to agree with the
                // trees already in it exactly as the real build's driver does.
                parseOptions: parseOptions ?? CSharpParseOptions.Default,
                optionsProvider: optionsProvider);

            var updated = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);
            return new GeneratorRun(output, diagnostics, updated.GetRunResult());
        }

        /// <summary>Runs the generator and returns the driver for a <c>Verify.SourceGenerators</c> snapshot of
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
