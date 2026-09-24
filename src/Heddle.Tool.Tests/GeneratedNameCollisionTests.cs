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
    /// <summary>A template's file name becomes a class in the generated namespace, and every simple name the
    /// generated source writes is looked up there first. Pins the regression where the emitter wrote
    /// <c>Stream</c>, <c>TextWriter</c>, <c>System.Buffers…</c> and <c>Heddle.…</c> unqualified: a template
    /// called <c>system.heddle</c>, <c>stream.heddle</c> or <c>heddle.heddle</c> produced source that did not
    /// compile (CS0426, CS0722, CS0738) — in the real build and in the design-time stubs alike.</summary>
    [Collection("PrecompiledProcessStateSerial")]
    public class GeneratedNameCollisionTests : IDisposable
    {
        private static readonly string[] Names =
        {
            "system", "stream", "heddle", "object", "string", "func", "delegate", "textWriter", "type",
            "invalidOperationException", "heddleTemplate", "precompiledTemplates", "buffers", "io", "global",
            "iPrecompiledSiteTable", "iHeddleCompiledArtifact", "heddleCompiledTemplates", "generated", "data",
            // Every member name the emitter writes into a wrapper or the artifact class, in the spellings a
            // key can reach (a class may not declare a member named after itself): the private ones in their
            // current and former spellings, and the artifact class's members.
            "bound", "_bound", "_bindLock", "bind", "bindLock", "heddleBound", "heddleBindLock", "heddleBind",
            "openArtifact", "artifactDigest", "tryGetSite", "site", "model", "writer", "chained", "callerData"
        };

        private readonly string _dir;
        private readonly Heddle.Data.TemplateOptions _savedDefaultOptions;

        public GeneratedNameCollisionTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "heddle-names-" + Guid.NewGuid().ToString("N"));
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

        [Theory]
        [InlineData("Native")]
        [InlineData("FullCSharp")]
        public void TemplatesNamedLikeFrameworkAndEngineNamesCompileAndRender(string mode)
        {
            string prefix = "n" + Guid.NewGuid().ToString("N").Substring(0, 8);
            string body = mode == "FullCSharp"
                ? "Hello @(Customer.Name) @(Qty + 1) @(@model.Qty.ToString(\"D2\"))\n"
                : "Hello @(Customer.Name) @(Qty + 1) @(Customer.Address.City ?? \"-\")\n";
            var arguments = new List<string>
            {
                "--project", Path.Combine(_dir, "proj.csproj"), "--root", _dir,
                "--output-profile", "Text", "--expression-mode", mode
            };
            foreach (var name in Names)
            {
                string path = Path.Combine(_dir, prefix, name + ".heddle");
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, body);
                // Root-level keys: the class is named after the file alone.
                arguments.Add("--template");
                arguments.Add(path + "|" + name + ".heddle||" + typeof(RoundtripOrder).AssemblyQualifiedName + "|");
            }

            string source = Path.Combine(_dir, mode + ".g.cs");
            string artifact = Path.Combine(_dir, mode + ".bin");
            arguments.AddRange(new[] { "--artifact-out", artifact, "--source-out", source,
                "--stamp", Path.Combine(_dir, mode + ".txt") });
            Run(arguments, mode + ".rsp");

            var loaded = Compile(File.ReadAllText(source), artifact);
            foreach (var name in Names)
            {
                string className = char.ToUpperInvariant(name[0]) + name.Substring(1);
                Assert.NotNull(loaded.GetType("Heddle.Generated." + className));
            }

            // The registry is process-wide and these keys are fixed by the names under test, so only one
            // row registers and renders; the other proves its source compiles.
            if (mode != "Native")
                return;
            Heddle.Precompiled.PrecompiledTemplates.Register(loaded);
            var options = new Heddle.Data.TemplateOptions("names") { OutputProfile = Heddle.Data.OutputProfile.Text };
            if (mode == "FullCSharp")
                options.ExpressionMode = Heddle.Data.ExpressionMode.FullCSharp;
            Heddle.Precompiled.PrecompiledTemplates.DefaultOptions = options;
            var model = new RoundtripOrder
            {
                Customer = new RoundtripCustomer { Name = "Ada", Address = new RoundtripAddress { City = "Minsk" } },
                Qty = 4
            };
            string expected = new Heddle.HeddleTemplate(body,
                new Heddle.Runtime.CompileContext(new Heddle.Data.TemplateOptions(options),
                    new Heddle.Data.ExType(typeof(RoundtripOrder)))).Generate(model);
            foreach (var name in new[] { "system", "heddle", "stream" })
            {
                var wrapper = loaded.GetType("Heddle.Generated." + char.ToUpperInvariant(name[0]) + name.Substring(1));
                var generate = wrapper.GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .Single(m => m.Name == "Generate" && m.ReturnType == typeof(string));
                Assert.Equal(expected, (string)generate.Invoke(null, new object[] { model, null, null }));
            }
        }

        [Fact]
        public void TheDesignTimeStubsCompileForTheSameNames()
        {
            var arguments = new List<string>
            {
                "--project", Path.Combine(_dir, "proj.csproj"), "--root", _dir,
                "--output-profile", "Text", "--expression-mode", "Native"
            };
            foreach (var name in Names)
            {
                string path = Path.Combine(_dir, "stubs", name + ".heddle");
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, "static\n");
                arguments.Add("--template");
                arguments.Add(path + "|" + name + ".heddle|||");
            }

            string stubs = Path.Combine(_dir, "stubs.g.cs");
            arguments.AddRange(new[] { "--stubs-only", stubs });
            Run(arguments, "stubs.rsp");
            Compile(File.ReadAllText(stubs), null);
        }

        /// <summary><c>Generate</c> is the one member of an entry class that callers name, so it cannot be
        /// renamed out of a key's way: a key that sanitizes to it is refused by the build, positioned at the
        /// template, instead of surfacing as CS0542 from generated code.</summary>
        [Theory]
        [InlineData("generate.heddle")]
        [InlineData("Generate.heddle")]
        [InlineData("heddleArtifact.heddle")]
        public void AKeyThatSanitizesToAReservedNameIsABuildError(string key)
        {
            string path = Path.Combine(_dir, "reserved", Guid.NewGuid().ToString("N"), key);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, "static\n");
            foreach (var mode in new[] { "real", "stubs" })
            {
                var arguments = new List<string>
                {
                    "--project", Path.Combine(_dir, "proj.csproj"), "--root", _dir,
                    "--output-profile", "Text", "--expression-mode", "Native",
                    "--template", path + "|" + key + "|||"
                };
                if (mode == "stubs")
                    arguments.AddRange(new[] { "--stubs-only", Path.Combine(_dir, "reserved.stubs.g.cs") });
                else
                    arguments.AddRange(new[] { "--artifact-out", Path.Combine(_dir, "r.bin"), "--source-out",
                        Path.Combine(_dir, "r.g.cs"), "--stamp", Path.Combine(_dir, "r.txt") });
                string rsp = Path.Combine(_dir, "reserved-" + mode + ".rsp");
                File.WriteAllLines(rsp, arguments);
                var stdout = new StringWriter();
                int exit = Heddle.Tool.Program.Run(new[] { "compile", "@" + rsp }, stdout, new StringWriter());
                Assert.True(exit != 0, mode + ": expected a build error. " + stdout);
                Assert.Contains("HED7010", stdout.ToString());
                Assert.Contains(key, stdout.ToString());
            }
        }

        [Fact]
        public void TwoKeysThatSanitizeToOneClassNameAreABuildErrorNotACompilerError()
        {
            var arguments = new List<string>
            {
                "--project", Path.Combine(_dir, "proj.csproj"), "--root", _dir,
                "--output-profile", "Text", "--expression-mode", "Native"
            };
            foreach (var name in new[] { "a-b", "a_b" })
            {
                string path = Path.Combine(_dir, "twins", name + ".heddle");
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, "static\n");
                arguments.Add("--template");
                arguments.Add(path + "|" + name + ".heddle|||");
            }

            arguments.AddRange(new[] { "--artifact-out", Path.Combine(_dir, "t.bin"), "--source-out",
                Path.Combine(_dir, "t.g.cs"), "--stamp", Path.Combine(_dir, "t.txt") });
            File.WriteAllLines(Path.Combine(_dir, "twins.rsp"), arguments);
            var stdout = new StringWriter();
            int exit = Heddle.Tool.Program.Run(new[] { "compile", "@" + Path.Combine(_dir, "twins.rsp") }, stdout,
                new StringWriter());
            Assert.NotEqual(0, exit);
            Assert.Contains("HED7010", stdout.ToString());
        }

        private void Run(List<string> arguments, string rspName)
        {
            string rsp = Path.Combine(_dir, rspName);
            File.WriteAllLines(rsp, arguments);
            var stdout = new StringWriter();
            var stderr = new StringWriter();
            int exit = Heddle.Tool.Program.Run(new[] { "compile", "@" + rsp }, stdout, stderr);
            Assert.True(exit == 0, "exit " + exit + "\nSTDOUT:\n" + stdout + "\nSTDERR:\n" + stderr);
        }

        private static Assembly Compile(string source, string artifactPath)
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

            var compilation = CSharpCompilation.Create("HeddleNames_" + Guid.NewGuid().ToString("N"),
                new[] { CSharpSyntaxTree.ParseText(source) }, references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            var image = new MemoryStream();
            var resources = artifactPath == null
                ? null
                : new[] { new ResourceDescription("Heddle.CompiledForm", () => File.OpenRead(artifactPath), true) };
            var emit = compilation.Emit(image, manifestResources: resources);
            Assert.True(emit.Success, "The generated source did not compile:\n" + string.Join("\n",
                emit.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Take(12)
                    .Select(d => d.ToString())));
            return Assembly.Load(image.ToArray());
        }
    }
}
