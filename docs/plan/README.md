# Heddle cross-stack template-engine benchmark — plan

> An eight-phase program that extends Heddle's parity-gated intra-.NET benchmark credibility across
> five language ecosystems — Rust, JVM, JS/Node, Python, Go — under one golden-output corpus, one
> cross-language parity contract, and one metrics/publication protocol, consolidated into a single
> cross-stack report and cross-checked, last and separately, on the same machine under Ubuntu 24.04.

## Phases

| # | Phase | Depends on | Goal (one line) | Status |
|---|---|---|---|---|
| 1 | [cross-stack-foundation](phase-1-cross-stack-foundation.md) | — | Expanded eight-workload set (raw + encoding-ON), golden oracle corpus, parity contract v2, and the metrics/publication protocol every later phase measures against | in progress |
| 2 | [rust](phase-2-rust.md) | 1 | Askama + Tera on both fairness tracks under Criterion — the closest-architectural-peer fair fight that makes "Heddle is fast" falsifiable | in progress |
| 3 | [jvm](phase-3-jvm.md) | 1 | JTE + Thymeleaf under JMH — the compiled JVM peer paired with the JVM's largest enterprise server-side templating install base | in progress |
| 4 | [js](phase-4-js.md) | 1 | Handlebars + Eta under mitata — reach/context evidence against what the largest practitioner audience actually uses, with explicit not-fair-fight framing | in progress |
| 5 | [python](phase-5-python.md) | 1 | Jinja2 + Mako under pyperf — the survey's most relatable data point, quantifying the cross-runtime gap credibly | in progress |
| 6 | [go](phase-6-go.md) | 1 | html/template + templ under Go testing/benchstat — the cast's only runtime contextual encoder and the program's third compiled/typed data point | in progress |
| 7 | [consolidated-report](phase-7-consolidated-report.md) | 1, 2–6 (any shipped subset) | The single consolidated cross-stack report — every shipped ecosystem's protocol-conformant results juxtaposed on wall time per render, dual-track, with the workload-shape strengths-and-weaknesses analysis | in progress |
| 8 | [linux-crosscheck](phase-8-linux-crosscheck.md) | 1, 2–7 (any shipped subset) | The same physical machine booted into Ubuntu 24.04 — the full protocol suite re-run across every shipped ecosystem, separately published as a Linux cross-check validating that the program's rankings, relative gaps, and dispersion findings hold under Linux CPU-isolation conditions | in progress |

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
