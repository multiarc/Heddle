using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Heddle;
using Heddle.Data;
using Heddle.Runtime;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Heddle.Samples.SsrAspNetCore
{
    // Sample 1 — classic server-side rendering on a minimal API. A TemplateResolver over templates/ composes a
    // page with @partial header/footer, iterates with @list and @for (phase 4 sugar), renders under the Html
    // profile with TrimDirectiveLines on, and caches compiled templates (the same request served twice is
    // byte-equal — the observable cache contract).
    public sealed class Article
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Author { get; set; }
        public int Stars { get; set; }
    }

    public sealed class SiteData
    {
        public string Title { get; set; }
        public IReadOnlyList<Article> Articles { get; set; }
    }

    internal static class Program
    {
        private static readonly IReadOnlyList<Article> Articles = new[]
        {
            new Article { Id = 1, Title = "Native Expressions Ship", Author = "Ada Lovelace", Stars = 3 },
            new Article { Id = 2, Title = "Streaming SSR", Author = "Alan Turing", Stars = 2 }
        };

        private static async Task<int> Main(string[] args)
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            HeddleTemplate.Configure(typeof(Program).Assembly);

            var templates = Path.Combine(SampleCapture.SampleRoot(), "templates") + Path.DirectorySeparatorChar;
            // One resolver for the app: Html profile + directive-line trimming, caching compiled templates
            // keyed by path+profile+trim (phase 4 upgrade). The resolver root is the templates directory.
            var resolver = new TemplateResolver(templates, checkFileChange: false, OutputProfile.Html,
                trimDirectiveLines: true);

            var builder = WebApplication.CreateBuilder();
            builder.Logging.ClearProviders();
            builder.Services.AddSingleton(resolver);
            var app = builder.Build();

            app.MapGet("/", () => Results.Content(RenderIndex(resolver), "text/html"));
            app.MapGet("/articles/{id:int}", (int id) =>
            {
                var article = Articles.FirstOrDefault(a => a.Id == id);
                return article == null
                    ? Results.NotFound()
                    : Results.Content(RenderArticle(resolver, article), "text/html");
            });

            var capture = args.Contains("--capture") ? SampleCapture.Resolve(args) : null;
            if (capture == null)
            {
                app.Run();
                return 0;
            }

            // Capture mode: bind an ephemeral port, self-request, write the bodies, assert the cache, shut down.
            app.Urls.Clear();
            app.Urls.Add("http://127.0.0.1:0");
            await app.StartAsync();
            var baseUrl = app.Services.GetRequiredService<IServer>()
                .Features.Get<IServerAddressesFeature>().Addresses.First();

            using var http = new HttpClient { BaseAddress = new Uri(baseUrl) };
            var index1 = await http.GetStringAsync("/");
            var index2 = await http.GetStringAsync("/");       // second hit is served from the resolver cache
            var article1 = await http.GetStringAsync("/articles/1");
            await app.StopAsync();

            if (!string.Equals(index1, index2, StringComparison.Ordinal))
                throw new InvalidOperationException("cache contract broken: the same page rendered differently on the second request.");

            SampleCapture.Write(capture, "index.html", index1);
            SampleCapture.Write(capture, "article-1.html", article1);
            Console.WriteLine("captured index.html, article-1.html (cache contract held)");
            return 0;
        }

        private static string RenderIndex(TemplateResolver resolver)
        {
            var template = resolver.GetTemplate("index.heddle", "Home", out _);
            return template.Generate(new SiteData { Title = "Latest articles", Articles = Articles });
        }

        private static string RenderArticle(TemplateResolver resolver, Article article)
        {
            var template = resolver.GetTemplate("article.heddle", "Articles", out _);
            return template.Generate(article);
        }
    }
}
