using System;
using System.Globalization;
using System.IO;
using BenchmarkDotNet.Attributes;
using Fluid.Benchmarks;
using Heddle;
using Heddle.Data;
using Heddle.Runtime;
using Heddle.Runtime.Expressions;

namespace Heddle.Performance.ThirdParty
{
    /// <summary>
    /// The Heddle entry (D3-R1) for the upstream Fluid product-render scenario. It slots into the
    /// vendored harness by deriving <see cref="BaseBenchmarks"/> exactly like the competitor entries
    /// (<see cref="FluidBenchmarks"/>, <see cref="ScribanBenchmarks"/>, <see cref="DotLiquidBenchmarks"/>,
    /// <see cref="HandlebarsBenchmarks"/>): same 100-<see cref="Product"/> model, same
    /// <see cref="BaseBenchmarks.CheckBenchmark"/> output oracle, same Parse/Render/ParseAndRender shape.
    ///
    /// <para>Idiomatic Heddle: the per-row markup is a <em>definition</em> (<c>&lt;product_row&gt;</c> — the
    /// Heddle analogue of a Liquid/Handlebars partial) invoked once per element by <c>@list(Products)</c>
    /// (the Heddle analogue of <c>{% for %}</c> / <c>{{#each}}</c>). The template runs under
    /// <see cref="ExpressionMode.Native"/> with a curated <see cref="FunctionRegistry"/> whose one entry,
    /// <c>truncate</c>, mirrors the Liquid <c>truncate</c> filter — the sandbox-safe tier, and the direct
    /// counterpart of the other engines' filter/helper.</para>
    ///
    /// Zero methodology change: only the template dialect and the (statically typed) model shape differ.
    /// See <c>ThirdParty/README.md</c> for the feature-mapping table and the upstream commit.
    /// </summary>
    [MemoryDiagnoser]
    public class HeddleBenchmarks : BaseBenchmarks
    {
        private static readonly string ProductTemplateHeddle;
        private static readonly FunctionRegistry Functions;

        private readonly HeddleProductModel _model;
        private readonly HeddleTemplate _heddleTemplate;

        static HeddleBenchmarks()
        {
            // Determinism: built-ins are invariant by design; pin thread culture so float prices render
            // "0".."99" regardless of the host locale (golden discipline, matching the other engines).
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

            // Register this assembly so the engine resolves the model/element types named in the template.
            HeddleTemplate.Configure(typeof(HeddleBenchmarks).Assembly);

            var assembly = typeof(HeddleBenchmarks).Assembly;
            using (var stream = assembly.GetManifestResourceStream("Fluid.Benchmarks.product.heddle"))
            using (var streamReader = new StreamReader(stream))
            {
                ProductTemplateHeddle = streamReader.ReadToEnd();
            }

            // The whole trust boundary for a Native-mode template: exactly one host function, `truncate`.
            Functions = new FunctionRegistry();
            Functions.Register("truncate",
                typeof(HeddleFilters).GetMethod(nameof(HeddleFilters.Truncate)));
        }

        public HeddleBenchmarks()
        {
            _model = new HeddleProductModel(Products);
            _heddleTemplate = Compile(ProductTemplateHeddle);

            CheckBenchmark();
        }

        private static HeddleTemplate Compile(string template)
        {
            var options = new TemplateOptions
            {
                ExpressionMode = ExpressionMode.Native,   // the sandbox-safe tier — no C# escape
                Functions = Functions,
            };

            var compiled = new HeddleTemplate(template, new CompileContext(options, typeof(HeddleProductModel)));
            if (!compiled.CompileResult.Success)
            {
                throw new InvalidOperationException(
                    "Heddle product template failed to compile: " +
                    string.Join("; ", compiled.CompileResult.ErrorList));
            }

            return compiled;
        }

        [Benchmark]
        public override object Parse()
        {
            return Compile(ProductTemplateHeddle);
        }

        // Parity with HandlebarsBenchmarks, which likewise does not carry the "big" (blog post) template;
        // Heddle is excluded from the ParseBig category in HeddleComparisonBenchmarks for the same reason.
        public override object ParseBig()
        {
            throw new NotSupportedException();
        }

        [Benchmark]
        public override string Render()
        {
            return _heddleTemplate.Generate(_model);
        }

        [Benchmark]
        public override string ParseAndRender()
        {
            return Compile(ProductTemplateHeddle).Generate(_model);
        }
    }
}
