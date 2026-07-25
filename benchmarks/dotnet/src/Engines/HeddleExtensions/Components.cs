using Heddle.Attributes;
using Heddle.Benchmarks.Dotnet.Engines.HeddleExtensions;
using Heddle.Benchmarks.Dotnet.Models;
using Heddle.Core;
using Heddle.Data;

// Heddle discovers extensions through this assembly-level attribute; every extension the
// composed-page templates call must be listed or the compile fails with an unknown-extension
// diagnostic rather than silently rendering nothing.
[assembly: ExportExtensions(
    typeof(AreaComponent),
    typeof(AssetsComponent),
    typeof(CustomStyles),
    typeof(HeadScriptsComponent),
    typeof(BodyScriptsComponent),
    typeof(BodyEndScriptsComponent))]

namespace Heddle.Benchmarks.Dotnet.Engines.HeddleExtensions
{
    /// <summary>
    /// The Heddle extensions the composed-page templates call (ledger E8), ported from the retired
    /// src/Heddle.Performance/TestSuite/Extensions/.
    ///
    /// Each one returns exactly the fragment the corresponding competitor twin reads from
    /// <see cref="Models.TwinContent"/>, so "the twins render what Heddle renders" is true by
    /// construction rather than by transcription. The area dictionary in particular now lives in
    /// <see cref="AreaData"/> and is read here, rather than being owned here and copied elsewhere.
    ///
    /// Every extension implements both <c>ProcessData</c> (value position) and <c>RenderData</c>
    /// (render position). The two must agree: the sink render techniques take the RenderData path
    /// while the string path can take either, and a divergence between them would make Heddle's own
    /// techniques disagree with each other -- which is exactly what the W7 differential asserts.
    /// </summary>
    [ExtensionName("area_component")]
    public class AreaComponent : AbstractExtension
    {
        public override object ProcessData(in Scope scope)
        {
            var areaName = scope.ModelData as string;
            if (string.IsNullOrEmpty(areaName)) return string.Empty;
            return AreaData.Areas.TryGetValue(areaName, out var content) ? content : string.Empty;
        }

        public override void RenderData(in Scope scope)
        {
            var areaName = scope.ModelData as string;
            if (string.IsNullOrEmpty(areaName)) return;
            if (AreaData.Areas.TryGetValue(areaName, out var content))
                scope.Renderer.Render(content);
        }
    }

    [ExtensionName("assets_component")]
    public class AssetsComponent : AbstractExtension
    {
        private static string For(string assetName) => assetName switch
        {
            "scripts" => TwinContent.CompAssetsScripts,
            "styles" => TwinContent.CompAssetsStyles,
            _ => string.Empty,
        };

        public override object ProcessData(in Scope scope) => For(scope.ModelData as string);

        public override void RenderData(in Scope scope)
        {
            var value = For(scope.ModelData as string);
            if (value.Length != 0) scope.Renderer.Render(value);
        }
    }

    [ExtensionName("custom_styles")]
    public class CustomStyles : AbstractExtension
    {
        public override object ProcessData(in Scope scope) => TwinContent.CompCustomStyles;
        public override void RenderData(in Scope scope) => scope.Renderer.Render(TwinContent.CompCustomStyles);
    }

    [ExtensionName("head_scripts")]
    public class HeadScriptsComponent : AbstractExtension
    {
        public override object ProcessData(in Scope scope) => TwinContent.CompHeadScripts;
        public override void RenderData(in Scope scope) => scope.Renderer.Render(TwinContent.CompHeadScripts);
    }

    [ExtensionName("body_scripts")]
    public class BodyScriptsComponent : AbstractExtension
    {
        public override object ProcessData(in Scope scope) => TwinContent.CompBodyScripts;
        public override void RenderData(in Scope scope) => scope.Renderer.Render(TwinContent.CompBodyScripts);
    }

    [ExtensionName("body_end_scripts")]
    public class BodyEndScriptsComponent : AbstractExtension
    {
        public override object ProcessData(in Scope scope) => TwinContent.CompBodyEndScripts;
        public override void RenderData(in Scope scope) => scope.Renderer.Render(TwinContent.CompBodyEndScripts);
    }
}
