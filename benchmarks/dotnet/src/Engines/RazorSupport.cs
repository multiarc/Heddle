using System;
using System.Diagnostics;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using System.IO;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;

namespace Heddle.Benchmarks.Dotnet.Engines
{
    /// <summary>
    /// The MVC host the Razor twin renders through.
    ///
    /// Razor is the one engine in this harness that renders through dependency injection, so it
    /// needs a host where the others need a parser. The host's content root is pointed at
    /// <c>templates/&lt;track&gt;/razor/</c>, so views resolve from the same templates tree every
    /// other engine reads from rather than from a copied-to-output <c>Views/</c> directory.
    /// </summary>
    public static class RazorSupport
    {
        /// <summary>Builds and starts a host whose views resolve under the given track.</summary>
        public static IHost BuildHost(string track)
        {
            var contentRoot = Path.Combine(Templates.Root(), track, "razor");
            if (!Directory.Exists(contentRoot))
                throw new DirectoryNotFoundException($"Razor templates not found at {contentRoot}");

            var host = Host.CreateDefaultBuilder()
                .ConfigureServices(services => ConfigureServices(services, contentRoot))
                // Lifecycle hygiene only, no measurement effect: suppress the console lifetime's
                // "Application started / Press Ctrl+C" banner so harness stdout stays parseable,
                // and keep warnings and above on stderr.
                .UseConsoleLifetime(o => o.SuppressStatusMessages = true)
                .ConfigureLogging(logging =>
                    logging.AddConsole(co => co.LogToStandardErrorThreshold = LogLevel.Warning))
                .Build();
            host.Start();
            return host;
        }

        private static void ConfigureServices(IServiceCollection services, string contentRoot)
        {
            services.AddSingleton<ObjectPoolProvider, DefaultObjectPoolProvider>();

            var diagnosticSource = new DiagnosticListener("Microsoft.AspNetCore");
            services.AddSingleton<DiagnosticSource>(diagnosticSource);
            services.AddSingleton<DiagnosticListener>(diagnosticSource);

            services.AddLogging();

            // Razor's DEFAULT HtmlEncoder escapes every non-ASCII character to a numeric reference:
            // the em-dash in the fortunes corpus comes out as `&#x2014;`. Contract v2's N5 rule
            // canonicalizes only the five markup-significant characters, so that is a genuine
            // divergence from the oracle, not something normalization absorbs -- it inflated
            // encoded-loop by 125,000 characters.
            //
            // Widening the encoder to the full Unicode range is engine CONFIGURATION, which the
            // parity contract prefers over a normalization carve-out, and is exactly parallel to the
            // FiveEntityTextEncoder the Handlebars twin configures for the same reason. The five
            // markup characters are still escaped -- the security floor proves it -- and the
            // apostrophe's `&#x27;` spelling is what N5 canonicalizes to `&#39;`.
            services.AddSingleton(HtmlEncoder.Create(UnicodeRanges.All));

            services.AddControllersWithViews();
            services.AddRazorPages()
                .AddRazorRuntimeCompilation()
                .AddApplicationPart(typeof(RazorSupport).Assembly);
            services.AddSingleton<ViewRenderer>();

            var fileProvider = new PhysicalFileProvider(contentRoot);
            services.AddSingleton<IWebHostEnvironment>(new HarnessHostingEnvironment(fileProvider, contentRoot));

            // Runtime compilation resolves .cshtml through its OWN file provider list, which is
            // seeded from IWebHostEnvironment.ContentRootFileProvider only for the default host.
            // Setting it explicitly is what makes views resolve out of templates/ instead of the
            // process working directory.
            // Obsolete upstream, and used deliberately: runtime compilation is the Razor mode this harness
            // benchmarks Heddle's dynamic tier against. Dropping it would remove the comparison, not modernise it.
#pragma warning disable ASPDEPR003
            services.Configure<Microsoft.AspNetCore.Mvc.Razor.RuntimeCompilation.MvcRazorRuntimeCompilationOptions>(
                options => options.FileProviders.Add(fileProvider));
#pragma warning restore ASPDEPR003
        }

        private sealed class HarnessHostingEnvironment : IWebHostEnvironment
        {
            public HarnessHostingEnvironment(IFileProvider provider, string root)
            {
                ContentRootFileProvider = provider;
                WebRootFileProvider = provider;
                ContentRootPath = root;
                WebRootPath = root;
            }

            public string EnvironmentName { get; set; } = "Production";
            public string ApplicationName { get; set; } = "Heddle.Benchmarks.Dotnet";
            public string WebRootPath { get; set; }
            public IFileProvider WebRootFileProvider { get; set; }
            public string ContentRootPath { get; set; }
            public IFileProvider ContentRootFileProvider { get; set; }
        }

        /// <summary>Finds a compiled view by name (ported from RazorViewToStringRenderer).</summary>
        public sealed class ViewRenderer
        {
            private readonly IRazorViewEngine _viewEngine;
            private readonly IServiceProvider _services;

            public ViewRenderer(IRazorViewEngine viewEngine, IServiceProvider services)
            {
                _viewEngine = viewEngine;
                _services = services;
            }

            public IView CompileView(string name)
            {
                var result = _viewEngine.GetView(executingFilePath: null,
                    viewPath: $"/Views/{name}.cshtml", isMainPage: true);
                if (result.Success) return result.View;

                var byName = _viewEngine.FindView(ActionContext(), name, isMainPage: true);
                if (byName.Success) return byName.View;

                throw new InvalidOperationException(
                    $"Razor could not find view '{name}'. Searched: " +
                    string.Join(", ", result.SearchedLocations ?? byName.SearchedLocations ?? Array.Empty<string>()));
            }

            public ActionContext ActionContext()
            {
                var httpContext = new DefaultHttpContext { RequestServices = _services };
                return new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
            }
        }
    }
}
