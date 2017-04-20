using System.Threading.Tasks;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewComponents;
using Templates.Attributes;
using Templates.Core;
using Templates.Data;
using Templates.Performance.TestSuite.Extensions;

namespace Templates.Performance.TestSuite.Extensions
{
    public class RazorHeadScriptsComponent : ViewComponent
    {
        public async Task<IViewComponentResult> InvokeAsync()
        {
            return new HtmlContentViewComponentResult(new HtmlString("<script src=\"/head.js\"></script>"));
        }
    }
}