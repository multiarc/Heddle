using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Heddle.Precompiled.CompiledForm;
using Heddle.Tool;
using Heddle.Tool.Compile;
using Xunit;

[assembly: Heddle.Attributes.ExportExtensions(typeof(Heddle.Tool.Tests.CompileVerbTests.UnfreezableFixtureExtension))]

namespace Heddle.Tool.Tests
{
    public class VerbModel
    {
        public string Name { get; set; }
    }

    /// <summary>The <c>heddle compile</c> verb through the real engine: response-file handling,
    /// option parsing, key derivation, artifact/source/stamp outputs, incrementality, probe and
    /// stubs modes, and MSBuild-format diagnostics with the specified exit codes. Every compile
    /// runs in a temp directory; assertions read the outputs back, never the harness.</summary>
    [Collection("PrecompiledProcessStateSerial")]
    public class CompileVerbTests : IDisposable
    {
        private readonly string _dir;

        public CompileVerbTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "heddle-compile-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

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

        [Fact]
        public void UnknownOptionIsExitTwo()
        {
            var result = Run("compile", "--project", Project(), "--bogus", "x");
            Assert.Equal(2, result.Exit);
            Assert.Contains("--bogus", result.Stderr);
        }

        [Fact]
        public void HostLoadedImageIsSharedNeverReloaded()
        {
            // An image the host already loaded (its engine assemblies, or this assembly in an
            // in-process run) must resolve to the loaded instance: a second copy in the build ALC
            // splits attribute identity and export discovery silently misses.
            string engine = typeof(Heddle.HeddleTemplate).Assembly.Location;
            using (var images = new ImageLoadContext())
            {
                var image = images.LoadImage(engine);
                Assert.Same(typeof(Heddle.HeddleTemplate).Assembly, image);
            }
        }

        [Fact]
        public void MissingProjectIsExitTwo()
        {
            var result = Run("compile", "--artifact-out", Out("form.bin"));
            Assert.Equal(2, result.Exit);
        }

        [Fact]
        public void UnreadableResponseFileIsExitTwo()
        {
            var result = Run("compile", "@" + Path.Combine(_dir, "missing.rsp"));
            Assert.Equal(2, result.Exit);
        }

        [Fact]
        public void BlankResponseLineIsExitTwo()
        {
            string rsp = WriteRsp("--project", Project(), "", "--artifact-out", Out("form.bin"));
            var result = Run("compile", "@" + rsp);
            Assert.Equal(2, result.Exit);
        }

        [Fact]
        public void UnparsableOptionReportsHED7009()
        {
            string template = WriteTemplate("t.heddle", "static\n");
            string rsp = WriteRsp("--project", Project(), "--root", _dir,
                "--output-profile", "Watercolor",
                "--template", template + "||||",
                "--artifact-out", Out("form.bin"), "--source-out", Out("gen.g.cs"),
                "--stamp", Out("stamp.txt"));
            var result = Run("compile", "@" + rsp);
            Assert.Equal(1, result.Exit);
            Assert.Contains("error HED7009", result.Stdout);
            Assert.Contains("HeddleOutputProfile", result.Stdout);
        }

        [Fact]
        public void MissingTemplateFileReportsHED7001()
        {
            string rsp = WriteRsp("--project", Project(), "--root", _dir,
                "--template", Path.Combine(_dir, "gone.heddle") + "||||",
                "--artifact-out", Out("form.bin"), "--source-out", Out("gen.g.cs"),
                "--stamp", Out("stamp.txt"));
            var result = Run("compile", "@" + rsp);
            Assert.Equal(1, result.Exit);
            Assert.Contains("error HED7001", result.Stdout);
        }

        [Fact]
        public void TwoTemplatesCompileToOneTwoRowArtifact()
        {
            string first = WriteTemplate("hello.heddle", "Hello, @(Name)!\n");
            string second = WriteTemplate("static.heddle", "static text\n");
            string rsp = WriteRsp("--project", Project(), "--root", _dir,
                "--output-profile", "Text",
                "--template", first + "||||", "--template", second + "||||",
                "--artifact-out", Out("form.bin"), "--source-out", Out("gen.g.cs"),
                "--stamp", Out("stamp.txt"));
            var result = Run("compile", "@" + rsp);
            Assert.Equal(0, result.Exit);
            var artifact = CompiledFormReader.Read(File.ReadAllBytes(Out("form.bin")));
            Assert.Equal(2, artifact.Templates.Count);
            Assert.Equal("hello.heddle", artifact.Templates[0].Key);
            Assert.Equal("static.heddle", artifact.Templates[1].Key);
            string source = File.ReadAllText(Out("gen.g.cs"));
            Assert.Contains("[assembly: global::Heddle.Precompiled.HeddleCompiledTemplates(typeof(global::Heddle.Generated.HeddleArtifact), 4,",
                source);
            Assert.Contains("public static class Hello", source);
            Assert.Contains("public static class Static", source);
            Assert.True(File.Exists(Out("stamp.txt")));
        }

        [Fact]
        public void UnchangedRebuildIsUpToDateWithoutRewriting()
        {
            string first = WriteTemplate("hello.heddle", "Hello, @(Name)!\n");
            string rsp = WriteRsp("--project", Project(), "--root", _dir,
                "--template", first + "||||",
                "--artifact-out", Out("form.bin"), "--source-out", Out("gen.g.cs"),
                "--stamp", Out("stamp.txt"));
            Assert.Equal(0, Run("compile", "@" + rsp).Exit);
            byte[] artifact = File.ReadAllBytes(Out("form.bin"));
            string source = File.ReadAllText(Out("gen.g.cs"));
            var second = Run("compile", "@" + rsp);
            Assert.Equal(0, second.Exit);
            Assert.Contains("up to date", second.Stdout);
            Assert.Equal(artifact, File.ReadAllBytes(Out("form.bin")));
            Assert.Equal(source, File.ReadAllText(Out("gen.g.cs")));
        }

        [Fact]
        public void ChangedTemplateRecompiles()
        {
            string first = WriteTemplate("hello.heddle", "Hello, @(Name)!\n");
            string rsp = WriteRsp("--project", Project(), "--root", _dir,
                "--template", first + "||||",
                "--artifact-out", Out("form.bin"), "--source-out", Out("gen.g.cs"),
                "--stamp", Out("stamp.txt"));
            Assert.Equal(0, Run("compile", "@" + rsp).Exit);
            byte[] before = File.ReadAllBytes(Out("form.bin"));
            File.WriteAllText(first, "Hello, @(Name)! Welcome.\n");
            Assert.Equal(0, Run("compile", "@" + rsp).Exit);
            Assert.NotEqual(before, File.ReadAllBytes(Out("form.bin")));
        }

        [Fact]
        public void EngineErrorReportsPositionedHEDLine()
        {
            string template = WriteTemplate("typed.heddle",
                "@model(){{Heddle.Tool.Tests.VerbModel}}\n@(NoSuchMember)\n");
            string rsp = WriteRsp("--project", Project(), "--root", _dir,
                "--reference", typeof(VerbModel).Assembly.Location,
                "--template", template + "||||",
                "--artifact-out", Out("form.bin"), "--source-out", Out("gen.g.cs"),
                "--stamp", Out("stamp.txt"));
            var result = Run("compile", "@" + rsp);
            Assert.Equal(1, result.Exit);
            Assert.Matches(@"typed\.heddle\(\d+,\d+,\d+,\d+\): error HED\d+:", result.Stdout);
            Assert.False(File.Exists(Out("form.bin")));
        }

        [Fact]
        public void UnresolvableModelTypeReportsHED7007()
        {
            string template = WriteTemplate("m.heddle", "Hello\n");
            string rsp = WriteRsp("--project", Project(), "--root", _dir,
                "--template", template + "|||No.Such.Type|",
                "--artifact-out", Out("form.bin"), "--source-out", Out("gen.g.cs"),
                "--stamp", Out("stamp.txt"));
            var result = Run("compile", "@" + rsp);
            Assert.Equal(1, result.Exit);
            Assert.Contains("error HED7007", result.Stdout);
        }

        [Fact]
        public void DuplicateExplicitKeyReportsHED7002()
        {
            string template = WriteTemplate("t.heddle", "static\n");
            string rsp = WriteRsp("--project", Project(), "--root", _dir,
                "--template", template + "|same|||",
                "--template", template + "|same|||",
                "--artifact-out", Out("form.bin"), "--source-out", Out("gen.g.cs"),
                "--stamp", Out("stamp.txt"));
            var result = Run("compile", "@" + rsp);
            Assert.Equal(1, result.Exit);
            Assert.Contains("error HED7002", result.Stdout);
        }

        [Fact]
        public void LateBoundCallIsInfoHED7031()
        {
            string template = WriteTemplate("late.heddle", "@toUpper(Name)\n");
            string rsp = WriteRsp("--project", Project(), "--root", _dir,
                "--template", template + "||||",
                "--artifact-out", Out("form.bin"), "--source-out", Out("gen.g.cs"),
                "--stamp", Out("stamp.txt"));
            var result = Run("compile", "@" + rsp);
            Assert.Equal(0, result.Exit);
            Assert.Contains("info HED7031", result.Stdout);
            Assert.Contains("toUpper", result.Stdout);
        }

        [Fact]
        public void ProbeWritesStubsAndUnresolved()
        {
            string template = WriteTemplate("p.heddle", "@model(){{No.Such.Type}}\nHello\n");
            string rsp = WriteRsp("--project", Project(), "--root", _dir,
                "--generated-namespace", "Acme.Generated",
                "--template", template + "||||",
                "--probe", Out("probe.json"));
            var result = Run("compile", "@" + rsp);
            Assert.Equal(0, result.Exit);
            string json = File.ReadAllText(Out("probe.json"));
            Assert.Contains("\"unresolved\":[\"No.Such.Type\"]", json);
            Assert.Contains("\"name\":\"P\"", json);
            Assert.False(File.Exists(Out("form.bin")));
        }

        [Fact]
        public void StubsOnlyWritesThrowingWrappers()
        {
            string template = WriteTemplate("s.heddle", "Hello, @(Name)!\n");
            string rsp = WriteRsp("--project", Project(), "--root", _dir,
                "--template", template + "||||",
                "--stubs-only", Out("stubs.g.cs"));
            var result = Run("compile", "@" + rsp);
            Assert.Equal(0, result.Exit);
            string stubs = File.ReadAllText(Out("stubs.g.cs"));
            Assert.Contains("public static class S", stubs);
            Assert.Contains("throw new global::System.InvalidOperationException", stubs);
            Assert.False(File.Exists(Out("stamp.txt")));
        }

        [Fact]
        public void NamedImportOnlyItemCompilesAndRendersFromHostBytes()
        {
            // The import-only file is literally named Banner, so the dynamic reference resolves
            // the same spelling off disk that the host serves from its item map.
            File.WriteAllText(Path.Combine(_dir, "Banner"), "@%<n_badge>{{NICK}}%@");
            string main = WriteTemplate("main.heddle", "BEFORE\n@<<{{Banner}}\nAFTER\n@n_badge()\n");
            string rsp = WriteRsp("--project", Project(), "--root", _dir,
                "--output-profile", "Text",
                "--template", main + "||||",
                "--import-only", Path.Combine(_dir, "Banner") + "||Banner",
                "--artifact-out", Out("form.bin"), "--source-out", Out("gen.g.cs"),
                "--stamp", Out("stamp.txt"));
            var result = Run("compile", "@" + rsp);
            Assert.Equal(0, result.Exit);
            byte[] image = File.ReadAllBytes(Out("form.bin"));
            var artifact = CompiledFormReader.Read(image);
            // The import-only file loads as a row (answering to Banner) but emits no wrapper. Rows
            // sit in ordinal key order (AC-9), whatever order the items arrived in.
            Assert.Equal(2, artifact.Templates.Count);
            Assert.Equal("Banner.heddle", artifact.Templates[0].Key);
            Assert.Equal("main.heddle", artifact.Templates[1].Key);
            string source = File.ReadAllText(Out("gen.g.cs"));
            Assert.Contains("public static class Main", source);
            Assert.DoesNotContain("Banner", source);

            var assembly = RegisterHostImage(image);
            var saved = Heddle.Precompiled.PrecompiledTemplates.DefaultOptions;
            Heddle.Precompiled.PrecompiledTemplates.DefaultOptions =
                new Heddle.Data.TemplateOptions("host")
                {
                    RootPath = _dir,
                    OutputProfile = Heddle.Data.OutputProfile.Text,
                    EnableFileChangeCheck = false,
                    ExpressionMode = Heddle.Data.ExpressionMode.Native
                };
            try
            {
                var bound = Heddle.Precompiled.PrecompiledTemplates.BindTyped(assembly,
                    "main.heddle", typeof(object));
                var options = new Heddle.Data.TemplateOptions("main")
                {
                    RootPath = _dir,
                    OutputProfile = Heddle.Data.OutputProfile.Text,
                    EnableFileChangeCheck = false,
                    ExpressionMode = Heddle.Data.ExpressionMode.Native
                };
                string expected = new Heddle.HeddleTemplate(File.ReadAllText(main),
                    new Heddle.Runtime.CompileContext(options, Heddle.Data.ExType.Dynamic))
                    .Generate(null);
                Assert.Equal(expected, bound.Generate(null));
                Assert.Equal("BEFORE\nAFTER\nNICK\n", expected);
            }
            finally
            {
                Heddle.Precompiled.PrecompiledTemplates.DefaultOptions = saved;
            }
        }

        [Fact]
        public void SanitizeNameFollowsTheBuildRule()
        {
            Assert.Equal("Views_Home_Index", SanitizeName.ForKey("views/home/index.heddle"));
            Assert.Equal("_9lives", SanitizeName.ForKey("9lives.heddle"));
            Assert.Equal("A_B", SanitizeName.ForKey("a/b.heddle"));
            Assert.Equal("_", SanitizeName.ForKey(string.Empty));
        }

        private string Project() => Path.Combine(_dir, "proj.csproj");

        private string Out(string name) => Path.Combine(_dir, "out", name);

        private string WriteTemplate(string name, string text)
        {
            string path = Path.Combine(_dir, name);
            File.WriteAllText(path, text);
            return path;
        }

        private string WriteRsp(params string[] lines)
        {
            string path = Path.Combine(_dir, Guid.NewGuid().ToString("N") + ".rsp");
            File.WriteAllLines(path, lines);
            return path;
        }

        public sealed class HostMarker : Heddle.Precompiled.IHeddleCompiledArtifact
        {
            internal static byte[] Image;

            public Stream OpenArtifact() => new MemoryStream(Image, writable: false);
        }

        private static Assembly RegisterHostImage(byte[] image)
        {
            HostMarker.Image = image;
            var name = new AssemblyName("HeddleHostAsm_" + Guid.NewGuid().ToString("N"));
            var assembly = AssemblyBuilder.DefineDynamicAssembly(name, AssemblyBuilderAccess.Run);
            var version = typeof(Heddle.HeddleTemplate).Assembly.GetName().Version;
            var ctor = typeof(Heddle.Precompiled.HeddleCompiledTemplatesAttribute).GetConstructor(
                new[] { typeof(Type), typeof(int), typeof(string) });
            assembly.SetCustomAttribute(new CustomAttributeBuilder(ctor, new object[]
            {
                typeof(HostMarker),
                Heddle.Precompiled.PrecompiledSchema.CompiledFormSchemaVersion,
                Heddle.Precompiled.PrecompiledSchema.FormatEngineVersion(version)
            }));
            Heddle.Precompiled.PrecompiledTemplates.Register(assembly);
            return assembly;
        }

        private sealed class RunResult
        {
            internal int Exit;
            internal string Stdout;
            internal string Stderr;
        }

        private static RunResult Run(params string[] args)
        {
            var stdout = new StringWriter();
            var stderr = new StringWriter();
            int exit = Program.Run(args, stdout, stderr);
            return new RunResult { Exit = exit, Stdout = stdout.ToString(), Stderr = stderr.ToString() };
        }
        /// <summary>AC-9: the same template set compiled in two item orders yields identical artifact bytes
        /// (and so the same digest) and the same generated source.</summary>
        [Fact]
        public void ItemOrderDoesNotChangeArtifactBytes()
        {
            string a = WriteTemplate("alpha.heddle", "A @(1 + 1)\n");
            string b = WriteTemplate("beta.heddle", "B static\n");
            string c = WriteTemplate("gamma.heddle", "G @(2 * 3)\n");
            string first = WriteRsp("--project", Project(), "--root", _dir, "--output-profile", "Text",
                "--template", a + "||||", "--template", b + "||||", "--template", c + "||||",
                "--artifact-out", Out("one.bin"), "--source-out", Out("one.g.cs"), "--stamp", Out("one.txt"));
            string second = WriteRsp("--project", Project(), "--root", _dir, "--output-profile", "Text",
                "--template", c + "||||", "--template", a + "||||", "--template", b + "||||",
                "--artifact-out", Out("two.bin"), "--source-out", Out("two.g.cs"), "--stamp", Out("two.txt"));
            Assert.Equal(0, Run("compile", "@" + first).Exit);
            Assert.Equal(0, Run("compile", "@" + second).Exit);
            Assert.Equal(File.ReadAllBytes(Out("one.bin")), File.ReadAllBytes(Out("two.bin")));
            Assert.Equal(File.ReadAllText(Out("one.g.cs")), File.ReadAllText(Out("two.g.cs")));
            var artifact = CompiledFormReader.Read(File.ReadAllBytes(Out("one.bin")));
            Assert.Equal(new[] { "alpha.heddle", "beta.heddle", "gamma.heddle" },
                artifact.Templates.Select(t => t.Key).ToArray());
        }

        /// <summary>P2-R4: an output that cannot be written after every template compiled is a reported
        /// error and exit 1; exit 3 stays for a host fault before any template compiled.</summary>
        [Fact]
        public void UnwritableOutputAfterCompileIsAReportedErrorNotExitThree()
        {
            string template = WriteTemplate("t.heddle", "static text\n");
            // The artifact path names a file inside a path that is itself a file, so the write fails.
            string blocker = Path.Combine(_dir, "blocker");
            File.WriteAllText(blocker, "not a directory");
            string rsp = WriteRsp("--project", Project(), "--root", _dir, "--output-profile", "Text",
                "--template", template + "||||",
                "--artifact-out", Path.Combine(blocker, "form.bin"), "--source-out", Out("gen.g.cs"),
                "--stamp", Out("stamp.txt"));
            var result = Run("compile", "@" + rsp);
            Assert.Equal(1, result.Exit);
            Assert.Contains("error HED7020", result.Stdout);
            Assert.Contains("outputs could not be written", result.Stdout);
        }

        /// <summary>P2-R8: HED7032 when the item's ModelType and the @model directive resolve to
        /// different types; equal spellings raise nothing.</summary>
        [Fact]
        public void ConflictingModelTypeDeclarationsReportHED7032()
        {
            string template = WriteTemplate("m.heddle",
                "@using(){{Heddle.Data}}\n@model(){{TemplateOptions}}\nstatic body\n");
            string rsp = WriteRsp("--project", Project(), "--root", _dir, "--output-profile", "Text",
                "--template", template + "|||Heddle.Runtime.CompileContext, Heddle|",
                "--artifact-out", Out("form.bin"), "--source-out", Out("gen.g.cs"),
                "--stamp", Out("stamp.txt"));
            var result = Run("compile", "@" + rsp);
            Assert.Equal(1, result.Exit);
            Assert.Contains("error HED7032", result.Stdout);
            string agreeing = WriteRsp("--project", Project(), "--root", _dir, "--output-profile", "Text",
                "--template", template + "|||Heddle.Data.TemplateOptions, Heddle|",
                "--artifact-out", Out("form2.bin"), "--source-out", Out("gen2.g.cs"),
                "--stamp", Out("stamp2.txt"));
            var ok = Run("compile", "@" + agreeing);
            Assert.Equal(0, ok.Exit);
            Assert.DoesNotContain("HED7032", ok.Stdout);
        }

        /// <summary>P2-R8: HED7028 when an import names a template by its key while the template also
        /// carries a registered Name; importing by the name is silent.</summary>
        [Fact]
        public void ImportByKeyOfANamedTemplateReportsHED7028()
        {
            string library = WriteTemplate("library.heddle", "LIB\n");
            string byKey = WriteTemplate("bykey.heddle", "@<<{{library}}\n");
            string byName = WriteTemplate("byname.heddle", "@<<{{Lib}}\n");
            string rsp = WriteRsp("--project", Project(), "--root", _dir, "--output-profile", "Text",
                "--template", library + "||Lib||",
                "--template", byKey + "||||", "--template", byName + "||||",
                "--artifact-out", Out("form.bin"), "--source-out", Out("gen.g.cs"),
                "--stamp", Out("stamp.txt"));
            var result = Run("compile", "@" + rsp);
            Assert.Equal(0, result.Exit);
            Assert.Contains("warning HED7028", result.Stdout);
            Assert.Contains("bykey.heddle", result.Stdout);
            Assert.Equal(1, result.Stdout.Split(new[] { "HED7028" }, StringSplitOptions.None).Length - 1);
        }

        /// <summary>P2-R8: a class (c) refusal — a bodied consumer over a call no build-time registration
        /// binds, whose argument has no static type — is HED7014 (warning) at the call, beside HED7031.</summary>
        [Fact]
        public void UnbindableCallTypingRefusalReportsHED7014AtTheCall()
        {
            string template = WriteTemplate("r.heddle", "@if(nosuchfn(Name)){{yes}}\n");
            string rsp = WriteRsp("--project", Project(), "--root", _dir, "--output-profile", "Text",
                "--template", template + "||||",
                "--artifact-out", Out("form.bin"), "--source-out", Out("gen.g.cs"),
                "--stamp", Out("stamp.txt"));
            var result = Run("compile", "@" + rsp);
            Assert.Equal(0, result.Exit);
            Assert.Contains("warning HED7014", result.Stdout);
            Assert.Contains("nosuchfn", result.Stdout);
            Assert.Contains("HED7031", result.Stdout);
        }

        /// <summary>P2-R8: a class (a) refusal — a bound extension declaring [PrecompileUnsupported] — is HED7033
        /// (warning) at the call, carrying the extension's declared reason verbatim, beside HED7031.</summary>
        [Fact]
        public void PrecompileUnsupportedExtensionReportsHED7033AtTheCallWithItsReason()
        {
            Heddle.HeddleTemplate.Register(typeof(UnfreezableFixtureExtension).Assembly);
            string template = WriteTemplate("u.heddle", "<p>@unfreezable()</p>\n");
            string rsp = WriteRsp("--project", Project(), "--root", _dir, "--output-profile", "Text",
                "--template", template + "||||",
                "--artifact-out", Out("form.bin"), "--source-out", Out("gen.g.cs"),
                "--stamp", Out("stamp.txt"));
            var result = Run("compile", "@" + rsp);
            Assert.Equal(0, result.Exit);
            Assert.Contains("warning HED7033", result.Stdout);
            Assert.True(result.Stdout.Contains("u.heddle(1,5"), "HED7033 should be positioned at the call:\n" + result.Stdout);
            Assert.Contains("its hook counts the enclosing chains", result.Stdout);
            Assert.Contains("HED7031", result.Stdout);
        }

        [Heddle.Attributes.ExtensionName("unfreezable")]
        [Heddle.Attributes.PrecompileUnsupported("its hook counts the enclosing chains")]
        public sealed class UnfreezableFixtureExtension : Heddle.Core.AbstractExtension
        {
            public override object ProcessData(in Heddle.Data.Scope scope) => "frozen";

            public override void RenderData(in Heddle.Data.Scope scope)
            {
                scope.Renderer.Render((string)ProcessData(scope));
            }
        }

    }
}
