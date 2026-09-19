using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using Heddle;
using Heddle.Data;
using Heddle.Precompiled;
using Heddle.Runtime;

// A strict host binds its functions at build: the Tool reads this declaration out of the
// intermediate assembly, so the @shout call site is bound at build and never late-bound.
[assembly: Heddle.Attributes.ExportFunctions(typeof(Heddle.Samples.PrecompiledAot.SampleFunctions))]

namespace Heddle.Samples.PrecompiledAot
{
    public static class SampleFunctions
    {
        public static string Shout(string value) =>
            value == null ? null : value.ToUpperInvariant();
    }

    public sealed class ComposedPageModel
    {
        public string Title { get; set; }
        public List<string> Links { get; set; }
    }

    public sealed class LargeLoopModel
    {
        public List<int> Items { get; set; }
        public int Count { get; set; }
    }

    public sealed class MixedPageModel
    {
        public string Heading { get; set; }
        public bool ShowBanner { get; set; }
        public decimal Subtotal { get; set; }
    }

    public sealed class ConditionalHeavyModel
    {
        public bool IsFeatured { get; set; }
        public bool IsArchived { get; set; }
    }

    public sealed class FragmentHeavyModel
    {
        public string Title { get; set; }
        public string Tagline { get; set; }
        public List<string> Notes { get; set; }
        public string Footer { get; set; }
    }

    public sealed class FortuneModel
    {
        public List<string> Fortunes { get; set; }
    }

    public sealed class EncodedLoopModel
    {
        public List<string> Entries { get; set; }
    }

    internal static class Program
    {
        private sealed class Workload
        {
            public string Key;
            public string File;
            // The typeof assigned here is what roots each model through the trimmed publish: the registry's
            // gauntlet walks the model's members by reflection, so they must survive trimming.
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
            public Type ModelType;
            public Func<object> NewModel;
            public Func<object, string> TypedRender;
        }

        private static int Main(string[] args)
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            PrecompiledTemplates.Register(typeof(Program).Assembly);
            HeddleTemplate.Configure(typeof(Program).Assembly);

            // The same container the build sees through [ExportFunctions]: until the build host binds
            // same-project exports itself (today it records HED7031 "late-bound functions: shout" and
            // defers the site), the request-side registry is what renders it — build declaration and
            // runtime registration agree function for function.
            var functions = new Heddle.Runtime.Expressions.FunctionRegistry();
            functions.RegisterFrom(typeof(Program).Assembly);
            PrecompiledTemplates.DefaultOptions = new TemplateOptions("precompiled-aot")
            {
                Functions = functions,
            };

            var report = PrecompiledTemplates.ValidateAll(PrecompiledTemplates.DefaultOptions);
            if (report.Failures.Count != 0)
                throw new InvalidOperationException(
                    "Precompiled validation failed: " + report.Failures[0].Reason + ": " +
                    report.Failures[0].Detail + ".");

            var workloads = new[]
            {
                new Workload
                {
                    Key = "templates/composed-page.heddle", File = "composed-page.txt",
                    ModelType = typeof(ComposedPageModel),
                    NewModel = () => new ComposedPageModel
                        { Title = "Hello", Links = new List<string> { "docs", "samples" } },
                    TypedRender = model => global::Heddle.Generated.Templates_Composed_page.Generate(
                        (ComposedPageModel)model),
                },
                new Workload
                {
                    Key = "templates/large-loop.heddle", File = "large-loop.txt",
                    ModelType = typeof(LargeLoopModel),
                    NewModel = () => new LargeLoopModel
                        { Items = new List<int> { 1, 2, 3, 4, 5 }, Count = 5 },
                    TypedRender = model => global::Heddle.Generated.Templates_Large_loop.Generate(
                        (LargeLoopModel)model),
                },
                new Workload
                {
                    Key = "templates/mixed-page.heddle", File = "mixed-page.txt",
                    ModelType = typeof(MixedPageModel),
                    NewModel = () => new MixedPageModel
                        { Heading = "Mixed", ShowBanner = true, Subtotal = 21.25m },
                    TypedRender = model => global::Heddle.Generated.Templates_Mixed_page.Generate(
                        (MixedPageModel)model),
                },
                new Workload
                {
                    Key = "templates/conditional-heavy.heddle", File = "conditional-heavy.txt",
                    ModelType = typeof(ConditionalHeavyModel),
                    NewModel = () => new ConditionalHeavyModel { IsFeatured = false, IsArchived = true },
                    TypedRender = model => global::Heddle.Generated.Templates_Conditional_heavy.Generate(
                        (ConditionalHeavyModel)model),
                },
                new Workload
                {
                    Key = "templates/fragment-heavy.heddle", File = "fragment-heavy.txt",
                    ModelType = typeof(FragmentHeavyModel),
                    NewModel = () => new FragmentHeavyModel
                    {
                        Title = "Fragments", Tagline = "Many small pieces",
                        Notes = new List<string> { "one", "two" }, Footer = "End",
                    },
                    TypedRender = model => global::Heddle.Generated.Templates_Fragment_heavy.Generate(
                        (FragmentHeavyModel)model),
                },
                new Workload
                {
                    Key = "templates/fortunes-encoded.heddle", File = "fortunes-encoded.txt",
                    ModelType = typeof(FortuneModel),
                    NewModel = () => new FortuneModel
                        { Fortunes = new List<string> { "a", "b" } },
                    TypedRender = model => global::Heddle.Generated.Templates_Fortunes_encoded.Generate(
                        (FortuneModel)model),
                },
                new Workload
                {
                    Key = "templates/encoded-loop.heddle", File = "encoded-loop.txt",
                    ModelType = typeof(EncodedLoopModel),
                    NewModel = () => new EncodedLoopModel
                        { Entries = new List<string> { "x", "y", "z" } },
                    TypedRender = model => global::Heddle.Generated.Templates_Encoded_loop.Generate(
                        (EncodedLoopModel)model),
                },
            };

            // Each workload renders three ways that must agree byte for byte: the build-time typed
            // wrapper, the registry under strict load, and a dynamically-compiled twin.
            var outputs = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var workload in workloads)
            {
                var model = workload.NewModel();
                var typed = workload.TypedRender(model);
                var viaRegistry = PrecompiledTemplates
                    .BindTyped(typeof(Program).Assembly, workload.Key, workload.ModelType)
                    .Generate(model);

                var source = System.IO.File.ReadAllText(
                    System.IO.Path.Combine(Heddle.Samples.SampleCapture.SampleRoot(),
                        "templates", workload.File.Replace(".txt", ".heddle")));
                string dynamic;
                var twinOptions = new TemplateOptions("precompiled-aot-twin") { Functions = functions };
                using (var twin = new HeddleTemplate(source,
                           new CompileContext(twinOptions, workload.ModelType)))
                {
                    if (!twin.CompileResult.Success)
                        throw new InvalidOperationException(
                            "dynamic twin failed for " + workload.Key + ": " + twin.CompileResult + ".");
                    dynamic = twin.Generate(model);
                }

                if (!string.Equals(typed, viaRegistry, StringComparison.Ordinal) ||
                    !string.Equals(typed, dynamic, StringComparison.Ordinal))
                    throw new InvalidOperationException(
                        "DIFFERENTIAL FAILED for " + workload.Key +
                        $": typed={typed.Length} registry={viaRegistry.Length} dynamic={dynamic.Length}.");
                outputs[workload.File] = typed;
            }

            // The AOT claim, asserted in capture: a host rendering only printed templates loads no
            // Microsoft.CodeAnalysis type (Heddle.CSharpTierEnabled=false trims the whole tier).
            var assemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetName().Name)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToList();
            var roslyn = assemblies
                .Where(n => n.StartsWith("Microsoft.CodeAnalysis", StringComparison.Ordinal))
                .ToList();
            if (roslyn.Count != 0)
                throw new InvalidOperationException(
                    "AOT CLAIM FAILED: rendering loaded " + string.Join(", ", roslyn) + ".");

            // The captured list is the host, the engine and its parser runtime, nothing else. Which other
            // assemblies a run reports is a statement about the runtime, not the host: the JIT lists its
            // dynamic-methods assembly and loads BCL and helper assemblies lazily, while a NativeAOT
            // binary lists what the compiler kept and nothing it trimmed. The same golden must hold for
            // the JIT run CI makes and for the published native binary. The assertion above still runs
            // over the full list.
            var captured = assemblies
                .Where(n => string.Equals(n, "PrecompiledAot", StringComparison.Ordinal) ||
                            n.StartsWith("Heddle", StringComparison.Ordinal) ||
                            n.StartsWith("Antlr4", StringComparison.Ordinal))
                .ToList();

            var capture = Heddle.Samples.SampleCapture.Resolve(args);
            if (capture != null)
            {
                foreach (var pair in outputs.OrderBy(p => p.Key, StringComparer.Ordinal))
                    Heddle.Samples.SampleCapture.Write(capture, pair.Key, pair.Value);
                Heddle.Samples.SampleCapture.Write(capture, "assemblies.txt",
                    string.Join("\n", captured) + "\n");
                Console.WriteLine("captured " + outputs.Count + " workload outputs + assemblies.txt " +
                    "(all three tiers held byte-identical; no Microsoft.CodeAnalysis loaded)");
                return 0;
            }

            foreach (var pair in outputs.OrderBy(p => p.Key, StringComparer.Ordinal))
                Console.WriteLine("=== " + pair.Key + " ===\n" + pair.Value);
            Console.WriteLine("differential: typed == registry == dynamic twin (identical, " +
                workloads.Length + "/" + workloads.Length + " workloads)");
            Console.WriteLine("assemblies loaded: " + assemblies.Count + ", none Microsoft.CodeAnalysis");
            return 0;
        }
    }
}
