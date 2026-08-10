using System.Linq;
using Heddle.Precompiled;
using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// The twin manifest-entry builders merged into one. Since <c>PrecompiledTemplateInfo</c> parameter
    /// <i>names</i> cannot come from <c>nameof</c>, the compile-and-register gate verifies that renamed or
    /// reordered constructor parameters are caught. Scenario: a manifest with both entry kinds at once.
    /// </summary>
    [Collection("PrecompiledRegistry")]
    public class MixedManifestCompileGateTests : PrecompiledRegistryTestBase
    {
        private const string ProductType = "Heddle.Generator.IntegrationTests.Fixtures.Product";

        [Fact]
        public void ManifestWithBothANormalAndAMarkerEntryCompilesAndRegisters()
        {
            const string normalKey = "views/wi6-normal.heddle";
            const string markerKey = "views/wi6-marker.heddle";

            var gen = DifferentialHarness.Generate(new[]
            {
                (normalKey, "@model(){{" + ProductType + "}}@\\\n<span>@(upper(Name))</span>\n"),
                // The marker half needs a call late binding genuinely cannot serve: the argument is an
                // expression over a second un-bindable name, so it has no static type to rank the outer call
                // against. A call over a typeable argument precompiles now and would not exercise this gate.
                (markerKey,
                    "@model(){{" + ProductType + "}}@\\\n<span>@(mystery(other(Name) + \"!\"))</span>\n"),
            });

            DifferentialHarness.ExpectPrecompiled(gen, normalKey);
            DifferentialHarness.ExpectDegrade(gen, markerKey);

            Assert.NotNull(gen.Assembly);

            PrecompiledTemplates.Register(gen.Assembly);
            var entries = PrecompiledTemplates.Entries.ToDictionary(e => e.Key);

            Assert.True(entries[normalKey].IsPrecompiled);
            Assert.NotNull(entries[normalKey].Strategy);
            Assert.NotNull(entries[normalKey].EntryPointType);
            Assert.Equal(PrecompiledCapabilities.StringOutput, entries[normalKey].Capabilities);

            Assert.False(entries[markerKey].IsPrecompiled);
            Assert.Null(entries[markerKey].Strategy);
            Assert.Null(entries[markerKey].EntryPointType);
            Assert.Equal(PrecompiledCapabilities.None, entries[markerKey].Capabilities);
            Assert.Contains(entries[markerKey].FunctionBindings, r => r.Name == "mystery" && r.TargetTypeName == null);

            Assert.Equal(entries[normalKey].OptionsFingerprint.Profile, entries[markerKey].OptionsFingerprint.Profile);
            Assert.Equal(entries[normalKey].OptionsFingerprint.ExpressionMode,
                entries[markerKey].OptionsFingerprint.ExpressionMode);
            Assert.Equal(entries[normalKey].OptionsFingerprint.TrimDirectiveLines,
                entries[markerKey].OptionsFingerprint.TrimDirectiveLines);
        }
    }
}
