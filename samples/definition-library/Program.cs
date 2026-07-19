using System;
using System.Globalization;
using System.IO;
using Heddle;
using Heddle.Data;
using Heddle.Runtime;

namespace Heddle.Samples.DefinitionLibrary
{
    // Sample 2 — composition imports. A page template pulls definitions in from a library file via @<<, overrides
    // one locally (document-order override), and narrows an abstract definition to a concrete type through
    // <name:name> inheritance. Templates are loaded from files (the resolver path this exercises).
    public sealed class Article
    {
        public string Title { get; set; }
        public string Body { get; set; }
    }

    internal static class Program
    {
        private static int Main(string[] args)
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            HeddleTemplate.Configure(typeof(Program).Assembly);

            // RootPath resolves @<<{{library.heddle}} and the page file, relative to the sample's own templates/ dir.
            var templates = Path.Combine(SampleCapture.SampleRoot(), "templates");
            var options = new TemplateOptions("page")
            {
                RootPath = templates + Path.DirectorySeparatorChar,
                FileNamePostfix = ".heddle"
            };

            using var t = new HeddleTemplate(new CompileContext(options));
            if (!t.CompileResult.Success)
                throw new InvalidOperationException("compile failed: " + t.CompileResult);

            var output = t.Generate(new Article
            {
                Title = "Native Expressions Ship",
                Body = "The sandbox-safe expression tier is here."
            });

            var capture = SampleCapture.Resolve(args);
            if (capture != null)
            {
                SampleCapture.Write(capture, "page.html", output);
                Console.WriteLine("captured page.html");
                return 0;
            }

            Console.WriteLine(output);
            return 0;
        }
    }
}
