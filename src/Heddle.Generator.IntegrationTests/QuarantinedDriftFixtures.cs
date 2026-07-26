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
    /// The <b>quarantine register</b>. Each fixture below is a guarded, gauntlet-crossing test of a known live drift.
    /// Every one of them fails on today's code, so each ships explicitly skipped with its issue named in the <c>Skip</c>
    /// string: the fix group un-skips its fixture as acceptance evidence. The register is:
    /// <list type="table">
    /// <item><description>content-hash input mismatch — BOM'd file under file-backed staleness (<b>fixed</b>, un-skipped).</description></item>
    /// <item><description>nested/generic AQN identity — a nested extension type (<b>fixed</b>, un-skipped).</description></item>
    /// <item><description>inherited <c>[ExtensionName]</c> subclass (<b>fixed</b>, un-skipped).</description></item>
    /// <item><description>non-leftmost <c>[ScopeChannel]</c> participant (<b>fixed</b>, un-skipped; the drift is latent on the precompiled tier — see the fixture's own note).</description></item>
    /// <item><description>overload-rank tie (<c>min(1, 2u)</c>) (<b>fixed</b>, un-skipped; reshaped to report HED7025 error — see the fixture's own note).</description></item>
    /// </list>
    /// </summary>
    [Collection("PrecompiledRegistry")]
    public class QuarantinedDriftFixtures : PrecompiledRegistryTestBase
    {
        // Content-hash input mismatch (FIXED). The generator hashes Roslyn's decoded SourceText re-encoded as UTF-8
        // without a BOM; the runtime used to hash the raw file byte stream, so any .heddle file saved with a BOM
        // (or as UTF-16) failed PrecompiledGauntlet.CheckStaleness on every request under EnableFileChangeCheck and
        // silently took the dynamic path. Both sides now hash decoded text through ContentHash.HashText.
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

        // Nested/generic AQN identity (FIXED). The manifest's identity string is "<CLR full type name>, <assembly simple
        // name>". The generator built it from Roslyn's FullyQualifiedFormat (Ns.Outer.Inner); the runtime builds
        // type.FullName (Ns.Outer+Inner). ExtensionBinder.CollectTypes enumerated namespace members only and never
        // descended into nested types, so templates degraded at build time before the AQN strings could be compared.
        // Both halves are fixed: the scan now recurses through INamedTypeSymbol.GetTypeMembers(), and both tiers
        // format the identity through the shared Precompiled/AqnFormatter.
        [Fact]
        public void NestedExtensionType_BindsAndCrossesTheGauntlet()
        {
            var (precompiled, dyn) = DifferentialHarness.RenderViaResolver(
                "drift-nested.heddle", "@model(){{System.String}}@\\\n@driftnested(this)\n", typeof(string), "hi");
            Assert.Equal(dyn, precompiled);
        }

        // Inherited [ExtensionName] (FIXED). The runtime reads [ExtensionName] with inherit: true, so an inherited
        // extension registers under the base name and replaces its base (IsAssignableFrom). The generator's declared-only
        // GetAttributes() read never saw the subclass and bound the base. ExtensionBinder now reads [ExtensionName] over
        // the base-type chain (the same walk used for [BranchRole], [ScopeChannel] and [Prop]) and resolves the
        // collision through the shared ExtensionRegistrationRules precedence — so the subclass takes the name on both tiers.
        [Fact]
        public void InheritedExtensionNameSubclass_CrossesTheGauntlet()
        {
            var (precompiled, dyn) = DifferentialHarness.RenderViaResolver(
                "drift-inherited.heddle", "@model(){{System.String}}@\\\n@driftbase(this)\n", typeof(string), "hi");
            Assert.Equal(dyn, precompiled);
        }

        // Needslocals / [ScopeChannel] participant detection (FIXED). RuntimeDocument.ComputeNeedsLocals walks every
        // item of a chain and recurses into nested chain parameters; the generator probed chain.Chain[0] only. Both
        // generator probes are replaced with the shared, full-chain, parameter-recursing Language/ParticipantScan.
        //
        // This fixture could not keep the original Assert.Equal(dyn, precompiled) because the shape still does not
        // reach the precompiled tier: every way a participant can sit non-leftmost is a shape the emitter refuses
        // for an unrelated, pre-existing reason. The scan gap remains latent on the precompiled tier.
        //
        // Instead, the fixture asserts two things: (1) DEGRADE PARITY — the control shape degrades identically to
        // a participant-free twin, so latency is pinned as fact; (2) THE OBSERVABLE HALF — the per-carrier flag
        // asymmetry, which is observable and pinned here.
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

            // (2) The observable half of drift #3, next to the latent one: a definition call whose BODY hosts a
            // participant (@gate) while its caller content does not. The dynamic tier hands the outer carrier a
            // cleared frame, so @peek reports "unseen"; the emitter's OR'd flag used to hand it a fresh one and
            // report "seen". The template must precompile — a degrade here would make the byte comparison vacuous
            // in exactly the way clause (1) documents for the nested shape.
            const string asymmetricKey = "drift-locals-asymmetric.heddle";
            const string asymmetric = "@model(){{System.String}}@\\\n" +
                                      "@%<box>{{[@gate(this)]@out()}}%@\n" +
                                      "@box(this){{@flag(this)@peek(this)}}";
            var asymmetricGen = DifferentialHarness.Generate(new[] { (asymmetricKey, asymmetric) });
            DifferentialHarness.ExpectPrecompiled(asymmetricGen, asymmetricKey);

            var (precompiled, dyn) = DifferentialHarness.Render(
                asymmetricKey, asymmetric, typeof(string), "hi");
            Assert.Equal(dyn, precompiled);
            Assert.Contains("unseen", precompiled);
        }

        // Function overload selection (FIXED). The runtime's rank vector is flat (every widening ranks 1), so
        // min(1, 2u) leaves three non-dominated candidates → HED1013 ambiguity. The generator delegated resolution
        // to the consumer's C# compiler, which picked one and emitted a template that renders. The two tiers disagreed
        // about whether the template was legal. Both sides now consult the shared OverloadRank core. The fixture asserts
        // verdict identity: the ambiguous call is refused at build time with HED7025 *and* the dynamic tier raises HED1013
        // (identical error verdicts), while the resolvable tie next to it still precompiles and renders byte-identically
        // (proving the guard resolves rather than blanket-degrades).
        [Fact]
        public void OverloadTie_ResolvesIdenticallyOnBothTiers()
        {
            const string key = "drift-overload.heddle";
            const string content = "@model(){{System.String}}@\\\n@(min(1, 2u))\n";

            // Build tier: the shared ranker reports the same three-way non-dominated front the runtime does. The
            // template is not precompiled AND the build says why — HED7025, at Error, naming the collided candidates.
            var gen = DifferentialHarness.Generate(new[] { (key, content) });
            var reported = Assert.Single(gen.Diagnostics,
                d => d.Id == HeddleDiagnosticIds.BuildFunctionCallNotBindable);
            Assert.Equal(Microsoft.CodeAnalysis.DiagnosticSeverity.Error, reported.Severity);
            Assert.Contains("min(long, long)", reported.GetMessage());
            DifferentialHarness.ExpectDegrade(gen, key);

            // Run tier: the same call is the runtime's ambiguity error — the verdict both tiers now share, at the
            // same severity, on the same template.
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
