using System.Linq;
using Heddle.Precompiled;
using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// The twin manifest-entry builders — thirteen named constructor arguments duplicated verbatim, differing in five
    /// lines — merged into one builder taking a marker flag. Since the <c>PrecompiledTemplateInfo</c> argument <i>names</i>
    /// cannot come from <c>nameof</c> (the type pulls <c>IProcessStrategy</c> and is not linkable into the analyzer), this
    /// suite's compile-and-register gate is the guard: a renamed or reordered constructor parameter is a red build here.
    /// <para>The suite covers a manifest containing <b>both</b> kinds of entry at once — the shape the merged builder has to
    /// keep producing.</para>
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
                (markerKey, "@model(){{" + ProductType + "}}@\\\n<span>@(mystery(Name))</span>\n"),
            });

            DifferentialHarness.ExpectPrecompiled(gen, normalKey);
            DifferentialHarness.ExpectDegrade(gen, markerKey);

            // The compile gate: the emitted manifest was compiled against the real Heddle reference to produce this
            // assembly, so every constructor argument name and order in both entry shapes is already proven.
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

            // Both entries carry the same options fingerprint — the merged builder formats one struct for both.
            Assert.Equal(entries[normalKey].OptionsFingerprint.Profile, entries[markerKey].OptionsFingerprint.Profile);
            Assert.Equal(entries[normalKey].OptionsFingerprint.ExpressionMode,
                entries[markerKey].OptionsFingerprint.ExpressionMode);
            Assert.Equal(entries[normalKey].OptionsFingerprint.TrimDirectiveLines,
                entries[markerKey].OptionsFingerprint.TrimDirectiveLines);
        }
    }
}
