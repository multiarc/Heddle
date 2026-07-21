// Handlebars engine module — Phase 4 WI4 (spec: README D4/D5/D7, templates-and-models.md
// §Handlebars, harness-and-run.md §Harness layout). Stock engine, never patched: no
// escapeExpression override — the encoded-suite `&#x27;` spelling is reconciled by N5 in the
// gate (README D4). Per-track environment factories on `Handlebars.create()` (one environment
// per workload, mirroring the per-test environments of the intra-.NET Handlebars.Net twins):
//   - controlled: runtime `env.compile(src)` once at startup (README D7);
//   - idiomatic: `env.precompile(src, { knownHelpersOnly: true, knownHelpers })` +
//     `env.template(eval("(" + spec + ")"))` once at startup — `knownHelpers: { area: true }`
//     only for the composed-page environment (README D7/D9).
// The complete registered-helper surface of the suite is the single `area` helper below
// (composed-page only — README D5); all other templates use only built-in helpers.
import Handlebars from "handlebars";
import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import path from "node:path";

import { model as composedPage } from "../models/composed-page.mjs";
import { model as trivialSubstitution } from "../models/trivial-substitution.mjs";
import { model as largeLoop } from "../models/large-loop.mjs";
import { model as mixedPage } from "../models/mixed-page.mjs";
import { model as conditionalHeavy } from "../models/conditional-heavy.mjs";
import { model as fragmentHeavy } from "../models/fragment-heavy.mjs";
import { model as fortunesEncoded } from "../models/fortunes-encoded.mjs";
import { model as encodedLoop } from "../models/encoded-loop.mjs";

const templatesDir = path.join(
  path.dirname(fileURLToPath(import.meta.url)),
  "..",
  "templates",
  "handlebars",
);

function readTemplate(track, name) {
  return readFileSync(path.join(templatesDir, track, name), "utf8");
}

/** Workload id -> frozen model, in workload order (Phase 1 workloads.md). */
const MODELS = {
  "composed-page": composedPage,
  "trivial-substitution": trivialSubstitution,
  "large-loop": largeLoop,
  "mixed-page": mixedPage,
  "conditional-heavy": conditionalHeavy,
  "fragment-heavy": fragmentHeavy,
  "fortunes-encoded": fortunesEncoded,
  "encoded-loop": encodedLoop,
};

// The one registered helper of the whole JS suite (README D5; templates-and-models.md
// §Registered helpers): name -> prebuilt fragment lookup, SafeString emission, empty string
// on a miss — the behavioral mirror of the Handlebars.Net twin's WriteSafeString helper.
function registerAreaHelper(env) {
  env.registerHelper("area", function (name) {
    return new Handlebars.SafeString(
      Object.prototype.hasOwnProperty.call(composedPage.areas, name) ? composedPage.areas[name] : "",
    );
  });
}

/** Controlled track: fresh environment per workload, runtime compile (README D7). */
function compileControlled(id) {
  const env = Handlebars.create();
  if (id === "composed-page") {
    env.registerPartial("layout", readTemplate("controlled", "layout.partial.hbs"));
    registerAreaHelper(env);
  }
  if (id === "fragment-heavy") {
    env.registerPartial("tile", readTemplate("controlled", "tile.partial.hbs"));
  }
  return env.compile(readTemplate("controlled", `${id}.hbs`));
}

// Materializes one precompiled spec: the docs-canonical
// `Handlebars.template(eval("(" + precompile(src) + ")"))` path (README D7).
function materialize(env, source, options) {
  const spec = env.precompile(source, options);
  return env.template(eval(`(${spec})`));
}

/** Idiomatic track: fresh environment per workload, precompiled `knownHelpersOnly` (README D7/D9). */
function compileIdiomatic(id) {
  const env = Handlebars.create();
  const options =
    id === "composed-page"
      ? { knownHelpersOnly: true, knownHelpers: { area: true } }
      : { knownHelpersOnly: true };
  if (id === "composed-page") {
    env.registerPartial("layout", materialize(env, readTemplate("idiomatic", "layout.partial.hbs"), options));
    registerAreaHelper(env);
  }
  if (id === "fragment-heavy") {
    env.registerPartial("tile", materialize(env, readTemplate("idiomatic", "tile.partial.hbs"), options));
  }
  return materialize(env, readTemplate("idiomatic", `${id}.hbs`), options);
}

function buildTrack(compile) {
  const table = {};
  for (const id of Object.keys(MODELS)) {
    const template = compile(id);
    const model = MODELS[id];
    table[id] = () => template(model);
  }
  return Object.freeze(table);
}

/** Per-track render tables: `handlebars[track][workloadId] -> () => string`. All templates are
 * compiled once at module load — outside every timed region (metrics rule 1). */
export const handlebars = Object.freeze({
  controlled: buildTrack(compileControlled),
  idiomatic: buildTrack(compileIdiomatic),
});
