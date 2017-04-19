using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.Extensions.PlatformAbstractions;
using Templates.Attributes;
using Templates.Data;
using Templates.Runtime;

namespace Templates.Mvc
{
    public class TtlViewEngine : IViewEngine
    {
        public TemplateResolver Resolver { get; }

        public TtlViewEngine(IHostingEnvironment hostingEnvironment)
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
            object controllerOption;
            string controllerName = string.Empty;
            if (context.RouteData.Values.TryGetValue("controller", out controllerOption))
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
                IEnumerable<string> searchLocations;
                var result = Resolver.GetTemplate(viewName, controllerName, out searchLocations, null, TemplatePathType.View);
                if (result != null)
                {
                    return ViewEngineResult.Found(viewName, (TtlView) result);
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
            IEnumerable<string> searchLocations;
            var result = Resolver.GetTemplate(partialViewName, controllerName, out searchLocations, null, TemplatePathType.PartialView);
            if (result != null)
            {
                return ViewEngineResult.Found(partialViewName, (TtlView)result);
            }
            return ViewEngineResult.NotFound(partialViewName, searchLocations);
        }
    }
}