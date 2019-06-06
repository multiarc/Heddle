using System.Threading.Tasks;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewComponents;

namespace Templates.Performance.TestSuite.Extensions
{
    public class RazorHeadScriptsComponent : ViewComponent
    {
        public Task<IViewComponentResult> InvokeAsync()
        {
            return Task.FromResult<IViewComponentResult>(new HtmlContentViewComponentResult(new HtmlString("<script src=\"/head.js\"></script>")));
        }
    }
}