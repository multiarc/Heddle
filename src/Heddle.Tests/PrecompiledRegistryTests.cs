using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Heddle.Data;
using Heddle.Precompiled;
using Heddle.Runtime;
using Xunit;

namespace Heddle.Tests
{
    internal sealed class RegFakeStrategy : IProcessStrategy
    {
        public string Execute(in Scope scope) => string.Empty;
        public void Render(in Scope scope) { }
    }

    internal static class RegEntries
    {
        private static readonly IProcessStrategy Strategy = new RegFakeStrategy();

        public static PrecompiledTemplateInfo One(string key) => new PrecompiledTemplateInfo(
            key, typeof(object), null, false, "0",
            Array.Empty<PrecompiledImport>(),
            new PrecompiledOptionsFingerprint(OutputProfile.Text, ExpressionMode.Native, false),
            Array.Empty<PrecompiledExtensionBinding>(),
            Array.Empty<PrecompiledFunctionBinding>(),
            PrecompiledCapabilities.StringOutput, Strategy);
    }

    internal sealed class RegManifestOne : IHeddleTemplateManifest
    {
        public IReadOnlyList<PrecompiledTemplateInfo> GetTemplates()
            => new[] { RegEntries.One("reg/one.heddle") };
    }

    internal sealed class RegManifestDup : IHeddleTemplateManifest
    {
        public IReadOnlyList<PrecompiledTemplateInfo> GetTemplates()
            => new[] { RegEntries.One("reg/dup.heddle") };
    }

    internal sealed class RegManifestCase : IHeddleTemplateManifest
    {
        public IReadOnlyList<PrecompiledTemplateInfo> GetTemplates()
            => new[] { RegEntries.One("Reg/Case.heddle") };
    }

    [CollectionDefinition("PrecompiledRegistrySerial", DisableParallelization = true)]
    public sealed class PrecompiledRegistrySerialCollection { }

    /// <summary>Validates registry registration, duplicate-key rejection, per-assembly idempotence, normalized <see cref="PrecompiledTemplates.TryGet"/>, and case-mismatch fallback. Serialized — the registry is process-global static state.</summary>
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

        /// <summary>Out-of-window edges derived from the window rather than written as literals, so they update automatically on schema version bumps.</summary>
        public static IEnumerable<object[]> OutOfWindowSchemas => new[]
        {
            new object[] { PrecompiledSchema.MinSupportedSchemaVersion - 1 },
            new object[] { PrecompiledSchema.MaxSupportedSchemaVersion + 1 }
        };

        private static Assembly BuildAssembly(Type manifestType, int schema, string engineVersion, string name)
        {
            var ab = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(name), AssemblyBuilderAccess.Run);
            var ctor = typeof(HeddleCompiledTemplatesAttribute)
                .GetConstructor(new[] { typeof(Type), typeof(int), typeof(string) });
            ab.SetCustomAttribute(new CustomAttributeBuilder(ctor,
                new object[] { manifestType, schema, engineVersion }));
            return ab;
        }

        [Fact]
        public void RegisterThenTryGet()
        {
            var asm = BuildAssembly(typeof(RegManifestOne), SupportedSchema, CompatibleVersion, "HeddleTestAsm_One_" + Guid.NewGuid().ToString("N"));
            PrecompiledTemplates.Register(asm);

            Assert.True(PrecompiledTemplates.TryGet("reg/one.heddle", out var entry));
            Assert.Equal("reg/one.heddle", entry.Key);
            // Normalization: backslashes and missing extension resolve to the same key.
            Assert.True(PrecompiledTemplates.TryGet("reg\\one", out _));
        }

        [Fact]
        public void RegisterIsIdempotentPerAssembly()
        {
            var asm = BuildAssembly(typeof(RegManifestOne), SupportedSchema, CompatibleVersion, "HeddleTestAsm_Idem_" + Guid.NewGuid().ToString("N"));
            PrecompiledTemplates.Register(asm);
            PrecompiledTemplates.Register(asm); // no throw, no duplicate
            Assert.Single(PrecompiledTemplates.Entries);
        }

        [Fact]
        public void DuplicateKeyAcrossAssembliesThrows()
        {
            var a = BuildAssembly(typeof(RegManifestDup), SupportedSchema, CompatibleVersion, "HeddleTestAsm_DupA_" + Guid.NewGuid().ToString("N"));
            var b = BuildAssembly(typeof(RegManifestDup), SupportedSchema, CompatibleVersion, "HeddleTestAsm_DupB_" + Guid.NewGuid().ToString("N"));
            PrecompiledTemplates.Register(a);
            var ex = Assert.Throws<PrecompiledRegistrationException>(() => PrecompiledTemplates.Register(b));
            Assert.Equal("reg/dup.heddle", ex.Key);
            Assert.Equal(a.GetName().Name, ex.ExistingAssemblyName);
            Assert.Equal(b.GetName().Name, ex.NewAssemblyName);
            Assert.Contains("already registered", ex.Message);
        }

        /// <summary>Both edges of the supported schema window; derived to avoid staleness on version bumps.</summary>
        [Theory]
        [MemberData(nameof(OutOfWindowSchemas))]
        public void UnsupportedSchemaIgnoresManifest(int schema)
        {
            PrecompiledFallbackEvent? captured = null;
            PrecompiledTemplates.OnFallback = e => captured = e;
            var asm = BuildAssembly(typeof(RegManifestOne), schema, CompatibleVersion, "HeddleTestAsm_Schema_" + Guid.NewGuid().ToString("N"));
            PrecompiledTemplates.Register(asm);
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
            var asm = BuildAssembly(typeof(RegManifestOne), SupportedSchema, newer, "HeddleTestAsm_Engine_" + Guid.NewGuid().ToString("N"));
            PrecompiledTemplates.Register(asm);
            Assert.False(PrecompiledTemplates.TryGet("reg/one.heddle", out _));
            Assert.NotNull(captured);
            Assert.Equal(PrecompiledFallbackReason.EngineVersionIncompatible, captured.Value.Reason);
        }

        [Fact]
        public void CaseMismatchFiresShadowCallback()
        {
            PrecompiledFallbackEvent? captured = null;
            PrecompiledTemplates.OnFallback = e => captured = e;
            var asm = BuildAssembly(typeof(RegManifestCase), SupportedSchema, CompatibleVersion, "HeddleTestAsm_Case_" + Guid.NewGuid().ToString("N"));
            PrecompiledTemplates.Register(asm);

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
