# Phase 6 — go (spec)

## Header

- **Status:** Specified — implementation-ready. The former whitespace-only escalation
  ([D13](#d13--whitespace-only-divergence-resolved-program-wide-by-the-n3b-whitespace-run-collapse-maintainer-ruling-2026-07-20-option-a))
  is **resolved** by the maintainer's 2026-07-20 ruling (option A): contract v2 gains the uniform
  N3b whitespace-run collapse, so any whitespace-only divergence passes the controlled gate
  program-wide and the templ `composed-page` cell is expected to pass. No open escalation remains.
- **Source plan:** [phase-6-go.md](../../../plan/phase-6-go.md) (standing rulings in the
  [plan index](../../../plan/README.md); resolved Q&A register in
  [open-questions.md](../../../plan/open-questions.md) — every resolution there is binding on
  this spec: Q6.1, Q6.2, Q2.2 = A, and the inherited Q1.1, Q1.2-as-modified, Q1.3, Q1.5, Q1.6,
  Q1.7)
- **Assumes merged:** the full
  [Phase 1 — cross-stack-foundation spec](../phase-1-cross-stack-foundation/README.md)
  implementation — specifically its WI6 (exported golden corpus + `verify.json` files) and WI8
  (the published protocol run that sources the Heddle reference rows). Phase 6 is independent of
  phases 2–5 and assumes nothing from them.

| Document | Purpose |
|---|---|
| [README.md](README.md) (this file) | Entry document: assumed state, design decisions, implementation plan, gates |
| [templ-feasibility.md](templ-feasibility.md) | The templ controlled-track feasibility analysis: per-workload whitespace trace against the pinned shapes, the S1 verification spike, and both outcome branches fully specified |
| [port-mapping.md](port-mapping.md) | Per-workload Go port: model transcription, controlled template texts for text/template, html/template, and templ; composition mapping; idiomatic-track authoring; quicktemplate twin shape |
| [harness-and-measurement.md](harness-and-measurement.md) | Module layout, N1–N5 Go implementation, gates, `testing.B` shape, run/stability settings, benchstat, and the report format |

## Scope and goal

Port the eight Phase 1 workloads to the Go ecosystem's two ruled-in engines — the stdlib engine
(text/template for the raw suites, html/template for the encoded suite, per Q6.1) and templ —
under both fairness tracks; gate every controlled cell against the golden corpus byte gate and
every idiomatic cell against the Phase 1 verifier before any timing; measure under
`go test -bench` + benchstat per the metrics protocol; publish one date-stamped
`docs/benchmarks/<date>/` report with the interpreted-vs-precompiled framing, the Heddle
reference row and ratio anchor, the contextual-encoding section, and the honest-reporting rules.
quicktemplate is a conditional stretch under the operationalized rule of
[D10](#d10--quicktemplate-stretch-operationalized-dormancy-disclosed); hero stays excluded.

Out of scope: any change to the golden corpus, contract v2, the metrics protocol, or the Heddle
engine; any cross-ecosystem ranking of non-Heddle engines; any cross-runtime allocation or
cold-cost comparison; HTTP/database layers; re-litigating the engine cast.

## Assumed state

Re-verified against current source and primary sources while authoring (2026-07-20); not
trusted from the plan or the spikes where a first-hand check was possible.

| Seam | Verified state |
|---|---|
| Phase 1 artifacts | **Specified, not yet implemented**: no `GoldenCorpus/` directory exists in `src/Heddle.Performance` *(listed)* — the corpus, `verify.json` files, and the protocol run land with Phase 1's WI6/WI8. Phase 6 implementation starts only after they merge (dependency-order rule); this spec's template shapes are pinned against Phase 1's normative texts, which the corpus export must match by Phase 1's own gates |
| Repo layout | No top-level `benchmarks/` directory and **no Go code anywhere in the repo** (`git ls-files` scan for `*.go` / `go.mod`: zero hits) — D2's location choice creates new surface, collides with nothing |
| Composed-page oracle shape | The .NET twins concatenate the section/component/area fragments with **zero separator bytes** (`LiquidTemplates.LayoutTemplate` *(read)* chains its output actions with no whitespace) and today's parity gate passes all four twins at 55,456 raw / 34,837 normalized chars (Runners README *(read)*), so the normalized oracle contains `/>/* CSS Comment Test */<script` at the one non-tag-flanked fragment boundary — the load-bearing fact for the templ P4 probe |
| Composed-page fragment data | `TwinContent.cs` *(read)*: six component constants, four section values, `AreaOrder[7]`; area fragments are multi-KB verbatim literals in `AreaComponent.cs` *(read, head)* whose whitespace all sits between tags; twins read them as **runtime data**, never transcribe them into template text — the Go ports do the same ([port-mapping.md](port-mapping.md#model-transcription-shared-by-all-engines-and-tracks)) |
| Go toolchain | **`go1.26.5`** is current stable — verified first-hand via <https://go.dev/VERSION?m=text> (the spike E flag "recheck" is closed; the spike's value was correct) |
| templ | **v0.3.1020** (2026-05-10) is latest — verified first-hand via the Go module proxy (`proxy.golang.org/github.com/a-h/templ/@latest`). No whitespace-control syntax; generator collapses HTML text whitespace to single spaces (a-h/templ#90, closed enhancement, no preserve flag shipped); `<style>` contents documented as rendered unchanged; expressions accept strings/numbers/bools; `templ.Raw` is the raw bypass — all re-verified against templ.guide and the issue while authoring ([templ-feasibility.md — facts table](templ-feasibility.md#the-engine-facts-this-analysis-rests-on)) |
| quicktemplate | **v1.8.0**, module time 2024-07-04 — verified via proxy `@latest`; identical to its last upstream commit date (spike E), i.e. **dormant for ~2 years**: no releases or commits since. Whitespace control (`stripspace`/`collapsespace`/`{%- -%}`) confirmed in its README (spike E, primary fetch) |
| benchstat | `golang.org/x/perf` latest = `v0.0.0-20260709024250-82a0b07e230d` (2026-07-09) — verified via proxy; run-count guidance "at least 10, ideally 20" (pkg.go.dev, spike E primary fetch) |
| html/template escaper | Fixed stdlib tables escape `& < > " ' +` in text/quoted-attr contexts with all-decimal spellings (`&#34;`, `&#39;`, `&#43;`), broader still in unquoted-attr; no configuration surface (spike C item 9 + golang/go#42506, carried into Phase 1's D2/D4 — the untrusted-data alphabet excludes `+`/`=`/`` ` `` for exactly this reason) |
| templ escaper | `templ.EscapeString` → stdlib `html.EscapeString`: five characters, decimal `&#34;`/`&#39;` in text; attribute path auto-quotes and attribute-encodes (spike C item 10; templ.guide attributes page) |
| Encoded model data vs alphabet | Every pinned fortunes message and encoded-loop formula checked character-by-character against the alphabet: **no `+`, `=`, `` ` ``, or `&#` appears in any untrusted position** (workloads.md *(read)*; fortunes row 1 is `4.33e67` by Phase 1 design) |
| Protocol bindings | Statistic mapping row for Go = benchstat `sec/op` + `±%` (metrics-protocol.md *(read)*); presentation rules (Heddle row, ratio anchor) fixed in Phase 1 D13; the within-ecosystem allocation baseline = the **credibility-pick stdlib engine**, realized per Q6.1 as its per-suite surface (text/template on the raw suites, html/template on the encoded suite). Phase 1 D13 / metrics-protocol presentation rule 3 name this baseline "html/template"; because Q6.1 splits the stdlib surface per suite, that label is the **encoded-suite instantiation** of the one credibility-pick engine — html/template measures no raw-suite rows to baseline, so the raw-suite baseline is the same engine's raw surface, text/template (recorded correction per [spec-conventions §Relationship to the owning plan](../../common/spec-conventions.md#relationship-to-the-owning-plan); rule 3's baseline-is-the-credibility-pick rule is unchanged, so phase 7 still inherits a stdlib-baselined Go table). Exclusion policy + whitespace rule in contract v2 §exclusion-policy *(read)* |

## Design decisions

### D1 — Q6.1 bound to code: text/template raw / html/template encoded, both tracks
- **Decision.** The stdlib engine measures as text/template on all six raw workloads and
  html/template on both encoded workloads, in **both** the controlled and idiomatic tracks;
  benchmark ids `stdlib-text` / `stdlib-html`; every report row labels the surface
  ([harness-and-measurement.md — report item 3](harness-and-measurement.md#report-format-instantiates-the-protocol)).
- **Rationale.** Q6.1 resolution verbatim for the suite split. The **primary** reason to extend
  it to the idiomatic track is measurement consistency: holding the stdlib surface identical
  across both tracks makes a controlled-vs-idiomatic delta isolate *authoring style*, not a
  surface swap. It is also the idiomatic-faithful reading — text/template's package doc
  (cited below) is the canonical **non-escaping** templating surface, which is exactly what the
  raw suites model; an author who wants HTML auto-escaping reaches for html/template (the encoded
  suite's surface), not html/template with per-value raw bypass on raw output. (An idiomatic
  author could defensibly use html/template's safe default even for raw output, escaping to a
  byte-level no-op on the escape-free raw alphabet; the measurement-consistency reason is what
  makes text/template the recorded choice, not a claim that html/template would be non-idiomatic.)
- **Alternatives rejected.** html/template + per-value `template.HTML` bypass for raw suites
  (Q6.1 option B — measures bypass-wrapper overhead as templating, rejected by the user);
  idiomatic-track-only html/template everywhere (would make raw-suite track deltas conflate
  surface and style).
- **Grounding.** [open-questions Q6.1](../../../plan/open-questions.md); pkg.go.dev/text/template.

### D2 — Harness lives at `benchmarks/go/` (new top-level per-ecosystem convention)
- **Decision.** All Go-side code is one Go module at `benchmarks/go/`
  ([layout](harness-and-measurement.md#module-layout)); `benchmarks/<ecosystem>/` is hereby the
  cross-phase convention for phases 2–6 harnesses. The corpus stays at
  `src/Heddle.Performance/GoldenCorpus/`, read via repo-relative path.
- **Rationale.** No stronger repo convention exists (verified: no `benchmarks/` dir, no Go code;
  `src/` is the .NET solution tree and a Go module inside it would entangle .NET tooling
  globs). Phase 1's D6 declined a top-level `benchmarks/` directory *for the corpus*, explicitly
  deferring the repo-structure decision to a phase that needs it — this phase is the named
  trigger's first consumer, and a harness (unlike the corpus) has no generator co-location
  argument for living under `src/`.
- **Alternatives rejected.** `src/Heddle.Performance.Go/` (implies a .NET project by naming
  convention and pollutes the solution directory); moving the corpus next to the harnesses
  (Phase 1 D6's revisit trigger is "cannot conveniently consume", which a relative path
  disproves).
- **Grounding.** Phase 1 [D6](../phase-1-cross-stack-foundation/README.md#d6--the-corpus-stores-the-normalized-oracle-under-srcheddleperformancegoldencorpus)
  + its deferred-items row; repo scan (Assumed state).

### D3 — Toolchain and dependency pins
- **Decision.** `go.mod`: `go 1.26` + `toolchain go1.26.5`; `github.com/a-h/templ v0.3.1020` as
  a require (runtime package) **and** its CLI plus benchstat as `tool` directives
  (`tool github.com/a-h/templ/cmd/templ`, `tool golang.org/x/perf/cmd/benchstat` at
  `golang.org/x/perf v0.0.0-20260709024250-82a0b07e230d`), so `go.sum` locks every byte and
  `go tool templ` / `go tool benchstat` run the pinned versions with no global installs.
  Generated `*_templ.go` files are committed; `run-benchmarks.ps1` re-generates and asserts
  `git diff --exit-code` so commits can never drift from the pinned generator. quicktemplate, if
  the stretch triggers: `github.com/valyala/quicktemplate v1.8.0` + `tool …/qtc`, same pattern.
- **Rationale.** All four versions verified first-hand (Assumed state). The `tool` directive
  (Go ≥ 1.24) is the stdlib-native pinning mechanism; committing generated code keeps
  `go test` runnable at any commit without the generator installed, while the freshness assert
  keeps the pin honest.
- **Alternatives rejected.** `go run pkg@version` ad-hoc invocations (no `go.sum` lock, version
  drift by typo); not committing generated code (every consumer needs the generator; gate runs
  in CI-less contexts would silently skip templ); floating `@latest` (irreproducible).
- **Grounding.** go.dev/VERSION, module-proxy `@latest` responses (Assumed state); Go modules
  `tool` directive documentation (go.dev/ref/mod).

### D4 — N5 entity canonicalization is implemented in the Go gate runner
- **Decision.** The Go gate implements the contract's N5 table
  ([Go implementation](harness-and-measurement.md#normalization-pipeline-go-implementation))
  and applies it to encoded-suite candidate output. No engine configuration is attempted.
- **Rationale.** Q1.1 prefers engine configuration *where offered*; **neither Go engine offers
  any** — html/template's tables are fixed stdlib internals and templ has no escaper hook
  (spike C items 9–10, re-verified) — so the contract's own fallback (gate-runner N5) is the
  only conformant path. On the pinned alphabet the only non-canonical spelling either engine
  emits is `&#34;` for `"` (both engines, text path; templ's attribute path may emit `&quot;`,
  already canonical) — squarely inside the N5 table.
- **Alternatives rejected.** Wrapping values in pre-escaped `template.HTML` to control
  spellings (changes what is measured — the escaper is the metric); a Go-side custom escaper
  FuncMap (same objection, and departs from the engine's stock path the report claims to
  measure).
- **Grounding.** [parity-contract-v2.md — N5](../phase-1-cross-stack-foundation/parity-contract-v2.md#entity-canonicalization-n5)
  rule 3; spike C §(c).

### D5 — Alphabet compliance is re-asserted by the Go gate
- **Decision.** Gate startup scans all materialized encoded-suite model values against the
  untrusted-data alphabet and fails with a named error on violation
  ([harness-and-measurement.md](harness-and-measurement.md#controlled-byte-gate)); this spec's
  authoring-time verification of the pinned data is recorded in the Assumed state.
- **Rationale.** html/template is the engine the alphabet was designed around (`+` → `&#43;`);
  a future corpus/model edit that reintroduces `+` would otherwise surface as an opaque byte
  diff in this phase. A named assert converts that failure mode into its cause.
- **Alternatives rejected.** Trusting the Phase 1 authoring rule alone (correct today — verified
  — but this phase is the blast radius when it breaks, so the guard belongs here); asserting in
  every phase's spec prose only (not machine-checked).
- **Grounding.** [contract — untrusted-data alphabet](../phase-1-cross-stack-foundation/parity-contract-v2.md#untrusted-data-alphabet);
  golang/go#42506; workloads.md data check (Assumed state).

### D6 — templ controlled-track feasibility: analysis now, S1 spike first, expected pass + non-whitespace contingency decided
- **Decision.** As specified in [templ-feasibility.md](templ-feasibility.md): all eight workloads
  are analyzed as whitespace-safe under dense authoring, and since the 2026-07-20 N3b ruling
  (D13) templ's whitespace collapsing — including any inter-statement separator space at the
  `composed-page` CSS-comment boundary — is reconciled by the pipeline, so no cell is at risk on
  whitespace grounds. Verification spike **S1** (probes P1–P4) is the first implementation work
  item; its expected outcome is that all probes pass, with a single **non-whitespace** contingency
  (an escaper spelling outside the N5 table) routing to the Q1.2 exclusion procedure.
- **Rationale.** The plan flags this as the phase's concentrated risk and requires spec-time
  verification before bulk porting; the analysis narrows the executed spike to the four
  mechanics documentation cannot settle, and pre-deciding the expected outcome and the
  non-whitespace contingency removes every implementation-time design decision.
- **Alternatives rejected.** Skipping the spike on the strength of the doc analysis (F3/F4 are
  doc-only claims; the byte gate tolerates zero surprises); running the spike on `mixed-page`
  instead of the smallest workload (P1 uses trivial-substitution per the plan's
  smallest-workload instruction; mixed-page's unique mechanic — style passthrough — is carved
  out as its own probe P3, so nothing is lost). Whitespace-only divergence now passes uniformly
  via N3b (D13, maintainer ruling 2026-07-20).
- **Grounding.** [templ-feasibility.md](templ-feasibility.md) (facts F1–F6, per-workload trace);
  a-h/templ#90; contract v2 §exclusion-policy.

### D7 — Composition mapping: associated templates and templ components
- **Decision.** `composed-page`: layout as an associated template (`{{define "layout"}}` +
  `{{template "layout" .}}`) with sections/components as map lookups and a real `range` over
  `AreaOrder`, fragments as runtime data — structurally identical to the .NET twins; templ uses
  one component issuing `@templ.Raw` per fragment plus the loop. `fragment-heavy`: stdlib
  `{{define "tile"}}` + `{{template "tile" .}}` inside `range`; templ component
  `@tile(item)`. Full shapes in [port-mapping.md](port-mapping.md).
- **Rationale.** These are each engine's native partial/composition constructs (the Q1.7-style
  documentation-pattern mechanisms), and they mirror the probe-E-verified twin constructs
  one-for-one, so the composition workloads measure composition machinery, not emulation. The
  plan's "maps awkwardly" risk dissolves once the oracle's zero-separator concatenation shape is
  known (Assumed state) — text/template reproduces it trivially; templ's residual question is
  exactly S1-P4.
- **Alternatives rejected.** Inlining the tile body into the loop (removes the per-call
  composition cost the workload owns); `template.Must(t.New(...))` per-render cloning
  (not the cached-render path); string-concatenating fragments in Go code outside the template
  (bypasses the engine's composition machinery — the thing measured).
- **Grounding.** pkg.go.dev/text/template (associated templates); templ.guide composition page
  (F6); [Runners README fidelity note](../../../../src/Heddle.Performance/Runners/README.md)
  *(read)*; LiquidTemplates.cs *(read)*.

### D8 — Benchmark shape: `b.Loop`, `b.ReportAllocs`, nested naming, prebuilt binary
- **Decision.** As pinned in
  [harness-and-measurement.md — benchmark shape](harness-and-measurement.md#benchmark-shape-testingb):
  `BenchmarkRender/<track>/<workload>/<engine>` nesting, `b.ReportAllocs()` in every benchmark,
  `for b.Loop()` bodies with a package-level sink, setup in `init`/`TestMain`, a reused
  pre-grown buffer reset per iteration, and gates in `TestMain` ahead of `m.Run()`.
- **Rationale.** `b.Loop` is the pinned toolchain's recommended DCE-safe loop; `ReportAllocs`
  makes allocs/op structural (the phase must report it for every benchmark — plan success
  criterion); the nesting produces benchstat-groupable names; `TestMain` is the only Go-native
  point that provably precedes every timed region in the same invocation (contract gate rule 2).
- **Alternatives rejected.** `b.N` manual loops (DCE risk without the sink discipline, and
  `b.Loop` subsumes it on go1.26); `-benchmem` flag reliance (a forgotten flag silently drops a
  required metric); per-benchmark gate calls inside benchmark functions (would run the gate 20×
  per cell inside the measured process phase — `TestMain` runs it once per invocation).
- **Grounding.** pkg.go.dev/testing (`B.Loop`, `B.ReportAllocs`); contract v2
  §controlled-track-gate 2.

### D9 — Stability settings on the protocol machine
- **Decision.** `-count=20`, `-benchtime=1s`, prebuilt test binary launched at **High** priority
  via `cmd /c start /high /wait /b`, no CPU affinity, runtime-default `GOMAXPROCS`/`GOGC`
  (recorded); fallback trigger: benchstat variation > ±5% on any suite → that suite re-runs
  pinned to CCD0 (`/affinity 0xFFFF`) with the pinning recorded in the report. Full table in
  [harness-and-measurement.md](harness-and-measurement.md#repeated-runs-stability-settings-benchstat).
- **Rationale.** Q1.6 delegates per-harness Windows stability settings to this spec. Count 20 is
  benchstat's own "ideally 20"; High priority mirrors what BenchmarkDotNet applied to the
  published .NET numbers on this box (continuity — the same argument that chose the machine);
  no-affinity is likewise the .NET runs' posture, with the dual-CCD pinning held as a recorded,
  evidence-triggered fallback rather than a default that departs from precedent. Priority is set
  at process creation, so no race with the first warmup.
- **Alternatives rejected.** Default (unelevated) priority (departs from the box's published-run
  posture and invites scheduler noise); affinity-by-default (untested benefit here, breaks
  posture symmetry with the .NET runs; kept as trigger); REALTIME priority (starves the
  system — pyperf uses it for tiny workers, BenchmarkDotNet deliberately stops at High).
- **Grounding.** benchstat run-count guidance (spike E, primary); Q1.6 resolution;
  BenchmarkDotNet elevated-priority default (benchmarkdotnet.org); metrics-protocol.md machine
  section.

### D10 — quicktemplate stretch operationalized, dormancy disclosed
- **Decision.** quicktemplate twins are authored **only if both hold**: (a) templ's controlled
  track is fully green — all eight workloads passing the byte gate, zero excluded cells — and
  (b) the remaining work is template-twin authoring over the existing harness per the
  pre-specified shape in [port-mapping.md](port-mapping.md#quicktemplate-twins-conditional-stretch-only)
  — zero new harness, gate, or contract work. Under any schedule pressure it is cut before any
  core item. Pin: v1.8.0 (the last version that exists). The report's engine-selection section
  discloses the dormancy evidence (last upstream change 2024-07-04, no releases since) whether
  or not the stretch ships; if it ships, dormancy is part of its row's framing ("stable but
  dormant"), and its 20x marketing claim is quoted only next to this phase's measured number.
- **Rationale.** The plan's conditional rule is binding and is transcribed, not re-decided; the
  dormancy evidence (verified via the module proxy) is factored as *disclosure and framing*, not
  as a veto — the ruling made inclusion conditional on cost, not on upstream liveness, and a
  dormant-but-stable codegen engine still tests the claim the plan wants tested. Deciding the
  disclosure now means triggering the stretch adds zero design work.
- **Alternatives rejected.** Dropping the stretch outright on dormancy grounds (overrides a
  standing ruling this spec has no authority to override; the rule already prices the risk by
  making it cut-first); pinning a pseudo-version past v1.8.0 (nothing newer exists).
- **Grounding.** Plan §Design direction (conditional rule, verbatim); proxy `@latest` =
  v1.8.0/2024-07-04 (Assumed state); spike E §7.

### D11 — Report format instantiation
- **Decision.** The published report implements the ten mandatory items of
  [harness-and-measurement.md — report format](harness-and-measurement.md#report-format-instantiates-the-protocol):
  environment block with all pins, interpreted-vs-precompiled organization, Q6.1 per-suite
  surface labels, Heddle reference row + Heddle-anchored ratio column (Q2.2 = A, Q6.2),
  stdlib-baselined allocation sidebar with the verbatim label, the dedicated
  contextual-encoding section with the verbatim confinement caveat, the engine-selection record
  (hero exclusion, quicktemplate status + dormancy), honest-reporting rules, the Q1.3 cold-cost
  sidebar, and evidence-linked exclusion cells.
- **Rationale.** Every element is a protocol or plan success-criterion requirement bound to a
  concrete report location; the contextual-encoding section carries the phase's headline (the
  program's only runtime contextual encoder) under the architectural-comparison framing the plan
  mandates.
- **Alternatives rejected.** Folding the contextual-encoding discussion into the results
  narrative (plan requires a dedicated, legible section); anchoring the wall-time ratio to the
  stdlib engine (Q6.2 resolved: Heddle row anchors wall-time ratios; the stdlib engine baselines
  only non-comparable metrics).
- **Grounding.** [metrics-protocol.md](../phase-1-cross-stack-foundation/metrics-protocol.md)
  (presentation rules, label texts); plan success criteria.

### D12 — Idiomatic track per Q1.7, verified by the Phase 1 verifier in Go
- **Decision.** Idiomatic implementations for all eight workloads on both engines, authored
  in-repo to each engine's official documentation patterns with doc URLs cited in a header
  comment per file ([port-mapping.md](port-mapping.md#idiomatic-track--both-engines-all-eight-workloads));
  gated by the Go implementation of the Phase 1 verifier, whose conformance is proven by the
  accept-golden / reject-corruptions unit test
  ([harness-and-measurement.md](harness-and-measurement.md#idiomatic-verifier)).
- **Rationale.** Q1.7/D16 verbatim; the calibration test is what makes "the same verifier,
  reimplemented" a checked claim rather than an assertion.
- **Alternatives rejected.** Importing templates from slinso/goTemplateBenchmark or similar
  (explicitly rejected by Q1.7); skipping Go-side calibration because Phase 1 calibrated the
  definitions (Phase 1 calibrated *its* implementation; this phase's reimplementation needs its
  own proof).
- **Grounding.** [open-questions Q1.7](../../../plan/open-questions.md); contract v2
  §idiomatic-track-gate; golden-corpus.md §verification.

### D13 — Whitespace-only divergence: resolved program-wide by the N3b whitespace-run collapse (maintainer ruling 2026-07-20, option A)
- **Decision (CLOSED).** The former escalation — whether a whitespace-only divergence the old
  N2–N4 could not erase (templ's collapsing at a non-tag-flanked boundary — S1-P4 territory on
  `composed-page`) *passes* (Q1.2's literal words) or forces *exclusion* (contract v2's then-closed
  pipeline) — is **resolved by the maintainer's 2026-07-20 ruling: option A.** Contract v2's closed
  normalization list gains **one uniform, disclosed step, N3b** — *collapse every whitespace run to
  **nothing** (remove it), applied to both the oracle and the candidate at comparison so the gate
  compares only their non-whitespace bytes* — identical in every ecosystem
  ([contract v2 — normalization pipeline](../phase-1-cross-stack-foundation/parity-contract-v2.md#normalization-pipeline)).
  Consequence, program-wide: **any whitespace-only divergence passes the controlled byte gate** —
  run-length change **or** presence-vs-absence (the composed-page `>␠/*` boundary included)
  (honoring Q1.2's literal outcome), and the exclusion path applies **only to non-whitespace
  divergence**. The Go gate implements the amended list (N1, N2, N3, N3b, N4, N5) exactly — N3b is
  no longer a per-phase widening but a ratified part of the one portable contract, so the
  closed-list principle is preserved (a gate that omits N3b, or adds anything beyond the list, is
  non-conformant).
- **Effect on the templ risk.** templ's only identified controlled-track divergence mechanism was
  whitespace-only (an inserted separator space between statement/`templ.Raw` nodes at the
  composed-page CSS-comment boundary). Under N3b that class of divergence passes by construction,
  so the `composed-page` cell is **expected to pass**; it is no longer a candidate for exclusion on
  whitespace grounds. See [templ-feasibility.md — S1](templ-feasibility.md#s1--the-verification-spike-first-work-item).
- **Residual (non-whitespace) contingency.** The exclusion path now triggers only for a
  **non-whitespace** divergence — for templ the sole remaining candidate is an escaper spelling
  that falls *outside* the N5 table (a byte the alphabet did not already exclude). That path is
  unchanged and remains fully specified.
- **Rationale.** A single uniform disclosed step is exactly the reconciliation Q1.2 assumed the
  gate already performed; it honors "whitespace-only passes" without a per-phase pipeline fork
  (which would have been non-conformant) or a curve (forbidden by §exclusion-policy 1). Because it
  is program-wide and part of the ratified contract, every ecosystem's gate stays identical and the
  output-equality credibility claim is preserved.
- **Alternatives rejected.** Per-phase pipeline widening (rejected by the maintainer in favor of a
  uniform contract-level step); reading Q1.2 narrowly as "whatever the old N2–N4 already erase
  passes" and excluding the cell (former interim default — superseded by this ruling, option B not
  taken); leaving the gate byte-strict without N3b (would keep publishing an excluded cell for a
  purely whitespace difference, against Q1.2's literal ruling).
- **Grounding.** Maintainer decision 2026-07-20 (D13 escalation → RESOLVED, option A; see Phase 1
  D8 / parity-contract-v2 §normalization-pipeline below); [open-questions Q1.2](../../../plan/open-questions.md) resolution text;
  [contract v2 — normalization pipeline (N3b)](../phase-1-cross-stack-foundation/parity-contract-v2.md#normalization-pipeline)
  and §exclusion-policy 1–2; [Phase 1 README D8](../phase-1-cross-stack-foundation/README.md#d8--normalization-v2-v1s-three-steps--defined-encoding--portable-whitespace-definition--n3b-run-collapse).

## Implementation plan

Ordered; Phase 1's WI6/WI8 merged is the entry precondition. Every item names its files
(under `benchmarks/go/` unless stated) and completion check.

### WI1 — Module scaffold and gate machinery
- **Files.** `go.mod`/`go.sum` (D3 pins), `README.md`, `internal/corpus/normalize.go`,
  `gate.go`, `verify.go`, `internal/model/` (all eight models + `composed.go` fragments),
  `suites/gate_test.go` (TestMain + `Test…` gate functions), unit tests for N1–N5 and the
  verifier calibration.
- **Change.** As specified in [harness-and-measurement.md](harness-and-measurement.md) and
  [port-mapping.md — model transcription](port-mapping.md#model-transcription-shared-by-all-engines-and-tracks).
- **Done when.** `go test ./internal/...` green: N5 table tests (incl. no-rescan), verifier
  accepts all eight goldens and rejects every synthesized corruption with the correct kind,
  alphabet assert passes on the pinned models and fails on a deliberately violating fixture.

### WI2 — S1: templ feasibility spike (first porting work)
- **Files.** `internal/spike/` probes P1–P4 (+ `attempts/` if variants are needed),
  `README.md` "S1 results" section.
- **Change.** Exactly the four probes of
  [templ-feasibility.md — S1](templ-feasibility.md#s1--the-verification-spike-first-work-item).
- **Done when.** The results block records all four probes with observed bytes; the expected
  outcome is a clean pass (whitespace-only differences reconciled by N3/N3b). Only a confirmed
  **non-whitespace** divergence (the contingency — e.g. an escaper spelling outside N5) produces
  `EXCLUSIONS.md` entries per the evidence procedure. Later work items proceed accordingly.

### WI3 — Stdlib controlled ports (raw then encoded)
- **Files.** `internal/stdlibtpl/templates.go`, `render.go`; gate registrations in
  `suites/gate_test.go`.
- **Change.** The six text/template raw templates then the two html/template encoded templates,
  per [port-mapping.md](port-mapping.md#controlled-track--stdlib-surfaces); `composed-page`
  first within the raw set (it exercises the association machinery and the fragment
  transcription earliest).
- **Done when.** `go test ./suites` shows all eight `stdlib-*` controlled gates `[PASS]`
  including the encoded security floor.

### WI4 — templ controlled ports
- **Files.** `internal/templeng/*.templ` + committed `*_templ.go`.
- **Change.** All eight components per
  [port-mapping.md — templ rules 1–8](port-mapping.md#controlled-track--templ), authored in
  the forms S1 proved byte-clean; order: `composed-page` first (historically-flagged cell first,
  per plan internal ordering), then encoded pair, then the remaining raw workloads.
- **Done when.** All eight `templ` controlled gates `[PASS]` — or any cell with a confirmed
  non-whitespace divergence carries a completed exclusion record and every other cell passes.

### WI5 — Idiomatic ports, both engines
- **Files.** `internal/stdlibtpl/idiomatic.go`, `internal/templeng/idiomatic/*.templ` (+
  generated), gate registrations.
- **Change.** Per [port-mapping.md — idiomatic track](port-mapping.md#idiomatic-track--both-engines-all-eight-workloads),
  each file citing its official doc pages in a header comment.
- **Done when.** All 16 idiomatic verifications `[PASS]`; every file's header carries its
  citations (reviewed by inspection).

### WI6 — Benchmarks and runner script
- **Files.** `suites/bench_test.go`, `suites/coldparse_test.go`, `run-benchmarks.ps1`.
- **Change.** The D8 benchmark shape for all cells that passed their gates; the D9 runner
  script including version asserts and the `go tool templ generate` freshness check.
- **Done when.** A short smoke run (`-count=1 -benchtime=100ms`) completes end-to-end from the
  script, producing benchstat-consumable output for every non-excluded cell, with allocs/op
  present on every line.

### WI7 — quicktemplate stretch (conditional — D10)
- **Files.** `internal/qtpl/*.qtpl` (+ generated), go.mod additions, bench registrations.
- **Change.** Only if the D10 rule triggers after WI4; shape pre-specified in
  [port-mapping.md](port-mapping.md#quicktemplate-twins-conditional-stretch-only).
- **Done when.** All quicktemplate cells pass the same gates as core engines; or the item is
  skipped and the report records why (rule not triggered / cut under pressure).

### WI8 — Protocol measurement run
- **Files.** `results/` outputs (staged for publication; raw txt + benchstat tables).
- **Change.** Execute `run-benchmarks.ps1` on the protocol machine per the D9 settings
  (`-count=20`); apply the ±5% fallback trigger if tripped; capture the environment block data.
- **Done when.** benchstat consumes all runs and emits `sec/op ±%` for every non-excluded
  cell; the gate log for the same invocation shows all-pass.

### WI9 — Report authored and published
- **Files.** New `docs/benchmarks/<yyyy-MM-dd>/index.md` + copied `bench-*.txt` /
  `benchstat-*.txt` artifacts.
- **Change.** The ten mandatory report items of D11/harness-and-measurement.md.
- **Done when.** A checklist review against
  [report items 1–10](harness-and-measurement.md#report-format-instantiates-the-protocol) and
  the protocol's honest-reporting rules passes; every plan success criterion for this phase is
  satisfiable by pointing at a line of the published report or a gate log.

## Public API / contract

No Heddle public API is touched; the Go module is repo-local (`heddle.dev/benchmarks/go`, never
published, all packages `internal/` except the `suites` test package). The externally consumed
artifacts this phase *produces* are: the published `docs/benchmarks/<date>/` report (consumed by
phase 7 for the Go rows) and, if exclusions occur, `benchmarks/go/EXCLUSIONS.md` (consumed by
report links). The phase *consumes* the Phase 1 corpus, verify.json, contract v2, and protocol
unchanged. Thread-safety: all model/template state is initialized once before `m.Run()` and read-only
thereafter; benchmarks are single-goroutine.

## Diagnostics / error surface

**HED\* compiler diagnostics: n/a** — no compiler, grammar, or engine surface changes; the
diagnostic registry is untouched. The phase's own error surface (all host-side):

| Surface | Type / exit | Message shape | Trigger |
|---|---|---|---|
| Controlled gate | `TestMain` prints and `os.Exit(1)`; as `Test…`: `t.Fatalf` | `gate: <workload>/<engine> diverged: first diff at <i> (exp <n>/act <m>)\n  expected: …±40-char excerpt…\n  actual:   …` | normalized candidate ≠ corpus bytes |
| Corpus integrity | same | `gate: corpus file <file> hash mismatch vs manifest` | checkout/corpus corruption |
| Security floor | same | `gate: <workload>/<engine> forbidden payload "<script>alert(" found <M> times (want 0)` / `…escaped form found <M> (want <N>)` | raw payload present / escaped count wrong |
| Alphabet assert | same | `gate: model value <workload>.<field>[<row>] violates untrusted-data alphabet: <char U+XXXX>` | forbidden character in encoded model data |
| Idiomatic verifier | same | `verify: <workload>/<engine> <value\|marker\|forbidden\|required> failed: <entry>, expected <N>, found <M>` | any verifier check miss |
| Runner script | non-zero exit before any timing | `run-benchmarks: go version mismatch (want go1.26.5, got …)` / `…: generated templ code is stale — run 'go tool templ generate' and commit` | pin or freshness violation |

## Testing plan

**TDD verdict.** The gates are the executable spec, red-first by construction: WI1 lands the
gate machinery with its own unit tests (N5 table, verifier calibration, alphabet fixture)
before any port exists; each port then turns its gate green (WI3/WI4/WI5). No xUnit/.NET tests
are touched; the Go module's `go test ./...` is its complete test suite and the phase's parity
command.

**Named checks.**
- Unit: N2–N4 equivalence vectors (shared strings with expected outputs, including the
  contract's six-character whitespace set edge cases), N5 table + no-rescan case, verifier
  accept/reject calibration for all eight workloads, alphabet positive/negative fixtures.
- Gates: 8 × controlled × per engine, 8 × idiomatic × per engine, security floor on encoded,
  corpus hash integrity — all in `TestMain` and as `Test…` functions.
- Benchmarks: `BenchmarkRender` (≤ 32 cells + stretch) and `BenchmarkColdParse` (2 cells), all
  behind the TestMain gate.
- Negative/security: the Fortunes rule asserted per engine output (not only the oracle); the
  non-whitespace exclusion-evidence procedure if S1 surfaces a genuine non-whitespace divergence.

**Regression gate (before merge of each WI, and combined before WI8):**
1. `go vet ./...` and `go test ./...` in `benchmarks/go` — all green.
2. `go tool templ generate` then `git diff --exit-code -- '*_templ.go'` — no drift.
3. `dotnet run -c Release --project src/Heddle.Performance -f net10.0 -- verify-corpus` — exit
   0 (proves this phase changed nothing upstream; the corpus this phase gated against is fresh).
4. `git status` shows no modification under `src/` or `docs/benchmarks/<existing dates>/`.

## Back-compat and migration

Nothing existing changes: no file under `src/`, no published report, no corpus byte, no plan or
Phase 1 spec text is edited. New surface only: the `benchmarks/go/` module, one new
`docs/benchmarks/<date>/` directory (WI9), and — on the benchmark machine — the Go toolchain +
pinned tools recorded in the environment block. A future corpus regeneration (ordinary
versioned change, Q1.5) is absorbed by re-running this phase's gates like any repo change; no
migration machinery exists or is needed. The spec master index gains this initiative's row per
the index-maintenance convention **in the change that lands this spec folder** (the only edit
outside this folder, sanctioned by the convention itself).

## Performance considerations

- The measured hot paths are the engines', not ours: the shared render helper contributes one
  buffer `Reset()` and one `String()` copy per iteration, identical across engines so relative
  numbers are unaffected; buffers are pre-grown so growth never lands in a timed region
  (encoded-loop's ~1 MB output is the sizing case).
- Gates, model materialization, and template parsing are setup-time only (`init`/`TestMain`),
  outside all timed regions; the gate adds one render + byte-compare per cell per invocation.
- `b.Loop` + sink prevent dead-code elimination from flattering any engine.
- Allocation columns will reflect output size for every engine equally (the .NET suite's
  accepted posture); the stdlib baseline anchors the within-ecosystem comparison.
- Guarding benchmarks: the suite itself; WI8's published run is the phase's recorded baseline
  for phase 7 and the phase 8 Linux cross-check.

## Standards compliance

- **DRY:** normalization, gate, and verifier logic exist once in `internal/corpus` and serve
  both tracks, all engines, and the spike; model data exists once in `internal/model` for all
  engines/tracks (the .NET `Shared` discipline transposed); the composed-page fragments are
  transcribed once and byte-checked by the gate rather than trusted.
- **YAGNI:** no cross-ecosystem shared harness abstraction (each phase implements the contract
  against the exported artifacts — Phase 1's recorded stance); no quicktemplate code until the
  D10 rule fires; no affinity/isolation machinery beyond the evidence-triggered fallback; no
  report auto-generation tooling (one report, authored once).
- **Open/closed at the right seam:** adding the stretch engine (or a future engine, with user
  sign-off) is a new `internal/<engine>` package plus gate/bench registrations — no change to
  corpus, gate, or verifier code.
- **Benchmarks exempt from DRY:** the per-cell benchmark functions deliberately repeat the
  register/render pattern so a failing cell is diagnosable at a glance, matching the .NET
  suite's practice.

## Deferred items / non-goals

| Item | Trigger |
|---|---|
| quicktemplate twins (WI7) | D10 rule: templ controlled track fully green **and** zero-harness-work confirmed; cut first under pressure |
| CCD0 affinity pinning as default | benchstat variation > ±5% on any suite in WI8 (then: per-suite re-run, recorded — D9) |
| Moving the corpus out of `src/Heddle.Performance/GoldenCorpus/` | Not triggered — the relative-path consumption works (Phase 1 D6's trigger condition remains unmet) |
| Go-side cold-cost beyond the stdlib parse sidebar | A ratified protocol change adding cold-cost columns (Q1.3 currently confines it) |
| Linux re-run of this suite | Phase 8 (Q5.2), consuming this phase's shipped artifacts |

## External references

- Go toolchain current version: <https://go.dev/VERSION?m=text> (`go1.26.5`, fetched
  2026-07-20); release notes <https://go.dev/doc/go1.26> (Green Tea GC default)
- `text/template` / `html/template`: <https://pkg.go.dev/text/template>,
  <https://pkg.go.dev/html/template>; escaping tables
  <https://github.com/golang/go/blob/master/src/html/template/html.go>; the `+` decision
  <https://github.com/golang/go/issues/42506>
- `testing` package (`B.Loop`, `B.ReportAllocs`): <https://pkg.go.dev/testing>
- Go modules `tool` directive: <https://go.dev/ref/mod#go-mod-file-tool>
- benchstat: <https://pkg.go.dev/golang.org/x/perf/cmd/benchstat> (run-count guidance); module
  version via <https://proxy.golang.org/golang.org/x/perf/@latest>
- templ: <https://github.com/a-h/templ> (v0.3.1020 via
  <https://proxy.golang.org/github.com/a-h/templ/@latest>); whitespace behavior
  <https://github.com/a-h/templ/issues/90>; docs pages cited per fact in
  [templ-feasibility.md](templ-feasibility.md#the-engine-facts-this-analysis-rests-on)
- quicktemplate: <https://github.com/valyala/quicktemplate> (README: `stripspace` /
  `collapsespace` / `{%- -%}`); v1.8.0 + 2024-07-04 via
  <https://proxy.golang.org/github.com/valyala/quicktemplate/@latest>
- slinso/goTemplateBenchmark (ecosystem framing precedent):
  <https://github.com/slinso/goTemplateBenchmark>
- Phase 1 spec (binding): [README](../phase-1-cross-stack-foundation/README.md),
  [workloads.md](../phase-1-cross-stack-foundation/workloads.md),
  [parity-contract-v2.md](../phase-1-cross-stack-foundation/parity-contract-v2.md),
  [golden-corpus.md](../phase-1-cross-stack-foundation/golden-corpus.md),
  [metrics-protocol.md](../phase-1-cross-stack-foundation/metrics-protocol.md)
- Repo conventions bound by this spec:
  [spec-conventions](../../common/spec-conventions.md),
  [cross-cutting-decisions](../../common/cross-cutting-decisions.md) (amendments ledger)
