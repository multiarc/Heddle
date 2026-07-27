using System.Collections.Generic;
using System.Reflection;
using BenchmarkDotNet.Attributes;
using Heddle;
using Heddle.Data;
using Heddle.Native;
using Heddle.Runtime;

namespace Heddle.Performance;

/// <summary>
/// Phase 5 (D7) allocation pins: prop-less definitions keep the pre-phase render path (allocation parity),
/// all-constant call sites are zero extra allocation (the shared frozen array), dynamic call sites pay the
/// documented per-invocation clone, and a parameterized slot renders the caller body per projection. Run with
/// <c>[MemoryDiagnoser]</c>; the mandatory zero-allocation proof also lives as a unit test
/// (<c>PropsConcurrencyTests.AllConstantBindIsZeroAllocation</c>).
/// </summary>
[MemoryDiagnoser]
public class PropsRenderBenchmarks
{
    public class Article
    {
        public string Title { get; set; }
        public string Summary { get; set; }
    }

    public class Option
    {
        public int Id { get; set; }
        public string Label { get; set; }
    }

    public class Menu
    {
        public IEnumerable<Option> Options { get; set; }
        public string Style { get; set; }
    }

    private HeddleTemplate _noProps;
    private HeddleTemplate _allConstant;
    private HeddleTemplate _dynamic;
    private HeddleTemplate _slot;

    private Article _article;
    private Menu _menu;

    [GlobalSetup]
    public void Setup()
    {
        AssemblyHelper.Configure(typeof(PropsRenderBenchmarks).GetTypeInfo().Assembly);

        _noProps = new HeddleTemplate(
            "@% <card>{{<article><h2>@(Title)</h2><p>@(Summary)</p></article>}} :: Heddle.Performance.PropsRenderBenchmarks.Article %@\n@card(this)",
            new CompileContext(new TemplateOptions(), typeof(Article)));

        _allConstant = new HeddleTemplate(
            "@% <card(style: string = \"plain\")>{{<article class=\"@(style)\"><h2>@(Title)</h2><p>@(Summary)</p></article>}} :: Heddle.Performance.PropsRenderBenchmarks.Article %@\n@card(this)",
            new CompileContext(new TemplateOptions(), typeof(Article)));

        _dynamic = new HeddleTemplate(
            "@% <card(style: string = \"plain\")>{{<article class=\"@(style)\"><h2>@(Title)</h2><p>@(Summary)</p></article>}} :: Heddle.Performance.PropsRenderBenchmarks.Article %@\n@card(this, style: Title)",
            new CompileContext(new TemplateOptions(), typeof(Article)));

        _slot = new HeddleTemplate(
            "@% <picker(out:: Heddle.Performance.PropsRenderBenchmarks.Option)>{{<ul>@list(Options){{<li>@out(this)</li>}}</ul>}} :: Heddle.Performance.PropsRenderBenchmarks.Menu %@\n@picker(this){{<a>@(Id):@(Label)</a>}}",
            new CompileContext(new TemplateOptions(), typeof(Menu)));

        _article = new Article { Title = "Hello", Summary = "World" };
        _menu = new Menu
        {
            Options = new List<Option>
            {
                new Option { Id = 1, Label = "A" },
                new Option { Id = 2, Label = "B" },
                new Option { Id = 3, Label = "C" }
            }
        };
    }

    [Benchmark(Baseline = true)]
    public string RenderDefinitionNoProps()
    {
        string last = null;
        for (int i = 0; i < 10_000; i++)
            last = _noProps.Generate(_article);
        return last;
    }

    [Benchmark]
    public string RenderAllConstantProps10K()
    {
        string last = null;
        for (int i = 0; i < 10_000; i++)
            last = _allConstant.Generate(_article);
        return last;
    }

    [Benchmark]
    public string RenderDynamicProps10K()
    {
        string last = null;
        for (int i = 0; i < 10_000; i++)
            last = _dynamic.Generate(_article);
        return last;
    }

    [Benchmark]
    public string RenderParameterizedSlot10K()
    {
        string last = null;
        for (int i = 0; i < 10_000; i++)
            last = _slot.Generate(_menu);
        return last;
    }
}
