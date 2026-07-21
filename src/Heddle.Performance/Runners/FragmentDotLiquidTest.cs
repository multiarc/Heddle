using System.Globalization;
using System.Threading.Tasks;
using DotLiquid;
using DotLiquid.FileSystems;

namespace Heddle.Performance.Runners
{
    /// <summary>
    /// DotLiquid (Liquid) twin of the fragment-heavy workload. The <c>tile</c> partial is pulled
    /// in 48 times with a plain <c>{% include 'tile' %}</c>, which shares the enclosing scope so
    /// the partial reads the loop's <c>item.*</c> members (probe-E-verified construct;
    /// <c>include … with &lt;hash&gt;</c> must NOT be used — DotLiquid enumerates a Hash argument
    /// and renders once per key). The tile file system rides the per-template
    /// <see cref="Template.Registers"/> (<c>file_system</c> register) rather than the process-wide
    /// <see cref="Template.FileSystem"/>, which the composed-page twin owns. DotLiquid does not
    /// HTML-encode output, matching the Heddle oracle's <c>OutputProfile.Text</c>. Output is
    /// asserted equal to Heddle under the parity normalizer.
    /// </summary>
    public sealed class FragmentDotLiquidTest
    {
        private readonly Template _page;
        private long _length;

        public FragmentDotLiquidTest()
        {
            _page = Template.Parse(FragmentLiquidTemplates.DotLiquidPageTemplate);
            _page.Registers["file_system"] = TileFileSystem.Instance;
        }

        public string Render() => _page.Render(new RenderParameters(CultureInfo.CurrentCulture)
        {
            LocalVariables = FragmentContent.DotLiquidModel(),
        });

        public Task Run()
        {
            _length += Render().Length;
            return Task.CompletedTask;
        }

        private sealed class TileFileSystem : IFileSystem
        {
            public static readonly TileFileSystem Instance = new TileFileSystem();
            public string ReadTemplateFile(Context context, string templateName) => FragmentLiquidTemplates.DotLiquidTileTemplate;
        }
    }
}
