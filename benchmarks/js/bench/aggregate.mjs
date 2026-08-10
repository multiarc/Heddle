// aggregate.mjs -- collapse N repeat passes of a bench script into the ecosystem's published
// artifact, and emit the cross-pass stability verdict from the same data.
//
// Why this exists. mitata exposes no per-cell time budget: `B.run()` builds its own
// options object internally and `run()` forwards only `throw`, so `min_cpu_time` (642 ms) cannot
// be raised through the public API. At that budget the JS leg measured 32 cells in 31 s -- about
// 1 s/cell, against 15-31 s/cell everywhere else. JS therefore spends its share of the uniform
// per-ecosystem budget on *many independent processes* rather than one long run, which is the
// same term JMH buys with forks and pyperf with its 20 workers, and is precisely the term a
// single-process harness lacks.
//
// The published `avg` becomes the MEDIAN of the per-pass `avg` values and the published
// dispersion becomes the min...max spread ACROSS passes, so the reported interval is
// cross-process variation rather than within-process sampling noise. The shape of the emitted
// JSON is byte-compatible with what `writeArtifacts` produces for a single run -- 16 entries,
// engine-paired groups in protocol order -- because benchmarks/report/consolidate.py asserts
// that shape (`load_js`).
//
// Stability verdict, per cell, on the cross-pass RSD of `avg`: <= 5% verified; 5-10%
// verified-with-disclosure (the report must carry the per-cell RSD table); > 10% failed, which
// exits 1 so no publication can be assembled from the run.

import { mkdirSync, readFileSync, readdirSync, writeFileSync } from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const artifactsDir = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..", "artifacts");
// Per-script, so two tracks cannot be medianed together; run.sh clears it before a sequence.
const stabilityDir = (name) => path.join(artifactsDir, "stability", name);

/** Verdict thresholds, per cell, on cross-pass RSD of the mitata `avg`. */
const RSD_VERIFIED = 5.0;
const RSD_DISCLOSE = 10.0;

/** Five runs is the protocol floor; the time budget usually funds more. Fewer is not a verdict. */
const MIN_PASSES = 5;

const die = (message) => {
  console.error(`aggregate: ${message}`);
  process.exit(1);
};

const median = (xs) => {
  const s = [...xs].sort((a, b) => a - b);
  const m = s.length >> 1;
  return s.length % 2 ? s[m] : (s[m - 1] + s[m]) / 2;
};

const rsdPercent = (xs) => {
  if (xs.length < 2) return 0;
  const mean = xs.reduce((a, b) => a + b, 0) / xs.length;
  if (mean === 0) return 0;
  const variance = xs.reduce((a, b) => a + (b - mean) ** 2, 0) / (xs.length - 1);
  return (Math.sqrt(variance) / mean) * 100;
};

const name = process.argv[2];
if (!name) die("usage: node bench/aggregate.mjs <controlled|idiomatic|cold-compile>");

// ---- load the passes -------------------------------------------------------------------------

const passDir = stabilityDir(name);
let dirEntries;
try {
  dirEntries = readdirSync(passDir, { withFileTypes: true });
} catch {
  die(`no pass directory at artifacts/stability/${name}/ — run ./run.sh bench/${name}.mjs --repeat <N> first`);
}
const files = dirEntries
  .filter((d) => d.isFile())
  .map((d) => d.name)
  .filter((f) => /^run-\d+\.json$/.test(f))
  .sort((a, b) => Number(a.match(/\d+/)[0]) - Number(b.match(/\d+/)[0]));

if (files.length < MIN_PASSES) {
  die(
    `found ${files.length} pass artifact(s) under artifacts/stability/${name}/, need at least ${MIN_PASSES} ` +
      `for a stability verdict. Run ./run.sh bench/${name}.mjs --repeat <N> first.`,
  );
}

const passes = files.map((f) => {
  const full = path.join(passDir, f);
  let data;
  try {
    data = JSON.parse(readFileSync(full, "utf8"));
  } catch (error) {
    die(`${f} is not readable JSON: ${error.message}`);
  }
  if (!Array.isArray(data) || data.length === 0) die(`${f} holds no benchmark entries`);
  return { file: f, data };
});

// Every pass must describe the same cells in the same order, or a positional median is nonsense.
const shapeOf = (data) => data.map((e) => `${e.alias}#${e.group}`).join("|");
const reference = shapeOf(passes[0].data);
for (const p of passes.slice(1)) {
  if (shapeOf(p.data) !== reference) {
    die(`${p.file} does not describe the same cells in the same order as ${passes[0].file}`);
  }
}

// ---- aggregate -------------------------------------------------------------------------------

const cellCount = passes[0].data.length;
const rows = [];

const aggregate = passes[0].data.map((entry, i) => {
  const avgs = passes.map((p) => Number(p.data[i].runs[0].stats.avg));
  if (avgs.some((v) => !Number.isFinite(v) || v <= 0)) {
    die(`cell ${entry.alias}[${i}] has a non-finite or non-positive avg in at least one pass`);
  }
  const med = median(avgs);
  const rsd = rsdPercent(avgs);
  rows.push({ index: i, alias: entry.alias, avgs, med, rsd });

  // Heap is averaged rather than medianed: it is a byte count with no cross-pass spread to
  // report, and consolidate.py reads only `heap.avg`.
  const heaps = passes
    .map((p) => p.data[i].runs[0].stats.heap?.avg)
    .filter((v) => typeof v === "number" && Number.isFinite(v));

  const stats = {
    ...passes[0].data[i].runs[0].stats,
    avg: med,
    min: Math.min(...avgs),
    max: Math.max(...avgs),
    p50: med,
    // Provenance, so an aggregate artifact is never mistaken for a single run.
    aggregate: {
      passes: passes.length,
      source: files,
      statistic: "median of per-pass avg",
      dispersion: "min...max of per-pass avg (cross-process)",
      perPassAvg: avgs,
      rsdPercent: rsd,
    },
  };
  if (heaps.length) stats.heap = { ...stats.heap, avg: heaps.reduce((a, b) => a + b, 0) / heaps.length };

  return {
    ...entry,
    runs: [{ ...entry.runs[0], stats }],
  };
});

// ---- verdict ---------------------------------------------------------------------------------

const worst = rows.reduce((a, b) => (b.rsd > a.rsd ? b : a));
const failing = rows.filter((r) => r.rsd > RSD_DISCLOSE);
const disclosing = rows.filter((r) => r.rsd > RSD_VERIFIED && r.rsd <= RSD_DISCLOSE);
const verdict = failing.length ? "failed" : disclosing.length ? "verified-with-disclosure" : "verified";

// ---- write -----------------------------------------------------------------------------------

mkdirSync(artifactsDir, { recursive: true });
writeFileSync(path.join(artifactsDir, `${name}.json`), JSON.stringify(aggregate, null, 2), "utf8");

const fixed = (v, n = 2) => v.toFixed(n);
const summary = [
  `# JS stability summary — \`${name}\``,
  "",
  `Stability verdict computed from **${passes.length} consecutive passes** of`,
  `\`bench/${name}.mjs\`, each a separate node process. Thresholds are per cell on the`,
  "cross-pass RSD of mitata's `avg`: ≤ 5% verified; 5–10% publishable but the report must carry",
  "this table; > 10% blocks publication.",
  "",
  `STABILITY: ${verdict}`,
  "",
  `- passes: ${passes.length} (${files[0]} … ${files[files.length - 1]})`,
  `- cells: ${cellCount}`,
  `- cross-pass RSD: median ${fixed(median(rows.map((r) => r.rsd)))}% · max ${fixed(worst.rsd)}% (${worst.alias}, cell ${worst.index})`,
  `- cells above 5%: ${disclosing.length} · above 10%: ${failing.length}`,
  "",
  "| # | Engine | Published (median of passes) | Cross-pass min … max | RSD |",
  "| ---: | --- | ---: | --- | ---: |",
  ...rows.map(
    (r) =>
      `| ${r.index} | ${r.alias} | ${fixed(r.med, 1)} ns | ${fixed(Math.min(...r.avgs), 1)} … ${fixed(Math.max(...r.avgs), 1)} ns | ${fixed(r.rsd)}% |`,
  ),
  "",
  "The published artifact `" + name + ".json` carries this aggregate: `stats.avg` is the median",
  "of the per-pass averages and `stats.min`/`stats.max` span the passes, so its dispersion is",
  "cross-process variation. Per-cell provenance is under `stats.aggregate`.",
  "",
].join("\n");
writeFileSync(path.join(artifactsDir, `stability-summary-${name}.md`), summary, "utf8");

// The human-readable capture and the machine-readable numbers must describe the SAME
// data, so the published tables and artifacts cannot disagree. Each pass still writes both views
// of itself (under stability/<name>/run-<k>.txt|.json), but `writeArtifacts` also leaves
// artifacts/<name>.txt holding the LAST pass while the JSON beside it is now the aggregate.
// Overwrite it so the pair matches again, and point at the per-pass captures.
writeFileSync(
  path.join(artifactsDir, `${name}.txt`),
  [
    `# ${name} — aggregate of ${passes.length} passes`,
    "#",
    "# This is NOT a single mitata run. The published statistic is the median of the per-pass",
    "# `avg` values and the dispersion is their min…max spread, so the interval below is",
    "# cross-process variation. Each pass's own mitata-format capture and JSON — the two views",
    `# every run must publish — are under stability/${name}/run-<k>.txt|.json.`,
    "#",
    `# STABILITY: ${verdict} (cross-pass RSD median ${fixed(median(rows.map((r) => r.rsd)))}%, max ${fixed(worst.rsd)}%)`,
    `# Full verdict table: stability-summary-${name}.md`,
    "",
    ...rows.map(
      (r) =>
        `${String(r.index).padStart(2)}  ${r.alias.padEnd(12)} ${fixed(r.med, 1).padStart(14)} ns` +
        `   ${fixed(Math.min(...r.avgs), 1)} … ${fixed(Math.max(...r.avgs), 1)} ns   RSD ${fixed(r.rsd)}%`,
    ),
    "",
  ].join("\n"),
  "utf8",
);

console.log(
  `aggregate: ${passes.length} passes → artifacts/${name}.json ` +
    `(median of per-pass avg; dispersion = cross-pass min…max)`,
);
console.log(
  `aggregate: cross-pass RSD median ${fixed(median(rows.map((r) => r.rsd)))}% · ` +
    `max ${fixed(worst.rsd)}% (${worst.alias}, cell ${worst.index})`,
);
console.log(`STABILITY: ${verdict}`);
if (failing.length) {
  for (const r of failing) {
    console.error(`aggregate: cell ${r.index} (${r.alias}) RSD ${fixed(r.rsd)}% exceeds ${RSD_DISCLOSE}%`);
  }
  die(`stability verdict is 'failed' — ${failing.length} cell(s) above ${RSD_DISCLOSE}% cross-pass RSD`);
}
