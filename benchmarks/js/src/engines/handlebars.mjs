// Handlebars engine module. Stock engine, never patched: no escapeExpression override — the
// encoded-suite `&#x27;` spelling is reconciled by the gate's N5 entity canonicalization.
// Per-track environment factories on `Handlebars.create()` (one environment per workload,
// mirroring the per-test environments of the intra-.NET Handlebars.Net twins):
//   - controlled: runtime `env.compile(src)` once at startup;
//   - idiomatic: `env.precompile(src, { knownHelpersOnly: true })` +
//     `env.template(eval("(" + spec + ")"))` once at startup.
// The suite registers NO helpers (composed-page is pure template composition), so
// `knownHelpersOnly: true` needs no `knownHelpers` extension anywhere: the built-ins
// (#if/#each) are the whole helper surface. Composition is partials only:
//   - composed-page: partial-block layout — the page is `{{#> layout}}…slider…{{/layout}}`,
//     the layout partial carries the full literal chrome and splices the body at
//     `{{> @partial-block}}` (handlebarsjs.com/guide/partials.html#partial-blocks); the inert
//     chrome fragments are per-fragment literal partials mirroring chrome-fragments.heddle,
//     and the nav renders through nested partials (mega_menu → nav_column → nav_section →
//     nav_link) over the fixture-loaded nav model.
//   - fragment-heavy: six partials (tile, card, badge, price, media_row, stat)
//     dispatched per row by the chained `{{#if is_*}}` boolean chain; card nests
//     `{{> badge promo}}{{> price promo}}`.
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

/** Workload id -> frozen model, in workload order. */
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

/** Workload id -> partial names; each name N is the file `{track}/shared/N.partial.hbs`. */
const PARTIALS = {
  // Partial-block layout + the chrome-fragment library + the nested nav partials.
  "composed-page": [
    "layout",
    "alert_top",
    "secondary_wholesale_menu",
    "secondary_retail_menu",
    "alert_below",
    "assets_styles",
    "assets_scripts",
    "custom_styles",
    "head_scripts",
    "body_scripts",
    "body_end_scripts",
    "mega_menu",
    "nav_column",
    "nav_section",
    "nav_link",
  ],
  // Six dispatched fragment partials; card nests badge + price against the row promo.
  "fragment-heavy": ["tile", "card", "badge", "price", "media_row", "stat"],
};

/** Controlled track: fresh environment per workload, runtime compile. */
function compileControlled(id) {
  const env = Handlebars.create();
  for (const name of PARTIALS[id] ?? []) {
    env.registerPartial(name, readTemplate("controlled", path.join("shared", `${name}.partial.hbs`)));
  }
  return env.compile(readTemplate("controlled", `${id}.hbs`));
}

// Materializes one precompiled spec: the docs-canonical
// `Handlebars.template(eval("(" + precompile(src) + ")"))` path.
function materialize(env, source, options) {
  const spec = env.precompile(source, options);
  return env.template(eval(`(${spec})`));
}

/** Idiomatic track: fresh environment per workload, precompiled `knownHelpersOnly`. */
function compileIdiomatic(id) {
  const env = Handlebars.create();
  const options = { knownHelpersOnly: true };
  for (const name of PARTIALS[id] ?? []) {
    env.registerPartial(name, materialize(env, readTemplate("idiomatic", path.join("shared", `${name}.partial.hbs`)), options));
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
 * compiled once at module load — outside every timed region. */
export const handlebars = Object.freeze({
  controlled: buildTrack(compileControlled),
  idiomatic: buildTrack(compileIdiomatic),
});
