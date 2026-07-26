using Heddle.Data;
using Heddle.Exceptions;
using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>Render-budget differential fixture: both backends enforce budgets at the renderer seam
    /// identically, byte-for-byte in success cases and same exception kind in failures.</summary>
    public class RenderBudgetDifferentialTests
    {
        [Fact]
        public void BudgetCompleting_IsByteIdenticalAcrossBackends()
        {
            const string content = "<p>@(V)</p>";
            var model = new EncoderDifferentialTests.HostileModel { V = "hello" };
            // Generous limits ensure both backends produce identical bytes.
            var options = new TemplateOptions
            {
                OutputProfile = OutputProfile.Html,
                RenderBudget = new RenderBudget { MaxOutputChars = 10_000, MaxRenderOps = 10_000 },
            };

            var (precompiled, dyn) = DifferentialHarness.RenderWithOptions(
                "views/budget-ok.heddle", content, typeof(EncoderDifferentialTests.HostileModel), model, options);

            Assert.Equal(dyn, precompiled);
            Assert.Equal("<p>hello</p>", precompiled);
        }

        [Fact]
        public void BudgetBreach_OutputChars_ThrowsSameKindOnBothBackends()
        {
            const string content = "<p>@(V)</p>";
            var model = new EncoderDifferentialTests.HostileModel { V = "a very long value that blows the char budget" };
            var options = new TemplateOptions
            {
                OutputProfile = OutputProfile.Html,
                RenderBudget = new RenderBudget { MaxOutputChars = 3 },
            };

            var (precompiled, dynamic) = DifferentialHarness.DeferredWithOptions(
                "views/budget-chars.heddle", content, typeof(EncoderDifferentialTests.HostileModel), model, options);

            var pre = Assert.Throws<TemplateRenderBudgetException>(() => precompiled());
            var dyn = Assert.Throws<TemplateRenderBudgetException>(() => dynamic());
            Assert.Equal(RenderBudgetKind.OutputChars, pre.Kind);
            Assert.Equal(dyn.Kind, pre.Kind);
        }

        [Fact]
        public void BudgetBreach_RenderOps_ThrowsSameKindOnBothBackends()
        {
            // MaxRenderOps=1 trips on the second write op (static, value, static).
            const string content = "<p>@(V)</p>";
            var model = new EncoderDifferentialTests.HostileModel { V = "value" };
            var options = new TemplateOptions
            {
                OutputProfile = OutputProfile.Html,
                RenderBudget = new RenderBudget { MaxRenderOps = 1 },
            };

            var (precompiled, dynamic) = DifferentialHarness.DeferredWithOptions(
                "views/budget-ops.heddle", content, typeof(EncoderDifferentialTests.HostileModel), model, options);

            var pre = Assert.Throws<TemplateRenderBudgetException>(() => precompiled());
            var dyn = Assert.Throws<TemplateRenderBudgetException>(() => dynamic());
            Assert.Equal(RenderBudgetKind.RenderOps, pre.Kind);
            Assert.Equal(dyn.Kind, pre.Kind);
        }
    }
}
