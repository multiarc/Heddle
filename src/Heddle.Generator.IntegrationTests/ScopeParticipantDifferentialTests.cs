using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// The <c>needsLocals</c> parity fixtures. Definition call sites build two carriers (inner body, outer caller
    /// content), each provisioning its frame independently. This validates the fix to the bug where flags were ORed
    /// together instead of kept separate per carrier.
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
        /// A definition whose body both hosts a participant and calls itself; the pre-mark flag must survive the
        /// mid-population self-call to avoid baking the wrong flag.
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
