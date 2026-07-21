# Go harness, gates, measurement settings, and report format

Supplementary document of the [Phase 6 — go spec](README.md). It pins the Go module layout, the
gate implementations (controlled byte gate, idiomatic verifier, security floor), the
`testing.B` benchmark shape, the repeated-run and stability settings for the protocol machine,
and the phase's report format as an instantiation of the Phase 1
[metrics & publication protocol](../phase-1-cross-stack-foundation/metrics-protocol.md).

## Module layout

```
benchmarks/go/                       ← D2: new top-level benchmarks/<ecosystem>/ convention
  go.mod / go.sum                    ← module heddle.dev/benchmarks/go (local-only, never published)
  README.md                          ← how to run; S1 results block; regeneration commands
  EXCLUSIONS.md                      ← created only on a confirmed non-whitespace exclusion (templ-feasibility.md)
  internal/model/                    ← pinned models (port-mapping.md), incl. composed.go fragments
  internal/corpus/                   ← corpus loader, N1–N5 pipeline, byte gate, verifier, alphabet assert
  internal/stdlibtpl/                ← text/template + html/template sources & renderers, both tracks
  internal/templeng/                 ← controlled .templ + generated *_templ.go (committed)
  internal/templeng/idiomatic/       ← idiomatic .templ + generated (committed)
  internal/spike/                    ← S1 probes + attempts/ (evidence, excluded from timing)
  internal/qtpl/                     ← created only if the quicktemplate stretch triggers (D10)
  suites/                            ← bench_test.go, gate_test.go, coldparse_test.go, TestMain
  run-benchmarks.ps1                 ← the reproduce-it-yourself entry point (below)
```

Corpus files are read at gate time from the repo-relative path
`../../src/Heddle.Performance/GoldenCorpus/` (resolved from the module root via
`runtime.Caller`-anchored path, so `go test` works from any working directory). The corpus does
not move (Phase 1 [D6](../phase-1-cross-stack-foundation/README.md#d6--the-corpus-stores-the-normalized-oracle-under-srcheddleperformancegoldencorpus)
names relocation only if a consumer *cannot* reach it — a relative path within one repo can).

## Normalization pipeline (Go implementation)

`internal/corpus/normalize.go` implements the contract's closed list, nothing more:

- **N1** — `utf8.Valid(bytes)` must hold (invalid UTF-8 = gate failure); a leading BOM is *not*
  stripped (it flows into the comparison and fails it).
- **N2** — `strings.ReplaceAll(s, "\r\n", "\n")` then `strings.ReplaceAll(s, "\r", "\n")`.
- **N3** — collapse `>\s+<` to `><` with *whitespace* pinned to the contract's six-character set:
  regex `>[\t\n\v\f\r ]+<` (RE2 `\s` is the ASCII subset `[\t\n\f\r ]`, which **omits VT (U+000B)**
  that the contract's six-character set includes, so the explicit class is used, not `\s`),
  replaced repeatedly until no match; a single `regexp.ReplaceAllString` pass suffices
  for the same reason the contract records (`><` reintroduces no whitespace).
- **N3b** — remove every whitespace run anywhere (collapse to **nothing**, not to a space; the
  contract's 2026-07-20 maintainer step): `regexp.MustCompile("[\t\n\v\f\r ]+").ReplaceAllString(s, "")`,
  same six-character set. Applied at comparison to **both** the candidate's normalized output and
  the loaded oracle, so the gate compares their non-whitespace bytes; any whitespace-only divergence
  (run-length or presence/absence) passes. (N3b subsumes N3/N4 at comparison — they remain only to
  produce the stored oracle's readable bytes.)
- **N4** — `strings.Trim(s, "\t\n\v\f\r ")` (the same six characters — not `strings.TrimSpace`,
  which trims Unicode space and would exceed the closed definition).
- **N5** (encoded suite only) — the contract's five-character canonicalization table as a single
  left-to-right scan without rescanning replacements: implemented as a hand-rolled scanner over
  `&…;` candidates (match named spellings case-sensitively; numeric spellings per the contract's
  leading-zero / case-insensitive-hex rules; map to `&amp; &lt; &gt; &quot; &#39;`), not as
  sequential `ReplaceAll` calls (which would rescan). Unit-tested against the contract's table
  rows plus the no-rescan example (`&amp;#39;` stays `&amp;#39;`).

## Controlled byte gate

`internal/corpus/gate.go`: `AssertControlled(workloadID, suite, render func() string)` —
render, normalize (N1–N4, +N5 when `suite == "encoded"`), UTF-8 bytes, compare with the corpus
entry; on mismatch, fail with workload id, engine name, expected/actual byte lengths, first-diff
index, and a ±40-char / 120-char-window excerpt (the `ParityCheck.Describe` shape). The manifest
`hash` is also recomputed from the corpus file and checked, so a corrupted checkout is
distinguished from a divergent render.

**Encoded security floor** (contract §controlled-track-gate 5): for encoded workloads the gate
additionally asserts, on the **un-normalized** candidate: zero occurrences of `<script>alert(`,
and the expected count of the escaped form after N5 (`&lt;script&gt;alert(` — expected counts: 1
for fortunes-encoded, 0 for encoded-loop, whose data carries no script payload).

**Alphabet assert** ([port-mapping.md](port-mapping.md#model-transcription-shared-by-all-engines-and-tracks)):
gate startup scans every materialized encoded-suite model string for `+ = `` ` `` `, the `&#`
substring, U+00A0–U+00FF, and surrogate-requiring code points, failing with the offending value.

**When gates run.** `suites/gate_test.go` defines `TestMain(m *testing.M)`: before `m.Run()` it
executes every controlled gate (all eight workloads × every engine present in the run) and every
idiomatic verification; any failure prints the failure surface and `os.Exit(1)` — so **no
benchmark in the package can time until every gate passed in the same invocation**, satisfying
the contract's before-any-timing rule. The same checks are additionally exposed as ordinary
`Test…` functions so plain `go test ./suites` is the phase's parity command (the Go analogue of
the .NET `-- parity` verb).

## Idiomatic verifier

`internal/corpus/verify.go` loads `<id>.verify.json` and implements the contract's four check
kinds exactly (values / ordered markers / forbidden — checked against both raw and normalized
output — / required), with the contract's failure-report shape (check kind, entry,
expected-vs-found). Conformance proof: a unit test feeds each workload's **golden** corpus entry
through the verifier (must accept) and each of the Phase 1 calibration corruptions synthesized
the same way (removed row / reordered sections, + unescaped payload for encoded — recipes per
[golden-corpus.md](../phase-1-cross-stack-foundation/golden-corpus.md#verification)) (must
reject with the correct kind). This proves the Go verifier is the same verifier, not a
lookalike.

## Benchmark shape (`testing.B`)

One benchmark function per workload × engine × track in `suites/bench_test.go`, named for
benchstat grouping:

```
BenchmarkRender/controlled/composed-page/stdlib-text
BenchmarkRender/controlled/composed-page/templ
…
BenchmarkRender/idiomatic/encoded-loop/stdlib-html
BenchmarkRender/idiomatic/encoded-loop/templ
```

(top-level `BenchmarkRender` with nested `b.Run(track, …)` → `b.Run(workload, …)` →
`b.Run(engine, …)`; 8 workloads × 2 engines × 2 tracks = 32 cells, +16 if the quicktemplate
stretch adds a third engine). Body shape, normative:

```go
b.ReportAllocs()                 // allocs/op + B/op on every benchmark, independent of flags
for b.Loop() {                   // Go 1.24+ loop; keeps the result alive (no DCE), excludes setup
    sink = render()              // package-level var sink string
}
```

- `b.ReportAllocs()` (not only `-benchmem`) so allocation figures are structural, not
  flag-dependent.
- `b.Loop()` rather than `for i := 0; i < b.N; i++` — it prevents dead-code elimination of the
  render result and auto-excludes setup/teardown from timing (Go testing docs), which is the
  modern stdlib-recommended shape on the pinned toolchain; the `sink` assignment is
  belt-and-braces.
- Template parse / `templ` component setup / model materialization all happen in package `init`
  or `TestMain` — never inside the benchmark — per the protocol's cached-render definition.
- Renders write into a per-benchmark `bytes.Buffer` that is `Reset()` at the top of each
  iteration inside `render()`; the buffer is pre-grown once to the workload's output size so
  neither growth policy nor the reset is a per-engine variable (identical helper for all
  engines; the engines differ only in the render call itself).

## Cold parse/compile sidebar (Q1.3 — per-ecosystem only)

`suites/coldparse_test.go`: `BenchmarkColdParse/stdlib-text` and
`BenchmarkColdParse/stdlib-html` parse the full workload template set from source per iteration
(`template.New(...).Parse(...)` for each). **Scope caveat (html/template).** html/template runs
its contextual escaping analysis **lazily at the first `Execute`, not at `Parse`** (Go
html/template), so a Parse-only loop does not capture that escape-compilation step. Both stdlib
cold rows are therefore labeled **cold parse only** (not parse + escape-compile); html/template's
escaper-compilation cost is out of scope for this per-ecosystem, non-comparable sidebar and is not
published under a "compile" claim it does not earn — the sidebar's only load-bearing point is the
templ = AOT-zero asymmetry, which the parse-only stdlib figures already establish. templ (and
quicktemplate) have **no runtime parse step** — their cells print
`AOT — no runtime parse (compiled by go generate)` in the report, the same asymmetry statement
Phase 1 records for Heddle/Askama/JTE. These figures appear only in the per-ecosystem sidebar,
labeled non-comparable, never as a cross-language column.

## Repeated runs, stability settings, benchstat

Fixed for every published Phase 6 run on the protocol machine (Windows 11, Ryzen 9 9950X — Q1.6):

| Setting | Value | Rationale |
|---|---|---|
| Repeat count | `-count=20` | benchstat's documented guidance: "at least 10, ideally 20" (pkg.go.dev/golang.org/x/perf/cmd/benchstat) |
| Per-benchmark time | `-benchtime=1s` (harness default, stated explicitly) | default keeps the harness stock/credible (Q2.1 spirit); 20 × 1s per cell bounds the full 32-cell run under ~1.5 h including gates |
| Bench selection | `-run '^$' -bench '^BenchmarkRender$'` (cold-parse sidebar in a separate invocation with `-bench '^BenchmarkColdParse$'`) | keeps unit tests out of timed runs |
| Process priority | High — the test binary is prebuilt (`go test -c -o bench.exe ./suites`) and launched via `cmd /c start /high /wait /b bench.exe -test.run ^$ -test.bench … -test.count=20` from `run-benchmarks.ps1` | mirrors BenchmarkDotNet's elevated-priority default used by the published .NET runs on this box; priority is set at process creation (no race) |
| CPU affinity | none (all 32 logical CPUs available) | continuity with the published .NET protocol runs (BenchmarkDotNet default: no affinity); **recorded fallback trigger**: if benchstat reports variation > ±5% on any suite, that suite is re-run pinned to CCD0 (`start /high /affinity 0xFFFF …`) and the report records the pinning and shows the pinned numbers with the unpinned dispersion noted |
| `GOMAXPROCS` / GC | runtime defaults, values recorded in the environment block (`GOMAXPROCS=32`, `GOGC=100`, Go 1.26 Green Tea GC default) | render loops are single-goroutine; defaults are the stock-harness posture; the Go 1.26 GC change is noted in the report as a discontinuity marker vs any pre-1.26 literature numbers |
| Output | `> results\bench-<track>-<yyyyMMdd>.txt` per invocation; `benchstat bench-….txt > benchstat-….txt` | benchstat consumes the standard `go test -bench` text format |

"Wall time per render" for this ecosystem = **benchstat's `sec/op` summary (median-based)** with
its `±%` variation published alongside — fixed by the Phase 1
[statistic mapping](../phase-1-cross-stack-foundation/metrics-protocol.md#wall-time-statistic-mapping-q21);
this spec may not and does not vary it. Tables containing the Heddle reference row also print
ns/render so the ratio column is dimensionless.

`run-benchmarks.ps1` (committed) performs, in order: toolchain version assertions
(`go version` = `go1.26.5`, `go tool templ version` = `v0.3.1020` — the pinned CLI is invoked
through `go tool`, never a global install, per [D3](README.md#d3--toolchain-and-dependency-pins)),
`go tool templ generate` + `git diff --exit-code -- '*_templ.go'` (regeneration freshness — committed generated code must match the
pinned generator), `go vet ./...`, `go test ./...` (gates + unit tests), the prebuild, the two
timed invocations, and benchstat. It is the reproduce command the report prints.

## Report format (instantiates the protocol)

Published as `docs/benchmarks/<yyyy-MM-dd>/` — `index.md` in the pinned protocol shape plus the
raw `bench-*.txt` and `benchstat-*.txt` artifacts. Phase-specific content requirements, all
mandatory:

1. **Environment block** — protocol fields plus: `go1.26.5`, templ `v0.3.1020`, benchstat
   (`golang.org/x/perf v0.0.0-20260709024250-82a0b07e230d`), quicktemplate `v1.8.0` (only if
   included), `GOMAXPROCS`, priority/affinity actually used, corpus `generatingCommit`.
2. **Interpreted-vs-precompiled organization** — results grouped as *runtime/reflection tier*
   (the stdlib engine) vs *AOT-compiled tier* (templ, + quicktemplate if included), the
   slinso/goTemplateBenchmark framing named once with a link; each engine's role labeled
   (stdlib baseline / compiled peer).
3. **Q6.1 surface labeling** — every raw-suite stdlib row is labeled
   `Go stdlib (text/template — raw path)` and every encoded-suite stdlib row
   `Go stdlib (html/template — auto-escaping path)`, with one methodology sentence presenting
   them as the one stdlib engine's two paths.
4. **Heddle reference row + ratio anchor (Q2.2 = A, Q6.2)** — per workload, the labeled
   wall-time-only row `Heddle (reference — .NET 10, same machine, from <date> run)` excerpted
   from the Phase 1 protocol publication; the ratio column = engine time / Heddle reference
   time; Heddle row carries dashes in all allocation cells.
5. **Allocation sidebar** — allocs/op and B/op per engine, **within-ecosystem baseline = the
   credibility-pick stdlib engine**, realized per suite as its Q6.1 surface: **text/template**
   baselines the six raw-suite allocation rows, **html/template** the two encoded-suite rows.
   (metrics-protocol presentation rule 3 names this baseline "html/template"; because Q6.1 splits
   the stdlib surface per suite, that label is the encoded-suite instantiation of the one
   credibility-pick engine — html/template measures no raw-suite rows to baseline, so the raw-suite
   baseline is the same engine's raw surface, text/template. Rule 3's baseline-is-the-credibility-pick
   rule is unchanged and phase 7 still inherits a stdlib-baselined Go table — recorded correction
   per [spec-conventions §Relationship to the owning plan](../../common/spec-conventions.md#relationship-to-the-owning-plan).)
   Carries the protocol's verbatim allocation label; no table or prose juxtaposes Go
   allocation/GC numbers with another runtime's.
6. **Contextual-encoding section** — a dedicated section on the encoded suite that: identifies
   html/template as the program cast's only *runtime* contextual encoder (Heddle and JTE decide
   context at compile time; templ escapes with a flat five-character escaper); frames the
   html/template-vs-templ delta as a whole-architecture difference (runtime reflection +
   contextual escaping vs AOT codegen + flat escaping), never as the isolated cost of context
   analysis; and includes, adjacent to the encoded numbers, the protocol's **verbatim**
   confinement caveat: *"Encoded workloads confine untrusted data to HTML text and
   attribute-value contexts, where flat and contextual encoders emit the same escaped output.
   These numbers therefore measure escaping cost in confined contexts and cannot show the
   differentiating benefit of contextual encoding; a contextual encoder's encoded-suite result
   must not be read as 'context awareness is pure overhead.'"*
7. **Engine-selection record** — both ruled-in picks with their roles; hero excluded with its
   reason (maintenance status uncertain, research spike 2); quicktemplate's status either way —
   if excluded, cite the D10 rule outcome; included or not, disclose its dormancy evidence
   (last upstream commit 2024-07-04 = v1.8.0; no releases since).
8. **Honest-reporting rules 1–6** applied; specifically, every workload where the stdlib engine
   beats templ (or vice versa) on any metric is named in prose, and any suite where the
   interpreted baseline outperforms the compiled pick is called out as prominently as the
   converse.
9. **Cold-cost sidebar** per Q1.3 (this document, above), labeled non-comparable.
10. **Excluded cells** (if any) print `excluded — documented evidence` linking to
    `benchmarks/go/EXCLUSIONS.md` — never a blank cell.
