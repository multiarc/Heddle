using System;
using System.IO;
using BenchmarkDotNet.Attributes;
using Heddle.Data;
using Heddle.Language;
using Heddle.Performance.Runners;
using Heddle.Runtime;

namespace Heddle.Performance
{
    /// <summary>
    /// The ALL(*) prediction-cost guard for phase 1: the new CALL-mode tokens and the fourth <c>call</c>
    /// alternative change parse prediction for every template, so this benchmark parses the existing TestSuite
    /// corpus via <see cref="DocumentParser.Parse"/>. Acceptance: allocated bytes not increased and mean time
    /// within BenchmarkDotNet's reported error, measured before/after the grammar change on the same machine.
    /// </summary>
    [MemoryDiagnoser]
    public class TemplateParseBenchmarks
    {
        private string _home;
        private string _layout;
        private string _rootPath;

        [GlobalSetup]
        public void Setup()
        {
            var homePath = Locate("home.heddle");
            _rootPath = Path.GetDirectoryName(homePath);
            _home = File.ReadAllText(homePath);
            _layout = File.ReadAllText(Locate("layout.heddle"));
        }

        // BenchmarkDotNet runs each benchmark from a nested working directory, so probe upward from the
        // assembly location for the copied-to-output TestTemplates corpus.
        private static string Locate(string fileName)
        {
            var dir = AppContext.BaseDirectory;
            for (int i = 0; i < 8 && dir != null; i++)
            {
                var candidate = Path.Combine(dir, "TestTemplates", fileName);
                if (File.Exists(candidate))
                    return candidate;
                dir = Path.GetDirectoryName(dir.TrimEnd(Path.DirectorySeparatorChar));
            }

            return Path.Combine("TestTemplates", fileName);
        }

        // D1-R4: cold parse/compile cost, one benchmark per engine over the same two composed
        // templates (layout + home). The render suite (TextRenderBenchmarks) already measures the
        // cached-render path where each engine reuses its parsed/compiled template.
        [Benchmark(Baseline = true)]
        public void ParseHeddle()
        {
            DocumentParser.Parse(_home, new CompileContext(new TemplateOptions { RootPath = _rootPath }), out _);
            DocumentParser.Parse(_layout, new CompileContext(new TemplateOptions { RootPath = _rootPath }), out _);
        }

        [Benchmark]
        public void ParseFluid() => FluidTest.ColdParse();

        [Benchmark]
        public void ParseScriban() => ScribanTest.ColdParse();

        [Benchmark]
        public void ParseDotLiquid() => DotLiquidTest.ColdParse();

        [Benchmark]
        public void ParseHandlebars() => HandlebarsTest.ColdParse();
    }
}
