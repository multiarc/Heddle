using Templates.Core;
using Templates.Data;

namespace Templates.Performance.TestSuite.Extensions
{
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