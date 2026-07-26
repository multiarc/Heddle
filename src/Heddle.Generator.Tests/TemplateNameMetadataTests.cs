using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Heddle.Generator.Tests
{
    /// <summary>
    /// <para>Q8.12: the <c>Name</c> item metadata as an <b>optional custom key mapping</b>. Phase 5 deleted the
    /// metadata on a register entry reading "<c>Name</c> removed per the recommendation"; the ask had only ever been
    /// to wire <c>Precompile</c>. <c>Name</c> was genuinely dead — declared <c>CompilerVisibleItemMetadata</c>, never
    /// mapped onto <c>AdditionalFiles</c> by <c>Heddle.Generator.targets</c>, never read — so its removal changed no
    /// behaviour and <c>samples/codegen-t4-successor</c>'s <c>Name="BuildReport"</c> was always ignored. The defect
    /// was that the feature was never wired, not that the metadata existed.</para>
    /// <para><b>The semantics.</b> <c>Key</c> and <c>Name</c> are two spellings of one setting — "the explicit
    /// registration key for this item" — so they share every downstream rule: the same
    /// <see cref="Heddle.Precompiled.TemplateKey"/> normalisation, the same HED7002 duplicate check, the same HED7003
    /// case-only-twin check, the same HED7018 suppression (an explicit key means the flattened out-of-root key is
    /// <em>intentional</em>, which is the whole premise of that warning), and the same HED7004 on a value the
    /// normaliser rejects. Naming the one setting twice with two values that mean two keys is itself a fault, and it
    /// draws HED7004 too: the item's explicit key metadata is unusable, which is exactly what HED7004 says.</para>
    /// </summary>
    public class TemplateNameMetadataTests
    {
        private const string Root = "/repo/app";
        private const string ReportPath = "/repo/app/templates/report.heddle";

        private static Dictionary<string, string> RootOption =>
            new Dictionary<string, string> { ["build_property.HeddleTemplateRoot"] = Root };

        private static Dictionary<string, Dictionary<string, string>> Meta(string path,
            string key = null, string name = null)
        {
            var per = new Dictionary<string, string>();
            if (key != null)
                per["build_metadata.AdditionalFiles.Key"] = key;
            if (name != null)
                per["build_metadata.AdditionalFiles.Name"] = name;
            return new Dictionary<string, Dictionary<string, string>> { [path] = per };
        }

        private static string Manifest(GeneratorRun run) =>
            run.GeneratedSourceTexts.First(s => s.Contains("__HeddleManifest"));

        /// <summary>The feature itself: <c>Name</c> overrides the path-derived key, in the manifest row and in the
        /// generated entry class the key sanitizes to. This is the assertion the sample's csproj has been asking for
        /// since 2.0.</summary>
        [Fact]
        public void NameMetadataSetsTheRegistrationKey()
        {
            var run = GeneratorHarness.Run(new[] { (ReportPath, "hello\n") },
                globalOptions: RootOption, perFileOptions: Meta(ReportPath, name: "BuildReport"));

            Assert.Empty(run.GeneratorDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
            var manifest = Manifest(run);
            Assert.Contains("key: \"BuildReport.heddle\"", manifest);
            Assert.DoesNotContain("templates/report.heddle", manifest);
            Assert.Contains(run.GeneratedSourceTexts, s => s.Contains("class BuildReport"));
        }

        /// <summary><c>Name</c> goes through the shared normaliser exactly as <c>Key</c> does: host path idioms are
        /// unified, the extension is appended, case is preserved.</summary>
        [Theory]
        [InlineData("BuildReport", "BuildReport.heddle")]
        [InlineData("reports\\Build", "reports/Build.heddle")]
        [InlineData("~/reports//Build", "reports/Build.heddle")]
        [InlineData("Reports/build.txt", "Reports/build.txt")]
        public void NameMetadataIsNormalizedByTheSharedRule(string metadata, string expectedKey)
        {
            var run = GeneratorHarness.Run(new[] { (ReportPath, "hello\n") },
                globalOptions: RootOption, perFileOptions: Meta(ReportPath, name: metadata));

            Assert.Contains("key: \"" + expectedKey + "\"", Manifest(run));
        }

        /// <summary>A <c>Name</c> the normaliser rejects is an error, not a silent un-precompile: the user asked for
        /// a key and did not get one. Same id and same shape as the <c>Key</c> arm — the message names which
        /// metadata carried the bad value.</summary>
        [Theory]
        [InlineData("../escape")]
        [InlineData("  ")]
        [InlineData("a/./b")]
        public void MalformedNameMetadataReportsHed7004(string metadata)
        {
            var run = GeneratorHarness.Run(new[] { (ReportPath, "hello\n") },
                globalOptions: RootOption, perFileOptions: Meta(ReportPath, name: metadata));

            var hed7004 = Assert.Single(run.GeneratorDiagnostics.Where(d => d.Id == "HED7004"));
            Assert.Equal(DiagnosticSeverity.Error, hed7004.Severity);
            Assert.Contains("Name", hed7004.GetMessage());
            Assert.Contains(ReportPath, hed7004.GetMessage());
            // The template contributes nothing: no manifest row under a guessed key.
            Assert.DoesNotContain("key: \"", Manifest(run));
        }

        /// <summary>Naming the one setting twice, with two values that mean two different keys, is a fault the build
        /// must refuse: picking either silently would be the "generator quietly chose for you" failure mode the
        /// program exists to eliminate, and there is no defensible precedence between two equally explicit
        /// requests.</summary>
        [Fact]
        public void KeyAndNameThatDisagreeReportHed7004()
        {
            var run = GeneratorHarness.Run(new[] { (ReportPath, "hello\n") },
                globalOptions: RootOption, perFileOptions: Meta(ReportPath, key: "one", name: "two"));

            var hed7004 = Assert.Single(run.GeneratorDiagnostics.Where(d => d.Id == "HED7004"));
            Assert.Equal(DiagnosticSeverity.Error, hed7004.Severity);
            Assert.Contains("one", hed7004.GetMessage());
            Assert.Contains("two", hed7004.GetMessage());
            Assert.DoesNotContain("key: \"", Manifest(run));
        }

        /// <summary>Two spellings that normalize to the same key are one request, not a conflict — the redundancy is
        /// harmless and must not be an error.</summary>
        [Fact]
        public void KeyAndNameThatAgreeAfterNormalizationAreAccepted()
        {
            var run = GeneratorHarness.Run(new[] { (ReportPath, "hello\n") },
                globalOptions: RootOption,
                perFileOptions: Meta(ReportPath, key: "reports/Build", name: "reports\\Build"));

            Assert.Empty(run.GeneratorDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
            Assert.Contains("key: \"reports/Build.heddle\"", Manifest(run));
        }

        /// <summary>An empty <c>Name</c> is absent, not malformed — MSBuild materializes unset metadata as the empty
        /// string on every item, so treating "" as a request would red every build.</summary>
        [Fact]
        public void EmptyNameMetadataIsAbsentAndTheKeyStaysPathDerived()
        {
            var run = GeneratorHarness.Run(new[] { (ReportPath, "hello\n") },
                globalOptions: RootOption, perFileOptions: Meta(ReportPath, key: "", name: ""));

            Assert.Empty(run.GeneratorDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
            Assert.Contains("key: \"templates/report.heddle\"", Manifest(run));
        }

        /// <summary>HED7002: a <c>Name</c> is not exempt from the duplicate-key check — it is the ordinary way to
        /// collide with a sibling's path-derived key, and the collision is reported at the same id as any other.</summary>
        [Fact]
        public void ANameThatCollidesWithAnotherTemplatesKeyReportsHed7002()
        {
            var run = GeneratorHarness.Run(new[]
            {
                ("/repo/app/shared/banner.heddle", "a\n"),
                (ReportPath, "b\n")
            }, globalOptions: RootOption, perFileOptions: Meta(ReportPath, name: "shared/banner"));

            var hed7002 = Assert.Single(run.GeneratorDiagnostics.Where(d => d.Id == "HED7002"));
            Assert.Contains("shared/banner.heddle", hed7002.GetMessage());
        }

        /// <summary>HED7003: a <c>Name</c> differing from a sibling key only by case still shadows it under the
        /// ordinal registry lookup, so the warning fires for the <c>Name</c>-derived key too.</summary>
        [Fact]
        public void ANameThatDiffersOnlyByCaseFromAnotherKeyReportsHed7003()
        {
            var run = GeneratorHarness.Run(new[]
            {
                ("/repo/app/shared/banner.heddle", "a\n"),
                (ReportPath, "b\n")
            }, globalOptions: RootOption, perFileOptions: Meta(ReportPath, name: "shared/Banner"));

            var hed7003 = Assert.Single(run.GeneratorDiagnostics.Where(d => d.Id == "HED7003"));
            Assert.Equal(DiagnosticSeverity.Warning, hed7003.Severity);
            Assert.Contains("shared/banner.heddle", hed7003.GetMessage());
            Assert.Contains("shared/Banner.heddle", hed7003.GetMessage());
        }

        /// <summary>The <c>#line</c> file names the <b>file</b>, the key names the <b>registration</b>. Conflating
        /// them was invisible while every key was path-derived — the two strings were equal — and wiring <c>Name</c>
        /// made it observable: the generated code's mapped spans pointed at <c>BuildReport.heddle</c>, a path that
        /// exists nowhere. Byte-neutral for a path-derived key, which is why no snapshot moved.</summary>
        [Fact]
        public void TheLineDirectiveNamesTheFileNotTheOverriddenKey()
        {
            var withName = GeneratorHarness.Run(new[] { (ReportPath, "@model(){{System.String}}@\\\nx @(this) y\n") },
                globalOptions: RootOption, perFileOptions: Meta(ReportPath, name: "BuildReport"));
            var body = withName.GeneratedSourceTexts.First(s => s.Contains("class BuildReport"));
            Assert.Contains("\"templates/report.heddle\"", body);
            Assert.DoesNotContain("\"BuildReport.heddle\"", body);

            // And with no explicit key the two are the same string, so nothing existing moved.
            var plain = GeneratorHarness.Run(new[] { (ReportPath, "@model(){{System.String}}@\\\nx @(this) y\n") },
                globalOptions: RootOption);
            Assert.Contains("\"templates/report.heddle\"",
                plain.GeneratedSourceTexts.First(s => s.Contains("class Templates_Report")));
        }

        /// <summary>HED7018 exists to say "your directory silently vanished from the key and you did not ask for
        /// that". An explicit <c>Name</c> <b>is</b> asking for that, so the warning must not fire — the same
        /// suppression <c>Key</c> already had, now decided deliberately rather than inherited by accident.</summary>
        [Fact]
        public void AnExplicitNameSuppressesTheOutOfRootWarning()
        {
            const string outside = "/repo/shared/banner.heddle";

            var withoutName = GeneratorHarness.Run(new[] { (outside, "hello\n") }, globalOptions: RootOption);
            Assert.Single(withoutName.GeneratorDiagnostics.Where(d => d.Id == "HED7018"));

            var withName = GeneratorHarness.Run(new[] { (outside, "hello\n") },
                globalOptions: RootOption, perFileOptions: Meta(outside, name: "BuildReport"));
            Assert.Empty(withName.GeneratorDiagnostics.Where(d => d.Id == "HED7018"));
            Assert.Contains("key: \"BuildReport.heddle\"", Manifest(withName));
        }
    }
}
