using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Templates.Performance.TestSuite;

namespace Templates.Performance.Runners
{
    public class RazorTest
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IView _view;
        private readonly ViewDataDictionary<object> _viewData;
        private readonly HtmlHelperOptions _htmlHelperOptions;
        private readonly TempDataDictionary _tempData;
        private readonly ActionContext _actionContext;

        private ActionContext GetActionContext()
        {
            var httpContext = new DefaultHttpContext {RequestServices = _serviceProvider};
            return new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        }

        public RazorTest(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            var renderer = _serviceProvider.GetRequiredService<RazorViewToStringRenderer>();
            _actionContext = GetActionContext();

            _view = renderer.CompileView("home");

            _viewData = new ViewDataDictionary<object>(
                metadataProvider: new EmptyModelMetadataProvider(),
                modelState: new ModelStateDictionary());

            _htmlHelperOptions = new HtmlHelperOptions();
            var tempDataProvider = _serviceProvider.GetRequiredService<ITempDataProvider>();

            _tempData = new TempDataDictionary(
                _actionContext.HttpContext,
                tempDataProvider);
        }

        private long _length = 0;

        public async Task Run() {
            using (var output = new StringWriter()) {
                var viewContext = new ViewContext(
                    _actionContext,
                    _view,
                    _viewData, _tempData,
                    output, _htmlHelperOptions);

                await _view.RenderAsync(viewContext);
                output.Flush();
                var text = output.ToString();
                _length += text.Length;
            }
        }
    }
}