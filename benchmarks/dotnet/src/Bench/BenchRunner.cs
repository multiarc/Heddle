using System;
using System.Collections.Generic;
using System.Linq;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;

namespace Heddle.Benchmarks.Dotnet.Bench
{
    /// <summary>
    /// Turns a bench verb into a BenchmarkDotNet run over a fixed, named set of types.
    ///
    /// <para><b>Why a fixed set rather than the whole assembly.</b> A switcher over everything would
    /// let a stray <c>[Benchmark]</c> anywhere in the harness silently join the cross-stack sweep,
    /// and the sweep's row set is normative — it is what the report tables and the consolidation
    /// tool are built from. Naming the types per verb makes that set reviewable, and makes
    /// "internal suites are not competitor rows" a property of the code rather than of a
    /// convention.</para>
    ///
    /// <para><b>Remaining arguments pass straight through to BenchmarkDotNet</b>, which is how the
    /// master runners select one suite per step (<c>--filter *MixedPageBenchmarks*</c>) and layer
    /// the measurement budget (<c>--warmupCount 7 --iterationCount 15</c>) or a smoke shape
    /// (<c>--job Dry</c>) on top. With no filter given the verb runs every type it owns: a switcher
    /// that stopped to ask would hang a runner step forever.</para>
    /// </summary>
    public static class BenchRunner
    {
        /// <summary>The eight cross-stack suites, in protocol workload order.</summary>
        public static Type[] CrossStackTypes => new[]
        {
            typeof(ComposedPageBenchmarks),
            typeof(TrivialSubstitutionBenchmarks),
            typeof(LargeLoopBenchmarks),
            typeof(MixedPageBenchmarks),
            typeof(ConditionalHeavyBenchmarks),
            typeof(FragmentHeavyBenchmarks),
            typeof(FortunesEncodedBenchmarks),
            typeof(EncodedLoopBenchmarks),
        };

        public static Type[] TechniqueTypes => new[]
        {
            typeof(TechniqueRuntimeBenchmarks),
            typeof(TechniquePrecompiledBenchmarks),
        };

        public static Type[] InternalTypes => new[]
        {
            typeof(PropsBenchmarks),
            typeof(BranchBenchmarks),
            typeof(LanguageServiceBenchmarks),
        };

        public static Type[] ColdTypes => new[] { typeof(ColdCompileBenchmarks) };

        /// <summary>Runs one verb's types. Returns the process exit code.</summary>
        public static int Run(string verb, Type[] types, string[] args)
        {
            var effective = Has(args, "--filter") ? args : args.Concat(new[] { "--filter", "*" }).ToArray();

            Console.WriteLine($"{verb}: {types.Length} suite(s) — {string.Join(", ", types.Select(t => t.Name))}");
            Console.WriteLine($"{verb}: BenchmarkDotNet args: {string.Join(" ", effective)}");

            var summaries = BenchmarkSwitcher.FromTypes(types).Run(effective, Config(args)).ToList();
            return Verdict(verb, summaries);
        }

        /// <summary>
        /// The default job, supplied as configuration rather than as a <c>[ShortRunJob]</c> attribute
        /// on every suite. Both spellings give a bare invocation the ~10-minute-per-ecosystem short
        /// measurement budget, but an attribute cannot be replaced from the command line — BenchmarkDotNet
        /// ADDS the CLI job to it — so <c>--job Dry</c> would have produced a smoke pass that ran the
        /// full measurement as well as the dry one, which is the opposite of a smoke pass. Supplying
        /// it here means a caller that names a job gets exactly that job.
        /// </summary>
        private static IConfig Config(string[] args)
            => Has(args, "--job") ? null : ManualConfig.Create(DefaultConfig.Instance).AddJob(Job.ShortRun);

        private static bool Has(IEnumerable<string> args, string option)
            => args.Any(a => string.Equals(a, option, StringComparison.OrdinalIgnoreCase)
                          || a.StartsWith(option + "=", StringComparison.OrdinalIgnoreCase));

        /// <summary>
        /// A non-zero exit on anything that would leave the artifacts incomplete. BenchmarkDotNet
        /// reports a failed benchmark in its console output and then exits 0, which in a master
        /// runner reads as a green step over a missing row — the exact failure mode the program's
        /// dropped-row guard exists to catch elsewhere.
        /// </summary>
        private static int Verdict(string verb, List<Summary> summaries)
        {
            if (summaries.Count == 0)
            {
                Console.Error.WriteLine($"{verb}: no benchmarks matched the filter — nothing was measured.");
                return 1;
            }

            var failed = 0;
            var measured = 0;
            foreach (var summary in summaries)
            {
                if (summary.HasCriticalValidationErrors)
                {
                    Console.Error.WriteLine($"{verb}: {summary.Title} had critical validation errors.");
                    failed++;
                    continue;
                }
                foreach (var report in summary.Reports)
                {
                    measured++;
                    if (report.Success) continue;
                    Console.Error.WriteLine($"{verb}: {report.BenchmarkCase.DisplayInfo} did not complete successfully.");
                    failed++;
                }
            }

            if (measured == 0)
            {
                Console.Error.WriteLine($"{verb}: every summary was empty — nothing was measured.");
                return 1;
            }

            Console.WriteLine($"\n{verb}: {measured - failed} of {measured} benchmark case(s) completed.");
            return failed == 0 ? 0 : 1;
        }
    }
}
