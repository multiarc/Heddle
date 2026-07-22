# Cross-stack benchmarks (spec initiative)

The implementation-ready specification of the eight-phase cross-stack benchmark program
ratified in the [owning plan](../../plan/README.md): extending Heddle's parity-gated
intra-.NET benchmark credibility across five language ecosystems — Rust, JVM, JS/Node,
Python, Go — under one golden-output corpus, one cross-language parity contract, and one
metrics/publication protocol, consolidated into a single cross-stack report and cross-checked,
last and separately, on the same machine under Ubuntu 24.04. Every phase below is folder-shaped
per the [spec conventions](../common/spec-conventions.md); every plan open question is closed
(resolved by the user 2026-07-20 in the plan's
[Q&A register](../../plan/open-questions.md)), so this initiative carries **no
open-questions register** — none is needed, per the
[no-open-questions discipline](../common/spec-conventions.md#no-open-questions).

## Phases

All eight phases share one status: **Specified — ready for implementation.**

| # | Phase | Purpose and most consequential decisions | Status |
|---|---|---|---|
| 1 | [cross-stack-foundation](phase-1-cross-stack-foundation/README.md) | The keystone every later phase consumes: eight workloads (three anchors byte-unchanged + five new incl. the encoding-ON pair), the exported golden corpus under `src/Heddle.Performance/GoldenCorpus/` (D6), parity contract v2 with the N1–N5 pipeline and the N3b strip-at-comparison rule (D8), the metrics & publication protocol (D12–D14), a Handlebars.Net five-entity `ITextEncoder` (D3), and controlled-track-only intra-.NET scope (D15) | Specified — ready for implementation |
| 2 | [rust](phase-2-rust/README.md) | Askama 0.16.0 + Tera 2.0.0 under Criterion 0.8.2 on Rust 1.97.1 — the fair-fight peer ecosystem and the contract's first external consumer; establishes the top-level `benchmarks/<ecosystem>/` harness convention (D2), reconciles Askama's decimal spellings via N5 in the gate runner rather than a custom escaper (D4), and files errata upstream through the amendments ledger, never patching locally (D14) | Specified — ready for implementation |
| 3 | [jvm](phase-3-jvm/README.md) | JTE 3.2.4 + Thymeleaf 3.1.5.RELEASE under JMH 1.37 on Temurin 25 — the phase's riskiest port (Thymeleaf controlled track) is feasibility-probed first via a block-only authoring pattern and probe ladder (D1); JTE's encoded suite renders through a custom `FiveEntityHtmlOutput` (D4); Maven harness at `benchmarks/jvm/` (D8); one verifier-needle erratum settled directly in the unshipped Phase 1 spec (D6) | Specified — ready for implementation |
| 4 | [js](phase-4-js/README.md) | Handlebars 4.7.9 + Eta 4.6.0 under mitata 1.0.34 on Node 24.18.0 — reach/context evidence with explicit not-fair-fight framing; the suite is time-only (D3, closing Q4.1), stock Handlebars is reconciled by N5 in the gate with no monkey-patching (D4), and a five-run Windows stability procedure gates first publication (D13) | Specified — ready for implementation |
| 5 | [python](phase-5-python/README.md) | Jinja2 3.1.6 + Mako 1.3.12 under pyperf 2.10.0 on CPython 3.14.6 — the survey's most relatable data point; the contract gate executes inside every pyperf worker (D7), the Q5.1 memory metric comes from a separate tracemalloc pass (D11), and the Q5.2 stability posture is disclosed rather than tuned away (D9) | Specified — ready for implementation |
| 6 | [go](phase-6-go/README.md) | text/template (raw) + html/template (encoded) per Q6.1 plus templ under `go test -bench` + benchstat on Go 1.26 — the cast's only runtime contextual encoder; templ controlled-track feasibility is analyzed with an S1 verification spike and both outcome branches specified (D6); the phase's former whitespace-only escalation is resolved program-wide by the N3b ruling (D13); quicktemplate is an operationalized conditional stretch (D10) | Specified — ready for implementation |
| 7 | [consolidated-report](phase-7-consolidated-report/README.md) | The single consolidated cross-stack report over whichever subset of phases 2–6 shipped (Q7.2, no quorum): exactly one immutable `docs/benchmarks/<date>/` directory as the sole surface (D2, Q7.1 as amended 2026-07-20), every figure a verbatim excerpt of a published source table with unit conversion as the only arithmetic (D4), no cross-ecosystem ranking of non-Heddle engines (D6), stdlib-only `consolidate.py` assembly tooling (D13) | Specified — ready for implementation |
| 8 | [linux-crosscheck](phase-8-linux-crosscheck/README.md) | The same protocol machine booted bare-metal into Ubuntu 24.04, re-running the full suite for every shipped ecosystem and publishing a separate, clearly-labeled cross-check report that states — at fixed material-divergence thresholds and in dimensionless dispersion form — whether the Windows findings held, with no cross-OS absolute-time juxtaposition anywhere | Specified — ready for implementation |

## Supplementary documents

Every supplementary document of every phase, as enumerated by the phases' own Documents
tables (entry documents are linked in the phase table above; no orphan files exist).

| Phase | Document | Purpose |
|---|---|---|
| 1 | [workloads.md](phase-1-cross-stack-foundation/workloads.md) | The eight workloads: exact template shapes per engine, exact model data, expected output characteristics, owned dimensions |
| 1 | [parity-contract-v2.md](phase-1-cross-stack-foundation/parity-contract-v2.md) | Parity contract v2 full text: normalization pipeline, entity canonicalization, untrusted-data alphabet, controlled/idiomatic gates, exclusion policy |
| 1 | [golden-corpus.md](phase-1-cross-stack-foundation/golden-corpus.md) | Golden corpus: on-disk format, manifest, export tool, verification commands, verifier definitions, regeneration policy |
| 1 | [metrics-protocol.md](phase-1-cross-stack-foundation/metrics-protocol.md) | Metrics & publication protocol full text: statistic mapping, presentation rules, machine record, report format, honest-reporting rules |
| 2 | [workload-ports.md](phase-2-rust/workload-ports.md) | The 32 cells: normative Askama/Tera template texts per workload per track, Rust model construction, per-cell authoring notes and gate assets |
| 3 | [thymeleaf-controlled-feasibility.md](phase-3-jvm/thymeleaf-controlled-feasibility.md) | The phase's riskiest port, retired first: divergence taxonomy, the block-only authoring pattern, the feasibility-probe ladder, and the exclusion-evidence procedure |
| 3 | [construct-mapping.md](phase-3-jvm/construct-mapping.md) | The full 8 × 2 × 2 cell matrix with normative template texts per workload, engine, and track, plus the Java model definitions |
| 3 | [harness-and-jmh.md](phase-3-jvm/harness-and-jmh.md) | Harness layout under `benchmarks/jvm/`, Maven build, JTE precompilation, gate runner, JMH configuration, run procedure, and report assembly |
| 4 | [templates-and-models.md](phase-4-js/templates-and-models.md) | Normative JS model transcriptions and the exact template texts per engine and track, helper/partial registrations, and the report disclosure texts |
| 4 | [harness-and-run.md](phase-4-js/harness-and-run.md) | Harness layout under `benchmarks/js/`, dependency pinning, gate implementation, mitata run shape, deopt handling, the Windows stability verification procedure, and the publication/report format |
| 5 | [templates.md](phase-5-python/templates.md) | Normative template texts and authoring rules for both engines on both tracks, per workload, with the idiomatic track's per-implementation documentation citations |
| 5 | [harness.md](phase-5-python/harness.md) | The pyperf harness: directory layout, pinned environment, gate runner, exact CLI invocations, the separate tracemalloc memory pass, cold-compile pass, and the report-format instantiation |
| 6 | [templ-feasibility.md](phase-6-go/templ-feasibility.md) | The templ controlled-track feasibility analysis: per-workload whitespace trace, the S1 verification spike, and both outcome branches fully specified |
| 6 | [port-mapping.md](phase-6-go/port-mapping.md) | Per-workload Go port: model transcription, controlled template texts for text/template, html/template, and templ; composition mapping; idiomatic-track authoring; quicktemplate twin shape |
| 6 | [harness-and-measurement.md](phase-6-go/harness-and-measurement.md) | Module layout, N1–N5 Go implementation, gates, `testing.B` shape, run/stability settings, benchstat, and the report format |
| 7 | [report-assembly.md](phase-7-consolidated-report/report-assembly.md) | The normative consolidated-report format and assembly mechanics: directory contents, section order, table shapes and provenance, `sources.json` schema, `consolidate.py` contract, drift-review procedures, and the publication checklist |
| 8 | [environment-and-toolchains.md](phase-8-linux-crosscheck/environment-and-toolchains.md) | Dual-boot establishment rules, the one-machine-state rule, the closed environment-record checklist, version pinning + delta procedure, CPython provenance |
| 8 | [harness-settings-and-validation.md](phase-8-linux-crosscheck/harness-settings-and-validation.md) | Per-harness Linux stability settings for all six harnesses, gate re-assertion + failure triage, measurement-run matrix, two-part report format, material-divergence thresholds |

## Cross-phase conventions

Shared material is owned once and referenced, never restated. What phases 2–8 actually share:

- **Contract, corpus, and protocol live in Phase 1 by design** (the plan's standing ruling).
  The normalization pipeline — including **N3b**, the strip-at-comparison rule under which
  whitespace runs are removed from both oracle and candidate so the controlled gate compares
  only non-whitespace bytes — is owned by
  [parity-contract-v2.md](phase-1-cross-stack-foundation/parity-contract-v2.md#normalization-pipeline);
  its decision record is
  [Phase 1 D8](phase-1-cross-stack-foundation/README.md#d8--normalization-v2-v1s-three-steps--defined-encoding--portable-whitespace-definition--n3b-run-collapse).
- **Harness location convention:** each ecosystem harness lives under a top-level
  `benchmarks/<ecosystem>/` directory — established as the cross-phase convention by
  [Phase 2 D2](phase-2-rust/README.md#d2--harness-location-benchmarksrust-a-new-top-level-benchmarks-directory)
  and instantiated (not re-decided) by phases 3–6.
- **Maintainer rulings recorded in the phase decision records (2026-07-20/21):** the N3b
  whitespace-run collapse, option A, which closed Phase 6's escalation program-wide
  ([Phase 1 D8](phase-1-cross-stack-foundation/README.md#d8--normalization-v2-v1s-three-steps--defined-encoding--portable-whitespace-definition--n3b-run-collapse),
  [Phase 6 D13](phase-6-go/README.md#d13--whitespace-only-divergence-resolved-program-wide-by-the-n3b-whitespace-run-collapse-maintainer-ruling-2026-07-20-option-a));
  the rejection of corpus-versioning ceremony
  ([Phase 1 D7](phase-1-cross-stack-foundation/README.md#d7--corpus-regeneration-is-an-ordinary-versioned-change-q15));
  the Q7.1 amendment making the date-stamped directory Phase 7's sole publication surface
  ([Phase 7 header](phase-7-consolidated-report/README.md#header)); and the verifier-needle
  erratum settled directly in the unshipped Phase 1 spec
  ([Phase 3 D6](phase-3-jvm/README.md#d6--jte-idiomatic-encoded-cells-need-a-verifier-needle-amendment-erratum-not-local-patch)).

**Common documents: none.** Mirroring the owning plan's own finding, no
`cross-stack-benchmarks` common document is warranted: the shared contract/protocol material
is owned by Phase 1 by design and referenced by phases 2–8, and the derivation check found no
material stated normatively in two or more phase folders — per-phase restatements are
citations of Phase 1's owned text, not duplicated ownership.
