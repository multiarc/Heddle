using System.Reflection;
using System.Web.Mvc;
using DemoWebSite;
using Microsoft.Owin;
using Owin;
using Templates.Mvc;
using Templates.Runtime;

[assembly: OwinStartup(typeof(Startup))]
namespace DemoWebSite
{
    public partial class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            ConfigureAuth(app);
            ViewEngines.Engines.Clear();
            ViewEngines.Engines.Add(new TtlViewEngine());
            TemplateFactory.LoadAddExtensionsFromAssembly(Assembly.GetAssembly(typeof (TtlViewEngine)));
        }
    }
}