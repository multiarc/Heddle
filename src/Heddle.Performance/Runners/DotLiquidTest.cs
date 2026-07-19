using System.Collections.Generic;
using System.Threading.Tasks;
using DotLiquid;
using DotLiquid.FileSystems;

namespace Heddle.Performance.Runners
{
    /// <summary>
    /// DotLiquid (Liquid) twin of the composed home page. The layout is pulled in with
    /// <c>{% include 'layout' %}</c> via a file system, reusable sections are <c>section.*</c>
    /// members, component calls are <c>areas[...]</c> / <c>comp.*</c> lookups, and the ordered area
    /// menus are rendered with a real <c>{% for %}</c> loop. Shares its Liquid source with
    /// <see cref="FluidTest"/>. Output is asserted byte-identical to Heddle. See Runners/README.md.
    /// </summary>
    public sealed class DotLiquidTest
    {
        private static readonly object Gate = new object();
        private static bool _fileSystemSet;

        private readonly Template _home;
        private long _length;

        public DotLiquidTest()
        {
            EnsureFileSystem();
            _home = Template.Parse(LiquidTemplates.HomeTemplate);
        }

        private static void EnsureFileSystem()
        {
            lock (Gate)
            {
                if (_fileSystemSet) return;
                Template.FileSystem = new LayoutFileSystem();
                _fileSystemSet = true;
            }
        }

        private static Hash BuildModel()
        {
            var section = new Hash();
            foreach (var kv in TwinContent.Sections()) section[kv.Key] = kv.Value;

            var comp = new Hash();
            foreach (var kv in TwinContent.Components()) comp[kv.Key] = kv.Value;

            var areas = new Hash();
            foreach (var kv in TwinContent.Areas) areas[kv.Key] = kv.Value;

            var root = new Hash();
            root["section"] = section;
            root["comp"] = comp;
            root["areas"] = areas;
            root["area_names"] = new List<string>(TwinContent.AreaOrder);
            return root;
        }

        // DotLiquid does not HTML-encode output, matching Heddle.
        public string Render() => _home.Render(BuildModel());

        public Task Run()
        {
            _length += Render().Length;
            return Task.CompletedTask;
        }

        /// <summary>Cold parse of both templates (D1-R4 parse cost).</summary>
        public static void ColdParse()
        {
            Template.Parse(LiquidTemplates.LayoutTemplate);
            Template.Parse(LiquidTemplates.HomeTemplate);
        }

        private sealed class LayoutFileSystem : IFileSystem
        {
            public string ReadTemplateFile(Context context, string templateName) => LiquidTemplates.LayoutTemplate;
        }
    }
}
