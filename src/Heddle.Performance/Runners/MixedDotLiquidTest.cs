using System.Threading.Tasks;
using DotLiquid;

namespace Heddle.Performance.Runners
{
    /// <summary>
    /// DotLiquid (Liquid) twin of the mixed-page workload. One page template over the shared
    /// <see cref="Hash"/> model (DotLiquid resolves <c>p.name</c> only on its native
    /// <c>Hash</c>/<c>Drop</c> containers). DotLiquid does not HTML-encode output, matching the
    /// Heddle oracle's <c>OutputProfile.Text</c>. Output is asserted equal to Heddle under the
    /// parity normalizer. Shares its Liquid source with <see cref="MixedFluidTest"/>.
    /// </summary>
    public sealed class MixedDotLiquidTest
    {
        private readonly Template _page;
        private long _length;

        public MixedDotLiquidTest()
        {
            _page = Template.Parse(MixedLiquidTemplates.PageTemplate);
        }

        public string Render() => _page.Render(MixedContent.DotLiquidModel());

        public Task Run()
        {
            _length += Render().Length;
            return Task.CompletedTask;
        }
    }
}
