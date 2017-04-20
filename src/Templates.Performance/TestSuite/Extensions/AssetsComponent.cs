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
        public override object ProcessData(ref Scope scope)
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
    }
}