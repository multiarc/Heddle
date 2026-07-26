using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// Generator plan phase 1 WI1 (D2) — the <c>needsLocals</c> parity fixtures.
    /// <para>A definition call site builds <b>two</b> carriers that host two different documents: the inner one
    /// holds the definition body, the outer one holds this invocation site's caller content. The dynamic tier
    /// derives each carrier's frame-provisioning flag from its own document
    /// (<c>AbstractExtension.InitStart</c> → <c>RuntimeDocument.NeedsLocals</c>). The emitter used to OR the two
    /// flags together and hand the result to both carriers, which is not a harmless over-provision: under
    /// <c>AbstractExtension.GetInnerResult</c>, <c>needsLocals: false</c> is the instruction to hand a
    /// <em>cleared</em> frame to a non-participating body, and the OR replaced that with a fresh one.</para>
    /// <para>The fixtures are written the way the plan requires — asserting the <b>dynamic</b> tier's bytes, which
    /// is why they failed on the precompiled tier before the fix. <c>@flag</c> publishes opportunistically and
    /// <c>@peek</c> reads, and neither carries <c>[ScopeChannel]</c>, so a body holding only those two is
    /// non-participating on both tiers and its rendered text reports whether it was given a frame regardless.</para>
    /// </summary>
    public class ScopeParticipantDifferentialTests
    {
        private const string Header = "@model(){{System.String}}@\\\n";

        private static void AssertParity(string key, string content, string model, string expected)
        {
            var gen = DifferentialHarness.Generate(new[] { (key, content) });
            DifferentialHarness.ExpectPrecompiled(gen, key);

            var (precompiled, dyn) = DifferentialHarness.Render(key, content, typeof(string), model);
            Assert.Equal(dyn, precompiled);
            Assert.Contains(expected, precompiled);
        }

        /// <summary>
        /// scope-carrier-asymmetric — the definition BODY participates, the caller content does not. The outer
        /// carrier must get <c>callerContentNeedsLocals: false</c>, so the caller content sees no frame and
        /// <c>@peek</c> reports "unseen"; with the OR it saw a fresh frame and reported "seen".
        /// </summary>
        [Fact]
        public void BodyParticipatesCallerContentDoesNot()
        {
            const string key = "views/scope-carrier-asymmetric.heddle";
            var t = Header +
                    "@%<box>{{[@gate(this)]@out()}}%@\n" +
                    "@box(this){{@flag(this)@peek(this)}}";
            AssertParity(key, t, "hi", "unseen");
        }

        /// <summary>
        /// scope-carrier-asymmetric (mirror) — the caller content participates, the definition body does not. The
        /// inner carrier must get <c>bodyNeedsLocals: false</c>.
        /// </summary>
        [Fact]
        public void CallerContentParticipatesBodyDoesNot()
        {
            const string key = "views/scope-carrier-asymmetric-mirror.heddle";
            var t = Header +
                    "@%<plain>{{@flag(this)@peek(this)[@out()]}}%@\n" +
                    "@plain(this){{@gate(this)}}";
            AssertParity(key, t, "hi", "unseen");
        }

        /// <summary>Both carriers participate — the row the OR happened to get right, kept so the fix is shown to
        /// be a split rather than a blanket "false".</summary>
        [Fact]
        public void BothCarriersParticipate()
        {
            const string key = "views/scope-carrier-both.heddle";
            var t = Header +
                    "@%<box>{{[@gate(this)@peek(this)]@out()}}%@\n" +
                    "@box(this){{@gate(this)@peek(this)}}";
            AssertParity(key, t, "hi", "seen");
        }

        /// <summary>Neither carrier participates — the other row the OR got right.</summary>
        [Fact]
        public void NeitherCarrierParticipates()
        {
            const string key = "views/scope-carrier-neither.heddle";
            var t = Header +
                    "@%<box>{{[@flag(this)@peek(this)]@out()}}%@\n" +
                    "@box(this){{@flag(this)@peek(this)}}";
            AssertParity(key, t, "hi", "unseen");
        }

        /// <summary>
        /// scope-selfcall-participant — the <c>GetOrBuildDefinitionBody</c> pre-mark path under the shared
        /// recursive scan: a definition whose body both hosts a participant and calls itself. The pre-mark exists
        /// so the self-call encountered mid-population bakes the right flag; this fixture is what would catch it
        /// baking the wrong one.
        /// </summary>
        [Fact]
        public void SelfCallingDefinitionWithAParticipant()
        {
            const string key = "views/scope-selfcall-participant.heddle";
            var t = Header +
                    "@%<walk>{{[@gate(this)@ifnot(this){{@walk()}}]}}%@\n" +
                    "@walk(this)";
            var gen = DifferentialHarness.Generate(new[] { (key, t) });
            var (precompiled, dyn) = DifferentialHarness.Render(key, t, typeof(string), "hi");
            Assert.Equal(dyn, precompiled);
            Assert.NotNull(gen);
        }
    }
}
