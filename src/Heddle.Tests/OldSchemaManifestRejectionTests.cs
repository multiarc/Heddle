using System;
using System.Collections.Generic;
using Heddle.Precompiled;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// <para>Q8.2, the binary break, <b>demonstrated</b> rather than asserted. The schema-4 prop-layout fingerprint
    /// landed as an optional third constructor parameter on <see cref="PrecompiledExtensionBinding"/>, so the
    /// two-argument <c>.ctor(string, string)</c> that every schema 1–3 manifest calls no longer exists in metadata.
    /// <c>MinSupportedSchemaVersion</c> was still <c>1</c>, so the registration gate <em>accepted</em> exactly the
    /// manifests that cannot run: the fault surfaced as a <see cref="MissingMethodException"/> out of
    /// <c>PrecompiledTemplates.Register</c> at host startup — not a degrade, not a render, and not the clean fallback
    /// the fallback taxonomy promises for a manifest outside the schema window.</para>
    ///
    /// <para>The three tests below are a controlled comparison. The fixture assembly's IL is old-schema in every case
    /// (see <see cref="OldSchemaManifestFixture"/> for how, and for why it is not the optional-parameter substitution);
    /// only the schema the manifest <em>declares</em> changes. Declaring <c>3</c> — below the new
    /// <c>MinSupportedSchemaVersion</c> — is rejected cleanly, with the manifest type never instantiated. Declaring
    /// <c>4</c> is admitted and faults, which is what the boundary is for: it shows the gate is the only thing standing
    /// between a 2.0-built consumer and a startup crash, and that <c>Min = 4</c> is exactly where it belongs.</para>
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

        /// <summary>The break, in metadata. Every other claim here rests on this one: the constructor a schema 1–3
        /// manifest's IL names is gone, so such a manifest cannot run at all — which is why the compatibility window
        /// must not claim to accept it.</summary>
        [Fact]
        public void TheTwoArgumentExtensionBindingConstructorIsGoneFromMetadata()
        {
            Assert.False(OldSchemaManifestFixture.LiveTypeHasTheTwoArgumentConstructor(),
                "PrecompiledExtensionBinding regained a 2-arg .ctor. If that is intentional, " +
                "MinSupportedSchemaVersion can come back down and this whole file should be reconsidered.");

            // And the three-parameter form is the only one, so the schema-4 field really did replace the old arity
            // rather than adding beside it (BindDefinition, facing the same problem, added three real overloads).
            var ctor = Assert.Single(typeof(PrecompiledExtensionBinding).GetConstructors());
            Assert.Equal(3, ctor.GetParameters().Length);
        }

        /// <summary>The clean rejection, on a real old manifest. <c>SchemaVersionUnsupported</c> with the window in the
        /// detail, one callback, no throw — and, load-bearing, the manifest type is never instantiated, so the gate
        /// short-circuits <em>before</em> the <c>Activator.CreateInstance</c> that used to fault.</summary>
        [Fact]
        public void AGenuineOldSchemaManifestIsRejectedCleanlyByTheGate()
        {
            var events = new List<PrecompiledFallbackEvent>();
            PrecompiledTemplates.OnFallback = e => events.Add(e);

            var assembly = OldSchemaManifestFixture.Load(OldSchemaManifestFixture.Emit(schemaVersion: 3));

            // No throw is half the assertion; the other half is that nothing from the assembly registered.
            PrecompiledTemplates.Register(assembly);

            var evt = Assert.Single(events);
            Assert.Equal(PrecompiledFallbackReason.SchemaVersionUnsupported, evt.Reason);
            Assert.Equal(HeddleDiagnosticIdsForFallback, evt.DiagnosticId);
            Assert.Contains("manifest=3", evt.Detail);
            Assert.Contains(
                "supported=" + PrecompiledSchema.MinSupportedSchemaVersion + "-" +
                PrecompiledSchema.MaxSupportedSchemaVersion, evt.Detail);
            Assert.Empty(PrecompiledTemplates.Entries);
        }

        /// <summary>The control arm, and the reason the boundary is at 4 rather than anywhere else: the same
        /// old-schema IL, declaring a schema <em>inside</em> the window, is admitted — and then faults with a
        /// <see cref="MissingMethodException"/> naming the missing constructor, out of <c>Register</c>. This is the
        /// shipped 2.0 behaviour for every real schema 1–3 manifest, pinned so the claim "the gate prevents a startup
        /// crash" is evidence rather than narration.</summary>
        [Fact]
        public void TheSameOldSchemaIlFaultsWhenTheGateAdmitsIt()
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
