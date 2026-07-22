# JS harness, measurement run, and publication

Supplementary document of the [Phase 4 — js spec](README.md). It pins the harness project
layout and dependency story, the JS gate implementation, the mitata run shape and flag
handling, the Windows stability verification procedure, and the report format — including the
verbatim texts the published report must carry. Template texts and models are in
[templates-and-models.md](templates-and-models.md); the contract and protocol being implemented
are Phase 1's [parity-contract-v2.md](../phase-1-cross-stack-foundation/parity-contract-v2.md)
and [metrics-protocol.md](../phase-1-cross-stack-foundation/metrics-protocol.md).

## Harness layout

```
benchmarks/js/
  package.json                 ← pins (below); "private": true; "type": "module"; npm scripts
  package-lock.json            ← committed; npm ci is the only sanctioned install
  .npmrc                       ← engine-strict=true / save-exact=true
  .gitignore                   ← node_modules/, artifacts/
  .gitattributes               ← * text eol=lf   (deterministic template bytes on checkout)
  README.md                    ← pointer to this spec + reproduce commands
  run.ps1                      ← launcher: priority class, output capture, -Repeat mode
  bench/
    controlled.mjs             ← gate → register 16 benches → run() → artifacts
    idiomatic.mjs              ← verifier → register 16 benches → run() → artifacts
    cold-compile.mjs           ← gate (reuses controlled outputs) → 16 cold benches → artifacts
    _shared.mjs                ← registration helpers, artifact writer, DEOPT-CHECK trailer
  src/
    models/<id>.mjs            ← 8 frozen models (templates-and-models.md)
    templates/
      handlebars/controlled/   ← 8 × .hbs + layout.partial.hbs + tile.partial.hbs
      handlebars/idiomatic/    ← mirror set with citation headers
      eta/controlled/          ← 8 × .eta + layout.partial.eta + tile.partial.eta
      eta/idiomatic/           ← mirror set + shell.layout.eta + page.layout.eta
    engines/
      handlebars.mjs           ← per-track environment factories + render table
      eta.mjs                  ← per-track Eta instances + render table
      index.mjs                ← aggregates both into the per-track `renderers` table (renderers.handlebars[id] / renderers.eta[id])
    gate/
      corpus.mjs               ← loads GoldenCorpus files + manifest + verify.json
      normalize.mjs            ← N1–N5
      controlled.mjs           ← byte gate + security floor
      verifier.mjs             ← idiomatic verifier
      run-all.mjs              ← `npm run gate`: both gates, all 32 cells, [PASS]/[FAIL] lines
  test/
    gate-selftest.mjs          ← normalization fixtures + calibration re-run
  artifacts/                   ← gitignored run outputs (copied into docs/benchmarks/<date>/)
```

The corpus is read from `src/Heddle.Performance/GoldenCorpus/` via a repo-relative path
resolved from `import.meta.url` (the harness never copies corpus files — one source of truth).

## Dependency pinning and install story

`package.json` (normative content; formatting free):

```json
{
  "name": "heddle-benchmarks-js",
  "private": true,
  "type": "module",
  "engines": { "node": "24.18.0" },
  "dependencies": {
    "eta": "4.6.0",
    "handlebars": "4.7.9",
    "mitata": "1.0.34"
  },
  "scripts": {
    "gate": "node src/gate/run-all.mjs",
    "bench:controlled": "node --expose-gc --allow-natives-syntax bench/controlled.mjs",
    "bench:idiomatic": "node --expose-gc --allow-natives-syntax bench/idiomatic.mjs",
    "bench:cold": "node --expose-gc --allow-natives-syntax bench/cold-compile.mjs",
    "selftest": "node test/gate-selftest.mjs"
  }
}
```

`.npmrc`: `engine-strict=true` and `save-exact=true`. `package-lock.json` is committed at WI1
and only changes when a pin changes. Install/reproduce story, exactly: clone → install Node
24.18.0 → `cd benchmarks/js` → `npm ci` → `npm run gate` → `npm run bench:*` (via `run.ps1`
for published runs). No global npm packages, no postinstall scripts are relied on (none of the
three pinned packages declares install scripts at the pinned versions). Version-bump policy:
README D2 (bump pin + lockfile in a commit that precedes the measurement run).

`run.ps1` (normative behavior): starts
`node --expose-gc --allow-natives-syntax <script>` with stdout captured to
`artifacts/<name>.txt` (in addition to the JSON the script writes itself), sets the process
priority class to **High** immediately after start, waits for exit, propagates the exit code;
`-Repeat N` runs the same invocation N times sequentially into
`artifacts/stability/run-<k>.txt/.json` (used by the stability procedure).

## Gate implementation

`src/gate/normalize.mjs` implements contract v2's closed pipeline, exactly:

- **N1** — input is the engine's returned JS string (already decoded); the *byte* semantics
  enter at comparison time: the normalized string is encoded with `TextEncoder` (UTF-8, never
  emits a BOM). A leading U+FEFF in the candidate string is **not** stripped (it survives and
  fails the compare). Well-formedness: the candidate must contain no lone surrogates
  (`str.isWellFormed() === true`), else the gate fails as invalid-UTF-8.
- **N2** — `replaceAll("\r\n", "\n")` then `replaceAll("\r", "\n")`.
- **N3** — single pass `replace(/>[\t\n\v\f\r ]+</g, "><")` — the contract's six-character
  whitespace class written explicitly (never `\s`); a single pass is equivalent because the
  replacement contains no whitespace (contract note carried).
- **N3b** — `replace(/[\t\n\v\f\r ]+/g, "")` — remove every whitespace run anywhere to **nothing**
  (not to a space; the 2026-07-20 maintainer step; same six-character class). Applied at comparison
  to **both** the candidate's normalized output and the loaded oracle, so the gate compares
  non-whitespace bytes. Consequence: any whitespace-only divergence — run-length or
  presence-vs-absence — passes the controlled gate. (N3/N4 shape the stored oracle; N3b subsumes
  them at comparison.)
- **N4** — trim leading/trailing characters of the same six-character class (explicit
  regex `/^[\t\n\v\f\r ]+|[\t\n\v\f\r ]+$/g`, not `String.trim()`, which strips wider Unicode).
- **N5** *(encoded suites only)* — single left-to-right scan replacing every recognized
  spelling of the five characters with the canonical spelling, output never rescanned.
  Implementation: one regex alternation over named entities (`&amp;`, `&lt;`, `&gt;`,
  `&quot;`, `&apos;` — case-sensitive) and numeric forms
  (`&#0*(38|60|62|34|39);` and case-insensitive `&#x0*(26|3C|3E|22|27);`), mapping to
  `&amp; &lt; &gt; &quot; &#39;` per the contract table. Applied to **all** encoded
  candidates of both engines (identity on Eta output; maps Handlebars' `&#x27;`).

`src/gate/controlled.mjs`: per cell — render once, normalize (N1–N5), then N3b-strip both the
candidate output and the loaded corpus text, `TextEncoder` encode, compare the resulting
`Uint8Array`s; on mismatch throw with workload, engine, expected/
actual byte lengths, first-diff index, and a ±40-char excerpt (`\n`-escaped) — the contract's
failure surface. Encoded cells additionally run the security floor on the **un-normalized**
output: `<script>alert(` count must be 0 and the escaped form's expected count must match the
corpus-derived count. Also verifies each corpus file's SHA-256 against `manifest.json` before
use (corrupted-checkout guard; mismatch aborts with a distinct message).

`src/gate/verifier.mjs`: implements the four check kinds over the whitespace-stripped projection of
the normalized candidate (N1–N5, then N3b-strip) with the same strip applied to each needle string,
exactly as contract v2's idiomatic gate defines: `values` exact non-overlapping counts, `markers`
strictly-ordered search, `forbidden` zero occurrences in raw **and** (stripped) normalized output,
`required` minCounts; failure reports kind + entry +
expected-vs-found. Definitions are loaded from the corpus `<id>.verify.json` files — never
re-authored in JS.

`test/gate-selftest.mjs`: fixtures for each pipeline step (CRLF, BOM survival, lone-surrogate
rejection, inter-tag collapse incl. multi-run, N3b removal of a non-tag-boundary whitespace run to
nothing incl. a space-vs-nothing pair that must compare equal, edge trim, every N5 spelling variant
incl. leading zeros and hex case)
plus the calibration re-run: for each workload, verifier accepts
the committed golden and rejects the Phase 1 canonical corruptions (removed row / reordered
sections for all; unescaped payload for encoded) synthesized by the same rules as
`verify-corpus` (golden-corpus.md §Verification).

## mitata run shape

Both track scripts follow the same skeleton (README D10–D12):

```js
import { bench, group, run, do_not_optimize } from "mitata";
import { assertControlledGate } from "../src/gate/controlled.mjs";  // or verifier
import { renderers } from "../src/engines/index.mjs";               // per-track render table

assertControlledGate("controlled");   // throws → exit 1 → no numbers (contract rule 2)

for (const id of WORKLOAD_IDS) {
  group(`${id} [controlled]`, () => {
    bench("handlebars", () => do_not_optimize(renderers.handlebars[id]()));
    bench("eta", () => do_not_optimize(renderers.eta[id]()));
  });
}

const result = await run();           // default 'mitata' format → stdout (captured by run.ps1)
writeArtifacts("controlled", result); // JSON: result.benchmarks, BigInt→string replacer
deoptCheckTrailer();                  // scans in-process capture buffer for '!' → DEOPT-CHECK line
```

Normative points:

- **Flags:** every measurement invocation uses `--expose-gc --allow-natives-syntax` (mitata's
  canonical set; enables GC control between benches and the optimization-status/DCE marker).
- **Warmup/sampling:** mitata's automatic warmup and sampling, unmodified — no manual warmup,
  no custom min/max iteration settings. (Per-harness stability settings recorded in the report:
  the flag set, priority class, power plan, and this "harness defaults" statement.)
- **Statistic:** the published wall time per render is mitata's `avg`, per [Phase 1 README D12
  — Wall-time statistic mapping (Q2.1)](../phase-1-cross-stack-foundation/README.md#d12--wall-time-statistic-mapping-q21)
  (which reads it off the protocol's mapping table); dispersion published alongside is the
  printed spread (`min … max`, `p75`, `p99`) as emitted.
- **Renderers:** `renderers.handlebars[id]` closes over the compiled template function
  (controlled: `hb.compile` product; idiomatic: `Handlebars.template(spec)` product) and the
  frozen model; `renderers.eta[id]` is `() => eta.render("@" + id, model)`. Compilation,
  registration, and gating all complete before the first `bench()` registration.
- **Artifacts per run:** `<track>.txt` (captured default-format output) and `<track>.json`
  (`JSON.stringify(result.benchmarks, bigintReplacer, 2)`) — one process, one sample set, two
  views. Cold-compile script: same skeleton; benches are the D14 fresh-environment
  compile+first-render closures, group `cold-compile [per-ecosystem]`.
- **DEOPT-CHECK (D12):** the scan is done **in-process**, not by reading the externally
  redirected `.txt` (which run.ps1 is still writing at trailer time and cannot be relied on to
  be flushed). `_shared.mjs` accumulates mitata's rendered output in-process — a `print` tap
  fed to `run()` so the same lines that reach stdout are also collected in a buffer — and scans
  that buffer for the `!` marker, emitting the final line `DEOPT-CHECK: clean` or
  `DEOPT-CHECK: flagged <bench names>`. The publication checklist consumes it; the `.txt`
  capture remains the human-readable mirror only.

## Windows stability verification

Executed once on the protocol machine before the first published run (README D13; WI7).
Stability is a property of the mitata harness on this box, not of any one suite, so the
controlled track — the heaviest and most representative set, exercising both engines across all
eight workloads under the same `run()` machinery the idiomatic and cold-compile runs use — is
the stand-in that gates all three published run types; verifying it verifies the harness the
idiomatic and cold-compile runs share.

**Preconditions (recorded in `stability-summary.md` and the report's environment notes):**
Windows 11 protocol box (Ryzen 9 9950X), High-performance power plan, no interactive use, no
other benchmark/build processes; launcher `run.ps1` (High priority class); pinned Node and
packages (`npm ci` beforehand).

**Procedure:**

1. `./run.ps1 bench/controlled.mjs -Repeat 5` — five consecutive full controlled-suite runs;
   captures land in `artifacts/stability/run-1..5.{txt,json}`.
2. A summarizer step (small script or manual table — the JSON artifacts carry `avg` per bench)
   computes, per benchmark cell, mean, stddev, and **RSD = stddev/mean** of the five `avg`
   values, into `stability-summary.md`.
3. Verdict per README D13 thresholds: all cells ≤ 5% → `STABILITY: verified`; any cell in
   (5%, 10%] after one investigate-and-re-run cycle → `STABILITY: verified-with-disclosure`
   (the RSD table must be published in the report); any cell > 10% persisting →
   `STABILITY: failed` → no publication; evidence recorded; tinybench amendment proposed via
   the cross-spec amendments ledger.
4. Any `!` marker inside stability runs is handled by the D12 procedure before the verdict is
   recorded (an optimized-out bench would fake stability).

The five stability captures and the summary ship with the published report (immutable
evidence for the "spec-verified before any published run" plan criterion).

## Report format

One new directory `docs/benchmarks/<yyyy-MM-dd>/` in the protocol's publication format;
`index.md` sections in the protocol's order, with these phase bindings:

1. **Intro** — suites covered (JS controlled, JS idiomatic, JS cold-compile) and the reproduce
   commands verbatim:
   `cd benchmarks/js && npm ci && npm run gate && ./run.ps1 bench/controlled.mjs`
   (and the idiomatic/cold variants).
2. **`## Environment`** — fenced block recording: mitata 1.0.34; OS name + build; CPU model;
   `node --version` (24.18.0) and `process.versions.v8`; handlebars 4.7.9; eta 4.6.0; repo
   commit; launcher settings (High priority class, High-performance power plan, flags
   `--expose-gc --allow-natives-syntax`, "mitata defaults — no custom warmup/sampling");
   stability verdict line and, if `verified-with-disclosure`, the RSD table.
3. **`## The workloads`** — one bullet per workload: id, owned dimension (from Phase 1
   workloads.md), suite, and the gate that ran (controlled byte gate / idiomatic verifier);
   the **encoded confinement caveat** (protocol verbatim text) beside the encoded entries.
4. **`## Results — what this run actually shows`** — in order:
   - **Framing statement, first paragraph, verbatim:**
     > *JS/Node results in this report are reach and context evidence, not fair-fight
     > evidence: both engines measured here are dynamically typed template engines, and no
     > architectural peer to Heddle — an AOT-compiled, typed-model engine — exists in this
     > ecosystem. A Heddle wall-time win here is the expected outcome and is not presented as
     > proof of engineering superiority; the fair-fight evidence lives in the Rust and JVM
     > reports. What these numbers show is how Heddle compares against what the largest
     > practitioner audience actually uses.*
   - **Controlled-track table** (caption names the track): one row per workload per engine,
     columns: `avg` in mitata's native unit, spread (`min … max`, `p75`, `p99`), ns/render,
     and `ratio vs Heddle`. Plus the labeled row
     `Heddle (reference — .NET 10, same machine, from <date> run)` per workload, wall-time
     only, sourced from the Phase 1 protocol publication; ratio column anchors to it (Q6.2).
   - **Time-only statement** (README D3 verbatim text) immediately after the first table.
   - **Idiomatic-track table**, same shape, same Heddle reference rows; a methodology line
     states the Handlebars execution mode split (runtime-compile vs precompiled
     `knownHelpersOnly`) and that Eta idiomatic uses the native layout system.
   - **Helper disclosure text** (templates-and-models.md verbatim) in the controlled
     narrative.
   - **Prose callouts** per honest-reporting rule 2 and the plan criteria: every workload
     where Handlebars or Eta beats the other **or beats Heddle** on wall time, named with
     numbers, as prominently as Heddle wins.
   - **Cold-compile table** (per-ecosystem): cold compile + first render per workload per
     engine, baseline = Handlebars, with the Q1.3 label: *"Cold parse/compile figures are
     per-ecosystem only and not comparable across ecosystems: AOT compilation and runtime
     parse/compile are different operations."* Placed after the track tables, before the
     side-note.
   - **`### Side-note: the same templates on two runtimes — Handlebars (V8) vs
     Handlebars.Net (.NET)`** — last subsection of Results, outside and below every
     engine-ranking table (Q4.2 placement per Q2.2 = A). Framing text, verbatim:
     > *The controlled-track Handlebars templates in this run are byte-parity twins of the
     > Handlebars.Net templates in the intra-.NET suite: the same template language, the same
     > workloads, byte-identical normalized output against the same golden corpus, measured on
     > the same machine under each ecosystem's standard harness. The wall-time comparison
     > below is port/runtime context — how one template dialect performs across two runtimes —
     > and is not part of this report's ecosystem verdict. It is not an engine ranking and not
     > a .NET-vs-Node runtime shootout: the two implementations are independent codebases that
     > share a template language, and wall time is the only metric that may be juxtaposed
     > across runtimes under this program's protocol.*
     Table: workload | Handlebars (JS, this run, ns/render) | Handlebars.Net (from the
     Phase 1 run `<date>`, ns/render). Wall-time only; no ratio column feeding any ranking; no
     allocation cells. Both compile modes are runtime-compile (README D7) — stated under the
     table.
   - **Handlebars reach figure** wherever the pick is motivated: the current-week
     `api.npmjs.org/downloads/point/last-week/handlebars` figure with its week range
     (spec-verified baseline: 39,494,320 for 2026-07-13 → 2026-07-19).
5. **`## Files`** — links to `js-controlled.txt/.json`, `js-idiomatic.txt/.json`,
   `js-cold-compile.txt/.json`, `stability-summary.md` (+ the five stability captures).

**Publication checklist** (WI8's completion check; every item required):
framing statement present and first; Heddle reference rows labeled with the exact protocol
format and ratio anchored; time-only statement verbatim; encoded caveat verbatim beside
encoded results; cold-compile label verbatim; helper disclosure verbatim; side-note present,
last, correctly titled and framed; loses-reported-as-prominently prose check; download figure
current and sourced; `DEOPT-CHECK: clean` for all three runs or the flagged cells documented
in the narrative; stability verdict `verified` or `verified-with-disclosure` (with table);
environment block complete; reproduce commands exact; no allocation/GC/memory number anywhere
in a table (time-only); no cross-ecosystem non-Heddle comparison anywhere.
