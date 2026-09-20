using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Heddle.Tool.Compile;
using Xunit;

namespace Heddle.Tool.Tests
{
    /// <summary>An entry-point wrapper names its model type in a parameter and in a <c>typeof</c>, inside the
    /// consumer's assembly. Pins the regression where any internal model was spelled there — including one
    /// from a <b>referenced</b> assembly that grants the consumer no access, which is <c>CS0122</c> in the
    /// consumer's build. An internal type is named only when the consumer provably sees it (its own assembly,
    /// or an unsigned <c>InternalsVisibleTo</c> grant naming it); otherwise the wrapper takes <c>object</c>,
    /// and the row still binds and renders.</summary>
    [Collection("PrecompiledProcessStateSerial")]
    public class WrapperModelAccessibilityTests : IDisposable
    {
        private readonly string _dir;
        private readonly Heddle.Data.TemplateOptions _savedDefaultOptions;

        private readonly Dictionary<string, Assembly> _byName =
            new Dictionary<string, Assembly>(StringComparer.Ordinal);

        private Assembly ResolveByName(System.Runtime.Loader.AssemblyLoadContext context, AssemblyName name)
        {
            Assembly found;
            return name.Name != null && _byName.TryGetValue(name.Name, out found) ? found : null;
        }

        public WrapperModelAccessibilityTests()
        {
            System.Runtime.Loader.AssemblyLoadContext.Default.Resolving += ResolveByName;
            _dir = Path.Combine(Path.GetTempPath(), "heddle-access-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
            _savedDefaultOptions = Heddle.Precompiled.PrecompiledTemplates.DefaultOptions;
        }

        public void Dispose()
        {
            System.Runtime.Loader.AssemblyLoadContext.Default.Resolving -= ResolveByName;
            Heddle.Precompiled.PrecompiledTemplates.DefaultOptions = _savedDefaultOptions;
            try
            {
                Directory.Delete(_dir, true);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
            }
        }

        [Fact]
        public void InternalModelOfAReferencedAssemblyWithNoGrantDegradesToObjectAndStillRenders()
        {
            string consumer = "Consumer_" + Guid.NewGuid().ToString("N");
            var result = Build(consumer, passConsumerName: true, grantTo: null);
            Assert.Contains("Generate(object model", result.Source);
            Assert.DoesNotContain(result.ModelFullName, result.Source);
            Assert.Contains("public static class", result.Source);
            // Its sites cannot be printed either, and the notice says why and what to do.
            Assert.Contains("HED7031", result.Stdout);
            Assert.Contains("non-public type '" + result.ModelFullName + "'", result.Stdout);
            Assert.Contains("make the model type and the members the template reads public", result.Stdout);
            AssertRenders(result);
        }

        [Fact]
        public void InternalModelGrantedToTheConsumerIsNamedAndTheWrapperIsInternal()
        {
            string consumer = "Consumer_" + Guid.NewGuid().ToString("N");
            var result = Build(consumer, passConsumerName: true, grantTo: consumer);
            Assert.Contains("Generate(global::" + result.ModelFullName + " model", result.Source);
            Assert.Contains("typeof(global::" + result.ModelFullName + ")", result.Source);
            Assert.Contains("internal static class", result.Source);
            AssertRenders(result);
        }

        [Fact]
        public void AGrantToSomeOtherAssemblyOrAnUnknownConsumerDegradesToObject()
        {
            string consumer = "Consumer_" + Guid.NewGuid().ToString("N");
            var other = Build(consumer, passConsumerName: true, grantTo: "SomebodyElse");
            Assert.Contains("Generate(object model", other.Source);
            AssertRenders(other);

            var unknown = Build(consumer, passConsumerName: false, grantTo: consumer);
            Assert.Contains("Generate(object model", unknown.Source);
            Assert.DoesNotContain(unknown.ModelFullName, unknown.Source);
        }

        private sealed class BuildResult
        {
            internal string Key;
            internal string Source;
            internal string ModelFullName;
            internal Assembly Library;
            internal Assembly Consumer;
            internal string Template;
            internal string Stdout;
        }

        private BuildResult Build(string consumerName, bool passConsumerName, string grantTo)
        {
            string ns = "Lib_" + Guid.NewGuid().ToString("N");
            string libraryName = ns;
            string source =
                (grantTo == null
                    ? string.Empty
                    : "[assembly: System.Runtime.CompilerServices.InternalsVisibleTo(\"" + grantTo + "\")]\n") +
                "namespace " + ns + " { internal class Secret { public string Owner { get; set; } " +
                "public int Level { get; set; } } }";
            string libraryPath = Path.Combine(_dir, libraryName + ".dll");
            var library = CSharpCompilation.Create(libraryName,
                new[] { CSharpSyntaxTree.ParseText(source) }, References(),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            var emitted = library.Emit(libraryPath);
            Assert.True(emitted.Success, string.Join("\n", emitted.Diagnostics));
            // Loaded here first, so the host shares this copy instead of a collectible one it unloads — and
            // from bytes, so it has no Location: this directory is deleted when the test ends, and a loaded
            // assembly pointing at a file that is gone breaks every later test that harvests references.
            var loadedLibrary = Assembly.Load(File.ReadAllBytes(libraryPath));
            // A byte-loaded assembly answers to no name; the consumer compiled below references it by one.
            _byName[libraryName] = loadedLibrary;

            string key = "access-" + Guid.NewGuid().ToString("N");
            string template = "@model(){{" + ns + ".Secret}}\nOwner: @(Owner) level @(Level + 1)\n";
            string path = Path.Combine(_dir, key + ".heddle");
            File.WriteAllText(path, template);
            var arguments = new List<string>
            {
                "--project", Path.Combine(_dir, consumerName + ".csproj"), "--root", _dir,
                "--output-profile", "Text", "--expression-mode", "Native",
                "--reference", libraryPath,
                "--template", path + "|" + key + "|||",
                "--artifact-out", Path.Combine(_dir, key + ".bin"),
                "--source-out", Path.Combine(_dir, key + ".g.cs"),
                "--stamp", Path.Combine(_dir, key + ".txt")
            };
            if (passConsumerName)
            {
                arguments.Add("--assembly-name");
                arguments.Add(consumerName);
            }

            string rsp = Path.Combine(_dir, key + ".rsp");
            File.WriteAllLines(rsp, arguments);
            var stdout = new StringWriter();
            var stderr = new StringWriter();
            int exit = Program.Run(new[] { "compile", "@" + rsp }, stdout, stderr);
            Assert.True(exit == 0, "exit " + exit + "\nSTDOUT:\n" + stdout + "\nSTDERR:\n" + stderr);

            string generated = File.ReadAllText(Path.Combine(_dir, key + ".g.cs"));
            var references = References();
            references.Add(MetadataReference.CreateFromFile(libraryPath));
            var consumer = CSharpCompilation.Create(consumerName,
                new[] { CSharpSyntaxTree.ParseText(generated) }, references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            var image = new MemoryStream();
            var emit = consumer.Emit(image, manifestResources: new[]
            {
                new ResourceDescription("Heddle.CompiledForm",
                    () => File.OpenRead(Path.Combine(_dir, key + ".bin")), true)
            });
            Assert.True(emit.Success, "The consumer did not compile: " + string.Join("\n",
                emit.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.ToString())) +
                "\n" + generated);

            return new BuildResult
            {
                Key = key,
                Source = generated,
                ModelFullName = ns + ".Secret",
                Library = loadedLibrary,
                Consumer = Assembly.Load(image.ToArray()),
                Template = template,
                Stdout = stdout.ToString()
            };
        }

        private void AssertRenders(BuildResult result)
        {
            var modelType = result.Library.GetType(result.ModelFullName, true);
            object model = Activator.CreateInstance(modelType, true);
            modelType.GetProperty("Owner").SetValue(model, "ada");
            modelType.GetProperty("Level").SetValue(model, 4);

            Heddle.HeddleTemplate.Register(result.Library);
            var options = new Heddle.Data.TemplateOptions("access") { OutputProfile = Heddle.Data.OutputProfile.Text };
            string expected = new Heddle.HeddleTemplate(result.Template,
                new Heddle.Runtime.CompileContext(options, new Heddle.Data.ExType(modelType))).Generate(model);
            Assert.Equal("Owner: ada level 5\n", expected);

            Heddle.Precompiled.PrecompiledTemplates.Register(result.Consumer);
            Heddle.Precompiled.PrecompiledTemplates.DefaultOptions =
                new Heddle.Data.TemplateOptions("access-host") { OutputProfile = Heddle.Data.OutputProfile.Text };
            var wrapper = result.Consumer.GetType("Heddle.Generated." + SanitizeName.ForKey(result.Key), true);
            var generate = wrapper.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Single(m => m.Name == "Generate" && m.ReturnType == typeof(string));
            Assert.Equal(expected, (string)generate.Invoke(null, new[] { model, null, null }));
        }

        private static List<MetadataReference> References()
        {
            var references = new List<MetadataReference>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.IsDynamic || string.IsNullOrEmpty(assembly.Location) ||
                    !File.Exists(assembly.Location) || !seen.Add(assembly.Location))
                    continue;
                references.Add(MetadataReference.CreateFromFile(assembly.Location));
            }

            return references;
        }
    }
}
