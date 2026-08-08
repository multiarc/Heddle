using BenchmarkDotNet.Attributes;
using Fluid;
using Heddle.Benchmarks.Dotnet.Engines;
using Heddle.Language;
using Heddle.Runtime;
using DotLiquidTemplate = DotLiquid.Template;
using ScribanTemplate = Scriban.Template;
// Fluid also ships a TemplateOptions; aliasing Heddle's keeps both usable without qualifying
// every occurrence, and keeps the Fluid namespace imported for its Parse extension method.
using TemplateOptions = Heddle.Data.TemplateOptions;
using OutputProfile = Heddle.Data.OutputProfile;
using ExpressionMode = Heddle.Data.ExpressionMode;

namespace Heddle.Benchmarks.Dotnet.Bench
{
    /// <summary>
    /// The cold parse/compile sidebar: what each engine pays ONCE, before it can render anything.
    ///
    /// <para><b>A sidebar, never a cross-stack row.</b> Every render suite in this program measures
    /// the warm path — the template is parsed in setup and reused, which is what a server does. That
    /// makes the cold cost invisible, and it is a real cost for a process that renders a page a few
    /// times and exits. This suite states it separately rather than folding it into a number whose
    /// meaning would then depend on how often you render.</para>
    ///
    /// <para><b>The engines are not doing the same amount of work, and the rows say so.</b> Fluid,
    /// Scriban and DotLiquid expose a parse that produces an interpretable tree; Handlebars.Net
    /// compiles to a delegate; Heddle is measured both ways, parse alone and the full compile a
    /// caller actually triggers. Comparing across those columns is comparing different steps, so the
    /// method names are the disclosure: <c>Parse*</c> and <c>Compile*</c> are different questions.
    /// </para>
    ///
    /// <para><b>Razor is absent, deliberately.</b> Its view compilation runs through MVC's runtime
    /// compiler against a hosted file provider, invokes the C# compiler, and is dominated by host
    /// construction — it is not the same step as any row here, and including it would produce a
    /// number readers would inevitably compare.</para>
    /// </summary>
    [MemoryDiagnoser]
    public class ColdCompileBenchmarks
    {
        // composed-page is the cold subject for every engine: it is the only workload that composes
        // two sources (a layout and the page that extends it), so it exercises import resolution as
        // well as parsing. Its sources are read once here; file I/O is not what this measures.
        private string _heddleHome;
        private string _heddleLayout;
        private string _heddleRoot;
        private string _fluid;
        private string _scriban;
        private string _liquid;
        private string _handlebars;

        [GlobalSetup]
        public void Setup()
        {
            _heddleRoot = System.IO.Path.Combine(Templates.Root(), "controlled", "heddle");
            _heddleHome = Templates.Load("controlled", "heddle", "home.heddle");
            _heddleLayout = Templates.Load("controlled", "heddle", "layout.heddle");
            _fluid = Templates.Load("controlled", "liquid", "composed-page.liquid");
            _scriban = Templates.Load("controlled", "scriban", "composed-page.scriban");
            _liquid = Templates.Load("controlled", "liquid", "composed-page.liquid");
            _handlebars = Templates.Load("controlled", "handlebars", "composed-page.hbs");
        }

        /// <summary>Heddle's parse step alone, over both composed sources — the row comparable with
        /// the three parse-only engines below.</summary>
        [Benchmark(Baseline = true)]
        public void ParseHeddle()
        {
            var options = new TemplateOptions { RootPath = _heddleRoot };
            DocumentParser.Parse(_heddleHome, new CompileContext(options), out _);
            DocumentParser.Parse(_heddleLayout, new CompileContext(options), out _);
        }

        /// <summary>The whole first-use cost a Heddle caller actually pays: parse, bind and build the
        /// render tree for composed-page, imports and all.</summary>
        [Benchmark]
        public bool CompileHeddle()
        {
            var options = new TemplateOptions("home")
            {
                FileNamePostfix = ".heddle",
                RootPath = _heddleRoot,
                OutputProfile = OutputProfile.Text,
                ExpressionMode = ExpressionMode.Native,
                ProvideLanguageFeatures = false,
            };
            return new HeddleTemplate(new CompileContext(options)).CompileResult != null;
        }

        [Benchmark]
        public object ParseFluid() => new FluidParser().Parse(_fluid);

        [Benchmark]
        public object ParseScriban() => ScribanTemplate.Parse(_scriban);

        [Benchmark]
        public object ParseDotLiquid() => DotLiquidTemplate.Parse(_liquid);

        /// <summary>Handlebars.Net compiles to a delegate, so this row is a compile, not a parse.</summary>
        [Benchmark]
        public object CompileHandlebars() => HandlebarsDotNet.Handlebars.Create().Compile(_handlebars);
    }
}
