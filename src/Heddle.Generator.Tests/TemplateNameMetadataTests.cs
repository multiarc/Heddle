using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Heddle.Generator.Tests
{
    /// <summary>
    /// <c>Name</c> metadata registers an additional import spelling distinct from the registration <c>Key</c>,
    /// without displacing the key or breaking existing imports.
    /// </summary>
    public class TemplateNameMetadataTests
    {
        private const string Root = "/repo/app";
        private const string ReportPath = "/repo/app/templates/report.heddle";

        /// <summary>Asserts that an import resolved by checking if the imported definition's body is inlined into the importer.</summary>
        private const string DefinesBanner = "@%\n  <banner>{{ BANNER-TEXT }}\n%@\n";

        private static string Imports(string spelling) => "@<<{{" + spelling + "}}@\\\n@banner()\n";

        private static Dictionary<string, string> RootOption =>
            new Dictionary<string, string> { ["build_property.HeddleTemplateRoot"] = Root };

        private static Dictionary<string, Dictionary<string, string>> Meta(string path,
            string key = null, string name = null, string precompile = null)
        {
            var per = new Dictionary<string, string>();
            if (key != null)
                per["build_metadata.AdditionalFiles.Key"] = key;
            if (name != null)
                per["build_metadata.AdditionalFiles.Name"] = name;
            if (precompile != null)
                per["build_metadata.AdditionalFiles.Precompile"] = precompile;
            return new Dictionary<string, Dictionary<string, string>> { [path] = per };
        }

        private static string Manifest(GeneratorRun run) =>
            run.GeneratedSourceTexts.First(s => s.Contains("__HeddleManifest"));

        private static string Source(GeneratorRun run, string containing) =>
            run.GeneratedSourceTexts.First(s => s.Contains(containing));

        private static IEnumerable<Diagnostic> WithId(GeneratorRun run, string id) =>
            run.GeneratorDiagnostics.Where(d => d.Id == id);

        /// <summary>A named template imported by its path still resolves; nothing that resolved before may stop resolving.</summary>
        [Fact]
        public void ANamedTemplateIsStillImportableByItsPath()
        {
            var run = GeneratorHarness.Run(new[]
            {
                (ReportPath, DefinesBanner),
                ("/repo/app/page.heddle", Imports("templates/report.heddle"))
            }, globalOptions: RootOption, perFileOptions: Meta(ReportPath, name: "BuildReport"));

            Assert.DoesNotContain(run.GeneratorDiagnostics, d => d.Id == "HED7011");
            Assert.Contains("BANNER-TEXT", Source(run, "class Page"));
        }

        /// <summary>The registered name resolves as an import spelling.</summary>
        [Fact]
        public void ANamedTemplateIsImportableByItsRegisteredName()
        {
            var run = GeneratorHarness.Run(new[]
            {
                (ReportPath, DefinesBanner),
                ("/repo/app/page.heddle", Imports("BuildReport"))
            }, globalOptions: RootOption, perFileOptions: Meta(ReportPath, name: "BuildReport"));

            Assert.DoesNotContain(run.GeneratorDiagnostics, d => d.Id == "HED7011");
            Assert.Contains("BANNER-TEXT", Source(run, "class Page"));
        }

        /// <summary>Two spellings resolve in one compilation, asserted together to prevent implementations that merely swap which one works.</summary>
        [Fact]
        public void BothSpellingsResolveInOneCompilation()
        {
            var run = GeneratorHarness.Run(new[]
            {
                (ReportPath, DefinesBanner),
                ("/repo/app/byPath.heddle", Imports("templates/report.heddle")),
                ("/repo/app/byName.heddle", Imports("BuildReport"))
            }, globalOptions: RootOption, perFileOptions: Meta(ReportPath, name: "BuildReport"));

            Assert.Empty(run.GeneratorDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
            Assert.Contains("BANNER-TEXT", Source(run, "class ByPath"));
            Assert.Contains("BANNER-TEXT", Source(run, "class ByName"));
        }

        /// <summary>An import-only partial is reachable by its registered name; neither spelling registers a manifest row.</summary>
        [Fact]
        public void AnImportOnlyPartialIsReachableByItsRegisteredName()
        {
            var run = GeneratorHarness.Run(new[]
            {
                ("/repo/app/partials/_banner.heddle", DefinesBanner),
                ("/repo/app/page.heddle", Imports("Banner"))
            }, globalOptions: RootOption,
                perFileOptions: Meta("/repo/app/partials/_banner.heddle", name: "Banner", precompile: "false"));

            Assert.Empty(run.GeneratorDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
            Assert.Contains("BANNER-TEXT", Source(run, "class Page"));
            Assert.DoesNotContain("key: \"Banner.heddle\"", Manifest(run));
            Assert.DoesNotContain("key: \"partials/_banner.heddle\"", Manifest(run));
        }

        /// <summary>A name that spells a sibling's path does not displace it; the name becomes unusable (HED7004).</summary>
        [Fact]
        public void ANameNeverDisplacesAnotherTemplatesKey()
        {
            var run = GeneratorHarness.Run(new[]
            {
                ("/repo/app/shared/banner.heddle", "@%\n  <banner>{{ THE-REAL-BANNER }}\n%@\n"),
                (ReportPath, "@%\n  <banner>{{ THE-REPORT }}\n%@\n"),
                ("/repo/app/page.heddle", Imports("shared/banner.heddle"))
            }, globalOptions: RootOption, perFileOptions: Meta(ReportPath, name: "shared/banner"));

            Assert.Contains("THE-REAL-BANNER", Source(run, "class Page"));
            Assert.DoesNotContain("THE-REPORT", Source(run, "class Page"));
        }

        /// <summary>Name is recorded as a distinct field, not as the key; key and entry class remain path-derived.</summary>
        [Fact]
        public void NameIsRecordedAsANameAndDoesNotChangeTheKeyOrTheEntryClass()
        {
            var run = GeneratorHarness.Run(new[] { (ReportPath, "hello\n") },
                globalOptions: RootOption, perFileOptions: Meta(ReportPath, name: "BuildReport"));

            Assert.Empty(run.GeneratorDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
            var manifest = Manifest(run);
            Assert.Contains("key: \"templates/report.heddle\"", manifest);
            Assert.Contains("registeredName: \"BuildReport.heddle\"", manifest);
            Assert.DoesNotContain("key: \"BuildReport", manifest);
            Assert.Contains(run.GeneratedSourceTexts, s => s.Contains("class Templates_Report"));
            Assert.DoesNotContain(run.GeneratedSourceTexts, s => s.Contains("class BuildReport"));
        }

        /// <summary>Unnamed templates record a null name field to maintain consistent manifest row shape.</summary>
        [Fact]
        public void AnUnnamedTemplateRecordsANullName()
        {
            var run = GeneratorHarness.Run(new[] { (ReportPath, "hello\n") }, globalOptions: RootOption);

            Assert.Contains("registeredName: null", Manifest(run));
        }

        /// <summary>An unregisterable name (colliding with a sibling's key) must not reach the manifest (HED7004).</summary>
        [Fact]
        public void AnUnregisterableNameIsNotRecordedInTheManifest()
        {
            var run = GeneratorHarness.Run(new[]
            {
                ("/repo/app/shared/banner.heddle", "a\n"),
                (ReportPath, "b\n")
            }, globalOptions: RootOption, perFileOptions: Meta(ReportPath, name: "shared/banner"));

            Assert.Single(WithId(run, "HED7004"));
            var manifest = Manifest(run);
            Assert.Contains("key: \"templates/report.heddle\"", manifest);
            Assert.DoesNotContain("registeredName: \"shared/banner.heddle\"", manifest);
        }

        /// <summary>A name equal to the template's own key is not recorded because the key row already is that spelling.</summary>
        [Fact]
        public void ANameEqualToTheOwnKeyIsNotRecordedInTheManifest()
        {
            var run = GeneratorHarness.Run(new[] { (ReportPath, "hello\n") },
                globalOptions: RootOption, perFileOptions: Meta(ReportPath, name: "templates/report.heddle"));

            Assert.Contains("registeredName: null", Manifest(run));
        }

        [Fact]
        public void KeyStillSetsTheRegistrationKey()
        {
            var run = GeneratorHarness.Run(new[] { (ReportPath, "hello\n") },
                globalOptions: RootOption, perFileOptions: Meta(ReportPath, key: "reports/Build"));

            Assert.Contains("key: \"reports/Build.heddle\"", Manifest(run));
        }

        /// <summary>A name occupies the same import-path namespace as keys and is normalized the same way.</summary>
        [Theory]
        [InlineData("BuildReport", "BuildReport")]
        [InlineData("reports\\Build", "reports/Build.heddle")]
        [InlineData("~/reports//Build", "reports/Build")]
        [InlineData("Reports/build.txt", "Reports/build.txt")]
        public void ANameIsNormalizedByTheSharedRule(string metadata, string importSpelling)
        {
            var run = GeneratorHarness.Run(new[]
            {
                (ReportPath, DefinesBanner),
                ("/repo/app/page.heddle", Imports(importSpelling))
            }, globalOptions: RootOption, perFileOptions: Meta(ReportPath, name: metadata));

            Assert.DoesNotContain(run.GeneratorDiagnostics, d => d.Id == "HED7011");
            Assert.Contains("BANNER-TEXT", Source(run, "class Page"));
        }

        /// <summary>Empty <c>Name</c> metadata (MSBuild's default for unset values) is treated as absent, not an error.</summary>
        [Fact]
        public void EmptyNameMetadataIsAbsent()
        {
            var run = GeneratorHarness.Run(new[] { (ReportPath, "hello\n") },
                globalOptions: RootOption, perFileOptions: Meta(ReportPath, key: "", name: ""));

            Assert.Empty(run.GeneratorDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
            Assert.Contains("key: \"templates/report.heddle\"", Manifest(run));
        }

        /// <summary><c>Key</c> and <c>Name</c> are two names for one template; the key sets registration, the name adds a spelling.</summary>
        [Fact]
        public void KeyAndNameTogetherAreTwoNamesForOneTemplateNotAConflict()
        {
            var run = GeneratorHarness.Run(new[]
            {
                (ReportPath, DefinesBanner),
                ("/repo/app/byKey.heddle", Imports("one")),
                ("/repo/app/byName.heddle", Imports("two"))
            }, globalOptions: RootOption, perFileOptions: Meta(ReportPath, key: "one", name: "two"));

            Assert.Empty(WithId(run, "HED7004"));
            Assert.Contains("key: \"one.heddle\"", Manifest(run));
            Assert.Contains("BANNER-TEXT", Source(run, "class ByKey"));
            Assert.Contains("BANNER-TEXT", Source(run, "class ByName"));
        }

        /// <summary>A name collision is HED7004 (import-name collision), not HED7002 (duplicate key).</summary>
        [Fact]
        public void ANameThatSpellsASiblingsKeyIsNotHed7002()
        {
            var run = GeneratorHarness.Run(new[]
            {
                ("/repo/app/shared/banner.heddle", "a\n"),
                (ReportPath, "b\n")
            }, globalOptions: RootOption, perFileOptions: Meta(ReportPath, name: "shared/banner"));

            Assert.Empty(WithId(run, "HED7002"));
            var manifest = Manifest(run);
            Assert.Contains("key: \"shared/banner.heddle\"", manifest);
            Assert.Contains("key: \"templates/report.heddle\"", manifest);
        }

        /// <summary>A case-only variant of another template's key is not HED7003; names do not participate in registry lookups.</summary>
        [Fact]
        public void ANameThatDiffersOnlyByCaseFromASiblingsKeyIsNotHed7003()
        {
            var run = GeneratorHarness.Run(new[]
            {
                ("/repo/app/shared/banner.heddle", "a\n"),
                (ReportPath, "b\n")
            }, globalOptions: RootOption, perFileOptions: Meta(ReportPath, name: "shared/Banner"));

            Assert.Empty(WithId(run, "HED7003"));
        }

        /// <summary>Key-side duplicate and case-twin checks (HED7002, HED7003) are unaffected by the <c>Name</c> feature.</summary>
        [Fact]
        public void TheKeySideDuplicateAndCaseTwinChecksStillFire()
        {
            var dup = GeneratorHarness.Run(new[]
            {
                ("/repo/app/shared/banner.heddle", "a\n"),
                (ReportPath, "b\n")
            }, globalOptions: RootOption, perFileOptions: Meta(ReportPath, key: "shared/banner"));
            Assert.Single(WithId(dup, "HED7002"));

            var twin = GeneratorHarness.Run(new[]
            {
                ("/repo/app/shared/banner.heddle", "a\n"),
                (ReportPath, "b\n")
            }, globalOptions: RootOption, perFileOptions: Meta(ReportPath, key: "shared/Banner"));
            Assert.Single(WithId(twin, "HED7003"));
        }

        /// <summary>A malformed name is HED7004 but does not un-precompile the template; the key is unaffected.</summary>
        [Theory]
        [InlineData("../escape")]
        [InlineData("  ")]
        [InlineData("a/./b")]
        public void MalformedNameMetadataReportsHed7004AndKeepsTheKey(string metadata)
        {
            var run = GeneratorHarness.Run(new[] { (ReportPath, "hello\n") },
                globalOptions: RootOption, perFileOptions: Meta(ReportPath, name: metadata));

            var hed7004 = Assert.Single(WithId(run, "HED7004"));
            Assert.Equal(DiagnosticSeverity.Error, hed7004.Severity);
            Assert.Contains("Name", hed7004.GetMessage());
            Assert.Contains(ReportPath, hed7004.GetMessage());
            Assert.Contains("key: \"templates/report.heddle\"", Manifest(run));
        }

        /// <summary>A malformed key is HED7004 and un-precompiles the template; the <c>Name</c> feature does not change this.</summary>
        [Fact]
        public void MalformedKeyMetadataStillReportsHed7004AndUnprecompiles()
        {
            var run = GeneratorHarness.Run(new[] { (ReportPath, "hello\n") },
                globalOptions: RootOption, perFileOptions: Meta(ReportPath, key: "../escape"));

            var hed7004 = Assert.Single(WithId(run, "HED7004"));
            Assert.Contains("Key", hed7004.GetMessage());
            Assert.DoesNotContain("key: \"", Manifest(run));
        }

        /// <summary>A name that matches another template's key is HED7004 against the name.</summary>
        [Fact]
        public void ANameASiblingsKeyAlreadyAnswersToReportsHed7004()
        {
            var run = GeneratorHarness.Run(new[]
            {
                ("/repo/app/shared/banner.heddle", "a\n"),
                (ReportPath, "b\n")
            }, globalOptions: RootOption, perFileOptions: Meta(ReportPath, name: "shared/banner"));

            var hed7004 = Assert.Single(WithId(run, "HED7004"));
            Assert.Equal(DiagnosticSeverity.Error, hed7004.Severity);
            Assert.Contains("Name", hed7004.GetMessage());
            Assert.Contains("shared/banner.heddle", hed7004.GetMessage());
        }

        /// <summary>Two templates claiming the same name; the second receives HED7004 by first-come-first-served.</summary>
        [Fact]
        public void TwoTemplatesClaimingOneNameReportHed7004()
        {
            var run = GeneratorHarness.Run(new[]
            {
                ("/repo/app/a.heddle", "a\n"),
                ("/repo/app/b.heddle", "b\n")
            }, globalOptions: RootOption,
                perFileOptions: new Dictionary<string, Dictionary<string, string>>
                {
                    ["/repo/app/a.heddle"] = new Dictionary<string, string>
                        { ["build_metadata.AdditionalFiles.Name"] = "Shared" },
                    ["/repo/app/b.heddle"] = new Dictionary<string, string>
                        { ["build_metadata.AdditionalFiles.Name"] = "Shared" }
                });

            var hed7004 = Assert.Single(WithId(run, "HED7004"));
            Assert.Contains("/repo/app/b.heddle", hed7004.GetMessage());
            Assert.Contains("Shared.heddle", hed7004.GetMessage());
        }

        /// <summary>A name equal to the template's own key is accepted silently as redundant, not an error.</summary>
        [Fact]
        public void ANameEqualToTheTemplatesOwnKeyIsAccepted()
        {
            var run = GeneratorHarness.Run(new[]
            {
                (ReportPath, DefinesBanner),
                ("/repo/app/page.heddle", Imports("templates/report.heddle"))
            }, globalOptions: RootOption, perFileOptions: Meta(ReportPath, name: "templates/report.heddle"));

            Assert.Empty(WithId(run, "HED7004"));
            Assert.Empty(WithId(run, "HED7028"));
            Assert.Contains("BANNER-TEXT", Source(run, "class Page"));
        }

        /// <summary>Only an explicit <c>Key</c> suppresses HED7018 (out-of-root warning); an additive <c>Name</c> does not.</summary>
        [Fact]
        public void OnlyAnExplicitKeySuppressesTheOutOfRootWarning()
        {
            const string outside = "/repo/shared/banner.heddle";

            var bare = GeneratorHarness.Run(new[] { (outside, "hello\n") }, globalOptions: RootOption);
            Assert.Single(WithId(bare, "HED7018"));

            var withKey = GeneratorHarness.Run(new[] { (outside, "hello\n") },
                globalOptions: RootOption, perFileOptions: Meta(outside, key: "BuildReport"));
            Assert.Empty(WithId(withKey, "HED7018"));

            var withName = GeneratorHarness.Run(new[] { (outside, "hello\n") },
                globalOptions: RootOption, perFileOptions: Meta(outside, name: "BuildReport"));
            var hed7018 = Assert.Single(WithId(withName, "HED7018"));
            Assert.Contains("banner.heddle", hed7018.GetMessage());
            Assert.Contains("key: \"banner.heddle\"", Manifest(withName));
        }

        /// <summary>Importing by key rather than name reports HED7028 (advisory) but resolves successfully.</summary>
        [Fact]
        public void ImportingANamedTemplateByItsKeyReportsHed7028()
        {
            var run = GeneratorHarness.Run(new[]
            {
                (ReportPath, DefinesBanner),
                ("/repo/app/page.heddle", Imports("templates/report.heddle"))
            }, globalOptions: RootOption, perFileOptions: Meta(ReportPath, name: "BuildReport"));

            var hed7028 = Assert.Single(WithId(run, "HED7028"));
            Assert.Equal(DiagnosticSeverity.Warning, hed7028.Severity);
            Assert.Contains("templates/report.heddle", hed7028.GetMessage());
            Assert.Contains("BuildReport.heddle", hed7028.GetMessage());
            Assert.Empty(run.GeneratorDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
            Assert.Equal("/repo/app/page.heddle", hed7028.Location.GetLineSpan().Path);
        }

        /// <summary>HED7028 is positioned at the import block, not the file, so it can be acted on in templates with multiple imports.</summary>
        [Fact]
        public void Hed7028IsPositionedAtTheImportBlock()
        {
            var run = GeneratorHarness.Run(new[]
            {
                (ReportPath, DefinesBanner),
                ("/repo/app/page.heddle", "top\n" + Imports("templates/report.heddle"))
            }, globalOptions: RootOption, perFileOptions: Meta(ReportPath, name: "BuildReport"));

            var hed7028 = Assert.Single(WithId(run, "HED7028"));
            Assert.Equal(2, hed7028.Location.GetLineSpan().StartLinePosition.Line + 1);
        }

        /// <summary>Repeated imports of one spelling are advised once, resolved to the first block, not duplicated.</summary>
        [Fact]
        public void RepeatedImportsOfOneSpellingAdviseOnce()
        {
            var run = GeneratorHarness.Run(new[]
            {
                (ReportPath, DefinesBanner),
                ("/repo/app/page.heddle",
                    Imports("templates/report.heddle") + Imports("templates/report.heddle"))
            }, globalOptions: RootOption, perFileOptions: Meta(ReportPath, name: "BuildReport"));

            Assert.Single(WithId(run, "HED7028"));
        }

        /// <summary>Importing by the preferred name spelling is silent.</summary>
        [Fact]
        public void ImportingANamedTemplateByItsNameIsSilent()
        {
            var run = GeneratorHarness.Run(new[]
            {
                (ReportPath, DefinesBanner),
                ("/repo/app/page.heddle", Imports("BuildReport"))
            }, globalOptions: RootOption, perFileOptions: Meta(ReportPath, name: "BuildReport"));

            Assert.Empty(WithId(run, "HED7028"));
        }

        /// <summary>Unnamed templates imported by path remain silent; the feature adds no warning noise to existing projects.</summary>
        [Fact]
        public void ImportingAnUnnamedTemplateByPathIsSilent()
        {
            var run = GeneratorHarness.Run(new[]
            {
                (ReportPath, DefinesBanner),
                ("/repo/app/page.heddle", Imports("templates/report.heddle"))
            }, globalOptions: RootOption);

            Assert.Empty(WithId(run, "HED7028"));
        }

        /// <summary>An unregisterable name (HED7004) does not trigger HED7028; there is no preferred spelling.</summary>
        [Fact]
        public void AnUnregisterableNameAdvisesNothing()
        {
            var run = GeneratorHarness.Run(new[]
            {
                ("/repo/app/shared/banner.heddle", "a\n"),
                (ReportPath, DefinesBanner),
                ("/repo/app/page.heddle", Imports("templates/report.heddle"))
            }, globalOptions: RootOption, perFileOptions: Meta(ReportPath, name: "shared/banner"));

            Assert.Single(WithId(run, "HED7004"));
            Assert.Empty(WithId(run, "HED7028"));
        }

        /// <summary>A malformed name on an opted-out item is HED7004; opted-out items participate in the import graph.</summary>
        [Theory]
        [InlineData("../escape")]
        [InlineData("  ")]
        [InlineData("a/./b")]
        public void AMalformedNameOnAnOptedOutItemReportsHed7004(string metadata)
        {
            var run = GeneratorHarness.Run(new[] { (ReportPath, DefinesBanner) },
                globalOptions: RootOption,
                perFileOptions: Meta(ReportPath, name: metadata, precompile: "false"));

            var hed7004 = Assert.Single(WithId(run, "HED7004"));
            Assert.Equal(DiagnosticSeverity.Error, hed7004.Severity);
            Assert.Contains("Name", hed7004.GetMessage());
            Assert.Contains(ReportPath, hed7004.GetMessage());
        }

        /// <summary>An already-taken name on an opted-out item is HED7004; the name is visible at the defining file.</summary>
        [Fact]
        public void AnAlreadyTakenNameOnAnOptedOutItemReportsHed7004()
        {
            var run = GeneratorHarness.Run(new[]
            {
                ("/repo/app/shared/banner.heddle", "a\n"),
                (ReportPath, DefinesBanner)
            }, globalOptions: RootOption,
                perFileOptions: Meta(ReportPath, name: "shared/banner", precompile: "false"));

            var hed7004 = Assert.Single(WithId(run, "HED7004"));
            Assert.Contains("Name", hed7004.GetMessage());
            Assert.Contains("shared/banner.heddle", hed7004.GetMessage());
        }

        /// <summary>A malformed key on an opted-out item is HED7004; the same fault reports the same way whether opted-out or not.</summary>
        [Fact]
        public void AMalformedKeyOnAnOptedOutItemReportsHed7004()
        {
            var run = GeneratorHarness.Run(new[] { (ReportPath, DefinesBanner) },
                globalOptions: RootOption,
                perFileOptions: Meta(ReportPath, key: "../escape", precompile: "false"));

            var hed7004 = Assert.Single(WithId(run, "HED7004"));
            Assert.Contains("Key", hed7004.GetMessage());
        }

        /// <summary>Validating an opted-out item does not start precompiling it; no entry point or manifest entry.</summary>
        [Fact]
        public void AValidatedOptedOutItemStillContributesNoEntryPointAndNoManifestEntry()
        {
            var run = GeneratorHarness.Run(new[]
            {
                (ReportPath, DefinesBanner),
                ("/repo/app/page.heddle", Imports("BuildReport"))
            }, globalOptions: RootOption,
                perFileOptions: Meta(ReportPath, name: "BuildReport", precompile: "false"));

            Assert.Empty(run.GeneratorDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
            var manifest = Manifest(run);
            Assert.DoesNotContain("key: \"templates/report.heddle\"", manifest);
            Assert.DoesNotContain("BuildReport", manifest);
            Assert.DoesNotContain(run.GeneratedSourceTexts, s => s.Contains("class Templates_Report"));
            Assert.Contains("BANNER-TEXT", Source(run, "class Page"));
        }

        /// <summary>HED7028 fires for imports inside opted-out files; they participate in the import graph.</summary>
        [Fact]
        public void Hed7028FiresForAnImportInsideAnOptedOutFile()
        {
            const string importer = "/repo/app/partials/_wrapper.heddle";
            var run = GeneratorHarness.Run(new[]
            {
                (ReportPath, DefinesBanner),
                (importer, Imports("templates/report.heddle"))
            }, globalOptions: RootOption,
                perFileOptions: new Dictionary<string, Dictionary<string, string>>
                {
                    [ReportPath] = new Dictionary<string, string>
                        { ["build_metadata.AdditionalFiles.Name"] = "BuildReport" },
                    [importer] = new Dictionary<string, string>
                        { ["build_metadata.AdditionalFiles.Precompile"] = "false" }
                });

            var hed7028 = Assert.Single(WithId(run, "HED7028"));
            Assert.Equal(DiagnosticSeverity.Warning, hed7028.Severity);
            Assert.Equal(importer, hed7028.Location.GetLineSpan().Path);
            Assert.Contains("BuildReport.heddle", hed7028.GetMessage());
        }

        /// <summary>The advisory parse of an opted-out file reports only HED7028; missing imports and parse errors stay silent.</summary>
        [Fact]
        public void TheAdvisoryParseOfAnOptedOutFileReportsNothingElse()
        {
            const string partial = "/repo/app/partials/_wrapper.heddle";
            var run = GeneratorHarness.Run(new[]
            {
                (partial, "@<<{{ does/not/exist.heddle }}@\\\ntext\n")
            }, globalOptions: RootOption,
                perFileOptions: Meta(partial, precompile: "false"));

            Assert.Empty(WithId(run, "HED7011"));
            Assert.Empty(run.GeneratorDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
        }

        /// <summary>A precompiled file still reports its missing imports; opt-out suppression is scoped narrowly.</summary>
        [Fact]
        public void APrecompiledFileStillReportsItsMissingImport()
        {
            var run = GeneratorHarness.Run(new[]
            {
                ("/repo/app/page.heddle", "@<<{{ does/not/exist.heddle }}@\\\ntext\n")
            }, globalOptions: RootOption);

            Assert.Single(WithId(run, "HED7011"));
        }

        /// <summary>The <c>#line</c> file names the file (path-derived), not the key; this is observable when <c>Key</c> is explicit.</summary>
        [Fact]
        public void TheLineDirectiveNamesTheFileNotTheKey()
        {
            const string body = "@model(){{System.String}}@\\\nx @(this) y\n";

            var withKey = GeneratorHarness.Run(new[] { (ReportPath, body) },
                globalOptions: RootOption, perFileOptions: Meta(ReportPath, key: "BuildReport"));
            Assert.Contains("\"templates/report.heddle\"", Source(withKey, "class BuildReport"));
            Assert.DoesNotContain("\"BuildReport.heddle\"", Source(withKey, "class BuildReport"));

            var withName = GeneratorHarness.Run(new[] { (ReportPath, body) },
                globalOptions: RootOption, perFileOptions: Meta(ReportPath, name: "BuildReport"));
            Assert.Contains("\"templates/report.heddle\"", Source(withName, "class Templates_Report"));

            var plain = GeneratorHarness.Run(new[] { (ReportPath, body) }, globalOptions: RootOption);
            Assert.Contains("\"templates/report.heddle\"", Source(plain, "class Templates_Report"));
        }

        /// <summary>Under the template root, <c>#line</c> is root-relative; the form is recorded in the manifest, not as a comment.</summary>
        [Fact]
        public void UnderTheRootTheLineFileIsRootRelativeAndTheFormIsRecordedInTheManifest()
        {
            var run = GeneratorHarness.Run(new[] { (ReportPath, "@model(){{System.String}}@\\\nx @(this) y\n") },
                globalOptions: RootOption);

            var source = Source(run, "class Templates_Report");
            Assert.Contains("\"templates/report.heddle\"", source);
            Assert.Contains("linePathForm: global::Heddle.Precompiled.PrecompiledLinePathForm.RootRelative",
                Manifest(run));
            Assert.DoesNotContain("#line file names below", source);
        }

        /// <summary>Outside the template root, <c>#line</c> is the template's path; relativity has no anchor.</summary>
        [Fact]
        public void OutsideTheRootTheLineFileIsTheTemplatesOwnPathAndTheFormIsRecorded()
        {
            const string outside = "/repo/shared/banner.heddle";
            var run = GeneratorHarness.Run(new[] { (outside, "@model(){{System.String}}@\\\nx @(this) y\n") },
                globalOptions: RootOption);

            var source = Source(run, "class Banner");
            Assert.Contains("\"" + outside + "\"", source);
            Assert.Contains("linePathForm: global::Heddle.Precompiled.PrecompiledLinePathForm.TemplatePath",
                Manifest(run));
            Assert.DoesNotContain("#line file names below", source);
            Assert.DoesNotContain("\"banner.heddle\"", source);
        }

        /// <summary>One compilation containing both rooted and out-of-root templates records a different form for each.</summary>
        [Fact]
        public void TheTwoLineFormsAreRecordedDistinctlyInOneCompilation()
        {
            const string body = "@model(){{System.String}}@\\\nx @(this) y\n";
            var run = GeneratorHarness.Run(new[]
            {
                (ReportPath, body),
                ("/repo/shared/banner.heddle", body)
            }, globalOptions: RootOption);

            var manifest = Manifest(run);
            Assert.Contains("linePathForm: global::Heddle.Precompiled.PrecompiledLinePathForm.RootRelative", manifest);
            Assert.Contains("linePathForm: global::Heddle.Precompiled.PrecompiledLinePathForm.TemplatePath", manifest);
        }
    }
}
