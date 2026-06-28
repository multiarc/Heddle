using System.Threading.Tasks;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewComponents;

namespace Heddle.Performance.TestSuite.Extensions
{
    public class RazorCustomStyles : ViewComponent
    {
        public Task<IViewComponentResult> InvokeAsync()
        {
            return Task.FromResult<IViewComponentResult>(new HtmlContentViewComponentResult(new HtmlString("/* CSS Comment Test */")));
        }
    }
}