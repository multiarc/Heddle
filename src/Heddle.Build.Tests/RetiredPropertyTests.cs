using Xunit;

namespace Heddle.Build.Tests
{
    /// <summary>The retired 2.x options through real MSBuild: each set property draws its own
    /// <c>HED7037</c> and the build still succeeds byte-identical — warnings, not errors. Set means
    /// set: a property present-but-empty is unset and stays silent, whether it arrives through the
    /// project file or the command line.</summary>
    public class RetiredPropertyTests
    {
        private static readonly string[] Retired =
        {
            "HeddleObserveEngine",
            "HeddleNodeFallback",
            "HeddleEmitUtf8Pieces",
            "HeddleObserveIntermediatePath",
            "HeddleObserveImplementationPath"
        };

        private static string PropertiesXml()
        {
            var xml = string.Empty;
            foreach (string name in Retired)
                xml += "    <" + name + ">true</" + name + ">\n";
            return xml;
        }

        [Fact]
        public void EachSetPropertyDrawsItsOwnHED7037()
        {
            using (var fixture = new MsBuildFixture())
            {
                fixture.Write("templates/hello.heddle", "Hello, @(Name)!\n");
                string project = fixture.Write("app.csproj",
                    MsBuildFixture.ProjectXml("net10.0", PropertiesXml(),
                        "<HeddleTemplate Include=\"templates/hello.heddle\" />\n"));

                var result = fixture.Build(project);
                result.AssertSuccess("retired properties warn, not error");
                foreach (string name in Retired)
                    Assert.Contains("warning HED7037", result.Output);
                foreach (string name in Retired)
                    Assert.Contains(name + " is retired and ignored", result.Output);
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
