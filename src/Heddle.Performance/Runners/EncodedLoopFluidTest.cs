using System.Threading.Tasks;
using Fluid;

namespace Heddle.Performance.Runners
{
    /// <summary>
    /// Fluid (Liquid) twin of the encoded-loop workload. One <c>{% for %}</c> over the shared
    /// 5,000 dictionary rows with every untrusted member through <c>| escape</c> — Fluid's HTML
    /// escaper, which on the pinned untrusted-data alphabet matches Heddle's <c>@attr</c> and
    /// <c>@(…)</c> encoders byte-for-byte. Output is asserted against the Heddle oracle. Shares
    /// its Liquid source with <see cref="EncodedLoopDotLiquidTest"/>.
    /// </summary>
    public sealed class EncodedLoopFluidTest
    {
        private static readonly FluidParser Parser = new FluidParser();

        private readonly IFluidTemplate _template;
        private long _length;

        public EncodedLoopFluidTest()
        {
            _template = Parser.Parse(EncodedLoopLiquidTemplates.LoopTemplate);
        }

        private static TemplateContext CreateContext()
        {
            var context = new TemplateContext();
            foreach (var kv in EncodedLoopContent.LiquidModel())
                context.SetValue(kv.Key, kv.Value);
            return context;
        }

        public string Render() => _template.Render(CreateContext());

        public Task Run()
        {
            _length += Render().Length;
            return Task.CompletedTask;
        }
    }
}
