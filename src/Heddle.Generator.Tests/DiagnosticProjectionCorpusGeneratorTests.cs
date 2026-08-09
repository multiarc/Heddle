extern alias gen;
using System;
using System.Linq;
using Heddle.Tests;
using Xunit;

namespace Heddle.Generator.Tests
{
    /// <summary>
    /// The build-tier arm of the projection-equivalence corpus. Same <see cref="DiagnosticCorpusVectors"/> table
    /// as the run tier and the editor, asserted against what the generator actually reports for the same template
    /// bytes.
    /// <para>The build tier has two deltas, measured per fixture: it forwards the parse channel plus the
    /// compile-channel warnings its own walk reaches, which is less than the run tier drains; and where a front-end
    /// diagnostic is out of its reach it raises its own <c>HED7xxx</c> twin instead. Both are columns in the corpus,
    /// so a diagnostic that stops being forwarded or a twin that stops firing turns this red on the fixture that
    /// moved.</para>
    /// </summary>
    public class DiagnosticProjectionCorpusGeneratorTests
    {
        /// <summary>Excluded because the harness lacks <c>HeddleTemplateRoot</c>, not because of the fixture.</summary>
        private const string HarnessRootWarning = "HED7018";

        public static TheoryData<string> Names
        {
            get
            {
                var data = new TheoryData<string>();
                foreach (var c in DiagnosticCorpusVectors.Cases)
                    data.Add(c.Name);
                return data;
            }
        }

        private static Microsoft.CodeAnalysis.Diagnostic[] Report(string template) =>
            GeneratorHarness.Run(new[] { ("views/corpus.heddle", template) })
                .GeneratorDiagnostics
                .Where(d => d.Id != HarnessRootWarning)
                .ToArray();

        [Theory]
        [MemberData(nameof(Names))]
        public void TheBuildTierForwardsExactlyWhatTheCorpusDeclares(string name)
        {
            var c = DiagnosticCorpusVectors.Cases.First(x => x.Name == name);

            var forwarded = Report(c.Template)
                .Where(d => !d.Id.StartsWith("HED7", StringComparison.Ordinal))   // front-end ids only
                .Select(d => $"{d.Id}/" +
                             $"{(d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Warning ? "W" : "E")}" +
                             $"@{d.Location.SourceSpan.Start},{d.Location.SourceSpan.Length}")
                .ToList();

            Assert.Equal(c.BuildForwarded, forwarded);
        }

        [Theory]
        [MemberData(nameof(Names))]
        public void TheBuildTierRaisesItsOwnTwinsWhereTheCorpusSaysSo(string name)
        {
            var c = DiagnosticCorpusVectors.Cases.First(x => x.Name == name);

            var twins = Report(c.Template)
                .Where(d => d.Id.StartsWith("HED7", StringComparison.Ordinal))
                // HED7031 (BuildTemplateNotPrecompiled) is not a twin. A twin is a build-tier
                // diagnostic mirroring one the engine raises for the same bytes; HED7031 reports
                // that the emitter DECLINED to precompile, a build-tier capability notice with no
                // run-tier counterpart at all. Counting it here would make every degrading corpus
                // case look as though it had grown a new authoring diagnostic.
                .Where(d => !string.Equals(d.Id, "HED7031", StringComparison.Ordinal))
                .Select(d => d.Id)
                .Distinct()
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToList();

            Assert.Equal(c.BuildTwins.OrderBy(id => id, StringComparer.Ordinal).ToList(), twins);
        }

        /// <summary>What the build tier forwards is a subset of what the run tier drains, never a superset: the
        /// build may say less than the engine — a registry built at run time, a compile stage it does not run —
        /// but it may not invent an entry the engine would not raise for the same bytes.</summary>
        [Fact]
        public void NothingIsForwardedThatTheRunTierWouldNotRaise()
        {
            foreach (var c in DiagnosticCorpusVectors.Cases)
                Assert.Empty(c.BuildForwarded.Except(c.Entries));
        }

        /// <summary>The compile channel is no longer wholly out of the build tier's reach: fixtures whose only
        /// entries are compile-channel are forwarded now. Stated as an executable fact so that a regression which
        /// silently restores the old parse-channel-only posture cannot pass.</summary>
        [Fact]
        public void CompileChannelWarningsReachTheBuildTier()
        {
            var drained = DiagnosticCorpusVectors.Cases
                .Where(c => c.ParseChannel.Length == 0 && c.BuildForwarded.Length > 0)
                .ToList();

            Assert.NotEmpty(drained);
            foreach (var c in drained)
                Assert.All(c.BuildForwarded, entry => Assert.Contains(entry, c.Entries));
        }

        /// <summary>The residue, named: entries the run tier raises and the build tier still does not. Every one
        /// is a compile-channel entry that needs something a build cannot have — an instantiated extension, a
        /// registry the host fills at run time, or an evaluated expression — so the list is expected to be
        /// non-empty, and this states which fixtures are in it rather than leaving it to be inferred.</summary>
        [Fact]
        public void TheResidueIsNamedPerFixture()
        {
            var residue = DiagnosticCorpusVectors.Cases
                .Where(c => c.Entries.Except(c.BuildForwarded).Any())
                .Select(c => c.Name)
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToList();

            Assert.Equal(
                new[]
                {
                    "orphanElse", "rangeStep", "unknownFunction", "unknownProfile", "unresolvableFunctionArgument"
                },
                residue);
        }
    }
}
