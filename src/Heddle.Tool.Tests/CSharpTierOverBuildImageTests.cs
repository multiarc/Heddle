using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Heddle.Tool.Tests
{
    /// <summary>Embedded C# over a model the build host loads for itself — a referenced image, which is what a
    /// project's own intermediate model assembly is — rather than one the test process already has. Pins two
    /// regressions that only this shape shows: the host exited 1 with no output at all, because a fault raised
    /// while the C# tier finished its compile lived on the compile result and the host forwarded only the
    /// compile context's list; and the fault itself, which made every FullCSharp template over a same-project
    /// model fail to precompile.</summary>
    [Collection("PrecompiledProcessStateSerial")]
    public class CSharpTierOverBuildImageTests : IDisposable
    {
        private readonly string _dir;
        private readonly Heddle.Data.TemplateOptions _savedDefaultOptions;

        public CSharpTierOverBuildImageTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "heddle-csimage-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
            _savedDefaultOptions = Heddle.Precompiled.PrecompiledTemplates.DefaultOptions;
        }

        public void Dispose()
        {
            Heddle.Precompiled.PrecompiledTemplates.DefaultOptions = _savedDefaultOptions;
            try
            {
                Directory.Delete(_dir, true);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
            }
        }

        private string EmitModels(out string assemblyName)
        {
            assemblyName = "ImageModels" + Guid.NewGuid().ToString("N");
            // A namespace of its own: every image stays loaded for the life of this process, and two
            // assemblies declaring one type name would make that name ambiguous to the engine.
            return Emit(assemblyName, _dir,
                "namespace " + assemblyName + " { public class Plain { public string Value { get; set; } } " +
                "public class Outer { public class Inner { public string Value { get; set; } } } }");
        }

        private static string Emit(string assemblyName, string directory, string source)
        {
            var references = new List<MetadataReference>();
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (!assembly.IsDynamic && !string.IsNullOrEmpty(assembly.Location) && File.Exists(assembly.Location))
                    references.Add(MetadataReference.CreateFromFile(assembly.Location));
            }

            var compilation = CSharpCompilation.Create(assemblyName,
                new[] { CSharpSyntaxTree.ParseText(source) },
                references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, assemblyName + ".dll");
            var emit = compilation.Emit(path);
            Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
            return path;
        }

        private int Compile(string template, string modelSpelling, string modelsPath, string key,
            out string stdout, out string stderr, string expressionMode = "FullCSharp")
        {
            string path = Path.Combine(_dir, key + ".heddle");
            File.WriteAllText(path, "@model(){{" + modelSpelling + "}}\n" + template);
            string rsp = Path.Combine(_dir, key + ".rsp");
            File.WriteAllLines(rsp, new[]
            {
                "--project", Path.Combine(_dir, "proj.csproj"), "--root", _dir,
                "--output-profile", "Text", "--expression-mode", expressionMode,
                "--reference", modelsPath,
                "--template", path + "|" + key + "|||",
                "--artifact-out", Path.Combine(_dir, key + ".bin"),
                "--source-out", Path.Combine(_dir, key + ".g.cs"),
                "--stamp", Path.Combine(_dir, key + ".txt")
            });
            var output = new StringWriter();
            var error = new StringWriter();
            int exit = Program.Run(new[] { "compile", "@" + rsp }, output, error);
            stdout = output.ToString();
            stderr = error.ToString();
            return exit;
        }

        [Theory]
        [InlineData("Plain", "Flat: @(@model.Value.ToUpper())\n")]
        [InlineData("Outer.Inner", "Deep: @(@model.Value.ToUpper())|@(@model.Value.Length + 1)\n")]
        public void EmbeddedCSharpPrecompilesOverAModelTheHostLoadsItself(string modelName, string template)
        {
            string modelsPath = EmitModels(out var ns);
            string modelSpelling = ns + "." + modelName;
            string key = "csimg-" + Guid.NewGuid().ToString("N");
            int exit = Compile(template, modelSpelling, modelsPath, key, out var stdout, out var stderr);
            Assert.True(exit == 0, "exit " + exit + "\nSTDOUT:\n" + stdout + "\nSTDERR:\n" + stderr);
            Assert.True(File.Exists(Path.Combine(_dir, key + ".g.cs")), "no generated source was written.");
        }

        /// <summary>A process that loads images more than once can be handed two different ones under one name — a
        /// rebuilt project, in a test run or any long-lived caller. Pins that an image is shared by what it is,
        /// not by what it is called: the second load answers with the second image, not the first.</summary>
        [Fact]
        public void ASecondImageUnderTheSameNameIsNotAnsweredWithTheFirst()
        {
            string name = "Rebuilt" + Guid.NewGuid().ToString("N");
            string first = Emit(name, Path.Combine(_dir, "v1"),
                "namespace " + name + " { public class Plain { public string Value { get; set; } } }");
            string second = Emit(name, Path.Combine(_dir, "v2"),
                "namespace " + name + " { public class Plain { public string Renamed { get; set; } } }");

            Assembly one, two, again;
            using (var images = new Heddle.Tool.Compile.ImageLoadContext())
                one = images.LoadImage(first);
            using (var images = new Heddle.Tool.Compile.ImageLoadContext())
            {
                two = images.LoadImage(second);
                again = images.LoadImage(second);
            }

            Assert.NotSame(one, two);
            Assert.Same(two, again);
            Assert.NotNull(one.GetType(name + ".Plain", true).GetProperty("Value"));
            Assert.NotNull(two.GetType(name + ".Plain", true).GetProperty("Renamed"));
        }

        /// <summary>A caller that stays alive — this test run is one — gets its images loaded from bytes, so what
        /// it compiled over can be rebuilt or deleted while the process is still there. Deleting proves that only
        /// where an open file cannot be deleted, which is Windows; the image having no location proves it
        /// everywhere, and is what the delete follows from. Both expression tiers: the C# tier hands the image
        /// to the compiler as a reference, which is a second way to end up holding the file.</summary>
        [Theory]
        [InlineData("Native", "Flat: @(Value)\n")]
        [InlineData("FullCSharp", "Flat: @(@model.Value.ToUpper())\n")]
        public void NoImageFileStaysOpenAfterACompile(string expressionMode, string template)
        {
            string modelsPath = EmitModels(out var ns);
            int exit = Compile(template, ns + ".Plain", modelsPath, "open-" + ns, out var stdout, out var stderr,
                expressionMode);
            Assert.True(exit == 0, "STDOUT:\n" + stdout + "\nSTDERR:\n" + stderr);

            using (var images = new Heddle.Tool.Compile.ImageLoadContext())
                Assert.Equal(string.Empty, images.LoadImage(modelsPath).Location);
            File.Delete(modelsPath);
            Assert.False(File.Exists(modelsPath));
        }

        /// <summary>Whatever goes wrong, the host never fails in silence: a failed template is reported with an
        /// id and a message, so the build shows what happened instead of "exited with code 1".</summary>
        [Fact]
        public void AFailedTemplateIsNeverReportedAsABareExitCode()
        {
            string modelsPath = EmitModels(out var ns);
            string key = "csfail-" + Guid.NewGuid().ToString("N");
            // A C# expression that names nothing: the C# tier's own compile fails.
            int exit = Compile("Bad: @(@model.Value.NoSuchMember())\n", ns + ".Plain", modelsPath, key,
                out var stdout, out var stderr);
            Assert.NotEqual(0, exit);
            Assert.True(stdout.Contains("HED") || stderr.Contains("HED"),
                "the host failed without reporting a diagnostic.\nSTDOUT:\n" + stdout + "\nSTDERR:\n" + stderr);
        }
    }
}
