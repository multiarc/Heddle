using Heddle.Data;
using Heddle.Exceptions;
using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// C1 (G-R3) — the render-budget differential fixture. Budgets are enforced entirely at the renderer seam, which
    /// both backends write through, so the guarantee is: a budget-completing render is byte-identical on the
    /// precompiled and dynamic backends, and a budget-breaching render throws the same
    /// <see cref="TemplateRenderBudgetException.Kind"/> on both. The budget flows to the precompiled side via the
    /// options-carrying <c>GenerateString</c> overload exactly as it reaches the dynamic engine's
    /// <c>HeddleTemplate.Generate</c>.
    /// </summary>
    public class RenderBudgetDifferentialTests
    {
        [Fact]
        public void BudgetCompleting_IsByteIdenticalAcrossBackends()
        {
            const string content = "<p>@(V)</p>";
            var model = new EncoderDifferentialTests.HostileModel { V = "hello" };
            // Generous limits: the render completes well under every cap, so both backends produce identical bytes.
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
            // Multiple write ops (static + value + static): MaxRenderOps=1 trips on the second op on both backends.
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
