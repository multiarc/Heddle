using BenchmarkDotNet.Attributes;

namespace Heddle.Benchmarks.Dotnet.Bench
{
    // The eight cross-stack suites. Each is the same six rows over a different protocol workload,
    // and each is what one `--filter *<Suite>*` invocation of `bench-crossstack` runs — the per-
    // workload granularity the master runners log, resume and copy artifacts per step.
    //
    // The class names ARE the workload ids in Pascal case, which is how the consolidation tool maps
    // an artifact back to a workload, and matches the JVM harness's class convention. Renaming one
    // means renaming its row in benchmarks/report/consolidate.py.
    //
    // Heddle is the ratio baseline everywhere: every other engine's number is reported relative to
    // it, so the baseline has to be the engine under test rather than whichever twin sorts first.

    /// <summary>Workload 1 — composed-page: layout composition, the largest raw output.</summary>
    public class ComposedPageBenchmarks : CrossStackSuite
    {
        protected override string Workload => "composed-page";

        [Benchmark(Baseline = true)] public string RenderHeddle() => Render("Heddle");
        [Benchmark] public int RenderHeddleUtf8() => RenderHeddleUtf8Sink();
        [Benchmark] public int RenderHeddleTextWriter() => RenderHeddleTextWriterSink();
        [Benchmark] public string RenderFluid() => Render("Fluid");
        [Benchmark] public string RenderScriban() => Render("Scriban");
        [Benchmark] public string RenderDotLiquid() => Render("DotLiquid");
        [Benchmark] public string RenderHandlebars() => Render("Handlebars");
        [Benchmark] public string RenderRazor() => Render("Razor");
    }

    /// <summary>Workload 2 — trivial-substitution: the floor, where fixed overhead dominates.</summary>
    public class TrivialSubstitutionBenchmarks : CrossStackSuite
    {
        protected override string Workload => "trivial-substitution";

        [Benchmark(Baseline = true)] public string RenderHeddle() => Render("Heddle");
        [Benchmark] public int RenderHeddleUtf8() => RenderHeddleUtf8Sink();
        [Benchmark] public int RenderHeddleTextWriter() => RenderHeddleTextWriterSink();
        [Benchmark] public string RenderFluid() => Render("Fluid");
        [Benchmark] public string RenderScriban() => Render("Scriban");
        [Benchmark] public string RenderDotLiquid() => Render("DotLiquid");
        [Benchmark] public string RenderHandlebars() => Render("Handlebars");
        [Benchmark] public string RenderRazor() => Render("Razor");
    }

    /// <summary>Workload 3 — large-loop: 5,000 iterations, loop-dispatch cost.</summary>
    public class LargeLoopBenchmarks : CrossStackSuite
    {
        protected override string Workload => "large-loop";

        [Benchmark(Baseline = true)] public string RenderHeddle() => Render("Heddle");
        [Benchmark] public int RenderHeddleUtf8() => RenderHeddleUtf8Sink();
        [Benchmark] public int RenderHeddleTextWriter() => RenderHeddleTextWriterSink();
        [Benchmark] public string RenderFluid() => Render("Fluid");
        [Benchmark] public string RenderScriban() => Render("Scriban");
        [Benchmark] public string RenderDotLiquid() => Render("DotLiquid");
        [Benchmark] public string RenderHandlebars() => Render("Handlebars");
        [Benchmark] public string RenderRazor() => Render("Razor");
    }

    /// <summary>Workload 4 — mixed-page: a realistic page, the closest thing to a typical result.</summary>
    public class MixedPageBenchmarks : CrossStackSuite
    {
        protected override string Workload => "mixed-page";

        [Benchmark(Baseline = true)] public string RenderHeddle() => Render("Heddle");
        [Benchmark] public int RenderHeddleUtf8() => RenderHeddleUtf8Sink();
        [Benchmark] public int RenderHeddleTextWriter() => RenderHeddleTextWriterSink();
        [Benchmark] public string RenderFluid() => Render("Fluid");
        [Benchmark] public string RenderScriban() => Render("Scriban");
        [Benchmark] public string RenderDotLiquid() => Render("DotLiquid");
        [Benchmark] public string RenderHandlebars() => Render("Handlebars");
        [Benchmark] public string RenderRazor() => Render("Razor");
    }

    /// <summary>Workload 5 — conditional-heavy: branch density rather than output volume.</summary>
    public class ConditionalHeavyBenchmarks : CrossStackSuite
    {
        protected override string Workload => "conditional-heavy";

        [Benchmark(Baseline = true)] public string RenderHeddle() => Render("Heddle");
        [Benchmark] public int RenderHeddleUtf8() => RenderHeddleUtf8Sink();
        [Benchmark] public int RenderHeddleTextWriter() => RenderHeddleTextWriterSink();
        [Benchmark] public string RenderFluid() => Render("Fluid");
        [Benchmark] public string RenderScriban() => Render("Scriban");
        [Benchmark] public string RenderDotLiquid() => Render("DotLiquid");
        [Benchmark] public string RenderHandlebars() => Render("Handlebars");
        [Benchmark] public string RenderRazor() => Render("Razor");
    }

    /// <summary>Workload 6 — fragment-heavy: partial/definition invocation cost.</summary>
    public class FragmentHeavyBenchmarks : CrossStackSuite
    {
        protected override string Workload => "fragment-heavy";

        [Benchmark(Baseline = true)] public string RenderHeddle() => Render("Heddle");
        [Benchmark] public int RenderHeddleUtf8() => RenderHeddleUtf8Sink();
        [Benchmark] public int RenderHeddleTextWriter() => RenderHeddleTextWriterSink();
        [Benchmark] public string RenderFluid() => Render("Fluid");
        [Benchmark] public string RenderScriban() => Render("Scriban");
        [Benchmark] public string RenderDotLiquid() => Render("DotLiquid");
        [Benchmark] public string RenderHandlebars() => Render("Handlebars");
        [Benchmark] public string RenderRazor() => Render("Razor");
    }

    /// <summary>Workload 7 — fortunes-encoded: encoding ON over small, adversarial data.</summary>
    public class FortunesEncodedBenchmarks : CrossStackSuite
    {
        protected override string Workload => "fortunes-encoded";

        [Benchmark(Baseline = true)] public string RenderHeddle() => Render("Heddle");
        [Benchmark] public int RenderHeddleUtf8() => RenderHeddleUtf8Sink();
        [Benchmark] public int RenderHeddleTextWriter() => RenderHeddleTextWriterSink();
        [Benchmark] public string RenderFluid() => Render("Fluid");
        [Benchmark] public string RenderScriban() => Render("Scriban");
        [Benchmark] public string RenderDotLiquid() => Render("DotLiquid");
        [Benchmark] public string RenderHandlebars() => Render("Handlebars");
        [Benchmark] public string RenderRazor() => Render("Razor");
    }

    /// <summary>Workload 8 — encoded-loop: encoding ON at volume, the escaper's throughput.</summary>
    public class EncodedLoopBenchmarks : CrossStackSuite
    {
        protected override string Workload => "encoded-loop";

        [Benchmark(Baseline = true)] public string RenderHeddle() => Render("Heddle");
        [Benchmark] public int RenderHeddleUtf8() => RenderHeddleUtf8Sink();
        [Benchmark] public int RenderHeddleTextWriter() => RenderHeddleTextWriterSink();
        [Benchmark] public string RenderFluid() => Render("Fluid");
        [Benchmark] public string RenderScriban() => Render("Scriban");
        [Benchmark] public string RenderDotLiquid() => Render("DotLiquid");
        [Benchmark] public string RenderHandlebars() => Render("Handlebars");
        [Benchmark] public string RenderRazor() => Render("Razor");
    }
}
