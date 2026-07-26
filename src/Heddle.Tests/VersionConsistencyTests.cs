using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Heddle.Precompiled;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// Version consistency gate: all release-line statements are asserted against the canonical
    /// <c>&lt;VersionPrefix&gt;</c> in Directory.Build.props. Deliberately not asserted: historical version pins,
    /// sample data, and third-party references.
    /// </summary>
    public class VersionConsistencyTests
    {
        /// <summary>The one place the release line is stated. Everything else is checked against this.</summary>
        private static string Canonical
        {
            get
            {
                var props = Read("Directory.Build.props");
                var matches = Regex.Matches(props, @"<VersionPrefix>(?<v>[^<]+)</VersionPrefix>");
                Assert.True(matches.Count == 1,
                    "Directory.Build.props must state exactly one <VersionPrefix>; found " + matches.Count);
                var value = matches[0].Groups["v"].Value.Trim();
                Assert.Matches(@"^\d+\.\d+\.\d+$", value);
                return value;
            }
        }

        private static string Read(string relative) =>
            File.ReadAllText(PipelineContractTests.FindRepoFile(relative.Replace('/', Path.DirectorySeparatorChar)))
                .Replace("\r\n", "\n");

        private static string RepoRoot =>
            Path.GetDirectoryName(PipelineContractTests.FindRepoFile("Directory.Build.props"));

        private static bool IsNotSource(string relative) =>
            relative.Split(Path.DirectorySeparatorChar)
                .Any(s => s == "bin" || s == "obj" || s.StartsWith("."));

        [Fact]
        public void NoProjectStatesItsOwnVersion()
        {
            var offenders = new List<string>();
            foreach (var project in Directory.EnumerateFiles(RepoRoot, "*.csproj", SearchOption.AllDirectories))
            {
                var relative = project.Substring(RepoRoot.Length + 1);
                if (IsNotSource(relative))
                    continue;
                foreach (Match m in Regex.Matches(File.ReadAllText(project),
                    @"<(Version|VersionPrefix|VersionSuffix|AssemblyVersion|FileVersion|PackageVersion)>"))
                    offenders.Add(relative + ": " + m.Value);
            }

            Assert.True(offenders.Count == 0,
                "Version elements outside Directory.Build.props (the version is centralised there): " +
                string.Join(", ", offenders));
        }

        /// <summary>The built assembly version matches Directory.Build.props, making the props file the release line.</summary>
        [Fact]
        public void TheBuiltEngineAssemblyCarriesTheCanonicalVersion()
        {
            var version = typeof(TemplateKey).Assembly.GetName().Version;
            Assert.NotNull(version);
            Assert.Equal(Canonical, $"{version.Major}.{version.Minor}.{version.Build}");
        }

        /// <summary>npm manifests and their lockfiles state the version (lockfiles state it twice).</summary>
        [Theory]
        [InlineData("editors/vscode/package.json", 1)]
        [InlineData("editors/vscode/package-lock.json", 2)]
        [InlineData("src/Heddle.Language/package.json", 1)]
        [InlineData("src/Heddle.Language/package-lock.json", 2)]
        public void NpmManifestsCarryTheCanonicalVersion(string relative, int expectedStatements)
        {
            var text = Read(relative);
            var cut = new[] { "\"dependencies\"", "\"devDependencies\"", "\"engines\"" }
                .Select(k => text.IndexOf(k, StringComparison.Ordinal))
                .Where(i => i >= 0)
                .DefaultIfEmpty(text.Length)
                .Min();
            var head = text.Substring(0, cut);

            var found = Regex.Matches(head, @"""version"":\s*""(?<v>[^""]+)""")
                .Cast<Match>()
                .Select(m => m.Groups["v"].Value)
                .ToList();
            Assert.Equal(expectedStatements, found.Count);
            Assert.All(found, v => Assert.Equal(Canonical, v));
        }

        /// <summary>The VS Code extension pins the tool version; a stale pin sends users to the wrong version.</summary>
        [Fact]
        public void TheVsCodeExtensionPinsTheCanonicalToolVersion()
        {
            var m = Regex.Match(Read("editors/vscode/src/extension.ts"),
                @"const PINNED_VERSION = '(?<v>[^']+)';");
            Assert.True(m.Success, "PINNED_VERSION not found in editors/vscode/src/extension.ts");
            Assert.Equal(Canonical, m.Groups["v"].Value);
        }

        /// <summary>The LSP workflow installs the tool with an explicit <c>--version</c> literal.</summary>
        [Fact]
        public void TheLspWorkflowInstallsTheCanonicalToolVersion()
        {
            var found = Regex.Matches(Read(".github/workflows/lsp.yml"), @"--version (?<v>\d+\.\d+\.\d+)")
                .Cast<Match>()
                .Select(m => m.Groups["v"].Value)
                .ToList();
            Assert.NotEmpty(found);
            Assert.All(found, v => Assert.Equal(Canonical, v));
        }

        /// <summary>The <c>--version-suffix</c> must not lead with '-' because the SDK joins prefix and suffix with one dash.</summary>
        [Fact]
        public void TheBetaVersionSuffixCarriesNoLeadingDash()
        {
            foreach (Match m in Regex.Matches(Read(".github/workflows/dotnet.yml"),
                @"--version-suffix\s+""(?<s>[^""]*)"""))
                Assert.False(m.Groups["s"].Value.StartsWith("-", StringComparison.Ordinal),
                    "A --version-suffix must not start with '-': VersionPrefix and VersionSuffix are joined with " +
                    "one dash, so '" + m.Groups["s"].Value + "' would produce a double dash.");
        }

        /// <summary>Prose statements of the current release line in documentation files.</summary>
        [Theory]
        [InlineData("docs/building.md", @"current release line is \*\*(?<v>\d+\.\d+\.\d+)\*\*")]
        [InlineData("docs/README.md", @"Current release line: \*\*(?<v>\d+\.\d+\.\d+)\*\*")]
        [InlineData("docs/coming-from-liquid.md", @"verified to compile against \*\*Heddle (?<v>\d+\.\d+\.\d+)\*\*")]
        [InlineData("docs/coming-from-razor.md", @"verified to compile against \*\*Heddle (?<v>\d+\.\d+\.\d+)\*\*")]
        public void DocsStateTheCanonicalReleaseLine(string relative, string pattern)
        {
            var found = Regex.Matches(Read(relative), pattern)
                .Cast<Match>()
                .Select(m => m.Groups["v"].Value)
                .ToList();
            Assert.NotEmpty(found);
            Assert.All(found, v => Assert.Equal(Canonical, v));
        }

        /// <summary>The language server derives its version from the assembly; any literal is a regression.</summary>
        [Fact]
        public void TheLanguageServerDerivesItsReportedVersionRatherThanStatingOne()
        {
            var source = Read("src/Heddle.LanguageServer/LspServer.cs");
            var body = Regex.Replace(source, @"///.*", string.Empty);
            var literals = Regex.Matches(body, @"""\d+\.\d+\.\d+""")
                .Cast<Match>()
                .Select(m => m.Value)
                .ToList();
            Assert.True(literals.Count == 0,
                "LspServer.cs states a version literal instead of reading it off the assembly: " +
                string.Join(", ", literals));
            Assert.Contains("AssemblyInformationalVersionAttribute", body);
        }

        /// <summary>Every assembly-producing project under src/ is strong-named to prevent CS8002 warnings.</summary>
        [Fact]
        public void EveryFirstPartyProjectUnderSrcIsStrongNamed()
        {
            var accepted = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Heddle.Performance.ThirdParty.csproj"
            };

            var src = Path.Combine(RepoRoot, "src");
            var unsigned = new List<string>();
            foreach (var project in Directory.EnumerateFiles(src, "*.csproj", SearchOption.AllDirectories))
            {
                var relative = project.Substring(RepoRoot.Length + 1);
                if (IsNotSource(relative))
                    continue;
                if (accepted.Contains(Path.GetFileName(project)))
                    continue;

                var text = File.ReadAllText(project);
                if (!Regex.IsMatch(text, @"<SignAssembly>\s*true\s*</SignAssembly>") ||
                    !text.Contains("heddle.snk"))
                    unsigned.Add(relative);
            }

            Assert.True(unsigned.Count == 0,
                "First-party projects under src/ that are not strong-named (each one is a CS8002 source for every " +
                "signed project that references it): " + string.Join(", ", unsigned));
        }

        /// <summary>Unsigned third-party references are declared in Directory.Build.targets, not blanket-suppressed.</summary>
        [Fact]
        public void UnsignedThirdPartyReferencesAreAcceptedByNameAndNotByABlanketNoWarn()
        {
            var targets = Read("Directory.Build.targets");
            Assert.Contains("<HeddleAcceptedUnsignedReference Include=\"Scriban\" />", targets);

            foreach (var project in Directory.EnumerateFiles(Path.Combine(RepoRoot, "src"), "*.csproj",
                SearchOption.AllDirectories).Concat(new[] { Path.Combine(RepoRoot, "Directory.Build.props") }))
            {
                var relative = project.Substring(RepoRoot.Length + 1);
                if (IsNotSource(relative))
                    continue;
                var withoutComments = Regex.Replace(File.ReadAllText(project), @"<!--.*?-->", string.Empty,
                    RegexOptions.Singleline);
                Assert.DoesNotContain("CS8002", withoutComments);
            }
        }

        /// <summary>The CHANGELOG has a section and compare link for the canonical version.</summary>
        [Fact]
        public void TheChangelogHasASectionForTheCanonicalVersion()
        {
            var changelog = Read("CHANGELOG.md");
            Assert.Contains("## [" + Canonical + "]", changelog);
            Assert.Contains("[" + Canonical + "]: https://github.com/multiarc/Heddle/compare/", changelog);
        }
    }
}
