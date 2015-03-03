using System;
using System.Collections.Generic;
using System.IO;
using System.Web;
using System.Web.Mvc;
using Templates.Data;
using Templates.Runtime;

namespace Templates.Mvc
{
    public class TtlViewEngine : IViewEngine
    {
        public static TemplateResolver Resolver { get; }

        static TtlViewEngine()
        {
            Resolver = new TemplateResolver(Path.GetDirectoryName(HttpRuntime.AppDomainAppPath));
        }

        public ViewEngineResult FindPartialView(ControllerContext controllerContext, string partialViewName, bool useCache)
        {
            if (string.IsNullOrWhiteSpace(partialViewName))
                throw new ArgumentException("viewName");
            var controllerName = GetControllerName(controllerContext);
            IEnumerable<string> searchLocations;
            var result = Resolver.GetTemplate(partialViewName, controllerName, out searchLocations, null, TemplatePathType.PartialView);
            if (result != null) {
                return new ViewEngineResult((TtlView)result, this);
            }
            return new ViewEngineResult(searchLocations);
        }

        private static string GetControllerName(ControllerContext controllerContext)
        {
            object controllerOption;
            string controllerName = string.Empty;
            if (controllerContext.RouteData.Values.TryGetValue("controller", out controllerOption))
            {
                controllerName = controllerOption.ToString();
            }
            return controllerName;
        }

        public ViewEngineResult FindView(ControllerContext controllerContext, string viewName, string masterName, bool useCache)
        {
            if (string.IsNullOrWhiteSpace(viewName))
                throw new ArgumentException("viewName");
            var controllerName = GetControllerName(controllerContext);
            IEnumerable<string> searchLocations;
            var result = Resolver.GetTemplate(viewName, controllerName, out searchLocations, null, TemplatePathType.View);
            if (result != null)
            {
                return new ViewEngineResult((TtlView)result, this);
            }
            return new ViewEngineResult(searchLocations);
        }

        public void ReleaseView(ControllerContext controllerContext, IView view)
        {
            var template = view as TtlView;
            Resolver.RemoveFromCache(template?.Template);
            template?.Dispose();
        }
    }
}