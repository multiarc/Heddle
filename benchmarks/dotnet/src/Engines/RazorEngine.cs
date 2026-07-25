using System;
using System.Collections.Generic;
using System.IO;
using Heddle.Benchmarks.Dotnet.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Heddle.Benchmarks.Dotnet.Engines
{
    /// <summary>
    /// ASP.NET Core Razor twin, ALL EIGHT workloads (ledger E9).
    ///
    /// The retired harness measured Razor on <c>composed-page</c> and nothing else, so the
    /// repository's "faster than Razor" claim rested on the single least favourable workload in the
    /// set — a bulk-concatenation shape whose UTF-16 output crosses the Large Object Heap threshold.
    /// There was no realistic-sized Razor figure at all, and the gap had to be published as a
    /// caveat. This closes it.
    ///
    /// Razor renders through MVC DI, so unlike every other engine here it needs a host. The host is
    /// built once and its views compiled once, both in setup: view compilation is Razor's parse
    /// step and belongs to the cold-compile sidebar, not to a render measurement.
    /// </summary>
    public static class RazorEngine
    {
        public const string Name = "Razor (ASP.NET Core MVC)";

        // One host per track: the two tracks' views live under different content roots, and a
        // Razor host binds its file providers at construction.
        private static readonly Dictionary<string, IHost> Hosts =
            new Dictionary<string, IHost>(StringComparer.Ordinal);
        private static readonly Dictionary<string, RazorSupport.ViewRenderer> Renderers =
            new Dictionary<string, RazorSupport.ViewRenderer>(StringComparer.Ordinal);

        /// <summary>The track's renderer, built on first use. Disposed by <see cref="Shutdown"/>.</summary>
        private static RazorSupport.ViewRenderer Renderer(string track)
        {
            if (Renderers.TryGetValue(track, out var existing)) return existing;
            var host = RazorSupport.BuildHost(track);
            Hosts[track] = host;
            var renderer = host.Services.GetRequiredService<RazorSupport.ViewRenderer>();
            Renderers[track] = renderer;
            return renderer;
        }

        /// <summary>
        /// Stops and disposes the host. Without disposal the runtime-compilation file watchers and
        /// the console lifetime registrations stay alive and can keep a BenchmarkDotNet child
        /// process from exiting on Windows after the run finishes.
        /// </summary>
        public static void Shutdown()
        {
            foreach (var host in Hosts.Values)
            {
                host.StopAsync().GetAwaiter().GetResult();
                host.Dispose();
            }
            Hosts.Clear();
            Renderers.Clear();
        }

        public static IEnumerable<Cell> Cells(string track)
        {
            yield return Make(track, "composed-page", (object)null);
            yield return Make(track, "trivial-substitution", SubstitutionContent.Model());
            yield return Make(track, "large-loop", LoopContent.Model());
            yield return Make(track, "mixed-page", MixedContent.Model());
            yield return Make(track, "conditional-heavy", ConditionalContent.Model());
            yield return Make(track, "fragment-heavy", FragmentContent.Model());
            yield return Make(track, "fortunes-encoded", FortunesContent.Model());
            yield return Make(track, "encoded-loop", EncodedLoopContent.Model());
        }

        private static Cell Make<TModel>(string track, string workload, TModel model)
        {
            // Compiled lazily but exactly once, on the first render of this cell. Doing it here
            // rather than in Cells() keeps registry construction cheap for the gate, which builds
            // every cell but may only run some of them.
            IView view = null;
            RazorSupport.ViewRenderer renderer = null;

            return new Cell
            {
                Engine = Name, Track = track, Workload = workload, InCrossStack = true,
                Render = () =>
                {
                    if (view == null)
                    {
                        renderer = Renderer(track);
                        view = renderer.CompileView(workload);
                    }
                    return Render(renderer, view, model);
                },
            };
        }

        /// <summary>
        /// One render. The per-render allocations here — the writer, the view data and the view
        /// context — are what an MVC caller genuinely pays per request; the host, the view engine
        /// and the compiled view are setup and are hoisted out. Blurring that line is what made the
        /// old full-page Razor row unfair in the other direction.
        /// </summary>
        private static string Render<TModel>(RazorSupport.ViewRenderer renderer, IView view, TModel model)
        {
            using var output = new StringWriter();
            var viewData = new ViewDataDictionary<TModel>(
                new EmptyModelMetadataProvider(), new ModelStateDictionary())
            {
                Model = model,
            };
            var actionContext = renderer.ActionContext();
            var tempData = new TempDataDictionary(
                actionContext.HttpContext,
                actionContext.HttpContext.RequestServices.GetRequiredService<ITempDataProvider>());

            var viewContext = new ViewContext(
                actionContext, view, viewData, tempData, output, new HtmlHelperOptions());

            // MVC's rendering pipeline is async-only. The view is fully in memory and does no I/O,
            // so this completes synchronously in practice; GetAwaiter().GetResult() rethrows
            // without the AggregateException wrapper .Wait() would add.
            view.RenderAsync(viewContext).GetAwaiter().GetResult();
            output.Flush();
            return output.ToString();
        }
    }
}
