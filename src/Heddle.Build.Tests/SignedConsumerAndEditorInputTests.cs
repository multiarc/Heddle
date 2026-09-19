using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace Heddle.Build.Tests
{
    public class SignedConsumerAndEditorInputTests
    {
        /// <summary>The intermediate compile has to be the assembly the real compile will produce, identity
        /// included. Pins the regression where it ignored the project's strong-name settings: a signed
        /// consumer that reads a friend assembly's internals — granted to its public key — failed the
        /// intermediate pass with CS0281 although the real compile succeeds, and the identities recorded
        /// from an unsigned image were not the final assembly's.</summary>
        [Fact]
        public void SignedConsumerUsingAFriendAssemblysInternalsPrecompilesItsOwnModel()
        {
            using (var fixture = new MsBuildFixture())
            {
                string key = Path.Combine(fixture.Root, "test.snk");
                File.Copy(Path.Combine(MsBuildFixture.RepoRoot, "heddle.snk"), key);
                string publicKey = Regex.Match(
                    File.ReadAllText(Path.Combine(MsBuildFixture.RepoRoot, "src", "Heddle.Tool", "Heddle.Tool.csproj")),
                    "PublicKey=([0-9A-Fa-f]+)").Groups[1].Value;
                Assert.False(string.IsNullOrEmpty(publicKey), "could not read the repository key's public key.");

                fixture.Write("friend/friend.csproj",
                    "<Project Sdk=\"Microsoft.NET.Sdk\">\n  <PropertyGroup>\n    <TargetFramework>net10.0</TargetFramework>\n"
                    + "    <Nullable>disable</Nullable>\n    <SignAssembly>true</SignAssembly>\n"
                    + "    <AssemblyOriginatorKeyFile>../test.snk</AssemblyOriginatorKeyFile>\n  </PropertyGroup>\n</Project>\n");
                fixture.Write("friend/Secrets.cs",
                    "[assembly: System.Runtime.CompilerServices.InternalsVisibleTo(\"app, PublicKey=" + publicKey + "\")]\n"
                    + "namespace Friend { internal static class Secrets { internal static string Tag => \"friend\"; } }\n");
                fixture.Write("templates/order.heddle", "@model(){{SignedApp.Order}}\nOrder: @(Name) @(Tag)\n");
                fixture.Write("Models.cs",
                    "namespace SignedApp { public class Order { public string Name { get; set; } "
                    + "public string Tag => Friend.Secrets.Tag; } }\n");
                string project = fixture.Write("app.csproj",
                    MsBuildFixture.ProjectXml("net10.0",
                        "    <SignAssembly>true</SignAssembly>\n    <AssemblyOriginatorKeyFile>test.snk</AssemblyOriginatorKeyFile>\n",
                        "<ProjectReference Include=\"friend/friend.csproj\" />\n"
                        + "    <HeddleTemplate Include=\"templates/order.heddle\" />\n"
                        + "    <Compile Remove=\"friend/**/*.cs\" />\n"));

                var build = fixture.Build(project);
                build.AssertSuccess("signed consumer with a friend assembly");
                Assert.DoesNotContain("CS0281", build.Output);
                Assert.Contains("global::SignedApp.Order",
                    File.ReadAllText(MsBuildFixture.GeneratedSource(fixture.Root)));

                string[] models = Directory.GetFiles(Path.Combine(fixture.Root, "obj"), "app.dll",
                    SearchOption.AllDirectories);
                bool signed = false;
                foreach (string model in models)
                {
                    if (!model.Replace(Path.DirectorySeparatorChar, '/').Contains("/heddle/models/"))
                        continue;
                    byte[] token = System.Reflection.AssemblyName.GetAssemblyName(model).GetPublicKeyToken();
                    signed = token != null && token.Length != 0;
                }

                Assert.True(signed, "the intermediate model assembly carries no public key.");
            }
        }

        /// <summary>Editors decide whether to build at all from declared inputs. Pins the omission where
        /// templates were declared to neither Visual Studio's fast up-to-date check nor <c>dotnet watch</c>,
        /// so a template edit built nothing and the stale embedded artifact kept being served.</summary>
        [Fact]
        public void TemplatesAndDeclaredAssembliesAreUpToDateCheckAndWatchInputs()
        {
            using (var fixture = new MsBuildFixture())
            {
                fixture.Write("templates/hello.heddle", "Hello\n");
                fixture.Write("libs/Models.dll", "not read by this test");
                // Items before the import, as they are when the targets arrive from the package.
                string project = fixture.Write("app.csproj",
                    "<Project Sdk=\"Microsoft.NET.Sdk\">\n  <PropertyGroup>\n    <TargetFramework>net10.0</TargetFramework>\n"
                    + "    <EnableDefaultHeddleTemplates>false</EnableDefaultHeddleTemplates>\n  </PropertyGroup>\n"
                    + "  <ItemGroup>\n    <HeddleTemplate Include=\"templates/hello.heddle\" />\n"
                    + "    <HeddleModelAssembly Include=\"libs/Models.dll\" />\n  </ItemGroup>\n"
                    + "  <Import Project=\"" + MsBuildFixture.PropsPath + "\" />\n"
                    + "  <Import Project=\"" + MsBuildFixture.TargetsPath + "\" />\n</Project>\n");

                var inputs = fixture.Dotnet("msbuild \"" + project + "\" -getItem:UpToDateCheckInput -getItem:Watch");
                inputs.AssertSuccess("item query");
                // JSON escapes a Windows separator as two backslashes.
                string json = inputs.Output.Replace(@"\\", "/");
                int watch = json.IndexOf("\"Watch\"", System.StringComparison.Ordinal);
                int upToDate = json.IndexOf("\"UpToDateCheckInput\"", System.StringComparison.Ordinal);
                Assert.True(watch >= 0 && upToDate >= 0, json);
                string upToDatePart = upToDate < watch ? json.Substring(upToDate, watch - upToDate) : json.Substring(upToDate);
                string watchPart = watch < upToDate ? json.Substring(watch, upToDate - watch) : json.Substring(watch);
                Assert.Contains("templates/hello.heddle", upToDatePart);
                Assert.Contains("libs/Models.dll", upToDatePart);
                Assert.Contains("templates/hello.heddle", watchPart);
            }
        }
    }
}
