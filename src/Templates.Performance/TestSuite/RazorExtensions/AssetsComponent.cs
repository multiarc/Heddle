using System.Threading.Tasks;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewComponents;

namespace Templates.Performance.TestSuite.Extensions
{
    public class RazorAssetsComponent : ViewComponent
    {
        public async Task<IViewComponentResult> InvokeAsync(string name)
        {
            var assetName = name;
            if (!string.IsNullOrEmpty(assetName))
            {
                switch (assetName)
                {
                    case "scripts":
                        return new HtmlContentViewComponentResult(new HtmlString("<script src=\"/main.js\"></script>"));
                    case "styles":
                        return new HtmlContentViewComponentResult(new HtmlString("<link rel=\"stylesheet\" href=\"/main.css\" />"));
                }
            }
            return new ContentViewComponentResult(string.Empty);
        }
    }
}