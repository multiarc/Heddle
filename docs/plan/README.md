# Heddle cross-stack template-engine benchmark — plan

> An eight-phase program that extends Heddle's parity-gated intra-.NET benchmark credibility across
> five language ecosystems — Rust, JVM, JS/Node, Python, Go — under one golden-output corpus, one
> cross-language parity contract, and one metrics/publication protocol, consolidated into a single
> cross-stack report and cross-checked, last and separately, on the same machine under Ubuntu 24.04.

> **The .NET leg was rebuilt from scratch.** `src/Heddle.Performance` is **decommissioned for
> comparative benchmarking and deleted**; the leg now lives at
> [`benchmarks/dotnet/`](../../benchmarks/README.md), structured like the other five ecosystem
> harnesses. Nothing in this program may reference that project's code. Every *link* in the phase
> documents below has been repointed at the replacement file; the surrounding prose is a ratified
> record and still names the old project where it describes prior art. The full mapping is recorded
> once as [ledger E12](../spec/records.md#cross-spec-amendments-ledger).

## Phases

| # | Phase | Depends on | Goal (one line) | Status |
|---|---|---|---|---|
| 1 | [cross-stack-foundation](phase-1-cross-stack-foundation.md) | — | Expanded eight-workload set (raw + encoding-ON), golden oracle corpus, parity contract v2, and the metrics/publication protocol every later phase measures against | contract, corpus and the rebuilt .NET leg all shipped and gated; the protocol run has never happened |
| 2 | [rust](phase-2-rust.md) | 1 | Askama + Tera on both fairness tracks under Criterion — the closest-architectural-peer fair fight that makes "Heddle is fast" falsifiable | harness shipped, 32/32 cells gated; measured off-protocol only |
| 3 | [jvm](phase-3-jvm.md) | 1 | JTE + Thymeleaf under JMH — the compiled JVM peer paired with the JVM's largest enterprise server-side templating install base | harness shipped, 32/32 cells gated; measured off-protocol only |
| 4 | [js](phase-4-js.md) | 1 | Handlebars + Eta under mitata — reach/context evidence against what the largest practitioner audience actually uses, with explicit not-fair-fight framing | harness shipped, 32/32 cells gated; measured off-protocol only |
| 5 | [python](phase-5-python.md) | 1 | Jinja2 + Mako under pyperf — the survey's most relatable data point, quantifying the cross-runtime gap credibly | harness shipped, 32/32 cells gated; measured off-protocol only |
| 6 | [go](phase-6-go.md) | 1 | html/template + templ under Go testing/benchstat — the cast's only runtime contextual encoder and the program's third compiled/typed data point | harness shipped, 32/32 cells gated; measured off-protocol only |
| 7 | [consolidated-report](phase-7-consolidated-report.md) | 1, 2–6 (any shipped subset) | The single consolidated cross-stack report — every shipped ecosystem's protocol-conformant results juxtaposed on wall time per render, dual-track, with the workload-shape strengths-and-weaknesses analysis | assembled and published once ([2026-07-25](../benchmarks/2026-07-25/index.md)), self-declared as carrying no protocol standing; re-issue pending |
| 8 | [linux-crosscheck](phase-8-linux-crosscheck.md) | 1, 2–7 (any shipped subset) | The same physical machine booted into Ubuntu 24.04 — the full protocol suite re-run across every shipped ecosystem, separately published as a Linux cross-check validating that the program's rankings, relative gaps, and dispersion findings hold under Linux CPU-isolation conditions | tooling shipped and green on WSL; the bare-metal run has never happened |

## Where this stands (2026-08-01)

Every phase's *machinery* now exists, and the .NET leg has been rebuilt from scratch. What the
program still does not have is a run its own protocol would accept.

**Verified on the protocol machine (Windows 11, .NET SDK 10.0.302) at the time of writing:**
`benchmarks/dotnet` builds warning-free; `gate` reports **143 passed / 0 failed** over 143 cells;
`selftest` reports **141 passed / 0 failed**; `verify-corpus` passes freshness on all eight
workloads and all twenty verifier-calibration cases; and every bench verb executes. The other five
ecosystem harnesses were last gated green on Windows and on WSL Ubuntu 24.04 at the pinned
toolchains; they were not re-run for this note.

### 1. The .NET leg — rebuilt and complete

The twelve-item rebuild is finished. `benchmarks/dotnet` is a single unsigned `net10.0` project with
a verb-dispatching entry point mirroring every other ecosystem harness:

| Verb | What it does |
|---|---|
| `gate` | Every registered cell: byte gate (controlled track), functional verifier (idiomatic track), security floor (encoded workloads), materialisation trailer, precompiled-coverage statement |
| `selftest` | The gate's own checks, including the six-technique differential and the contract vectors pinned against the JS reference implementation |
| `export-corpus` / `verify-corpus` | The golden corpus: regenerate from the Heddle oracles; re-prove freshness and re-calibrate the verifier |
| `bench-crossstack` | The sweep the master runners drive — eight suites, one per workload, six engines, **both fairness tracks** |
| `bench-techniques` | Heddle's six render techniques against each other (three sinks × two backends). Never a cross-stack row |
| `bench-cold` | Cold parse/compile per engine. `Parse*` and `Compile*` are different steps and the row names say so |
| `bench-internal` | Heddle against itself: props allocation, branching cost, language-service metadata |

Both master runners and the Phase 8 launcher drive this project; `benchmarks/report/consolidate.py`
reads its artifacts. The engine grants it **no** `InternalsVisibleTo`: it reaches Heddle through the
public surface only, because a benchmark that needs private access to the thing it measures is
measuring something no caller can reach.

**Evidence the replacement is faithful, not merely new:** re-exporting the corpus with the new tool
reproduces all sixteen committed artifacts — eight goldens and eight `.verify.json` sidecars — byte
for byte.

**Two consequences worth knowing before the next run.** The .NET leg now measures both fairness
tracks, which discharges Phase 1 D15 and retires the standing limitation that .NET had no idiomatic
row for the idiomatic tables to anchor to.

And the measurement budget changed unit.
[Ledger E13](../spec/records.md#cross-spec-amendments-ledger) makes it **per engine** rather than per
ecosystem: five legs carry two or three engines, .NET carries six, and holding its leg total to the
same figure would have sampled every .NET cell about a third as well as a Rust cell. The .NET leg
therefore runs **66 min short / ~175 min baseline** — 11 and ~29 min *per engine* — spending its
budget on launches, which is the term a single-launch job never samples at all. The other five legs
are still sized per ecosystem, so they sit at roughly 3–5 min per engine; making the program uniform
means roughly doubling each of them, which E13 records as a maintainer decision rather than taking.

### 2. The generator gaps that cap precompiled coverage

Precompiled coverage is **5 of 8 workloads**. Three remain on the dynamic path, for two distinct
reasons, and the harness discovers this from the manifest at runtime rather than assuming it — a
fallback measured and labelled "precompiled" would report the runtime backend under the wrong name.

- **`composed-page` (`home.heddle`)** is refused by the emitter twice over. First, `layout.heddle`
  passes embedded C# as component arguments (`@area_component(@"…")`) while the project compiles at
  `HeddleExpressionMode=Native`, and the emitter refuses embedded C# outside `FullCSharp`
  (`TemplateEmitter.cs:3106`) — the runtime backend renders this same workload at `FullCSharp`, so
  the two backends disagree on mode. Second, `home.heddle` full-overrides the `<body:body>` region
  the layout declares, and the emitter refuses full overrides outright
  (`TemplateEmitter.cs:1364`, "definition override/layering"). The first is a project-configuration
  question; the second is a real emitter limitation.
- **`fortunes-encoded` and `encoded-loop`** cannot be precompiled in the same compilation as the raw
  six at all: `HeddleOutputProfile` is a compilation-wide MSBuild property and they need `Html`
  where the others need `Text`. Precompiling them needs a second compilation unit, or a
  per-template profile in the generator's MSBuild contract.

Underneath both: the emitter computes a refusal reason (`TemplateEmitter.Result.UnsupportedReason`)
that **no caller ever reads**, so an unsupported construct degrades to the dynamic path with no
diagnostic at all. Surfacing it would follow the HED7014 precedent already in
`HeddleTemplateGenerator.cs`. None of this blocks a run — it bounds what `bench-techniques` can
measure, not whether the sweep executes.

### 3. Measurement and publication — the only work left

- **Phase 1's protocol run (WI8) has never been performed.** The Windows attempt of 2026-07-22 was
  invalidated by the JS ConsString rope defect and its report withdrawn. The .NET SDK pin in
  [benchmarks/README.md](../../benchmarks/README.md) is still **TBD** for exactly this reason.
- **The only published cross-stack run is [2026-07-25](../benchmarks/2026-07-25/index.md)**, and it
  says of itself that it is the Linux side of the protocol box, run through `run-all.sh` rather than
  either protocol, with no CPU isolation — internally consistent, but carrying no protocol standing
  and corroborated by no second platform. Two of its nineteen recorded limitations (#8, no .NET
  idiomatic track; #17, Razor measured on one workload) are already discharged by the rebuilt leg
  and will lapse when the report is re-issued.
- **Phase 8's bare-metal run has never been performed.** Only the WSL functional sweep is green; the
  `isolcpus` GRUB entry the protocol requires does not exist on the box.
- **`benchmarks/rust/heddle-reference.toml` transcribes the off-protocol run** and carries a
  standing note to repoint `source-run` and re-transcribe once a protocol run is published.

Sequencing from here: run Phase 1 WI8 on the Windows protocol machine, re-issue Phase 7 from it,
then Phase 8 bare metal.

## Ordering rationale

- **Phase 1 is the keystone.** Nothing in phases 2–6 may start against a workload whose
  golden-corpus entry does not exist; the corpus, parity contract v2, and the metrics/publication
  protocol are the only artifacts the ecosystem phases share, and proving each workload can pass
  the byte gate inside one runtime (the existing intra-.NET harness with four dissimilar twins) is
  far cheaper than debugging parity failures across five ecosystems simultaneously.
- **Phases 2–6 are independent of each other, priority-ordered, and individually cuttable**
  (standing ruling): Rust (1) → JVM (2) → JS/Node (3) → Python (4) → Go (5). The order encodes
  credibility value, not dependency: Rust first because Askama is Heddle's closest architectural
  peer anywhere in the survey (the fair-fight argument) and because Rust is the hardest first
  consumer of the Phase 1 contract, shaking out ambiguities the later phases then inherit as
  errata rather than rediscovering; JVM second for the second compiled peer (JTE) plus the JVM's
  largest enterprise install base (Thymeleaf); JS third for the largest practitioner reach; Python
  fourth for relatability (Jinja2, the world's default engine); Go fifth, included for completeness
  — but carrying the cast's only runtime contextual encoder (html/template), and with it the
  program's only compile-time-vs-runtime contextual-encoding comparison, which is the recorded
  caveat against cutting it.
- **Phase 7 consumes any shipped subset of 2–6.** The consolidated report aggregates only runs
  produced under the Phase 1 protocol; a cut ecosystem phase simply means missing rows, not a
  blocked report.
- **Phase 8 runs last, after everything it validates.** The Linux cross-check (a fresh user ruling,
  Q5.2: same machine, Ubuntu 24.04, later, separately) re-runs the full protocol suite on the same
  physical machine dual-booted into Ubuntu 24.04, consuming whatever subset of phases 2–7 shipped
  (mirroring Phase 7's subset semantics) and individually cuttable like the ecosystem phases.
  Sequencing it last means it validates final published findings rather than moving targets, and
  the protocol box leaves its recorded Windows configuration only after every Windows run the
  program will publish is complete; its separately-published numbers are never merged into or
  cross-compared with the Windows numbers inside the phase 2–7 reports.

## Cross-phase notes

- **Ecosystem and engine cast (standing ruling — not re-litigated in any phase):** two engines per
  ecosystem, a credibility pick + a performance/peer pick — Rust: Tera + Askama; JVM: Thymeleaf +
  JTE; JS: Handlebars + Eta; Python: Jinja2 + Mako; Go: html/template + templ (quicktemplate as an
  optional third under Phase 6's conditional-inclusion rule).
- **PHP and Ruby are non-goals of the whole plan.** The rationale is recorded once, at plan level,
  in [Phase 1's Non-goals / scope boundary](phase-1-cross-stack-foundation.md#non-goals--scope-boundary);
  this index links there rather than restating it.
- **Shared machinery lives in Phase 1 by design.** The dual-track fairness posture (controlled
  byte-identical track + idiomatic functional-equivalence track), the byte-parity gate and its
  exclusion policy, the wall-time-per-render-only cross-comparability rule, and the
  honest-reporting publication style are defined in Phase 1 and referenced — not restated — by
  phases 2–6.

## Common documents

None currently. The `common/` directory is reserved for genuinely shared material; the phase
documents were deliberately authored to reference Phase 1 for all shared contract, metrics, and
posture content instead of restating it, so no common document is warranted at this time.

## Open questions

See [open-questions.md](open-questions.md) for the Q&A register. **All questions are resolved (user,
2026-07-20) and folded into the phases — the Open section is empty, so the plan is at DoR.**
