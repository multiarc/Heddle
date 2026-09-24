using Xunit;

namespace Heddle.Build.Tests
{
    /// <summary>The retired 2.x options through real MSBuild (P4-R5): three of them set draw their own
    /// <c>HED7037</c> and the build still succeeds byte-identical — warnings, not errors — while the two
    /// observe-path properties retire silently. Set means set: a property present-but-empty is unset and
    /// stays silent, whether it arrives through the project file or the command line.</summary>
    public class RetiredPropertyTests
    {
        private static readonly string[] Warned =
        {
            "HeddleObserveEngine",
            "HeddleNodeFallback",
            "HeddleEmitUtf8Pieces"
        };

        private static readonly string[] Silent =
        {
            "HeddleObserveIntermediatePath",
            "HeddleObserveImplementationPath"
        };

        private static string PropertiesXml()
        {
            var xml = string.Empty;
            foreach (string name in Warned)
                xml += "    <" + name + ">true</" + name + ">\n";
            foreach (string name in Silent)
                xml += "    <" + name + ">true</" + name + ">\n";
            return xml;
        }

        [Fact]
        public void ThreePropertiesWarnAndTheTwoObservePathsStaySilent()
        {
            using (var fixture = new MsBuildFixture())
            {
                fixture.Write("templates/hello.heddle", "Hello, @(Name)!\n");
                string project = fixture.Write("app.csproj",
                    MsBuildFixture.ProjectXml("net10.0", PropertiesXml(),
                        "<HeddleTemplate Include=\"templates/hello.heddle\" />\n"));
                var result = fixture.Build(project);
                result.AssertSuccess("retired properties warn, not error");
                Assert.Contains("warning HED7037", result.Output);
                foreach (string name in Warned)
                    Assert.Contains(name + " is retired and ignored", result.Output);
                foreach (string name in Silent)
                    Assert.DoesNotContain(name + " is retired", result.Output);
                // Positioned at the project file: the warning line carries the project path before the id.
                bool positioned = false;
                foreach (string line in result.Output.Split('\n'))
                    if (line.Contains("warning HED7037") && line.Contains("app.csproj") &&
                        line.IndexOf("app.csproj", System.StringComparison.Ordinal) < line.IndexOf("warning HED7037", System.StringComparison.Ordinal))
                        positioned = true;
                Assert.True(positioned, "HED7037 should be positioned at the project file:\n" + result.Output);
            }
        }

        [Fact]
        public void CommandLinePropertiesStickPastEvaluation()
        {
            using (var fixture = new MsBuildFixture())
            {
                fixture.Write("templates/hello.heddle", "Hello, @(Name)!\n");
                string project = fixture.Write("app.csproj",
                    MsBuildFixture.ProjectXml("net10.0", string.Empty,
                        "<HeddleTemplate Include=\"templates/hello.heddle\" />\n"));
                var result = fixture.Build(project, "/p:HeddleObserveEngine=Auto");
                result.AssertSuccess("command-line retired property warns, not error");
                Assert.Contains("warning HED7037", result.Output);
                Assert.Contains("HeddleObserveEngine is retired and ignored", result.Output);
            }
        }

        [Fact]
        public void UnsetPropertiesStaySilent()
        {
            using (var fixture = new MsBuildFixture())
            {
                fixture.Write("templates/hello.heddle", "Hello, @(Name)!\n");
                string project = fixture.Write("app.csproj",
                    MsBuildFixture.ProjectXml("net10.0", string.Empty,
                        "<HeddleTemplate Include=\"templates/hello.heddle\" />\n"));
                var result = fixture.Build(project);
                result.AssertSuccess("clean build");
                Assert.DoesNotContain("HED7037", result.Output);
            }
        }
    }
}
