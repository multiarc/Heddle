using System.Threading.Tasks;
using DotLiquid;

namespace Heddle.Performance.Runners
{
    /// <summary>
    /// DotLiquid (Liquid) twin of the trivial-substitution card. Ten scalar <c>{{ member }}</c>
    /// substitutions over a <see cref="Hash"/> built once from the shared model — no layout,
    /// include, or loop. DotLiquid does not HTML-encode output, matching the Heddle oracle's
    /// <c>OutputProfile.Text</c>. Output is asserted byte-identical to Heddle. Shares its Liquid
    /// source with <see cref="SubstitutionFluidTest"/>.
    /// </summary>
    public sealed class SubstitutionDotLiquidTest
    {
        private static readonly Hash Model = BuildModel();

        private readonly Template _card;
        private long _length;

        public SubstitutionDotLiquidTest()
        {
            _card = Template.Parse(SubstitutionLiquidTemplates.CardTemplate);
        }

        private static Hash BuildModel()
        {
            var root = new Hash();
            foreach (var kv in SubstitutionContent.LiquidModel())
                root[kv.Key] = kv.Value;
            return root;
        }

        public string Render() => _card.Render(Model);

        public Task Run()
        {
            _length += Render().Length;
            return Task.CompletedTask;
        }
    }
}
