using System;
using System.Globalization;
using System.Linq;
using System.Text;
using Heddle;
using Heddle.Data;
using Heddle.Precompiled;
using Heddle.Runtime;

namespace Heddle.Samples.Precompiled
{
    public sealed class Invoice
    {
        public int Number { get; set; }
        public string Customer { get; set; }
        public decimal Amount { get; set; }
    }

    internal static class Program
    {
        private static int Main(string[] args)
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            PrecompiledTemplates.Register(typeof(Program).Assembly);
            HeddleTemplate.Configure(typeof(Program).Assembly);

            var model = new Invoice { Number = 1042, Customer = "Ada Lovelace", Amount = 129.50m };

            // Precompiled path: the build-time typed entry point (zero parse, zero runtime compile).
            var precompiled = global::Heddle.Generated.Templates_Invoice.Generate(model);

            // Differential: render the SAME template + data through a dynamically-compiled twin and require equality.
            var invoiceSource = System.IO.File.ReadAllText(
                System.IO.Path.Combine(SampleCapture.SampleRoot(), "templates", "invoice.heddle"));
            string dynamic;
            using (var twin = new HeddleTemplate(invoiceSource, new CompileContext(new TemplateOptions(), typeof(Invoice))))
            {
                if (!twin.CompileResult.Success)
                    throw new InvalidOperationException("dynamic twin failed: " + twin.CompileResult);
                dynamic = twin.Generate(model);
            }

            if (!string.Equals(precompiled, dynamic, StringComparison.Ordinal))
                throw new InvalidOperationException("DIFFERENTIAL FAILED: precompiled output != dynamic twin output.");

            // Discovery: the public registry enumeration (ordered).
            var discovery = new StringBuilder();
            foreach (var entry in PrecompiledTemplates.Entries.OrderBy(e => e.Key, StringComparer.Ordinal))
                discovery.Append(entry.Key).Append("  model=").Append(entry.ModelType?.Name ?? "(none)")
                    .Append("  precompiled=").Append(entry.IsPrecompiled).Append('\n');

            var capture = SampleCapture.Resolve(args);
            if (capture != null)
            {
                SampleCapture.Write(capture, "precompiled-output.html", precompiled);
                SampleCapture.Write(capture, "discovery.txt", discovery.ToString());
                SampleCapture.Write(capture, "differential.txt",
                    $"identical\nbytes={Encoding.UTF8.GetByteCount(precompiled)}\n");
                Console.WriteLine("captured precompiled-output.html, discovery.txt, differential.txt (differential held)");
                return 0;
            }

            Console.WriteLine("=== precompiled output ===\n" + precompiled);
            Console.WriteLine("=== discovery ===\n" + discovery);
            Console.WriteLine("differential: precompiled == dynamic twin (identical)");
            return 0;
        }
    }
}
