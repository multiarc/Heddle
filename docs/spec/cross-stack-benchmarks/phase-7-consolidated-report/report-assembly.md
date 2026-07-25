# Consolidated report — format and assembly mechanics

Supplementary document of the [Phase 7 — consolidated-report spec](README.md). Everything here
is **normative**: the published directory's contents, the exact `index.md` structure, table
shapes and provenance format, figure extraction and spot-check procedures, the `sources.json`
schema, the `consolidate.py` contract, the engine classification tables, the counted-claims and
claim-downgrade rules, the drift-review and re-run-request procedures, the verbatim texts, and
the publication checklist. Decision rationale lives in the [README](README.md) (D1–D14); this
document is the instantiation an implementer executes.

Throughout, `<date>` is the report's publication date and `../<run-date>/` denotes a source run
directory reached relatively from inside `docs/benchmarks/<date>/`.

## Published directory contents

`docs/benchmarks/<date>/` contains exactly four files (README D2):

| File | Content | Produced by |
|---|---|---|
| `index.md` | The consolidated report (structure below) | WI4 (authored; tables pasted from `consolidated-tables.md`) |
| `sources.json` | Frozen machine-readable inclusion manifest | WI1 |
| `consolidate.py` | The assembly script (contract below) | WI3 |
| `consolidated-tables.md` | The script's verbatim table output — byte-reproducibility anchor | WI3 (`consolidate.py` output, committed) |

No other file. No file outside the directory changes at publication. The directory is immutable
once published; corrections are a complete new dated directory stating what it corrects.

## `index.md` — normative structure

Sections in this exact order. Ecosystem order everywhere: **.NET, Rust, JVM, JS, Python, Go**
(absent ones simply missing from tables, present in the manifest). Workload order everywhere:
the protocol order 1–8 (`composed-page`, `trivial-substitution`, `large-loop`, `mixed-page`,
`conditional-heavy`, `fragment-heavy`, `fortunes-encoded`, `encoded-loop`).

1. **H1** — `# Cross-stack consolidated report — <date>`.
2. **Intro** (no heading) — required statements, each present:
   - what this document is (the program's single consolidated cross-language comparison) and
     what it is not — **amended 2026-07-25:** it is not a *league table*: it carries
     per-workload rankings but no aggregate score, no overall winner, and no cross-workload
     average of any kind (D6(c), unchanged), so no single-number verdict can be quoted from it;
   - **no-new-measurements statement**: every figure is a verbatim excerpt of an
     already-published protocol-conformant run; this report measured nothing and derived no
     new metric (the only arithmetic: ns/render unit conversion, and a Heddle ratio only where
     a source table lacked one, flagged per table);
   - links to the metrics & publication protocol and parity contract v2 (as published paths
     from the report: `../../spec/cross-stack-benchmarks/...`);
   - **gate description** naming the amended pipeline: controlled cells passed the byte gate
     under the closed normalization list **N1, N2, N3, N3b, N4, N5** — including N3b, the
     program-wide comparison-time whitespace strip that removes **every** whitespace run,
     anywhere, to **nothing** from both the oracle and the candidate, so the gate compares
     **non-whitespace bytes only** (maintainer decision 2026-07-20, recorded in Phase 1 D8 and
     parity-contract-v2 §normalization-pipeline — *not* the
     superseded "collapse to a single space" form); "byte-identical" therefore means
     byte-identical after that disclosed pipeline, with entity spellings canonicalized by N5;
     idiomatic cells passed the functional-equivalence verifier;
   - the pre-protocol history sentence: the 2026-07-11 and 2026-07-18 reports are the
     intra-.NET historical record, cited as motivation, **not aggregated**.
3. **`## Inclusion manifest`** — format below.
4. **`## Source environments, drift, and reproduce commands`** — format below.
5. **`## How to read these tables`** — required content:
   - the two **track meaning statements**, verbatim (see [Verbatim texts](#verbatim-texts));
   - the statistic rule: each figure is its harness's default central-tendency point estimate
     with the harness-native dispersion alongside (Q2.1), reprinting the protocol's mapping
     table (harness → statistic → dispersion) for the included ecosystems;
   - the ratio rule: every ratio column anchors to the Heddle reference row
     (`ratio = engine wall time ÷ Heddle reference wall time`; < 1.00 means the engine beat
     the Heddle reference);
   - the **ranking-scope rule** (as amended 2026-07-25 by the [D6
     amendment](README.md#d6--ranking-scope-enforcement-q62-and-the-no-score-rule-made-checkable)):
     Heddle may be compared against any engine, wall-time-only. Per-ecosystem tables are one
     ecosystem's engines plus the Heddle row, and remain the normative view. In *addition*,
     each workload publishes one cross-stack ranked table spanning ecosystems, carrying an
     evidence-class column and an implied-throughput column; it is a per-workload ranking
     only — **no aggregate score, overall winner, or cross-workload average exists anywhere**
     (D6(c)). The report's *prose* still never ranks two non-Heddle engines against each
     other across ecosystems (D6(b)): the narrative is Heddle-anchored throughout;
   - the evidence-class rule: Rust/JVM/Go tables are fair-fight evidence (compiled peers /
     same-class engines); JS/Python tables are reach/context evidence, not a fair fight;
   - the counted-claims convention (tag format below) so readers can parse the analysis
     section's evidence tags.
6. **`## Controlled track — wall time per render`** — table shapes below; one `###` per
   workload; encoded workload subsections carry the confinement caveat verbatim directly
   below their tables.
7. **`## Idiomatic track — wall time per render`** — same shape; opens with one line stating
   that .NET shipped controlled-track-only (Phase 1 D15), so these tables carry the Heddle
   reference row and phases 2–6 engines only.
8. **`## Workload-shape analysis`** — eight `###` subsections, requirements below.
9. **`## Findings — where Heddle wins, loses, and narrows`** — requirements below; ends with
   the no-universal-superiority statement verbatim.
10. **`## Per-ecosystem sidebars — not cross-comparable`** — one `###` per included
    ecosystem, contents per README D7; every sidebar containing allocation/memory data opens
    with the protocol's verbatim allocation label; every sidebar ends with its source-report
    link.
11. **`## Limitations`** — the caveat register below, instantiated for the included subset.
12. **`## Files`** — the four directory files with one-line purposes; the reproduce statement
    for the report itself (`python consolidate.py --check`); a link list of every aggregated
    source run directory; the spot-check statement (which cells were verified against raw
    artifacts, per the procedure below).

## Inclusion manifest format

A single table, one row per ecosystem plus the out-of-scope row, followed by the excluded-cell
list and (if applicable) the multi-reference-run disclosure.

| Column | Content |
|---|---|
| Ecosystem | `.NET (intra — source of the Heddle reference)`, `Rust`, `JVM`, `JS/Node`, `Python`, `Go`, `PHP / Ruby` |
| Status | `included` / `absent` / `out of scope — program-level non-goal` |
| Reason (absent/out-of-scope only) | closed vocabulary: `phase cut` · `run not yet published` · `run not protocol-conformant (R1)` · for PHP/Ruby: link to the recorded rationale in [Phase 1's plan](../../../plan/phase-1-cross-stack-foundation.md#non-goals--scope-boundary) |
| Source run | `[<run-date>](../<run-date>/index.md)` |
| Commit | `<shorthash>` from the run's environment block |
| Engines | the run's measured engines with role labels (e.g. `Askama (performance/peer), Tera (credibility)`); Go stdlib listed as `html/template + text/template (one engine, two surfaces — Q6.1)`; quicktemplate listed iff shipped |
| Tracks | `controlled + idiomatic`; `.NET: controlled only (Phase 1 D15)` |
| Heddle reference run | the Phase 1 run date this ecosystem's report cites |

Below the table, required:

- **Excluded cells** — one bullet per contract-v2 excluded controlled cell across all included
  runs: `<workload> / <engine> / <track> — excluded — documented evidence: <one-line reason>`
  with a link to the owning run's evidence record. "None" is stated explicitly if none exist.
- **Multi-reference-run disclosure** (only if rule R5 fired): which ecosystems cite which
  Phase 1 run, with the sentence that cross-ecosystem shape comparisons involving the affected
  ecosystems carry the R5 caveat.
- **Older protocol runs** (only if the newest-run rule skipped any): listed as history with
  dates, not aggregated.

## Source environments, drift, and reproduce commands — format

1. **Environment summary table** — one row per aggregated run: ecosystem · run date (link) ·
   commit · OS build · CPU · harness + version · runtime/toolchain line · recorded stability
   posture (e.g. `High priority, no affinity` / `REALTIME + --affinity=4, elevated shell` /
   `no priority manipulation`). Values transcribed from each run's environment block; the full
   fenced block is one link away and is never re-typed here.
2. **Reproduce commands** — per run, its reproduce command(s) copied **verbatim** from its
   `index.md` intro (e.g. the `dotnet run … --filter *<Suite>*` forms; `cargo run --release
   --bin gate` + `cargo bench -- --noplot`; the `java -jar benchmarks.jar …` line; the
   `node --expose-gc --allow-natives-syntax bench/….mjs` lines; the elevated-shell pyperf
   invocations; `run-benchmarks.ps1`). This report's own reproduce command:
   `python consolidate.py --check` inside this directory.
3. **Drift table** — one row per ordered pair finding from the WI2 review worth recording,
   plus a summary line when clean: rule id (R1–R5) · runs compared · differing lines (quoted
   from the environment blocks) · verdict (`not material — disclosed` / `material — re-run
   published <date>` / `material — waived by maintainer <date>`) · placed caveat (where in
   this report the caveat appears, for material-waived findings).

## Table shapes, captions, and provenance

**Wall-time table (per workload, per ecosystem, per track).** Caption line above the table:

```
**<workload-id> — <ecosystem> (<track>) — <fair-fight | reach/context> evidence**
```

with `— reach/context evidence (not a fair fight)` for JS and Python and `— fair-fight
evidence` for Rust, JVM, Go; the .NET table caption reads `— intra-.NET (source of the Heddle
reference)`. Columns, closed set:

| Engine | harness statistic | harness dispersion | ns/render | vs Heddle |
|---|---|---|---|---|

- Row 1 is always `Heddle (reference — .NET 10, same machine, from <run-date> run)` with wall
  time and ns/render populated, dashes in the dispersion column if the source printed the
  Heddle excerpt without one, and `1.00` in the ratio column. (In the .NET table Heddle is the
  measured baseline participant, same label convention as the Phase 1 run.)
- Remaining rows: that ecosystem's engines only, in the source table's order, with role/surface
  labels carried (`JTE (performance/peer)`, `text/template` / `html/template` per suite, etc.).
- The statistic/dispersion column headers name the harness pair (`Mean (BenchmarkDotNet)` +
  `Error/StdDev`; `mean (Criterion)` + `95% CI`; `Score avgt (JMH)` + `Error (99.9% CI)`;
  `avg (mitata)` + `min … max / p75 / p99`; `mean (pyperf)` + `std dev` — Python rows also
  carry the median as printed; `sec/op (benchstat)` + `±%`). Values are printed exactly as the
  source table printed them (same unit, same significant digits).
- An excluded cell prints `excluded — documented evidence` across its numeric columns, linking
  the evidence record. No blank cells, no dropped rows.
- **No other column may ever appear** — allocation, memory, cold-cost, significance, and any
  derived column are banned here.

**Provenance note** — the line directly under every table:

```
*Source: [<ecosystem> run <run-date>](../<run-date>/index.md), commit `<shorthash>` — figures
verbatim from its "<source table caption>" table; raw artifacts: <artifact file name(s)> in
the same directory.<flags>*
```

`<flags>` is empty, or names the applied D4 transformation(s):
` ns/render converted from <unit> by this report.` and/or ` Ratio computed by this report
(source table published none): engine ns ÷ Heddle reference ns.`

**Sidebar tables** reproduce the source run's non-comparable tables verbatim (columns, values,
within-ecosystem baseline unchanged) and use the same provenance-note format.

## Figure extraction and the spot-check layer

Primary source (README D4): the published source `index.md` tables. The raw artifacts below
are the **spot-check layer**. Spot-check procedure (WI5): for every included ecosystem × track,
take the deterministic cell set — **the first and the last workload row of each engine in the
consolidated table order** — and verify the source `index.md` figure against the raw artifact
per this table. Any mismatch is a D14 escalation and blocks publication.

| Ecosystem | Raw artifact (in the source run directory) | Where the figure lives |
|---|---|---|
| .NET | `<Suite>-report.csv` (BenchmarkDotNet triplet) | `Mean`, `Error`, `StdDev` columns of the benchmark's row |
| Rust | `criterion/<group>/<fn>/estimates.json` | `mean.point_estimate` (ns), `mean.confidence_interval.lower_bound` / `upper_bound` |
| JVM | `jmh-result.json` | the benchmark entry's `primaryMetric.score` and `scoreError` (avgt, ns/op) |
| JS | `js-<track>.json` (mitata JSON artifact) | the bench entry's average and percentile fields for `group '<workload> [<track>]'` / bench name; the printed `js-<track>.txt` capture is the authoritative rendering of the spread *(verify at implementation: exact JSON field names against the committed artifact — the mitata text artifact governs on any doubt)* |
| Python | `bench_<engine>_<track>.json` | `python -m pyperf stats <file>` for the named benchmark: mean, std dev, median |
| Go | `benchstat-*.txt` | the cell's `sec/op` summary and `±%` for `BenchmarkRender/<track>/<workload>/<engine>`, where `<engine>` is the benchstat id (`stdlib-text` for raw-suite `text/template` rows, `stdlib-html` for encoded-suite `html/template` rows, `templ`; per Phase 6 Q6.1) — map the consolidated surface label back to this id for the lookup |

Sidebar spot-check sources: .NET allocation columns (`-report.csv` `Allocated`), Rust
`alloc-report.txt`, JVM `jmh-result.json` `gc.alloc.rate.norm` secondary metric, JS
`js-cold-compile.json`, Python `memory.json`, Go `bench-*.txt` `B/op`/`allocs/op` lines.

The published-table ↔ consolidated-table layer is checked **totally**, not spot-checked:
`consolidate.py --check` (below).

## Engine classification tables

The workload-shape analysis uses exactly these classes. Only cross-stack cast engines plus
Heddle count toward evidence tags (intra-.NET twins are citable context, never counted in n).
Rows exist only for engines whose ecosystems shipped; quicktemplate joins the AOT row iff its
rows shipped.

**Compilation model**

| Class | Engines |
|---|---|
| AOT-compiled / typed codegen | Heddle (.NET), Askama (Rust), JTE (JVM), templ (Go); quicktemplate (Go, stretch) |
| No AOT typed codegen — runtime interpreter | Tera (Rust), Thymeleaf (JVM), text/template + html/template (Go) |
| No AOT typed codegen — compiles to untyped host code / bytecode at runtime | Handlebars (JS), Eta (JS), Jinja2 (Python), Mako (Python) |

**Escaping model** (encoded suite)

| Class | Engines |
|---|---|
| Compile-time contextual | Heddle, JTE |
| Runtime contextual | html/template (Go) — the cast's only one |
| Flat escaper | Askama, Tera, Thymeleaf, Handlebars, Eta, Jinja2, Mako, templ (and quicktemplate if shipped) |

**Template-language expressiveness**

| Class | Engines |
|---|---|
| Logic-less | Handlebars (JS) — the only cross-stack one (Handlebars.Net is its intra-.NET twin context) |
| Expression-bearing | all others |

**Runtime memory model**

| Class | Engines |
|---|---|
| Native, no GC | Askama, Tera (Rust) |
| Managed / GC runtime | Heddle (.NET), JTE, Thymeleaf (JVM), Handlebars, Eta (V8), Jinja2, Mako (CPython refcount + cycle GC), html/template, text/template, templ (Go) |

## Workload-shape analysis — per-subsection requirements

Eight `###` subsections, titled `### <dimension> — <workload-id>`, dimensions from
[workloads.md](../phase-1-cross-stack-foundation/workloads.md#the-set-at-a-glance):

1. `### Layout/section/component composition machinery — composed-page`
2. `### Per-render fixed-overhead floor — trivial-substitution`
3. `### Bulk iteration and output writing — large-loop`
4. `### Realistic mixture — mixed-page`
5. `### Branch evaluation and control-flow dispatch — conditional-heavy`
6. `### Per-call composition overhead — fragment-heavy`
7. `### Escaping correctness and cost at micro scale — fortunes-encoded`
8. `### Escaping throughput at scale — encoded-loop`

Required in each subsection:

- per track, which classification-table classes **led, trailed, or converged** on this
  workload, and whether that is consistent with or surprising given the class's design;
- every architectural sentence carries the **counted-claims tag**, exact format:
  `*(evidence: <engine list>; <workload-id(s)>; <controlled|idiomatic|both>; n = <k>)*`
  where `n` counts supporting engine×workload data points; **n ≤ 2 ⇒** the sentence opens
  with `Thin evidence —` or states the question as narrowed/open;
- claims supported only by JS/Python data are labeled reach/context in the sentence;
- subsections 7–8 additionally: results framed strictly as **correlation between escaping
  model and whole-engine wall time** (never an isolated cost of context analysis); the
  confinement caveat present verbatim in or immediately adjacent to the subsection; if Go
  shipped, the compile-time-vs-runtime contextual reading (Heddle/JTE vs html/template); if
  Go is absent, the could-not-be-performed statement per the downgrade rules;
- any claim depending on an absent ecosystem: the matching downgrade rule applied verbatim
  (next section);
- the Q4.2 Handlebars-vs-Handlebars.Net side-note may be cited **by link only** (one
  sentence, wall-time framing, subsections 2 or 4 or the findings prose), never re-tabulated.

## Claim-downgrade rules per absent ecosystem

Applied verbatim by WI4 for whatever the frozen subset is. "AOT peers" = non-Heddle members of
the AOT class that shipped.

| Absent | Mandatory downgrade text/behavior |
|---|---|
| Rust | The compiled-thesis claims lose Heddle's closest architectural peer (Askama): state the generalization over the remaining AOT peers and name the loss ("the closest-peer fair fight could not be run"). The **native vs managed-GC** comparison could not be performed (Rust carried the cast's only no-GC engines) — that class analysis is replaced by an explicit could-not-be-performed statement. |
| JVM | Lose JTE — one AOT peer **and** the only non-Heddle compile-time-contextual encoder: the compile-time-contextual-vs-flat reading is downgraded to Heddle-only evidence ("Thin evidence —" mandatory); the SAC 2026 caveat drops from Limitations (no JVM rows exist). |
| JS | Lose the cast's only logic-less engine: logic-less-vs-expression-bearing claims are replaced by a could-not-be-performed statement; the Q4.2 side-note reference is unavailable; reach evidence reduced to Python (if shipped). |
| Python | Reach evidence reduced to JS (if shipped); runtime-bytecode-compiler class claims count Handlebars/Eta only and say so. |
| Go | The cast's only **runtime contextual encoder** is lost: the compile-time-vs-runtime contextual-encoding comparison is replaced by the statement that it **could not be performed and why** (the caveat Phase 6 itself records), while the compile-time-contextual-vs-flat reading continues through JTE if the JVM shipped; the AOT-peer count drops by one (templ). |
| Compound: zero AOT peers shipped (Rust, JVM, Go all absent) | The compiled-thesis question ("does the compiled result generalize beyond .NET?") is stated **open — unanswered by this report**; the report is labeled reach-only evidence throughout, per Q7.2's honesty burden. |
| Compound: exactly one AOT peer shipped | Compiled-thesis claims fall under the n ≤ 2 rule wherever it applies, and the *generalization across toolchains* claim is in all cases stated as narrowed — one additional toolchain cannot generalize. |

## Findings section — requirements

1. **Coverage:** every workload × track × engine cell with published ratio < 1.00, and every
   cell whose ratio's published dispersion interval contains 1.00 ("narrows to measurement
   distance" — computed from published numbers only), is named in prose with its numbers and
   an account of what the result reveals about Heddle's design cost — as prominently as any
   win. The authoritative coverage list is `consolidate.py`'s `heddle-trails` output (an
   authoring checklist, never published as a table).
2. **Track divergence:** for each engine whose controlled and idiomatic results for a workload
   diverge notably, that divergence is reported as a finding (the cost of the engine's
   idiomatic style against its ceiling).
3. **Workload-named summaries only:** no summary sentence compares engines without naming its
   workload(s).
4. **Closing statement:** the no-universal-superiority statement, verbatim (below).

## Limitations — the caveat register

Instantiate conditionally on the frozen manifest; for every register entry whose owning run
published a text, **copy the published text** (the run is authoritative); the register is the
completeness checklist. A caveat the register expects but the run omitted → D14 escalation.

| # | Condition | Caveat carried |
|---|---|---|
| L1 | always | Encoded-suite confinement caveat, verbatim (also adjacent to every encoded table/subsection). |
| L2 | always | Dated, hardware-specific numbers: one machine (Windows 11 / Ryzen 9 9950X), per-run dates; nothing here predicts other hardware, OSes, or dates. |
| L3 | always | The drift disclosures from the WI2 table (including any R2/R5 waivers with their caveat texts). |
| L4 | always | What "byte-identical" means: the controlled gate's closed normalization pipeline N1–N5 **including N3b** (the comparison-time strip that removes every whitespace run, anywhere, to nothing from both oracle and candidate, so the gate compares non-whitespace bytes only — maintainer decision 2026-07-20, Phase 1 D8 / parity-contract-v2 §normalization-pipeline, not the superseded "collapse to a single space" form) and N5 entity-spelling canonicalization — link to the contract. |
| L5 | JVM included | The SAC 2026 disclosed-limitations note (isolated JMH microbenchmark JIT profiles), verbatim as published in the JVM run (text pinned by Phase 3 D12; reproduced in Verbatim texts below for completeness). |
| L6 | JS included | The time-only statement (mitata memory semantics undocumented); the deopt-check rule and this run's `DEOPT-CHECK` outcome; the Windows 5-run stability verdict (`verified` / `verified-with-disclosure` incl. the RSD table reference). |
| L7 | Python included | The pyperf Windows stability posture (no `system tune` on Windows; REALTIME priority + single-CPU affinity; read dispersion before assuming precision) and any quoted pyperf instability warnings, as published. |
| L8 | Rust included | The Criterion Windows posture (no priority/affinity manipulation; 10 s measurement time; the CI-half-width re-run disclosure if the Phase 2 D9 trigger fired), as published. |
| L9 | Go included | The benchstat variance posture (`-count=20`, read `±%`; High priority, no affinity — or the CCD0 pinning disclosure if the Phase 6 fallback fired), as published; the quicktemplate dormancy disclosure iff its rows shipped. |
| L10 | any sidebar with allocation/memory data | The protocol's allocation non-comparability label, verbatim, in each such sidebar (register entry here points at the sidebars; the label lives there). |
| L11 | always | Pre-protocol reports (2026-07-11, 2026-07-18) are history, not aggregated rows. |

## Verbatim texts

**Track meaning statements** (How to read, both required):

> **Controlled track** — byte-identical, equivalently-authored templates under the parity
> contract's closed normalization pipeline. These numbers answer *"how fast is the engine"* on
> identical work. They may be used to compare engine execution speed on a given workload —
> and nothing else.

> **Idiomatic track** — functionally-equivalent templates written the way each engine's own
> documentation teaches, verified by the functional-equivalence gate. These numbers answer
> *"how fast is the practitioner experience."* They may be used to compare what a practitioner
> gets writing each engine's documented style; they must not be read as engine
> execution-speed rankings.

**No-universal-superiority statement** (closes the Findings section):

> No engine measured by this program is universally fastest, and this report declares no
> overall winner. Every figure above is workload-shaped, dated, and specific to one machine
> (Windows 11, AMD Ryzen 9 9950X); a different workload mix, machine, or date can reorder any
> table here. Nothing in this report may be quoted as evidence of the universal superiority of
> any engine — Heddle included.

**Allocation label** (protocol, copied wherever allocation/GC/memory data appears):

> Allocation and GC figures are measured within one runtime and are not comparable across
> ecosystems: allocator designs differ and the numbers do not mean the same thing.

**Encoded-suite confinement caveat** (protocol, adjacent to every encoded result):

> Encoded workloads confine untrusted data to HTML text and attribute-value contexts, where
> flat and contextual encoders emit the same escaped output. These numbers therefore measure
> escaping cost in confined contexts and cannot show the differentiating benefit of contextual
> encoding; a contextual encoder's encoded-suite result must not be read as 'context awareness
> is pure overhead.'

**SAC 2026 note** (L5; the JVM run's published copy is authoritative — this is Phase 3 D12's
pinned text):

> **Disclosed limitation — isolated-microbenchmark JIT profiles.** These numbers come from
> isolated JMH microbenchmarks. Schiavio, Bulej & Binder, "Misleading Microbenchmarks on the
> JVM" (ACM SAC 2026, arXiv:2605.23570), show that isolated JMH runs can settle into
> tiered-JIT compilation profiles unrepresentative of the same code running inside a larger
> application, and that warmup iteration counts alone do not buy profile realism. The figures
> here are steady-state per-render measurements — internally consistent across the two
> engines, which are measured under the identical regime — and are not a prediction of
> in-application rendering performance.

## `sources.json` schema

One JSON object; unknown fields are a validation error. Reason and status vocabularies are
closed.

```json
{
  "schemaVersion": 1,
  "reportDate": "<yyyy-MM-dd>",
  "assembledUtc": "<ISO-8601>",
  "ecosystems": [
    {
      "id": "dotnet | rust | jvm | js | python | go | php-ruby",
      "status": "included | absent | out-of-scope",
      "reason": "phase cut | run not yet published | run not protocol-conformant (R1) | program-level non-goal",
      "runDate": "<yyyy-MM-dd>",
      "commit": "<shorthash>",
      "engines": ["<engine (role label)>", "..."],
      "tracks": ["controlled", "idiomatic"],
      "heddleReferenceRun": "<yyyy-MM-dd>",
      "artifacts": ["<file>", "..."],
      "exclusions": [
        {
          "workload": "<workload-id>",
          "engine": "<engine>",
          "track": "controlled",
          "reason": "<one line>",
          "evidence": "<relative link>"
        }
      ],
      "olderRuns": ["<yyyy-MM-dd>", "..."]
    }
  ]
}
```

Rules: `reason` present iff status ≠ `included`; `runDate`/`commit`/`engines`/`tracks`/
`heddleReferenceRun`/`artifacts` present iff `included`; `dotnet.tracks` is
`["controlled"]`; `php-ruby.status` is always `out-of-scope`; `exclusions`/`olderRuns` may be
empty arrays; every ecosystem id appears exactly once.

## `consolidate.py` contract

- **Runtime:** CPython ≥ 3.12, standard library only. Run from inside the report directory.
- **CLI:** `python consolidate.py [--check] [--repo-root <path>]` (`--repo-root` defaults to
  `../../..`, used only to resolve `docs/benchmarks/` siblings and commit existence checks via
  `git rev-parse --verify <shorthash>^{commit}` — the sole subprocess).
- **Generate mode (default):**
  1. load + schema-validate `sources.json` (error surface per the README);
  2. for each included ecosystem, read `../<runDate>/index.md`; locate the wall-time tables by
     the **pinned lookup keys** — a GFM table whose caption/preceding heading contains the
     track word (`controlled` / `idiomatic`; protocol presentation rule 5 mandates captions
     name the track) and whose rows contain the engine labels and the
     `Heddle (reference` row-label prefix; locate sidebar tables by their phase-pinned
     captions (allocation-label proximity, `cold`, `memory`);
  3. extract cells verbatim (string-preserving; no numeric reformatting); apply D4
     transformations where a needed column is absent, recording a per-table flag;
  4. emit `consolidated-tables.md`: all D5 wall-time tables (workload-major, ecosystem-minor,
     per track) and all D7 sidebar tables, each with caption and provenance note exactly per
     this document; excluded cells rendered as specified; absent ecosystems simply absent;
  5. emit the `heddle-trails` checklist to stdout (every cell with ratio < 1.00 or dispersion
     interval containing 1.00) — advisory output, never written into the published files.
- **Check mode (`--check`):** regenerate to memory and fail non-zero unless (a) output
  byte-equals committed `consolidated-tables.md`; (b) every table block (caption line through
  provenance note) appears **verbatim** in the committed `index.md`; (c) every
  `sources.json`-cited run directory exists and every commit resolves; (d) `sources.json`
  validates. Prints the first differing line / missing block on failure.
- **Determinism:** output depends only on `sources.json` and the source `index.md` files;
  re-runs are byte-identical (no timestamps in output).
- **Adaptation rule:** if a published source table defies the pinned lookup keys, the
  published run wins — the parser is adapted in this script (a reviewed change inside this
  directory, before publication) and the deviation is noted in the Files section; the source
  run is never edited.

## Drift review and re-run requests

**Procedure (WI2).** Collect the environment blocks of all included runs (including the Phase
1 reference run(s)). Evaluate each pair against R1–R5 (README D11):

| Rule | Condition | Verdict |
|---|---|---|
| R1 | CPU model / physical machine differs | run non-conformant with Q1.6 → **not aggregable**; manifest status `absent`, reason `run not protocol-conformant (R1)` |
| R2 | OS feature-update build differs (e.g. 10.0.26200 → 10.0.26300) or recorded BIOS/firmware/hardware-config change | **material** — publication blocks pending owning-phase re-run or written maintainer waiver + caveat placed next to every affected juxtaposition |
| R3 | OS servicing build (UBR) differs within one feature build | not material — disclosed in the drift table |
| R4 | Per-run stability postures differ (priority/affinity/power plan) | not material — each is its harness's recorded protocol posture; summarized in the drift table |
| R5 | Two included ecosystems cite different Phase 1 Heddle-reference runs | **material for cross-ecosystem shape claims only** — manifest disclosure + caveat in every affected shape subsection; per-ecosystem tables unaffected |

**Re-run-request file.** The first triggered R1/R2/R5 finding creates
`rerun-requests.md` in this spec folder. Entry format (append-only table):
`| <date> | <rule> | <source run> | <evidence: quoted env-block lines> | <owning phase> |
<requested action> | <disposition> |` — disposition is `re-run published <date>` or
`waived by maintainer <date> — caveat: "<text>"`. Publication is blocked while any entry's
disposition is empty. Phase 7 performs no measurement under any disposition.

## Publication checklist

Every item binary; all must pass before the publication commit (WI5). Plan success-criteria
coverage noted per item.

- **C1** — exactly one new `docs/benchmarks/<date>/` directory; the publication commit touches
  nothing else (`git status` proof). *(plan SC 1–2)*
- **C2** — the directory contains exactly the four pinned files.
- **C3** — `python consolidate.py --check` exits 0.
- **C4** — spot-check executed per the procedure (deterministic cell set, raw artifacts); all
  cells match; the Files section records the statement. *(plan SC 3)*
- **C5** — every aggregated run is a phase 1–6 protocol run on the protocol machine;
  2026-07-11/2026-07-18 appear as cited history only (manifest classification). *(plan SC 4)*
- **C6** — every wall-time table: Q2.1 statistic + dispersion + ns/render + Heddle ratio, no
  other column; no allocation/memory/cold figure appears outside the sidebars H2; no table
  contains two ecosystems' non-Heddle engines; no prose ranks non-Heddle engines across
  ecosystems. *(plan SC 5)*
- **C7** — every included ecosystem has its sidebar, opening with the verbatim allocation
  label where applicable, using the within-ecosystem credibility-pick baseline as published,
  citing its source report. *(plan SC 6)*
- **C8** — track separation: every table captioned `controlled` or `idiomatic`; no mixed
  table; the two track meaning statements present verbatim. *(plan SC 7)*
- **C9** — inclusion manifest complete: all six ecosystems + PHP/Ruby row + .NET
  controlled-only note + every excluded cell surfaced with evidence link + closed-vocabulary
  absence reasons. *(plan SC 8)*
- **C10** — workload-shape analysis: exactly eight subsections in order; every architectural
  sentence tagged; every `n ≤ 2` claim carries thin-evidence wording; downgrade rules applied
  verbatim for every absent ecosystem; encoded subsections carry the confinement caveat and
  correlation framing. *(plan SC 9)*
- **C11** — findings: every `heddle-trails` cell covered in prose with numbers;
  track-divergence findings present; the no-universal-superiority statement present verbatim.
  *(plan SC 10)*
- **C12** — no aggregate cross-workload score of any kind (grep gate: no geomean/points/medal
  construct); every summary names its workload(s). *(plan SC 11)*
- **C13** — source-environment summary table, verbatim per-run reproduce commands, and the
  drift table present; zero open re-run requests; every R2/R5 waiver's caveat placed.
  *(plan SC 12, drift scenario)*
- **C14** — limitations register instantiated correctly against the manifest (L1–L11
  conditions), texts copied from the published runs. *(plan SC 12)*
- **C15** — evidence-class labels on every table caption and carried into analysis prose
  (fair-fight vs reach/context). *(plan SC 13)*
- **C16** — every relative link resolves; the gate-description sentence names N1–N5 including
  N3b and states its corrected mechanics — every whitespace run removed to nothing at
  comparison, both sides, so the gate compares non-whitespace bytes only (maintainer decision
  2026-07-20, Phase 1 D8 / parity-contract-v2 §normalization-pipeline, not
  the superseded "collapse to a single space" form); the intro carries the no-new-measurements
  statement.
