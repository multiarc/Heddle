using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Heddle.Demo.Wasm
{
    /// <summary>
    /// The browser shell (phase 9 D6): JSON in, JSON out. Contains marshalling only — every behavior lives in
    /// <see cref="DemoHost"/> (tested on CoreCLR by DemoContractTests). The worker calls these through
    /// <c>getAssemblyExports(...).Heddle.Demo.Wasm.DemoInterop.*</c>.
    /// </summary>
    [SupportedOSPlatform("browser")]
    public static partial class DemoInterop
    {
        private static readonly DemoHost Host = new DemoHost();

        [JSExport]
        internal static string Init() => Serialize(Host.Init(), DemoJsonContext.Default.InitResult);

        [JSExport]
        internal static string Analyze(string path, string text, int version) =>
            Serialize(Host.Analyze(path, text, version), DemoJsonContext.Default.AnalyzeResult);

        [JSExport]
        internal static string Complete(string path, int offset) =>
            Serialize(Host.Complete(path, offset), DemoJsonContext.Default.CompleteResult);

        [JSExport]
        internal static string Hover(string path, int offset)
        {
            var hover = Host.Hover(path, offset);
            return hover == null ? "null" : Serialize(hover, DemoJsonContext.Default.HoverResultDto);
        }

        [JSExport]
        internal static string Render(string path, string modelId) =>
            Serialize(Host.Render(path, modelId), DemoJsonContext.Default.RenderResult);

        private static string Serialize<T>(T value, JsonTypeInfo<T> typeInfo) => JsonSerializer.Serialize(value, typeInfo);
    }
}
