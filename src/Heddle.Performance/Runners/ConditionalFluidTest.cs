using System.Threading.Tasks;
using Fluid;

namespace Heddle.Performance.Runners
{
    /// <summary>
    /// Fluid (Liquid) twin of the conditional-heavy workload. One <c>{% for %}</c> over the 200
    /// shared dictionary rows, each row running the four-way <c>{% if %}/{% elsif %}</c> tier
    /// chain plus two toggles on the same precomputed booleans as every other engine. Fluid's
    /// default convenience encoder is raw, matching the Heddle oracle's
    /// <c>OutputProfile.Text</c>. Output is asserted equal to Heddle under the parity normalizer.
    /// Shares its Liquid source with <see cref="ConditionalDotLiquidTest"/>.
    /// </summary>
    public sealed class ConditionalFluidTest
    {
        private static readonly FluidParser Parser = new FluidParser();

        private readonly IFluidTemplate _page;
        private long _length;

        public ConditionalFluidTest()
        {
            _page = Parser.Parse(ConditionalLiquidTemplates.PageTemplate);
        }

        private static TemplateContext CreateContext()
        {
            var context = new TemplateContext();
            foreach (var kv in ConditionalContent.LiquidModel())
                context.SetValue(kv.Key, kv.Value);
            return context;
        }

        public string Render() => _page.Render(CreateContext());

        public Task Run()
        {
            _length += Render().Length;
            return Task.CompletedTask;
        }
    }
}
