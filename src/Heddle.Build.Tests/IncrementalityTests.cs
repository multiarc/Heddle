using System.IO;
using Xunit;

namespace Heddle.Build.Tests
{
    /// <summary>Incrementality through real MSBuild: an unchanged rebuild never reaches the host, a
    /// rebuilt model reference with an unchanged identity leaves the artifact alone, a changed
    /// identity recompiles, and every input the artifact's bytes depend on is declared — the item set
    /// itself and the libraries an <c>@&lt;&lt;</c> import reaches off disk included. A
    /// <c>Dummy.cs</c> forces <c>CoreCompile</c> to run so the skipped <c>_HeddleCompile</c> logs its
    /// up-to-date line instead of never being visited.</summary>
    public class IncrementalityTests
    {
        [Fact]
        public void UnchangedRebuildSkipsHeddleCompile()
        {
            using (var fixture = new MsBuildFixture())
            {
                fixture.Write("templates/hello.heddle", "Hello, @(Name)!\n");
                fixture.Write("Dummy.cs", "class Dummy { }\n");
                string project = fixture.Write("app.csproj",
                    MsBuildFixture.ProjectXml("net10.0", string.Empty,
                        "<HeddleTemplate Include=\"templates/hello.heddle\" />\n"));

                fixture.Build(project).AssertSuccess("baseline build");
                string before = MsBuildFixture.Sha256File(MsBuildFixture.Artifact(fixture.Root));

                System.Threading.Thread.Sleep(1100);
                File.SetLastWriteTimeUtc(Path.Combine(fixture.Root, "Dummy.cs"), System.DateTime.UtcNow);
                string binlog = Path.Combine(fixture.Root, "rebuild.binlog");
                var rebuild = fixture.BuildWithBinlog(project, binlog);
                rebuild.AssertSuccess("rebuild");
                Assert.Contains("Skipping target \"_HeddleCompile\" because all output files are up-to-date", rebuild.Output);
                // P2-R11: the same fact asserted on the binary log, replayed rather than read off the console.
                Assert.True(File.Exists(binlog), "the rebuild wrote no binary log at " + binlog);
                var replay = fixture.ReplayBinlog(binlog);
                replay.AssertSuccess("binlog replay");
                Assert.Contains("Skipping target \"_HeddleCompile\" because all output files are up-to-date", replay.Output);
                Assert.Equal(before, MsBuildFixture.Sha256File(MsBuildFixture.Artifact(fixture.Root)));
            }
        }

        /// <summary>A template removed from a glob leaves the item set, and MSBuild's newest-input check
        /// only ever sees the items still there — so every remaining input stays older than the outputs
        /// it no longer describes. Pins the regression where the rebuild was skipped and the deleted
        /// template's row and wrapper kept shipping, while the rewritten response file already listed
        /// only the survivor.</summary>
        [Fact]
        public void DeletedTemplateLeavesTheArtifactAndTheGeneratedSource()
        {
            using (var fixture = new MsBuildFixture())
            {
                fixture.Write("templates/alpha.heddle", "Alpha, @(Name)!\n");
                fixture.Write("templates/beta.heddle", "Beta, @(Name)!\n");
                fixture.Write("Dummy.cs", "class Dummy { }\n");
                string project = fixture.Write("app.csproj",
                    MsBuildFixture.ProjectXml("net10.0", string.Empty,
                        "<HeddleTemplate Include=\"templates/**/*.heddle\" />\n"));

                fixture.Build(project).AssertSuccess("baseline build");
                Assert.Contains("Templates_Beta", File.ReadAllText(MsBuildFixture.GeneratedSource(fixture.Root)));

                System.Threading.Thread.Sleep(1100);
                File.Delete(Path.Combine(fixture.Root, "templates", "beta.heddle"));
                var rebuild = fixture.Build(project);
                rebuild.AssertSuccess("rebuild after the delete");
                Assert.DoesNotContain("Skipping target \"_HeddleCompile\"", rebuild.Output);
                Assert.DoesNotContain("Templates_Beta",
                    File.ReadAllText(MsBuildFixture.GeneratedSource(fixture.Root)));
                var artifact = Heddle.Precompiled.CompiledForm.CompiledFormReader.Read(
                    File.ReadAllBytes(MsBuildFixture.Artifact(fixture.Root)));
                Assert.Single(artifact.Templates);
                Assert.Equal("templates/alpha.heddle", artifact.Templates[0].Key);
            }
        }

        /// <summary>A rename keeps the file's timestamp, so a template older than the artifact changes
        /// its key without moving anything MSBuild compares. Pins the regression where the old wrapper
        /// kept shipping and the new one never appeared.</summary>
        [Fact]
        public void RenamedTemplateWithAnUnchangedTimestampRecompiles()
        {
            using (var fixture = new MsBuildFixture())
            {
                fixture.Write("templates/alpha.heddle", "Alpha, @(Name)!\n");
                string named = fixture.Write("templates/oldname.heddle", "Named, @(Name)!\n");
                fixture.Write("Dummy.cs", "class Dummy { }\n");
                var frozen = new System.DateTime(2020, 1, 1, 0, 0, 0, System.DateTimeKind.Utc);
                File.SetLastWriteTimeUtc(named, frozen);
                string project = fixture.Write("app.csproj",
                    MsBuildFixture.ProjectXml("net10.0", string.Empty,
                        "<HeddleTemplate Include=\"templates/**/*.heddle\" />\n"));

                fixture.Build(project).AssertSuccess("baseline build");
                Assert.Contains("Templates_Oldname", File.ReadAllText(MsBuildFixture.GeneratedSource(fixture.Root)));

                System.Threading.Thread.Sleep(1100);
                string renamed = Path.Combine(fixture.Root, "templates", "newname.heddle");
                File.Move(named, renamed);
                File.SetLastWriteTimeUtc(renamed, frozen);
                var rebuild = fixture.Build(project);
                rebuild.AssertSuccess("rebuild after the rename");
                Assert.DoesNotContain("Skipping target \"_HeddleCompile\"", rebuild.Output);
                string source = File.ReadAllText(MsBuildFixture.GeneratedSource(fixture.Root));
                Assert.Contains("Templates_Newname", source);
                Assert.DoesNotContain("Templates_Oldname", source);
            }
        }

        /// <summary>A library an <c>@&lt;&lt;</c> import reaches through the disk fallback — here outside
        /// the project directory, so outside any template glob — is compiled into the importing template
        /// while no item declares it. Pins both halves of the regression: the target skipped the compile
        /// outright, and forcing it to run wrote nothing because the host's stamp covered the importer's
        /// content and not the import's. The closing leg pins the other side of the contract, that
        /// declaring the library costs no churn on a rebuild that changed nothing.</summary>
        [Fact]
        public void EditedImportOutsideTheItemSetRecompiles()
        {
            using (var fixture = new MsBuildFixture())
            {
                fixture.Write("shared/lib.heddle", "@%\n<libline>\n{{version one}}\n%@\n");
                fixture.Write("app/templates/page.heddle", "@<<{{../shared/lib.heddle}}\nPage: @libline()\n");
                fixture.Write("app/Dummy.cs", "class Dummy { }\n");
                string project = fixture.Write("app/app.csproj",
                    MsBuildFixture.ProjectXml("net10.0", string.Empty,
                        "<HeddleTemplate Include=\"templates/**/*.heddle\" />\n"));

                fixture.Build(project).AssertSuccess("baseline build");
                string before = MsBuildFixture.Sha256File(MsBuildFixture.Artifact(fixture.Root));
                string stampBefore = File.ReadAllText(MsBuildFixture.Stamp(fixture.Root));
                Assert.Contains("lib.heddle", File.ReadAllText(MsBuildFixture.DiskImports(fixture.Root)));

                System.Threading.Thread.Sleep(1100);
                fixture.Write("shared/lib.heddle", "@%\n<libline>\n{{version two}}\n%@\n");
                var rebuild = fixture.Build(project);
                rebuild.AssertSuccess("rebuild after editing the import");
                Assert.DoesNotContain("Skipping target \"_HeddleCompile\"", rebuild.Output);
                Assert.True(before != MsBuildFixture.Sha256File(MsBuildFixture.Artifact(fixture.Root)),
                    "editing a library reached only through the import disk fallback left the artifact byte-identical.");
                Assert.True(stampBefore != File.ReadAllText(MsBuildFixture.Stamp(fixture.Root)),
                    "editing the import left the host's stamp identical, so the host wrote nothing.");

                System.Threading.Thread.Sleep(1100);
                File.SetLastWriteTimeUtc(Path.Combine(fixture.Root, "app", "Dummy.cs"), System.DateTime.UtcNow);
                var unchanged = fixture.Build(project);
                unchanged.AssertSuccess("unchanged rebuild");
                Assert.Contains("Skipping target \"_HeddleCompile\" because all output files are up-to-date",
                    unchanged.Output);
            }
        }

        /// <summary>The template root decides every key the artifact records, and so every generated
        /// class name — while the item rows the host stamps carry absolute paths, so nothing else in
        /// the digest moves with it. Pins the regression where the targets ran the compile on the
        /// changed root and the host's own stamp threw the work away: an incremental build then
        /// disagreed with a clean one over byte-identical sources.</summary>
        [Fact]
        public void ChangedTemplateRootRecompiles()
        {
            using (var fixture = new MsBuildFixture())
            {
                fixture.Write("templates/alpha.heddle", "Alpha, @(Name)!\n");
                fixture.Write("Dummy.cs", "class Dummy { }\n");
                const string Item = "<HeddleTemplate Include=\"templates/alpha.heddle\" />\n";
                string project = fixture.Write("app.csproj",
                    MsBuildFixture.ProjectXml("net10.0", string.Empty, Item));

                fixture.Build(project).AssertSuccess("baseline build");
                Assert.Contains("class Templates_Alpha",
                    File.ReadAllText(MsBuildFixture.GeneratedSource(fixture.Root)));

                System.Threading.Thread.Sleep(1100);
                string root = Path.Combine(fixture.Root, "templates").Replace('\\', '/');
                fixture.Write("app.csproj", MsBuildFixture.ProjectXml("net10.0",
                    "    <HeddleTemplateRoot>" + root + "</HeddleTemplateRoot>\n", Item));
                var rebuild = fixture.Build(project);
                rebuild.AssertSuccess("rebuild with a changed template root");
                string source = File.ReadAllText(MsBuildFixture.GeneratedSource(fixture.Root));
                Assert.DoesNotContain("class Templates_Alpha", source);
                Assert.Contains("class Alpha", source);
                var artifact = Heddle.Precompiled.CompiledForm.CompiledFormReader.Read(
                    File.ReadAllBytes(MsBuildFixture.Artifact(fixture.Root)));
                Assert.Equal("alpha.heddle", artifact.Templates[0].Key);
            }
        }

        /// <summary>An option changed on the command line touches no project file; the options
        /// fingerprint file makes it a compile input, and an unchanged repeat is still up to date.</summary>
        [Fact]
        public void OptionChangedOnTheCommandLineRecompiles()
        {
            using (var fixture = new MsBuildFixture())
            {
                fixture.Write("templates/hello.heddle", "Hello, @(Name)!\n");
                fixture.Write("Dummy.cs", "class Dummy { }\n");
                string project = fixture.Write("app.csproj",
                    MsBuildFixture.ProjectXml("net10.0", string.Empty,
                        "<HeddleTemplate Include=\"templates/hello.heddle\" />\n"));

                fixture.Build(project).AssertSuccess("baseline build");
                string before = MsBuildFixture.Sha256File(MsBuildFixture.Artifact(fixture.Root));

                var changed = fixture.Build(project, "/p:HeddleOutputProfile=Text");
                changed.AssertSuccess("rebuild with a changed option");
                Assert.DoesNotContain("Skipping target \"_HeddleCompile\"", changed.Output);
                Assert.NotEqual(before, MsBuildFixture.Sha256File(MsBuildFixture.Artifact(fixture.Root)));

                System.Threading.Thread.Sleep(1100);
                File.SetLastWriteTimeUtc(Path.Combine(fixture.Root, "Dummy.cs"), System.DateTime.UtcNow);
                var same = fixture.Build(project, "/p:HeddleOutputProfile=Text");
                same.AssertSuccess("rebuild with the same option");
                Assert.Contains("Skipping target \"_HeddleCompile\" because all output files are up-to-date", same.Output);
            }
        }

        [Fact]
        public void RebuiltModelWithSameIdentityKeepsTheArtifact()
        {
            using (var fixture = new MsBuildFixture())
            {
                string app = WriteModelConsumer(fixture);

                fixture.Build(app).AssertSuccess("baseline build");
                string before = MsBuildFixture.Sha256File(MsBuildFixture.Artifact(fixture.Root));

                // Rebuild the model with no content change: a forced full rebuild keeps the MVID
                // (deterministic build) while refreshing the image timestamp, so MSBuild reruns the
                // target but the host finds the stamp unchanged and writes nothing.
                string models = Path.Combine(fixture.Root, "models", "models.csproj");
                fixture.Dotnet("build \"" + models + "\" -c " + MsBuildFixture.Configuration + " -m:1 -v:m --no-incremental").AssertSuccess("models rebuild");
                string stampBefore = File.ReadAllText(MsBuildFixture.Stamp(fixture.Root));
                var rebuild = fixture.Build(app);
                rebuild.AssertSuccess("consumer rebuild");
                Assert.DoesNotContain("Skipping target \"_HeddleCompile\"", rebuild.Output);
                Assert.Equal(stampBefore, File.ReadAllText(MsBuildFixture.Stamp(fixture.Root)));
                Assert.Equal(before, MsBuildFixture.Sha256File(MsBuildFixture.Artifact(fixture.Root)));
            }
        }

        [Fact]
        public void RebuiltModelWithChangedIdentityRecompiles()
        {
            using (var fixture = new MsBuildFixture())
            {
                string app = WriteModelConsumer(fixture);

                fixture.Build(app).AssertSuccess("baseline build");

                File.AppendAllText(Path.Combine(fixture.Root, "models", "Widget.cs"),
                    "namespace Models { public partial class Widget { public int Extra { get; set; } } }\n");
                string stampBefore = File.ReadAllText(MsBuildFixture.Stamp(fixture.Root));
                var rebuild = fixture.Build(app);
                rebuild.AssertSuccess("consumer rebuild");
                Assert.DoesNotContain("Skipping target \"_HeddleCompile\"", rebuild.Output);
                Assert.True(stampBefore != File.ReadAllText(MsBuildFixture.Stamp(fixture.Root)),
                    "changed model identity left the stamp identical.");
            }
        }

        private static string WriteModelConsumer(MsBuildFixture fixture)
        {
            fixture.Write("models/models.csproj",
                "<Project Sdk=\"Microsoft.NET.Sdk\">\n  <PropertyGroup>\n    <TargetFramework>net10.0</TargetFramework>\n"
                + "    <Nullable>disable</Nullable>\n    <LangVersion>latest</LangVersion>\n  </PropertyGroup>\n</Project>\n");
            fixture.Write("models/Widget.cs", "namespace Models { public partial class Widget { public string Label { get; set; } } }\n");
            fixture.Write("templates/widget.heddle", "@model(){{Models.Widget}}\nLabel: @(Label)\n");
            return fixture.Write("app.csproj",
                MsBuildFixture.ProjectXml("net10.0", string.Empty,
                    "<ProjectReference Include=\"models/models.csproj\" />\n"
                    + "    <HeddleTemplate Include=\"templates/widget.heddle\" />\n"
                    + "    <Compile Remove=\"models/**/*.cs\" />\n"));
        }
    }
}
