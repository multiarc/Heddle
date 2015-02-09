using System;
using System.Web.Mvc;

namespace Templates.Mvc
{
    public class TtlViewEngine : BuildManagerViewEngine
    {
        public TemplateResolver Resolver { get; set; }

        protected override IView CreatePartialView(ControllerContext controllerContext, string partialPath)
        {
            return CreateView(controllerContext, partialPath, null);
        }

        protected override IView CreateView(ControllerContext controllerContext, string viewPath, string masterPath)
        {
            if (string.IsNullOrWhiteSpace(viewPath))
                throw new ArgumentException("viewName");
            return (TtlView) Resolver.GetView(viewPath);
        }

        public TtlViewEngine() : this(null)
        {
            
        }

        public TtlViewEngine(IViewPageActivator viewPageActivator)
            : base(viewPageActivator)
        {
            Resolver = new TemplateResolver();
            AreaViewLocationFormats = new string[2]
            {
                @"\Areas\{2}\Views\{1}\{0}.thtml",
                @"\Areas\{2}\Views\Shared\{0}.thtml"
            };
            AreaMasterLocationFormats = new string[2]
            {
                @"\Areas\{2}\Views\{1}\{0}.thtml",
                @"\Areas\{2}\Views\Shared\{0}.thtml"
            };
            AreaPartialViewLocationFormats = new string[2]
            {
                @"\Areas\{2}\Views\{1}\{0}.thtml",
                @"\Areas\{2}\Views\Shared\{0}.thtml"
            };
            ViewLocationFormats = new string[2]
            {
                @"\Views\{1}\{0}.thtml",
                @"\Views\Shared\{0}.thtml"
            };
            MasterLocationFormats = new string[2]
            {
                @"\Views\{1}\{0}.thtml",
                @"\Views\Shared\{0}.thtml"
            };
            PartialViewLocationFormats = new string[2]
            {
                @"\Views\{1}\{0}.thtml",
                @"\Views\Shared\{0}.thtml"
            };
            FileExtensions = new string[1]
            {
                "thtml"
            };
        }
    }
}