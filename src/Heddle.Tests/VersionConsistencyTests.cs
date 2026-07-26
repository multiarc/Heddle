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
    /// <para>Before this gate, the release line was hand-maintained in <b>nine</b>
    /// <c>&lt;Version&gt;</c> elements plus four npm manifests, a TypeScript constant, a workflow argument and a C#
    /// const — eighteen statements of one fact, with nothing forcing them to agree. They did not: the language
    /// server's own <c>InformationalVersion</c> still read <c>1.0.0</c> at 2.0, which is what
    /// <c>heddle-lsp --version</c> printed and what the LSP <c>initialize</c> response reported, and the one test
    /// that touched it compared it against itself.</para>
    /// <para>The mechanism is one <c>&lt;VersionPrefix&gt;</c> in <c>Directory.Build.props</c>; this is the gate that
    /// keeps the statements that <em>cannot</em> be centralised (npm manifests, the VS Code pin, the workflow's tool
    /// install, prose release-line statements) in step with it. Every assertion names its file, so a failure says
    /// which statement drifted rather than that "a version is wrong".</para>
    /// <para><b>Deliberately not asserted:</b> version numbers that are not the release line — the semver spec URL in
    /// the CHANGELOG, "byte-identical to 2.0.0" behaviour pins that name a historical release, the
    /// <c>codegen-t4-successor</c> sample's <c>Version = "2.0.0"</c> model datum (sample data that happens to look
    /// like a version), and third-party pins.</para>
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

        /// <summary>Set equality against the empty set, not a count: the duplication is gone, and a project that
        /// reintroduces its own <c>&lt;Version&gt;</c> is named in the failure message. The three non-shipping
        /// projects carried one too — overridden by CI, read by nobody — so "it is only documentation" was never a
        /// reason to keep them.</summary>
        /// <summary>Build output and nested checkouts are not repository source. The dot-directory arm matters
        /// as much as bin/obj: a git worktree under <c>.claude/</c> is a whole second copy of the tree, so without
        /// it this gate reports every project twice and fails on files no release ever ships.</summary>
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

        /// <summary>The built assembly agrees with the props. This is what makes the props value the release line
        /// rather than a comment: a CI tag override (<c>-p:Version=</c>) that disagrees with the checked-in prefix
        /// reds the build instead of shipping a package whose number no source states.</summary>
        [Fact]
        public void TheBuiltEngineAssemblyCarriesTheCanonicalVersion()
        {
            var version = typeof(TemplateKey).Assembly.GetName().Version;
            Assert.NotNull(version);
            Assert.Equal(Canonical, $"{version.Major}.{version.Minor}.{version.Build}");
        }

        /// <summary>The npm manifests and their lockfiles. The lockfile states the version twice (root and the
        /// <c>packages[""]</c> self-entry) and <c>npm version</c> updates both, so both are asserted — a hand-edited
        /// manifest with a stale lock is the drift this catches.</summary>
        [Theory]
        [InlineData("editors/vscode/package.json", 1)]
        [InlineData("editors/vscode/package-lock.json", 2)]
        [InlineData("src/Heddle.Language/package.json", 1)]
        [InlineData("src/Heddle.Language/package-lock.json", 2)]
        public void NpmManifestsCarryTheCanonicalVersion(string relative, int expectedStatements)
        {
            // Only the manifest's own version — dependency versions live deeper in the file, after the first
            // "dependencies"/"devDependencies" key, and are third-party pins.
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

        /// <summary>The riskiest statement in the survey: the VS Code extension pins the NuGet tool version it tells
        /// the user to install, in TypeScript, outside every MSBuild and NuGet check. A stale pin sends users to a
        /// version of the language server that does not exist yet, or an old one.</summary>
        [Fact]
        public void TheVsCodeExtensionPinsTheCanonicalToolVersion()
        {
            var m = Regex.Match(Read("editors/vscode/src/extension.ts"),
                @"const PINNED_VERSION = '(?<v>[^']+)';");
            Assert.True(m.Success, "PINNED_VERSION not found in editors/vscode/src/extension.ts");
            Assert.Equal(Canonical, m.Groups["v"].Value);
        }

        /// <summary>The LSP workflow packs and installs the tool by an explicit <c>--version</c>, with no tag
        /// override, so its literal is a third statement of the release line.</summary>
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

        /// <summary>The <c>--version-suffix</c> contract. <c>VersionPrefix</c> + <c>VersionSuffix</c> are joined by
        /// the SDK with a single <c>-</c>, so a suffix that carries its own leading dash — which is what the beta job
        /// passed while the projects spelled <c>2.0.0$(VersionSuffix)</c> by hand — now produces <c>2.1.0--beta.N</c>.
        /// Centralising the version made that argument's shape load-bearing, so it is pinned here rather than
        /// discovered on a release.</summary>
        [Fact]
        public void TheBetaVersionSuffixCarriesNoLeadingDash()
        {
            foreach (Match m in Regex.Matches(Read(".github/workflows/dotnet.yml"),
                @"--version-suffix\s+""(?<s>[^""]*)"""))
                Assert.False(m.Groups["s"].Value.StartsWith("-", StringComparison.Ordinal),
                    "A --version-suffix must not start with '-': VersionPrefix and VersionSuffix are joined with " +
                    "one dash, so '" + m.Groups["s"].Value + "' would produce a double dash.");
        }

        /// <summary>The prose statements of the release line. Limited to sentences that state the <em>current</em>
        /// line, because a document naming a historical release is making a different claim.</summary>
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

        /// <summary>The language server's reported version is <b>derived</b>, not stated: a literal there is what
        /// made <c>heddle-lsp --version</c> print <c>1.0.0</c> for the whole 2.0 line. This is a source-shape pin
        /// rather than a value comparison, stated as such: the value cannot drift once it is read off the assembly, so
        /// what needs guarding is the reintroduction of a literal.</summary>
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

        /// <summary>
        /// <para>The signing half. The build has no <c>TreatWarningsAsErrors</c>, so "the <c>CS8002</c>
        /// warnings stopped" is not a property any gate held — an unsigned first-party project would simply start
        /// warning again and nothing would fail. This is that gate, expressed the way the warning arises: <b>every</b>
        /// project under <c>src/</c> that produces an assembly is signed, unless it is named below with a reason.</para>
        /// <para>Deliberately scoped to <c>src/</c>. The samples and the benchmark project are consumer-shaped —
        /// they model what a user's project looks like, and a user's project is not signed — and they reference nothing
        /// signed, so they raise no <c>CS8002</c>. Signing them would be modelling a lie.</para>
        /// </summary>
        [Fact]
        public void EveryFirstPartyProjectUnderSrcIsStrongNamed()
        {
            // The one deliberate exception, named with its reason rather than silently absent: this project exists to
            // host the unsigned third-party comparison engines, and signing it would only manufacture the CS8002 the
            // rest of this work removes.
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

        /// <summary>The third-party half. Unsigned references we accept are a <em>declared list</em> in
        /// <c>Directory.Build.targets</c>, not a project-level <c>NoWarn</c> — so an unsigned reference that is not on
        /// the list still warns wherever it appears. Pinned as a source shape because the mechanism is the point: a
        /// blanket suppression would look identical from the outside and would silence the next one silently.</summary>
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

        /// <summary>The release line has a CHANGELOG section and a compare link. Keeping this in the gate is what
        /// stops a version bump from shipping with no record of what changed — the CHANGELOG entry the binary
        /// break requires is then structurally impossible to forget.</summary>
        [Fact]
        public void TheChangelogHasASectionForTheCanonicalVersion()
        {
            var changelog = Read("CHANGELOG.md");
            Assert.Contains("## [" + Canonical + "]", changelog);
            Assert.Contains("[" + Canonical + "]: https://github.com/multiarc/Heddle/compare/", changelog);
        }
    }
}
