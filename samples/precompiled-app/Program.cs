using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Heddle;
using Heddle.Data;
using Heddle.Precompiled;
using Heddle.Runtime;

// One declaration, both tiers. The typeof cannot be spelled without a reference, and this project has no
// ProjectReference on Acme.Models — the `@(HeddleModelAssembly)` item supplies it — so if that item ever stops
// reaching the compiler, this line is CS0246 before anything else can go quietly wrong. At run time
// HeddleTemplate.Configure reads the same attribute and registers the assembly, so the engine resolves the
// spelling `@model(){{Acme.Models.Ticket}}` regardless of what else the process happens to have touched.
[assembly: Heddle.Attributes.HeddleModelAssembly(typeof(Acme.Models.Ticket))]

namespace Heddle.Samples.Precompiled
{
    public sealed class Invoice
    {
        public int Number { get; set; }
        public string Customer { get; set; }
        public decimal Amount { get; set; }
    }

    /// <summary>
    /// `@(HeddleModelAssembly)` reaches the COMPILER and nothing else — that is the whole design, because it is what
    /// keeps build output a function of the compilation's declared inputs. It is not an ordinary reference, so it is
    /// absent from the deps file and the host runtime will not find `Acme.Models` on its own. Putting the file where
    /// the process can load it stays deployment's job, which is the obligation the item form carries and the
    /// attribute form does not: a project that takes an ordinary reference to its model assembly needs none of this.
    /// <para>In a module initializer rather than at the top of <c>Main</c> because the CLR resolves a method's types
    /// when it compiles the method — a load statement inside <c>Main</c> would run after <c>Main</c> had already
    /// failed to bind <c>Acme.Models.Ticket</c>.</para>
    /// </summary>
    internal static class ExternalModelAssembly
    {
        [ModuleInitializer]
        internal static void Load() =>
            Assembly.LoadFrom(System.IO.Path.Combine(AppContext.BaseDirectory, "Acme.Models.dll"));
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

            // The same two tiers over a model type that reaches the build only through @(HeddleModelAssembly) and
            // the run tier only through the assembly attribute. Both tiers configured by one declaration, and the
            // differential is what proves they agreed rather than that one of them quietly took over.
            var ticket = new Acme.Models.Ticket { Reference = "AC-7781", Passenger = "Grace Hopper", Seat = "12A" };
            var ticketPrecompiled = global::Heddle.Generated.Templates_Ticket.Generate(ticket);

            var ticketSource = System.IO.File.ReadAllText(
                System.IO.Path.Combine(SampleCapture.SampleRoot(), "templates", "ticket.heddle"));
            string ticketDynamic;
            using (var twin = new HeddleTemplate(ticketSource,
                       new CompileContext(new TemplateOptions(), typeof(Acme.Models.Ticket))))
            {
                if (!twin.CompileResult.Success)
                    throw new InvalidOperationException("dynamic ticket twin failed: " + twin.CompileResult);
                ticketDynamic = twin.Generate(ticket);
            }

            if (!string.Equals(ticketPrecompiled, ticketDynamic, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "DIFFERENTIAL FAILED: external-model precompiled output != dynamic twin output.");

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
                SampleCapture.Write(capture, "external-model-output.txt", ticketPrecompiled);
                Console.WriteLine(
                    "captured precompiled-output.html, discovery.txt, differential.txt, external-model-output.txt " +
                    "(both differentials held)");
                return 0;
            }

            Console.WriteLine("=== precompiled output ===\n" + precompiled);
            Console.WriteLine("=== external-model output ===\n" + ticketPrecompiled);
            Console.WriteLine("=== discovery ===\n" + discovery);
            Console.WriteLine("differential: precompiled == dynamic twin (identical)");
            return 0;
        }
    }
}
