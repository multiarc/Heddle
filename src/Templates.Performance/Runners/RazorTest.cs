using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Templates.Data;
using Templates.Performance.TestSuite;
using Templates.Runtime;

namespace Templates.Performance.Runners
{
    public class RazorTest : IRunner
    {
        private readonly IServiceProvider _serviceProvider;

        private ActionContext GetActionContext()
        {
            var httpContext = new DefaultHttpContext {RequestServices = _serviceProvider};
            return new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        }

        public RazorTest(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;

            //JIT precompilation

            using (var target =
                new TtlTemplate(
                    new CompileContext(
                        new TemplateOptions("template")
                        {
                            FileNamePostfix = ".ttl",
                            RootPath = @"TestTemplates",
                            AllowCSharp = true
                        }
                    )
                )
            )
            {
                target.Generate(DataFiller.FillData());
            }

            var renderer = ServiceProviderServiceExtensions.GetRequiredService<RazorViewToStringRenderer>(_serviceProvider);

            var view = renderer.CompileView("jit");
            var viewData = new ViewDataDictionary(
                metadataProvider: new EmptyModelMetadataProvider(),
                modelState: new ModelStateDictionary())
            {
                Model = null
            };

            var htmlHelperOptions = new HtmlHelperOptions();

            var actionContext = GetActionContext();
            var tempDataProvider = ServiceProviderServiceExtensions.GetRequiredService<ITempDataProvider>(_serviceProvider);

            var tempData = new TempDataDictionary(
                actionContext.HttpContext,
                tempDataProvider);

            using (var output = new StringWriter())
            {
                var viewContext = new ViewContext(
                    actionContext,
                    view,
                    viewData, tempData,
                    output, htmlHelperOptions);

                view.RenderAsync(viewContext).Wait();
                output.Flush();
                // ReSharper disable once ReturnValueOfPureMethodIsNotUsed
                output.ToString();
            }
        }

        public void Run()
        {
            RunTemplateEngine();
            RunRazor();
        }

        private void RunRazor()
        {
            Console.WriteLine("Razor Tests:");

            Console.WriteLine("Enter tries count (template generate):");
            // = 1000;
            string quantity = Console.ReadLine();
            int n;
            int.TryParse(quantity, out n);

            var watcher = new Stopwatch();

            watcher.Start();

            var actionContext = GetActionContext();
            var renderer = ServiceProviderServiceExtensions.GetRequiredService<RazorViewToStringRenderer>(_serviceProvider);

            var data = new TestViewContext
            {
                User = new ClaimsPrincipal()
            };

            var view = renderer.CompileView("home");

            var tempDataProvider = ServiceProviderServiceExtensions.GetRequiredService<ITempDataProvider>(_serviceProvider);

            long length;
            var viewData = new ViewDataDictionary<TestViewContext>(
                metadataProvider: new EmptyModelMetadataProvider(),
                modelState: new ModelStateDictionary())
            {
                Model = data
            };

            var htmlHelperOptions = new HtmlHelperOptions();

            var tempData = new TempDataDictionary(
                actionContext.HttpContext,
                tempDataProvider);

            using (var output = new StringWriter())
            {
                var viewContext = new ViewContext(
                    actionContext,
                    view,
                    viewData, tempData,
                    output, htmlHelperOptions);

                view.RenderAsync(viewContext).Wait();
                output.Flush();
                var text = output.ToString();
                watcher.Stop();
                File.WriteAllText("razor.html", text);
                length = text.Length * (long)n;
            }

            Console.WriteLine("Compile time & first run: {0}", watcher.Elapsed);

            watcher.Reset();
            long entireLength = 0;
            watcher.Start();
            for (var i = 0; i < n; i++)
            {
                using (var output = new StringWriter())
                {
                    var viewContext = new ViewContext(
                        actionContext,
                        view,
                        viewData, tempData,
                        output, htmlHelperOptions);

                    view.RenderAsync(viewContext).Wait();
                    output.Flush();
                    entireLength += output.ToString().Length;
                }
            }
            watcher.Stop();
            Console.WriteLine(entireLength);
            Console.WriteLine("Single Thread:");
            Console.WriteLine("{0} run times: {1}", n, watcher.Elapsed);
            Console.WriteLine("Out Speed: {0:F0} Pages/s", n / watcher.Elapsed.TotalSeconds);
            Console.WriteLine("{0:F2} Mb/s", length / watcher.Elapsed.TotalSeconds / 1048576.0 * sizeof(char));
            Console.WriteLine("Total Size: {0:F2} Mb", length / 1048576.0 * sizeof(char));
        }

        private static void RunTemplateEngine()
        {
//var list = new List<TestDataStructure>();
            Console.WriteLine("Enter tries count (template generate):");
            // = 1000;
            string quantity = Console.ReadLine();
            int n;
            int.TryParse(quantity, out n);
            var data = new TestViewContext
            {
                User = new ClaimsPrincipal()
            };
            //for (int i = 0; i < n; i++)
            //    list.Add(data);
            var watcher = new Stopwatch();
            watcher.Start();
            using (var target = new TtlTemplate(
                new CompileContext(
                    new TemplateOptions("home")
                    {
                        FileNamePostfix = ".html",
                        RootPath = @"TestTemplates",
                        AllowCSharp = true,
                        ForceRemoveWhitespace = true,
                        ProvideLanguageFeatures = false
                    }
                )
            ))
            {
                long length = target.Generate(data).Length * (long)n;
                watcher.Stop();
                Console.WriteLine("Compile time & first run: {0}", watcher.Elapsed);
                if (!target.CompileResult.Success)
                {
                    Console.Write(target.CompileResult.ToString());
                    return;
                }
                watcher.Reset();
                //watcher.Start();

                //Enumerable.Repeat(data, n).AsParallel().ForAll(item => target.Generate(item));
                //watcher.Stop();
                //Console.WriteLine("Parrallel implementation:");
                //Console.WriteLine("{0} run times: {1}", n, watcher.Elapsed);
                //Console.WriteLine("Out Speed: {0:F0} Pages/s", n / watcher.Elapsed.TotalSeconds);
                //Console.WriteLine("{0:F2} Mb/s", length / watcher.Elapsed.TotalSeconds / 1048576.0 * sizeof(char));
                //Console.WriteLine("Total Size: {0:F2} Mb", length / 1048576.0 * sizeof(char));
                //watcher.Reset();
                watcher.Start();
                for (var i = 0; i < n; i++)
                {
                    target.Generate(data);
                }
                watcher.Stop();
                Console.WriteLine("Single thread implementation:");
                Console.WriteLine("{0} run times: {1}", n, watcher.Elapsed);
                Console.WriteLine("Out Speed: {0:F0} Pages/s", n / watcher.Elapsed.TotalSeconds);
                Console.WriteLine("{0:F2} Mb/s", length / watcher.Elapsed.TotalSeconds / 1048576.0 * sizeof(char));
                Console.WriteLine("Total Size: {0:F2} Mb", length / 1048576.0 * sizeof(char));
            }
        }
    }
}