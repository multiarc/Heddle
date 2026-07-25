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
using Heddle.Performance.TestSuite;

namespace Heddle.Performance.Runners
{
    /// <summary>
    /// The ASP.NET Core Razor parity twin (D1; ledger entry E5).
    ///
    /// Renders <c>Views/twin-home.cshtml</c> + <c>Views/twin-layout.cshtml</c>, which reproduce the
    /// byte sequence Heddle emits, from the shared <see cref="TwinContent"/> fixtures projected onto
    /// <see cref="RazorTwinModel"/>. Before E5 this class rendered the full <c>Views/layout.cshtml</c>
    /// page — a larger, different payload — and so sat outside the parity assertion while occupying a
    /// row inside a protocol suite whose premise is identical work.
    ///
    /// Unlike the other twins this one needs an <see cref="IServiceProvider"/>: MVC view rendering
    /// resolves the view engine, temp-data provider and model metadata from DI. That is why
    /// <see cref="ParityCheck"/> has a host-aware overload rather than a single parameterless one.
    /// </summary>
    public class RazorTest
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IView _view;
        private readonly ViewDataDictionary<RazorTwinModel> _viewData;
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

            _view = renderer.CompileView("twin-home");

            _viewData = new ViewDataDictionary<RazorTwinModel>(
                metadataProvider: new EmptyModelMetadataProvider(),
                modelState: new ModelStateDictionary())
            {
                Model = RazorTwinModel.Create(),
            };

            _htmlHelperOptions = new HtmlHelperOptions();
            var tempDataProvider = _serviceProvider.GetRequiredService<ITempDataProvider>();

            _tempData = new TempDataDictionary(
                _actionContext.HttpContext,
                tempDataProvider);
        }

        /// <summary>
        /// Renders the twin to a string. Every other twin exposes this shape, which is what lets
        /// <see cref="ParityCheck"/> treat them uniformly as <c>Func&lt;string&gt;</c>.
        /// </summary>
        public string Render()
        {
            using (var output = new StringWriter())
            {
                var viewContext = new ViewContext(
                    _actionContext,
                    _view,
                    _viewData, _tempData,
                    output, _htmlHelperOptions);

                // MVC's rendering pipeline is async-only. The view is fully in-memory and does no
                // I/O, so this completes synchronously in practice; GetAwaiter().GetResult()
                // rethrows without the AggregateException wrapper .Wait() would add.
                _view.RenderAsync(viewContext).GetAwaiter().GetResult();
                output.Flush();
                return output.ToString();
            }
        }

        public Task Run()
        {
            RenderSink = Render();
            return Task.CompletedTask;
        }

        /// <summary>Keeps the rendered output observable so the render cannot be optimized away.</summary>
        public string RenderSink { get; private set; }
    }
}