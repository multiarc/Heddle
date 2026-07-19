using System.Threading.Tasks;
using DotLiquid;

namespace Heddle.Performance.Runners
{
    /// <summary>
    /// DotLiquid (Liquid) twin of the large-loop workload. One <c>{% for %}</c> over the shared
    /// 5,000 <see cref="Hash"/> rows (DotLiquid resolves <c>item.name</c> only on its native
    /// <c>Hash</c>/<c>Drop</c> containers, not on a plain CLR POCO). DotLiquid does not
    /// HTML-encode output, matching the Heddle oracle's <c>OutputProfile.Text</c>. Output is
    /// asserted byte-identical to Heddle. Shares its Liquid source with <see cref="LoopFluidTest"/>.
    /// </summary>
    public sealed class LoopDotLiquidTest
    {
        private readonly Template _loop;
        private long _length;

        public LoopDotLiquidTest()
        {
            _loop = Template.Parse(LoopLiquidTemplates.LoopTemplate);
        }

        public string Render() => _loop.Render(LoopContent.DotLiquidModel());

        public Task Run()
        {
            _length += Render().Length;
            return Task.CompletedTask;
        }
    }
}
