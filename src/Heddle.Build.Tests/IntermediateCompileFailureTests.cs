using System;
using System.IO;
using Xunit;

namespace Heddle.Build.Tests
{
    public class IntermediateCompileFailureTests
    {
        private const string Models =
            "namespace LibApp { public class Order { public string Name { get; set; } } }\n";

        private static string WriteProject(MsBuildFixture fixture, string extraItems)
        {
            fixture.Write("templates/order.heddle", "@model(){{LibApp.Order}}\nOrder: @(Name)\n");
            fixture.Write("Models.cs", Models);
            return fixture.Write("app.csproj",
                MsBuildFixture.ProjectXml("net10.0", "    <HeddleOutputProfile>Text</HeddleOutputProfile>\n",
                    "<HeddleTemplate Include=\"templates/order.heddle\" />\n" + extraItems));
        }

        /// <summary>The intermediate compile is a compiler run the project never asked for, from a target it
        /// cannot see. When it fails the build still fails — but the compiler's errors are followed by one
        /// Heddle error saying whose pass that was, that the project's own compile was not reached, and what
        /// the author can do. Pins the omission where raw CS errors appeared with nothing to attribute them.</summary>
        [Fact]
        public void AFailedIntermediateCompileIsAttributedToHeddle()
        {
            using (var fixture = new MsBuildFixture())
            {
                string project = WriteProject(fixture, string.Empty);
                fixture.Write("Broken.cs", "namespace LibApp { public class Broken { public NoSuchType Value; } }\n");

                var build = fixture.Build(project);
                Assert.NotEqual(0, build.Exit);
                int compilerError = build.Output.IndexOf("CS0246", StringComparison.Ordinal);
                int attribution = build.Output.IndexOf("HED7038", StringComparison.Ordinal);
                Assert.True(compilerError >= 0, "the compiler's own error is missing:\n" + build.Output);
                Assert.True(attribution > compilerError,
                    "the attribution has to follow the compiler's own errors:\n" + build.Output);
                Assert.Contains("Precompile=\"false\"", build.Output);
                Assert.Contains("was not reached", build.Output);

                // Nothing of the template compile ran, and the project's own compile was never reached.
                Assert.Empty(Directory.GetFiles(fixture.Root, "Heddle.CompiledForm.bin", SearchOption.AllDirectories));
                Assert.False(Directory.Exists(Path.Combine(fixture.Root, "bin")) &&
                    Directory.GetFiles(Path.Combine(fixture.Root, "bin"), "app.dll", SearchOption.AllDirectories).Length != 0,
                    "the project's own compile ran.");
            }
        }

        /// <summary>The documented way out: a template opted out of precompilation no longer needs the
        /// same-project model at build time, so the pass does not run and the project's own errors are the
        /// only ones shown.</summary>
        [Fact]
        public void OptingTheTemplateOutSkipsThePassAndLeavesOnlyTheProjectsOwnErrors()
        {
            using (var fixture = new MsBuildFixture())
            {
                string project = WriteProject(fixture, string.Empty);
                File.WriteAllText(project, File.ReadAllText(project).Replace(
                    "<HeddleTemplate Include=\"templates/order.heddle\" />",
                    "<HeddleTemplate Include=\"templates/order.heddle\" Precompile=\"false\" />"));
                fixture.Write("Broken.cs", "namespace LibApp { public class Broken { public NoSuchType Value; } }\n");

                var build = fixture.Build(project);
                Assert.NotEqual(0, build.Exit);
                Assert.Contains("CS0246", build.Output);
                Assert.DoesNotContain("HED7038", build.Output);
            }
        }

        /// <summary>A digest input that cannot be read is never hashed to a placeholder — every such input would
        /// look unchanged for ever, and an image built before it went missing would be served. It makes the
        /// digest unique instead, so the pass runs, and the compiler says what is wrong with the input.</summary>
        [Fact]
        public void AnUnreadableDigestInputNeverReusesAnImage()
        {
            using (var fixture = new MsBuildFixture())
            {
                fixture.Write("gen/Fake.Generator.dll", "not a loadable analyzer; both compiles only warn");
                string project = WriteProject(fixture, "    <Analyzer Include=\"gen/Fake.Generator.dll\" />\n");
                fixture.Build(project).AssertSuccess("baseline build");
                string models = Assert.Single(Directory.GetDirectories(Path.Combine(fixture.Root, "obj"), "models",
                    SearchOption.AllDirectories));
                string intact = Assert.Single(Directory.GetDirectories(models));

                File.Delete(Path.Combine(fixture.Root, "gen", "Fake.Generator.dll"));
                var first = fixture.Build(project);
                Assert.NotEqual(0, first.Exit);
                Assert.Contains("could not be read for the intermediate model digest", first.Output);
                Assert.Contains("HED7038", first.Output);
                string afterFirst = Assert.Single(Directory.GetDirectories(models));
                Assert.NotEqual(intact, afterFirst);

                Assert.NotEqual(0, fixture.Build(project).Exit);
                Assert.NotEqual(afterFirst, Assert.Single(Directory.GetDirectories(models)));
            }
        }
    }
}
