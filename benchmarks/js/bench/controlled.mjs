// Controlled-track bench script — Phase 4 WI6 (spec: README D10–D12, harness-and-run.md
// §mitata run shape). Invocation (canonical flags, D11):
//   node --expose-gc --allow-natives-syntax bench/controlled.mjs      (npm run bench:controlled)
// Shape: in-process controlled byte gate over all 16 cells BEFORE any bench() registration
// (failure exits 1 before run() — no numbers exist for a failed gate, D10) → one group per
// workload with `handlebars`/`eta` benches, bodies `() => do_not_optimize(render())` → a
// SINGLE run() → artifacts/controlled.txt + artifacts/controlled.json (two views of the same
// samples) → DEOPT-CHECK trailer over the in-process capture buffer (D12).
import { tracks } from "../src/engines/index.mjs";
import {
  assertControlledGate,
  registerTrackGroups,
  runAndCapture,
  writeArtifacts,
  deoptCheckTrailer,
} from "./_shared.mjs";

const renderers = tracks.controlled; // per-track render table: renderers.<engine>[id] -> () => string

assertControlledGate(renderers); // throws/exits 1 → no numbers (contract v2 rule 2, D10)

registerTrackGroups("controlled", renderers);

const result = await runAndCapture(); // default 'mitata' format → stdout + in-process buffer
writeArtifacts("controlled", result); // JSON: result.benchmarks, BigInt→string replacer
deoptCheckTrailer(); // scans the in-process capture buffer for '!' → DEOPT-CHECK line
