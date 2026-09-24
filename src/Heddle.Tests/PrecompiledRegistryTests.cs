using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Heddle.Data;
using Heddle.Precompiled;
using Heddle.Precompiled.CompiledForm;
using Xunit;

namespace Heddle.Tests
{
    [CollectionDefinition("PrecompiledRegistrySerial", DisableParallelization = true)]
    public sealed class PrecompiledRegistrySerialCollection { }

    /// <summary>Validates registry registration, duplicate-key rejection, per-assembly idempotence, normalized <see cref="PrecompiledTemplates.TryGet"/>, and case-mismatch fallback. Rows arrive through the loader's internal constructor over an in-memory artifact, registered through a dynamic marker assembly. Serialized — the registry is process-global static state.</summary>
    [Collection("PrecompiledRegistrySerial")]
    public class PrecompiledRegistryTests : IDisposable
    {
        private readonly Action<PrecompiledFallbackEvent> _savedCallback;

        public PrecompiledRegistryTests()
        {
            _savedCallback = PrecompiledTemplates.OnFallback;
            PrecompiledTemplates.ResetForTests();
        }

        public void Dispose()
        {
            PrecompiledTemplates.OnFallback = _savedCallback;
            PrecompiledTemplates.ResetForTests();
        }

        private static Version RuntimeVersion =>
            typeof(PrecompiledTemplates).Assembly.GetName().Version ?? new Version(1, 0, 0, 0);

        private static string CompatibleVersion =>
            $"{RuntimeVersion.Major}.{Math.Max(RuntimeVersion.Minor, 0)}.{Math.Max(RuntimeVersion.Build, 0)}";

        /// <summary>Uses the minimum supported schema version, not a literal, so tests use the oldest schema the engine accepts.</summary>
        private static int SupportedSchema => PrecompiledSchema.MinSupportedSchemaVersion;

        private static CompiledArtifact OneRow(string key)
        {
            var artifact = CompiledFormHarness.MinimalArtifact();
            artifact.Templates.Add(CompiledFormHarness.TemplateRow(key));
            return artifact;
        }

        /// <summary>Registers one artifact's rows through a dynamic marker assembly carrying the given
        /// schema version — the only hand control the suite needs over the marker.</summary>
        private static Assembly BuildAssembly(CompiledArtifact artifact, int schema, string engineVersion,
            string name)
        {
            CompiledFormHarness.HarnessMarker.Image = CompiledFormWriter.Write(artifact);
            var ab = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(name), AssemblyBuilderAccess.Run);
            var ctor = typeof(HeddleCompiledTemplatesAttribute)
                .GetConstructor(new[] { typeof(Type), typeof(int), typeof(string) });
            ab.SetCustomAttribute(new CustomAttributeBuilder(ctor,
                new object[] { typeof(CompiledFormHarness.HarnessMarker), schema, engineVersion }));
            return ab;
        }

        private static Assembly Register(CompiledArtifact artifact, int schema, string engineVersion,
            string name)
        {
            var asm = BuildAssembly(artifact, schema, engineVersion,
                name + "_" + Guid.NewGuid().ToString("N"));
            PrecompiledTemplates.Register(asm);
            return asm;
        }

        [Fact]
        public void RegisterThenTryGet()
        {
            Register(OneRow("reg/one.heddle"), SupportedSchema, CompatibleVersion, "HeddleTestAsm_One");

            Assert.True(PrecompiledTemplates.TryGet("reg/one.heddle", out var entry));
            Assert.Equal("reg/one.heddle", entry.Key);
            // Normalization: backslashes and missing extension resolve to the same key.
            Assert.True(PrecompiledTemplates.TryGet("reg\\one", out _));
        }

        [Fact]
        public void RegisterIsIdempotentPerAssembly()
        {
            var asm = Register(OneRow("reg/one.heddle"), SupportedSchema, CompatibleVersion,
                "HeddleTestAsm_Idem");
            PrecompiledTemplates.Register(asm); // no throw, no duplicate
            Assert.Single(PrecompiledTemplates.Entries);
        }

        [Fact]
        public void DuplicateKeyAcrossAssembliesThrows()
        {
            var a = Register(OneRow("reg/dup.heddle"), SupportedSchema, CompatibleVersion, "HeddleTestAsm_DupA");
            var b = BuildAssembly(OneRow("reg/dup.heddle"), SupportedSchema, CompatibleVersion,
                "HeddleTestAsm_DupB_" + Guid.NewGuid().ToString("N"));
            var ex = Assert.Throws<PrecompiledRegistrationException>(() => PrecompiledTemplates.Register(b));
            Assert.Equal("reg/dup.heddle", ex.Key);
            Assert.Equal(a.GetName().Name, ex.ExistingAssemblyName);
            Assert.Equal(b.GetName().Name, ex.NewAssemblyName);
            Assert.Contains("already registered", ex.Message);
        }

        /// <summary>A marker below the compiled-form schema is a 2.x manifest: refused before the artifact
        /// is touched, by throw — not by fallback, since there is no entry to degrade.</summary>
        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        public void BelowCompiledFormSchemaThrowsWithoutRegistering(int schema)
        {
            var asm = BuildAssembly(OneRow("reg/one.heddle"), schema, CompatibleVersion,
                "HeddleTestAsm_Schema_" + Guid.NewGuid().ToString("N"));
            Assert.Throws<PrecompiledRegistrationException>(() => PrecompiledTemplates.Register(asm));
            Assert.False(PrecompiledTemplates.TryGet("reg/one.heddle", out _));
        }

        /// <summary>Above the supported window the marker is well-formed but unreadable: the assembly is
        /// refused whole through the HED7102 fallback and nothing registers.</summary>
        [Fact]
        public void AboveSupportedSchemaIgnoresManifest()
        {
            PrecompiledFallbackEvent? captured = null;
            PrecompiledTemplates.OnFallback = e => captured = e;
            var schema = PrecompiledSchema.MaxSupportedSchemaVersion + 1;
            Register(OneRow("reg/one.heddle"), schema, CompatibleVersion, "HeddleTestAsm_SchemaHi");
            Assert.False(PrecompiledTemplates.TryGet("reg/one.heddle", out _));
            Assert.NotNull(captured);
            Assert.Equal(PrecompiledFallbackReason.SchemaVersionUnsupported, captured.Value.Reason);
            Assert.Equal($"SchemaVersion: manifest={schema} supported=" +
                $"{PrecompiledSchema.MinSupportedSchemaVersion}-{PrecompiledSchema.MaxSupportedSchemaVersion}",
                captured.Value.Detail);
            Assert.Equal("HED7102", captured.Value.DiagnosticId);
        }

        [Fact]
        public void IncompatibleEngineVersionIgnoresManifest()
        {
            PrecompiledFallbackEvent? captured = null;
            PrecompiledTemplates.OnFallback = e => captured = e;
            var newer = $"{RuntimeVersion.Major + 1}.0.0";
            Register(OneRow("reg/one.heddle"), SupportedSchema, newer, "HeddleTestAsm_Engine");
            Assert.False(PrecompiledTemplates.TryGet("reg/one.heddle", out _));
            Assert.NotNull(captured);
            Assert.Equal(PrecompiledFallbackReason.EngineVersionIncompatible, captured.Value.Reason);
        }

        [Fact]
        public void CaseMismatchFiresShadowCallback()
        {
            PrecompiledFallbackEvent? captured = null;
            PrecompiledTemplates.OnFallback = e => captured = e;
            var artifact = CompiledFormHarness.MinimalArtifact();
            artifact.Templates.Add(CompiledFormHarness.TemplateRow("Reg/Case.heddle"));
            Register(artifact, SupportedSchema, CompatibleVersion, "HeddleTestAsm_Case");

            // Exact case hits.
            Assert.True(PrecompiledTemplates.TryGet("Reg/Case.heddle", out _));
            // Wrong case misses but fires the informational shadow callback.
            Assert.False(PrecompiledTemplates.TryGet("reg/case.heddle", out _));
            Assert.NotNull(captured);
            Assert.Equal(PrecompiledFallbackReason.CaseMismatch, captured.Value.Reason);
            Assert.Equal("Key: requested 'reg/case.heddle' registered 'Reg/Case.heddle'", captured.Value.Detail);
            Assert.Equal("HED7103", captured.Value.DiagnosticId);
        }
    }
}
