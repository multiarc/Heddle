using System.IO;
using Xunit;

namespace Heddle.Build.Tests
{
    /// <summary>Same-project models through real MSBuild, in the project shape most consumers have: a class
    /// library under <c>ImplicitUsings</c>. Pins four regressions of the intermediate compile: it demanded an
    /// entry point (no target type, so <c>CS5001</c> in every library); it dropped the SDK's generated global
    /// usings with everything else under <c>obj</c> (<c>CS0246</c>); a model edit was not a compile input, so
    /// the artifact went stale; and the wrapper spelled its model from the reflection name, which is not C#
    /// for a generic or nested type and not accessible for an internal one.</summary>
    public class SameProjectModelTests
    {
        private const string Models =
            "namespace LibApp;\n"
            // No using directive: List<> and DateTime resolve through the implicit global usings only.
            + "public class Order { public string Name { get; set; } public List<string> Tags { get; set; } public DateTime Placed { get; set; } }\n"
            + "internal class Ledger { public string Owner { get; set; } }\n"
            + "public class Box<T> { public T Content { get; set; } public int Count { get; set; } }\n"
            + "public class Outer { public class Inner { public string Label { get; set; } } }\n"
            + "public static class Caller\n"
            + "{\n"
            + "    public static string Render(Order order) => Heddle.Generated.Templates_Order.Generate(order);\n"
            + "}\n";

        private static string WriteLibrary(MsBuildFixture fixture, string extraProperties = "")
        {
            fixture.Write("templates/order.heddle", "@model(){{LibApp.Order}}\nOrder: @(Name) @list(Tags){{#@(this)}}\n");
            fixture.Write("templates/ledger.heddle", "@model(){{LibApp.Ledger}}\nOwner: @(Owner)\n");
            fixture.Write("templates/box.heddle", "@model(){{LibApp.Box<LibApp.Order>}}\nBox: @(Content.Name) x@(Count)\n");
            fixture.Write("templates/inner.heddle", "@model(){{LibApp.Outer.Inner}}\nInner: @(Label)\n");
            fixture.Write("Models.cs", Models);
            return WriteLibraryProject(fixture, extraProperties);
        }

        private static string WriteLibraryProject(MsBuildFixture fixture, string extraProperties)
        {
            return fixture.Write("app.csproj",
                MsBuildFixture.ProjectXml("net10.0",
                    "    <ImplicitUsings>enable</ImplicitUsings>\n    <HeddleOutputProfile>Text</HeddleOutputProfile>\n"
                    + extraProperties,
                    "<HeddleTemplate Include=\"templates/*.heddle\" />\n"));
        }

        [Fact]
        public void LibraryWithImplicitUsingsPrecompilesInternalGenericAndNestedModels()
        {
            using (var fixture = new MsBuildFixture())
            {
                string project = WriteLibrary(fixture);

                var build = fixture.Build(project);
                build.AssertSuccess("class-library build with same-project models");
                Assert.DoesNotContain("CS5001", build.Output);
                Assert.DoesNotContain("CS0246", build.Output);
                Assert.DoesNotContain("CS0051", build.Output);

                string source = File.ReadAllText(MsBuildFixture.GeneratedSource(fixture.Root));
                Assert.Contains("Generate(global::LibApp.Order model", source);
                Assert.Contains("Generate(global::LibApp.Ledger model", source);
                Assert.Contains("Generate(global::LibApp.Box<global::LibApp.Order> model", source);
                Assert.Contains("Generate(global::LibApp.Outer.Inner model", source);
                Assert.Contains("public static class Templates_Order", source);
                Assert.Contains("internal static class Templates_Ledger", source);
                Assert.True(File.Exists(MsBuildFixture.BuiltAssembly(fixture.Root, "app")));
            }
        }

        [Fact]
        public void EditingAModelRecompilesTheTemplatesAndAnUnchangedRebuildStillSkips()
        {
            using (var fixture = new MsBuildFixture())
            {
                string project = WriteLibrary(fixture);
                fixture.Write("templates/order.heddle", "@model(){{LibApp.Order}}\nOrder: @(Name) @(Placed.Year)\n");

                fixture.Build(project).AssertSuccess("baseline build");
                string artifactBefore = MsBuildFixture.Sha256File(MsBuildFixture.Artifact(fixture.Root));
                string stampBefore = File.ReadAllText(MsBuildFixture.Stamp(fixture.Root));
                Assert.Single(ModelDirectories(fixture));

                // The member the template reads changes type: a stale artifact would still record DateTime.
                System.Threading.Thread.Sleep(1100);
                fixture.Write("Models.cs", Models.Replace("public DateTime Placed", "public DateTimeOffset Placed"));
                var edited = fixture.Build(project);
                edited.AssertSuccess("rebuild after a model edit");
                Assert.DoesNotContain("Skipping target \"_HeddleCompile\"", edited.Output);
                Assert.NotEqual(stampBefore, File.ReadAllText(MsBuildFixture.Stamp(fixture.Root)));
                Assert.NotEqual(artifactBefore, MsBuildFixture.Sha256File(MsBuildFixture.Artifact(fixture.Root)));
                Assert.Single(ModelDirectories(fixture));

                string artifactEdited = MsBuildFixture.Sha256File(MsBuildFixture.Artifact(fixture.Root));
                System.Threading.Thread.Sleep(1100);
                fixture.Write("Unrelated.txt", "not a compile input\n");
                var unchanged = fixture.Build(project);
                unchanged.AssertSuccess("unchanged rebuild");
                Assert.Contains("Skipping target \"_HeddleCompile\" because all output files are up-to-date",
                    unchanged.Output);
                Assert.Equal(artifactEdited, MsBuildFixture.Sha256File(MsBuildFixture.Artifact(fixture.Root)));

                // Reverting is an edit too: the artifact returns to the first build's bytes.
                System.Threading.Thread.Sleep(1100);
                fixture.Write("Models.cs", Models);
                var reverted = fixture.Build(project);
                reverted.AssertSuccess("rebuild after reverting the model edit");
                Assert.DoesNotContain("Skipping target \"_HeddleCompile\"", reverted.Output);
                Assert.Equal(artifactBefore, MsBuildFixture.Sha256File(MsBuildFixture.Artifact(fixture.Root)));
                Assert.Single(ModelDirectories(fixture));
            }
        }

        /// <summary>The intermediate image is content-addressed, so a build that changed none of its inputs
        /// has nothing to compile. Pins two regressions at once: the pass ran the compiler on every build, and
        /// the digest that would let it skip covered sources, references and analyzers only — so skipping on
        /// it alone would have served a stale image after a changed define or signing key.</summary>
        [Fact]
        public void IntermediateCompileRunsOnlyWhenOneOfItsInputsChanged()
        {
            using (var fixture = new MsBuildFixture())
            {
                // Switched from inside the project: a global define or key-file property would flow into
                // the referenced engine project as well.
                string project = WriteLibrary(fixture,
                    "    <DefineConstants Condition=\"'$(WithSymbol)' == 'true'\">$(DefineConstants);EXTRA_SYMBOL</DefineConstants>\n"
                    + "    <SignAssembly Condition=\"'$(WithKey)' == 'true'\">true</SignAssembly>\n"
                    + "    <AssemblyOriginatorKeyFile Condition=\"'$(WithKey)' == 'true'\">test.snk</AssemblyOriginatorKeyFile>\n");
                File.Copy(Path.Combine(MsBuildFixture.RepoRoot, "heddle.snk"), Path.Combine(fixture.Root, "test.snk"));

                fixture.Build(project).AssertSuccess("baseline build");
                string first = Assert.Single(ModelDirectories(fixture));
                string image = Path.Combine(first, "app.dll");
                var written = File.GetLastWriteTimeUtc(image);

                System.Threading.Thread.Sleep(1100);
                File.SetLastWriteTimeUtc(Path.Combine(fixture.Root, "Models.cs"), System.DateTime.UtcNow);
                fixture.Build(project).AssertSuccess("no-op rebuild");
                Assert.Equal(first, Assert.Single(ModelDirectories(fixture)));
                Assert.Equal(written, File.GetLastWriteTimeUtc(image));

                fixture.Build(project, "/p:WithSymbol=true").AssertSuccess("rebuild with a define");
                string defined = Assert.Single(ModelDirectories(fixture));
                Assert.NotEqual(first, defined);

                fixture.Build(project, "/p:WithSymbol=true",
                    "/p:WithKey=true").AssertSuccess("rebuild with a key");
                string signed = Assert.Single(ModelDirectories(fixture));
                Assert.NotEqual(defined, signed);
                Assert.NotEmpty(System.Reflection.AssemblyName.GetAssemblyName(Path.Combine(signed, "app.dll"))
                    .GetPublicKeyToken());

                fixture.Write(".editorconfig",
                    string.Join("\n", "root = true", "[*.cs]", "dotnet_diagnostic.CS0168.severity = none", ""));
                fixture.Build(project, "/p:WithSymbol=true",
                    "/p:WithKey=true").AssertSuccess("rebuild with analyzer configuration");
                Assert.NotEqual(signed, Assert.Single(ModelDirectories(fixture)));
            }
        }

        /// <summary>The digest has to read what it claims to cover. Pins the regression where the task resolved
        /// the project's source paths against the template root: with a root other than the project directory
        /// every read failed, every source hashed to the same placeholder, and — once an existing image was
        /// reused — a model edit kept serving the image, and the artifact, built before it.</summary>
        [Fact]
        public void ModelEditIsSeenUnderATemplateRootThatIsNotTheProjectDirectory()
        {
            using (var fixture = new MsBuildFixture())
            {
                // Keys are measured from the root, so the entry class is Order, not Templates_Order.
                string models = Models.Replace("Templates_Order", "Order");
                fixture.Write("Models.cs", models);
                string project = WriteLibraryProject(fixture,
                    "    <HeddleTemplateRoot>$(MSBuildProjectDirectory)/templates</HeddleTemplateRoot>\n");
                fixture.Write("templates/order.heddle", "@model(){{LibApp.Order}}\nOrder: @(Name) @(Placed.Year)\n");

                var first = fixture.Build(project);
                first.AssertSuccess("baseline build");
                Assert.DoesNotContain("HED7036", first.Output);
                string digestBefore = Assert.Single(ModelDirectories(fixture));
                string artifactBefore = MsBuildFixture.Sha256File(MsBuildFixture.Artifact(fixture.Root));

                System.Threading.Thread.Sleep(1100);
                fixture.Write("Models.cs", models.Replace("public DateTime Placed", "public DateTimeOffset Placed"));
                fixture.Build(project).AssertSuccess("rebuild after a model edit");
                Assert.NotEqual(digestBefore, Assert.Single(ModelDirectories(fixture)));
                Assert.NotEqual(artifactBefore, MsBuildFixture.Sha256File(MsBuildFixture.Artifact(fixture.Root)));
            }
        }

        /// <summary>An image is reused only when the pass that wrote it finished and it still reads as an
        /// assembly. Pins the regression where any file at the image's path counted: an interrupted compile
        /// left a truncated image that every later build tried, and failed, to load.</summary>
        [Fact]
        public void ADamagedIntermediateImageIsRebuiltNotReused()
        {
            using (var fixture = new MsBuildFixture())
            {
                string project = WriteLibrary(fixture);
                fixture.Build(project).AssertSuccess("baseline build");
                string image = Path.Combine(Assert.Single(ModelDirectories(fixture)), "app.dll");
                long intact = new FileInfo(image).Length;

                File.WriteAllBytes(image, new byte[] { 0x4D, 0x5A, 0x00 });
                File.Delete(MsBuildFixture.Stamp(fixture.Root));
                var rebuilt = fixture.Build(project);
                rebuilt.AssertSuccess("rebuild over a truncated image");
                Assert.DoesNotContain("HED7036", rebuilt.Output);
                Assert.Equal(intact, new FileInfo(image).Length);
            }
        }

        /// <summary>A source generator rebuilt in place — a project-referenced analyzer — changes what the
        /// intermediate pass produces without changing its path. Pins the omission where analyzers entered
        /// the digest by path alone.</summary>
        [Fact]
        public void AnAnalyzerRebuiltAtTheSamePathMovesTheDigest()
        {
            using (var fixture = new MsBuildFixture())
            {
                // Not a loadable analyzer: both compiles only warn about it, which is all this needs.
                fixture.Write("gen/Fake.Generator.dll", "first build of the generator");
                string project = fixture.Write("app.csproj",
                    MsBuildFixture.ProjectXml("net10.0",
                        "    <ImplicitUsings>enable</ImplicitUsings>\n    <HeddleOutputProfile>Text</HeddleOutputProfile>\n",
                        "<HeddleTemplate Include=\"templates/order.heddle\" />\n    <Analyzer Include=\"gen/Fake.Generator.dll\" />\n"));
                fixture.Write("templates/order.heddle", "@model(){{LibApp.Order}}\nOrder: @(Name)\n");
                fixture.Write("Models.cs", Models);

                fixture.Build(project).AssertSuccess("baseline build");
                string before = Assert.Single(ModelDirectories(fixture));

                fixture.Write("gen/Fake.Generator.dll", "second build of the generator, same path");
                fixture.Build(project).AssertSuccess("rebuild after the analyzer changed in place");
                Assert.NotEqual(before, Assert.Single(ModelDirectories(fixture)));
            }
        }

        private static string[] ModelDirectories(MsBuildFixture fixture)
        {
            string[] roots = Directory.GetDirectories(Path.Combine(fixture.Root, "obj"), "models",
                SearchOption.AllDirectories);
            Assert.Single(roots);
            return Directory.GetDirectories(roots[0]);
        }
    }
}
