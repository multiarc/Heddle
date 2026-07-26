extern alias generator;
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
using Heddle.Precompiled;
using Heddle.Runtime;
using Heddle.TestCorpus;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// The differential harness: runs the generator over a fixture template as an <c>AdditionalFiles</c> analyzer
    /// input, compiles the generated <c>.g.cs</c> into a real assembly, renders it through the typed entry point,
    /// and renders the same template + model through the dynamic engine — the runtime is the semantic reference; the
    /// emitter must match it byte-for-byte.
    /// </summary>
    internal static class DifferentialHarness
    {
        internal const string GeneratedNamespace = "Heddle.Precompiled.Generated";

        private static readonly IReadOnlyList<MetadataReference> References = BuildReferences();

        /// <summary>
        /// The engine test models (<c>Heddle.Tests.dll</c>), which the corpus runs hand to the compilations they
        /// create so a template declaring <c>:: PropArticle</c> can bind.
        /// <para>This replaces <c>HeddleTestsDll()</c>, which was triplicated across the three corpus suites and
        /// worked by taking <b>this</b> assembly's location and string-replacing the project name inside the path to
        /// guess where a sibling project's build output sat. That encoded the configuration name, the TFM directory
        /// and the project nesting as assumptions, and returned <c>null</c> on a miss — which is how five tests came
        /// to silently <c>return</c> and report a pass. The .csproj now copies the DLL into this project's own
        /// output (<c>OutputItemType="Content"</c>), so the lookup is <see cref="AppContext.BaseDirectory"/> and a
        /// miss throws.</para>
        /// </summary>
        internal static string EngineTestModelsDll()
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Heddle.Tests.dll");
            if (!File.Exists(path))
                throw new InvalidOperationException(
                    "Heddle.Tests.dll is not in this project's output directory (" + path +
                    "). It is copied there by the ProjectReference in Heddle.Generator.IntegrationTests.csproj. " +
                    "This is a build-wiring failure, not a skippable condition.");
            return path;
        }

        /// <summary>The engine test models as a single-element reference set — what every corpus suite passes as
        /// <c>extraReferences</c>.</summary>
        internal static IReadOnlyList<MetadataReference> EngineTestModelReferences() =>
            new[] { MetadataReference.CreateFromFile(EngineTestModelsDll()) };

        // The direct-invoke paths only ever *compile* against the extra references (Heddle.Tests for the corpus
        // models/extensions), so those assemblies never had to load. Registering generated output does load them —
        // PrecompiledTemplates.Register instantiates the manifest, whose entry rows touch every generated entry
        // class's static ctor. Remember each extra reference's path and satisfy the load from it.
        private static readonly Dictionary<string, string> ExtraReferencePaths =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        static DifferentialHarness()
        {
            AppDomain.CurrentDomain.AssemblyResolve += (_, args) =>
            {
                var simpleName = new AssemblyName(args.Name).Name;
                string path;
                lock (ExtraReferencePaths)
                {
                    if (!ExtraReferencePaths.TryGetValue(simpleName, out path))
                        return null;
                }

                return Assembly.LoadFrom(path);
            };
        }

        private static void RememberExtraReferences(IReadOnlyList<MetadataReference> extraReferences)
        {
            if (extraReferences == null)
                return;
            lock (ExtraReferencePaths)
            {
                foreach (var reference in extraReferences)
                {
                    var path = reference.Display;
                    if (string.IsNullOrEmpty(path) || !File.Exists(path))
                        continue;
                    ExtraReferencePaths[Path.GetFileNameWithoutExtension(path)] = path;
                }
            }
        }

        private static IReadOnlyList<MetadataReference> BuildReferences()
        {
            var tpa = (string) AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES");
            var refs = tpa.Split(Path.PathSeparator)
                .Where(p => !string.IsNullOrEmpty(p) && File.Exists(p))
                // Heddle.Generator is an *analyzer*, never a reference — a consuming project never compiles against
                // it. The runtime's option/fingerprint sources are linked into it, so leaving it on the reference
                // list would make every one of those type names ambiguous (CS0433) in generated code.
                .Where(p => !string.Equals(Path.GetFileNameWithoutExtension(p), "Heddle.Generator",
                    StringComparison.OrdinalIgnoreCase))
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
        /// assembly, and returns both. Global options map build_property.* keys.
        /// <para><paramref name="rewriteManifest"/> rewrites the emitted manifest source before it is compiled — the
        /// seam the seeded-mismatch meta-suite uses to corrupt one manifest row (a content hash, an extension AQN)
        /// while everything else stays real generator output. Production code never sees it.</para>
        /// </summary>
        public static GenResult Generate(IReadOnlyList<(string key, string content)> templates,
            Dictionary<string, string> globalOptions = null,
            IReadOnlyList<MetadataReference> extraReferences = null,
            Func<string, string> rewriteManifest = null)
        {
            RememberExtraReferences(extraReferences);
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
                new[] { new generator::Heddle.Generator.HeddleTemplateGenerator().AsSourceGenerator() },
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
                {
                    if (rewriteManifest != null)
                        src = rewriteManifest(src);
                    result.ManifestSource = src;
                }
                else
                {
                    result.TemplateSources[gen.HintName] = src;
                }

                trees.Add(CSharpSyntaxTree.ParseText(SourceText.From(src, Encoding.UTF8),
                    (CSharpParseOptions) CSharpParseOptions.Default));
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

            // The default path declares the precompiled expectation (entry class + non-null manifest strategy),
            // so a silent build-time degrade can never be mistaken for a passing differential test.
            ExpectPrecompiled(gen, key);
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

        /// <summary>Renders one template through both backends under an explicit <see cref="TemplateOptions"/> —
        /// threaded into the precompiled backend via the options-carrying <c>GenerateString</c> overload so a
        /// <c>TemplateOptions.Encoder</c> reaches the precompiled sink exactly as it reaches the dynamic engine.
        /// Used by the marker-encoder differential fixture.</summary>
        public static (string precompiled, string dynamic) RenderWithOptions(string key, string content,
            Type modelType, object model, TemplateOptions options, Dictionary<string, string> globalOptions = null)
        {
            var gen = Generate(new[] { (key, content) }, globalOptions);
            var errors = gen.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
            if (errors.Count != 0)
                throw new InvalidOperationException("Generator errors: " + string.Join("\n", errors.Select(e => e.ToString())));
            if (gen.Assembly == null)
                throw new InvalidOperationException("No assembly produced (template was not precompiled).");

            ExpectPrecompiled(gen, key);   // Declared precompiled expectation
            var entryType = FindEntryTypeByKey(gen.Assembly, key)
                            ?? throw new InvalidOperationException("Generated entry class not found for key: " + key);
            var root = (IProcessStrategy) entryType
                .GetField("Root", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static).GetValue(null);
            var precompiled = Heddle.Precompiled.PrecompiledRuntime.GenerateString(root, model, null, null, options);

            var modelExType = modelType == null || modelType == typeof(object)
                ? Heddle.Data.ExType.Dynamic
                : new Heddle.Data.ExType(modelType);
            var dynamicTemplate = new HeddleTemplate(content, new CompileContext(options, modelExType));
            if (!dynamicTemplate.CompileResult.Success)
                throw new InvalidOperationException("Dynamic compile failed: " + dynamicTemplate.CompileResult);
            var dyn = dynamicTemplate.Generate(model);
            return (precompiled, dyn);
        }

        /// <summary>Like <see cref="RenderWithOptions"/> but returns each backend as a separately-invokable delegate,
        /// so a test can assert each throws independently — the byte-identical tuple form short-circuits on the first
        /// backend's exception, which hides whether the second backend behaves the same. Used by the C1 budget-breach
        /// differential fixture (G-R3: identical exception kind on both backends).</summary>
        public static (Func<string> precompiled, Func<string> dynamic) DeferredWithOptions(string key, string content,
            Type modelType, object model, TemplateOptions options, Dictionary<string, string> globalOptions = null)
        {
            var gen = Generate(new[] { (key, content) }, globalOptions);
            var errors = gen.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
            if (errors.Count != 0)
                throw new InvalidOperationException("Generator errors: " + string.Join("\n", errors.Select(e => e.ToString())));
            if (gen.Assembly == null)
                throw new InvalidOperationException("No assembly produced (template was not precompiled).");

            ExpectPrecompiled(gen, key);   // Declared precompiled expectation
            var entryType = FindEntryTypeByKey(gen.Assembly, key)
                            ?? throw new InvalidOperationException("Generated entry class not found for key: " + key);
            var root = (IProcessStrategy) entryType
                .GetField("Root", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static).GetValue(null);

            var modelExType = modelType == null || modelType == typeof(object)
                ? Heddle.Data.ExType.Dynamic
                : new Heddle.Data.ExType(modelType);
            var dynamicTemplate = new HeddleTemplate(content, new CompileContext(options, modelExType));
            if (!dynamicTemplate.CompileResult.Success)
                throw new InvalidOperationException("Dynamic compile failed: " + dynamicTemplate.CompileResult);

            Func<string> precompiled = () => Heddle.Precompiled.PrecompiledRuntime.GenerateString(root, model, null, null, options);
            Func<string> dynamic = () => dynamicTemplate.Generate(model);
            return (precompiled, dynamic);
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

            ExpectPrecompiled(gen, targetKey);   // Declared precompiled expectation
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

        /// <summary>
        /// The gauntlet-crossing twin of <see cref="RenderInCorpus"/>: runs the generator over the corpus, registers
        /// the compiled assembly into the process-global <see cref="PrecompiledTemplates"/> registry, and renders
        /// <paramref name="targetKey"/> through a real <see cref="TemplateResolver"/> (<c>TemplatePathType.None</c> →
        /// <c>ConsultPrecompiled</c> → <c>TryResolve</c> → the per-request gauntlet) under two guards:
        /// <see cref="PrecompiledMismatchPolicy.Strict"/> on the request options, and a <see cref="FallbackGuard"/>
        /// around the whole render. Returns <c>(precompiled, dynamic)</c> like the direct-invoke paths.
        /// <para>Two sub-modes. <b>Registry-only</b> (<paramref name="fileBacked"/> false, the default): the resolver
        /// root points at a directory that does not exist, so a fallback that somehow escaped both guards still could
        /// not fake the render — the dynamic re-compile has nothing to read. <b>File-backed</b>: the corpus is staged
        /// into a real temp directory and the resolver is built with <c>checkFileChange: true</c>, so the gauntlet's
        /// staleness step (<c>CheckStaleness</c>/<c>HashFile</c>) runs against real generator-emitted content hashes.</para>
        /// <para>The caller is responsible for registry isolation (<see cref="PrecompiledRegistryTestBase"/>): the
        /// registry is process-global and <c>Register</c> throws on a duplicate key.</para>
        /// </summary>
        public static (string precompiled, string dynamic) RenderViaResolver(
            IReadOnlyList<(string key, string content)> corpus, string targetKey, string targetContent,
            Type modelType, object model, string dynamicRootPath,
            bool fileBacked = false,
            Dictionary<string, string> globalOptions = null,
            IReadOnlyList<MetadataReference> extraReferences = null)
        {
            var swept = SweepViaResolver(corpus,
                new[] { new ResolverTarget(targetKey, targetContent, modelType, model) },
                dynamicRootPath, fileBacked, renderDynamicReference: true, render: true,
                globalOptions: globalOptions, extraReferences: extraReferences);
            return (swept[0].Precompiled, swept[0].Dynamic);
        }

        /// <summary>Single-template convenience over <see cref="RenderViaResolver"/> — the corpus is the one template
        /// and the dynamic reference reads no imports.</summary>
        public static (string precompiled, string dynamic) RenderViaResolver(string key, string content,
            Type modelType, object model, bool fileBacked = false,
            Dictionary<string, string> globalOptions = null)
        {
            var corpus = new[] { (key, content) };
            return RenderViaResolver(corpus, key, content, modelType, model,
                dynamicRootPath: AppContext.BaseDirectory, fileBacked: fileBacked, globalOptions: globalOptions);
        }

        /// <summary>One template to render on the resolver path.</summary>
        internal sealed class ResolverTarget
        {
            public ResolverTarget(string key, string content, Type modelType, object model, bool render = true)
            {
                Render = render;
                Key = key;
                Content = content;
                ModelType = modelType;
                Model = model;
            }

            public string Key { get; }
            public string Content { get; }
            public Type ModelType { get; }
            public object Model { get; }

            /// <summary>Whether THIS target is rendered after it resolves.
            /// <para>Per target, not per sweep. The sweep-wide <c>render: false</c> this replaces was a blanket:
            /// because a handful of corpus entries genuinely cannot render standalone (a bare <c>@else</c>
            /// continuation has no matching opener in its own scope), EVERY entry lost its byte assertion. Declaring
            /// it per entry means the sweep renders the <c>Standalone</c> set and skips only what genuinely cannot
            /// be rendered — a strict coverage gain that falls out of the taxonomy rather than needing its own
            /// work.</para>
            /// <para><c>render: false</c> still proves the entry crossed the gauntlet: the verdict lands at
            /// <c>TryResolve</c>, before any byte is produced.</para></summary>
            public bool Render { get; }
        }

        /// <summary>The result of one swept target: the precompiled-adapter output and (unless the caller opted out)
        /// the dynamic reference to byte-compare it against.</summary>
        internal sealed class ResolverSweepResult
        {
            public string Key { get; set; }
            public string Precompiled { get; set; }
            public string Dynamic { get; set; }
        }

        /// <summary>
        /// The gauntlet-crossing sweep: one generator run and one <see cref="PrecompiledTemplates.Register"/> for the
        /// whole corpus, then every target resolved and rendered through one <see cref="TemplateResolver"/> under a
        /// single <see cref="FallbackGuard"/> and <see cref="PrecompiledMismatchPolicy.Strict"/>. Sharing the
        /// generator run and the registration minimizes suite time: the gauntlet's verdict is per-template, so the
        /// marginal cost of a target is one <c>TryResolve</c> plus one render.
        /// </summary>
        internal static IReadOnlyList<ResolverSweepResult> SweepViaResolver(
            IReadOnlyList<(string key, string content)> corpus, IReadOnlyList<ResolverTarget> targets,
            string dynamicRootPath, bool fileBacked = false, bool renderDynamicReference = true,
            bool render = true,
            Dictionary<string, string> globalOptions = null,
            IReadOnlyList<MetadataReference> extraReferences = null)
        {
            var gen = Generate(corpus, globalOptions, extraReferences);
            var errors = gen.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
            if (errors.Count != 0)
                throw new InvalidOperationException("Generator errors: " + string.Join("\n", errors.Select(e => e.ToString())));
            if (gen.Assembly == null)
                throw new InvalidOperationException("No assembly produced.");

            // The build-tier expectation is declared, not assumed — an entry class AND a non-null manifest strategy.
            foreach (var target in targets)
                ExpectPrecompiled(gen, target.Key);

            var stageDir = fileBacked ? StageCorpus(corpus) : NonexistentRoot();
            var results = new List<ResolverSweepResult>(targets.Count);
            try
            {
                PrecompiledTemplates.Register(gen.Assembly);

                var options = FallbackGuard.GuardedOptions();
                options.RootPath = stageDir + Path.DirectorySeparatorChar;
                options.FileNamePostfix = ".heddle";
                options.EnableFileChangeCheck = fileBacked;

                using (var guard = FallbackGuard.Install())
                {
                    var resolver = new TemplateResolver(Path.Combine(stageDir, "root.marker"), fileBacked);
                    foreach (var target in targets)
                    {
                        var template = resolver.GetTemplate(target.Key, string.Empty, out _,
                            new CompileContext(options, ToExType(target.ModelType)), TemplatePathType.None);
                        if (template == null)
                            throw new InvalidOperationException("Resolver returned no template for key: " + target.Key);
                        if (!template.CompileResult.Success)
                            throw new InvalidOperationException(
                                "Resolver template did not compile: " + template.CompileResult);
                        AssertServedByPrecompiledAdapter(template, target.Key);
                        results.Add(new ResolverSweepResult
                        {
                            Key = target.Key,
                            // render: false resolves without rendering — the gauntlet runs at TryResolve, before any
                            // byte is produced, so a corpus fragment that is only meaningful when imported (a bare
                            // @else continuation, say) still proves it crossed the gauntlet on the precompiled tier.
                            Precompiled = render && target.Render ? template.Generate(target.Model) : null,
                        });
                    }

                    guard.Verify();
                }
            }
            finally
            {
                if (fileBacked)
                    TryDeleteDirectory(stageDir);
            }

            if (!renderDynamicReference)
                return results;

            // The dynamic reference renders exactly as RenderInCorpus does: imports resolve from the real corpus
            // directory, and a model-less corpus template types dynamic (the precompiled Root types it dynamic too).
            var rooted = dynamicRootPath.EndsWith("/") || dynamicRootPath.EndsWith("\\")
                ? dynamicRootPath
                : dynamicRootPath + Path.DirectorySeparatorChar;
            for (int i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                var dynamicOptions = new TemplateOptions { RootPath = rooted, FileNamePostfix = ".heddle" };
                var dynamicTemplate = new HeddleTemplate(target.Content,
                    new CompileContext(dynamicOptions, ToExType(target.ModelType)));
                if (!target.Render)
                    continue;   // nothing to compare against: the precompiled half was resolve-only by declaration.
                if (!dynamicTemplate.CompileResult.Success)
                    throw new InvalidOperationException("Dynamic compile failed: " + dynamicTemplate.CompileResult);
                results[i].Dynamic = dynamicTemplate.Generate(target.Model);
            }

            return results;
        }

        /// <summary>The default intent: the template <b>precompiled</b>. Asserts both halves the hand-rolled probes
        /// used to assert separately: the manifest carries an entry with a non-null <c>strategy</c>, and the compiled
        /// assembly carries the generated entry class for the key.</summary>
        public static void ExpectPrecompiled(GenResult gen, string key)
        {
            var state = ClassifyInManifest(gen.ManifestSource, key);
            if (state != ManifestState.Precompiled)
                throw new InvalidOperationException(
                    $"Expected '{key}' to precompile, but the manifest says {state} (build-time degrade). " +
                    "If the degrade is the subject of the test, declare it with DifferentialHarness.ExpectDegrade.");
            if (gen.Assembly != null && FindEntryTypeByKey(gen.Assembly, key) == null)
                throw new InvalidOperationException("Generated entry class not found (fell back): " + key);
        }

        /// <summary>The declared build-time degrade: the emitter deliberately refused this template, so the manifest
        /// carries no bound strategy for it (either a HED7014 marker entry or no entry at all) and no entry class was
        /// generated. This is the exhaustive, greppable list of tests that expect a build-tier fallback; the
        /// runtime-tier equivalent is <see cref="FallbackGuard.Expect"/>.</summary>
        public static void ExpectDegrade(GenResult gen, string key)
        {
            var state = ClassifyInManifest(gen.ManifestSource, key);
            if (state == ManifestState.Precompiled)
                throw new InvalidOperationException(
                    $"Expected a dynamic-tier degrade for '{key}', but the template precompiled.");
            if (gen.Assembly != null && FindEntryTypeByKey(gen.Assembly, key) != null)
                throw new InvalidOperationException(
                    $"Expected no generated entry class for '{key}' (degrade), but one exists.");
        }

        internal enum ManifestState
        {
            /// <summary>No manifest entry at all — the whole template degraded to the dynamic tier.</summary>
            Absent,
            /// <summary>A HED7014 fallback-marker entry (<c>strategy: null</c>, README D21).</summary>
            Marker,
            /// <summary>A bound entry with a non-null strategy.</summary>
            Precompiled,
        }

        /// <summary>The single copy of the manifest probe the deliberate-degrade suites used to hand-roll.</summary>
        internal static ManifestState ClassifyInManifest(string manifest, string key)
        {
            var marker = "key: \"" + key + "\"";
            var at = manifest?.IndexOf(marker, StringComparison.Ordinal) ?? -1;
            if (at < 0)
                return ManifestState.Absent;
            var next = manifest.IndexOf("key: \"", at + marker.Length, StringComparison.Ordinal);
            var block = next < 0 ? manifest.Substring(at) : manifest.Substring(at, next - at);
            return block.Contains("strategy: null") ? ManifestState.Marker : ManifestState.Precompiled;
        }

        private static ExType ToExType(Type modelType) =>
            modelType == null || modelType == typeof(object) ? ExType.Dynamic : new ExType(modelType);

        /// <summary>A registry miss is silent by design (no <c>OnFallback</c> event, no Strict throw), so the one
        /// failure mode the two guards cannot see is "the resolver never consulted a registered entry". The adapter
        /// constructor sets the private <c>_precompiled</c> flag; reading it is the only way to tell an adapter
        /// template from a dynamically-compiled one, and it is what makes file-backed mode trustworthy.</summary>
        private static void AssertServedByPrecompiledAdapter(HeddleTemplate template, string key)
        {
            var field = typeof(HeddleTemplate).GetField("_precompiled",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
                throw new InvalidOperationException("HeddleTemplate._precompiled not found — update the harness probe.");
            if (!(bool) field.GetValue(template))
                throw new InvalidOperationException(
                    "Resolver served a dynamically-compiled template for '" + key +
                    "' — the precompiled entry was never consulted (registry miss).");
        }

        internal static string NonexistentRoot() =>
            Path.Combine(Path.GetTempPath(), "heddle-registry-only-" + Guid.NewGuid().ToString("N"));

        /// <summary>
        /// Writes the corpus to a fresh temp directory, each entry <b>at its declared encoding</b>.
        /// <para>This used to write <c>new UTF8Encoding(false)</c> unconditionally, and that was a real hole. The
        /// file-backed sub-mode exists precisely to exercise <c>PrecompiledGauntlet.HashFile</c>, which opens a
        /// <c>FileStream</c> and decodes with <c>detectEncodingFromByteOrderMarks: true</c> — the BOM path. Eight
        /// corpus templates carry a UTF-8 BOM, and not one of them ever reached the staged tree with it, so the only
        /// sub-mode that can catch content-hash rule drift never staged the shape the BOM path was written for.</para>
        /// <para>The old doc comment argued the encoding was immaterial because both sides hash DECODED text. That is
        /// true, and it is exactly why hashing decoded text is the fix — but a test that only ever stages the shape
        /// which works is not evidence that the other shape works. The declared <see cref="CorpusIntent"/> BOM flag
        /// is what lets the staged tree reproduce each entry's real encoding; entries with no declared row (ad-hoc
        /// fixtures from other suites) keep the no-BOM default, which is the shape templates ship in.</para>
        /// </summary>
        internal static string StageCorpus(IReadOnlyList<(string key, string content)> corpus)
        {
            var dir = Path.Combine(Path.GetTempPath(), "heddle-file-backed-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            var utf8NoBom = new UTF8Encoding(false);
            var utf8Bom = new UTF8Encoding(true);
            foreach (var (key, content) in corpus)
            {
                var path = Path.Combine(dir, key.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                var declaresBom = CorpusIntent.TryGet(Path.GetFileName(key), out var row) && row.Bom;
                var encoding = declaresBom ? utf8Bom : utf8NoBom;
                File.WriteAllBytes(path, encoding.GetPreamble().Concat(encoding.GetBytes(content)).ToArray());
            }

            return dir;
        }

        internal static void TryDeleteDirectory(string dir)
        {
            try
            {
                if (Directory.Exists(dir))
                    Directory.Delete(dir, true);
            }
            catch (IOException)
            {
                // A leftover temp directory is not a test failure.
            }
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

        /// <summary>The generator's own <c>SanitizeName</c>, reached through <c>InternalsVisibleTo</c> rather than
        /// mirrored. The mirror this replaces was a line-for-line copy by its own admission, and a naming-rule change
        /// silently degraded the harness — <c>FindEntryTypeByKey</c> simply returned null, so the suite whose job is
        /// catching build/run divergence became the thing that drifted.</summary>
        private static string SanitizeKey(string key) =>
            generator::Heddle.Generator.HeddleTemplateGenerator.SanitizeName(key);

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
                // Three Generate overloads exist (string + two sinks). Select the string-returning entry.
                var m = type.GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .FirstOrDefault(mi => mi.Name == "Generate" && mi.ReturnType == typeof(string));
                if (m != null)
                    return m;
            }

            return null;
        }
    }
}
