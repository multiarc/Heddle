using System;
using System.Linq;
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
    ///   bench-crossstack  the leg run-all drives: one row per engine.
    ///   bench-techniques  Heddle's six render techniques against each other. NOT in run-all.
    ///   bench-internal    Heddle-internal suites (parse, props, branch, language service).
    ///   bench-cold        cold parse/compile sidebar.
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
                    "verify-corpus" => NotYetImplemented("verify-corpus", "W3 (needs the ported models)"),
                    "export-corpus" => NotYetImplemented("export-corpus", "W3 (needs the ported models)"),
                    "selftest" => SelfTest.Run(),
                    "bench-crossstack" => NotYetImplemented("bench-crossstack", "W9"),
                    "bench-techniques" => NotYetImplemented("bench-techniques", "W9"),
                    "bench-internal" => NotYetImplemented("bench-internal", "W8"),
                    "bench-cold" => NotYetImplemented("bench-cold", "W8"),
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
                    "gate: no cells registered — engine modules have not landed yet (W4-W7). " +
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

        private static int NotYetImplemented(string verb, string workItem)
        {
            Console.Error.WriteLine($"{verb}: not implemented yet — lands in {workItem}.");
            return 2;
        }

        private static int Usage(int code, string message = null)
        {
            if (message != null) Console.Error.WriteLine("dotnet-benchmarks: " + message);
            var w = code == 0 ? Console.Out : Console.Error;
            w.WriteLine("usage: dotnet run -c Release -- <verb>");
            w.WriteLine("  gate | verify-corpus | export-corpus | selftest");
            w.WriteLine("  bench-crossstack | bench-techniques | bench-internal | bench-cold");
            return code;
        }
    }
}
