// Idiomatic-track bench script — Phase 4 WI6 (spec: README D9–D12, harness-and-run.md
// §mitata run shape). Invocation (canonical flags, D11):
//   node --expose-gc --allow-natives-syntax bench/idiomatic.mjs       (npm run bench:idiomatic)
// Shape: in-process idiomatic verifier (corpus `<id>.verify.json` definitions) over all 16
// cells BEFORE any bench() registration (failure exits 1 before run(), D10) → one group per
// workload with `handlebars`/`eta` benches, bodies `() => do_not_optimize(render())` → a
// SINGLE run() → artifacts/idiomatic.txt + artifacts/idiomatic.json (two views of the same
// samples) → DEOPT-CHECK trailer over the in-process capture buffer (D12).
import { tracks } from "../src/engines/index.mjs";
import {
  assertIdiomaticGate,
  registerTrackGroups,
  runAndCapture,
  writeArtifacts,
  deoptCheckTrailer,
} from "./_shared.mjs";

const renderers = tracks.idiomatic; // per-track render table: renderers.<engine>[id] -> () => string

assertIdiomaticGate(renderers); // exits 1 before any registration on any verifier miss (D10)

registerTrackGroups("idiomatic", renderers);

const result = await runAndCapture(); // default 'mitata' format → stdout + in-process buffer
writeArtifacts("idiomatic", result); // JSON: result.benchmarks, BigInt→string replacer
deoptCheckTrailer(); // scans the in-process capture buffer for '!' → DEOPT-CHECK line
