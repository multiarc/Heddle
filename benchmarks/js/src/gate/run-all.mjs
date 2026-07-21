// `npm run gate` entry (Phase 4 WI3; spec: harness-and-run.md §Harness layout / README WI3):
// runs both gates over all 32 cells — 8 workloads × 2 engines (handlebars, eta) × 2 tracks
// (controlled byte gate + security floor, idiomatic verifier) — printing one [PASS]/[FAIL]
// line per cell and exiting non-zero unless every cell passes.
//
// Engine render tables arrive with WI4 (Handlebars) and WI5 (Eta) via src/engines/index.mjs,
// which must export `tracks[track][engine][workloadId] -> () => string`. Until then, cells
// without a registered renderer are reported cleanly as failures (the 32-PASS bar is WI4/WI5's
// Done-when).
import { WORKLOADS, loadVerifyDefinition } from "./corpus.mjs";
import { assertControlledCell } from "./controlled.mjs";
import { verify } from "./verifier.mjs";

// Optional filters (dev iteration only; the WI4/WI5 bar is the unfiltered 32-PASS run):
//   node src/gate/run-all.mjs [--engine <name>] [--workload <id>] [--track <name>]
function argValue(flag) {
  const i = process.argv.indexOf(flag);
  return i !== -1 && i + 1 < process.argv.length ? process.argv[i + 1] : null;
}
const engineFilter = argValue("--engine");
const workloadFilter = argValue("--workload");
const trackFilter = argValue("--track");

const ENGINES = ["handlebars", "eta"].filter((e) => !engineFilter || e === engineFilter);
const TRACKS = ["controlled", "idiomatic"].filter((t) => !trackFilter || t === trackFilter);
const SELECTED_WORKLOADS = WORKLOADS.filter((w) => !workloadFilter || w.id === workloadFilter);

let tracks = null;
try {
  ({ tracks } = await import("../engines/index.mjs"));
} catch (error) {
  if (error?.code !== "ERR_MODULE_NOT_FOUND") throw error;
  console.log("gate: src/engines/index.mjs not present yet (WI4/WI5) — all cells unregistered.");
}

let pass = 0;
let fail = 0;

for (const track of TRACKS) {
  for (const engine of ENGINES) {
    for (const { id } of SELECTED_WORKLOADS) {
      const cell = `${engine} ${id} [${track}]`;
      const render = tracks?.[track]?.[engine]?.[id];
      if (typeof render !== "function") {
        console.log(`[FAIL] ${cell}: no renderer registered (WI4/WI5 pending)`);
        fail++;
        continue;
      }
      try {
        if (track === "controlled") {
          assertControlledCell({ engine, workload: id, render });
        } else {
          const failures = verify(loadVerifyDefinition(id), render(), cell);
          if (failures.length > 0) {
            for (const f of failures) console.log(f.message);
            fail++;
            continue;
          }
        }
        console.log(`[PASS] ${cell}`);
        pass++;
      } catch (error) {
        console.log(error.message.startsWith("[FAIL]") ? error.message : `[FAIL] ${cell}: ${error.message}`);
        fail++;
      }
    }
  }
}

console.log(`gate: ${pass} passed, ${fail} failed (of ${TRACKS.length * ENGINES.length * SELECTED_WORKLOADS.length} cells).`);
process.exit(fail === 0 ? 0 : 1);
