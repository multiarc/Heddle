using System;
using System.Linq;
using Heddle.Benchmarks.Dotnet.Bench;
using Heddle.Benchmarks.Dotnet.Corpus;
using Heddle.Benchmarks.Dotnet.Engines;
using Heddle.Benchmarks.Dotnet.Gate;

namespace Heddle.Benchmarks.Dotnet
{
    /// <summary>
    /// Verb-dispatching entry point for the .NET cross-stack harness (ledger E8), mirroring the
    /// shape the other five ecosystems expose: a gate that must pass before anything is timed, and
    /// bench targets that refuse to run behind a red gate.
    ///
    ///   gate              every registered cell: byte gate (controlled), verifier (idiomatic),
    ///                     security floor (encoded), materialisation check. Exit 1 on any failure.
    ///   verify-corpus     re-prove corpus freshness and recalibrate the verifier.
    ///   export-corpus     regenerate the corpus from the Heddle oracles.
    ///   selftest          harness self-checks, including the six-technique differential.
    ///   bench-crossstack  the leg run-all drives: one row per engine, both tracks, per workload.
    ///   bench-techniques  Heddle's six render techniques against each other. NOT in run-all.
    ///   bench-internal    Heddle-internal suites (props, branching, language-service metadata).
    ///   bench-cold        cold parse/compile sidebar.
    ///
    /// Every bench verb passes its remaining arguments straight to BenchmarkDotNet, so a master
    /// runner selects one suite per step and layers the measurement budget on top:
    ///
    ///   dotnet run -c Release -- bench-crossstack --filter *MixedPageBenchmarks* --warmupCount 7
    /// </summary>
    public static class Program
    {
        public static int Main(string[] args)
        {
            var verb = args.Length > 0 ? args[0] : "gate";
            var rest = args.Skip(1).ToArray();

            try
            {
                return verb.ToLowerInvariant() switch
                {
                    "gate" => RunGate(),
                    "verify-corpus" => CorpusMaintenance.Verify(),
                    "export-corpus" => CorpusMaintenance.Export(
                        rest.Any(a => string.Equals(a, "--allow-dirty", StringComparison.OrdinalIgnoreCase))),
                    "selftest" => SelfTest.Run(),
                    "bench-crossstack" => BenchRunner.Run("bench-crossstack", BenchRunner.CrossStackTypes, rest),
                    "bench-techniques" => BenchRunner.Run("bench-techniques", BenchRunner.TechniqueTypes, rest),
                    "bench-internal" => BenchRunner.Run("bench-internal", BenchRunner.InternalTypes, rest),
                    "bench-cold" => BenchRunner.Run("bench-cold", BenchRunner.ColdTypes, rest),
                    "--help" or "-h" or "help" => Usage(0),
                    _ => Usage(2, $"unknown verb '{verb}'"),
                };
            }
            catch (CorpusException ex)
            {
                Console.Error.WriteLine("corpus: " + ex.Message);
                return 1;
            }
        }

        /// <summary>
        /// The gate. Walks the registry, byte-gating controlled cells and verifying idiomatic ones,
        /// and reports every failure rather than stopping at the first — a drifted template usually
        /// breaks several cells, and fixing them one re-run at a time is the slow path.
        /// </summary>
        private static int RunGate()
        {
            var entries = GoldenCorpus.VerifyAll();
            Console.WriteLine($"corpus: {entries} entries verified (SHA-256 + byte length) at {GoldenCorpus.Directory()}");

            var cells = Registry.All;
            if (cells.Count == 0)
            {
                // Loud rather than a vacuous pass: an empty registry means the engine modules did
                // not register, and "0 failed" would read as success.
                Console.Error.WriteLine(
                    "gate: no cells registered — the engine modules did not contribute anything. " +
                    "Refusing to report a pass over an empty set.");
                return 1;
            }

            var passed = 0;
            var failed = 0;
            foreach (var cell in cells)
            {
                try
                {
                    var output = cell.Render();
                    if (cell.Track == "controlled") Controlled.AssertCell(cell.Engine, cell.Workload, output);
                    else Verifier.AssertCell(cell.Engine, cell.Workload, output);
                    Console.WriteLine($"[PASS] {cell}");
                    passed++;
                }
                catch (GateFailure ex)
                {
                    Console.Error.WriteLine(ex.Message);
                    failed++;
                }
                catch (Exception ex)
                {
                    // A render that throws is a gate failure like any other. Letting it escape
                    // would abort the run and hide every cell after it, which is exactly when the
                    // report is most needed.
                    Console.Error.WriteLine($"[FAIL] {cell}: {ex.GetType().Name}: {ex.Message.Split('\n')[0]}");
                    failed++;
                }
            }

            // Precompiled coverage, stated rather than assumed. The generator silently leaves
            // templates its emitter does not cover un-precompiled, and the engine then serves them
            // from the dynamic path -- so a harness that assumed coverage would report the runtime
            // backend under the precompiled name.
            var covered = Engines.Precompiled.CoveredWorkloads().ToList();
            var uncovered = Engines.Precompiled.UncoveredWorkloads().ToList();
            Console.WriteLine($"\nPRECOMPILED-COVERAGE: {covered.Count}/{covered.Count + uncovered.Count} workloads " +
                              $"({string.Join(", ", covered)})");
            if (uncovered.Count > 0)
                Console.WriteLine($"  not precompiled, rendered by the runtime backend: {string.Join(", ", uncovered)}");

            // The de-optimization guard. Every streaming sink must have handled every unit of
            // output; see Gate/Materialisation.cs for why counting is not enough.
            var materialisation = SelfTest.MaterialisationTrailer();
            Console.WriteLine(materialisation.Line);

            // Razor holds an MVC host with runtime-compilation file watchers; without this the
            // process can hang after the gate finishes.
            Engines.RazorEngine.Shutdown();

            Console.WriteLine($"\ngate: {passed} passed, {failed} failed (of {cells.Count} cells).");
            return failed == 0 && materialisation.Clean ? 0 : 1;
        }

        private static int Usage(int code, string message = null)
        {
            if (message != null) Console.Error.WriteLine("dotnet-benchmarks: " + message);
            var w = code == 0 ? Console.Out : Console.Error;
            w.WriteLine("usage: dotnet run -c Release -- <verb> [args]");
            w.WriteLine("  gate                 every registered cell; nothing may be timed behind a red gate");
            w.WriteLine("  selftest             the gate's own checks, incl. the six-technique differential");
            w.WriteLine("  verify-corpus        corpus freshness + verifier calibration");
            w.WriteLine("  export-corpus [--allow-dirty]   regenerate the corpus from the Heddle oracles");
            w.WriteLine("  bench-crossstack     the sweep: 8 suites x 6 engines x 2 tracks");
            w.WriteLine("  bench-techniques     Heddle's six render techniques against each other");
            w.WriteLine("  bench-cold           cold parse/compile, per engine");
            w.WriteLine("  bench-internal       props, branching, language-service metadata");
            w.WriteLine("");
            w.WriteLine("Every bench verb forwards its remaining args to BenchmarkDotNet, e.g.");
            w.WriteLine("  dotnet run -c Release -- bench-crossstack --filter *MixedPageBenchmarks* --job Dry");
            return code;
        }
    }
}
