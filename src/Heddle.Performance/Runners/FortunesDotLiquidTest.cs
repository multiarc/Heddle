using System.Threading.Tasks;
using DotLiquid;

namespace Heddle.Performance.Runners
{
    /// <summary>
    /// DotLiquid (Liquid) twin of the fortunes-encoded workload. One <c>{% for %}</c> over the
    /// shared 12 <see cref="Hash"/> rows with every message through <c>| escape</c> — DotLiquid's
    /// HTML escaper, which on the pinned untrusted-data alphabet matches Heddle's
    /// <c>OutputProfile.Html</c> encoder byte-for-byte. Output is asserted against the Heddle
    /// oracle. Shares its Liquid source with <see cref="FortunesFluidTest"/>.
    /// </summary>
    public sealed class FortunesDotLiquidTest
    {
        private readonly Template _template;
        private long _length;

        public FortunesDotLiquidTest()
        {
            _template = Template.Parse(FortunesLiquidTemplates.FortunesTemplate);
        }

        public string Render() => _template.Render(FortunesContent.DotLiquidModel());

        public Task Run()
        {
            _length += Render().Length;
            return Task.CompletedTask;
        }
    }
}
