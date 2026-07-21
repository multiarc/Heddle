using System.Threading.Tasks;
using HandlebarsDotNet;

namespace Heddle.Performance.Runners
{
    /// <summary>
    /// Handlebars.Net twin of the fragment-heavy workload. The <c>tile</c> partial is registered
    /// once (<c>RegisterTemplate</c>, the <see cref="HandlebarsTest"/> mechanism) and invoked 48
    /// times with <c>{{&gt; tile this}}</c>, passing the current row as the partial's context so
    /// the partial reads <c>{{{name}}}</c>-form members (probe-E-verified construct). Triple
    /// mustaches keep the substitutions raw, matching the Heddle oracle's
    /// <c>OutputProfile.Text</c>. Output is asserted equal to Heddle under the parity normalizer.
    /// </summary>
    public sealed class FragmentHandlebarsTest
    {
        internal const string PageTemplate =
            "<div class=\"panel\">{{#each items}}{{> tile this}}{{/each}}</div>";

        internal const string TileTemplate =
            "<section class=\"tile\"><h3>{{{name}}}</h3><p class=\"v\">{{{value}}}</p><span class=\"badge\">{{{badge}}}</span></section>";

        private readonly HandlebarsTemplate<object, object> _page;
        private long _length;

        public FragmentHandlebarsTest()
        {
            var hb = Handlebars.Create();
            hb.RegisterTemplate("tile", TileTemplate);
            _page = hb.Compile(PageTemplate);
        }

        public string Render() => _page(FragmentContent.HandlebarsModel());

        public Task Run()
        {
            _length += Render().Length;
            return Task.CompletedTask;
        }
    }
}
