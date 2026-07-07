using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using Heddle;
using Heddle.Data;
using Heddle.Runtime;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// The phase 7 differential harness (WI9, D20): runs the generator over a fixture template as an
    /// <c>AdditionalFiles</c> analyzer input, compiles the generated <c>.g.cs</c> into a real assembly, renders it
    /// through the typed entry point, and renders the same template + model through the dynamic engine — the runtime
    /// is the semantic reference; the emitter must match it byte-for-byte.
    /// </summary>
    internal static class DifferentialHarness
    {
        internal const string GeneratedNamespace = "Heddle.Precompiled.Generated";

        private static readonly IReadOnlyList<MetadataReference> References = BuildReferences();

        private static IReadOnlyList<MetadataReference> BuildReferences()
        {
            var tpa = (string) AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES");
            var refs = tpa.Split(Path.PathSeparator)
                .Where(p => !string.IsNullOrEmpty(p) && File.Exists(p))
                .Select(p => (MetadataReference) MetadataReference.CreateFromFile(p))
                .ToList();
            refs.Add(MetadataReference.CreateFromFile(typeof(HeddleTemplate).Assembly.Location));
            refs.Add(MetadataReference.CreateFromFile(typeof(DifferentialHarness).Assembly.Location));
            return refs;
        }

        private sealed class TestAdditionalText : AdditionalText
        {
            private readonly SourceText _text;
            public TestAdditionalText(string path, string content) { Path = path; _text = SourceText.From(content, Encoding.UTF8); }
            public override string Path { get; }
            public override SourceText GetText(CancellationToken cancellationToken = default) => _text;
        }

        private sealed class Options : AnalyzerConfigOptions
        {
            private readonly Dictionary<string, string> _v;
            public Options(Dictionary<string, string> v) => _v = v;
            public override bool TryGetValue(string key, out string value) => _v.TryGetValue(key, out value);
        }

        private sealed class OptionsProvider : AnalyzerConfigOptionsProvider
        {
            private readonly Dictionary<string, string> _global;
            private readonly Dictionary<string, Dictionary<string, string>> _perFile;
            public OptionsProvider(Dictionary<string, string> g, Dictionary<string, Dictionary<string, string>> p) { _global = g; _perFile = p; }
            public override AnalyzerConfigOptions GlobalOptions => new Options(_global);
            public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => new Options(_global);
            public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) =>
                _perFile.TryGetValue(textFile.Path, out var v) ? new Options(v) : new Options(new Dictionary<string, string>());
        }

        internal sealed class GenResult
        {
            public string ManifestSource;
            public Dictionary<string, string> TemplateSources = new Dictionary<string, string>();
            public ImmutableArray<Diagnostic> Diagnostics;
            public Assembly Assembly;
        }

        /// <summary>Runs the generator over the given templates, compiles the generated sources into a loadable
        /// assembly, and returns both. Global options map build_property.* keys.</summary>
        public static GenResult Generate(IReadOnlyList<(string key, string content)> templates,
            Dictionary<string, string> globalOptions = null,
            IReadOnlyList<MetadataReference> extraReferences = null)
        {
            var references = References;
            if (extraReferences != null && extraReferences.Count != 0)
            {
                var merged = new List<MetadataReference>(References);
                merged.AddRange(extraReferences);
                references = merged;
            }

            var global = globalOptions ?? new Dictionary<string, string>();
            if (!global.ContainsKey("build_property.HeddleGeneratedNamespace"))
                global["build_property.HeddleGeneratedNamespace"] = GeneratedNamespace;

            // Each template keyed explicitly via per-file Key metadata so DeriveKey is deterministic and
            // OS-independent (paths carry the key too, for diagnostics).
            var perFile = new Dictionary<string, Dictionary<string, string>>();
            var additional = new List<AdditionalText>();
            foreach (var (key, content) in templates)
            {
                additional.Add(new TestAdditionalText(key, content));
                perFile[key] = new Dictionary<string, string> { ["build_metadata.AdditionalFiles.Key"] = key };
            }

            var inputCompilation = CSharpCompilation.Create("HeddleDiffInput",
                Array.Empty<SyntaxTree>(), references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            var driver = CSharpGeneratorDriver.Create(
                new[] { new HeddleTemplateGenerator().AsSourceGenerator() },
                additional.ToImmutableArray(),
                parseOptions: CSharpParseOptions.Default,
                optionsProvider: new OptionsProvider(global, perFile));

            var ranDriver = driver.RunGeneratorsAndUpdateCompilation(inputCompilation, out _, out var diagnostics);
            var runResult = ranDriver.GetRunResult();

            var result = new GenResult { Diagnostics = diagnostics };
            var trees = new List<SyntaxTree>();
            foreach (var gen in runResult.Results.SelectMany(r => r.GeneratedSources))
            {
                var src = gen.SourceText.ToString();
                if (gen.HintName.Contains("__HeddleManifest"))
                    result.ManifestSource = src;
                else
                    result.TemplateSources[gen.HintName] = src;
                trees.Add(CSharpSyntaxTree.ParseText(gen.SourceText, (CSharpParseOptions) CSharpParseOptions.Default));
            }

            if (diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error))
                return result; // caller asserts; do not attempt to compile broken generator output

            var outputCompilation = CSharpCompilation.Create("HeddleDiffOutput_" + Guid.NewGuid().ToString("N"),
                trees, references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                    optimizationLevel: OptimizationLevel.Release,
                    allowUnsafe: true));

            using var ms = new MemoryStream();
            var emit = outputCompilation.Emit(ms);
            if (!emit.Success)
            {
                var errors = string.Join("\n", emit.Diagnostics
                    .Where(d => d.Severity == DiagnosticSeverity.Error)
                    .Select(d => d.ToString()));
                var allSrc = string.Join("\n\n==========\n\n", result.TemplateSources.Values);
                throw new InvalidOperationException(
                    "Generated code failed to compile:\n" + errors + "\n\n--- generated ---\n" + allSrc);
            }

            ms.Position = 0;
            result.Assembly = Assembly.Load(ms.ToArray());
            return result;
        }

        /// <summary>Renders one template through the precompiled backend (compiled generated code) and the dynamic
        /// engine, returning both outputs for a byte-for-byte assertion.</summary>
        public static (string precompiled, string dynamic) Render(string key, string content, Type modelType,
            object model, Dictionary<string, string> globalOptions = null, TemplateOptions runtimeOptions = null)
        {
            var gen = Generate(new[] { (key, content) }, globalOptions);
            var errors = gen.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
            if (errors.Count != 0)
                throw new InvalidOperationException("Generator errors: " + string.Join("\n", errors.Select(e => e.ToString())));
            if (gen.Assembly == null)
                throw new InvalidOperationException("No assembly produced (template was not precompiled).");

            var method = FindEntryPoint(gen.Assembly)
                         ?? throw new InvalidOperationException("Generated entry class not found for key: " + key);
            var precompiled = (string) method.Invoke(null, new object[] { model, null, null });

            var options = runtimeOptions ?? new TemplateOptions();
            var dynamicTemplate = new HeddleTemplate(content, new CompileContext(options, modelType));
            if (!dynamicTemplate.CompileResult.Success)
                throw new InvalidOperationException("Dynamic compile failed: " + dynamicTemplate.CompileResult);
            var dyn = dynamicTemplate.Generate(model);

            return (precompiled, dyn);
        }

        /// <summary>Renders one target template (identified by <paramref name="targetKey"/>) through both backends
        /// when the corpus carries <c>@&lt;&lt;</c> imports: the generator sees every template as an
        /// <c>AdditionalFiles</c> input (imports resolve), and the dynamic engine reads imports from
        /// <paramref name="rootPath"/>. Returns both outputs for a byte-for-byte assertion.</summary>
        public static (string precompiled, string dynamic) RenderInCorpus(
            IReadOnlyList<(string key, string content)> corpus, string targetKey, string targetContent,
            Type modelType, object model, string rootPath,
            Dictionary<string, string> globalOptions = null, IReadOnlyList<MetadataReference> extraReferences = null)
        {
            var gen = Generate(corpus, globalOptions, extraReferences);
            var errors = gen.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
            if (errors.Count != 0)
                throw new InvalidOperationException("Generator errors: " + string.Join("\n", errors.Select(e => e.ToString())));
            if (gen.Assembly == null)
                throw new InvalidOperationException("No assembly produced.");

            var entryType = FindEntryTypeByKey(gen.Assembly, targetKey)
                         ?? throw new InvalidOperationException("Generated entry class not found (fell back): " + targetKey);

            // Render through the Root strategy with the request options so a generated @partial resolves its child
            // (registry first, then dynamic compile from RootPath) exactly as the runtime backend does.
            var rooted = rootPath.EndsWith("/") || rootPath.EndsWith("\\") ? rootPath : rootPath + Path.DirectorySeparatorChar;
            var options = new TemplateOptions { RootPath = rooted, FileNamePostfix = ".heddle" };
            var root = (IProcessStrategy) entryType
                .GetField("Root", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static).GetValue(null);
            var precompiled = Heddle.Precompiled.PrecompiledRuntime.GenerateString(root, model, null, null, options);

            // A model-less corpus template compiles on the dynamic tier (the precompiled Root types it dynamic too),
            // so the runtime reference uses ExType.Dynamic — a bare object type would reject member access.
            var modelExType = modelType == null || modelType == typeof(object)
                ? Heddle.Data.ExType.Dynamic
                : new Heddle.Data.ExType(modelType);
            var dynamicTemplate = new HeddleTemplate(targetContent, new CompileContext(options, modelExType));
            if (!dynamicTemplate.CompileResult.Success)
                throw new InvalidOperationException("Dynamic compile failed: " + dynamicTemplate.CompileResult);
            var dyn = dynamicTemplate.Generate(model);
            return (precompiled, dyn);
        }

        private static Type FindEntryTypeByKey(Assembly assembly, string key)
        {
            var sanitized = SanitizeKey(key);
            foreach (var type in assembly.GetTypes())
            {
                if (!type.IsClass || !type.IsAbstract || !type.IsSealed) continue;
                if (type.Namespace != GeneratedNamespace) continue;
                if (!string.Equals(type.Name, sanitized, StringComparison.Ordinal)) continue;
                return type;
            }

            return null;
        }

        /// <summary>Mirrors the generator's <c>HeddleTemplateGenerator.SanitizeName</c> (D11) to locate an entry
        /// class by key for the corpus render tests: first char of each path segment uppercased, non-identifier
        /// chars → '_', a leading digit prefixed with '_', extension dropped, segments joined by '_'.</summary>
        private static string SanitizeKey(string key)
        {
            var lastSlash = key.LastIndexOf('/');
            var dir = lastSlash >= 0 ? key.Substring(0, lastSlash) : string.Empty;
            var file = lastSlash >= 0 ? key.Substring(lastSlash + 1) : key;
            var dot = file.LastIndexOf('.');
            if (dot > 0)
                file = file.Substring(0, dot);

            var segments = new List<string>();
            if (dir.Length != 0)
                segments.AddRange(dir.Split('/'));
            segments.Add(file);

            var parts = new List<string>();
            foreach (var seg in segments)
            {
                if (seg.Length == 0)
                    continue;
                var sb = new StringBuilder(seg.Length);
                for (int i = 0; i < seg.Length; i++)
                {
                    var c = seg[i];
                    bool valid = c == '_' || char.IsLetter(c) || (i > 0 && char.IsDigit(c));
                    if (i == 0 && char.IsDigit(c))
                        sb.Append('_').Append(c);
                    else if (valid)
                        sb.Append(i == 0 ? char.ToUpperInvariant(c) : c);
                    else
                        sb.Append('_');
                }

                parts.Add(sb.ToString());
            }

            var result = string.Join("_", parts);
            return result.Length == 0 ? "_" : result;
        }

        /// <summary>Finds the single generated entry point in the compiled assembly (one template rendered per
        /// call): a public static class in the generated namespace exposing a public static <c>Generate</c>.</summary>
        private static MethodInfo FindEntryPoint(Assembly assembly)
        {
            foreach (var type in assembly.GetTypes())
            {
                if (!type.IsClass || !type.IsAbstract || !type.IsSealed) // static class
                    continue;
                if (type.Namespace != GeneratedNamespace)
                    continue;
                // Phase 8: three Generate overloads now exist (string + two sinks). Select the string-returning entry.
                var m = type.GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .FirstOrDefault(mi => mi.Name == "Generate" && mi.ReturnType == typeof(string));
                if (m != null)
                    return m;
            }

            return null;
        }
    }
}
