using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Heddle.Data;
using Heddle.Runtime;

namespace Heddle.Mvc
{
    public class HeddleViewEngine : IViewEngine
    {
        public TemplateResolver Resolver { get; }

        #if NETSTANDARD2_0
        public HeddleViewEngine(IHostingEnvironment hostingEnvironment)
        #else
        public HeddleViewEngine(IWebHostEnvironment hostingEnvironment)
        #endif
        {
            string path = ".";
            try
            {
                path = hostingEnvironment.WebRootPath;
            }
            finally
            {
                Resolver = new TemplateResolver(Path.GetDirectoryName(path));
            }
        }

        private static string GetControllerName(ActionContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            string controllerName = string.Empty;
            if (context.RouteData.Values.TryGetValue("controller", out var controllerOption))
            {
                controllerName = controllerOption.ToString();
            }
            return controllerName;
        }

        public ViewEngineResult FindView(ActionContext context, string viewName, bool isMainPage)
        {
            if (isMainPage)
            {
                if (context == null) throw new ArgumentNullException(nameof(context));
                if (string.IsNullOrWhiteSpace(viewName))
                    throw new ArgumentException("viewName");
                var controllerName = GetControllerName(context);
                var result = Resolver.GetTemplate(viewName, controllerName, out var searchLocations, null, TemplatePathType.View);
                if (result != null)
                {
                    return ViewEngineResult.Found(viewName, (HeddleView) result);
                }
                return ViewEngineResult.NotFound(viewName, searchLocations);
            }
            return FindPartialView(context, viewName);
        }

        public ViewEngineResult GetView(string executingFilePath, string viewPath, bool isMainPage)
        {
            return ViewEngineResult.NotFound(viewPath, Enumerable.Empty<string>());
        }

        private ViewEngineResult FindPartialView(ActionContext context, string partialViewName)
        {
            if (string.IsNullOrWhiteSpace(partialViewName))
                throw new ArgumentException("viewName");
            var controllerName = GetControllerName(context);
            var result = Resolver.GetTemplate(partialViewName, controllerName, out var searchLocations, null, TemplatePathType.PartialView);
            if (result != null)
            {
                return ViewEngineResult.Found(partialViewName, (HeddleView)result);
            }
            return ViewEngineResult.NotFound(partialViewName, searchLocations);
        }
    }
}