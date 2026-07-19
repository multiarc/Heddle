using System.Threading.Tasks;
using HandlebarsDotNet;

namespace Heddle.Performance.Runners
{
    /// <summary>
    /// Handlebars.Net twin of the large-loop workload. One <c>{{#each}}</c> over the shared 5,000
    /// dictionary rows (Handlebars resolves <c>{{name}}</c> off dictionary rows). The string member
    /// uses a triple mustache to stay raw, matching the Heddle oracle's <c>OutputProfile.Text</c>.
    /// Output is asserted byte-identical to Heddle.
    /// </summary>
    public sealed class LoopHandlebarsTest
    {
        internal const string LoopTemplate =
            "{{#each items}}<tr><td>{{{name}}}</td><td>{{value}}</td></tr>{{/each}}";

        private readonly HandlebarsTemplate<object, object> _loop;
        private long _length;

        public LoopHandlebarsTest()
        {
            _loop = Handlebars.Create().Compile(LoopTemplate);
        }

        public string Render() => _loop(LoopContent.HandlebarsModel());

        public Task Run()
        {
            _length += Render().Length;
            return Task.CompletedTask;
        }
    }
}
