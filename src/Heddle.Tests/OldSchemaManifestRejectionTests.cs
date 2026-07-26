using System;
using System.Collections.Generic;
using Heddle.Precompiled;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// Regression tests for a binary break: PrecompiledExtensionBinding gained an optional parameter, breaking
    /// released (v2.0.0) manifests. Without raising <c>MinSupportedSchemaVersion</c> to exclude them, those
    /// manifests crash at startup with <see cref="MissingMethodException"/> instead of the clean fallback they
    /// should receive. Tests verify that the gate now rejects released schemas cleanly and admits only supported ones.
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

        /// <summary>The foundation: released manifests call a 2-arg constructor that no longer exists in metadata.</summary>
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
        /// Rejection is clean: <c>SchemaVersionUnsupported</c> callback, no throw, manifest never instantiated
        /// (gate short-circuits before the failing <c>Activator.CreateInstance</c>). Tests both released schemas.
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

        /// <summary>Control: released IL declaring a supported schema is admitted and faults. Shows the gate prevents startup crash.</summary>
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
