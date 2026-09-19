using System.IO;
using Xunit;

namespace Heddle.Build.Tests
{
    /// <summary>Class-library fixtures whose model type comes from a NuGet package: the package's
    /// runtime image is in the implementation set, so the typed row precompiles with no intermediate
    /// compile and no executable output.</summary>
    public class ClassLibraryImageTests
    {
        [Fact]
        public void PackageModelPrecompilesInNetstandard20AndNet8Libraries()
        {
            using (var fixture = new MsBuildFixture())
            {
                fixture.Write("templates/j.heddle", "@model(){{Newtonsoft.Json.Linq.JObject}}\nTokens: @(Count)\n");
                string project = fixture.Write("app.csproj",
                    MsBuildFixture.ProjectXml("netstandard2.0;net8.0", string.Empty,
                        "<PackageReference Include=\"Newtonsoft.Json\" Version=\"13.0.3\" />\n"
                        + "    <HeddleTemplate Include=\"templates/j.heddle\" />\n"));

                fixture.Build(project).AssertSuccess("class-library build");
                foreach (string tfm in new[] { "netstandard2.0", "net8.0" })
                {
                    string source = Path.Combine(fixture.Root, "obj", MsBuildFixture.Configuration, tfm,
                        "heddle", "Heddle.CompiledForm.g.cs");
                    Assert.True(File.Exists(source), tfm + " generated source missing.");
                    Assert.Contains("Newtonsoft.Json.Linq.JObject", File.ReadAllText(source));
                }
            }
        }
    }
}
