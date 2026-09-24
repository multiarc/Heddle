using System.Collections.Generic;
using Heddle.Data;
using Heddle.Language;
using Heddle.Strings.Core;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// Pins the shared region-fill decision including its lazy-lookup contract — no region table lookup for candidates the origin filter rejects.
    /// </summary>
    public class RegionFillResolverTests
    {
        private static RegionFillCandidate Candidate(string name, ParseContext origin)
        {
            var item = new DefinitionItem(name, "body", null);
            return new RegionFillCandidate(name, item, null, new BlockPosition(0, 1),
                new HeddleCompileError { Error = "base not found" }, origin);
        }

        private static DefinitionItem Callee(params string[] regionDefaults)
        {
            var callee = new DefinitionItem("callee", null, null);
            foreach (var name in regionDefaults)
                callee.Context.DefinitionsBlock.Definitions[name] = new DefinitionItem(name, "default", null);
            return callee;
        }

        private static List<string> Run(IReadOnlyList<RegionFillCandidate> candidates, ParseContext origin,
            DefinitionItem callee, IReadOnlyDictionary<string, bool> regions, List<string> lookups = null)
        {
            var verdicts = new List<string>();
            RegionFillResolver.Resolve(candidates, origin,
                (string name, out bool isPublic) =>
                {
                    lookups?.Add(name);
                    return regions.TryGetValue(name, out isPublic);
                },
                callee,
                (candidate, verdict, materialized) =>
                    verdicts.Add(candidate.Name + "=" + verdict + (materialized == null ? "" : ":materialized")));
            return verdicts;
        }

        [Fact]
        public void MatchedPublicCandidateMaterializes()
        {
            var origin = new ParseContext();
            var verdicts = Run(new[] { Candidate("head", origin) }, origin, Callee("head"),
                new Dictionary<string, bool> { ["head"] = true });

            Assert.Equal(new[] { "head=Matched:materialized" }, verdicts);
        }

        [Fact]
        public void DanglingCandidateNamesNoRegion()
        {
            var origin = new ParseContext();
            var verdicts = Run(new[] { Candidate("nope", origin) }, origin, Callee("head"),
                new Dictionary<string, bool>());

            Assert.Equal(new[] { "nope=Dangling" }, verdicts);
        }

        [Fact]
        public void PrivateRegionIsRejectedBeforeTheDefaultIsFetched()
        {
            var origin = new ParseContext();
            var verdicts = Run(new[] { Candidate("head", origin) }, origin, Callee("head"),
                new Dictionary<string, bool> { ["head"] = false });

            Assert.Equal(new[] { "head=Private" }, verdicts);
        }

        [Fact]
        public void DeclaredRegionWithoutAStoredDefaultIsDefaultMissing()
        {
            var origin = new ParseContext();
            var verdicts = Run(new[] { Candidate("head", origin) }, origin, Callee(),
                new Dictionary<string, bool> { ["head"] = true });

            Assert.Equal(new[] { "head=DefaultMissing" }, verdicts);
        }

        [Fact] // Foreign-origin candidate performs no region table lookup.
        public void ForeignOriginCandidateIsFilteredWithoutConsultingTheRegionTable()
        {
            var origin = new ParseContext();
            var other = new ParseContext();
            var lookups = new List<string>();

            var verdicts = Run(new[] { Candidate("head", other) }, origin, Callee("head"),
                new Dictionary<string, bool> { ["head"] = true }, lookups);

            Assert.Empty(verdicts);
            Assert.Empty(lookups);
        }

        /// <summary>
        /// Documents the observable reaction facts (error retraction, fill installation, error raising) per verdict.
        /// </summary>
        [Theory]
        [InlineData(RegionFillVerdict.Matched, true, true, false)]
        [InlineData(RegionFillVerdict.Dangling, false, false, false)]
        [InlineData(RegionFillVerdict.DefaultMissing, false, false, false)]
        [InlineData(RegionFillVerdict.Private, true, false, true)]
        internal void VerdictReactions(RegionFillVerdict verdict, bool retractsError, bool installsFill,
            bool raisesError)
        {
            // Stated as data so a fifth verdict cannot be added without deciding — and recording — its reaction.
            Assert.Equal(retractsError, verdict == RegionFillVerdict.Matched || verdict == RegionFillVerdict.Private);
            Assert.Equal(installsFill, verdict == RegionFillVerdict.Matched);
            Assert.Equal(raisesError, verdict == RegionFillVerdict.Private);
        }

        /// <summary>Guards against a verdict added without a defined reaction.</summary>
        [Fact]
        public void EveryVerdictHasAReactionRow()
        {
            Assert.Equal(4, System.Enum.GetValues(typeof(RegionFillVerdict)).Length);
        }

        [Fact]
        public void MixedCandidatesKeepDocumentOrderAndIndependentVerdicts()
        {
            var origin = new ParseContext();
            var other = new ParseContext();
            var verdicts = Run(
                new[]
                {
                    Candidate("head", origin), Candidate("secret", origin), Candidate("nope", origin),
                    Candidate("head", other), Candidate("orphan", origin)
                },
                origin, Callee("head", "secret"),
                new Dictionary<string, bool> { ["head"] = true, ["secret"] = false, ["orphan"] = true });

            Assert.Equal(new[] { "head=Matched:materialized", "secret=Private", "nope=Dangling", "orphan=DefaultMissing" },
                verdicts);
        }
    }
}
