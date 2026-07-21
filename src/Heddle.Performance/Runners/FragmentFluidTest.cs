using System.IO;
using System.Text;
using System.Threading.Tasks;
using Fluid;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;

namespace Heddle.Performance.Runners
{
    /// <summary>
    /// Fluid (Liquid) twin of the fragment-heavy workload. The <c>tile</c> partial is pulled in
    /// 48 times with <c>{% include 'tile' with item %}</c> via an in-memory file provider (the
    /// <see cref="FluidTest"/> mechanism); <c>with</c> binds the row to a variable named after
    /// the partial, so the partial reads <c>tile.*</c> members (probe-E-verified construct).
    /// Fluid's default convenience encoder is raw, matching the Heddle oracle's
    /// <c>OutputProfile.Text</c>. Output is asserted equal to Heddle under the parity normalizer.
    /// </summary>
    public sealed class FragmentFluidTest
    {
        private static readonly FluidParser Parser = new FluidParser();

        private readonly IFluidTemplate _page;
        private readonly TemplateOptions _options;
        private long _length;

        public FragmentFluidTest()
        {
            _page = Parser.Parse(FragmentLiquidTemplates.FluidPageTemplate);
            _options = new TemplateOptions { FileProvider = TileFileProvider.Instance };
        }

        private TemplateContext CreateContext()
        {
            var context = new TemplateContext(_options);
            foreach (var kv in FragmentContent.LiquidModel())
                context.SetValue(kv.Key, kv.Value);
            return context;
        }

        public string Render() => _page.Render(CreateContext());

        public Task Run()
        {
            _length += Render().Length;
            return Task.CompletedTask;
        }

        private sealed class TileFileProvider : IFileProvider
        {
            public static readonly TileFileProvider Instance = new TileFileProvider();
            private readonly IFileInfo _tile = new StringFileInfo(FragmentLiquidTemplates.FluidTileTemplate, "tile.liquid");

            public IDirectoryContents GetDirectoryContents(string subpath) => NotFoundDirectoryContents.Singleton;
            public IFileInfo GetFileInfo(string subpath) => _tile; // only one include target exists
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
