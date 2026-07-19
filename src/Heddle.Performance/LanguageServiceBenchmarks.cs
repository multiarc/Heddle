using System.Reflection;
using BenchmarkDotNet.Attributes;
using Heddle.Data;
using Heddle.Runtime;

namespace Heddle.Performance;

/// <summary>
/// The phase 6 null-cost guard: compiles a home-page-sized template with <see cref="TemplateOptions.ProvideLanguageFeatures"/>
/// off (must equal the pre-phase baseline — the scope map is never created, one null check per body compile) and
/// on (the documented budget: within 10% mean / 5% allocated of flag-off). <c>[MemoryDiagnoser]</c> so allocated
/// bytes are compared, not just time.
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
    public void Setup()
    {
        HeddleTemplate.Configure(typeof(LanguageServiceBenchmarks).GetTypeInfo().Assembly);
    }

    [Benchmark(Baseline = true)]
    public bool CompileFlagOff()
    {
        var options = new TemplateOptions { ProvideLanguageFeatures = false };
        var template = new HeddleTemplate(HomePage, new CompileContext(options, typeof(object)));
        return template.CompileResult != null;
    }

    [Benchmark]
    public bool CompileFlagOn()
    {
        var options = new TemplateOptions { ProvideLanguageFeatures = true };
        var template = new HeddleTemplate(HomePage, new CompileContext(options, typeof(object)));
        return template.CompileResult != null;
    }
}
