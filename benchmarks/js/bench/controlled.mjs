// Controlled-track bench script. Invocation (canonical flags):
//   node --expose-gc --allow-natives-syntax bench/controlled.mjs      (npm run bench:controlled)
// Shape: in-process controlled byte gate over all 16 cells BEFORE any bench() registration
// (failure exits 1 before run() — no numbers exist for a failed gate) → one group per
// workload with `handlebars`/`eta` benches, bodies
// `() => do_not_optimize(flatten(render()))` so every rendered rope is materialised → a
// SINGLE run() → artifacts/controlled.txt + artifacts/controlled.json (two views of the same
// samples) → DEOPT-CHECK trailer over the in-process capture buffer → its
// MATERIALISATION-CHECK companion, which exits 1 if any cell's implied output throughput is
// physically impossible.
import { tracks } from "../src/engines/index.mjs";
import {
  assertControlledGate,
  registerTrackGroups,
  runAndCapture,
  writeArtifacts,
  deoptCheckTrailer,
  materialisationCheckTrailer,
} from "./_shared.mjs";

const renderers = tracks.controlled; // per-track render table: renderers.<engine>[id] -> () => string

assertControlledGate(renderers); // throws/exits 1 → no numbers exist for a failed gate

registerTrackGroups("controlled", renderers);

const result = await runAndCapture(); // default 'mitata' format → stdout + in-process buffer
writeArtifacts("controlled", result); // JSON: result.benchmarks, BigInt→string replacer
deoptCheckTrailer(); // scans the in-process capture buffer for '!' → DEOPT-CHECK line
materialisationCheckTrailer(result); // implied throughput vs the physical ceiling; fatal
