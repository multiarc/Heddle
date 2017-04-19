using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Templates.Performance
{
    public class Startup
    {
        public IConfiguration Configuration { get; }

        public Startup()
        {
            // Set the maximum number of concurrent connections
            var builder = new ConfigurationBuilder();
            Configuration = builder.Build();
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMvc();
            services.AddSingleton<IServer>(new ServerStuff());
        }

        public void Configure(IApplicationBuilder app)
        {
            app.UseMvc();
        }
    }

    public class ServerStuff : IServer
    {
        public void Dispose()
        {
        }

        public void Start<TContext>(IHttpApplication<TContext> application)
        {
        }

        public IFeatureCollection Features { get; } = new FeatureCollection();
    }
}