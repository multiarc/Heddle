// Shared bench-script machinery. Provides: the in-process gates (run BEFORE any bench()
// registration — a failure exits 1 before run(), so no numbers exist for a failed gate),
// the group/bench registration helper (one group per workload, bodies
// `() => do_not_optimize(flatten(render()))` so no engine can defer or skip work),
// the single-run() capture (a `print` tap feeds
// the same lines that reach stdout into an in-process buffer), the artifact writer (one
// process, one sample set, two views: `<name>.txt` from the capture buffer + `<name>.json`
// from run()'s returned benchmarks, BigInt-safe), the DEOPT-CHECK trailer (scans the
// in-process capture buffer — never the externally redirected .txt — for mitata's `!`
// "likely optimized out" marker), and its companion MATERIALISATION-CHECK (implied output
// throughput against a physical ceiling; fatal, unlike DEOPT-CHECK).
import { mkdirSync, writeFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import path from "node:path";
import { bench, group, run, do_not_optimize } from "mitata";

import { WORKLOADS, loadVerifyDefinition, loadCorpusEntry } from "../src/gate/corpus.mjs";
import { assertControlledCell } from "../src/gate/controlled.mjs";
import { verify } from "../src/gate/verifier.mjs";

/** The two engines of this phase, in disclosure order (Handlebars = credibility pick). */
export const ENGINES = Object.freeze(["handlebars", "eta"]);

/** The eight workload ids in workload order. */
export const WORKLOAD_IDS = Object.freeze(WORKLOADS.map((w) => w.id));

// benchmarks/js/bench/_shared.mjs -> artifacts live at benchmarks/js/artifacts/ (gitignored).
const artifactsDir = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..", "artifacts");

// ---- the materialisation primitive: flatten the rendered value so no engine can defer work ---

// Both engines build their output with `+=`, so `render()` hands back an unflattened V8
// ConsString rope. mitata's `do_not_optimize(v)` is `{ $._ = v; }` in full: it makes the value
// escape so V8 cannot prove it unread, and never walks it. Timing that measures rope
// CONSTRUCTION, not output production — the 2026-07-22 run reported eta/composed-page at
// 330.7 ns for 34,847 B, i.e. ~105 GB/s, which is above this machine's store bandwidth.
//
// %FlattenString forces the rope into a flat sequential string, which is the work every other
// ecosystem's harness already does. Note `s.length` would NOT work: V8 stores the length on the
// ConsString, so reading it is O(1) and never flattens.
//
// Natives syntax is parse-time, and `npm run gate` / `npm run selftest` do not pass
// --allow-natives-syntax. Building the primitive through `new Function` keeps its absence a
// catchable runtime error instead of an unconditional module-load SyntaxError for every module
// that imports this one.
let nativeFlatten = true;

/**
 * Forces V8 to materialise `s`. The sanctioned measurement path is `%FlattenString`; the
 * fallback exists only so the flag-less gate entry points can import this module, and a
 * measurement run that lands on it is failed by `materialisationCheckTrailer`.
 */
export const flatten = (() => {
  try {
    // eslint-disable-next-line no-new-func
    const native = new Function("s", "return %FlattenString(s);");
    native("ab" + String(Date.now() % 7)); // prove it runs, not just parses
    return native;
  } catch {
    nativeFlatten = false;
    return (s) => {
      // charCodeAt on a ConsString forces a flatten; do_not_optimize keeps the read live.
      do_not_optimize(s.charCodeAt(s.length - 1));
      return s;
    };
  }
})();

/** Whether `flatten` is the natives-backed primitive. False means this is not a valid run. */
export const nativeFlattenAvailable = () => nativeFlatten;

// The physical ceiling for output bytes per nanosecond on the protocol machine. A cell above
// this claims more sustained single-core store bandwidth than the box has while also running
// template logic, so the harness cannot be materialising its output. Kept numerically in step
// with benchmarks/report/consolidate.py's PLAUSIBILITY_CEILING_B_PER_NS.
const PLAUSIBILITY_CEILING_B_PER_NS = 50.0;

function failGate(messages) {
  for (const message of Array.isArray(messages) ? messages : [messages]) console.error(message);
  console.error(
    "gate failed — exiting before any benchmark registration; no numbers exist for a failed gate.",
  );
  process.exit(1);
}

/**
 * Controlled byte gate + encoded security floor over all 16 engine×workload cells.
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
 * Idiomatic functional-equivalence verifier over all 16 cells, fed from the corpus
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
 * Registration shape for the two track scripts: one `group('<workload-id> [<track>]')`
 * per workload containing the `handlebars` and `eta` benches, every body
 * `() => do_not_optimize(flatten(render()))` — `flatten` materialises the rope and
 * mitata's documented DCE guard then consumes the flat string.
 */
export function registerTrackGroups(track, renderers) {
  for (const id of WORKLOAD_IDS) {
    group(`${id} [${track}]`, () => {
      for (const engine of ENGINES) {
        const render = renderers[engine][id];
        namedBench(engine, () => do_not_optimize(flatten(render())));
      }
    });
  }
}

// ---- single run() + capture ------------------------------------------------------------------

// In-process capture buffer: the print tap hands run() a sink that both echoes to stdout and
// collects the identical lines here, so the DEOPT-CHECK trailer never depends on the
// externally redirected .txt being flushed.
const captured = [];

/**
 * The SINGLE `run()` of a bench script: default 'mitata' format to stdout, with every line
 * also collected in the in-process buffer. One process, one sample set — the text and JSON
 * artifacts are two views of the same run, never two runs.
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

// ---- DEOPT-CHECK trailer ---------------------------------------------------------------------

// eslint-disable-next-line no-control-regex
const ANSI = /\x1b\[[0-9;]*m/g;

/**
 * Scans the in-process capture buffer (never the redirected .txt, which may be unflushed at
 * trailer time) for mitata's `!` marker ("benchmark was likely optimized out"), and prints the
 * publication-checklist trailer: `DEOPT-CHECK: clean` or `DEOPT-CHECK: flagged <bench names>`.
 * Advisory (exit 0); publication-blocking via the checklist. Returns the flagged list.
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

// ---- MATERIALISATION-CHECK trailer ------------------------------------------------------------

/**
 * Divides each cell's golden output size by its reported `avg` and fails the run if any cell
 * claims more throughput than the machine can physically deliver — the signature of a harness
 * that is not materialising its output.
 *
 * This is the companion DEOPT-CHECK cannot be: mitata's `!` marker fires only when
 * `avg < 1.42 * noop.avg` against an EMPTY FUNCTION, so a cell can skip nearly all of its work
 * and still sit three orders of magnitude above the trigger. The 2026-07-22 run reported
 * `DEOPT-CHECK: clean` while measuring rope construction.
 *
 * Unlike DEOPT-CHECK, a violation here is fatal rather than explainable: it means the harness is
 * not doing the work it reports. Exits 1 on a violation, and on a non-natives `flatten`, since a
 * silent fallback would reintroduce the defect invisibly.
 *
 * `result.benchmarks` carries no workload name — `alias` is the engine only — so cells are
 * matched positionally, exactly as registerTrackGroups() emitted them (WORKLOAD_IDS order,
 * ENGINES within each group). The shape is asserted before it is trusted.
 */
export function materialisationCheckTrailer(result) {
  if (!nativeFlatten) {
    console.error(
      "MATERIALISATION-CHECK: FAILED — %FlattenString is unavailable, so the benchmark bodies " +
        "fell back to a non-sanctioned materialisation path. Run through run.ps1/run.sh or " +
        "npm run bench:* so node receives --allow-natives-syntax.",
    );
    process.exit(1);
  }

  const benchmarks = result?.benchmarks ?? [];
  const expected = ENGINES.length * WORKLOAD_IDS.length;
  if (benchmarks.length !== expected) {
    console.error(
      `MATERIALISATION-CHECK: FAILED — expected ${expected} cells in run() output, found ` +
        `${benchmarks.length}; the positional workload mapping cannot be trusted.`,
    );
    process.exit(1);
  }

  const flagged = [];
  for (let i = 0; i < benchmarks.length; i++) {
    const entry = benchmarks[i];
    const engine = ENGINES[i % ENGINES.length];
    if (entry.alias !== engine) {
      console.error(
        `MATERIALISATION-CHECK: FAILED — cell ${i} is '${entry.alias}', expected '${engine}'; ` +
          "the positional workload mapping cannot be trusted.",
      );
      process.exit(1);
    }
    const id = WORKLOAD_IDS[Math.floor(i / ENGINES.length)];
    const avg = Number(entry.runs?.[0]?.stats?.avg);
    if (!Number.isFinite(avg) || avg <= 0) {
      console.error(`MATERIALISATION-CHECK: FAILED — cell ${engine}/${id} has no usable avg.`);
      process.exit(1);
    }
    const bytesPerNs = loadCorpusEntry(id).bytes.length / avg;
    if (bytesPerNs > PLAUSIBILITY_CEILING_B_PER_NS) {
      flagged.push(`${engine}/${id} (${bytesPerNs.toFixed(1)} B/ns)`);
    }
  }

  if (flagged.length > 0) {
    console.error(`MATERIALISATION-CHECK: flagged ${flagged.join(", ")}`);
    console.error(
      `every flagged cell exceeds the ${PLAUSIBILITY_CEILING_B_PER_NS} B/ns ceiling, so its ` +
        "output cannot be being produced — the numbers from this run are not publishable.",
    );
    process.exit(1);
  }
  console.log("MATERIALISATION-CHECK: clean");
  return flagged;
}
