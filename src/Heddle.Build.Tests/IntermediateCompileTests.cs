using System.IO;
using Xunit;

namespace Heddle.Build.Tests
{
    /// <summary>A same-project model under <c>ImplicitUsings</c> with a <c>[GeneratedRegex]</c> source
    /// generator: the probe leaves the model unresolved, the intermediate compile (sources plus
    /// analyzer-driven generators) produces the content-addressed model image, and the host binds
    /// the typed row against it.</summary>
    public class IntermediateCompileTests
    {
        [Fact]
        public void SameProjectModelCompletesTheIntermediateCompile()
        {
            using (var fixture = new MsBuildFixture())
            {
                fixture.Write("templates/order.heddle", "@model(){{MidApp.Order}}\nOrder: @(Name)\n");
                fixture.Write("Program.cs",
                    "using System.Text.RegularExpressions;\n"
                    + "namespace MidApp;\n"
                    + "public class Order { public string Name { get; set; } }\n"
                    + "public partial class Shipper { [GeneratedRegex(\"A+\")] public static partial Regex Fast(); }\n"
                    + "public static class Program { public static void Main() => System.Console.WriteLine(Shipper.Fast().IsMatch(\"AAA\")); }\n");
                string project = fixture.Write("app.csproj",
                    MsBuildFixture.ProjectXml("net10.0",
                        "    <OutputType>Exe</OutputType>\n    <ImplicitUsings>enable</ImplicitUsings>\n",
                        "<HeddleTemplate Include=\"templates/order.heddle\" />\n"));

                fixture.Build(project).AssertSuccess("intermediate-compile build");

                string[] models = Directory.GetFiles(Path.Combine(fixture.Root, "obj"), "app.dll", SearchOption.AllDirectories);
                bool intermediate = false;
                foreach (string model in models)
                {
                    if (model.Replace('\\', '/').Contains("/heddle/models/"))
                        intermediate = true;
                }

                Assert.True(intermediate, "no content-addressed intermediate model under obj/.../heddle/models/.");
                string source = File.ReadAllText(MsBuildFixture.GeneratedSource(fixture.Root));
                Assert.Contains("global::MidApp.Order", source);
            }
        }
    }
}
