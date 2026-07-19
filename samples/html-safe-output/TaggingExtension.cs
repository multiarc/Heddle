using Heddle.Attributes;
using Heddle.Core;
using Heddle.Data;

// Export this assembly's [ExtensionName] extensions to the engine's TemplateFactory so @tagged resolves.
[assembly: ExportExtensions(typeof(Heddle.Samples.HtmlSafeOutput.TaggingExtension))]

namespace Heddle.Samples.HtmlSafeOutput
{
    // A custom output encoder plugged in at the per-extension seam: an [ExtensionName] extension the engine discovers
    // via HeddleTemplate.Configure. It transforms HTML-significant characters into visible tags instead of HTML
    // entities — a "tagging stub" that proves encoding is pluggable at the extension level. This is the 1.x seam;
    // for a whole-render encoder swap use TemplateOptions.Encoder (see Program.cs, options-encoder.html).
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
