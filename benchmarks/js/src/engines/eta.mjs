// Eta engine module (Phase 4 WI5; spec: README D8, harness-and-run.md §Harness layout,
// templates-and-models.md §Eta). One `new Eta()` instance per track, ALL defaults
// (autoEscape: true, autoTrim: [false, "nl"], varName: "it", default tags and escapeFunction).
// Templates are registered by name at startup via `eta.loadTemplate("@<id>", src)` (`@` =
// cached, non-filesystem); the measured render call is `eta.render("@<id>", model)` — Eta's
// documented cached-template render path. Raw suites use the raw tag `<%~ %>`; encoded suites
// use `<%= %>` (default XMLEscape — byte-canonical, N5 identity). The idiomatic track uses
// Eta's native layout system (`layout("@shell"/"@page", it)` + `<%~ it.body %>`) for
// composed-page and mixed-page.
//
// Exported registry fragment: `tracks[track].eta[workloadId] -> () => string`, merged by
// src/engines/index.mjs (WI6) into the harness-wide `tracks` / per-track `renderers` tables.
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

/** Builds one track's Eta instance (defaults) with its templates registered, and the render table. */
function buildTrack(track, partials) {
  const eta = new Eta();
  for (const [name, file] of partials) {
    eta.loadTemplate(name, readTemplate(track, file));
  }
  const renderers = {};
  for (const id of WORKLOAD_IDS) {
    eta.loadTemplate(`@${id}`, readTemplate(track, `${id}.eta`));
    const model = MODELS[id];
    renderers[id] = () => eta.render(`@${id}`, model);
  }
  return { eta, renderers: Object.freeze(renderers) };
}

const controlledTrack = buildTrack("controlled", [
  ["@layout", "layout.partial.eta"],
  ["@tile", "tile.partial.eta"],
]);
const idiomaticTrack = buildTrack("idiomatic", [
  ["@tile", "tile.partial.eta"],
  ["@shell", "shell.layout.eta"],
  ["@page", "page.layout.eta"],
]);

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
