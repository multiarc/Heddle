using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Fluid;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;

namespace Heddle.Performance.Runners
{
    /// <summary>
    /// Fluid (Liquid) twin of the composed home page. The layout is pulled in with
    /// <c>{% include 'layout' %}</c> via an in-memory file provider, reusable sections are
    /// <c>section.*</c> members, component calls are <c>areas[...]</c> / <c>comp.*</c> lookups, and
    /// the ordered area menus are rendered with a real <c>{% for %}</c> loop. Shares its Liquid
    /// source with <see cref="DotLiquidTest"/>. Output is asserted byte-identical to Heddle.
    /// See Runners/README.md.
    /// </summary>
    public sealed class FluidTest
    {
        private static readonly FluidParser Parser = new FluidParser();

        private readonly IFluidTemplate _home;
        private readonly TemplateOptions _options;
        private long _length;

        public FluidTest()
        {
            _home = Parser.Parse(LiquidTemplates.HomeTemplate);
            _options = new TemplateOptions { FileProvider = LayoutFileProvider.Instance };
        }

        private TemplateContext CreateContext()
        {
            var context = new TemplateContext(_options);
            context.SetValue("section", ToObjectDict(TwinContent.Sections()));
            context.SetValue("comp", ToObjectDict(TwinContent.Components()));
            context.SetValue("areas", ToObjectDict(TwinContent.Areas));
            context.SetValue("area_names", TwinContent.AreaOrder);
            return context;
        }

        private static Dictionary<string, object> ToObjectDict(IEnumerable<KeyValuePair<string, string>> src)
        {
            var d = new Dictionary<string, object>();
            foreach (var kv in src) d[kv.Key] = kv.Value;
            return d;
        }

        // Fluid's default convenience encoder is the raw (non-encoding) encoder, matching Heddle;
        // this keeps the area menus' `&`, `™`, and pre-escaped `&amp;` byte-identical.
        public string Render() => _home.Render(CreateContext());

        public Task Run()
        {
            _length += Render().Length;
            return Task.CompletedTask;
        }

        /// <summary>Cold parse of both templates (D1-R4 parse cost).</summary>
        public static void ColdParse()
        {
            Parser.Parse(LiquidTemplates.LayoutTemplate);
            Parser.Parse(LiquidTemplates.HomeTemplate);
        }

        private sealed class LayoutFileProvider : IFileProvider
        {
            public static readonly LayoutFileProvider Instance = new LayoutFileProvider();
            private readonly IFileInfo _layout = new StringFileInfo(LiquidTemplates.LayoutTemplate, "layout.liquid");

            public IDirectoryContents GetDirectoryContents(string subpath) => NotFoundDirectoryContents.Singleton;
            public IFileInfo GetFileInfo(string subpath) => _layout; // only one include target exists
            public IChangeToken Watch(string filter) => NullChangeToken.Singleton;
        }

        private sealed class StringFileInfo : IFileInfo
        {
            private readonly byte[] _bytes;
            public StringFileInfo(string content, string name) { _bytes = Encoding.UTF8.GetBytes(content); Name = name; }
            public bool Exists => true;
            public long Length => _bytes.Length;
            public string PhysicalPath => null;
            public string Name { get; }
            public System.DateTimeOffset LastModified => System.DateTimeOffset.MinValue;
            public bool IsDirectory => false;
            public Stream CreateReadStream() => new MemoryStream(_bytes, writable: false);
        }
    }
}
