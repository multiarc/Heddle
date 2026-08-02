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

        /// <summary>The reference set every generated compilation starts from. Exposed for suites that have to build
        /// a model assembly of their own before handing it back as an extra reference.</summary>
        internal static IReadOnlyList<MetadataReference> BaseReferences => References;

        /// <summary>
        /// The engine test models (<c>Heddle.Tests.dll</c>), loaded from <see cref="AppContext.BaseDirectory"/>
        /// and required for corpus model binding. Throws if not found.
        /// </summary>
        internal static string EngineTestModelsDll()
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Heddle.Tests.dll");
            if (File.Exists(path))
                return path;

            // xunit v3 test projects are executables, and on .NET Framework the managed assembly IS the .exe —
            // there is no companion .dll to copy. Same assembly, different extension.
            var exe = Path.Combine(AppContext.BaseDirectory, "Heddle.Tests.exe");
            if (File.Exists(exe))
                return exe;

            throw new InvalidOperationException(
                "Heddle.Tests.dll is not in this project's output directory (" + path +
                "). It is copied there by the ProjectReference in Heddle.Generator.IntegrationTests.csproj. " +
                "This is a build-wiring failure, not a skippable condition.");
        }

        /// <summary>The engine test models as a single-element reference set — what every corpus suite passes as
        /// <c>extraReferences</c>.</summary>
        internal static IReadOnlyList<MetadataReference> EngineTestModelReferences() =>
            new[] { MetadataReference.CreateFromFile(EngineTestModelsDll()) };

        // Extra references only compile-time load; registering generated output forces runtime loads (manifest instantiation).
        // Track paths to provide assembly resolution.
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
                    // Handing the assembly to Roslyn only equips the precompiled side. The engine resolves a model
                    // type by name over the assemblies actually LOADED in the process, so a corpus template naming
                    // one of these types compiled or failed depending on whether some earlier test in the same run
                    // had happened to load it — the same suite passed or failed on scheduling.
                    //
                    // This load stays, and it is deliberate rather than incidental: a differential test asks whether
                    // two tiers produce the same bytes from the same inputs, and "the model assembly is loaded" is an
                    // input. Leaving it to scheduling does not test the divergence, it just randomises which suite
                    // reports it. The divergence itself — build-time binding over compilation *references* against
                    // run-time binding over *loaded* assemblies — is a real engine property with real consequences
                    // for hosts, and it is asserted head-on in ModelResolutionLoadOrderTests, which also pins this
                    // very line. Delete it and that suite reddens.
                    Assembly.LoadFrom(path);
                }
            }
        }

        private static IReadOnlyList<MetadataReference> BuildReferences()
        {
            var tpa = Heddle.Generator.Tests.HostAssemblies.TrustedOrLoaded();
            var refs = tpa.Split(Path.PathSeparator)
                .Where(p => !string.IsNullOrEmpty(p) && File.Exists(p))
                // Exclude Heddle.Generator: its linked sources would cause name ambiguities (CS0433) in generated code.
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
            Func<string, string> rewriteManifest = null,
            bool checkOverflow = false)
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
                    allowUnsafe: true,
                    // The consumer's <CheckForOverflowUnderflow>, which the generated code is compiled under and
                    // has no say in. The engine's arithmetic is unchecked whatever the host sets, so the emitter's
                    // has to be too.
                    checkOverflow: checkOverflow));

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

        /// <summary>Renders one template through both backends, returning outputs for byte-for-byte comparison.</summary>
        public static (string precompiled, string dynamic) Render(string key, string content, Type modelType,
            object model, Dictionary<string, string> globalOptions = null, TemplateOptions runtimeOptions = null,
            bool checkOverflow = false, IReadOnlyList<MetadataReference> extraReferences = null)
        {
            var gen = Generate(new[] { (key, content) }, globalOptions, extraReferences, checkOverflow: checkOverflow);
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

        /// <summary>Renders an already-generated template through its precompiled entry. For tests that must run the
        /// generator and the engine at separate moments — the other entry points do both inside one call, which is
        /// no use when what is under test is what happens to the process between them.</summary>
        public static string RenderGenerated(GenResult gen, string key, object model,
            TemplateOptions options = null)
        {
            ExpectPrecompiled(gen, key);
            var entryType = FindEntryTypeByKey(gen.Assembly, key)
                            ?? throw new InvalidOperationException("Generated entry class not found for key: " + key);
            var root = (IProcessStrategy) entryType
                .GetField("Root", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static).GetValue(null);
            return PrecompiledRuntime.GenerateString(root, model, null, null, options ?? new TemplateOptions());
        }

        /// <summary>Renders one template through both backends with explicit <see cref="TemplateOptions"/>,
        /// ensuring the precompiled backend receives the same options as the dynamic engine.</summary>
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
        /// allowing independent exception testing (the tuple form short-circuits on the first exception).</summary>
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

        /// <summary>Renders one target template through both backends with corpus imports: the generator sees all
        /// templates as <c>AdditionalFiles</c>, and the dynamic engine reads imports from <paramref name="rootPath"/>.</summary>
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
        /// Runs the generator, registers the assembly into the process-global <see cref="PrecompiledTemplates"/> registry,
        /// and renders through <see cref="TemplateResolver"/> under <see cref="PrecompiledMismatchPolicy.Strict"/> and
        /// <see cref="FallbackGuard"/>. Registry-only mode (default): resolver root is nonexistent. File-backed: corpus staged
        /// to temp directory with file-change checking. Caller must ensure registry isolation (process-global, throws on key collision).
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

            /// <summary>Whether THIS target renders after resolution (per-target). <c>false</c> still verifies
            /// the entry crossed the gauntlet, stopping at <c>TryResolve</c> before rendering.</summary>
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
        /// One generator run and registration for the whole corpus, then resolves and renders every target through
        /// one <see cref="TemplateResolver"/> under <see cref="FallbackGuard"/> and <see cref="PrecompiledMismatchPolicy.Strict"/>.
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
                            // render: false verifies resolution at TryResolve (before rendering) without bytes.
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
            /// <summary>A HED7014 fallback-marker entry: present, with <c>strategy: null</c>.</summary>
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

        /// <summary>Verifies the template was served by a precompiled adapter by reading its private <c>_precompiled</c>
        /// flag (registry misses are silent by design and would escape the guards).</summary>
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

        /// <summary>Writes the corpus to a fresh temp directory, each entry at its declared encoding (via
        /// <see cref="CorpusIntent"/> BOM flag) to reproduce real file-content hashing behavior.</summary>
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

        /// <summary>Reaches the generator's <c>SanitizeName</c> via <c>InternalsVisibleTo</c> rather than mirroring
        /// it, preventing silent drift if naming rules change.</summary>
        private static string SanitizeKey(string key) =>
            generator::Heddle.Generator.HeddleTemplateGenerator.SanitizeName(key);

        /// <summary>Finds the generated entry point: a public static class in the generated namespace with a public
        /// static <c>Generate</c> method returning a string.</summary>
        private static MethodInfo FindEntryPoint(Assembly assembly)
        {
            foreach (var type in assembly.GetTypes())
            {
                if (!type.IsClass || !type.IsAbstract || !type.IsSealed)
                    continue;
                if (type.Namespace != GeneratedNamespace)
                    continue;
                // Select the string-returning overload; multiple Generate methods exist.
                var m = type.GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .FirstOrDefault(mi => mi.Name == "Generate" && mi.ReturnType == typeof(string));
                if (m != null)
                    return m;
            }

            return null;
        }
    }
}
