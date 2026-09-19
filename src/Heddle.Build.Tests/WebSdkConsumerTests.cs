using System.IO;
using Xunit;

namespace Heddle.Build.Tests
{
    /// <summary>Web SDK consumers with a same-project model. The build host runs on the base runtime alone,
    /// and the intermediate compile is a second compiler invocation: both have to cope with what a web
    /// project brings — types whose base class the host cannot load, and sources that exist only because a
    /// generator read the project's additional files.</summary>
    public class WebSdkConsumerTests
    {
        private static string WebProject(string items)
        {
            return "<Project Sdk=\"Microsoft.NET.Sdk.Web\">\n  <PropertyGroup>\n    <TargetFramework>net10.0</TargetFramework>\n"
                + "    <EnableDefaultHeddleTemplates>false</EnableDefaultHeddleTemplates>\n    <Nullable>disable</Nullable>\n"
                + "    <ImplicitUsings>enable</ImplicitUsings>\n    <HeddleOutputProfile>Text</HeddleOutputProfile>\n  </PropertyGroup>\n"
                + "  <ItemGroup>\n    <ProjectReference Include=\"" + MsBuildFixture.HeddleProject + "\" />\n" + items
                + "  </ItemGroup>\n"
                + "  <Import Project=\"" + MsBuildFixture.PropsPath + "\" />\n"
                + "  <Import Project=\"" + MsBuildFixture.TargetsPath + "\" />\n</Project>\n";
        }

        /// <summary>Pins the regression where one type the host cannot load — a controller, whose base class
        /// lives in the ASP.NET shared framework the host does not run on — made the engine drop every type
        /// of that assembly, so a short-name <c>@model</c> beside it did not resolve.</summary>
        [Fact]
        public void ShortNameModelResolvesBesideATypeTheHostCannotLoad()
        {
            using (var fixture = new MsBuildFixture())
            {
                fixture.Write("templates/mail.heddle", "@using(){{WebApp.Models}}\n@model(){{MailModel}}\nTo: @(To)\n");
                fixture.Write("Models.cs", "namespace WebApp.Models { public class MailModel { public string To { get; set; } } }\n");
                fixture.Write("HomeController.cs",
                    "namespace WebApp { public class HomeController : Microsoft.AspNetCore.Mvc.Controller { } }\n");
                fixture.Write("Program.cs", "var app = WebApplication.CreateBuilder(args).Build();\napp.Run();\n");
                string project = fixture.Write("app.csproj",
                    WebProject("    <HeddleTemplate Include=\"templates/mail.heddle\" />\n"));

                var build = fixture.Build(project);
                build.AssertSuccess("web project with a controller beside the model");
                Assert.DoesNotContain("HED7012", build.Output);
                Assert.Contains("global::WebApp.Models.MailModel",
                    File.ReadAllText(MsBuildFixture.GeneratedSource(fixture.Root)));
            }
        }

        /// <summary>Pins the regression where the intermediate compile ran the project's source generators
        /// without the project's additional files: the Razor generator produced no component classes, so a
        /// project that names a <c>.razor</c> component from C# failed the intermediate pass with CS0234
        /// although the real compile succeeds.</summary>
        [Fact]
        public void RazorComponentReferencedFromCodeSurvivesTheIntermediateCompile()
        {
            using (var fixture = new MsBuildFixture())
            {
                fixture.Write("templates/order.heddle", "@model(){{WebApp.Order}}\nOrder: @(Name)\n");
                fixture.Write("Models.cs", "namespace WebApp { public class Order { public string Name { get; set; } } }\n");
                fixture.Write("Components/Widget.razor", "<p>widget</p>\n");
                fixture.Write("Program.cs",
                    "var app = WebApplication.CreateBuilder(args).Build();\n"
                    + "System.Console.WriteLine(typeof(WebApp.Components.Widget).Name);\napp.Run();\n");
                string project = fixture.Write("WebApp.csproj",
                    WebProject("    <HeddleTemplate Include=\"templates/order.heddle\" />\n"));

                var build = fixture.Build(project);
                build.AssertSuccess("web project naming a razor component from code");
                Assert.DoesNotContain("CS0234", build.Output);
                Assert.Contains("global::WebApp.Order",
                    File.ReadAllText(MsBuildFixture.GeneratedSource(fixture.Root)));
            }
        }
    }
}
