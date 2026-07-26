using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Heddle.Generator.Tests
{
    /// <summary>
    /// <para>Q8.12 / Q8.25: the <c>Name</c> item metadata as an <b>additional import name</b>. Phase 5 deleted the
    /// metadata on a register entry reading "<c>Name</c> removed per the recommendation"; the ask had only ever been
    /// to wire <c>Precompile</c>. <c>Name</c> was genuinely dead — declared <c>CompilerVisibleItemMetadata</c>, never
    /// mapped onto <c>AdditionalFiles</c> by <c>Heddle.Generator.targets</c>, never read — so its removal changed no
    /// behaviour and <c>samples/codegen-t4-successor</c>'s <c>Name="BuildReport"</c> was always ignored. The defect
    /// was that the feature was never wired, not that the metadata existed.</para>
    /// <para><b>The first implementation of it was wrong, and this fixture is the corrected one.</b> Q8.12 landed
    /// <c>Name</c> as a <em>second spelling of <c>Key</c></em> — one setting, so it shared every downstream rule. That
    /// is an <em>override</em>: it replaced the path-derived key, and every <c>@&lt;&lt;</c> that named the file
    /// stopped resolving and drew HED7011. Q8.25 corrects it. <c>Name</c> is <b>additive</b>: the template keeps its
    /// path-derived (or explicit <c>Key</c>) registration key <em>and</em> gains the registered name, so both spellings
    /// resolve and nothing that resolved before stops resolving.</para>
    /// <para><b>What that makes of each rule</b>, re-derived rather than carried over from the override premise:
    /// <list type="bullet">
    /// <item>The registration key, the manifest row, the generated entry-class identifier and the emitted
    /// <c>#line</c> file are all untouched by <c>Name</c>. Only <c>Key</c> moves the key.</item>
    /// <item>HED7002 and HED7003 are over keys only — a name registers no manifest row and is never a registry
    /// lookup, so it can neither duplicate a key nor case-shadow one.</item>
    /// <item>HED7018 is suppressed by <c>Key</c> only: an additive <c>Name</c> leaves the flattened out-of-root key
    /// in place, still unasked-for, so the warning is still about something real.</item>
    /// <item><c>Key</c> + <c>Name</c> is <b>not</b> a conflict — it is two names for one template, which is the whole
    /// point. The override implementation reported HED7004 for it; that report is gone.</item>
    /// <item>HED7004 keeps a <c>Name</c> arm for the two ways a name is <em>unusable</em>: a value the normalizer
    /// refuses, and a spelling another template already answers to. Neither un-precompiles the template — a broken
    /// addition costs the addition and nothing else.</item>
    /// <item>HED7028 is new: importing a named template by its key resolves, and says so.</item>
    /// </list></para>
    /// </summary>
    public class TemplateNameMetadataTests
    {
        private const string Root = "/repo/app";
        private const string ReportPath = "/repo/app/templates/report.heddle";

        /// <summary>An <c>@&lt;&lt;</c> pulls in <b>definitions</b>, not literal text, so "the import resolved" is
        /// asserted by the definition body turning up inlined in the importer's generated pieces — not merely by the
        /// absence of HED7011, which an implementation that silently dropped the import would also satisfy.</summary>
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

        // ---- Additive resolution: both spellings resolve --------------------------------------------------

        /// <summary>The correction itself (Q8.25). A named template imported <b>by its path</b> still resolves. This
        /// is the test that reddened under the override implementation, with exactly HED7011, and it is the whole
        /// reason the implementation changed: "nothing that resolved before may stop resolving".</summary>
        [Fact]
        public void ANamedTemplateIsStillImportableByItsPath()
        {
            var run = GeneratorHarness.Run(new[]
            {
                (ReportPath, DefinesBanner),
                ("/repo/app/page.heddle", Imports("templates/report.heddle"))
            }, globalOptions: RootOption, perFileOptions: Meta(ReportPath, name: "BuildReport"));

            Assert.DoesNotContain(run.GeneratorDiagnostics, d => d.Id == "HED7011");
            // Resolved, not merely un-diagnosed: the imported definition's body is inlined into the importer.
            Assert.Contains("BANNER-TEXT", Source(run, "class Page"));
        }

        /// <summary>The feature: the registered name resolves too. This is what the sample's csproj has been asking
        /// for since 2.0.</summary>
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

        /// <summary>The name is additive in the strict sense: with one <c>Name</c> set, <b>two</b> spellings resolve in
        /// one compilation. Asserted together so an implementation that merely swapped which one works cannot pass.
        /// </summary>
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

        /// <summary>The intended shape of the feature: a <c>Precompile="false"</c> import-only partial reachable under
        /// a friendly name. Both halves of the item metadata are exercised at once, and neither the flattened path nor
        /// the name registers a manifest row.</summary>
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

        /// <summary>A registered name never displaces a real key spelling: keys are registered first, by construction,
        /// so a name that happens to spell a sibling's path leaves that sibling reachable by its own path. The name
        /// itself is then unusable and draws HED7004 (below).</summary>
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

        // ---- What Name does NOT change ------------------------------------------------------------------

        /// <summary>The registration key is untouched: the manifest row and the generated entry class stay
        /// path-derived. Under the override implementation this test's expectations were the exact inverse.</summary>
        [Fact]
        public void NameDoesNotChangeTheRegistrationKeyOrTheEntryClass()
        {
            var run = GeneratorHarness.Run(new[] { (ReportPath, "hello\n") },
                globalOptions: RootOption, perFileOptions: Meta(ReportPath, name: "BuildReport"));

            Assert.Empty(run.GeneratorDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
            var manifest = Manifest(run);
            Assert.Contains("key: \"templates/report.heddle\"", manifest);
            Assert.DoesNotContain("BuildReport", manifest);
            Assert.Contains(run.GeneratedSourceTexts, s => s.Contains("class Templates_Report"));
            Assert.DoesNotContain(run.GeneratedSourceTexts, s => s.Contains("class BuildReport"));
        }

        /// <summary>An explicit <c>Key</c> still moves the key — <c>Name</c>'s correction did not touch that path.
        /// </summary>
        [Fact]
        public void KeyStillSetsTheRegistrationKey()
        {
            var run = GeneratorHarness.Run(new[] { (ReportPath, "hello\n") },
                globalOptions: RootOption, perFileOptions: Meta(ReportPath, key: "reports/Build"));

            Assert.Contains("key: \"reports/Build.heddle\"", Manifest(run));
        }

        /// <summary>The name goes through the shared normalizer exactly as a key does — it occupies the same
        /// import-path namespace — which is observable as the spelling an <c>@&lt;&lt;</c> must use to hit it.</summary>
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

        /// <summary>An empty <c>Name</c> is absent, not malformed — MSBuild materializes unset metadata as the empty
        /// string on every item, so treating "" as a request would red every build.</summary>
        [Fact]
        public void EmptyNameMetadataIsAbsent()
        {
            var run = GeneratorHarness.Run(new[] { (ReportPath, "hello\n") },
                globalOptions: RootOption, perFileOptions: Meta(ReportPath, key: "", name: ""));

            Assert.Empty(run.GeneratorDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
            Assert.Contains("key: \"templates/report.heddle\"", Manifest(run));
        }

        /// <summary><c>Key</c> and <c>Name</c> together are <b>two names for one template</b>, not a conflict: the key
        /// is what <c>Key</c> says, and the name is an extra import spelling on top. The override implementation
        /// reported HED7004 here, on the reasoning that there was "no defensible precedence between two equally
        /// explicit requests" — with <c>Name</c> additive there is no precedence to settle, because the two requests
        /// are about different things.</summary>
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

        // ---- HED7002 / HED7003: keys only ---------------------------------------------------------------

        /// <summary>A name is not a key, so it cannot duplicate one: HED7002's population is unchanged by the feature.
        /// The collision is real, but it is an <em>import-name</em> collision and is reported as HED7004 against the
        /// name — not as a duplicate key, which would wrongly un-precompile a template whose key is fine.</summary>
        [Fact]
        public void ANameThatSpellsASiblingsKeyIsNotHed7002()
        {
            var run = GeneratorHarness.Run(new[]
            {
                ("/repo/app/shared/banner.heddle", "a\n"),
                (ReportPath, "b\n")
            }, globalOptions: RootOption, perFileOptions: Meta(ReportPath, name: "shared/banner"));

            Assert.Empty(WithId(run, "HED7002"));
            // Both templates still precompile under their own keys.
            var manifest = Manifest(run);
            Assert.Contains("key: \"shared/banner.heddle\"", manifest);
            Assert.Contains("key: \"templates/report.heddle\"", manifest);
        }

        /// <summary>Nor can a name case-shadow a key: HED7003 exists because the precompiled registry's lookup is
        /// ordinal, and a name is never a registry lookup.</summary>
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

        /// <summary>The key-side checks themselves are untouched: an explicit <c>Key</c> colliding with a sibling's
        /// path-derived key is still HED7002, and a case-only twin is still HED7003.</summary>
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

        // ---- HED7004: the two ways a name is unusable ---------------------------------------------------

        /// <summary>A <c>Name</c> the normalizer rejects is an error, not a silent no-op: the user asked for an import
        /// name and did not get one. It does <b>not</b> un-precompile the template — the key is unaffected, so the
        /// broken addition costs only the addition.</summary>
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

        /// <summary>A malformed <c>Key</c> is still the pre-existing fault: the item asked for a registration key and
        /// gets none, so it contributes nothing.</summary>
        [Fact]
        public void MalformedKeyMetadataStillReportsHed7004AndUnprecompiles()
        {
            var run = GeneratorHarness.Run(new[] { (ReportPath, "hello\n") },
                globalOptions: RootOption, perFileOptions: Meta(ReportPath, key: "../escape"));

            var hed7004 = Assert.Single(WithId(run, "HED7004"));
            Assert.Contains("Key", hed7004.GetMessage());
            Assert.DoesNotContain("key: \"", Manifest(run));
        }

        /// <summary>A name another template's <b>key</b> already answers to cannot be registered — registering it
        /// would be the override this correction removes — so it draws HED7004 against the name.</summary>
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

        /// <summary>Two templates claiming one name: the second cannot have it, and says so. First-come is the only
        /// order the import map can honour, and the loser is told rather than silently ignored.</summary>
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

        /// <summary>A <c>Name</c> that spells the template's <b>own</b> key is a redundant request, not a collision:
        /// the preferred spelling and the path spelling are the same string, so there is nothing to add and nothing to
        /// complain about.</summary>
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

        // ---- HED7018: only Key suppresses --------------------------------------------------------------

        /// <summary>HED7018 says "your directory silently vanished from the key and you did not ask for that". An
        /// explicit <c>Key</c> <em>is</em> asking for the key it names, so it suppresses. An additive <c>Name</c> is
        /// not: the flattened key still exists, is still what the registry and the staleness check use, and is still
        /// unasked-for — so the warning is about something real and must stand. Q8.12 recorded the opposite (a
        /// <c>Name</c> suppressed it too), which was correct only while <c>Name</c> replaced the key.</summary>
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
            // And it names the flattened key that really did register, not the name.
            Assert.Contains("banner.heddle", hed7018.GetMessage());
            Assert.Contains("key: \"banner.heddle\"", Manifest(withName));
        }

        // ---- HED7028: the advisory ----------------------------------------------------------------------

        /// <summary>HED7028: importing a named template by its key works, and the build says the name-first spelling
        /// is preferred. A warning at the importer's <c>@&lt;&lt;{{…}}</c> block — guidance, never a break, which is
        /// asserted by the absence of any error and by the import having actually resolved.</summary>
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

        /// <summary>The advisory is positioned at the import block the author typed, not at the file — otherwise it
        /// cannot be acted on in a template with several imports.</summary>
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

        /// <summary>Two <c>@&lt;&lt;</c>s naming one spelling advise <b>once</b>. The reader is called per import, and
        /// the position is resolved by finding the first block that names the path — so an undeduplicated report would
        /// stack two identical warnings on one block, which is noise pointing at the wrong place.</summary>
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

        /// <summary>Importing by the preferred spelling is silent — the advisory exists to move authors to it, so it
        /// must not fire once they have moved.</summary>
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

        /// <summary>An unnamed template imported by path is silent: the advisory is about named templates, and every
        /// pre-existing project is unnamed. This is the "no new warning noise for anyone who did not opt in"
        /// assertion.</summary>
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

        /// <summary>A name that could not be registered advises nothing: there is no preferred spelling to move to, so
        /// HED7004 is the whole report and HED7028 stays silent rather than pointing at a name that does not resolve.
        /// </summary>
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

        // ---- #line: the file, and its relativity (Q8.12 separation, Q8.27 form) ------------------------

        /// <summary>The <c>#line</c> file names the <b>file</b>, the key names the <b>registration</b>. Conflating them
        /// was invisible while every key was path-derived — the two strings were equal — and an explicit <c>Key</c>
        /// makes it observable: the generated code's mapped spans pointed at a path that exists nowhere. A <c>Name</c>
        /// cannot reach this at all any more, which is itself worth pinning.</summary>
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

        /// <summary>Q8.27, the relativity marking. Under the template root the <c>#line</c> file is root-relative and
        /// the generated file <b>says so</b>: an absolute path would be right for one machine and would bake that
        /// machine's layout into every checked-in generated-source golden, so the form stays relative and is labelled
        /// instead of being churned.</summary>
        [Fact]
        public void UnderTheRootTheLineFileIsRootRelativeAndMarkedAsSuch()
        {
            var run = GeneratorHarness.Run(new[] { (ReportPath, "@model(){{System.String}}@\\\nx @(this) y\n") },
                globalOptions: RootOption);

            var source = Source(run, "class Templates_Report");
            Assert.Contains("// #line file names below are RELATIVE to HeddleTemplateRoot.", source);
            Assert.Contains("\"templates/report.heddle\"", source);
        }

        /// <summary>Q8.27, the absolute half. Outside the root there is no anchor to be relative to, so the template's
        /// own path is emitted — absolute in a real build, which is what a <c>#line</c> is for — and the marker says
        /// which form it is. This replaces a bare-filename fallback that named no openable file and collided across
        /// directories.</summary>
        [Fact]
        public void OutsideTheRootTheLineFileIsTheTemplatesOwnPathAndMarkedAsSuch()
        {
            const string outside = "/repo/shared/banner.heddle";
            var run = GeneratorHarness.Run(new[] { (outside, "@model(){{System.String}}@\\\nx @(this) y\n") },
                globalOptions: RootOption);

            var source = Source(run, "class Banner");
            Assert.Contains("// #line file names below are the template's own path", source);
            Assert.Contains("\"" + outside + "\"", source);
            // The old form was the bare filename, which the compiler could not open.
            Assert.DoesNotContain("\"banner.heddle\"", source);
        }
    }
}
