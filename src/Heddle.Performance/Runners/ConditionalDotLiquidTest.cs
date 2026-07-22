using System.Threading.Tasks;
using DotLiquid;

namespace Heddle.Performance.Runners
{
    /// <summary>
    /// DotLiquid (Liquid) twin of the conditional-heavy workload. One <c>{% for %}</c> over the
    /// 200 shared <see cref="Hash"/> rows, each row running the four-way
    /// <c>{% if %}/{% elsif %}</c> tier chain plus two toggles on the same precomputed booleans
    /// as every other engine. DotLiquid does not HTML-encode output, matching the Heddle oracle's
    /// <c>OutputProfile.Text</c>. Output is asserted equal to Heddle under the parity normalizer.
    /// Shares its Liquid source with <see cref="ConditionalFluidTest"/>.
    /// </summary>
    public sealed class ConditionalDotLiquidTest
    {
        private readonly Template _page;
        private long _length;

        public ConditionalDotLiquidTest()
        {
            _page = Template.Parse(ConditionalLiquidTemplates.PageTemplate);
        }

        public string Render() => _page.Render(ConditionalContent.DotLiquidModel());

        public Task Run()
        {
            _length += Render().Length;
            return Task.CompletedTask;
        }
    }
}
