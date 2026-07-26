using System;
using System.Collections.Generic;
using Heddle.Precompiled;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// <para>The binary break, <b>demonstrated</b> rather than asserted. The prop-layout fingerprint landed as
    /// an optional third constructor parameter on <see cref="PrecompiledExtensionBinding"/>, so the two-argument
    /// <c>.ctor(string, string)</c> that every <b>released</b> manifest calls no longer exists in metadata.
    /// <c>MinSupportedSchemaVersion</c> was still <c>1</c>, so the registration gate <em>accepted</em> exactly the
    /// manifests that cannot run: the fault surfaced as a <see cref="MissingMethodException"/> out of
    /// <c>PrecompiledTemplates.Register</c> at host startup — not a degrade, not a render, and not the clean fallback
    /// the fallback taxonomy promises for a manifest outside the schema window.</para>
    ///
    /// <para><b>Which manifests those are.</b> Verified against the <c>v2.0.0</c> tag: the shipped generator emitted
    /// <c>schemaVersion: 2</c> and the shipped engine accepted <c>1–2</c>, so the released set is <b>{1, 2}</b> and
    /// nothing above 2 has ever left the repository. This file used to name schema 3 as the rejected shape and
    /// schema 4 as the admitted one, on the reasoning that "the break already shipped in 2.0.0" — both numbers, and
    /// the reasoning, were wrong: schemas 3–5 were unreleased and have since been collapsed into a single schema 3,
    /// so the break is <em>pending</em>, lands in 2.1, and its victim set is precisely the released pair.</para>
    ///
    /// <para>The tests below are a controlled comparison. The fixture assembly's IL is released-schema in every case
    /// (see <see cref="OldSchemaManifestFixture"/> for how, and for why it is not the optional-parameter
    /// substitution); only the schema the manifest <em>declares</em> changes. Declaring a released schema — below
    /// <c>MinSupportedSchemaVersion</c> — is rejected cleanly, with the manifest type never instantiated. Declaring
    /// <c>Min</c> itself is admitted and faults, which is what the boundary is for: it shows the gate is the only
    /// thing standing between a 2.0-built consumer and a startup crash, and that the floor is exactly where it
    /// belongs.</para>
    /// </summary>
    [Collection("PrecompiledRegistrySerial")]
    public class OldSchemaManifestRejectionTests : IDisposable
    {
        public OldSchemaManifestRejectionTests() => PrecompiledTemplates.ResetForTests();

        public void Dispose()
        {
            PrecompiledTemplates.OnFallback = null;
            PrecompiledTemplates.ResetForTests();
        }

        /// <summary>The break, in metadata. Every other claim here rests on this one: the constructor a released
        /// schema 1–2 manifest's IL names is gone, so such a manifest cannot run at all — which is why the
        /// compatibility window must not claim to accept it.</summary>
        [Fact]
        public void TheTwoArgumentExtensionBindingConstructorIsGoneFromMetadata()
        {
            Assert.False(OldSchemaManifestFixture.LiveTypeHasTheTwoArgumentConstructor(),
                "PrecompiledExtensionBinding regained a 2-arg .ctor. If that is intentional, " +
                "MinSupportedSchemaVersion can come back down and this whole file should be reconsidered.");

            // And the three-parameter form is the only one, so the prop-layout field really did replace the old arity
            // rather than adding beside it (BindDefinition, facing the same problem, added three real overloads).
            var ctor = Assert.Single(typeof(PrecompiledExtensionBinding).GetConstructors());
            Assert.Equal(3, ctor.GetParameters().Length);
        }

        /// <summary>
        /// <para>The clean rejection, on a real released manifest — <b>both</b> released schemas, because both are in
        /// the break's victim set and a test that covered only one would leave the other's disposition unpinned.
        /// <c>SchemaVersionUnsupported</c> with the window in the detail, one callback, no throw — and, load-bearing,
        /// the manifest type is never instantiated, so the gate short-circuits <em>before</em> the
        /// <c>Activator.CreateInstance</c> that would fault.</para>
        /// <para>The schemas are literals rather than <c>Min - 1</c>: they are the shipped generator's actual output,
        /// a fact about the <c>v2.0.0</c> tag, and deriving them from the current floor would make the test agree
        /// with any floor at all.</para>
        /// </summary>
        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        public void AGenuineReleasedSchemaManifestIsRejectedCleanlyByTheGate(int releasedSchema)
        {
            var events = new List<PrecompiledFallbackEvent>();
            PrecompiledTemplates.OnFallback = e => events.Add(e);

            var assembly = OldSchemaManifestFixture.Load(
                OldSchemaManifestFixture.Emit(schemaVersion: releasedSchema));

            // No throw is half the assertion; the other half is that nothing from the assembly registered.
            PrecompiledTemplates.Register(assembly);

            var evt = Assert.Single(events);
            Assert.Equal(PrecompiledFallbackReason.SchemaVersionUnsupported, evt.Reason);
            Assert.Equal(HeddleDiagnosticIdsForFallback, evt.DiagnosticId);
            Assert.Contains("manifest=" + releasedSchema, evt.Detail);
            Assert.Contains(
                "supported=" + PrecompiledSchema.MinSupportedSchemaVersion + "-" +
                PrecompiledSchema.MaxSupportedSchemaVersion, evt.Detail);
            Assert.Empty(PrecompiledTemplates.Entries);
        }

        /// <summary>The control arm, and the reason the boundary is where it is: the same released-schema IL,
        /// declaring a schema <em>inside</em> the window, is admitted — and then faults with a
        /// <see cref="MissingMethodException"/> naming the missing constructor, out of <c>Register</c>. This is what
        /// would happen to every real released manifest if the floor stayed at 1, pinned so the claim "the gate
        /// prevents a startup crash" is evidence rather than narration.</summary>
        [Fact]
        public void TheSameReleasedSchemaIlFaultsWhenTheGateAdmitsIt()
        {
            var events = new List<PrecompiledFallbackEvent>();
            PrecompiledTemplates.OnFallback = e => events.Add(e);

            var assembly = OldSchemaManifestFixture.Load(
                OldSchemaManifestFixture.Emit(schemaVersion: PrecompiledSchema.MinSupportedSchemaVersion));

            var ex = Assert.ThrowsAny<MissingMethodException>(() => PrecompiledTemplates.Register(assembly));
            Assert.Contains("PrecompiledExtensionBinding", ex.Message);
            Assert.Empty(events);
        }

        private const string HeddleDiagnosticIdsForFallback = "HED7102";
    }
}
