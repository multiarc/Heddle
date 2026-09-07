using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace Heddle.Build.Tests
{
    /// <summary>The version lock and the image load through real MSBuild: a foreign engine reference
    /// fails with a positioned <c>HED7035</c>, and an unloadable image with a positioned
    /// <c>HED7036</c>.</summary>
    public class VersionLockTests
    {
        [Fact]
        public void MismatchedEngineReferenceIsPositionedHED7035()
        {
            using (var fixture = new MsBuildFixture())
            {
                fixture.Write("fake/fake.csproj",
                    "<Project Sdk=\"Microsoft.NET.Sdk\">\n  <PropertyGroup>\n    <TargetFramework>net10.0</TargetFramework>\n"
                    + "    <AssemblyName>Heddle</AssemblyName>\n    <AssemblyVersion>1.0.0.0</AssemblyVersion>\n  </PropertyGroup>\n</Project>\n");
                fixture.Write("fake/X.cs", "class X { }\n");
                string fake = Path.Combine(fixture.Root, "fake", "fake.csproj");
                fixture.Dotnet("build \"" + fake + "\" -c " + MsBuildFixture.Configuration + " -m:1 -v:m").AssertSuccess("fake engine build");
                string fakeDll = Path.Combine(fixture.Root, "fake", "bin", MsBuildFixture.Configuration, "net10.0", "Heddle.dll");
                Assert.True(File.Exists(fakeDll), "fake engine missing at " + fakeDll);

                fixture.Write("templates/hello.heddle", "Hello, @(Name)!\n");
                string project = fixture.Write("app.csproj",
                    MsBuildFixture.ProjectXml("net10.0", string.Empty,
                        "<Reference Include=\"Heddle\"><HintPath>" + fakeDll.Replace('\\', '/')
                        + "</HintPath></Reference>\n    <HeddleTemplate Include=\"templates/hello.heddle\" />\n",
                        false));

                var result = fixture.Build(project);
                Assert.True(result.Exit != 0, "expected the build to fail:\n" + result.Output);
                Assert.Matches(@"app\.csproj\(1,1\): error HED7035", result.Output);
            }
        }

        [Fact]
        public void UnloadableImageIsPositionedHED7036()
        {
            using (var fixture = new MsBuildFixture())
            {
                fixture.Write("templates/hello.heddle", "Hello, @(Name)!\n");
                fixture.Write("garbage.dll", "NOT A DLL AT ALL\n");
                string project = fixture.Write("app.csproj",
                    MsBuildFixture.ProjectXml("net10.0", string.Empty,
                        "<HeddleTemplate Include=\"templates/hello.heddle\" />\n"
                        + "    <HeddleModelAssembly Include=\"garbage.dll\" />\n"));

                var result = fixture.Build(project);
                Assert.True(result.Exit != 0, "expected the build to fail:\n" + result.Output);
                Assert.Matches(@"app\.csproj\(1,1\): error HED7036", result.Output);
            }
        }
    }
}
