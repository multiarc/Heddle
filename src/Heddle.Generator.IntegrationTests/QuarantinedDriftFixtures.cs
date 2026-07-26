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

        [Fact]
        public void NestedExtensionType_BindsAndCrossesTheGauntlet()
        {
            var (precompiled, dyn) = DifferentialHarness.RenderViaResolver(
                "drift-nested.heddle", "@model(){{System.String}}@\\\n@driftnested(this)\n", typeof(string), "hi");
            Assert.Equal(dyn, precompiled);
        }

        [Fact]
        public void InheritedExtensionNameSubclass_CrossesTheGauntlet()
        {
            var (precompiled, dyn) = DifferentialHarness.RenderViaResolver(
                "drift-inherited.heddle", "@model(){{System.String}}@\\\n@driftbase(this)\n", typeof(string), "hi");
            Assert.Equal(dyn, precompiled);
        }

        [Fact]
        public void NonLeftmostScopeChannelParticipant_ProvisionsLocalsOnBothTiers()
        {
            const string nested = "@model(){{System.String}}@\\\n@yell(@row())\n";
            var nestedGen = DifferentialHarness.Generate(new[] { ("drift-locals.heddle", nested) });
            Assert.DoesNotContain(nestedGen.Diagnostics,
                d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
            DifferentialHarness.ExpectDegrade(nestedGen, "drift-locals.heddle");

            const string control = "@model(){{System.String}}@\\\n@yell(@yell(this))\n";
            var controlGen = DifferentialHarness.Generate(new[] { ("drift-locals-control.heddle", control) });
            DifferentialHarness.ExpectDegrade(controlGen, "drift-locals-control.heddle");

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

        [Fact]
        public void OverloadTie_ResolvesIdenticallyOnBothTiers()
        {
            const string key = "drift-overload.heddle";
            const string content = "@model(){{System.String}}@\\\n@(min(1, 2u))\n";

            var gen = DifferentialHarness.Generate(new[] { (key, content) });
            var reported = Assert.Single(gen.Diagnostics,
                d => d.Id == HeddleDiagnosticIds.BuildFunctionCallNotBindable);
            Assert.Equal(Microsoft.CodeAnalysis.DiagnosticSeverity.Error, reported.Severity);
            Assert.Contains("min(long, long)", reported.GetMessage());
            DifferentialHarness.ExpectDegrade(gen, key);

            var ambiguous = new HeddleTemplate(content, new CompileContext(new TemplateOptions(), typeof(string)));
            Assert.False(ambiguous.CompileResult.Success);
            Assert.Contains(ambiguous.CompileResult.ErrorList,
                e => e.DiagnosticId == HeddleDiagnosticIds.AmbiguousFunctionCall);

            var (precompiled, dyn) = DifferentialHarness.Render(
                "drift-overload-ok.heddle", "@model(){{System.String}}@\\\n@(min(1, 2))\n", typeof(string), "hi");
            Assert.Equal(dyn, precompiled);
            Assert.Contains("1", precompiled);
        }
    }
}
