using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Microsoft.AspNet.Mvc;
using Microsoft.AspNet.Mvc.Rendering;
using Microsoft.Dnx.Runtime;
using Microsoft.Dnx.Runtime.Infrastructure;
using Templates.Attributes;
using Templates.Data;
using Templates.Runtime;

namespace Templates.Mvc
{
    public class TtlViewEngine : IViewEngine
    {
        public static TemplateResolver Resolver { get; }

        static TtlViewEngine()
        {
            string path = ".";
            try
            {
                var env = (IApplicationEnvironment)CallContextServiceLocator.Locator.ServiceProvider.GetService(typeof(IApplicationEnvironment));
                path = env.ApplicationBasePath;
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

        public ViewEngineResult FindView(ActionContext context, string viewName)
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

        public ViewEngineResult FindPartialView(ActionContext context, string partialViewName)
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