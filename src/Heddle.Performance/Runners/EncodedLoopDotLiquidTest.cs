using System.Threading.Tasks;
using DotLiquid;

namespace Heddle.Performance.Runners
{
    /// <summary>
    /// DotLiquid (Liquid) twin of the encoded-loop workload. One <c>{% for %}</c> over the shared
    /// 5,000 <see cref="Hash"/> rows with every untrusted member through <c>| escape</c> —
    /// DotLiquid's HTML escaper, which on the pinned untrusted-data alphabet matches Heddle's
    /// <c>@attr</c> and <c>@(…)</c> encoders byte-for-byte. Output is asserted against the Heddle
    /// oracle. Shares its Liquid source with <see cref="EncodedLoopFluidTest"/>.
    /// </summary>
    public sealed class EncodedLoopDotLiquidTest
    {
        private readonly Template _template;
        private long _length;

        public EncodedLoopDotLiquidTest()
        {
            _template = Template.Parse(EncodedLoopLiquidTemplates.LoopTemplate);
        }

        public string Render() => _template.Render(EncodedLoopContent.DotLiquidModel());

        public Task Run()
        {
            _length += Render().Length;
            return Task.CompletedTask;
        }
    }
}
