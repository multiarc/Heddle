using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Heddle.Data;
using Heddle.Runtime;
using Heddle.TestCorpus;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// The gates that keep the shared corpus and its declared intent from drifting apart, asserted in
    /// the engine tier (which owns the corpus directory).
    /// <para>Every gate here is <b>set equality</b>. None is a count and none is a floor, and that is not stylistic
    /// fastidiousness: this repository shipped a <c>&gt;= 25</c> floor against an actual 40 and lost fifteen templates
    /// in silence, then shipped a <c>&gt;= 40</c> floor against an actual 62 next to a comment claiming "~45". A count
    /// says a number changed; it never says which file, and it is satisfied by editing a digit. A set difference has
    /// to name the file, which is the review artifact the count was reaching for and structurally could not
    /// produce.</para>
    /// </summary>
    public class CorpusIntentGateTests
    {
        /// <summary>
        /// Verifies template-intent row correspondence: every corpus template has exactly one row, and vice versa.
        /// </summary>
        [Fact]
        public void EveryCorpusTemplateHasExactlyOneIntentRowAndViceVersa()
        {
            var onDisk = TestCorpusIndex.Names();
            var declared = CorpusIntent.DeclaredNames();
            Assert.True(new HashSet<string>(onDisk, StringComparer.Ordinal).SetEquals(declared),
                CorpusIntent.Describe("The corpus intent table", declared, onDisk));
        }

        /// <summary><c>Why</c> is mandatory and non-empty. A classification with no stated reason is a rubber
        /// stamp, and the whole value of declaring intent is that contributing a template requires saying what it is
        /// for.</summary>
        [Fact]
        public void EveryIntentRowCarriesANonEmptyWhy()
        {
            var blank = CorpusIntent.Rows.Where(r => string.IsNullOrWhiteSpace(r.Why))
                .Select(r => r.Name).OrderBy(n => n, StringComparer.Ordinal).ToList();
            Assert.True(blank.Count == 0,
                "These corpus intent rows have no Why: " + string.Join(", ", blank));
        }

        /// <summary>
        /// Verifies templates carry a UTF-8 BOM iff declared. BOM and line-ending are independent pins;
        /// <c>.gitattributes</c> <c>eol=lf</c> controls only line endings.
        /// </summary>
        [Fact]
        public void CorpusTemplatesCarryABomExactlyWhenTheirRowDeclaresOne()
        {
            var declared = CorpusIntent.BomNames();
            var observed = TestCorpusIndex.Names().Where(TestCorpusIndex.HasUtf8Bom)
                .OrderBy(n => n, StringComparer.Ordinal).ToList();
            Assert.True(new HashSet<string>(observed, StringComparer.Ordinal).SetEquals(declared),
                CorpusIntent.Describe("The BOM-bearing template set", declared, observed)
                + "\n  A BOM and an LF pin are independent facts: .gitattributes covers newlines only.");
        }

        /// <summary>
        /// Verifies no golden file carries a BOM, which would become a silent three-byte prefix in byte comparisons.
        /// </summary>
        [Fact]
        public void NoCorpusGoldenCarriesABom()
        {
            var withBom = TestCorpusIndex.GoldenNames().Where(TestCorpusIndex.HasUtf8Bom)
                .OrderBy(n => n, StringComparer.Ordinal).ToList();
            Assert.True(withBom.Count == 0,
                "These corpus goldens carry a UTF-8 BOM, which no golden should: " + string.Join(", ", withBom));
        }

        /// <summary>
        /// Verifies no test writes to the shared corpus directory, which is input, not output, to preserve byte neutrality.
        /// </summary>
        [Fact]
        public void TheCorpusDirectoryHoldsNoTestWrittenArtifact()
        {
            var written = TestCorpusIndex.GoldenNames()
                .Where(n => n.StartsWith("test", StringComparison.Ordinal))
                .OrderBy(n => n, StringComparer.Ordinal).ToList();
            Assert.True(written.Count == 0,
                "The shared corpus directory holds files a test wrote into it: " + string.Join(", ", written) +
                ". The corpus is INPUT. Write rendered artifacts with TestCorpusIndex.WrittenArtifactPath(name), " +
                "which puts them in this project's own TestOutput/ folder instead.");
        }

        /// <summary>
        /// Verifies the shared corpus is present in this project's output directory, a canary for build-wiring failures.
        /// </summary>
        [Fact]
        public void TheCorpusIsInThisProjectsOwnOutputDirectory()
        {
            Assert.StartsWith(AppContext.BaseDirectory, TestCorpusIndex.CorpusDir, StringComparison.Ordinal);
            Assert.True(Directory.Exists(TestCorpusIndex.CorpusDir));
            Assert.NotEmpty(TestCorpusIndex.Templates);
        }

        /// <summary>
        /// Verifies the model table covers exactly the rows that need a model: every <c>WithModel</c> intent
        /// row has a <c>CorpusModels</c> entry, and no entry names a row that is not <c>WithModel</c>.
        /// </summary>
        [Fact]
        public void CorpusModelsCoversExactlyWithModelRows()
        {
            var declared = CorpusModels.DeclaredNames();
            var observed = CorpusIntent.NamesWithRender(CorpusRender.WithModel);
            Assert.True(new HashSet<string>(observed, StringComparer.Ordinal).SetEquals(declared),
                CorpusIntent.Describe("The CorpusModels table", declared, observed));
        }

        /// <summary>
        /// The build-tier gate (P1-R8): every compiling row is compiled exactly as the build compiles it —
        /// deferred unbound functions, form recording, the row's model and mode, the corpus fixture extensions
        /// — and the record's <c>RefusalSites</c> classes must equal the row's declared <c>refusals</c> as a
        /// set. A template the build refuses for any other reason, or with any other class, fails here by name.
        /// </summary>
        [Fact]
        public void EachCompilingRowRecordsExactlyItsDeclaredRefusals()
        {
            CorpusExtensionFixtures.Register();
            var drift = new List<string>();
            foreach (var row in CorpusIntent.Rows.Where(r => r.Tier != CorpusTier.EngineError))
            {
                string outcome;
                try
                {
                    var sites = BuildRow(row, out var errors);
                    if (errors != null)
                    {
                        outcome = "build errored: " + errors;
                    }
                    else
                    {
                        var declared = new SortedSet<string>(
                            row.Refusals.Select(r => r.ToString()), StringComparer.Ordinal);
                        var observed = new SortedSet<string>(
                            sites.Select(s => s.Class.ToString()), StringComparer.Ordinal);
                        outcome = declared.SetEquals(observed)
                            ? null
                            : "refusals [" + string.Join(", ", observed) + "] != declared ["
                              + string.Join(", ", declared) + "]";
                    }
                }
                catch (Exception e)
                {
                    outcome = "threw " + (e.InnerException ?? e).GetType().Name + ": "
                        + (e.InnerException ?? e).Message.Split('\n')[0];
                }

                if (outcome != null)
                    drift.Add(row.Name + ": " + outcome);
            }

            Assert.True(drift.Count == 0,
                "These rows drifted from the declared intent table (src/TestCorpus/CorpusIntent.cs):\n  "
                + string.Join("\n  ", drift)
                + "\n  Fix by changing the code, or by changing that file's row and saying why in its Why.");
        }

        /// <summary>
        /// The other half of the build-tier gate: every <c>EngineError</c> row must fail the same build the
        /// compiling rows pass — the engine refuses it under the row's mode, so no artifact exists. A row
        /// that starts compiling has changed tiers, not gotten lucky.
        /// </summary>
        [Fact]
        public void EngineErrorRowsFailTheBuild()
        {
            CorpusExtensionFixtures.Register();
            var drift = new List<string>();
            foreach (var row in CorpusIntent.Rows.Where(r => r.Tier == CorpusTier.EngineError))
            {
                string outcome;
                try
                {
                    BuildRow(row, out var errors);
                    outcome = errors == null ? "compiled cleanly; it is not an EngineError row" : null;
                }
                catch
                {
                    outcome = null; // A throw is also the build refusing the template.
                }

                if (outcome != null)
                    drift.Add(row.Name + ": " + outcome);
            }

            Assert.True(drift.Count == 0,
                "These EngineError rows compiled under the build (src/TestCorpus/CorpusIntent.cs):\n  "
                + string.Join("\n  ", drift)
                + "\n  Re-classify the row (Compiles, with the mode and model that save it) and say why in its Why.");
        }

        /// <summary>
        /// Compiles one row exactly as the build tier does and returns the record's refusal sites, or a
        /// non-null error summary when the build refuses the template. Inline text over the corpus directory
        /// (so imports resolve), the row's model (<c>CorpusModels</c> for <c>WithModel</c>, dynamic otherwise)
        /// and the row's mode.
        /// </summary>
        private static IList<Heddle.Precompiled.PrecompiledRefusalSite> BuildRow(
            CorpusIntentRow row, out string errors)
        {
            errors = null;
            Type modelType = row.Render == CorpusRender.WithModel ? CorpusModels.For(row.Name) : null;
            var modelEx = modelType == null ? ExType.Dynamic : new ExType(modelType);
            var options = new TemplateOptions(Path.GetFileNameWithoutExtension(row.Name))
            {
                RootPath = TestCorpusIndex.CorpusDir,
                FileNamePostfix = ".heddle",
                ExpressionMode = row.Mode,
            };
            var ctx = new CompileContext(options, modelEx);
            ctx.DeferUnboundFunctions = true;
            ctx.RecordForm = true;
            var text = TestCorpusIndex.Text(row.Name);
            var t = new HeddleTemplate(text, ctx);
            if (!t.CompileResult.Success || ctx.CompileErrors.Count != 0)
            {
                errors = string.Join("; ", ctx.CompileErrors.Select(e =>
                    (e.DiagnosticId ?? "?") + "@" + e.Position.StartIndex + ":" + e.Error));
                if (errors.Length == 0)
                    errors = t.CompileResult.ToString();
                return null;
            }

            var artifact = ctx.FormRecord.ToArtifact("test", "test", "key-" + row.Name, "hash",
                "reg-" + row.Name, null, modelEx, false, false, "Text", row.Mode.ToString(), false);
            return artifact.Templates[0].RefusalSites;
        }
    }
}
