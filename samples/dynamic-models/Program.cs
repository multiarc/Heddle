using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using Heddle;
using Heddle.Data;
using Heddle.Runtime;

namespace Heddle.Samples.DynamicModels
{
    // Sample 3 — dynamic models + runtime-assembled template strings. A template compiled with `:: dynamic`
    // renders against an anonymous-object graph and again against a dictionary-shaped (ExpandoObject) graph,
    // showing that one dynamic template serves heterogeneous model shapes.
    internal static class Program
    {
        private static int Main(string[] args)
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            HeddleTemplate.Configure(typeof(Program).Assembly);

            // The template is assembled at runtime — the point of the dynamic tier: no compiled model type.
            var template = string.Join("\n",
                "@model(){{dynamic}}",
                "Order #@(Id) for @(Customer.Name)",
                "Items:",
                "@list(Items){{ - @(this)",
                "}}");

            // A statically-typed object graph accessed entirely through the dynamic tier (public POCOs: the C#
            // runtime binder cannot see another assembly's *anonymous* types, so the object graph uses named types
            // — the dynamic member access at render time is the point, not how the graph was declared).
            var typed = new Order
            {
                Id = 42,
                Customer = new Customer { Name = "Ada Lovelace" },
                Items = new[] { "quill", "ink", "vellum" }
            };

            dynamic dict = new ExpandoObject();
            dict.Id = 7;
            dynamic customer = new ExpandoObject();
            customer.Name = "Alan Turing";
            dict.Customer = customer;
            dict.Items = new List<object> { "chalk", "slate" };

            var anonOut = Render(template, typed);
            var dictOut = Render(template, dict);

            var capture = Heddle.Samples.SampleCapture.Resolve(args);
            if (capture != null)
            {
                Heddle.Samples.SampleCapture.Write(capture, "dynamic-anon.txt", anonOut);
                Heddle.Samples.SampleCapture.Write(capture, "dynamic-dictionary.txt", dictOut);
                Console.WriteLine("captured dynamic-anon.txt, dynamic-dictionary.txt");
                return 0;
            }

            Console.WriteLine("=== anonymous object graph ===");
            Console.WriteLine(anonOut);
            Console.WriteLine("=== dictionary-shaped (ExpandoObject) graph ===");
            Console.WriteLine(dictOut);
            return 0;
        }

        private static string Render(string template, object model)
        {
            using var t = new HeddleTemplate(template, new CompileContext(new TemplateOptions()));
            if (!t.CompileResult.Success)
                throw new InvalidOperationException("compile failed: " + t.CompileResult);
            return t.Generate(model);
        }
    }

    public sealed class Order
    {
        public int Id { get; set; }
        public Customer Customer { get; set; }
        public string[] Items { get; set; }
    }

    public sealed class Customer
    {
        public string Name { get; set; }
    }
}
