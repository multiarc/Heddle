using System.Collections.Generic;
using System.Linq;
using BenchmarkDotNet.Attributes;
using Heddle.Data;
using Heddle.Runtime;

namespace Heddle.Benchmarks.Dotnet.Bench
{
    // The Heddle-INTERNAL suites: Heddle measured against itself, never against another engine.
    //
    // They exist to pin engine properties that the cross-stack sweep cannot see — the allocation
    // shape of a props call site, the cost of a branch that never publishes, the price of leaving
    // language-service metadata switched on. Nothing here is a competitor comparison, so nothing
    // here belongs in a cross-stack table; the consolidation tool surfaces them in the .NET sidebar
    // and excludes them from every ranking.
    //
    // Measured per operation rather than over an internal repeat loop. BenchmarkDotNet's
    // MemoryDiagnoser reports allocated bytes per op directly, so a loop only multiplies the number
    // it is meant to expose and makes a regression harder, not easier, to read.

    /// <summary>
    /// Props allocation pins. Prop-less definitions keep the pre-props render path; an all-constant
    /// call site allocates nothing extra because the binding array is shared and frozen; a dynamic
    /// call site pays one documented clone per invocation; a parameterized slot renders the caller's
    /// body once per projection.
    /// </summary>
    [MemoryDiagnoser]
    public class PropsBenchmarks
    {
        private const string ArticleType = "Heddle.Benchmarks.Dotnet.Bench.PropsArticle";
        private const string MenuType = "Heddle.Benchmarks.Dotnet.Bench.PropsMenu";
        private const string OptionType = "Heddle.Benchmarks.Dotnet.Bench.PropsOption";

        private HeddleTemplate _noProps;
        private HeddleTemplate _allConstant;
        private HeddleTemplate _dynamic;
        private HeddleTemplate _slot;

        private PropsArticle _article;
        private PropsMenu _menu;

        [GlobalSetup]
        public void Setup()
        {
            // Makes this assembly's types resolvable from a `::` clause. The public Configure entry
            // point is deliberate: the harness is not a friend assembly of the engine it measures.
            HeddleTemplate.Configure(typeof(PropsBenchmarks).Assembly);

            _noProps = new HeddleTemplate(
                "@% <card>{{<article><h2>@(Title)</h2><p>@(Summary)</p></article>}} :: " + ArticleType + " %@\n@card(this)",
                new CompileContext(new TemplateOptions(), typeof(PropsArticle)));

            _allConstant = new HeddleTemplate(
                "@% <card(style: string = \"plain\")>{{<article class=\"@(style)\"><h2>@(Title)</h2><p>@(Summary)</p></article>}} :: "
                + ArticleType + " %@\n@card(this)",
                new CompileContext(new TemplateOptions(), typeof(PropsArticle)));

            _dynamic = new HeddleTemplate(
                "@% <card(style: string = \"plain\")>{{<article class=\"@(style)\"><h2>@(Title)</h2><p>@(Summary)</p></article>}} :: "
                + ArticleType + " %@\n@card(this, style: Title)",
                new CompileContext(new TemplateOptions(), typeof(PropsArticle)));

            _slot = new HeddleTemplate(
                "@% <picker(out:: " + OptionType + ")>{{<ul>@list(Options){{<li>@out(this)</li>}}</ul>}} :: "
                + MenuType + " %@\n@picker(this){{<a>@(Id):@(Label)</a>}}",
                new CompileContext(new TemplateOptions(), typeof(PropsMenu)));

            _article = new PropsArticle { Title = "Hello", Summary = "World" };
            _menu = new PropsMenu
            {
                Options = new List<PropsOption>
                {
                    new PropsOption { Id = 1, Label = "A" },
                    new PropsOption { Id = 2, Label = "B" },
                    new PropsOption { Id = 3, Label = "C" },
                },
            };
        }

        [Benchmark(Baseline = true)]
        public string DefinitionNoProps() => _noProps.Generate(_article);

        [Benchmark]
        public string AllConstantProps() => _allConstant.Generate(_article);

        [Benchmark]
        public string DynamicProps() => _dynamic.Generate(_article);

        [Benchmark]
        public string ParameterizedSlot() => _slot.Generate(_menu);
    }

    /// <summary>
    /// Branching cost. A list with no channel participant must show no frame allocation over the
    /// pre-branch path; an <c>@if</c>/<c>@ifnot</c> pair is the same, because the opportunistic
    /// no-op keeps it off the channel; <c>@if</c>/<c>@else</c> is the documented worst case, paying
    /// one scope per iteration; and a realistic document that never publishes a branch pays nothing.
    /// </summary>
    [MemoryDiagnoser]
    public class BranchBenchmarks
    {
        private HeddleTemplate _noBranches;
        private HeddleTemplate _ifPair;
        private HeddleTemplate _ifElse;
        private HeddleTemplate _flagship;
        private BranchListModel _list;
        private BranchFlagModel _flags;

        [GlobalSetup]
        public void Setup()
        {
            HeddleTemplate.Configure(typeof(BranchBenchmarks).Assembly);

            _noBranches = new HeddleTemplate("@list(Items){{@(Name)}}", new CompileContext(typeof(BranchListModel)));
            _ifPair = new HeddleTemplate("@list(Items){{@if(F){{@(Name)}}@ifnot(F){{-}}}}",
                new CompileContext(typeof(BranchListModel)));
            _ifElse = new HeddleTemplate("@list(Items){{@if(F){{@(Name)}}@else(){{-}}}}",
                new CompileContext(typeof(BranchListModel)));
            _flagship = new HeddleTemplate(
                "<div>@if(IsFeatured){{<b>F</b>}}@ifnot(IsFeatured){{@if(IsArchived){{<i>A</i>}}@ifnot(IsArchived){{R}}}}</div>",
                new CompileContext(typeof(BranchFlagModel)));

            _list = new BranchListModel
            {
                Items = Enumerable.Range(0, 10_000)
                    .Select(i => new BranchCell { F = i % 2 == 0, Name = "n" + i })
                    .ToList(),
            };
            _flags = new BranchFlagModel { IsFeatured = false, IsArchived = true };
        }

        [Benchmark(Baseline = true)]
        public string ListNoBranches() => _noBranches.Generate(_list);

        [Benchmark]
        public string ListIfPair() => _ifPair.Generate(_list);

        [Benchmark]
        public string ListIfElse() => _ifElse.Generate(_list);

        [Benchmark]
        public string FlagshipNeverPublishes() => _flagship.Generate(_flags);
    }

    /// <summary>
    /// The null-cost guard for language-service metadata. Compiling with
    /// <see cref="TemplateOptions.ProvideLanguageFeatures"/> off must cost what it cost before the
    /// feature existed — the scope map is never built, and one null check per body compile is the
    /// whole overhead. Switching it on has a documented budget rather than a promise of free.
    /// </summary>
    [MemoryDiagnoser]
    public class LanguageServiceBenchmarks
    {
        private const string HomePage = """
            @model(){{object}}
            <html><head><title>Heddle</title></head><body>
              <h1>@(Title)</h1>
              @if(HasItems){{<ul>@list(Items){{<li>@(Name): @(Value)</li>}}</ul>}}
              @for(3){{<span>row @out()</span>}}
              <footer>@(Year)</footer>
            </body></html>
            """;

        [GlobalSetup]
        public void Setup() => HeddleTemplate.Configure(typeof(LanguageServiceBenchmarks).Assembly);

        [Benchmark(Baseline = true)]
        public bool CompileFlagOff()
        {
            var options = new TemplateOptions { ProvideLanguageFeatures = false };
            return new HeddleTemplate(HomePage, new CompileContext(options, typeof(object))).CompileResult != null;
        }

        [Benchmark]
        public bool CompileFlagOn()
        {
            var options = new TemplateOptions { ProvideLanguageFeatures = true };
            return new HeddleTemplate(HomePage, new CompileContext(options, typeof(object))).CompileResult != null;
        }
    }
}
