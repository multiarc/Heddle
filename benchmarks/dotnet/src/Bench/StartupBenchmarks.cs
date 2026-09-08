using System;
using System.Collections.Generic;
using System.Diagnostics;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Jobs;
using Heddle.Benchmarks.Dotnet.Corpus;
using Heddle.Benchmarks.Dotnet.Engines;
using Heddle.Benchmarks.Dotnet.Gate;
using Heddle.Data;
using Heddle.Precompiled;
using Heddle.Runtime;

namespace Heddle.Benchmarks.Dotnet.Bench
{
    /// <summary>
    /// The cold-start row (P3-R8): what a fresh process pays before its first render. Per-process facts
    /// are measured per process — <see cref="RunStrategy.ColdStart"/> with 20 launches, one invocation
    /// per launch — so warm JIT numbers never stand in for startup.
    ///
    /// <para><b>Not comparable across machines, runs, or with warm figures.</b> These numbers get their
    /// own table labelled per <c>benchmarks/docs/metrics-protocol.md</c>, never a column beside warm
    /// render figures. The artifact path is compared only against <see cref="CompileHeddle"/> in the
    /// same run, and must sit strictly below it.</para>
    /// </summary>
    [MemoryDiagnoser]
    [SimpleJob(RunStrategy.ColdStart, launchCount: 20, invocationCount: 1)]
    public class StartupBenchmarks
    {
        // composed-page is the cold subject: the only workload composing two sources (a layout and the
        // page that extends it), so it exercises import resolution as well as parsing.
        private const string Workload = "composed-page";

        private string _heddleRoot;

        [GlobalSetup]
        public void Setup()
        {
            _heddleRoot = System.IO.Path.Combine(Templates.Root(), "controlled", "heddle");
            // Gate before timing: the string sink is asserted against the corpus once per process.
            var output = HeddleEngine.Render("controlled", Workload, HeddleEngine.Sink.String);
            Controlled.AssertCell(HeddleEngine.Name, Workload, output);
        }

        /// <summary>The whole first-use cost a Heddle caller actually pays: parse, bind and build the
        /// render tree for composed-page, imports and all.</summary>
        [Benchmark(Baseline = true)]
        public int CompileHeddle()
        {
            var options = new TemplateOptions("composed-page")
            {
                FileNamePostfix = ".heddle",
                RootPath = _heddleRoot,
                OutputProfile = OutputProfile.Text,
                ExpressionMode = ExpressionMode.Native,
                ProvideLanguageFeatures = false,
            };
            var template = new HeddleTemplate(new CompileContext(options));
            return template.Generate(HeddleEngine.ModelFor(Workload)).Length;
        }

        /// <summary>The artifact path, table on: register, bind the typed entry, first render.</summary>
        [Benchmark]
        public int RegisterAndRenderCompiledForm()
        {
            AppContext.SetSwitch(TechniqueSetup.UseGeneratedSitesSwitch, true);
            return RegisterAndRender().Length;
        }

        /// <summary>The artifact path with the site table off: the data-only twin.</summary>
        [Benchmark]
        public int RegisterAndRenderCompiledFormDataOnly()
        {
            AppContext.SetSwitch(TechniqueSetup.UseGeneratedSitesSwitch, false);
            try
            {
                return RegisterAndRender().Length;
            }
            finally
            {
                AppContext.SetSwitch(TechniqueSetup.UseGeneratedSitesSwitch, true);
            }
        }

        /// <summary>The gate verb's materialisation trailer (P3-R8): artifact size in bytes and
        /// materialization time per workload. Each workload binds fresh — never through a shared
        /// cache — so the time is register+bind+first render, not a cache hit.</summary>
        public static void WriteGateTrailer(IReadOnlyList<string> coveredWorkloads)
        {
            long artifactBytes = -1;
            var assembly = typeof(Engines.Precompiled).Assembly;
            using (var stream = assembly.GetManifestResourceStream("Heddle.CompiledForm"))
                if (stream != null)
                    artifactBytes = stream.Length;
            Console.WriteLine(artifactBytes >= 0
                ? $"PRECOMPILED-ARTIFACT: {artifactBytes} bytes (embedded Heddle.CompiledForm image)"
                : "PRECOMPILED-ARTIFACT: unknown (no embedded Heddle.CompiledForm image in " +
                  assembly.GetName().Name + ")");
            var saved = PrecompiledTemplates.DefaultOptions;
            try
            {
                PrecompiledTemplates.Register(assembly);
                foreach (var workload in coveredWorkloads)
                {
                    PrecompiledTemplateInfo entry = null;
                    foreach (var candidate in PrecompiledTemplates.Entries)
                        if (string.Equals(candidate.Key, workload + ".heddle", StringComparison.Ordinal))
                            entry = candidate;
                    if (entry == null)
                    {
                        Console.WriteLine($"MATERIALISATION: {workload}: no entry");
                        continue;
                    }
                    PrecompiledTemplates.DefaultOptions = new TemplateOptions("benchmarks-trailer")
                    {
                        OutputProfile = entry.OptionsFingerprint.Profile,
                        ExpressionMode = entry.OptionsFingerprint.ExpressionMode,
                    };
                    var model = HeddleEngine.ModelFor(workload);
                    var watch = Stopwatch.StartNew();
                    var bound = PrecompiledTemplates.BindTyped(assembly, workload + ".heddle", entry.ModelType);
                    var output = bound.Generate(model);
                    watch.Stop();
                    Console.WriteLine($"MATERIALISATION: {workload}: " +
                        $"{watch.Elapsed.TotalMilliseconds:F1} ms, {output.Length} chars (register+bind+first render)");
                }
            }
            finally
            {
                PrecompiledTemplates.DefaultOptions = saved;
            }
        }

        private static string RegisterAndRender()
        {
            var assembly = typeof(Engines.Precompiled).Assembly;
            PrecompiledTemplates.Register(assembly);
            PrecompiledTemplateInfo entry = null;
            foreach (var candidate in PrecompiledTemplates.Entries)
                if (string.Equals(candidate.Key, Workload + ".heddle", StringComparison.Ordinal))
                    entry = candidate;
            if (entry == null)
                throw new InvalidOperationException(
                    $"no precompiled entry for '{Workload}' — the Heddle.Build tier should have compiled it.");
            PrecompiledTemplates.DefaultOptions = new TemplateOptions("benchmarks-startup")
            {
                OutputProfile = entry.OptionsFingerprint.Profile,
                ExpressionMode = entry.OptionsFingerprint.ExpressionMode,
            };
            var bound = PrecompiledTemplates.BindTyped(assembly, Workload + ".heddle", entry.ModelType);
            return bound.Generate(HeddleEngine.ModelFor(Workload));
        }
    }
}
