using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
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

namespace Heddle.Samples.StreamingSsr
{
    // Sample 10 — streaming SSR. The endpoint renders directly into Response.BodyWriter (an IBufferWriter<byte>)
    // then FlushAsync — no full-output string is ever materialized. A /text-writer endpoint shows the TextWriter
    // sink for contrast. Capture asserts encoding parity: the byte-sink output decoded as UTF-8 equals the
    // string Generate output for the same template + data.
    public sealed class Report
    {
        public string Title { get; set; }
        public IReadOnlyList<string> Items { get; set; }
    }

    internal static class Program
    {
        private const string TemplateText = """
            <h1>@(Title)</h1>
            <ul>
              @list(Items){{
                <li>@(this)</li>
              }}
            </ul>
            """;

        private static readonly Report Model = new Report
        {
            Title = "Streaming <report> & Co.",
            Items = new[] { "first & foremost", "second <b>", "third" }
        };

        private static async Task<int> Main(string[] args)
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            HeddleTemplate.Configure(typeof(Program).Assembly);

            // One compiled template, shared by both sinks (Html profile so encoding parity is meaningful).
            var template = new HeddleTemplate(TemplateText,
                new CompileContext(new TemplateOptions { OutputProfile = OutputProfile.Html }, typeof(Report)));
            if (!template.CompileResult.Success)
                throw new InvalidOperationException("compile failed: " + template.CompileResult);

            var builder = WebApplication.CreateBuilder();
            builder.Logging.ClearProviders();
            var app = builder.Build();

            // Byte sink: render straight into the response pipe, then flush. No intermediate string.
            app.MapGet("/", async (HttpContext ctx) =>
            {
                ctx.Response.ContentType = "text/html";
                template.Generate(Model, ctx.Response.BodyWriter);
                await ctx.Response.BodyWriter.FlushAsync();
            });

            // TextWriter sink for contrast.
            app.MapGet("/text-writer", async (HttpContext ctx) =>
            {
                ctx.Response.ContentType = "text/html";
                await using var writer = new StreamWriter(ctx.Response.Body, new UTF8Encoding(false));
                template.Generate(Model, writer);
                await writer.FlushAsync();
            });

            var capture = args.Contains("--capture") ? SampleCapture.Resolve(args) : null;
            if (capture == null)
            {
                app.Run();
                return 0;
            }

            app.Urls.Clear();
            app.Urls.Add("http://127.0.0.1:0");
            await app.StartAsync();
            var baseUrl = app.Services.GetRequiredService<IServer>()
                .Features.Get<IServerAddressesFeature>().Addresses.First();

            using var http = new HttpClient { BaseAddress = new Uri(baseUrl) };
            var streamed = await http.GetStringAsync("/");
            var textWriter = await http.GetStringAsync("/text-writer");
            await app.StopAsync();

            // Phase 8 encoding-parity rule at integration level: byte-sink == string Generate == TextWriter sink.
            var stringOut = template.Generate(Model);
            if (!string.Equals(streamed, stringOut, StringComparison.Ordinal))
                throw new InvalidOperationException("parity broken: IBufferWriter<byte> output != string Generate output.");
            if (!string.Equals(textWriter, stringOut, StringComparison.Ordinal))
                throw new InvalidOperationException("parity broken: TextWriter output != string Generate output.");

            SampleCapture.Write(capture, "streamed.html", streamed);
            SampleCapture.Write(capture, "textwriter.html", textWriter);
            Console.WriteLine("captured streamed.html, textwriter.html (byte/string/TextWriter parity held)");
            return 0;
        }
    }
}
