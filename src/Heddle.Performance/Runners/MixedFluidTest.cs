using System.Threading.Tasks;
using Fluid;

namespace Heddle.Performance.Runners
{
    /// <summary>
    /// Fluid (Liquid) twin of the mixed-page workload. One page template over the shared
    /// snake_case dictionary model (Fluid resolves <c>p.name</c> on dictionary members). Fluid's
    /// default convenience encoder is raw, matching the Heddle oracle's
    /// <c>OutputProfile.Text</c>. Output is asserted equal to Heddle under the parity normalizer.
    /// Shares its Liquid source with <see cref="MixedDotLiquidTest"/>.
    /// </summary>
    public sealed class MixedFluidTest
    {
        private static readonly FluidParser Parser = new FluidParser();

        private readonly IFluidTemplate _page;
        private long _length;

        public MixedFluidTest()
        {
            _page = Parser.Parse(MixedLiquidTemplates.PageTemplate);
        }

        private static TemplateContext CreateContext()
        {
            var context = new TemplateContext();
            foreach (var kv in MixedContent.LiquidModel())
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
