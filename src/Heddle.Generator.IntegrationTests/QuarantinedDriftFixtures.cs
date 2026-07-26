using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Heddle.Data;
using Heddle.Helpers;
using Heddle.Precompiled;
using Heddle.Runtime;
using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// Phase 0 WI6 (D6) — the <b>quarantine register</b>. Each fixture below is a guarded, gauntlet-crossing test of a
    /// known live drift from the research program. Every one of them fails on today's code, so each ships explicitly
    /// skipped with its owning phase and drift named in the <c>Skip</c> string: the owning phase's fix-first group
    /// un-skips its fixture as acceptance evidence. Phase 0 must land green without masking anything, so a fixture is
    /// never weakened to make it pass — it is quarantined instead.
    /// <para><b>An unexplained or orphaned skip in this file is a review failure.</b> The register is:</para>
    /// <list type="table">
    /// <item><term>phase 5 F1</term><description>content-hash input mismatch — BOM'd file under file-backed
    /// staleness.</description></item>
    /// <item><term>phase 3 F1</term><description>nested/generic AQN identity — a nested extension type; <b>fixed</b> by
    /// phase 3, un-skipped.</description></item>
    /// <item><term>phase 3 F3</term><description>inherited <c>[ExtensionName]</c> subclass — <b>fixed</b> by phase 3,
    /// un-skipped.</description></item>
    /// <item><term>phase 1 F11</term><description>non-leftmost <c>[ScopeChannel]</c> participant — <b>fixed</b> by
    /// phase 1 (WI1/WI4); un-skipped and reshaped (the drift is latent on the precompiled tier — see the
    /// fixture's own note).</description></item>
    /// <item><term>phase 4 F3</term><description>overload-rank tie (<c>min(1, 2u)</c>) — <b>fixed</b> by phase 4
    /// WI8, un-skipped.</description></item>
    /// </list>
    /// </summary>
    [Collection("PrecompiledRegistry")]
    public class QuarantinedDriftFixtures : PrecompiledRegistryTestBase
    {
        // ---------------------------------------------------------------------------------------------------
        // phase 5 F1 — content-hash input mismatch. FIXED by phase 5 WI1 (D1); un-skipped as its acceptance evidence.
        // The generator hashes Roslyn's decoded SourceText re-encoded as UTF-8 without a BOM; the runtime used to
        // hash the raw file byte stream, so any .heddle file saved with a BOM (or as UTF-16) failed
        // PrecompiledGauntlet.CheckStaleness on every request under EnableFileChangeCheck and silently took the
        // dynamic path. Verified red at authoring time: PrecompiledMismatchException(StaleContent) —
        // "Content: 'drift-bom.heddle' hash mismatch". Both sides now hash decoded text through ContentHash.HashText.
        // ---------------------------------------------------------------------------------------------------
        [Fact]
        public void BomTemplate_StaysOnThePrecompiledTier_UnderFileBackedStaleness()
        {
            const string key = "drift-bom.heddle";
            const string content = "@model(){{System.String}}@\\\nbom @(this)\n";

            var gen = DifferentialHarness.Generate(new[] { (key, content) });
            DifferentialHarness.ExpectPrecompiled(gen, key);
            PrecompiledTemplates.Register(gen.Assembly);

            var dir = Path.Combine(Path.GetTempPath(), "heddle-bom-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                // The same characters the generator saw, on disk with a UTF-8 BOM — the shape any editor produces
                // when "UTF-8 with signature" is the default encoding.
                var bytes = new List<byte>(Encoding.UTF8.GetPreamble());
                bytes.AddRange(new UTF8Encoding(false).GetBytes(content));
                File.WriteAllBytes(Path.Combine(dir, key), bytes.ToArray());

                var options = FallbackGuard.GuardedOptions();
                options.RootPath = dir + Path.DirectorySeparatorChar;
                options.FileNamePostfix = ".heddle";
                options.EnableFileChangeCheck = true;

                using var guard = FallbackGuard.Install();
                var resolver = new TemplateResolver(Path.Combine(dir, "root.marker"), true);
                var template = resolver.GetTemplate(key, string.Empty, out _,
                    new CompileContext(options, typeof(string)), TemplatePathType.None);
                Assert.NotNull(template);
                Assert.Equal("bom hi\n", template.Generate("hi"));
                guard.Verify();
            }
            finally
            {
                DifferentialHarness.TryDeleteDirectory(dir);
            }
        }

        // ---------------------------------------------------------------------------------------------------
        // phase 3 F1 — nested/generic AQN identity. FIXED by phase 3 (WI1/WI2); un-skipped as its acceptance
        // evidence.
        // The manifest's identity string is "<CLR full type name>, <assembly simple name>". The generator built it
        // from Roslyn's FullyQualifiedFormat (Ns.Outer.Inner); the runtime builds type.FullName (Ns.Outer+Inner).
        // Verified red at authoring time *earlier* than the plan predicted: ExtensionBinder.CollectTypes enumerated
        // namespace members only and never descended into nested types, so the template degraded at build time
        // (manifest entry Absent) before the AQN strings could ever be compared. Both halves are fixed: the scan
        // now recurses through INamedTypeSymbol.GetTypeMembers(), and both tiers format the identity through the
        // shared Precompiled/AqnFormatter, which joins nesting segments with '+'.
        // ---------------------------------------------------------------------------------------------------
        [Fact]
        public void NestedExtensionType_BindsAndCrossesTheGauntlet()
        {
            var (precompiled, dyn) = DifferentialHarness.RenderViaResolver(
                "drift-nested.heddle", "@model(){{System.String}}@\\\n@driftnested(this)\n", typeof(string), "hi");
            Assert.Equal(dyn, precompiled);
        }

        // ---------------------------------------------------------------------------------------------------
        // phase 3 F3 — inherited [ExtensionName].
        // The runtime reads [ExtensionName] with inherit: true, so DriftInheritedExtension registers under
        // "driftbase" and replaces its base (IsAssignableFrom). The generator's declared-only GetAttributes() read
        // never sees the subclass and binds the base. Verified red at authoring time:
        // PrecompiledMismatchException(ExtensionBindingMismatch) — "manifest=…DriftBaseExtension
        // live=…DriftInheritedExtension" — on every render, permanently.
        // ---------------------------------------------------------------------------------------------------
        // FIXED by phase 3 (WI3); un-skipped as its acceptance evidence. ExtensionBinder now reads
        // [ExtensionName] over the base-type chain (the same walk the file already used for [BranchRole],
        // [ScopeChannel] and [Prop]) and resolves the resulting collision through the shared
        // ExtensionRegistrationRules precedence — so the subclass takes the name on the build tier too.
        [Fact]
        public void InheritedExtensionNameSubclass_CrossesTheGauntlet()
        {
            var (precompiled, dyn) = DifferentialHarness.RenderViaResolver(
                "drift-inherited.heddle", "@model(){{System.String}}@\\\n@driftbase(this)\n", typeof(string), "hi");
            Assert.Equal(dyn, precompiled);
        }

        // ---------------------------------------------------------------------------------------------------
        // phase 1 F11 — needsLocals / [ScopeChannel] participant detection. RESOLVED by phase 1 (WI1/WI4);
        // un-skipped, and RESHAPED, because what phase 0 recorded as "verified red earlier than the plan
        // predicted" turned out to be the whole story rather than a detail.
        //
        // The drift: RuntimeDocument.ComputeNeedsLocals walks every item of a chain and recurses into nested chain
        // parameters; the generator probed chain.Chain[0] only. Phase 1 replaced both generator probes with the
        // shared, full-chain, parameter-recursing Language/ParticipantScan, so the rule now exists once and the
        // scan-level divergence is closed by construction (ParticipantScanLockstepTests drives it directly,
        // including the shadowed-name over-provision Q1.4 rules to keep).
        //
        // Why this fixture could not keep its original assertion. Assert.Equal(dyn, precompiled) needs the shape to
        // REACH the precompiled tier, and it still does not: every way a participant can sit non-leftmost is a
        // shape the emitter refuses for an unrelated, pre-existing reason — a multi-item chain is "chained call",
        // and a nested chain parameter naming an extension is "chain item extension". Phase 1 did not lift either
        // refusal (neither is a drift; both are emitter coverage), so the scan gap remains LATENT on the
        // precompiled tier and a byte comparison of @yell(@row()) would compare two dynamic renders — vacuous.
        //
        // What it asserts instead, following the precedent phase 4 set for its overload-tie fixture: the
        // conjunction the drift actually needs. (1) The control shape degrades identically on both tiers, so the
        // latency is pinned as a fact rather than a memory — if a later phase teaches the emitter nested chain
        // parameters, THIS assertion goes red and forces the byte comparison to be restored. (2) A neighbouring
        // template whose participant IS reachable still precompiles and renders byte-identically, proving the
        // participant machinery works rather than being switched off. (3) The scan rule itself agrees with the
        // runtime on the very shape the old probe missed — asserted through the shared scan, which is where the
        // rule now lives.
        // ---------------------------------------------------------------------------------------------------
        [Fact]
        public void NonLeftmostScopeChannelParticipant_ProvisionsLocalsOnBothTiers()
        {
            // (1) The control shape: a participant reachable only as a nested chain parameter. The emitter refuses
            // the nested parameter itself, so the template degrades — identically to the participant-free twin,
            // which is what "the drift is latent, not observable" means precisely.
            const string nested = "@model(){{System.String}}@\\\n@yell(@row())\n";
            var nestedGen = DifferentialHarness.Generate(new[] { ("drift-locals.heddle", nested) });
            Assert.DoesNotContain(nestedGen.Diagnostics,
                d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
            DifferentialHarness.ExpectDegrade(nestedGen, "drift-locals.heddle");

            const string control = "@model(){{System.String}}@\\\n@yell(@yell(this))\n";
            var controlGen = DifferentialHarness.Generate(new[] { ("drift-locals-control.heddle", control) });
            DifferentialHarness.ExpectDegrade(controlGen, "drift-locals-control.heddle");

            // (2) The reachable neighbour: the same participant, leftmost, still precompiles and matches.
            var (precompiled, dyn) = DifferentialHarness.Render(
                "drift-locals-ok.heddle", "@model(){{System.String}}@\\\n@row()@row()\n", typeof(string), "hi");
            Assert.Equal(dyn, precompiled);
            Assert.Contains("even", precompiled);
            Assert.Contains("odd", precompiled);

            // (3) The rule the drift was about, asserted where it now lives: the shared scan finds a participant
            // the old leftmost-only probe missed. Heddle.Tests.ParticipantScanLockstepTests carries the full
            // corpus, the legacy-probe characterization, and the runtime lockstep.
            var parse = Heddle.Language.DocumentParser.Parse("@(else())",
                new Heddle.Language.ParserSettings { RootPath = string.Empty }, out _);
            Assert.True(Heddle.Language.ParticipantScan.BodyHostsParticipant(parse,
                name => Heddle.Runtime.TemplateFactory.TryGetExtensionType(name, out var type) &&
                        type.IsHaveAttribute<Heddle.Attributes.ScopeChannelAttribute>(true)));
        }

        // ---------------------------------------------------------------------------------------------------
        // phase 4 F3 — function overload selection. FIXED by phase 4 WI8 (D10); un-skipped as its acceptance
        // evidence.
        // The runtime's rank vector is flat (every widening ranks 1), so min(1, 2u) leaves (long,long),
        // (double,double) and (decimal,decimal) non-dominated → HED1013 ambiguity. The generator delegated
        // resolution to the consumer's C# compiler, which picks Min(long, long) and emitted a template that renders.
        // Verified red at authoring time: the precompiled tier renders while the dynamic compile fails with
        // "HED1013: The call to function 'min' is ambiguous …" — the two tiers disagree about whether the template
        // is even legal.
        //
        // Both sides now consult the shared OverloadRank core, which encodes Heddle's flat Pareto rank (OQ2:
        // the runtime is normative). The fixture's assertion had to change shape with the fix, and deliberately got
        // *stronger* rather than weaker: the drift was "one tier renders, the other rejects", so the post-fix
        // property is verdict identity — and a template both tiers reject has no rendered bytes for the original
        // Assert.Equal(dyn, precompiled) to compare. It is asserted here as the conjunction the drift actually
        // needed: the ambiguous call degrades at build time *and* the dynamic tier raises HED1013 (identical
        // verdicts), while the resolvable tie next to it still precompiles and renders byte-identically (proving
        // the guard resolves rather than blanket-degrades).
        // ---------------------------------------------------------------------------------------------------
        [Fact]
        public void OverloadTie_ResolvesIdenticallyOnBothTiers()
        {
            const string key = "drift-overload.heddle";
            const string content = "@model(){{System.String}}@\\\n@(min(1, 2u))\n";

            // Build tier: the shared ranker reports the same three-way non-dominated front the runtime does, so the
            // template degrades instead of emitting C#'s Min(long, long).
            var gen = DifferentialHarness.Generate(new[] { (key, content) });
            Assert.DoesNotContain(gen.Diagnostics, d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
            DifferentialHarness.ExpectDegrade(gen, key);

            // Run tier: the same call is the runtime's ambiguity error — the verdict both tiers now share.
            var ambiguous = new HeddleTemplate(content, new CompileContext(new TemplateOptions(), typeof(string)));
            Assert.False(ambiguous.CompileResult.Success);
            Assert.Contains(ambiguous.CompileResult.ErrorList,
                e => e.DiagnosticId == HeddleDiagnosticIds.AmbiguousFunctionCall);

            // The resolvable tie still binds on both tiers and renders identically — min(1, 2) is not ambiguous
            // under the flat rank (only (int,int) is non-dominated), and the generator emits it cast-pinned.
            var (precompiled, dyn) = DifferentialHarness.Render(
                "drift-overload-ok.heddle", "@model(){{System.String}}@\\\n@(min(1, 2))\n", typeof(string), "hi");
            Assert.Equal(dyn, precompiled);
            Assert.Contains("1", precompiled);
        }
    }
}
