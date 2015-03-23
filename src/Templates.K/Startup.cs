using Microsoft.Framework.DependencyInjection;
using Microsoft.Framework.DependencyInjection.Fallback;
using System;

namespace Templates
{
    public class Startup
    {
        public IServiceProvider ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<ITtlTemplate, TtlTemplate>();
            return services.BuildServiceProvider();
        }
    }
}