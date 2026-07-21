// Shared bench-script machinery — Phase 4 WI6 (spec: README D10–D12, harness-and-run.md
// §mitata run shape). Provides: the in-process gates (run BEFORE any bench() registration —
// a failure exits 1 before run(), so no numbers exist for a failed gate, contract v2
// controlled-gate rule 2), the group/bench registration helper (one group per workload,
// bodies `() => do_not_optimize(render())`), the single-run() capture (a `print` tap feeds
// the same lines that reach stdout into an in-process buffer), the artifact writer (one
// process, one sample set, two views: `<name>.txt` from the capture buffer + `<name>.json`
// from run()'s returned benchmarks, BigInt-safe), and the D12 DEOPT-CHECK trailer (scans the
// in-process capture buffer — never the externally redirected .txt — for mitata's `!`
// "likely optimized out" marker).
import { mkdirSync, writeFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import path from "node:path";
import { bench, group, run, do_not_optimize } from "mitata";

import { WORKLOADS, loadVerifyDefinition } from "../src/gate/corpus.mjs";
import { assertControlledCell } from "../src/gate/controlled.mjs";
import { verify } from "../src/gate/verifier.mjs";

/** The two engines of this phase, in disclosure order (Handlebars = credibility pick). */
export const ENGINES = Object.freeze(["handlebars", "eta"]);

/** The eight workload ids in workload order (Phase 1 workloads.md). */
export const WORKLOAD_IDS = Object.freeze(WORKLOADS.map((w) => w.id));

// benchmarks/js/bench/_shared.mjs -> artifacts live at benchmarks/js/artifacts/ (gitignored).
const artifactsDir = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..", "artifacts");

function failGate(messages) {
  for (const message of Array.isArray(messages) ? messages : [messages]) console.error(message);
  console.error(
    "gate failed — exiting before any benchmark registration; no numbers exist for a failed gate " +
      "(parity-contract-v2 §Controlled-track gate rule 2; Phase 4 README D10).",
  );
  process.exit(1);
}

/**
 * Controlled byte gate + encoded security floor over all 16 engine×workload cells (D10).
 * `renderers` is a per-track table: `renderers[engine][workloadId] -> () => string`.
 * Exits 1 with the contract's failure surface before any bench() registration on any miss.
 */
export function assertControlledGate(renderers, label = "controlled") {
  let cells = 0;
  for (const engine of ENGINES) {
    for (const id of WORKLOAD_IDS) {
      const render = renderers?.[engine]?.[id];
      if (typeof render !== "function") {
        failGate(`[FAIL] ${engine} ${id} [${label}]: no renderer registered`);
      }
      try {
        assertControlledCell({ engine, workload: id, render });
        cells++;
      } catch (error) {
        failGate(error.message);
      }
    }
  }
  console.log(`gate: ${label} — ${cells}/${ENGINES.length * WORKLOAD_IDS.length} cells green (byte gate + security floor).`);
}

/**
 * Idiomatic functional-equivalence verifier over all 16 cells (D10), fed from the corpus
 * `<id>.verify.json` definitions. Exits 1 before any bench() registration on any miss.
 */
export function assertIdiomaticGate(renderers) {
  let cells = 0;
  for (const engine of ENGINES) {
    for (const id of WORKLOAD_IDS) {
      const render = renderers?.[engine]?.[id];
      if (typeof render !== "function") {
        failGate(`[FAIL] ${engine} ${id} [idiomatic]: no renderer registered`);
      }
      const failures = verify(loadVerifyDefinition(id), render(), `${engine} ${id}`);
      if (failures.length > 0) failGate(failures.map((f) => f.message));
      cells++;
    }
  }
  console.log(`gate: idiomatic — ${cells}/${ENGINES.length * WORKLOAD_IDS.length} cells green (verifier).`);
}

// ---- registration ----------------------------------------------------------------------------

// Every bench name registered through this module, for the DEOPT-CHECK name resolution.
const registeredNames = new Set();

/** `bench()` wrapper that records the display name for the DEOPT-CHECK trailer. */
export function namedBench(name, fn) {
  registeredNames.add(name);
  return bench(name, fn);
}

/**
 * D11 registration shape for the two track scripts: one `group('<workload-id> [<track>]')`
 * per workload containing the `handlebars` and `eta` benches, every body
 * `() => do_not_optimize(render())` (mitata's documented DCE guard consumes the string).
 */
export function registerTrackGroups(track, renderers) {
  for (const id of WORKLOAD_IDS) {
    group(`${id} [${track}]`, () => {
      for (const engine of ENGINES) {
        const render = renderers[engine][id];
        namedBench(engine, () => do_not_optimize(render()));
      }
    });
  }
}

// ---- single run() + capture ------------------------------------------------------------------

// In-process capture buffer: the print tap hands run() a sink that both echoes to stdout and
// collects the identical lines here (harness-and-run.md §mitata run shape, DEOPT-CHECK note).
const captured = [];

/**
 * The SINGLE `run()` of a bench script: default 'mitata' format to stdout, with every line
 * also collected in the in-process buffer. One process, one sample set — the text and JSON
 * artifacts are two views of the same run, never two runs (D11).
 */
export async function runAndCapture() {
  return await run({
    print: (s) => {
      captured.push(s);
      console.log(s);
    },
  });
}

const bigintReplacer = (_key, value) => (typeof value === "bigint" ? value.toString() : value);

/**
 * Writes both artifacts from the single run: `artifacts/<name>.txt` (the captured
 * default-format output) and `artifacts/<name>.json` (`result.benchmarks`, BigInt→string).
 * When run.ps1 drives the process its stdout redirect owns `<name>.txt`; the in-process
 * rewrite of the identical lines is then skipped with a note (same stream either way).
 */
export function writeArtifacts(name, result) {
  mkdirSync(artifactsDir, { recursive: true });
  writeFileSync(
    path.join(artifactsDir, `${name}.json`),
    JSON.stringify(result.benchmarks, bigintReplacer, 2),
    "utf8",
  );
  try {
    writeFileSync(path.join(artifactsDir, `${name}.txt`), captured.join("\n") + "\n", "utf8");
  } catch (error) {
    if (error?.code !== "EBUSY" && error?.code !== "EPERM" && error?.code !== "EACCES") throw error;
    console.log(
      `artifacts: ${name}.txt is held by the launcher's stdout redirect (run.ps1) — it captures the same lines.`,
    );
  }
  console.log(`artifacts: ${name}.txt + ${name}.json written under benchmarks/js/artifacts/ (single run, two views).`);
}

// ---- DEOPT-CHECK trailer (D12) ---------------------------------------------------------------

// eslint-disable-next-line no-control-regex
const ANSI = /\x1b\[[0-9;]*m/g;

/**
 * Scans the in-process capture buffer (never the redirected .txt, which may be unflushed at
 * trailer time) for mitata's `!` marker ("benchmark was likely optimized out"), and prints the
 * publication-checklist trailer: `DEOPT-CHECK: clean` or `DEOPT-CHECK: flagged <bench names>`.
 * Advisory (exit 0); publication-blocking via the checklist (D12). Returns the flagged list.
 */
export function deoptCheckTrailer() {
  const flagged = [];
  let currentGroup = null;
  for (const line of captured) {
    const plain = line.replace(ANSI, "");
    const groupHeader = plain.match(/^• (.*)$/);
    if (groupHeader) {
      currentGroup = groupHeader[1];
      continue;
    }
    // A flagged bench line ends in " !"; the legend line ends in "= !" and is not a bench.
    if (!plain.endsWith(" !") || plain.endsWith("= !")) continue;
    const name = [...registeredNames]
      .filter((n) => plain.startsWith(n))
      .sort((a, b) => b.length - a.length)[0];
    if (name) flagged.push(currentGroup ? `${name} (${currentGroup})` : name);
  }
  console.log(flagged.length === 0 ? "DEOPT-CHECK: clean" : `DEOPT-CHECK: flagged ${flagged.join(", ")}`);
  return flagged;
}
