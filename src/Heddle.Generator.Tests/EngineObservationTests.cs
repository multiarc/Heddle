extern alias gen;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using BundleLoader = gen::Heddle.Generator.Observe.BundleLoader;
using EngineObservation = gen::Heddle.Generator.Observe.EngineObservation;
using IntermediateAssembly = gen::Heddle.Generator.Observe.IntermediateAssembly;

namespace Heddle.Generator.Tests
{
    /// <summary>
    /// Layer 2 end to end: the build emits the compilation being built to a content-addressed intermediate
    /// assembly, loads it as a bundle with the consumer's own engine, compiles the real template through the real
    /// engine, and types a body from what the extension's own hook chose.
    /// <para>The extension used here is <b>declared in the compilation being built</b> — the case that was
    /// structurally impossible before an intermediate assembly existed, and the clearest proof that one does. Its
    /// <c>InitStart</c> re-types the body to the element type of the collection it is handed, which no table could
    /// predict for a third-party name.</para>
    /// </summary>
    public class EngineObservationTests : IDisposable
    {
        private readonly string _directory;

        public EngineObservationTests()
        {
            _directory = Path.Combine(Path.GetTempPath(),
                "heddle-observe-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
        }

        public void Dispose()
        {
            // The intermediate assemblies stay loaded for the life of this process by design, so the directory is
            // deliberately NOT deleted here: unloading is what content addressing exists to make unnecessary.
        }

        private Dictionary<string, string> Options(string mode) => new Dictionary<string, string>
        {
            ["build_property.HeddleObserveEngine"] = mode,
            ["build_property.HeddleObserveIntermediatePath"] = _directory
        };

        /// <summary>The source-declared extension: a bodied hook that hands its body the element type of the
        /// collection it was called with, and a string on the chained channel. Named uniquely per test because
        /// the engine's registry is process-global and append-only.</summary>
        private static string ExtensionSource(string name) => GeneratorHarness.WithAllExtensionsExported(@"
using Heddle.Attributes;
using Heddle.Core;
using Heddle.Data;

namespace ObserveProbe
{
    public class Row { public string Title { get; set; } }

    public class Page { public System.Collections.Generic.List<Row> Rows { get; set; } }

    [ExtensionName(""" + name + @""")]
    public class " + Pascal(name) + @"Extension : AbstractExtension
    {
        public override ExType InitStart(InitContext ctx, ExType dataType, ExType chainedType, ExType parent)
        {
            var element = dataType != null && dataType.Type != null && dataType.Type.IsGenericType
                ? (ExType)dataType.Type.GetGenericArguments()[0]
                : dataType;
            return base.InitStart(ctx, element, (ExType)typeof(string), parent);
        }

        public override object ProcessData(in Scope scope) => string.Empty;

        public override void RenderData(in Scope scope)
        {
        }
    }
}");

        private static string Pascal(string name) => char.ToUpperInvariant(name[0]) + name.Substring(1);

        private const string Template = "@model(){{ObserveProbe.Page}}@NAME(Rows){{[@(Title)]}}";

        /// <summary>
        /// <b>The flagship.</b> An extension declared in the compilation being built, whose hook re-types its body
        /// to the collection's element type, has that body emitted <i>typed</i> — the element type is cast in the
        /// generated source — because the build ran the real hook through the real engine and read the answer off
        /// the engine's own scope map. With observation off the same template still precompiles, through the
        /// type-agnostic accessor, and that is the whole difference.
        /// </summary>
        [Fact]
        public void AnExtensionDeclaredInTheCompilationBeingBuiltHasItsBodyTypedByItsOwnHook()
        {
            var observed = Run("obsflag", Options("Strict"));
            var blind = Run("obsflagoff", Options("Off"));

            // The hook's own answer, written into the site: the body's model is the collection's ELEMENT type,
            // which no name table could have predicted for a third-party extension.
            Assert.Contains("AssumedDataType = typeof(global::ObserveProbe.Row)", observed);
            Assert.Contains("TypeAgnostic = false", observed);
            Assert.Contains("(global::ObserveProbe.Row)scope.ModelData", observed);

            // Without observation the same body still precompiles, through the engine's own accessor.
            Assert.Contains("TypeAgnostic = true", blind);
            Assert.DoesNotContain("(global::ObserveProbe.Row)scope.ModelData", blind);
        }

        /// <summary>Observation never costs a template its tier: the same template precompiles either way, and
        /// what changes is only how its body reads a member.</summary>
        [Fact]
        public void ObservationNeverCostsATemplateItsTier()
        {
            Assert.Contains("__HeddleManifest", Run("obstier1", Options("Strict")));
            Assert.Contains("__HeddleManifest", Run("obstier2", Options("Off")));
            Assert.Contains("key: \"page.heddle\"", Run("obstier3", Options("Strict")));
            Assert.Contains("key: \"page.heddle\"", Run("obstier4", Options("Off")));
        }

        /// <summary>
        /// <b>The immutability proof, and the whole safety argument for loading out of a build directory.</b>
        /// <c>Assembly.LoadFrom</c> holds a file open for the compiler server's life, which is why build outputs
        /// were previously refused. Every path this process loaded is named after a digest of its own content, so
        /// a changed input produces a <i>different</i> path and the permanent lock is always on a file nothing
        /// will ever rewrite.
        /// </summary>
        [Fact]
        public void EveryLoadedPathIsContentAddressedAndNothingWillEverRewriteIt()
        {
            Run("obsimmut", Options("Strict"));

            var loaded = BundleLoader.LoadedFromPaths;
            Assert.NotEmpty(loaded);
            foreach (var path in loaded)
            {
                var name = Path.GetFileNameWithoutExtension(path);
                var tag = name.Substring(name.LastIndexOf('.') + 1);

                // A 32-hex module version id, or a 64-hex content digest. Either way the name is a function of the
                // bytes, so writing different bytes means writing a different file.
                Assert.True(tag.Length == 32 || tag.Length == 64,
                    "Loaded path is not content-addressed: " + path);
                Assert.True(tag.All(Uri.IsHexDigit), "Loaded path is not content-addressed: " + path);
            }
        }

        /// <summary>A changed compilation produces a different intermediate-assembly path, which is what makes the
        /// lock harmless: nothing ever overwrites a file that is already loaded.</summary>
        [Fact]
        public void AChangedCompilationProducesADifferentIntermediateAssemblyPath()
        {
            var first = DigestOf("class A { }");
            var again = DigestOf("class A { }");
            var changed = DigestOf("class A { int x; }");

            Assert.Equal(first, again);
            Assert.NotEqual(first, changed);
        }

        /// <summary>
        /// <b>Purity.</b> Two generator runs over identical inputs produce identical sources, and the second emits
        /// no intermediate assembly at all — every read is a cache hit.
        /// </summary>
        [Fact]
        public void ASecondRunOverIdenticalInputsEmitsNoIntermediateAssemblyAndTheSameSource()
        {
            var options = Options("Strict");
            var first = Run("obspure", options);
            var producedAfterFirst = IntermediateAssembly.ProducedCount;
            var compilesAfterFirst = EngineObservation.CompilesRun;

            var second = Run("obspure", options);

            Assert.Equal(first, second);
            Assert.Equal(producedAfterFirst, IntermediateAssembly.ProducedCount);
            Assert.Equal(compilesAfterFirst, EngineObservation.CompilesRun);
        }

        /// <summary>Under <c>Strict</c> a build that needed to observe and could not fails, so a CI leg never
        /// silently emits different sources from a developer machine that can. Under <c>Auto</c> the same build
        /// says nothing, because a build that never configured an observe directory is not one that tried and
        /// failed.</summary>
        [Fact]
        public void StrictErrorsWhereAutoIsSilentWhenThereIsNowhereToWrite()
        {
            var strict = Unwritable("obsnowhere1", "Strict");
            var auto = Unwritable("obsnowhere2", "Auto");

            var reported = strict.GeneratorDiagnostics.Where(d => d.Id == "HED7034").ToList();
            Assert.Single(reported);
            Assert.Equal(Microsoft.CodeAnalysis.DiagnosticSeverity.Error, reported[0].Severity);
            Assert.Empty(auto.GeneratorDiagnostics.Where(d => d.Id == "HED7034"));
        }

        /// <summary>
        /// A build whose templates the generator already types never asks to observe, so it emits no intermediate
        /// assembly, loads nothing, and cannot fail to observe — <c>Strict</c> included. Building the bundle is a
        /// whole C# compile of the compilation being built and the generator runs on every change to it, so an
        /// unconditional one would put that compile on an editor's keystroke path for every consumer, including
        /// every consumer with nothing to gain from it.
        /// </summary>
        [Fact]
        public void ATemplateTheBuildAlreadyTypesNeverAsksToObserve()
        {
            var run = GeneratorHarness.RunWithSources(
                new[] { ("page.heddle", "@model(){{ObserveProbe.Page}}@if(Rows){{[ok]}}") },
                new[] { ExtensionSource("obsunused") },
                new Dictionary<string, string>
                {
                    ["build_property.HeddleObserveEngine"] = "Strict"
                });

            Assert.Empty(run.GeneratorDiagnostics.Where(d => d.Id == "HED7034"));
            Assert.Contains("__HeddleManifest", string.Join("\n", run.GeneratedSourceTexts));
        }

        private static GeneratorRun Unwritable(string extensionName, string mode) =>
            GeneratorHarness.RunWithSources(
                new[] { ("page.heddle", Template.Replace("NAME", extensionName)) },
                new[] { ExtensionSource(extensionName) },
                new Dictionary<string, string> { ["build_property.HeddleObserveEngine"] = mode });

        /// <summary>An unparsable <c>HeddleObserveEngine</c> is HED7009 like every other option, and the build
        /// falls back to the default rather than guessing from a typo.</summary>
        [Fact]
        public void AnUnparsableObserveModeIsAnOptionParseError()
        {
            var run = GeneratorHarness.Run(new[] { ("page.heddle", "hello") },
                new Dictionary<string, string> { ["build_property.HeddleObserveEngine"] = "sometimes" });

            var error = Assert.Single(run.GeneratorDiagnostics.Where(d => d.Id == "HED7009"));
            Assert.Contains("HeddleObserveEngine", error.GetMessage());
            Assert.Contains("Off|Auto|Strict", error.GetMessage());
        }

        private string Run(string extensionName, Dictionary<string, string> options)
        {
            var run = GeneratorHarness.RunWithSources(
                new[] { ("page.heddle", Template.Replace("NAME", extensionName)) },
                new[] { ExtensionSource(extensionName) },
                options);
            return string.Join("\n----\n", run.GeneratedSourceTexts);
        }

        private static string DigestOf(string source)
        {
            var compilation = Microsoft.CodeAnalysis.CSharp.CSharpCompilation.Create("DigestProbe",
                new[] { Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(source) },
                new[]
                {
                    Microsoft.CodeAnalysis.MetadataReference.CreateFromFile(typeof(object).Assembly.Location)
                },
                new Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions(
                    Microsoft.CodeAnalysis.OutputKind.DynamicallyLinkedLibrary));
            return IntermediateAssembly.Digest(compilation);
        }
    }
}
