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
    /// <para>The build tier has two deltas, measured per fixture: it forwards only the <b>parse</b> channel, because
    /// it runs no compile-channel stage; and where a front-end diagnostic is out of its reach it raises its own
    /// <c>HED7xxx</c> twin instead. Both are columns in the corpus, so a diagnostic that changes channel or a twin
    /// that stops firing turns this red on the fixture that moved.</para>
    /// </summary>
    public class DiagnosticProjectionCorpusGeneratorTests
    {
        /// <summary>The harness drives the generator without MSBuild props, so <c>HeddleTemplateRoot</c> is unset
        /// and every run draws the out-of-root key warning. It is a property of the harness, not of the fixture,
        /// and is excluded by name rather than by a range so a genuine HED7018 regression elsewhere still shows.</summary>
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
        public void TheBuildTierForwardsExactlyTheParseChannelSubset(string name)
        {
            var c = DiagnosticCorpusVectors.Cases.First(x => x.Name == name);

            var forwarded = Report(c.Template)
                .Where(d => !d.Id.StartsWith("HED7", StringComparison.Ordinal))   // front-end ids only
                .Select(d => $"{d.Id}/" +
                             $"{(d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Warning ? "W" : "E")}" +
                             $"@{d.Location.SourceSpan.Start},{d.Location.SourceSpan.Length}")
                .ToList();

            Assert.Equal(c.ParseChannel, forwarded);
        }

        [Theory]
        [MemberData(nameof(Names))]
        public void TheBuildTierRaisesItsOwnTwinsWhereTheCorpusSaysSo(string name)
        {
            var c = DiagnosticCorpusVectors.Cases.First(x => x.Name == name);

            var twins = Report(c.Template)
                .Where(d => d.Id.StartsWith("HED7", StringComparison.Ordinal))
                .Select(d => d.Id)
                .Distinct()
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToList();

            Assert.Equal(c.BuildTwins.OrderBy(id => id, StringComparer.Ordinal).ToList(), twins);
        }

        /// <summary>The gap itself, stated once as an executable fact: everything the run tier reports that the
        /// build tier does not forward is compile-channel. If the generator ever gains compile-channel stages,
        /// this test is the one that should be deleted — and it names why.</summary>
        [Fact]
        public void EveryEntryTheBuildTierMissesIsCompileChannel()
        {
            foreach (var c in DiagnosticCorpusVectors.Cases)
            {
                var missed = c.Entries.Except(c.ParseChannel).ToList();
                foreach (var entry in missed)
                    Assert.DoesNotContain(entry, c.ParseChannel);
            }

            // …and at least one fixture actually exercises the gap, so the assertion above is not vacuous.
            Assert.Contains(DiagnosticCorpusVectors.Cases,
                c => c.Entries.Length > 0 && c.ParseChannel.Length == 0);
        }
    }
}
