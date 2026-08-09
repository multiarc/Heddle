using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Heddle.TestCorpus;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// The gates that keep the shared corpus and its declared intent from drifting apart, asserted in
    /// the engine tier (which owns the corpus directory) as well as in the generator tier.
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
    }
}
