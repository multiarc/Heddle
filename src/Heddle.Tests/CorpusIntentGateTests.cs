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
        /// The two-direction gates. A template added without a row fails naming the template; a row that
        /// outlives its template fails naming the row. This is the mechanism behind the standing rule that new
        /// feature areas contribute their templates to the corpus — a rule that had never been backfilled because it
        /// was prose in a standard with nothing behind it.
        /// </summary>
        [Fact]
        public void EveryCorpusTemplateHasExactlyOneIntentRowAndViceVersa()
        {
            var onDisk = TestCorpusIndex.Names();
            var declared = CorpusIntent.DeclaredNames();
            Assert.True(new HashSet<string>(onDisk, StringComparer.Ordinal).SetEquals(declared),
                CorpusIntent.Describe("The corpus intent table", declared, onDisk));
        }

        /// <summary>The table's own row count. It exists so that "this stage added N entries" is a one-line diff a
        /// reviewer can check against the stage's stated scope — a stage cannot smuggle extra templates in beside the
        /// ones it names.</summary>
        [Fact]
        public void TheIntentTableDeclaresExactlyTheRowCountItClaims()
        {
            Assert.Equal(CorpusIntent.DeclaredRowCount, CorpusIntent.Rows.Count);
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
        /// The encoding gate, BOM half: a corpus template carries a UTF-8 byte-order mark <b>iff</b> its intent
        /// row declares <c>Bom = true</c>.
        /// <para>Eight of the 62 templates carry one. Those BOMs are <b>deliberate coverage</b> — <c>PrecompiledGauntlet.HashFile</c>
        /// decodes with <c>detectEncodingFromByteOrderMarks: true</c> — but until this flag existed a deliberate BOM and an
        /// accidental one were indistinguishable, so nothing could tell you which you were looking at.</para>
        /// <para>This is a <b>separate pin from line endings</b>, and deliberately so.
        /// <c>.gitattributes</c>' <c>eol=lf</c> governs newlines and says nothing whatsoever about byte-order marks:
        /// the pre-program BOM drift across snapshots (two of eight had one) passed every
        /// <c>eol=lf</c> check there was. Two independent facts need two independent gates.</para>
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
        /// The encoding gate, golden half: no sibling output golden carries a BOM. The goldens are byte-compared
        /// against LF engine output on both Linux and Windows CI; a BOM on one of them would be a silent three-byte
        /// prefix on the expected side.
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
        /// <b>The corpus is input</b>. No file inside the shared corpus directory may be written by a test.
        /// <para>This was a real finding, not a hypothetical. Twenty-five sites across fourteen files wrote
        /// <c>test-&lt;name&gt;.html</c> straight back into <c>TestTemplate/</c>, six of those artifacts were checked
        /// in beside the inputs, and the engine tier's own output directory had accumulated 36 of them — so the
        /// directory held 148 files where the tracked corpus has 106. Once several projects copy that directory into
        /// their outputs, one project's test run writing into a directory another project's gate enumerates is a
        /// race, and "the corpus is input" is the invariant that makes byte-neutrality checkable at all. The writes
        /// now go to <c>TestOutput/</c> in the writer's own output directory
        /// (<see cref="TestCorpusIndex.WrittenArtifactPath"/>); the six checked-in artifacts were relocated to
        /// <c>src/Heddle.Tests/TestOutput/</c> and preserved, not deleted.</para>
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
        /// The corpus reaches this project's output directory. Stated as its own test so that a build-wiring
        /// regression fails with a wiring message rather than surfacing as a dozen confusing assertion failures elsewhere.
        /// <para>The validation-scenario canary: change the output layout (a different configuration, a renamed bin
        /// path) and this still passes, because it reads <c>AppContext.BaseDirectory</c>. That is the scenario which
        /// silently no-op'd five integration tests.</para>
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
