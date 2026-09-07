using System.IO;
using Xunit;

namespace Heddle.Build.Tests
{
    /// <summary>Every public item, metadatum and property of the <c>Heddle.Build</c> surface changes
    /// the artifact, the generated source or a diagnostic: the options chain rewrites one fixture and
    /// asserts each edit moves the outputs, while the error and assembly tests pin the diagnostics.</summary>
    public class SurfaceTests
    {
        private const string Template = "Hello, @(Name)!\n";

        [Fact]
        public void EveryPropertyAndMetadataChangesTheArtifactOrSource()
        {
            using (var fixture = new MsBuildFixture())
            {
                fixture.Write("templates/hello.heddle", Template);
                string project = fixture.Write("app.csproj",
                    MsBuildFixture.ProjectXml("net10.0", string.Empty,
                        "<HeddleTemplate Include=\"templates/hello.heddle\" />\n"));

                var baseline = fixture.Build(project);
                baseline.AssertSuccess("baseline build");
                string artifact = MsBuildFixture.Artifact(fixture.Root);
                string source = MsBuildFixture.GeneratedSource(fixture.Root);
                string previousArtifact = MsBuildFixture.Sha256File(artifact);
                string previousSource = File.ReadAllText(source);

                previousArtifact = RebuildWith(fixture, project, "HeddleOutputProfile", "Text", previousArtifact);
                previousArtifact = RebuildWithItem(fixture, project, "OutputProfile", "Html", previousArtifact);
                previousArtifact = RebuildWithItem(fixture, project, "Key", "custom-key", previousArtifact);
                previousSource = RebuildName(fixture, project, previousArtifact, previousSource);
                previousArtifact = RebuildWith(fixture, project, "HeddleExpressionMode", "FullCSharp", previousArtifact);
                previousArtifact = RebuildWith(fixture, project, "HeddleTrimDirectiveLines", "false", previousArtifact);
                previousArtifact = RebuildWithStamp(fixture, project, "HeddleMaxRecursionCount", "101");
                previousArtifact = RebuildWithRoot(fixture, project, previousArtifact);
                RebuildNamespace(fixture, project, previousSource);
            }
        }

        [Fact]
        public void PrecompileFalseEmitsRowWithoutWrapper()
        {
            using (var fixture = new MsBuildFixture())
            {
                fixture.Write("templates/hello.heddle", Template);
                string project = fixture.Write("app.csproj",
                    MsBuildFixture.ProjectXml("net10.0", string.Empty,
                        "<HeddleTemplate Include=\"templates/hello.heddle\" />\n"));

                fixture.Build(project).AssertSuccess("baseline build");
                var before = Heddle.Precompiled.CompiledForm.CompiledFormReader.Read(
                    File.ReadAllBytes(MsBuildFixture.Artifact(fixture.Root)));
                Assert.Equal(1, before.Templates.Count);
                Assert.Equal(1, CountWrappers(File.ReadAllText(MsBuildFixture.GeneratedSource(fixture.Root))));
                string stampBefore = File.ReadAllText(MsBuildFixture.Stamp(fixture.Root));

                // Import-only rows load and serve (the row stays) but emit no entry-point wrapper.
                fixture.Write("app.csproj",
                    MsBuildFixture.ProjectXml("net10.0", string.Empty,
                        "<HeddleTemplate Include=\"templates/hello.heddle\"><Precompile>false</Precompile></HeddleTemplate>\n"));
                fixture.Build(project).AssertSuccess("Precompile=false build");
                var after = Heddle.Precompiled.CompiledForm.CompiledFormReader.Read(
                    File.ReadAllBytes(MsBuildFixture.Artifact(fixture.Root)));
                Assert.Equal(1, after.Templates.Count);
                Assert.Equal("templates/hello.heddle", after.Templates[0].Key);
                Assert.Equal(0, CountWrappers(File.ReadAllText(MsBuildFixture.GeneratedSource(fixture.Root))));
                Assert.True(stampBefore != File.ReadAllText(MsBuildFixture.Stamp(fixture.Root)),
                    "Precompile=false left the stamp identical.");
            }
        }

        [Fact]
        public void UnresolvableModelTypeIsHED7007()
        {
            using (var fixture = new MsBuildFixture())
            {
                fixture.Write("templates/hello.heddle", Template);
                fixture.Write("Program.cs", "public static class Program { public static void Main() { } }\n");
                string project = fixture.Write("app.csproj",
                    MsBuildFixture.ProjectXml("net10.0", "    <OutputType>Exe</OutputType>\n",
                        "<HeddleTemplate Include=\"templates/hello.heddle\"><ModelType>No.Such.Type</ModelType></HeddleTemplate>\n"));

                var result = fixture.Build(project);
                Assert.True(result.Exit != 0, "expected the build to fail:\n" + result.Output);
                Assert.Contains("error HED7007", result.Output);
            }
        }

        [Fact]
        public void UnparsableOptionIsHED7009()
        {
            using (var fixture = new MsBuildFixture())
            {
                fixture.Write("templates/hello.heddle", Template);
                string project = fixture.Write("app.csproj",
                    MsBuildFixture.ProjectXml("net10.0",
                        "    <HeddleMaxRecursionCount>banana</HeddleMaxRecursionCount>\n",
                        "<HeddleTemplate Include=\"templates/hello.heddle\" />\n"));

                var result = fixture.Build(project);
                Assert.True(result.Exit != 0, "expected the build to fail:\n" + result.Output);
                Assert.Contains("error HED7009", result.Output);
            }
        }

        [Fact]
        public void DeclaredAssembliesFeedTheCompile()
        {
            using (var fixture = new MsBuildFixture())
            {
                fixture.Write("models/models.csproj",
                    "<Project Sdk=\"Microsoft.NET.Sdk\">\n  <PropertyGroup>\n    <TargetFramework>net10.0</TargetFramework>\n"
                    + "    <Nullable>disable</Nullable>\n    <LangVersion>latest</LangVersion>\n  </PropertyGroup>\n</Project>\n");
                fixture.Write("models/Gadget.cs", "namespace Models { public class Gadget { public string Label { get; set; } } }\n");
                fixture.Write("templates/gadget.heddle", "@model(){{Models.Gadget}}\nLabel: @(Label)\n");
                fixture.Write("Program.cs", "public static class Program { public static void Main() { } }\n");
                string models = Path.Combine(fixture.Root, "models", "models.csproj");
                fixture.Dotnet("build \"" + models + "\" -c " + MsBuildFixture.Configuration + " -m:1 -v:m").AssertSuccess("models build");
                string image = Path.Combine(fixture.Root, "models", "bin", MsBuildFixture.Configuration, "net10.0", "models.dll");
                Assert.True(File.Exists(image), "model image missing at " + image);

                string exeProperties = "    <OutputType>Exe</OutputType>\n";
                string project = fixture.Write("app.csproj",
                    MsBuildFixture.ProjectXml("net10.0", exeProperties,
                        "<HeddleTemplate Include=\"templates/gadget.heddle\" />\n"
                        + "    <Compile Remove=\"models/**/*.cs\" />\n"));

                var bare = fixture.Build(project);
                Assert.True(bare.Exit != 0, "expected the imageless build to fail:\n" + bare.Output);
                Assert.Contains("error HED7007", bare.Output);

                fixture.Write("app.csproj",
                    MsBuildFixture.ProjectXml("net10.0", exeProperties,
                        "<HeddleTemplate Include=\"templates/gadget.heddle\" />\n"
                        + "    <Compile Remove=\"models/**/*.cs\" />\n"
                        + "    <HeddleModelAssembly Include=\"" + image.Replace('\\', '/') + "\" />\n"));
                fixture.Build(project).AssertSuccess("HeddleModelAssembly build");
                Assert.Contains("Models.Gadget", File.ReadAllText(MsBuildFixture.GeneratedSource(fixture.Root)));

                fixture.Write("app.csproj",
                    MsBuildFixture.ProjectXml("net10.0", exeProperties,
                        "<HeddleTemplate Include=\"templates/gadget.heddle\" />\n"
                        + "    <Compile Remove=\"models/**/*.cs\" />\n"
                        + "    <HeddleExtensionAssembly Include=\"" + image.Replace('\\', '/') + "\" />\n"));
                fixture.Build(project).AssertSuccess("HeddleExtensionAssembly build");
                Assert.Contains("Models.Gadget", File.ReadAllText(MsBuildFixture.GeneratedSource(fixture.Root)));
            }
        }

        private static string RebuildWith(MsBuildFixture fixture, string project, string property, string value, string previousHash)
        {
            string text = File.ReadAllText(project);
            string element = "<" + property + ">" + value + "</" + property + ">";
            if (text.Contains("<" + property + ">"))
                text = ReplaceElement(text, property, element);
            else
                text = text.Replace("<LangVersion>latest</LangVersion>",
                    "<LangVersion>latest</LangVersion>\n    " + element);
            File.WriteAllText(project, text);
            var result = fixture.Build(project);
            result.AssertSuccess(property + "=" + value + " build");
            string hash = MsBuildFixture.Sha256File(MsBuildFixture.Artifact(fixture.Root));
            Assert.True(hash != previousHash, property + "=" + value + " left the artifact byte-identical.");
            return hash;
        }

        private static string RebuildWithItem(MsBuildFixture fixture, string project, string metadata, string value, string previousHash)
        {
            string text = File.ReadAllText(project);
            string open = "<HeddleTemplate Include=\"templates/hello.heddle\"";
            int start = text.IndexOf(open);
            Assert.True(start >= 0, "template item missing");
            int tagEnd = text.IndexOf('>', start + open.Length);
            bool selfClosed = text[tagEnd - 1] == '/';
            string withMetadata = selfClosed
                ? text.Substring(0, tagEnd - 1) + "><" + metadata + ">" + value + "</" + metadata + "></HeddleTemplate>"
                    + text.Substring(tagEnd + 1)
                : text.Insert(tagEnd + 1, "<" + metadata + ">" + value + "</" + metadata + ">");
            File.WriteAllText(project, withMetadata);
            var result = fixture.Build(project);
            result.AssertSuccess(metadata + "=" + value + " build");
            string hash = MsBuildFixture.Sha256File(MsBuildFixture.Artifact(fixture.Root));
            Assert.True(hash != previousHash, metadata + "=" + value + " left the artifact byte-identical.");
            return hash;
        }

        // The root moves the key derivation (templates/hello.heddle becomes hello.heddle); it must
        // be absolute — the task joins it with the item path, so a relative root doubles the path.
        private static string RebuildWithRoot(MsBuildFixture fixture, string project, string previousHash)
        {
            string root = Path.Combine(fixture.Root, "templates").Replace('\\', '/');
            string text = File.ReadAllText(project);
            text = text.Replace("<LangVersion>latest</LangVersion>",
                "<LangVersion>latest</LangVersion>\n    <HeddleTemplateRoot>" + root + "</HeddleTemplateRoot>");
            text = text.Replace("templates/hello.heddle", "hello.heddle");
            text = RemoveElement(text, "Key");
            text = RemoveElement(text, "Name");
            File.WriteAllText(project, text);
            fixture.Build(project).AssertSuccess("HeddleTemplateRoot build");
            string hash = MsBuildFixture.Sha256File(MsBuildFixture.Artifact(fixture.Root));
            Assert.True(hash != previousHash, "HeddleTemplateRoot left the artifact byte-identical.");
            return hash;
        }

        // MaxRecursionCount reaches the host (the stamp moves) but is not baked into the row bytes.
        private static string RebuildWithStamp(MsBuildFixture fixture, string project, string property, string value)
        {
            string stampBefore = File.ReadAllText(MsBuildFixture.Stamp(fixture.Root));
            string text = File.ReadAllText(project);
            text = text.Replace("<LangVersion>latest</LangVersion>",
                "<LangVersion>latest</LangVersion>\n    <" + property + ">" + value + "</" + property + ">");
            File.WriteAllText(project, text);
            fixture.Build(project).AssertSuccess(property + "=" + value + " build");
            Assert.True(stampBefore != File.ReadAllText(MsBuildFixture.Stamp(fixture.Root)),
                property + "=" + value + " left the stamp identical.");
            return MsBuildFixture.Sha256File(MsBuildFixture.Artifact(fixture.Root));
        }

        private static string RebuildName(MsBuildFixture fixture, string project, string previousArtifact, string previousSource)
        {
            // Name registers the import alias in the artifact row; the wrapper name follows the key.
            RebuildWithItem(fixture, project, "Name", "Special", previousArtifact);
            return File.ReadAllText(MsBuildFixture.GeneratedSource(fixture.Root));
        }

        private static int CountWrappers(string source)
        {
            int count = 0;
            int at = 0;
            while ((at = source.IndexOf("public static class Templates_", at)) >= 0)
            {
                count++;
                at++;
            }

            return count;
        }

        private static void RebuildNamespace(MsBuildFixture fixture, string project, string previousSource)
        {
            RebuildWithNamespace(fixture, project);
            string source = File.ReadAllText(MsBuildFixture.GeneratedSource(fixture.Root));
            Assert.True(source != previousSource, "HeddleGeneratedNamespace left the generated source identical.");
            Assert.Contains("Heddle.Custom", source);
        }

        private static void RebuildWithNamespace(MsBuildFixture fixture, string project)
        {
            // Heddle.Custom, not an outside namespace: the emitted wrapper names the bare
            // HeddleTemplate type, which resolves through the enclosing Heddle.* namespace.
            string text = File.ReadAllText(project);
            text = text.Replace("<LangVersion>latest</LangVersion>",
                "<LangVersion>latest</LangVersion>\n    <HeddleGeneratedNamespace>Heddle.Custom</HeddleGeneratedNamespace>");
            File.WriteAllText(project, text);
            fixture.Build(project).AssertSuccess("HeddleGeneratedNamespace build");
        }

        private static string RemoveElement(string text, string element)
        {
            int start = text.IndexOf("<" + element + ">");
            if (start < 0)
                return text;
            int end = text.IndexOf("</" + element + ">", start) + element.Length + 3;
            return text.Substring(0, start) + text.Substring(end);
        }

        private static string ReplaceElement(string text, string property, string element)
        {
            int start = text.IndexOf("<" + property + ">");
            int end = text.IndexOf("</" + property + ">", start) + property.Length + 3;
            return text.Substring(0, start) + element + text.Substring(end);
        }


    }
}
