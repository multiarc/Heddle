using System.IO;
using Xunit;

namespace Heddle.Build.Tests
{
    /// <summary>One package across target frameworks: <c>net8.0</c> and <c>net10.0</c> always, plus a
    /// <c>net48</c> leg that builds when the .NET Framework reference assemblies restore and reports
    /// itself skipped otherwise (Linux has no targeting pack in-box).</summary>
    public class MultiTargetTests
    {
        private readonly ITestOutputHelper _output;

        public MultiTargetTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void Net8AndNet10BuildWithOnePackage()
        {
            using (var fixture = new MsBuildFixture())
            {
                string project = WritePackageFixture(fixture, "net8.0;net10.0");
                fixture.Build(project).AssertSuccess("net8.0;net10.0 build");
                Assert.True(File.Exists(ArtifactFor(fixture.Root, "net8.0")), "net8.0 artifact missing.");
                Assert.True(File.Exists(ArtifactFor(fixture.Root, "net10.0")), "net10.0 artifact missing.");
            }
        }

        [Fact]
        public void Net48BuildsWithOnePackageWhenReferenceAssembliesRestore()
        {
            using (var fixture = new MsBuildFixture())
            {
                string project = WritePackageFixture(fixture, "net48");
                var restore = fixture.Dotnet("restore \"" + project + "\" -v:m");
                if (restore.Exit != 0 && restore.Output.Contains("NETFramework.ReferenceAssemblies"))
                {
                    _output.WriteLine("net48 leg skipped: Microsoft.NETFramework.ReferenceAssemblies is absent: "
                        + FirstLine(restore.Output, "NETFramework"));
                    return;
                }

                restore.AssertSuccess("net48 restore");
                fixture.Build(project).AssertSuccess("net48 build");
                Assert.True(File.Exists(ArtifactFor(fixture.Root, "net48")), "net48 artifact missing.");
            }
        }

        private static string WritePackageFixture(MsBuildFixture fixture, string tfms)
        {
            fixture.Write("templates/hello.heddle", "Hello, @(Name)!\n");
            return fixture.Write("app.csproj",
                MsBuildFixture.ProjectXml(tfms, string.Empty,
                    "<PackageReference Include=\"Newtonsoft.Json\" Version=\"13.0.3\" />\n"
                    + "    <HeddleTemplate Include=\"templates/hello.heddle\" />\n"));
        }

        private static string ArtifactFor(string root, string tfm)
        {
            return Path.Combine(root, "obj", MsBuildFixture.Configuration, tfm, "heddle", "Heddle.CompiledForm.bin");
        }

        private static string FirstLine(string output, string token)
        {
            foreach (string line in output.Split('\n'))
            {
                if (line.Contains(token))
                    return line.Trim();
            }

            return token;
        }
    }
}
