using System.IO;
using System.Linq;
using Xunit;

namespace Heddle.Build.Tests
{
    /// <summary>A same-project model under <c>ImplicitUsings</c> with a <c>[GeneratedRegex]</c> source
    /// generator: the probe leaves the model unresolved, the intermediate compile (sources plus
    /// analyzer-driven generators) produces the content-addressed model image, and the host binds
    /// the typed row against it.</summary>
    public class IntermediateCompileTests
    {
        /// <summary>Embedded C# over a model the project itself declares — top-level and nested. Pins the
        /// regression where every such template failed the build, and failed it in silence ("heddle compile
        /// exited with code 1 without reporting a diagnostic"): the C# tier's emitted assembly could not
        /// reference the intermediate model image the host had loaded, so it met a second copy of the model
        /// type, and the fault that raised was kept on the compile result, which the host did not forward.</summary>
        [Fact]
        public void EmbeddedCSharpOverSameProjectModelsPrecompiles()
        {
            using (var fixture = new MsBuildFixture())
            {
                fixture.Write("templates/plain.heddle", "@model(){{CsApp.Plain}}\nFlat: @(@model.Value.ToUpper())\n");
                fixture.Write("templates/inner.heddle",
                    "@model(){{CsApp.Outer.Inner}}\nDeep: @(@model.Value.ToUpper())|@(@model.Value.Length + 1)\n");
                fixture.Write("Models.cs",
                    "namespace CsApp { public class Plain { public string Value { get; set; } } "
                    + "public class Outer { public class Inner { public string Value { get; set; } } } }\n");
                string project = fixture.Write("app.csproj",
                    MsBuildFixture.ProjectXml("net10.0",
                        "    <HeddleExpressionMode>FullCSharp</HeddleExpressionMode>\n    <HeddleOutputProfile>Text</HeddleOutputProfile>\n",
                        "<HeddleTemplate Include=\"templates/*.heddle\" />\n"));

                var build = fixture.Build(project);
                build.AssertSuccess("FullCSharp build over same-project models");
                Assert.DoesNotContain("without reporting a diagnostic", build.Output);
                Assert.DoesNotContain("HED7020", build.Output);
                string source = File.ReadAllText(MsBuildFixture.GeneratedSource(fixture.Root));
                Assert.Contains("Generate(global::CsApp.Plain model", source);
                Assert.Contains("Generate(global::CsApp.Outer.Inner model", source);
            }
        }

        /// <summary>The model a compiled page binds may be declared by a library it imports, and the library
        /// may itself be opted out of precompilation. Pins the regression where the probe read only the
        /// compiled items' own text: the same-project model went uncounted, the intermediate compile never
        /// ran, and the page failed on a type the project plainly declares.</summary>
        [Fact]
        public void AModelDeclaredByAnOptedOutImportedLibraryStillStartsTheIntermediateCompile()
        {
            using (var fixture = new MsBuildFixture())
            {
                fixture.Write("templates/lib.heddle", "@model(){{Shop.Holder}}\n@% <greet>{{Hi}} %@\n");
                fixture.Write("templates/page.heddle", "@<<{{templates/lib.heddle}}@greet()\n");
                fixture.Write("Models.cs", "namespace Shop { public class Holder { public string Name { get; set; } } }\n");
                string project = fixture.Write("app.csproj",
                    MsBuildFixture.ProjectXml("net10.0", "    <HeddleOutputProfile>Text</HeddleOutputProfile>\n",
                        "<HeddleTemplate Include=\"templates/page.heddle\" />\n"
                        + "<HeddleTemplate Include=\"templates/lib.heddle\" Precompile=\"false\" />\n"));

                var build = fixture.Build(project);
                build.AssertSuccess("page importing an opted-out library that declares the model");
                Assert.DoesNotContain("HED7012", build.Output);
            }
        }

        /// <summary>The notice that a template is not fully precompiled is the only thing that tells its author so
        /// before a strict or NativeAOT host refuses it. Pins that an ordinary build shows it: it was written as a
        /// low-importance message, which no verbosity short of detailed prints.</summary>
        [Fact]
        public void TheNotFullyPrecompiledNoticeIsShownByAnOrdinaryBuild()
        {
            using (var fixture = new MsBuildFixture())
            {
                fixture.Write("templates/ledger.heddle", "@model(){{Books.Ledger}}\nTotal: @(Total) of @(Total)\n");
                fixture.Write("templates/shelf.heddle", "@model(){{Books.Shelf}}\nShelf: @(Name)\n");
                fixture.Write("Models.cs", "namespace Books { internal class Ledger { public int Total { get; set; } } "
                    + "public class Shelf { public string Name { get; set; } } }\n");
                string project = fixture.Write("app.csproj",
                    MsBuildFixture.ProjectXml("net10.0", "    <HeddleOutputProfile>Text</HeddleOutputProfile>\n",
                        "<HeddleTemplate Include=\"templates/*.heddle\" />\n"));

                // A notice is a message: a build that turns warnings into errors is none the worse for it.
                var build = fixture.Build(project, "-v:m", "-warnaserror");
                build.AssertSuccess("build over an internal same-project model, warnings as errors");
                var notices = build.Output.Split('\n').Where(line => line.Contains("HED7031")).ToList();
                // One per template that is not fully precompiled, however many sites it has; none for one that is.
                Assert.True(notices.Count == 1, "expected one notice, found " + notices.Count + ":\n" + string.Join("\n", notices));
                Assert.Contains("ledger.heddle", notices[0]);
                Assert.Contains("message HED7031", notices[0]);
                Assert.Contains("2 sites rebuilt at load", notices[0]);
                Assert.Contains("non-public type 'Books.Ledger'", notices[0]);
                Assert.DoesNotContain("shelf.heddle", string.Join("\n", notices));
            }
        }

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
