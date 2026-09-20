using System;
using System.Collections.Generic;
using System.IO;
using Heddle.Tool.Compile;
using Xunit;

namespace Heddle.Tool.Tests
{
    /// <summary>The incrementality stamp's standing contracts, asserted on <c>Stamp.Compute</c> and
    /// <c>DiskImports</c> themselves because a compile that reaches any of them either no longer
    /// succeeds or would need a race to observe: every input the artifact's bytes depend on moves the
    /// digest, an input that exists and refuses to be read never collapses to a constant, caller order
    /// never reaches the digest, and the digest describes the text the compile was handed rather than
    /// whatever is on disk once it finished.</summary>
    public class StampTests : IDisposable
    {
        private static readonly DiskImportRead[] NoImports = new DiskImportRead[0];

        private readonly string _dir;

        public StampTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "heddle-stamp-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(_dir, true);
            }
            catch (IOException)
            {
            }
        }

        /// <summary>Pins the template root as a digest input. It decides every key the artifact records,
        /// and so the generated class names — while the item rows carry absolute paths, so nothing else
        /// in the digest moves with it. Changing it alone used to leave the digest identical, so the
        /// targets ran the compile on a changed root and the host threw the work away: an incremental
        /// build then disagreed with a clean one over the same sources.</summary>
        [Fact]
        public void TheTemplateRootIsPartOfTheDigest()
        {
            var templates = new List<TemplateInput>();
            Assert.NotEqual(
                Stamp.Compute("1.0", Request(_dir), templates, NoImports, false),
                Stamp.Compute("1.0", Request(Path.Combine(_dir, "templates")), templates, NoImports, false));
        }

        /// <summary>Pins the placeholder defect: hashing an unreadable input to a fixed string makes
        /// every such input look unchanged for ever — the mistake <c>ComputeIntermediateDigest</c>
        /// documents avoiding and salts against. Both halves of the contract now salt. A file that is
        /// merely absent is a different fact and keeps its stable token, so it still goes up to date and
        /// still invalidates the moment it appears.</summary>
        [Fact]
        public void AnUnreadableInputSaltsTheDigestWhileAnAbsentOneDoesNot()
        {
            var request = Request(_dir);
            var templates = new List<TemplateInput>();

            string absent = Path.Combine(_dir, "not-there.heddle");
            var missing = Unhashed(absent);
            string whileMissing = Stamp.Compute("1.0", request, templates, missing, false);
            Assert.Equal(whileMissing, Stamp.Compute("1.0", request, templates, missing, false));
            File.WriteAllText(absent, "now it exists\n");
            Assert.NotEqual(whileMissing, Stamp.Compute("1.0", request, templates, missing, false));

            // A directory where a library is expected exists, is not missing, and cannot be read.
            string unreadable = Path.Combine(_dir, "lib.heddle");
            Directory.CreateDirectory(unreadable);
            var refused = Unhashed(unreadable);
            Assert.NotEqual(Stamp.Compute("1.0", request, templates, refused, false),
                Stamp.Compute("1.0", request, templates, refused, false));
        }

        /// <summary>Pins the order dependence the second import-only pass introduced: it re-read and
        /// re-hashed every opted-out item in response-file order, so reordering two of them moved the
        /// digest although the artifact was byte-identical. Every item is one sorted row now, and the
        /// opt-out is a field on it — so flipping <c>Precompile</c> still moves the digest.</summary>
        [Fact]
        public void CallerOrderNeverChangesTheDigestButTheOptOutDoes()
        {
            var request = Request(_dir);
            var alpha = Input("alpha.heddle", "aaa", false);
            var beta = Input("beta.heddle", "bbb", true);
            string first = Path.Combine(_dir, "first.heddle");
            string second = Path.Combine(_dir, "second.heddle");
            File.WriteAllText(first, "one\n");
            File.WriteAllText(second, "two\n");

            Assert.Equal(
                Stamp.Compute("1.0", request, new List<TemplateInput> { alpha, beta },
                    Unhashed(first, second), false),
                Stamp.Compute("1.0", request, new List<TemplateInput> { beta, alpha },
                    Unhashed(second, first), false));

            var compiled = Input("beta.heddle", "bbb", false);
            Assert.NotEqual(
                Stamp.Compute("1.0", request, new List<TemplateInput> { alpha, beta }, NoImports, false),
                Stamp.Compute("1.0", request, new List<TemplateInput> { alpha, compiled }, NoImports, false));
        }

        /// <summary>Pins the recorded-hash contract. The stamp the compile writes is computed after the
        /// compile finished, so re-reading a disk-served library there certifies bytes the artifact may
        /// never have been built from — a library saved mid-compile is read by some templates and not
        /// others, and the stamp then blesses the torn result. A row carrying the hash the reader took
        /// from the text it handed the parse is used as given; only a row carrying none — the previous
        /// build's list, which is what a check has to check — is read from disk.</summary>
        [Fact]
        public void ARecordedReadIsHashedFromWhatTheCompileWasHandedNotFromDiskAfterwards()
        {
            var request = Request(_dir);
            var templates = new List<TemplateInput>();
            string library = Path.Combine(_dir, "lib.heddle");
            File.WriteAllText(library, "version one\n");
            var asRead = new[] { new DiskImportRead(library, Heddle.Precompiled.ContentHash.HashText("version one\n")) };
            string stamped = Stamp.Compute("1.0", request, templates, asRead, false);

            File.WriteAllText(library, "version two\n");
            Assert.Equal(stamped, Stamp.Compute("1.0", request, templates, asRead, false));
            var onDiskNow = new[] { new DiskImportRead(library, Heddle.Precompiled.ContentHash.HashText("version two\n")) };
            Assert.Equal(Stamp.Compute("1.0", request, templates, onDiskNow, false),
                Stamp.Compute("1.0", request, templates, Unhashed(library), false));
            Assert.NotEqual(stamped, Stamp.Compute("1.0", request, templates, Unhashed(library), false));
        }

        /// <summary>A library that answered twice with different text was saved while the compile was
        /// reading it, so the artifact carries both and no hash describes what was built; a path the
        /// one-per-line record cannot round trip is not describable either. Neither is recorded as a
        /// fact, and the digest is salted instead of certifying a half-truth.</summary>
        [Fact]
        public void AReadTheRecordCannotDescribeCertifiesNothing()
        {
            var request = Request(_dir);
            var templates = new List<TemplateInput>();

            var torn = new DiskImports();
            string library = Path.Combine(_dir, "lib.heddle");
            torn.Record(library, "aaa");
            torn.Record(library, "aaa");
            Assert.False(torn.Unrecorded);
            Assert.Single(torn.Reads);
            torn.Record(library, "bbb");
            Assert.True(torn.Unrecorded);
            Assert.NotEqual(Stamp.Compute("1.0", request, templates, torn.Reads, torn.Unrecorded),
                Stamp.Compute("1.0", request, templates, torn.Reads, torn.Unrecorded));

            var unwritable = new DiskImports();
            unwritable.Record(Path.Combine(_dir, "two\nlines.heddle"), "aaa");
            Assert.Empty(unwritable.Reads);
            Assert.True(unwritable.Unrecorded);

            // And such a compile leaves no stamp, so the outputs are incomplete and the build that
            // follows runs the compile again instead of inheriting what nothing describes.
            string stampPath = Path.Combine(_dir, "stamp.txt");
            Stamp.Write(stampPath, "certified", true);
            Assert.Equal("certified", Stamp.Read(stampPath));
            Stamp.Write(stampPath, "uncertifiable", false);
            Assert.False(File.Exists(stampPath));
        }

        private CompileRequest Request(string root) =>
            new CompileRequest { Project = Path.Combine(_dir, "proj.csproj"), Root = root };

        private static DiskImportRead[] Unhashed(params string[] paths)
        {
            var reads = new DiskImportRead[paths.Length];
            for (int i = 0; i < paths.Length; i++)
                reads[i] = new DiskImportRead(paths[i], null);
            return reads;
        }

        private TemplateInput Input(string name, string contentHash, bool importOnly) =>
            new TemplateInput
            {
                Item = new TemplateItem { Path = Path.Combine(_dir, name), IsImportOnly = importOnly },
                FullPath = Path.Combine(_dir, name),
                ContentHash = contentHash
            };
    }
}
