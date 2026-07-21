# Go benchmark harness (Phase 6 — go)

The Go module for the cross-stack benchmarks Phase 6
([spec](../../docs/spec/cross-stack-benchmarks/phase-6-go/README.md)): the stdlib engine
(text/template on the raw suites, html/template on the encoded suite — Q6.1) and templ, both
fairness tracks, gated against the Phase 1 golden corpus at
`src/Heddle.Performance/GoldenCorpus/` (read repo-relative; the corpus does not move).

Module: `heddle.dev/benchmarks/go` (repo-local, never published). Pins (spec D3): `go 1.26` /
`toolchain go1.26.5`; `github.com/a-h/templ v0.3.1020` (runtime require + CLI `tool`
directive); benchstat via `tool golang.org/x/perf/cmd/benchstat` at
`golang.org/x/perf v0.0.0-20260709024250-82a0b07e230d`. Generated `*_templ.go` files are
committed; regenerate with the pinned CLI only:

```
go tool templ generate
```

## How to run

From `benchmarks/go/`:

```
go vet ./...          # static checks
go test ./internal/...  # unit tests: N1–N5 pipeline, verifier calibration, alphabet assert, model pins, S1 probes
go test ./suites      # the phase's parity command: TestMain runs every registered gate before anything times
```

Layout (harness-and-measurement.md §Module layout):

- `internal/corpus/` — corpus loader + manifest SHA-256 check, N1–N5 pipeline (N3b comparison
  strip included), controlled byte gate + encoded security floor, idiomatic verifier,
  untrusted-data alphabet assert (D5).
- `internal/model/` — the eight pinned models (C# field spellings, `strconv`/`%d` numerics);
  composed-page fragments embedded byte-exact as runtime data under
  `internal/model/data/composed-page/` (pinned `-text` in `.gitattributes`) and byte-checked
  against the oracle by `TestComposedAssemblyMatchesTheCorpusOracle`.
- `internal/spike/` — the S1 templ feasibility probes (evidence, excluded from timing).
- `suites/` — `gate_test.go` (TestMain + gate registry; engine cells register here as WI3–WI5
  land), later `bench_test.go` / `coldparse_test.go`.

Benchmarks, the runner script (`run-benchmarks.ps1`), and the engine ports land with WI3–WI9.

## S1 results (templ feasibility spike — WI2)

Executed 2026-07-21 at the pinned toolchain (`go1.26.5`, templ `v0.3.1020`, Windows 11).
Command: `go test ./internal/spike/ -v` (probes are the committed tests in
`internal/spike/spike_test.go`; templ sources in `internal/spike/spike.templ`, generated code
committed alongside). **Verdict: clean pass on all four probes — the expected outcome of
[templ-feasibility.md](../../docs/spec/cross-stack-benchmarks/phase-6-go/templ-feasibility.md).
No non-whitespace divergence surfaced, so no `EXCLUSIONS.md` and no `internal/spike/attempts/`
exist; the implementation plan proceeds unchanged (WI3+).**

### P1 — smallest-workload byte gate: PASS (byte-identical, pre-N3b)

`trivial-substitution.templ` authored densely renders, after N1–N4, **byte-identical to the
committed oracle even before the N3b comparison strip** — templ's collapser found nothing to
change in the dense form, and templ emitted the void element as `<img src="/img/handbook.png">`
(no self-closing slash), matching the oracle. Observed normalized output (338 bytes = the
oracle's byte length):

```
<article><h1>Heddle Handbook</h1><p class="sku">HB-2001</p>…<a class="link" href="/catalog/handbook"><img src="/img/handbook.png"></a>…</article>
```

Fixed-point check: `<p>{ a } { b } { c }</p>` with single literal spaces renders
`<p>Mercantile 2026 support at mercantile.example</p>` — **the single spaces survive
verbatim** (F3 fixed point confirmed; the mixed-page footer construct is safe).

### P2 — escaper bytes: PASS (every spelling inside the N5 table)

Text path (fortunes-shaped row over `& < > " '` + Japanese) — observed bytes:

```
<tr><td>1</td><td>&amp; &lt; &gt; &#34; &#39; こんにちは</td></tr>
```

Attribute path (encoded-loop-shaped row, `data-tag={ tag }`, pinned row-0 data) — observed
bytes (templ auto-quotes the attribute with `"` and uses the same escaper on both paths):

```
<tr><td data-tag="tag-0&amp;&#39;0&#39;">item &lt;0&gt; &amp; &#34;co&#34;</td><td>&#39;q&#39; &amp; &lt;angle&gt; &#34;d&#34; こんにちは 0</td></tr>
```

Emitted spellings: `&amp; &lt; &gt; &#34; &#39;` on **both** paths (templ.EscapeString →
stdlib html.EscapeString; the attribute path did *not* emit `&quot;`) — **every spelling is
inside the N5 table**; the only non-canonical spelling is `&#34;`, which N5 maps to `&quot;`,
after which both outputs equal the canonical oracle bytes exactly. The Japanese strings pass
through byte-intact, and escape-free data round-trips unescaped (raw-suite no-op premise
confirmed). The non-whitespace contingency (a spelling outside N5) did **not** occur.

### P3 — style passthrough: PASS (byte-identical)

The exact mixed-page `<style>` line (CSS braces, single spaces) renders with its content
**byte-identical to the authored bytes** (616 bytes total, `<style>` + 601-byte CSS +
`</style>`) — F4 executed and confirmed; the `@templ.Raw` fallback of port-mapping rule 6 is
**not** needed; mixed-page will be authored with a literal `<style>` element.

### P4 — composed-page inter-statement boundary: ZERO separator bytes (evidence)

Two-plus consecutive `@templ.Raw(...)` calls on separate lines, payloads = the pinned
`comp-assets-styles` → `comp-custom-styles` → `comp-head-scripts` fragments (the oracle's one
non-tag-flanked boundary). Observed: **templ emits zero bytes between consecutive statement
nodes** — the output is the exact zero-separator concatenation, containing

```
…/main.css" />/* CSS Comment Test */<script src="/head.js">…
```

byte-identical to the oracle's boundary. The formerly-hypothesized separator space does not
exist at this boundary (and even if it did, it would be whitespace-only and pass via N3b —
D13). `composed-page` can be authored with the separate-line `@templ.Raw` form (port-mapping
rule 5's default).
