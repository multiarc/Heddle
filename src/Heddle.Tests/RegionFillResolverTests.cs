using System.Collections.Generic;
using Heddle.Data;
using Heddle.Language;
using Heddle.Strings.Core;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// The verdict-level pins for the shared region-fill matching rule (<see cref="RegionFillResolver"/>).
    /// What is pinned here is the shared <em>decision</em>, including its lazy-lookup contract — no region
    /// table is consulted for a candidate the origin filter rejects.
    /// <para>The reaction per verdict is a contract: both backends map each verdict to the same observable
    /// facts (error retraction, fill installation, error raising). This table specifies the contract.</para>
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

        [Fact] // pins the lazy layout resolution: a foreign-origin candidate performs no lookup at all
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
        /// The reaction contract per verdict. Both backends map a verdict to the same three observable facts:
        /// whether the candidate's tentative base-not-found error is retracted, whether a fill is installed, and
        /// whether a new error is raised. The runtime's reactions live in <c>HeddleCompiler.BuildRegionFillScope</c>
        /// and the generator's in <c>TemplateEmitter.TryBuildGeneratorFillScope</c>; the cross-tier proof that they
        /// agree is the paired <c>RegionTests</c> fixture (build HED7024 ⇄ runtime HED5019, build-forwarded
        /// base-not-found ⇄ runtime base-not-found). This table is the specification those two adapters are
        /// written against.
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

        /// <summary>Every verdict the enum declares has a row in the reaction table above — the guard against a
        /// new verdict slipping in with an unstated reaction on one tier.</summary>
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
