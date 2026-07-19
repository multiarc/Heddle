using System.Threading.Tasks;
using Fluid;

namespace Heddle.Performance.Runners
{
    /// <summary>
    /// Fluid (Liquid) twin of the trivial-substitution card. Ten scalar <c>{{ member }}</c>
    /// substitutions over the shared lowercase-keyed model — no layout, include, or loop. Fluid's
    /// default convenience encoder is the raw (non-encoding) encoder, matching the Heddle oracle's
    /// <c>OutputProfile.Text</c>. Output is asserted byte-identical to Heddle. Shares its Liquid
    /// source with <see cref="SubstitutionDotLiquidTest"/>.
    /// </summary>
    public sealed class SubstitutionFluidTest
    {
        private static readonly FluidParser Parser = new FluidParser();

        private readonly IFluidTemplate _card;
        private long _length;

        public SubstitutionFluidTest()
        {
            _card = Parser.Parse(SubstitutionLiquidTemplates.CardTemplate);
        }

        private static TemplateContext CreateContext()
        {
            var context = new TemplateContext();
            foreach (var kv in SubstitutionContent.LiquidModel())
                context.SetValue(kv.Key, kv.Value);
            return context;
        }

        public string Render() => _card.Render(CreateContext());

        public Task Run()
        {
            _length += Render().Length;
            return Task.CompletedTask;
        }
    }
}
