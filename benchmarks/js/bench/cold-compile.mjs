// Cold-compile bench script — Phase 4 WI6 (spec: README D14, harness-and-run.md §mitata run
// shape / §Harness layout "cold-compile.mjs ← gate (reuses controlled outputs) → 16 cold
// benches → artifacts"). Invocation (canonical flags, D11):
//   node --expose-gc --allow-natives-syntax bench/cold-compile.mjs    (npm run bench:cold)
//
// Measures cold compile + first render per controlled template, per engine (D14):
//   - Handlebars: fresh `Handlebars.create()` environment (+ partial/helper registration where
//     the workload has them) + `compile(src)` + one render PER ITERATION — the render forces
//     Handlebars' lazy compile, so pure-compile() timing would be a lie;
//   - Eta: fresh `new Eta()` (+ `loadTemplate` of the workload's partials, the registration
//     analogue) + `renderString(src, model)` per iteration — methodologically identical cells.
// Template sources are read from src/templates/*/controlled/ ONCE at startup; file I/O is
// never inside a timed body. Per Q1.3 the resulting figures are per-ecosystem only.
//
// Shape: in-process controlled byte gate (reuses the controlled render tables) over all 16
// cells BEFORE any bench() registration (failure exits 1 before run(), D10) → the 16 cold
// benches under group `cold-compile [per-ecosystem]` (harness-and-run.md §mitata run shape),
// bench names `<engine> <workload-id>`, bodies `() => do_not_optimize(coldRender())` → a
// SINGLE run() → artifacts/cold-compile.txt + artifacts/cold-compile.json (two views of the
// same samples) → DEOPT-CHECK trailer over the in-process capture buffer (D12).
import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import path from "node:path";
import Handlebars from "handlebars";
import { Eta } from "eta";
import { group, do_not_optimize } from "mitata";

import { tracks } from "../src/engines/index.mjs";
import {
  WORKLOAD_IDS,
  assertControlledGate,
  namedBench,
  runAndCapture,
  writeArtifacts,
  deoptCheckTrailer,
} from "./_shared.mjs";

import { model as composedPage } from "../src/models/composed-page.mjs";
import { model as trivialSubstitution } from "../src/models/trivial-substitution.mjs";
import { model as largeLoop } from "../src/models/large-loop.mjs";
import { model as mixedPage } from "../src/models/mixed-page.mjs";
import { model as conditionalHeavy } from "../src/models/conditional-heavy.mjs";
import { model as fragmentHeavy } from "../src/models/fragment-heavy.mjs";
import { model as fortunesEncoded } from "../src/models/fortunes-encoded.mjs";
import { model as encodedLoop } from "../src/models/encoded-loop.mjs";

const MODELS = Object.freeze({
  "composed-page": composedPage,
  "trivial-substitution": trivialSubstitution,
  "large-loop": largeLoop,
  "mixed-page": mixedPage,
  "conditional-heavy": conditionalHeavy,
  "fragment-heavy": fragmentHeavy,
  "fortunes-encoded": fortunesEncoded,
  "encoded-loop": encodedLoop,
});

// ---- template sources, read once at startup (outside every timed region) ---------------------

const templatesDir = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..", "src", "templates");
const readControlled = (engine, file) => readFileSync(path.join(templatesDir, engine, "controlled", file), "utf8");

const hbsSources = Object.fromEntries(WORKLOAD_IDS.map((id) => [id, readControlled("handlebars", `${id}.hbs`)]));
const hbsLayoutPartial = readControlled("handlebars", "layout.partial.hbs");
const hbsTilePartial = readControlled("handlebars", "tile.partial.hbs");

const etaSources = Object.fromEntries(WORKLOAD_IDS.map((id) => [id, readControlled("eta", `${id}.eta`)]));
const etaLayoutPartial = readControlled("eta", "layout.partial.eta");
const etaTilePartial = readControlled("eta", "tile.partial.eta");

// The suite's single registered helper (README D5), re-registered on every fresh cold
// environment — mirrors src/engines/handlebars.mjs registerAreaHelper.
function registerAreaHelper(env) {
  env.registerHelper("area", function (name) {
    return new Handlebars.SafeString(
      Object.prototype.hasOwnProperty.call(composedPage.areas, name) ? composedPage.areas[name] : "",
    );
  });
}

/** Handlebars cold closure: fresh environment + registration + compile + one render (D14). */
function handlebarsCold(id) {
  const src = hbsSources[id];
  const model = MODELS[id];
  return () => {
    const env = Handlebars.create();
    if (id === "composed-page") {
      env.registerPartial("layout", hbsLayoutPartial);
      registerAreaHelper(env);
    }
    if (id === "fragment-heavy") {
      env.registerPartial("tile", hbsTilePartial);
    }
    return env.compile(src)(model);
  };
}

/** Eta cold closure: fresh instance + partial loadTemplate + renderString (D14). */
function etaCold(id) {
  const src = etaSources[id];
  const model = MODELS[id];
  return () => {
    const eta = new Eta();
    if (id === "composed-page") eta.loadTemplate("@layout", etaLayoutPartial);
    if (id === "fragment-heavy") eta.loadTemplate("@tile", etaTilePartial);
    return eta.renderString(src, model);
  };
}

// ---- gate (D10): reuses the controlled render tables; exits 1 before any registration --------

assertControlledGate(tracks.controlled);

// ---- the 16 cold benches, single group (harness-and-run.md §mitata run shape) ----------------

group("cold-compile [per-ecosystem]", () => {
  for (const id of WORKLOAD_IDS) {
    const hb = handlebarsCold(id);
    const et = etaCold(id);
    namedBench(`handlebars ${id}`, () => do_not_optimize(hb()));
    namedBench(`eta ${id}`, () => do_not_optimize(et()));
  }
});

const result = await runAndCapture(); // default 'mitata' format → stdout + in-process buffer
writeArtifacts("cold-compile", result); // JSON: result.benchmarks, BigInt→string replacer
deoptCheckTrailer(); // scans the in-process capture buffer for '!' → DEOPT-CHECK line
