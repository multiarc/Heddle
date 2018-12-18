using System.Threading.Tasks;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewComponents;

namespace Templates.Performance.TestSuite.Extensions
{
    public class RazorBodyScriptsComponent : ViewComponent
    {
        public async Task<IViewComponentResult> InvokeAsync()
        {
            return new HtmlContentViewComponentResult(new HtmlString("<script src=\"/body.js\"></script>"));
        }
    }
}