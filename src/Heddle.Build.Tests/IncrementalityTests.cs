using System.IO;
using Xunit;

namespace Heddle.Build.Tests
{
    /// <summary>Incrementality through real MSBuild: an unchanged rebuild never reaches the host, a
    /// rebuilt model reference with an unchanged identity leaves the artifact alone, and a changed
    /// identity recompiles. A <c>Dummy.cs</c> forces <c>CoreCompile</c> to run so the skipped
    /// <c>_HeddleCompile</c> logs its up-to-date line instead of never being visited.</summary>
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
