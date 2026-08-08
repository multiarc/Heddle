// Cold-compile bench script — Phase 4 WI6 (spec: README D14, harness-and-run.md §mitata run
// shape / §Harness layout "cold-compile.mjs ← gate (reuses controlled outputs) → 16 cold
// benches → artifacts"). Invocation (canonical flags, D11):
//   node --expose-gc --allow-natives-syntax bench/cold-compile.mjs    (npm run bench:cold)
//
// Measures cold compile + first render per controlled template, per engine (D14):
//   - Handlebars: fresh `Handlebars.create()` environment (+ partial registration where the
//     workload has partials) + `compile(src)` + one render PER ITERATION — the render forces
//     Handlebars' lazy compile, so pure-compile() timing would be a lie;
//   - Eta: fresh `new Eta()` (+ `loadTemplate` of the workload's partials, the registration
//     analogue) + `renderString(src, model)` per iteration — methodologically identical cells.
// Template sources are read from src/templates/*/controlled/ ONCE at startup; file I/O is
// never inside a timed body. Per Q1.3 the resulting figures are per-ecosystem only.
//
// Support templates (partials + layout shells) are DISCOVERED from each engine's controlled
// track's `shared/` subdirectory (`src/templates/<engine>/controlled/shared/` — only the eight
// entry templates sit at a track's top level; legacy top-level scan kept as fallback while a
// track has no `shared/`) by the shared file convention `<name>.partial.<ext>` / `<name>.layout.<ext>`
// (registered as `<name>` for Handlebars, `@<name>` for Eta — the names the engine modules
// register). The E20 fragment-heavy partial set ({tile, card, badge, price, media_row, stat})
// belongs to the fragment-heavy cell; every other support template belongs to composed-page
// (the E20/E22 native-layout shell, nav partials and literal chrome fragments). The pre-E20
// `area` helper is gone with the model's text blobs (E22: composed-page is pure template
// composition on every engine — no registered helpers anywhere in the suite).
//
// Shape: in-process controlled byte gate (reuses the controlled render tables) over all 16
// cells BEFORE any bench() registration (failure exits 1 before run(), D10) → the 16 cold
// benches under group `cold-compile [per-ecosystem]` (harness-and-run.md §mitata run shape),
// bench names `<engine> <workload-id>`, bodies
// `() => do_not_optimize(flatten(coldRender()))` (D11 as amended, records.md E4 — the compile
// is only half the cell; without flatten the first render's rope is never materialised) → a
// SINGLE run() → artifacts/cold-compile.txt + artifacts/cold-compile.json (two views of the
// same samples) → DEOPT-CHECK trailer over the in-process capture buffer (D12).
//
// No MATERIALISATION-CHECK here: these cells time compile + first render together, so their
// implied output throughput is dominated by compilation and the physical ceiling says nothing
// about them. The two track scripts carry that check.
import { readFileSync, readdirSync, existsSync } from "node:fs";
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
  flatten,
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
const etaSources = Object.fromEntries(WORKLOAD_IDS.map((id) => [id, readControlled("eta", `${id}.eta`)]));

// The fragment-heavy partial family (Phase 1 workloads.md workload 6, E20); every other
// support template is composed-page's (workload 1, E20/E22). Both dash and underscore
// spellings of media_row are accepted so either engine's file naming resolves.
const FRAGMENT_PARTIAL_NAMES = new Set(["tile", "card", "badge", "price", "media_row", "media-row", "stat"]);
const SUPPORT_FILE = /^(.+)\.(?:partial|layout)\.(?:hbs|eta)$/;

/**
 * [{ name, src }] support templates of one engine's controlled track, by the file convention.
 * Canonical location is the track's `shared/` subdirectory; when an engine's track has no
 * `shared/` yet, the legacy top-level scan of the track directory applies (fallback, never
 * both — `shared/` wins outright when present).
 */
function supportTemplates(engine) {
  const trackDir = path.join(templatesDir, engine, "controlled");
  const sharedDir = path.join(trackDir, "shared");
  const scanDir = existsSync(sharedDir) ? sharedDir : trackDir;
  const support = [];
  for (const file of readdirSync(scanDir).sort()) {
    const match = SUPPORT_FILE.exec(file);
    if (match) support.push({ name: match[1], src: readFileSync(path.join(scanDir, file), "utf8") });
  }
  return support;
}

/** The support templates one workload's cold cell must register (empty for most workloads). */
function supportFor(support, id) {
  if (id === "fragment-heavy") return support.filter((s) => FRAGMENT_PARTIAL_NAMES.has(s.name));
  if (id === "composed-page") return support.filter((s) => !FRAGMENT_PARTIAL_NAMES.has(s.name));
  return [];
}

const hbsSupport = supportTemplates("handlebars");
const etaSupport = supportTemplates("eta");

/** Handlebars cold closure: fresh environment + registration + compile + one render (D14). */
function handlebarsCold(id) {
  const src = hbsSources[id];
  const model = MODELS[id];
  const partials = supportFor(hbsSupport, id);
  return () => {
    const env = Handlebars.create();
    for (const { name, src: partialSrc } of partials) env.registerPartial(name, partialSrc);
    return env.compile(src)(model);
  };
}

/** Eta cold closure: fresh instance + partial loadTemplate + renderString (D14). */
function etaCold(id) {
  const src = etaSources[id];
  const model = MODELS[id];
  const partials = supportFor(etaSupport, id);
  return () => {
    const eta = new Eta();
    for (const { name, src: partialSrc } of partials) eta.loadTemplate(`@${name}`, partialSrc);
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
    namedBench(`handlebars ${id}`, () => do_not_optimize(flatten(hb())));
    namedBench(`eta ${id}`, () => do_not_optimize(flatten(et())));
  }
});

const result = await runAndCapture(); // default 'mitata' format → stdout + in-process buffer
writeArtifacts("cold-compile", result); // JSON: result.benchmarks, BigInt→string replacer
deoptCheckTrailer(); // scans the in-process capture buffer for '!' → DEOPT-CHECK line
