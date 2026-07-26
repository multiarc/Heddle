using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Heddle.Attributes;
using Heddle.Core;
using Heddle.Data;
using Heddle.Helpers;
using Heddle.Language;
using Heddle.Runtime;
using Xunit;
using Heddle.TestCorpus;

namespace Heddle.Tests
{
    /// <summary>
    /// Generator plan phase 1 WI1/WI4 (D2/D5) — the <c>[ScopeChannel]</c> participant scan.
    /// <para>Two things are pinned here. First, the <b>characterization</b>: <see cref="LegacyLeftmostScan"/> is a
    /// verbatim transcription of the generator's pre-phase-1 probe (<c>TemplateEmitter.ScanHostsParticipant</c>
    /// and the twin in <c>PopulateBody</c>, both of which looked at <c>chain.Chain[0]</c> only). It is kept so the
    /// divergence set the fix closes is stated as data rather than recalled from a plan.</para>
    /// <para>Second, the <b>lockstep</b>: the shared parse-level scan must agree with the runtime's compiled-tree
    /// <c>RuntimeDocument.NeedsLocals</c>, which walks every item of a chain and recurses into nested chain
    /// parameters. The two genuinely see different trees — the runtime's runs post-compile over extension
    /// <em>instances</em>, where carrier wrap and definition shadowing are already resolved — so the parse-level
    /// scan is allowed to be a superset (an unread frame is behavior-invisible), never a subset. Both directions
    /// are asserted, and the one documented over-provision has its own named row.</para>
    /// </summary>
    public class ParticipantScanLockstepTests
    {
        // -------------------------------------------------------------------------------------------------
        // Characterization pin (captured verbatim from the pre-extraction bodies, before the code moved).
        //
        //     private bool ScanHostsParticipant(ParseContext ctx)
        //     {
        //         if (ctx?.OutputChains == null)
        //             return false;
        //         foreach (var chain in ctx.OutputChains)
        //         {
        //             var lm = chain.Chain != null && chain.Chain.Count > 0 ? chain.Chain[0].ExtensionName : null;
        //             if (lm != null && _extensionBinder.TryResolve(lm, out var i) && i.HasScopeChannel)
        //                 return true;
        //         }
        //         return false;
        //     }
        // -------------------------------------------------------------------------------------------------
        private static bool LegacyLeftmostScan(ParseContext ctx, Func<string, bool> hasScopeChannel)
        {
            if (ctx?.OutputChains == null)
                return false;
            foreach (var chain in ctx.OutputChains)
            {
                var lm = chain.Chain != null && chain.Chain.Count > 0 ? chain.Chain[0].ExtensionName : null;
                if (lm != null && hasScopeChannel(lm))
                    return true;
            }

            return false;
        }

        private static readonly Func<string, bool> HasScopeChannel = name =>
            TemplateFactory.TryGetExtensionType(name, out var type) &&
            type.IsHaveAttribute<ScopeChannelAttribute>(true);

        private static ParseContext Parse(string template) =>
            DocumentParser.Parse(template, new ParserSettings { RootPath = string.Empty }, out _);

        /// <summary>Templates whose participant reachability the two scans must agree on. Chains and nested chain
        /// parameters both appear, because those are exactly the shapes the leftmost-only probe missed.</summary>
        public static IEnumerable<object[]> Corpus()
        {
            yield return new object[] { "no participant", "hello @(this)", false };
            yield return new object[] { "leftmost participant", "@if(this){{x}}@else(){{y}}", true };
            yield return new object[] { "participant in a chain tail", "@(this):else(){{y}}", true };
            yield return new object[] { "participant as a nested chain parameter", "@(else())", true };
            yield return new object[] { "participant nested in a bodied host's parameter", "@if(else()){{x}}", true };
            yield return new object[] { "non-participant chain", "@(this):@(this)", false };
        }

        [Theory]
        [MemberData(nameof(Corpus))]
        public void TheSharedScanFindsEveryReachableParticipant(string label, string template, bool expected)
        {
            Assert.Equal(expected, ParticipantScan.BodyHostsParticipant(Parse(template), HasScopeChannel));
            Assert.NotNull(label);
        }

        /// <summary>The divergence set, as data: every corpus row the legacy leftmost-only probe got wrong is one
        /// the shared scan now gets right, and the legacy probe was never <em>more</em> permissive — the fix only
        /// ever adds frames, which is why it cannot regress a template that already worked.</summary>
        [Theory]
        [MemberData(nameof(Corpus))]
        public void TheLegacyProbeWasOnlyEverAnUnderApproximation(string label, string template, bool expected)
        {
            var legacy = LegacyLeftmostScan(Parse(template), HasScopeChannel);
            var shared = ParticipantScan.BodyHostsParticipant(Parse(template), HasScopeChannel);

            Assert.Equal(expected, shared);
            Assert.False(legacy && !shared, label + ": the legacy probe found a participant the shared scan misses");
        }

        [Fact]
        public void TheLegacyProbeMissesANonLeftmostParticipant()
        {
            const string template = "@(this):else(){{y}}";
            Assert.False(LegacyLeftmostScan(Parse(template), HasScopeChannel));
            Assert.True(ParticipantScan.BodyHostsParticipant(Parse(template), HasScopeChannel));
        }

        [Fact]
        public void TheLegacyProbeMissesANestedChainParameterParticipant()
        {
            const string template = "@(else())";
            Assert.False(LegacyLeftmostScan(Parse(template), HasScopeChannel));
            Assert.True(ParticipantScan.BodyHostsParticipant(Parse(template), HasScopeChannel));
        }

        /// <summary>
        /// The lockstep against the compiled-tree scan. The parse-level rule may over-provision — see the
        /// shadowing row below — but must never miss what the runtime provisions for.
        /// </summary>
        [Theory]
        [MemberData(nameof(Corpus))]
        public void TheSharedScanIsNeverNarrowerThanTheRuntimeScan(string label, string template, bool expected)
        {
            var runtimeNeedsLocals = RuntimeNeedsLocals(template);
            var shared = ParticipantScan.BodyHostsParticipant(Parse(template), HasScopeChannel);

            Assert.Equal(expected, shared);
            Assert.False(runtimeNeedsLocals && !shared,
                label + ": the runtime provisions a frame the shared parse-level scan does not");
        }

        /// <summary>
        /// The documented over-provision (Q1.4, ruling: keep). A definition named after a <c>[ScopeChannel]</c>
        /// extension shadows it, so the compiled tree holds a definition carrier — no participant — while the
        /// parse-level scan, which deliberately runs before definition resolution, still counts the name. The
        /// resulting frame is never read (publish/read happens only inside participants), so this is
        /// behavior-invisible and emit-time only. Asserted explicitly rather than left implicit: the day this
        /// branch needs to change is the trigger for giving the scan a <c>definitionExists</c> predicate.
        /// </summary>
        [Fact]
        public void AShadowedParticipantNameOverProvisionsAndThatIsTheRuling()
        {
            const string template = "@%<else>{{shadow}}%@\n@else()";

            Assert.True(ParticipantScan.BodyHostsParticipant(Parse(template), HasScopeChannel),
                "the parse-level scan counts the shadowed name — the documented over-provision");
            Assert.False(RuntimeNeedsLocals(template),
                "the compiled tree holds a definition carrier, so the runtime provisions nothing");
        }

        private static bool RuntimeNeedsLocals(string template)
        {
            var compileScope = new CompileScope(new CompileContext(new TemplateOptions(), ExType.Dynamic));
            var parse = DocumentParser.Parse(template, new ParserSettings { RootPath = string.Empty },
                out var clean);
            var document = HeddleCompiler.Compile(clean, compileScope, parse, null);
            compileScope.CompileContext.Compile();
            return document != null && document.NeedsLocals;
        }

        // -------------------------------------------------------------------------------------------------------
        // The whole-corpus sweep the test matrix promised ("for every compiled fixture in the differential
        // corpus"). What stood here before was the six hand-picked rows above — a good divergence-set statement,
        // but not the sweep, and the plan's own risk table names "the lockstep corpus misses a shape" as the risk
        // this is the mitigation for. The corpus is src/Heddle.Tests/TestTemplate/**, the same set
        // CorpusDifferentialTests classifies.
        // -------------------------------------------------------------------------------------------------------

        /// <summary>Corpus templates whose compiled tree provisions a frame — the load-bearing rows of the sweep.
        /// Pinned exactly, not as a floor: a corpus that stopped exercising participants would make the sweep
        /// vacuous while still passing, which is the failure mode the phase-0 audit found in two other gates.</summary>
        private static readonly string[] CorpusTemplatesNeedingLocals =
        {
            "branch-import-else.heddle", "branching-flagship.heddle", "branching-interleaved.heddle",
            "branching-nested.heddle", "branching-partial-child.heddle", "branching-partial-parent.heddle"
        };

        /// <summary>Corpus templates where the parse-level scan legitimately over-provisions relative to the
        /// compiled tree (the Q1.4 ruling's shape — a definition shadowing a <c>[ScopeChannel]</c> name). Empty
        /// today: no corpus fixture has that shape, so agreement is exact and any new inexactness is a conscious
        /// edit to this list rather than a silent widening.</summary>
        private static readonly string[] CorpusOverProvisionAllowList = new string[0];

        [Fact]
        public void TheSharedScanAgreesWithTheRuntimeOverTheWholeCorpus()
        {
            var dir = TestCorpusIndex.CorpusDir;

            var files = Directory.GetFiles(dir, "*.heddle")
                .OrderBy(p => p, StringComparer.Ordinal).ToList();
            // Phase 7 D5: SET equality against the declared intent table, not the `Assert.Equal(62, …)` count that
            // stood here. The count was the right instinct and the wrong instrument — it goes green again the moment
            // somebody edits the digit, and it never names the file that left. This sweep going vacuous while still
            // passing is the exact failure mode its own comment says it exists to prevent, so the pin has to be the
            // kind that cannot be satisfied without naming what changed.
            var observed = files.Select(Path.GetFileName).ToList();
            var declared = CorpusIntent.DeclaredNames();
            Assert.True(new HashSet<string>(observed, StringComparer.Ordinal).SetEquals(declared),
                CorpusIntent.Describe("The participant-scan sweep corpus", declared, observed));

            var settings = new ParserSettings { RootPath = dir + Path.DirectorySeparatorChar };
            var options = new TemplateOptions { RootPath = dir, FileNamePostfix = ".heddle" };

            var narrower = new List<string>();
            var overProvisioned = new List<string>();
            var needingLocals = new List<string>();

            foreach (var file in files)
            {
                var name = Path.GetFileName(file);
                var text = File.ReadAllText(file).Replace("\r\n", "\n");

                var compileScope = new CompileScope(new CompileContext(options, ExType.Dynamic));
                var parse = DocumentParser.Parse(text, settings, out var clean);
                var document = HeddleCompiler.Compile(clean, compileScope, parse, null);
                compileScope.CompileContext.Compile();
                // Every corpus template compiles as a dynamic document today (the *-broken fixtures carry
                // front-end diagnostics, not compile failures), so there is no "unprobeable" escape hatch here.
                Assert.NotNull(document);

                var runtime = document.NeedsLocals;
                var shared = ParticipantScan.BodyHostsParticipant(
                    DocumentParser.Parse(text, settings, out _), HasScopeChannel);

                if (runtime)
                    needingLocals.Add(name);
                if (runtime && !shared)
                    narrower.Add(name);
                if (shared && !runtime)
                    overProvisioned.Add(name);
            }

            // The contract: the parse-level scan may over-provision, never under-provision.
            Assert.Empty(narrower);
            Assert.Equal(CorpusOverProvisionAllowList, overProvisioned.ToArray());
            Assert.Equal(CorpusTemplatesNeedingLocals, needingLocals.ToArray());
        }
    }
}
