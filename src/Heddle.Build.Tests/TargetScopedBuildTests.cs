using System.IO;
using Xunit;

namespace Heddle.Build.Tests
{
    /// <summary>P2-R6: a target-scoped invocation (<c>/t:Compile</c> — anything that never runs
    /// <c>BuildOnlySettings</c>, so <c>BuildingProject</c> is not <c>true</c>) gets the stubs pass only.
    /// The compile, the resource item and the retired-property warnings all stay skipped, so the stubs
    /// and the real generated source are never both in <c>@(Compile)</c>.</summary>
    public class TargetScopedBuildTests
    {
        [Fact]
        public void TargetScopedInvocationRunsOnlyTheStubsPass()
        {
            using (var fixture = new MsBuildFixture())
            {
                fixture.Write("templates/hello.heddle", "Hello, @(Name)!\n");
                string project = fixture.Write("app.csproj",
                    MsBuildFixture.ProjectXml("net10.0", "    <HeddleObserveEngine>true</HeddleObserveEngine>\n",
                        "<HeddleTemplate Include=\"templates/hello.heddle\" />\n"));
                var scoped = fixture.Build(project, "/t:Compile");
                scoped.AssertSuccess("target-scoped build");
                Assert.DoesNotContain("error CS0101", scoped.Output);
                string[] stubs = Directory.GetFiles(fixture.Root, "Heddle.Generated.Stubs.g.cs", SearchOption.AllDirectories);
                Assert.True(stubs.Length == 1, "expected one stubs file, found " + stubs.Length + ".");
                Assert.Equal(0, Directory.GetFiles(fixture.Root, "Heddle.CompiledForm.bin", SearchOption.AllDirectories).Length);
                Assert.Equal(0, Directory.GetFiles(fixture.Root, "Heddle.CompiledForm.g.cs", SearchOption.AllDirectories).Length);
                Assert.DoesNotContain("HED7037", scoped.Output);

                var real = fixture.Build(project);
                real.AssertSuccess("real build after the target-scoped one");
                Assert.Equal(1, Directory.GetFiles(fixture.Root, "Heddle.CompiledForm.bin", SearchOption.AllDirectories).Length);
                Assert.Contains("HeddleObserveEngine is retired and ignored", real.Output);
            }
        }
    }
}
