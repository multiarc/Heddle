using System.Threading.Tasks;
using HandlebarsDotNet;

namespace Heddle.Performance.Runners
{
    /// <summary>
    /// Handlebars.Net twin of the trivial-substitution card. Ten scalar triple-mustache
    /// (<c>{{{member}}}</c>) substitutions over the shared lowercase-keyed dictionary — no
    /// partial, helper, or loop. Triple mustaches keep the output raw (double mustaches would
    /// entity-encode), matching the Heddle oracle's <c>OutputProfile.Text</c>. Output is asserted
    /// byte-identical to Heddle.
    /// </summary>
    public sealed class SubstitutionHandlebarsTest
    {
        internal const string CardTemplate =
            "<article><h1>{{{title}}}</h1><p class=\"sku\">{{{sku}}}</p><p class=\"price\">{{{price}}}</p>" +
            "<p class=\"brand\">{{{brand}}}</p><p class=\"cat\">{{{category}}}</p><p class=\"avail\">{{{availability}}}</p>" +
            "<a class=\"link\" href=\"{{{url}}}\"><img src=\"{{{image_url}}}\"></a>" +
            "<p class=\"sum\">{{{summary}}}</p><p class=\"rating\">{{{rating}}}</p></article>";

        private readonly HandlebarsTemplate<object, object> _card;
        private long _length;

        public SubstitutionHandlebarsTest()
        {
            _card = Handlebars.Create().Compile(CardTemplate);
        }

        public string Render() => _card(SubstitutionContent.HandlebarsModel());

        public Task Run()
        {
            _length += Render().Length;
            return Task.CompletedTask;
        }
    }
}
