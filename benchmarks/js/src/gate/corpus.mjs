// Golden-corpus loader. Reads the corpus read-only from benchmarks/dotnet/GoldenCorpus/
// via a repo-relative path resolved from import.meta.url (one source of truth — never copied),
// and verifies each corpus file's SHA-256 + byte length against manifest.json before use
// (corrupted-checkout guard).
import { readFileSync, existsSync } from "node:fs";
import { createHash } from "node:crypto";
import { fileURLToPath } from "node:url";
import path from "node:path";

// benchmarks/js/src/gate/corpus.mjs -> repo root is four levels up.
const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..", "..", "..", "..");
export const corpusDir = path.join(repoRoot, "benchmarks", "dotnet", "GoldenCorpus");

/** The eight workloads, ordered by workload number. */
export const WORKLOADS = Object.freeze([
  Object.freeze({ id: "composed-page", suite: "raw" }),
  Object.freeze({ id: "trivial-substitution", suite: "raw" }),
  Object.freeze({ id: "large-loop", suite: "raw" }),
  Object.freeze({ id: "mixed-page", suite: "raw" }),
  Object.freeze({ id: "conditional-heavy", suite: "raw" }),
  Object.freeze({ id: "fragment-heavy", suite: "raw" }),
  Object.freeze({ id: "fortunes-encoded", suite: "encoded" }),
  Object.freeze({ id: "encoded-loop", suite: "encoded" }),
]);

let manifestCache = null;

/** Parsed manifest.json, loaded once. */
export function loadManifest() {
  if (manifestCache) return manifestCache;
  const manifestPath = path.join(corpusDir, "manifest.json");
  if (!existsSync(manifestPath)) {
    throw new Error(
      "corpus entry manifest.json not found under benchmarks/dotnet/GoldenCorpus/ — run the .NET export-corpus tool first",
    );
  }
  manifestCache = JSON.parse(readFileSync(manifestPath, "utf8"));
  return manifestCache;
}

const entryCache = new Map();

/**
 * Loads one golden corpus entry: `{ id, suite, text, bytes }`. The file's SHA-256 and byte
 * length are verified against the manifest before the bytes are used; a mismatch aborts with a
 * distinct corrupted-checkout message.
 */
export function loadCorpusEntry(id) {
  if (entryCache.has(id)) return entryCache.get(id);

  const file = path.join(corpusDir, `${id}.golden.html`);
  if (!existsSync(file)) {
    throw new Error(
      `corpus entry ${id} not found under benchmarks/dotnet/GoldenCorpus/ — run the .NET export-corpus tool first`,
    );
  }
  const manifest = loadManifest();
  const entry = (manifest.entries ?? []).find((e) => e.workload === id);
  if (!entry) {
    throw new Error(
      `corpus entry ${id} has no manifest.json entry under benchmarks/dotnet/GoldenCorpus/ — run the .NET export-corpus tool first`,
    );
  }

  const bytes = readFileSync(file);
  const hash = `sha256:${createHash("sha256").update(bytes).digest("hex")}`;
  if (hash !== entry.hash || bytes.length !== entry.byteLength) {
    throw new Error(
      `corpus entry ${id} is corrupted: GoldenCorpus/${id}.golden.html has ${hash} (${bytes.length} bytes) ` +
        `but manifest.json records ${entry.hash} (${entry.byteLength} bytes) — restore the checkout or re-run export-corpus`,
    );
  }

  const loaded = Object.freeze({
    id,
    suite: entry.suite,
    text: new TextDecoder("utf-8", { fatal: true }).decode(bytes),
    bytes,
  });
  entryCache.set(id, loaded);
  return loaded;
}

/** Loads one `<id>.verify.json` idiomatic-verifier definition (exported with the corpus). */
export function loadVerifyDefinition(id) {
  const file = path.join(corpusDir, `${id}.verify.json`);
  if (!existsSync(file)) {
    throw new Error(
      `corpus entry ${id} not found under benchmarks/dotnet/GoldenCorpus/ — run the .NET export-corpus tool first`,
    );
  }
  return JSON.parse(readFileSync(file, "utf8"));
}
