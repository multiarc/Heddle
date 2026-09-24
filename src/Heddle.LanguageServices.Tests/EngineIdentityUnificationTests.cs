using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Heddle.Attributes;
using Heddle.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Heddle.LanguageServices.Tests
{
    /// <summary>A workspace's model assemblies load into their own collectible context, and a build output
    /// directory has a copy of the engine beside them. Pins the regression where that copy was loaded too: the
    /// workspace's <c>[Hidden]</c>, extension base class and function attributes then belonged to a second
    /// engine, and nothing the server's engine asked of them by type matched. Engine references from the
    /// workspace now resolve to the server's own engine — whatever version the workspace was built against.</summary>
    public class EngineIdentityUnificationTests
    {
        [Fact]
        public void WorkspaceTypesShareTheServersEngineTypes()
        {
            var manager = new ModelAssemblyManager();
            try
            {
                var corpus = Assert.Single(manager.Load(new List<string> { CorpusFixture.ModelAssemblyPath }, null));
                Assert.True(File.Exists(Path.Combine(Path.GetDirectoryName(CorpusFixture.ModelAssemblyPath), "Heddle.dll")),
                    "the fixture needs an engine copy beside the model assembly for this test to mean anything.");

                var secret = corpus.GetType("Corpus.Account", true).GetProperty("Secret",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
                Assert.NotNull(secret.GetCustomAttribute<HiddenAttribute>(false));
                Assert.Same(typeof(AbstractExtension), corpus.GetType("Corpus.BadgeExtension", true).BaseType);
                Assert.Same(typeof(HiddenAttribute).Assembly,
                    corpus.GetCustomAttributesData().First(a => a.AttributeType.Name == "ExportFunctionsAttribute")
                        .AttributeType.Assembly);
                Assert.DoesNotContain(AppDomain.CurrentDomain.GetAssemblies(),
                    a => a.GetName().Name == "Heddle" && a != typeof(HiddenAttribute).Assembly);
            }
            finally
            {
                manager.Unload();
            }
        }

        /// <summary>A workspace built against another engine version still unifies to the server's engine — its
        /// sandbox attributes keep their meaning — and the server says so once, in its log, rather than failing
        /// or quietly treating the workspace's attributes as strangers. Nothing of the workspace is held open,
        /// and its context still unloads.</summary>
        [Fact]
        public void AWorkspaceBuiltAgainstAnotherEngineVersionUnifiesAndIsLoggedOnce()
        {
            using (var workspace = new SkewedWorkspace())
            {
                var log = new List<string>();
                var reference = LoadInspectAndUnload(workspace.ModelPath, log);

                // A rebuild can overwrite both files while the server runs.
                File.Delete(workspace.ModelPath);
                File.Delete(workspace.EnginePath);

                var skew = Assert.Single(log, line => line.Contains("1.2.3.4"));
                Assert.Contains(workspace.ModelName, skew);
                Assert.Contains(typeof(HiddenAttribute).Assembly.GetName().Version.ToString(), skew);

                for (int i = 0; i < 10 && reference.IsAlive; i++)
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                }

                Assert.False(reference.IsAlive, "the workspace's context did not unload.");
            }
        }

        /// <summary>The same workspace through the service: the hidden member is not offered, and the version
        /// note reaches the log although the workspace loads while the service is still being constructed —
        /// before the host has had the chance to attach its log sink.</summary>
        [Fact]
        public void TheServiceHonoursHiddenAndLogsTheVersionNoteForAWorkspaceLoadedAtConstruction()
        {
            using (var workspace = new SkewedWorkspace())
            {
                var log = new List<string>();
                var options = new HeddleLanguageServiceOptions
                {
                    AssemblyPaths = new List<string> { workspace.ModelPath }
                };
                using (var service = new HeddleLanguageService(options) { LogSink = log.Add })
                {
                    var (text, offset) = CorpusFixture.At("@model(){{Skew.Vault}} @(§)");
                    service.Analyze("doc.heddle", text, 1);
                    var labels = CorpusFixture.Labels(service.GetCompletions("doc.heddle", offset));
                    Assert.Contains("Open", labels);
                    Assert.DoesNotContain("Secret", labels);
                }

                Assert.Single(log, line => line.Contains("1.2.3.4"));
            }
        }

        /// <summary>Listing the engine's own assemblies in <c>assemblies</c> — a glob over an output directory
        /// does it without anyone meaning to — must not load them. Pins the regression where a configured path
        /// was byte-loaded whatever it was: the workspace's exports then belonged to that second engine, and the
        /// server stopped offering them without a word.</summary>
        [Fact]
        public void EngineAssembliesListedInTheConfigurationAreNotLoadedAndTheExportsStay()
        {
            string dir = Path.GetDirectoryName(CorpusFixture.ModelAssemblyPath);
            var log = new List<string>();
            var options = new HeddleLanguageServiceOptions
            {
                AssemblyPaths = new List<string>
                {
                    Path.Combine(dir, "Heddle.dll"),
                    Path.Combine(dir, "Heddle.Language.dll"),
                    CorpusFixture.ModelAssemblyPath
                }
            };
            using (var service = new HeddleLanguageService(options) { LogSink = log.Add })
            {
                Assert.NotNull(service.Functions);
                Assert.True(service.Functions.Contains("titlecase"));
                var (text, offset) = CorpusFixture.At("@§");
                service.Analyze("doc.heddle", text, 1);
                Assert.Contains("badge", CorpusFixture.Labels(service.GetCompletions("doc.heddle", offset)));
                Assert.DoesNotContain(AppDomain.CurrentDomain.GetAssemblies(),
                    a => a.GetName().Name == "Heddle" && a != typeof(HiddenAttribute).Assembly);

                service.ReloadModelAssemblies();
            }

            Assert.Single(log, line => line.Contains("Heddle.dll") && line.Contains("not loaded"));
            Assert.Single(log, line => line.Contains("Heddle.Language.dll") && line.Contains("not loaded"));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static WeakReference LoadInspectAndUnload(string modelPath, List<string> log)
        {
            var manager = new ModelAssemblyManager();
            var loaded = Assert.Single(manager.Load(new List<string> { modelPath }, log.Add));
            var vault = loaded.GetType("Skew.Vault", true);
            // The attribute written against the old engine IS the server's attribute.
            Assert.NotNull(vault.GetProperty("Secret").GetCustomAttribute<HiddenAttribute>(false));

            // A second load of the same workspace says nothing new.
            manager.Unload();
            manager.Load(new List<string> { modelPath }, log.Add);
            manager.Unload();
            return manager.LastUnloaded;
        }

        /// <summary>A model assembly compiled against a stand-in for an older engine — same simple name, another
        /// version, no strong name — with that stand-in beside it, as a build output directory would have it.</summary>
        private sealed class SkewedWorkspace : IDisposable
        {
            private readonly string _dir;

            internal SkewedWorkspace()
            {
                _dir = Path.Combine(Path.GetTempPath(), "heddle-skew-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(_dir);
                var corlib = MetadataReference.CreateFromFile(typeof(object).Assembly.Location);
                var runtime = MetadataReference.CreateFromFile(
                    Path.Combine(Path.GetDirectoryName(typeof(object).Assembly.Location), "System.Runtime.dll"));
                var oldEngine = CSharpCompilation.Create("Heddle",
                    new[]
                    {
                        CSharpSyntaxTree.ParseText(
                            "[assembly: System.Reflection.AssemblyVersion(\"1.2.3.4\")]\n" +
                            "namespace Heddle.Attributes { [System.AttributeUsage(System.AttributeTargets.Property)] " +
                            "public sealed class HiddenAttribute : System.Attribute { } }")
                    },
                    new[] { corlib, runtime }, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
                EnginePath = Path.Combine(_dir, "Heddle.dll");
                Assert.True(oldEngine.Emit(EnginePath).Success);

                ModelName = "SkewModels" + Guid.NewGuid().ToString("N");
                var models = CSharpCompilation.Create(ModelName,
                    new[]
                    {
                        CSharpSyntaxTree.ParseText(
                            "namespace Skew { public class Vault { [Heddle.Attributes.Hidden] public string Secret " +
                            "{ get; set; } public string Open { get; set; } } }")
                    },
                    new[] { corlib, runtime, MetadataReference.CreateFromFile(EnginePath) },
                    new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
                ModelPath = Path.Combine(_dir, ModelName + ".dll");
                Assert.True(models.Emit(ModelPath).Success);
            }

            internal string ModelName { get; }

            internal string ModelPath { get; }

            internal string EnginePath { get; }

            public void Dispose()
            {
                try
                {
                    Directory.Delete(_dir, true);
                }
                catch (IOException)
                {
                }
            }
        }
    }
}
