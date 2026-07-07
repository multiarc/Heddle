using Heddle.Attributes;
using Heddle.Core;
using Heddle.Data;

// Export this assembly's [ExtensionName] extensions to the engine's TemplateFactory so @tagged resolves.
[assembly: ExportExtensions(typeof(Heddle.Samples.HtmlSafeOutput.TaggingExtension))]

namespace Heddle.Samples.HtmlSafeOutput
{
    // A custom output encoder plugged in at the sanctioned seam: an [ExtensionName] extension the engine discovers
    // via HeddleTemplate.Configure. It transforms HTML-significant characters into visible tags instead of HTML
    // entities — a "tagging stub" that proves encoding is pluggable at the extension level (a dedicated
    // TemplateOptions.Encoder is a 2.0-window feature; the extension seam is how 1.x plugs a custom encoder).
    [ExtensionName("tagged")]
    [EncodeOutput]
    public sealed class TaggingExtension : AbstractExtension
    {
        public override object ProcessData(in Scope scope) => Tag(Value(scope));

        public override void RenderData(in Scope scope) => scope.Renderer.Render(Tag(Value(scope)));

        private string Value(in Scope scope)
        {
            if (InnerExist)
                return GetInnerResult(scope)?.ToString();
            return scope.ModelData?.ToString();
        }

        private static string Tag(string s)
        {
            if (string.IsNullOrEmpty(s))
                return s ?? string.Empty;
            return s.Replace("&", "[amp]")
                    .Replace("<", "[lt]")
                    .Replace(">", "[gt]")
                    .Replace("\"", "[quot]")
                    .Replace("'", "[apos]");
        }
    }
}
