// Engine aggregator. Exposes the per-track render tables as
// `tracks[track][engine][workloadId] -> () => string`, the shape src/gate/run-all.mjs consumes.
//
// Merge seam: each engine lives in its own module (./handlebars.mjs, ./eta.mjs) exporting
// `{ controlled, idiomatic }` render tables under its engine name. The Eta import is
// tolerant: a missing ./eta.mjs leaves the eta cells unregistered (run-all
// reports them as clean failures); any other load error propagates.
import { handlebars } from "./handlebars.mjs";

let eta = null;
try {
  ({ eta } = await import("./eta.mjs"));
} catch (error) {
  const missingEtaModule =
    error?.code === "ERR_MODULE_NOT_FOUND" && /eta\.mjs/.test(error?.message ?? "");
  if (!missingEtaModule) throw error;
}

function byEngine(track) {
  const engines = { handlebars: handlebars[track] };
  if (eta) engines.eta = eta[track];
  return Object.freeze(engines);
}

export const tracks = Object.freeze({
  controlled: byEngine("controlled"),
  idiomatic: byEngine("idiomatic"),
});
