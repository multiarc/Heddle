using System.Threading.Tasks;
using Fluid;

namespace Heddle.Performance.Runners
{
    /// <summary>
    /// Fluid (Liquid) twin of the fortunes-encoded workload. One <c>{% for %}</c> over the shared
    /// 12 dictionary rows with every message through <c>| escape</c> — Fluid's HTML escaper, which
    /// on the pinned untrusted-data alphabet matches Heddle's <c>OutputProfile.Html</c> encoder
    /// byte-for-byte. Output is asserted against the Heddle oracle. Shares its Liquid source with
    /// <see cref="FortunesDotLiquidTest"/>.
    /// </summary>
    public sealed class FortunesFluidTest
    {
        private static readonly FluidParser Parser = new FluidParser();

        private readonly IFluidTemplate _template;
        private long _length;

        public FortunesFluidTest()
        {
            _template = Parser.Parse(FortunesLiquidTemplates.FortunesTemplate);
        }

        private static TemplateContext CreateContext()
        {
            var context = new TemplateContext();
            foreach (var kv in FortunesContent.LiquidModel())
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
