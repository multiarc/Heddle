using System.IO;
using Xunit;

namespace Heddle.Build.Tests
{
    /// <summary>A design-time build yields compiling stubs and no engine outputs, and the next real
    /// build finds <c>_HeddleCompile</c> out of date and compiles for real.</summary>
    public class DesignTimeBuildTests
    {
        [Fact]
        public void DesignTimeBuildYieldsStubsOnlyThenRealBuildCompiles()
        {
            using (var fixture = new MsBuildFixture())
            {
                fixture.Write("templates/hello.heddle", "Hello, @(Name)!\n");
                fixture.Write("templates/partial.heddle", "@% <greet>{{Hi}} %@\n");
                string project = fixture.Write("app.csproj",
                    MsBuildFixture.ProjectXml("net10.0", string.Empty,
                        "<HeddleTemplate Include=\"templates/hello.heddle\" />\n" +
                        "<HeddleTemplate Include=\"templates/partial.heddle\"><Precompile>false</Precompile></HeddleTemplate>\n"));

                var designTime = fixture.Build(project, "/p:DesignTimeBuild=true");
                designTime.AssertSuccess("design-time build");

                string[] stubs = Directory.GetFiles(fixture.Root, "Heddle.Generated.Stubs.g.cs", SearchOption.AllDirectories);
                Assert.True(stubs.Length == 1, "expected one stubs file, found " + stubs.Length + ".");
                string stubsText = File.ReadAllText(stubs[0]);
                Assert.Contains("public static class Templates_Hello", stubsText);
                Assert.Contains("throw new global::System.InvalidOperationException", stubsText);
                // The opted-out item gets no stub either: the IDE never sees an entry point the real
                // build omits.
                Assert.DoesNotContain("Templates_Partial", stubsText);
                Assert.Equal(0, Directory.GetFiles(fixture.Root, "Heddle.CompiledForm.bin", SearchOption.AllDirectories).Length);
                Assert.Equal(0, Directory.GetFiles(fixture.Root, "Heddle.CompiledForm.g.cs", SearchOption.AllDirectories).Length);
                Assert.Equal(0, Directory.GetFiles(fixture.Root, "stamp.txt", SearchOption.AllDirectories).Length);

                var real = fixture.Build(project);
                real.AssertSuccess("real build after design-time");
                Assert.DoesNotContain("Skipping target \"_HeddleCompile\"", real.Output);
                Assert.Equal(1, Directory.GetFiles(fixture.Root, "Heddle.CompiledForm.bin", SearchOption.AllDirectories).Length);
            }
        }
    }
}
