using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Templates.Data;
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
            /*JIT*/
            var test = new TtlTemplate(
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
            );
            if (!test.CompileResult.Success)
            {
                Console.Write(test.CompileResult.ToString());
            }
            else
            {
                test.Generate(null, new TestViewContext
                {
                    User = new ClaimsPrincipal()
                });
                test.Dispose();
            }

            /*END JIT*/
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
            var viewEngine = _serviceProvider.GetRequiredService<ICompositeViewEngine>();
            var result = viewEngine.GetView(null, "TestTemplates/home.cshtml", false);
            if (!result.Success)
            {
                result = viewEngine.FindView(actionContext, "TestTemplates/home.cshtml", false);
            }

            if (!result.Success)
            {
                Console.WriteLine("Can't find a view");
            }

            result.EnsureSuccessful(null);

            watcher.Stop();
            Console.WriteLine("Compile time: {0}", watcher.Elapsed);

            var data = new TestViewContext
            {
                User = new ClaimsPrincipal()
            };

            ViewDataDictionary viewData =
                new ViewDataDictionary(
                    new ViewDataDictionary(
                        new DefaultModelMetadataProvider(
                            actionContext.HttpContext.RequestServices
                                .GetRequiredService<ICompositeMetadataDetailsProvider>()),
                        actionContext.ModelState))
                {
                    Model = data
                };
            var tempDataFactory =
                actionContext.HttpContext.RequestServices.GetRequiredService<ITempDataDictionaryFactory>();

            watcher.Reset();

            long length;
            using (var writer = new StringWriter())
            {
                ViewContext viewContext = new ViewContext(actionContext, result.View, viewData,
                    tempDataFactory.GetTempData(actionContext.HttpContext), writer,
                    new HtmlHelperOptions());
                result.View.RenderAsync(viewContext).Wait();
                writer.Flush();
                length = writer.ToString().Length * n;
            }
            watcher.Start();
            Enumerable.Repeat(data, n)
                .AsParallel()
                .ForAll(item =>
                {
                    using (var writer = new StringWriter())
                    {
                        ViewContext viewContext = new ViewContext(actionContext, result.View, viewData,
                            tempDataFactory.GetTempData(actionContext.HttpContext), writer,
                            new HtmlHelperOptions());
                        result.View.RenderAsync(viewContext).Wait();
                        writer.Flush();
                        length = writer.ToString().Length * n;
                    }
                });
            watcher.Stop();
            Console.WriteLine("Parrallel implementation:");
            Console.WriteLine("{0} run times: {1}", n, watcher.Elapsed);
            Console.WriteLine("Out Speed: {0} Pages/s", n / watcher.Elapsed.TotalSeconds);
            Console.WriteLine("{0:F} Mb/s", length / watcher.Elapsed.TotalSeconds / 1048576.0 * sizeof(char));
            Console.WriteLine("Total Size: {0} Mb", length / 1048576.0 * sizeof(char));
            watcher.Reset();
            watcher.Start();
            for (var i = 0; i < n; i++)
            {
                using (var writer = new StringWriter())
                {
                    ViewContext viewContext = new ViewContext(actionContext, result.View, viewData,
                        tempDataFactory.GetTempData(actionContext.HttpContext), writer,
                        new HtmlHelperOptions());
                    result.View.RenderAsync(viewContext).Wait();
                    writer.Flush();
                    length = writer.ToString().Length * n;
                }
            }
            watcher.Stop();
            Console.WriteLine("Syncronous implementation:");
            Console.WriteLine("{0} run times: {1}", n, watcher.Elapsed);
            Console.WriteLine("Out Speed: {0} Pages/s", n / watcher.Elapsed.TotalSeconds);
            Console.WriteLine("{0:F} Mb/s, ticks:{1}", length / watcher.Elapsed.TotalSeconds / 1048576.0 * sizeof(char),
                watcher.ElapsedTicks);
            Console.WriteLine("Total Size: {0} Mb", length / 1048576.0 * sizeof(char));
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
                watcher.Stop();
                Console.WriteLine("Compile time: {0}", watcher.Elapsed);
                if (!target.CompileResult.Success)
                {
                    Console.Write(target.CompileResult.ToString());
                    return;
                }
                watcher.Reset();
                long length = target.Generate(data).Length * n;
                watcher.Start();
                Enumerable.Repeat(data, n).AsParallel().ForAll(item => target.Generate(item));
                watcher.Stop();
                Console.WriteLine("Parrallel implementation:");
                Console.WriteLine("{0} run times: {1}", n, watcher.Elapsed);
                Console.WriteLine("Out Speed: {0} Pages/s", n / watcher.Elapsed.TotalSeconds);
                Console.WriteLine("{0:F} Mb/s", length / watcher.Elapsed.TotalSeconds / 1048576.0 * sizeof(char));
                Console.WriteLine("Total Size: {0} Mb", length / 1048576.0 * sizeof(char));
                watcher.Reset();
                watcher.Start();
                for (var i = 0; i < n; i++)
                {
                    target.Generate(data);
                }
                watcher.Stop();
                Console.WriteLine("Syncronous implementation:");
                Console.WriteLine("{0} run times: {1}", n, watcher.Elapsed);
                Console.WriteLine("Out Speed: {0} Pages/s", n / watcher.Elapsed.TotalSeconds);
                Console.WriteLine("{0:F} Mb/s, ticks:{1}", length / watcher.Elapsed.TotalSeconds / 1048576.0 * sizeof(char),
                    watcher.ElapsedTicks);
                Console.WriteLine("Total Size: {0} Mb", length / 1048576.0 * sizeof(char));
            }
        }
    }

    public class TemplaterTest : IRunner
    {
        public TemplaterTest()
        {
            /*JIT*/
            var test = new TtlTemplate
            (new CompileContext
            (new TemplateOptions("template")
            {
                FileNamePostfix = ".ttl",
                RootPath = @"TestTemplates",
                AllowCSharp = true
            }));
            if (!test.CompileResult.Success)
            {
                Console.Write(test.CompileResult.ToString());
            }
            else
            {
                var testString = test.Generate(DataFiller.FillData());
                File.WriteAllText("test.html", testString);
                test.Dispose();
            }
            /*END JIT*/
        }

        public void Run()
        {
            //var list = new List<TestDataStructure>();
            Console.WriteLine("Enter tries count (template generate):");
            // = 1000;
            string quantity = Console.ReadLine();
            int n;
            int.TryParse(quantity, out n);
            var data = DataFiller.FillData();
            //for (int i = 0; i < n; i++)
            //    list.Add(data);
            var watcher = new Stopwatch();
            watcher.Start();
            using (var target = new TtlTemplate

            (new CompileContext
            (new TemplateOptions("template")
            {
                FileNamePostfix = ".ttl",
                RootPath = @"TestTemplates",
                AllowCSharp = true
            })))
            {
                watcher.Stop();
                Console.WriteLine("Compile time: {0}, ticks:{1}", watcher.Elapsed, watcher.ElapsedTicks);
                if (!target.CompileResult.Success)
                {
                    Console.Write(target.CompileResult.ToString());
                    return;
                }
                watcher.Reset();
                long length = target.Generate(data).Length * n;
                watcher.Start();
                Enumerable.Repeat(data, n).AsParallel().ForAll(item => target.Generate(item));
                watcher.Stop();
                Console.WriteLine("Parrallel implementation:");
                Console.WriteLine("{0} run times: {1}", n, watcher.Elapsed);
                Console.WriteLine("Out Speed: {0:F} Mb/s, ticks:{1}", length / watcher.Elapsed.TotalSeconds / 1048576.0 * sizeof(char),
                    watcher.ElapsedTicks);
                Console.WriteLine("Total Size: {0} Mb", length / 1048576.0 * sizeof(char));
                watcher.Reset();
                watcher.Start();
                for (var i = 0; i < n; i++)
                {
                    target.Generate(data);
                }
                watcher.Stop();
                Console.WriteLine("Syncronous implementation:");
                Console.WriteLine("{0} run times: {1}", n, watcher.Elapsed);
                Console.WriteLine("Out Speed: {0:F} Mb/s, ticks:{1}", length / watcher.Elapsed.TotalSeconds / 1048576.0 * sizeof(char),
                    watcher.ElapsedTicks);
                Console.WriteLine("Total Size: {0} Mb", length / 1048576.0 * sizeof(char));
            }
        }
    }
}