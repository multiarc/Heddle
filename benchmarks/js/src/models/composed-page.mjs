// composed-page model. The model is pure structured data — `{ nav }`,
// nothing else. The nav model (menus / footer_columns, snake_case keys) is loaded once at
// module init from the corpus fixture
// benchmarks/dotnet/GoldenCorpus/fixtures/composed-page/nav.json — the single source of truth
// exported by export-corpus — via the gate's corpus-dir resolution, verified against the
// manifest `fixtures` section before use (same corrupted-checkout guard as the golden loader),
// and deep-frozen. No text blobs live here (no section defaults, fragment bodies, or area
// dictionary): every literal page fragment lives in the template tier — templates hold ALL
// text; the model-preparation tier carries DATA only.
import { readFileSync } from "node:fs";
import { createHash } from "node:crypto";
import path from "node:path";
import { corpusDir, loadManifest } from "../gate/corpus.mjs";
import { deepFreeze } from "./_deep-freeze.mjs";

const fixtureFile = "fixtures/composed-page/nav.json";
const navPath = path.join(corpusDir, "fixtures", "composed-page", "nav.json");
const bytes = readFileSync(navPath);

const fixture = (loadManifest().fixtures ?? []).find((f) => f.file === fixtureFile);
if (!fixture) {
  throw new Error(
    `${fixtureFile} has no manifest.json fixtures entry under benchmarks/dotnet/GoldenCorpus/ ` +
      "— run the .NET export-corpus tool first",
  );
}
const hash = `sha256:${createHash("sha256").update(bytes).digest("hex")}`;
if (hash !== fixture.hash || bytes.length !== fixture.byteLength) {
  throw new Error(
    `${fixtureFile} is corrupted: file has ${hash} (${bytes.length} bytes) but manifest.json ` +
      `records ${fixture.hash} (${fixture.byteLength} bytes) — restore the checkout or re-run export-corpus`,
  );
}

const nav = JSON.parse(new TextDecoder("utf-8", { fatal: true }).decode(bytes));

export const model = deepFreeze({ nav });
