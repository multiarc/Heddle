using System.Threading.Tasks;
using Fluid;

namespace Heddle.Performance.Runners
{
    /// <summary>
    /// Fluid (Liquid) twin of the large-loop workload. One <c>{% for %}</c> over the shared
    /// 5,000 dictionary rows (Fluid resolves <c>item.name</c> on dictionary members; its default
    /// member-access strategy denies un-registered POCO members, so the rows are the engine-native
    /// container). Fluid's default convenience encoder is raw, matching the Heddle oracle's
    /// <c>OutputProfile.Text</c>. Output is asserted byte-identical to Heddle. Shares its Liquid
    /// source with <see cref="LoopDotLiquidTest"/>.
    /// </summary>
    public sealed class LoopFluidTest
    {
        private static readonly FluidParser Parser = new FluidParser();

        private readonly IFluidTemplate _loop;
        private long _length;

        public LoopFluidTest()
        {
            _loop = Parser.Parse(LoopLiquidTemplates.LoopTemplate);
        }

        private static TemplateContext CreateContext()
        {
            var context = new TemplateContext();
            foreach (var kv in LoopContent.LiquidModel())
                context.SetValue(kv.Key, kv.Value);
            return context;
        }

        public string Render() => _loop.Render(CreateContext());

        public Task Run()
        {
            _length += Render().Length;
            return Task.CompletedTask;
        }
    }
}
