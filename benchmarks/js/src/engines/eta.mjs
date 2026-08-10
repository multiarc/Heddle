// Eta engine module. One `new Eta()` instance per track, ALL defaults (autoEscape: true,
// autoTrim: [false, "nl"], varName: "it", default tags and escapeFunction). Templates are
// registered by name at startup via `eta.loadTemplate("@<id>", src)` (`@` = cached,
// non-filesystem); the measured render call is `eta.render("@<id>", model)` — Eta's documented
// cached-template render path. Raw suites use the raw tag `<%~ %>` in the controlled track
// (idiomatic scalar substitutions use the docs-default `<%= %>` — a byte no-op, since raw-suite
// values contain nothing escapable); encoded suites use `<%= %>` (default XMLEscape —
// byte-canonical, an identity under the gate's N5 entity canonicalization).
//
// composed-page uses Eta's NATIVE layout system in BOTH tracks: the child declares
// `<% layout("@shell", it) %>` with the slider markup as its body, and the shell — the full
// literal page chrome transcribed from the Heddle layout — splices it at `<%~ it.body %>`.
// The structured nav renders through nested partials (mega_menu → nav_column → nav_section →
// nav_link) and the inert chrome fragments are per-fragment literal partials mirroring the
// Heddle chrome-fragments.heddle definition library. fragment-heavy dispatches four
// fragment kinds per row over six partials (card nests badge + price against the row's promo).
//
// Exported registry fragment: `tracks[track].eta[workloadId] -> () => string`, merged by
// src/engines/index.mjs into the harness-wide `tracks` / per-track `renderers` tables.
import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import path from "node:path";
import { Eta } from "eta";

import { model as composedPage } from "../models/composed-page.mjs";
import { model as trivialSubstitution } from "../models/trivial-substitution.mjs";
import { model as largeLoop } from "../models/large-loop.mjs";
import { model as mixedPage } from "../models/mixed-page.mjs";
import { model as conditionalHeavy } from "../models/conditional-heavy.mjs";
import { model as fragmentHeavy } from "../models/fragment-heavy.mjs";
import { model as fortunesEncoded } from "../models/fortunes-encoded.mjs";
import { model as encodedLoop } from "../models/encoded-loop.mjs";

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

const WORKLOAD_IDS = Object.freeze(Object.keys(MODELS));

const templatesDir = path.resolve(
  path.dirname(fileURLToPath(import.meta.url)),
  "..",
  "templates",
  "eta",
);

function readTemplate(track, file) {
  return readFileSync(path.join(templatesDir, track, file), "utf8");
}

function readSupportTemplate(track, file) {
  return readFileSync(path.join(templatesDir, track, "shared", file), "utf8");
}

// Support templates (partials + the layout shell), identical name sets in both tracks, living
// under `<track>/shared/` (only the eight entry templates sit at the track's top level). File
// naming is the convention bench/cold-compile.mjs discovers support templates by:
// `shared/<name>.partial.eta` / `shared/<name>.layout.eta` registers as `@<name>`.
const SUPPORT_TEMPLATES = Object.freeze([
  // composed-page: native-layout shell + nested nav partials + chrome fragments.
  ["@shell", "shell.layout.eta"],
  ["@nav_link", "nav_link.partial.eta"],
  ["@nav_section", "nav_section.partial.eta"],
  ["@nav_column", "nav_column.partial.eta"],
  ["@mega_menu", "mega_menu.partial.eta"],
  ["@alert_top", "alert_top.partial.eta"],
  ["@alert_below", "alert_below.partial.eta"],
  ["@secondary_wholesale_menu", "secondary_wholesale_menu.partial.eta"],
  ["@secondary_retail_menu", "secondary_retail_menu.partial.eta"],
  ["@assets_styles", "assets_styles.partial.eta"],
  ["@assets_scripts", "assets_scripts.partial.eta"],
  ["@custom_styles", "custom_styles.partial.eta"],
  ["@head_scripts", "head_scripts.partial.eta"],
  ["@body_scripts", "body_scripts.partial.eta"],
  ["@body_end_scripts", "body_end_scripts.partial.eta"],
  // fragment-heavy: six dispatched fragment partials (card nests badge + price).
  ["@tile", "tile.partial.eta"],
  ["@card", "card.partial.eta"],
  ["@badge", "badge.partial.eta"],
  ["@price", "price.partial.eta"],
  ["@media_row", "media_row.partial.eta"],
  ["@stat", "stat.partial.eta"],
]);

/** Builds one track's Eta instance (defaults) with its templates registered, and the render table. */
function buildTrack(track) {
  const eta = new Eta();
  for (const [name, file] of SUPPORT_TEMPLATES) {
    eta.loadTemplate(name, readSupportTemplate(track, file));
  }
  const renderers = {};
  for (const id of WORKLOAD_IDS) {
    eta.loadTemplate(`@${id}`, readTemplate(track, `${id}.eta`));
    const model = MODELS[id];
    renderers[id] = () => eta.render(`@${id}`, model);
  }
  return { eta, renderers: Object.freeze(renderers) };
}

const controlledTrack = buildTrack("controlled");
const idiomaticTrack = buildTrack("idiomatic");

/** Controlled-track render table: `controlled[id]() -> string`. */
export const controlled = controlledTrack.renderers;
/** Idiomatic-track render table: `idiomatic[id]() -> string`. */
export const idiomatic = idiomaticTrack.renderers;

/** Per-engine fragment consumed by src/engines/index.mjs: `eta[track][id] -> () => string`. */
export const eta = Object.freeze({ controlled, idiomatic });

/** Registry fragment in run-all's shape: `tracks[track].eta[id] -> () => string`. */
export const tracks = Object.freeze({
  controlled: Object.freeze({ eta: controlled }),
  idiomatic: Object.freeze({ eta: idiomatic }),
});
