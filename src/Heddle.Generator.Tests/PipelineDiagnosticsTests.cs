using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Heddle.Generator.Diagnostics;
using Heddle.Generator.Emit;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Heddle.Generator.Tests
{
    /// <summary>
    /// Build-tier gates: the two new diagnostics (`HED7018` out-of-root key, `HED7019` engine-version fallback),
    /// the `Precompile` per-item opt-out, the emitter-defect error path that replaced the blanket
    /// <c>catch (Exception)</c>, and the registry lockstep that keeps the descriptor set, the claimed-IDs registry,
    /// and the docs table from drifting apart.
    /// </summary>
    public class PipelineDiagnosticsTests
    {
        private const string Simple = "@model(){{System.String}}@\\\nHello @(this)!\n";

        private static Dictionary<string, string> Root(string root) =>
            new Dictionary<string, string> { ["build_property.HeddleTemplateRoot"] = root };

        [Fact]
        public void OutOfRootTemplateReportsHed7018OnceAndStillRegistersTheFlattenedKey()
        {
            var run = GeneratorHarness.Run(new[] { ("/repo/shared/banner.heddle", Simple) },
                globalOptions: Root("/repo/app"));

            var hed7018 = Assert.Single(run.GeneratorDiagnostics.Where(d => d.Id == "HED7018"));
            Assert.Equal(DiagnosticSeverity.Warning, hed7018.Severity);
            var message = hed7018.GetMessage();
            Assert.Contains("/repo/shared/banner.heddle", message);
            Assert.Contains("/repo/app", message);
            Assert.Contains("banner.heddle", message);

            // Behavior is unchanged: the flattened key still registers.
            var manifest = run.GeneratedSourceTexts.First(s => s.Contains("__HeddleManifest"));
            Assert.Contains("key: \"banner.heddle\"", manifest);
        }

        [Fact]
        public void InRootTemplateDoesNotReportHed7018()
        {
            var run = GeneratorHarness.Run(new[] { ("/repo/app/views/home.heddle", Simple) },
                globalOptions: Root("/repo/app"));

            Assert.DoesNotContain(run.GeneratorDiagnostics, d => d.Id == "HED7018");
            var manifest = run.GeneratedSourceTexts.First(s => s.Contains("__HeddleManifest"));
            Assert.Contains("key: \"views/home.heddle\"", manifest);
        }

        /// <summary>An explicit `Key` is the documented remedy, so it suppresses the warning by construction — the
        /// flattening path is only reached when no key was set.</summary>
        [Fact]
        public void ExplicitKeyMetadataSuppressesHed7018()
        {
            var run = GeneratorHarness.Run(new[] { ("/repo/shared/banner.heddle", Simple) },
                globalOptions: Root("/repo/app"),
                perFileOptions: new Dictionary<string, Dictionary<string, string>>
                {
                    ["/repo/shared/banner.heddle"] = new Dictionary<string, string>
                    {
                        ["build_metadata.AdditionalFiles.Key"] = "shared/banner.heddle"
                    }
                });

            Assert.DoesNotContain(run.GeneratorDiagnostics, d => d.Id == "HED7018");
        }

        /// <summary>The scenario that made HED7002 look inexplicable: two out-of-root files sharing a filename now
        /// draw two HED7018 warnings that explain the duplicate.</summary>
        [Fact]
        public void TwoOutOfRootFilesWithOneFilenameWarnTwiceThenReportHed7002()
        {
            var run = GeneratorHarness.Run(new[]
            {
                ("/repo/a/index.heddle", Simple),
                ("/repo/b/index.heddle", Simple),
            }, globalOptions: Root("/repo/app"));

            Assert.Equal(2, run.GeneratorDiagnostics.Count(d => d.Id == "HED7018"));
            Assert.Single(run.GeneratorDiagnostics.Where(d => d.Id == "HED7002"));
        }

        /// <summary>The harness compiles without a visible <c>Heddle</c> reference identity in
        /// <c>ReferencedAssemblySymbols</c> only when the reference is absent; with it present the manifest records
        /// the observed version and no warning fires.</summary>
        [Fact]
        public void VisibleHeddleReferenceEmitsTheObservedVersionAndNoHed7019()
        {
            var run = GeneratorHarness.Run(new[] { ("/repo/app/home.heddle", Simple) },
                globalOptions: Root("/repo/app"));

            Assert.DoesNotContain(run.GeneratorDiagnostics, d => d.Id == "HED7019");
            var manifest = run.GeneratedSourceTexts.First(s => s.Contains("__HeddleManifest"));
            Assert.Contains("engineVersion: \"2.1.0\"", manifest);
        }

        /// <summary>Without the engine reference the generator no longer fabricates a literal: it emits its own
        /// version (identical today, correct in every future release) and says so with HED7019.</summary>
        [Fact]
        public void MissingHeddleReferenceEmitsTheGeneratorVersionAndReportsHed7019()
        {
            var run = GeneratorHarness.RunWithoutHeddleReference(new[] { ("/repo/app/home.heddle", Simple) },
                globalOptions: Root("/repo/app"));

            var hed7019 = Assert.Single(run.GeneratorDiagnostics.Where(d => d.Id == "HED7019"));
            Assert.Equal(DiagnosticSeverity.Warning, hed7019.Severity);

            var self = typeof(HeddleTemplateGenerator).Assembly.GetName().Version;
            var expected = $"{self.Major}.{self.Minor}.{self.Build}";
            Assert.Contains(expected, hed7019.GetMessage());

            var manifest = run.GeneratedSourceTexts.First(s => s.Contains("__HeddleManifest"));
            Assert.Contains($"engineVersion: \"{expected}\"", manifest);
        }

        /// <summary>The allowed-value lists used to be hand-copied string arrays 2,500 lines from the enums they
        /// mirrored; they now come from <c>Enum.GetNames</c> over the linked types. The diagnostic text must read
        /// exactly as it shipped.</summary>
        [Theory]
        [InlineData("HeddleOutputProfile", "WebForms", "Text|Html")]
        [InlineData("HeddleExpressionMode", "Roslyn", "MemberPathsOnly|Native|FullCSharp")]
        [InlineData("HeddleTrimDirectiveLines", "maybe", "true|false")]
        [InlineData("HeddleMaxRecursionCount", "0", "a positive integer")]
        public void UnparsableOptionValuesReportTheShippedExpectedValuesText(string property, string value,
            string expected)
        {
            var run = GeneratorHarness.Run(new[] { ("/repo/app/home.heddle", Simple) },
                globalOptions: new Dictionary<string, string>
                {
                    ["build_property.HeddleTemplateRoot"] = "/repo/app",
                    ["build_property." + property] = value
                });

            var hed7009 = Assert.Single(run.GeneratorDiagnostics.Where(d => d.Id == "HED7009"));
            var message = hed7009.GetMessage();
            Assert.Contains(value, message);
            Assert.Contains(property, message);
            Assert.Contains(expected, message);
        }

        [Fact]
        public void PrecompileFalseEmitsNoEntryPointAndNoManifestEntryButStillServesImports()
        {
            var run = GeneratorHarness.Run(new[]
            {
                ("/repo/app/_layout.heddle", "layout\n"),
                ("/repo/app/page.heddle", "@<<{{_layout.heddle}}@\\\npage\n"),
            }, globalOptions: Root("/repo/app"),
                perFileOptions: new Dictionary<string, Dictionary<string, string>>
                {
                    ["/repo/app/_layout.heddle"] = new Dictionary<string, string>
                    {
                        ["build_metadata.AdditionalFiles.Precompile"] = "false"
                    }
                });

            // The importer still resolves the import — no HED7011.
            Assert.DoesNotContain(run.GeneratorDiagnostics, d => d.Id == "HED7011");

            var manifest = run.GeneratedSourceTexts.First(s => s.Contains("__HeddleManifest"));
            Assert.DoesNotContain("key: \"_layout.heddle\"", manifest);
            Assert.Contains("key: \"page.heddle\"", manifest);
            Assert.DoesNotContain(run.GeneratedSourceTexts, s => s.Contains("class _layout"));
        }

        [Theory]
        [InlineData("true")]
        [InlineData("")]
        [InlineData("nonsense")]
        public void AbsentOrNonFalsePrecompileMetadataIsTodaysBehavior(string metadata)
        {
            var run = GeneratorHarness.Run(new[] { ("/repo/app/page.heddle", Simple) },
                globalOptions: Root("/repo/app"),
                perFileOptions: new Dictionary<string, Dictionary<string, string>>
                {
                    ["/repo/app/page.heddle"] = new Dictionary<string, string>
                    {
                        ["build_metadata.AdditionalFiles.Precompile"] = metadata
                    }
                });

            var manifest = run.GeneratedSourceTexts.First(s => s.Contains("__HeddleManifest"));
            Assert.Contains("key: \"page.heddle\"", manifest);
        }

        /// <summary>The fallback-legitimacy ruling in executable form: an exception out of the emitter is a defect,
        /// so it reds the build with a per-template error naming the template and the exception — while every other
        /// template's source and the manifest still emit (a bare rethrow would downgrade to the CS8785 warning and
        /// discard the generator's entire contribution).</summary>
        [Fact]
        public void EmitterDefectSurfacesAsAnErrorAndTheRestOfThePassStillEmits()
        {
            var templates = Enumerable.Range(0, 10)
                .Select(i => ($"/repo/app/t{i}.heddle", Simple))
                .ToList();

            TemplateEmitter.FaultInjector = key =>
            {
                if (key == "t3.heddle")
                    throw new InvalidOperationException("seeded emitter defect");
            };

            GeneratorRun run;
            try
            {
                run = GeneratorHarness.Run(templates, globalOptions: Root("/repo/app"));
            }
            finally
            {
                TemplateEmitter.FaultInjector = null;
            }

            var error = Assert.Single(run.GeneratorDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
            Assert.Equal("HED7020", error.Id);
            Assert.Contains("t3.heddle", error.GetMessage());
            Assert.Contains(nameof(InvalidOperationException), error.GetMessage());
            Assert.Contains("seeded emitter defect", error.GetMessage());

            var manifest = run.GeneratedSourceTexts.First(s => s.Contains("__HeddleManifest"));
            Assert.DoesNotContain("key: \"t3.heddle\"", manifest);
            for (var i = 0; i < 10; i++)
            {
                if (i == 3) continue;
                Assert.Contains($"key: \"t{i}.heddle\"", manifest);
            }
        }

        /// <summary>Code ↔ claimed-IDs registry ↔ docs table. Every `HED70xx` the generator can report must be claimed
        /// and listed in the build-time diagnostics documentation; an unclaimed or undocumented id is a red build
        /// rather than a review miss.</summary>
        [Fact]
        public void EveryGeneratorDiagnosticIdIsClaimedInTheRegistryAndListedInTheDocsTable()
        {
            var declared = typeof(GeneratorDiagnostics)
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.FieldType == typeof(DiagnosticDescriptor))
                .Select(f => ((DiagnosticDescriptor)f.GetValue(null)).Id)
                .Where(id => id.StartsWith("HED70", StringComparison.Ordinal))
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToList();
            Assert.NotEmpty(declared);

            var claimed = ClaimedIds(ReadRepoFile(Path.Combine("docs", "spec", "common", "cross-cutting-decisions.md")));
            var documented = DocumentedIds(ReadRepoFile(Path.Combine("docs", "precompilation.md")));

            var unclaimed = declared.Where(id => !claimed.Contains(id)).ToList();
            Assert.True(unclaimed.Count == 0,
                "Diagnostic ids not claimed in the registry: " + string.Join(", ", unclaimed));

            var undocumented = declared.Where(id => !documented.Contains(id)).ToList();
            Assert.True(undocumented.Count == 0,
                "Diagnostic ids missing from the precompilation.md table: " + string.Join(", ", undocumented));

            // And nothing is documented that no descriptor can report.
            var orphaned = documented.Where(id => !declared.Contains(id)).ToList();
            Assert.True(orphaned.Count == 0,
                "Documented ids with no descriptor: " + string.Join(", ", orphaned));
        }

        /// <summary>Expands the registry table's ids, including its `HEDaaaa`–`HEDbbbb` range rows.</summary>
        private static HashSet<string> ClaimedIds(string markdown)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (Match match in Regex.Matches(markdown, @"`HED(?<from>\d{4})`(?:[–-]`HED(?<to>\d{4})`)?"))
            {
                var from = int.Parse(match.Groups["from"].Value);
                var to = match.Groups["to"].Success ? int.Parse(match.Groups["to"].Value) : from;
                for (var i = from; i <= to; i++)
                    ids.Add("HED" + i.ToString("D4"));
            }

            return ids;
        }

        private static HashSet<string> DocumentedIds(string markdown)
        {
            var table = markdown.Substring(markdown.IndexOf("## Build‑time diagnostics", StringComparison.Ordinal));
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (Match match in Regex.Matches(table, @"^\| `HED(?<id>70\d{2})`", RegexOptions.Multiline))
                ids.Add("HED" + match.Groups["id"].Value);
            // The forwarded-error/warning row lists two ids in one cell.
            foreach (Match match in Regex.Matches(table, @"`HED(?<id>70\d{2})`/`HED(?<id2>70\d{2})`"))
            {
                ids.Add("HED" + match.Groups["id"].Value);
                ids.Add("HED" + match.Groups["id2"].Value);
            }

            return ids;
        }

        private static string ReadRepoFile(string relativePath)
        {
            var dir = new DirectoryInfo(Path.GetDirectoryName(typeof(PipelineDiagnosticsTests).Assembly.Location));
            while (dir != null)
            {
                var candidate = Path.Combine(dir.FullName, relativePath);
                if (File.Exists(candidate))
                    return File.ReadAllText(candidate);
                dir = dir.Parent;
            }

            throw new FileNotFoundException($"Could not locate '{relativePath}' above the test assembly.");
        }
    }
}
