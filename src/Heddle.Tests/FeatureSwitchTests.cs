using System;
using System.Linq;
using System.Reflection;
using Heddle.Data;
using Heddle.Runtime;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// Phase 9 D4 — the <c>Heddle.CSharpTierEnabled</c> trim-time feature switch and its HED9001 guard. The switch
    /// is an <see cref="AppContext"/> boolean; assembly test parallelization is disabled
    /// (<c>CollectionBehavior(DisableTestParallelization = true)</c>), so flipping it here cannot race other tests.
    /// Every test restores the switch to the enabled state in a <c>finally</c> so the default-behavior rows and the
    /// rest of the suite see the unchanged engine.
    /// </summary>
    public class FeatureSwitchTests
    {
        private const string SwitchName = "Heddle.CSharpTierEnabled";

        // A minimal C#-tier template: the inner '@' selects the Roslyn tier, which registers one method.
        private const string CSharpTierTemplate = "@(@ 1 + 2 )";

        private static void WithSwitch(bool? value, Action body)
        {
            // AppContext switches cannot be truly unset; enabling it is behaviorally identical to unset for
            // HeddleFeatures.CSharpTierEnabled (unset || enabled == true). null models the unset/default host.
            try
            {
                if (value.HasValue)
                    AppContext.SetSwitch(SwitchName, value.Value);
                body();
            }
            finally
            {
                AppContext.SetSwitch(SwitchName, true);
            }
        }

        [Fact]
        public void SwitchUnsetDefault_CompilesAndRendersCSharpTier()
        {
            HeddleTemplate.Configure(typeof(FeatureSwitchTests).GetTypeInfo().Assembly);
            // Default host: switch left as the suite baseline (enabled).
            using var template = new HeddleTemplate(CSharpTierTemplate,
                new CompileContext(new TemplateOptions { AllowCSharp = true }));
            Assert.True(template.CompileResult.Success, template.CompileResult.ToString());
            Assert.Equal("3", template.Generate(null));
        }

        [Fact]
        public void SwitchOnExplicit_BehavesIdentically()
        {
            HeddleTemplate.Configure(typeof(FeatureSwitchTests).GetTypeInfo().Assembly);
            WithSwitch(true, () =>
            {
                using var template = new HeddleTemplate(CSharpTierTemplate,
                    new CompileContext(new TemplateOptions { AllowCSharp = true }));
                Assert.True(template.CompileResult.Success, template.CompileResult.ToString());
                Assert.Equal("3", template.Generate(null));
            });
        }

        [Fact]
        public void SwitchOff_WithRegisteredCSharpMethod_CollectsHed9001()
        {
            HeddleTemplate.Configure(typeof(FeatureSwitchTests).GetTypeInfo().Assembly);
            WithSwitch(false, () =>
            {
                HeddleCompileResult result = null;
                var ex = Record.Exception(() =>
                {
                    using var template = new HeddleTemplate(CSharpTierTemplate,
                        new CompileContext(new TemplateOptions { AllowCSharp = true }));
                    result = template.CompileResult;
                });

                // Nothing is thrown — the compile-path condition is collected, not raised (coding standards).
                Assert.Null(ex);
                Assert.NotNull(result);
                Assert.False(result.Success);

                var diag = Assert.Single(result.Errors, e => e.DiagnosticId == HeddleFeatures.CSharpTierDisabledDiagnosticId);
                Assert.Equal("HED9001", diag.DiagnosticId);
                // The guard is a host-capability condition on the whole compile: document-start position.
                Assert.Equal(0, diag.Position.StartIndex);
                Assert.Equal(0, diag.Position.Length);
                Assert.Contains("Heddle.CSharpTierEnabled", diag.Error);
            });
        }

        [Fact]
        public void SwitchOff_NativeTemplate_Unaffected()
        {
            // The switch only gates the C# tier: a native-expression template compiles and renders normally with
            // the switch off — the browser demo's whole surface.
            HeddleTemplate.Configure(typeof(FeatureSwitchTests).GetTypeInfo().Assembly);
            WithSwitch(false, () =>
            {
                using var template = new HeddleTemplate("@(1 + 2)",
                    new CompileContext(new TemplateOptions { ExpressionMode = ExpressionMode.Native }));
                Assert.True(template.CompileResult.Success, template.CompileResult.ToString());
                Assert.Equal("3", template.Generate(null));
            });
        }

        [Fact]
        public void SwitchOff_ThenBackOn_RestoresCSharpTier()
        {
            HeddleTemplate.Configure(typeof(FeatureSwitchTests).GetTypeInfo().Assembly);
            WithSwitch(false, () =>
            {
                using var off = new HeddleTemplate(CSharpTierTemplate,
                    new CompileContext(new TemplateOptions { AllowCSharp = true }));
                Assert.False(off.CompileResult.Success);
            });

            // After restore, the tier is available again with byte-identical output.
            using var on = new HeddleTemplate(CSharpTierTemplate,
                new CompileContext(new TemplateOptions { AllowCSharp = true }));
            Assert.True(on.CompileResult.Success, on.CompileResult.ToString());
            Assert.Equal("3", on.Generate(null));
        }
    }
}
