using Microsoft.Owin;
using Owin;

[assembly: OwinStartupAttribute(typeof(DemoWebSite.Startup))]
namespace DemoWebSite
{
    public partial class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            ConfigureAuth(app);
        }
    }
}
