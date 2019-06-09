using Templates.Attributes;
using Templates.Core;
using Templates.Data;
using Templates.Performance.TestSuite.Extensions;

[assembly: ExportExtensions(typeof(AssetsComponent))]

namespace Templates.Performance.TestSuite.Extensions
{
    [ExtensionName("assetscomponent")]
    public class AssetsComponent : AbstractExtension
    {
        public override object ProcessData(in Scope scope)
        {
            var assetName = scope.ModelData as string;
            if (!string.IsNullOrEmpty(assetName))
            {
                switch (assetName)
                {
                    case "scripts":
                        return "<script src=\"/main.js\"></script>";
                    case "styles":
                        return "<link rel=\"stylesheet\" href=\"/main.css\" />";
                }
            }
            return string.Empty;
        }

        public override void RenderData(in Scope scope)
        {
            var assetName = scope.ModelData as string;
            if (!string.IsNullOrEmpty(assetName))
            {
                switch (assetName)
                {
                    case "scripts":
                        scope.Render("<script src=\"/main.js\"></script>");
                        break;
                    case "styles":
                        scope.Render("<link rel=\"stylesheet\" href=\"/main.css\" />");
                        break;
                }
            }
        }
    }
}