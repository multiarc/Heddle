using System;
using System.Threading.Tasks;
using BenchmarkDotNet.Running;
using Heddle.Performance.Runners;

namespace Heddle.Performance
{
    public class Program
    {
        public static void Main(string[] args) {
            // Fast, host-free D1 parity harness: `dotnet run -c Release -- parity`. Confirms every
            // competitor twin renders output identical to Heddle after the documented normalization
            // without spinning up the full BenchmarkDotNet run (which the render GlobalSetup also asserts).
            if (args != null && args.Length > 0 && string.Equals(args[0], "parity", StringComparison.OrdinalIgnoreCase))
            {
                var all = true;
                foreach (var report in new Func<(bool AllMatch, string Text)>[]
                {
                    ParityCheck.Report,
                    ParityCheck.ReportSubstitution,
                    ParityCheck.ReportLoop,
                    ParityCheck.ReportMixed,
                    ParityCheck.ReportConditional,
                    ParityCheck.ReportFragment,
                    ParityCheck.ReportFortunes,
                    ParityCheck.ReportEncodedLoop,
                })
                {
                    var (match, text) = report();
                    Console.Write(text);
                    all &= match;
                }
                Environment.Exit(all ? 0 : 1);
                return;
            }

            // Golden-corpus verbs (cross-stack phase 1 WI6): `export-corpus [--allow-dirty]`
            // writes the eight normalized oracles + verify.json definitions + manifest;
            // `verify-corpus` re-proves freshness (byte-exact vs the stored form + SHA-256 vs
            // the manifest) and calibrates the idiomatic verifier (accept golden, reject the
            // synthesized corruptions). See docs/spec/cross-stack-benchmarks/
            // phase-1-cross-stack-foundation/golden-corpus.md.
            if (args != null && args.Length > 0 && string.Equals(args[0], "export-corpus", StringComparison.OrdinalIgnoreCase))
            {
                var allowDirty = Array.Exists(args, a => string.Equals(a, "--allow-dirty", StringComparison.OrdinalIgnoreCase));
                Environment.Exit(GoldenCorpus.Export(allowDirty));
                return;
            }

            if (args != null && args.Length > 0 && string.Equals(args[0], "verify-corpus", StringComparison.OrdinalIgnoreCase))
            {
                Environment.Exit(GoldenCorpus.Verify());
                return;
            }

            // Switcher so either suite runs, e.g. `dotnet run -c Release -- --filter *TemplateParseBenchmarks*`.
            // With no args it prompts; the render head-to-head stays the default when a single type is selected.
            if (args != null && args.Length > 0)
            {
                BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
                return;
            }

            BenchmarkRunner.Run<TextRenderBenchmarks>();
            // var runner = new TextRenderBenchmarks();
            // await runner.Setup();
            // while (true) {
            //     await runner.RenderHeddle();
            // }
            //
            // await runner.Teardown();
        }
    }
}