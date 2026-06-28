using System.Threading.Tasks;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewComponents;

namespace Heddle.Performance.TestSuite.Extensions
{
    public class RazorAssetsComponent : ViewComponent
    {
        public Task<IViewComponentResult> InvokeAsync(string name)
        {
            var assetName = name;
            if (!string.IsNullOrEmpty(assetName))
            {
                switch (assetName)
                {
                    case "scripts":
                        return Task.FromResult<IViewComponentResult>(new HtmlContentViewComponentResult(new HtmlString("<script src=\"/main.js\"></script>")));
                    case "styles":
                        return Task.FromResult<IViewComponentResult>(
                            new HtmlContentViewComponentResult(new HtmlString("<link rel=\"stylesheet\" href=\"/main.css\" />")));
                }
            }

            return Task.FromResult<IViewComponentResult>(new ContentViewComponentResult(string.Empty));
        }
    }
}