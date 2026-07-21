# Phase 2 — rust (spec)

## Header

- **Status:** Specified — ready for implementation
- **Source plan:** [phase-2-rust.md](../../../plan/phase-2-rust.md) (standing rulings in the
  [plan index](../../../plan/README.md); resolved Q&A register in
  [open-questions.md](../../../plan/open-questions.md) — Q2.1, Q2.2 = A, Q6.2, and Phase 1's
  Q1.1–Q1.7 resolutions are binding on this spec)
- **Assumes merged:** the complete
  [Phase 1 — cross-stack-foundation spec](../phase-1-cross-stack-foundation/README.md) —
  specifically the golden corpus and verifier JSON
  ([golden-corpus.md](../phase-1-cross-stack-foundation/golden-corpus.md)), parity contract v2
  ([parity-contract-v2.md](../phase-1-cross-stack-foundation/parity-contract-v2.md)), the
  metrics & publication protocol
  ([metrics-protocol.md](../phase-1-cross-stack-foundation/metrics-protocol.md)) including its
  published first protocol run (the source of every Heddle reference row), and the workload set
  ([workloads.md](../phase-1-cross-stack-foundation/workloads.md)). All are consumed
  **read-only**; any defect found here is recorded upstream (D14), never patched locally.

| Document | Purpose |
|---|---|
| [README.md](README.md) (this file) | Entry document: assumed state, design decisions, implementation plan, harness contract, gates, report |
| [workload-ports.md](workload-ports.md) | The 32 cells: normative Askama/Tera template texts per workload per track, Rust model construction, per-cell authoring notes and gate assets |

## Scope and goal

Deliver the Rust ecosystem comparison: **Askama 0.16.0** (performance/peer pick — compile-time
derive-macro codegen, Heddle's closest architectural peer) and **Tera 2.0.0** (credibility pick —
runtime-interpreted Jinja2-family, highest all-time Rust template-engine downloads), each porting
all eight Phase 1 workloads on both fairness tracks (32 cells, zero expected exclusions), gated
by parity contract v2 **before any timing**, measured under Criterion 0.8.2 on the Phase 1
protocol machine (Windows 11 / Ryzen 9 9950X), with a per-ecosystem allocation pass as a separate
non-timed run, published as one date-stamped `docs/benchmarks/<date>/` report carrying the
labeled wall-time-only Heddle reference row (Q2.2 = A) with the ratio column anchored to it
(Q6.2).

Out of scope: any other Rust engine (minijinja rejected at plan level; maud structurally
excluded), SSR/component frameworks, HTTP/database/throughput scope, modifications to the corpus
or contract, engine patches or forks (both engines measured as released on crates.io), and
cross-language verdicts beyond the protocol's wall-time rule (Phase 7 owns consolidation).

## Assumed state

Verified against the current repo and against primary sources while authoring; Phase 1 artifacts
are cited by section from its spec (Phase 1 is specified but not yet merged — implementation of
this phase starts only after Phase 1's corpus and protocol run exist, per the plan's dependency
order).

| Seam | Verified state |
|---|---|
| Golden corpus location/format | `src/Heddle.Performance/GoldenCorpus/<id>.golden.html` — **normalized** oracle bytes, UTF-8 no BOM, no trailing newline; `manifest.json` with `byteLength`/`sha256`/`generatingCommit`; `<id>.verify.json` per workload ([golden-corpus.md — location, on-disk format, manifest](../phase-1-cross-stack-foundation/golden-corpus.md#location-and-layout)). Consumers normalize their own output (N1–N5), then N3b removes every whitespace run from both that output and the loaded oracle before the byte compare |
| Normalization pipeline | Closed list N1–N5; whitespace = the six ASCII chars TAB/LF/VT/FF/CR/SPACE; N5 (encoded only) canonicalizes the five-char entity spellings per a closed table with leading-zero/hex-case tolerance and a no-rescan rule ([parity-contract-v2.md — normalization pipeline, N5](../phase-1-cross-stack-foundation/parity-contract-v2.md#normalization-pipeline)) |
| Gate ordering & failure surface | Gate runs before any timing in the same process; failure reports workload id, engine, byte lengths, first-diff index, ±40-char excerpt; failed suite publishes no numbers ([parity-contract-v2.md — controlled-track gate](../phase-1-cross-stack-foundation/parity-contract-v2.md#controlled-track-gate); [metrics-protocol.md — honest-reporting rule 5](../phase-1-cross-stack-foundation/metrics-protocol.md#honest-reporting-rules)) |
| Idiomatic gate | Verifier semantics (values / ordered markers / forbidden / required over N1–N4(+N5)-normalized output) and the Q1.7 authoring standard (official-doc patterns, doc pages cited per implementation file) ([parity-contract-v2.md — idiomatic-track gate](../phase-1-cross-stack-foundation/parity-contract-v2.md#idiomatic-track-gate)) |
| Statistic mapping | "Wall time per render" for Rust = the point estimate of Criterion's `mean`, dispersion = the 95% CI bounds — fixed in [metrics-protocol.md — wall-time statistic mapping](../phase-1-cross-stack-foundation/metrics-protocol.md#wall-time-statistic-mapping-q21); this spec pins versions/settings but may not vary the statistic |
| Presentation rules | Heddle reference row (label format pinned), ratio anchored to it, credibility pick (Tera) as within-ecosystem baseline for non-comparable metrics, track labels per table, verbatim allocation + encoded-caveat texts ([metrics-protocol.md — presentation rules, metric rules](../phase-1-cross-stack-foundation/metrics-protocol.md#presentation-rules-q22--a-q62)) |
| Composed-page twin authoring precedent | `Runners/TwinContent.cs` *(read)* — fragment consts (`SectionMeta`, `SectionSocial`, six `Comp*`), `AreaOrder` (7 names, one empty-content), `Areas` = `AreaComponent.Areas`; `Runners/LiquidTemplates.cs` *(read)* — home = `{% include 'layout' %}`, layout = ordered concatenation + `{% for name in area_names %}{{ areas[name] }}{% endfor %}`; `AreaComponent.cs` at `src/Heddle.Performance/TestSuite/Extensions/` *(read, head)* — multi-KB verbatim HTML literals |
| Anchor templates | `TestTemplates/trivial-substitution.heddle`, `large-loop.heddle` *(read)* — transcribed into the controlled texts of [workload-ports.md](workload-ports.md); model values from `Runners/SubstitutionContent.cs` *(read)* |
| Repo surface for the harness | No top-level `benchmarks/` directory exists *(listed)*; `Heddle.sln` references only `src/`; CI workflows (`dco/docs/dotnet/lsp/npm/samples.yml`) contain no glob that would pick up a new top-level directory *(listed)*; root `.gitattributes` is `* text=auto` + fixture pins *(read)* |
| Tera 2.0.0 (re-verified — spike D flagged the 1.x→2.0 rewrite) | crates.io stable = 2.0.0 (2026-06-26), a from-scratch rewrite. **Re-verified against 2.0.0 primary sources:** default `escape_html` escapes exactly `& < > " '` to `&amp; &lt; &gt; &quot; &#39;` — the canonical spellings — and does **not** escape `/` (docs.rs 2.0.0 source, `src/tera/utils.rs.html#108-130`; the doc comment mentions OWASP's `/` advice but no match arm exists for it); a `fast_escape` feature swaps in `pulldown_cmark_escape` but **no feature is enabled by default** (docs.rs feature list: "0 of them enabled by default"); `Tera::autoescape_on(suffixes)` survives in 2.0 (signature now `impl IntoIterator<Item = impl Into<Cow<'static, str>>>`), default autoescape on `.html`/`.htm`/`.xml`, disable via empty iterator; `set_escape_fn`/`reset_escape_fn` exist; whitespace control `{%- -%}` / `{{- -}}` / `{#- -#}`; `{% include %}` renders "using the current context"; `{% if %}/{% elif %}/{% else %}`, `{% for %}`, `{% extends %}`/`{% block %}` all present; 2.0 also adds a first-class `{% component name(params = default) %}…{% endcomponent %}` construct — the partial-with-explicit-arguments mechanism (Tera's docs state `{% include %}` cannot be passed a custom context, so components are the sanctioned way to do that), defined with `{% component %}` and invoked JSX-style `{{<ns.name .../>}}`; there is **no** `render_component` symbol (docs.rs `struct.Tera.html` 2.0.0; keats.github.io/tera docs — components and include sections) |
| Askama 0.16.0 (re-verified) | crates.io stable = 0.16.0, repo `askama-rs/askama`. **Escaper correction vs Phase 1's spike-C citation:** the 0.16.0 `Html` escaper emits **all-decimal** NCRs — `"`→`&#34;`, `&`→`&#38;`, `'`→`&#39;`, `<`→`&#60;`, `>`→`&#62;` (v0.16.0 tag, `askama/src/filters/escape.rs` doc table + `askama/src/html.rs` decimal-codepoint TABLE; test shows `<script>` → `&#60;script&#62;`). The legacy `&#x27;` hex spelling belongs to the pre-fork `djc/askama` escaper Phase 1 cited — see D4/D14. Whitespace control `-`/`~`/`+` with `#[template(whitespace = …)]`/`askama.toml` defaults; `#[template(path/source/ext/escape/config)]` attributes, `escape = "none"` overrides the extension-inferred escaper; includes get full context access incl. loop variables; `elif` and `else if` both accepted (0.16.0 book, `book/src/template_syntax.md`) |
| Harness/tooling versions | Criterion 0.8.2 current stable, MSRV 1.86, defaults: 3 s warm-up / 5 s measurement / 100 samples / 100k resamples / 0.95 confidence; no Windows-specific guidance in its book or tracker (spike D, crates.io + docs.rs). `allocation-counter` 0.8.1: thread-local `GlobalAlloc` wrapper over `std::alloc::System`, `measure(closure) -> AllocationInfo`, platform-agnostic, Windows in CI matrix. **The crate exposes no cargo features of its own** (docs.rs/crate/allocation-counter/0.8.1/features: "This release does not have any feature flags") and **self-installs its counting global allocator by declaring `#[global_allocator]` internally** — merely compiling the crate into a binary replaces `std::alloc::System`, which is exactly why the crate's own README gates it behind a *consumer-defined* feature; the documented minimal usage is `allocation_counter::measure(\|\| …)` with **no** user `#[global_allocator]` line (spike D, source read; docs.rs 0.8.1 crate docs). Current stable Rust = 1.97.1 (released 2026-07-16, a patch over 1.97.0 [2026-07-09] fixing a critical LLVM miscompilation present since 1.87; rust-lang blog) |

## Design decisions

### D1 — Version pins: Askama 0.16.0, Tera 2.0.0 (no features), Criterion 0.8.2, allocation-counter 0.8.1, Rust 1.97.1
- **Decision.** `Cargo.toml` pins exact versions: `askama = "=0.16.0"`, `tera = { version = "=2.0.0", default-features = false }`
  (2.0.0 has zero default features; the explicit flag documents intent and blocks accidental
  feature unification), `serde` (with `derive`) and `serde_json` pinned `=` to the current 1.x
  patch versions at implementation time (they are format-stable, not measurement-relevant, and
  not render-path code; the exact pins land in `Cargo.toml` and the lockfile and are recorded in
  the run's environment block), `criterion = "=0.8.2"` (dev-dependency),
  `allocation-counter = { version = "=0.8.1", optional = true }`.
  `Cargo.lock` is committed. `rust-toolchain.toml` pins `channel = "1.97.1"` (satisfies
  Criterion's MSRV 1.86 and both engines' MSRVs), target `x86_64-pc-windows-msvc` on the protocol
  machine. Tera's opt-in `fast` feature family is **not** enabled (see Deferred items).
- **Rationale.** "Engines as published, at spec-pinned versions" is a plan non-goal made
  concrete; exact `=` pins + lockfile + toolchain file make the published run reproducible
  bit-for-bit at the recorded commit. Stock configuration means default features — Tera's
  default (non-`fast`) escaper is also the one whose spellings were verified canonical.
- **Alternatives rejected.** Caret ranges (a silent minor bump would invalidate the recorded
  environment block); Tera `fast`/`fast_escape` (opt-in, and its `pulldown_cmark_escape` backend's
  spellings are unverified against N5 — deferred with trigger); `stable` channel in
  rust-toolchain.toml (drifts under the run).
- **Grounding.** crates.io/docs.rs version and feature pages (Assumed state rows); spike D;
  [Rust 1.97.1 announcement](https://blog.rust-lang.org/2026/07/16/Rust-1.97.1/) — the pinned
  patch, a critical LLVM-miscompilation fix over
  [Rust 1.97.0](https://blog.rust-lang.org/2026/07/09/Rust-1.97.0/); the patch (not 1.97.0) is
  pinned precisely because the miscompilation it fixes would otherwise be a live risk in the run.

### D2 — Harness location: `benchmarks/rust/`, a new top-level `benchmarks/` directory
- **Decision.** The Rust workspace lives at `benchmarks/rust/` — the first entry of a new
  top-level `benchmarks/` directory that phases 3–6 will reuse (`benchmarks/jvm/`,
  `benchmarks/js/`, …), recorded here as the cross-phase convention. Layout:

  ```
  benchmarks/rust/
    rust-toolchain.toml  Cargo.toml  Cargo.lock  askama.toml
    data/composed-page/…                ← fragment data files (workload-ports.md)
    templates/{controlled,idiomatic}/{askama,tera}/…
    src/
      lib.rs  models.rs  normalize.rs  corpus.rs  verifier.rs  gates.rs
      engines/{askama_controlled,askama_idiomatic,tera_controlled,tera_idiomatic}.rs
      bin/{gate,alloc_report,summarize}.rs
    benches/{controlled,idiomatic,cold}.rs
  ```

  The corpus stays where Phase 1 put it and is read via the repo-relative path
  `../../src/Heddle.Performance/GoldenCorpus/` (resolved from `CARGO_MANIFEST_DIR`), so Phase 1
  D6's revisit trigger ("harness cannot conveniently reach into `src/`") is **not** fired — a
  relative read is convenient. `.gitattributes` gains two lines in the same change:
  `benchmarks/rust/data/** -text` (fragment bytes must round-trip exactly) and
  `benchmarks/rust/templates/** text eol=lf` (template files; their line endings are erased by
  N2 but LF keeps diffs clean). `askama.toml` sets `[general] dirs = ["templates"]`.
- **Rationale.** No existing repo convention places non-.NET code anywhere (verified — no
  top-level `benchmarks/`, `src/` is .NET-only); a `src/`-adjacent Rust crate would tangle two
  toolchains' conventions. A dedicated top-level directory is invisible to the .sln and to every
  CI workflow (verified — no workflow glob matches it), satisfying the plan's "cannot affect
  existing CI or packaging".
- **Alternatives rejected.** `src/Heddle.Performance.Rust/` (implies .NET project conventions
  and solution membership it will never have); one workspace per track (two crates duplicate
  models/gates for no consumer); moving the corpus into `benchmarks/` (Phase 1 D6 explicitly
  reserved that as a later ordinary move — its trigger did not fire).
- **Grounding.** Repo listing (Assumed state); [Phase 1 D6](../phase-1-cross-stack-foundation/README.md#d6--the-corpus-stores-the-normalized-oracle-under-srcheddleperformancegoldencorpus).

### D3 — Escaping mode per template per track
- **Decision.** Four quadrants, pinned per cell in [workload-ports.md](workload-ports.md):
  1. **Controlled raw (12 cells).** Askama: `escape = "none"` in the derive attribute (explicit,
     documented override of the `.html`-extension-inferred escaper). Tera: templates registered
     in a dedicated `tera_controlled_raw` instance created with
     `autoescape_on(std::iter::empty::<&str>())` (the documented disable form).
  2. **Controlled encoded (4 cells).** Askama: default `Html` escaper (no `escape` override).
     Tera: a `tera_controlled_encoded` instance with default autoescape (template names end in
     `.html`).
  3. **Idiomatic raw (12 cells).** Defaults **on** for both engines (that is the idiomatic
     posture): byte-neutral for five raw workloads because their pinned model values contain no
     `& < > " '` (Phase 1 authoring rule 4); `composed-page`'s model values are trusted HTML
     fragments, so its idiomatic templates use the documented `safe` filter per fragment
     expression.
  4. **Idiomatic encoded (4 cells).** Defaults on, no filters — the documented
     untrusted-data posture.
- **Rationale.** The controlled track measures each engine's raw path (Phase 1 shared authoring
  rule 1) and escaping path (rule 2) exactly as the .NET twins do; the idiomatic track measures
  the documented practitioner posture, and turning escaping off there would be neither idiomatic
  nor necessary. Every mode choice is provable: the byte gate fails a wrongly-escaped controlled
  cell, the verifier's forbidden/required entries fail a wrongly-escaped idiomatic cell (plan
  risk table, "proven by the byte gate itself").
- **Alternatives rejected.** Renaming raw templates to a non-HTML extension to dodge Askama's
  inferred escaper (works, but `escape = "none"` is the mechanism the plan names and is visible
  at the declaration site); one Tera instance with per-block `{% autoescape %}` toggles (spreads
  the mode decision into 16 template texts instead of two instance constructions).
- **Grounding.** Askama 0.16.0 `#[template(escape = …)]` docs; Tera 2.0.0 `autoescape_on`
  (Assumed state); [workloads.md — shared authoring rules 1–2](../phase-1-cross-stack-foundation/workloads.md#shared-authoring-rules-for-the-five-new-workloads).

### D4 — Askama's spellings are reconciled by N5 in the gate runner, not by a custom escaper
- **Decision.** The Rust gate runner implements contract N5 (encoded suite only) exactly per the
  contract's table and rules — recognized decimal/hex/named spellings of the five characters,
  leading-zero and hex-case tolerance, single left-to-right scan, no rescan of replacements.
  Applied to *candidate* output only (the oracle is stored canonical). This reconciles Askama
  0.16.0's all-decimal family (`&#34; &#38; &#60; &#62;` → `&quot; &amp; &lt; &gt;`; its `&#39;`
  is already canonical) and is an identity transform on Tera's canonical output. **No custom
  Askama escaper is registered** even though `askama.toml` technically allows one.
- **Rationale.** Q1.1's preference order is "engine configuration where the engine offers it" —
  but Askama offers no configuration of its *stock* escaper's spellings; the only route is
  substituting a user-written `Escaper` implementation, which would replace the very code path
  the encoded suite exists to measure (unlike Phase 1's Handlebars.Net `ITextEncoder`, which was
  forced by divergence *beyond* spelling — an unescaped apostrophe — where N5 could not help).
  Contract N5 rule 3 already directs exactly this: engines without spelling configuration
  "implement N5 in their gate runner". Spelling-only divergence is N5's designed job.
- **Alternatives rejected.** Custom `Escaper` in `askama.toml` (measures our escaper, not
  Askama's; violates the engines-as-published posture for a problem N5 already solves);
  canonicalizing the oracle toward Askama (the canonical spellings are fixed contract-wide).
- **Correction recorded (feeds D14).** Phase 1's contract and README cite "Askama's fixed
  `&#x27;`" (hex) from the legacy `djc/askama` escaper; askama-rs 0.16.0 emits decimal `&#39;`
  (and decimal NCRs for the other four). All 0.16.0 spellings are inside N5's recognized set, so
  **no normative rule changes** — the parenthetical examples are stale, which is erratum E2.
- **Grounding.** v0.16.0 `askama/src/html.rs` (decimal TABLE, `<script>`→`&#60;script&#62;`
  test); [parity-contract-v2.md — N5 rules 1–3](../phase-1-cross-stack-foundation/parity-contract-v2.md#entity-canonicalization-n5);
  [open-questions Q1.1](../../../plan/open-questions.md).

### D5 — Normalization is hand-rolled in `normalize.rs` — no regex dependency
- **Decision.** `src/normalize.rs` implements N1–N5 as plain scans over `&str`/bytes: N1 is
  inherent (engine output is a Rust `String`, valid UTF-8 by construction; the corpus file is
  decoded and byte-compared, and a BOM in candidate output survives to fail the compare); N2 =
  two `replace` passes (`\r\n`→`\n`, `\r`→`\n`); N3 = single left-to-right scan collapsing runs
  of the six whitespace chars between `>` and `<` (equivalent to the contract's repeated-regex
  definition because the replacement `><` contains no whitespace); N3b = single scan removing
  every run of the six whitespace chars anywhere to **nothing** (not to a space; the 2026-07-20
  maintainer step), applied to **both** the candidate's normalized output and the loaded oracle at
  comparison so the gate compares non-whitespace bytes; N4 = trim of exactly the six-char set (**not**
  Rust's Unicode `str::trim`); N5 = the D4 scanner. Unit-tested against contract-derived vectors
  (Testing plan).
- **Rationale.** The contract pins the portable six-char whitespace set precisely so consumers
  don't inherit a regex engine's `\s` semantics; a dependency-free scan implements the closed
  definition literally and keeps the harness's dependency surface auditable.
- **Alternatives rejected.** `regex` crate with `>[\t\n\x0B\x0C\r ]+<` (correct but an extra
  dependency for one replace); `str::trim` for N4 (trims Unicode whitespace — wider than the
  contract's closed set).
- **Grounding.** [parity-contract-v2.md — normalization pipeline](../phase-1-cross-stack-foundation/parity-contract-v2.md#normalization-pipeline).

### D6 — No whitespace-control markers, on either engine, in either track
- **Decision.** Neither the controlled nor the idiomatic templates use Askama's `-`/`~`/`+` or
  Tera's `{%- -%}` family, and no engine-wide whitespace default is configured (Askama stays
  `preserve`). The authoring discipline of [workload-ports.md](workload-ports.md) makes them
  unnecessary: substitutions and element text are always authored tight against their tags;
  block tags sit inline or on lines whose neighbours are tags, so every layout-introduced run is
  inter-tag whitespace that N2–N4 (and the verifier's identical normalization) erase.
- **Rationale.** The plan flags Askama trim semantics as the controlled track's top risk; the
  cheapest mitigation is not to depend on trim behavior at all — the v1 "whitespace-free
  authoring" discipline the .NET twins already use. Both engines' marker syntaxes are verified
  and documented (spike C for Askama, re-verified for Tera 2.0) so the fallback is real, but an
  unused feature cannot mis-trim.
- **Alternatives rejected.** `#[template(whitespace = "suppress")]` globally (changes bytes
  *inside* element text runs too — exactly the region normalization must not touch);
  per-tag trims for cosmetic multi-line authoring (spends risk budget on layout aesthetics).
- **Revisit trigger.** A controlled-gate failure whose first-diff excerpt shows whitespace inside
  element text adjacent to a block tag → add the minimal local trim marker on that tag, document
  it in the template's header comment, re-gate.
- **Grounding.** Askama book whitespace-control section (0.16.0); Tera 2.0 docs (Assumed state);
  [workloads.md — shared authoring rule 3](../phase-1-cross-stack-foundation/workloads.md#shared-authoring-rules-for-the-five-new-workloads).

### D7 — Controlled-track construct mapping: one Jinja-family text where the engines agree
- **Decision.** Controlled ports use the construct classes of the Phase 1 twins, per workload:
  scalar substitution, `{% for %}`, `{% if %}/{% elif %}/{% else %}` on precomputed booleans,
  `{% include %}` for the layout (composed-page) and the per-call partial (fragment-heavy) with
  scope-shared loop variables, and an ordered-name loop + lookup for the area sequence — Tera
  bracket indexing `areas[name]`, Askama a `self.area(name)` method (bracket indexing with a
  variable key is not documented for Askama; method access is). Six of eight workloads share one
  template text verbatim across both engines; the two composition workloads differ only in
  include paths and the lookup spelling. Full texts and per-cell notes:
  [workload-ports.md](workload-ports.md).
- **Rationale.** "Equivalently authored" (Are-We-Fast-Yet) means same construct classes doing the
  same work — sharing literal text wherever the dialects coincide is the strongest possible form
  of it, and the two divergences (lookup spelling, include path) are forced by documented
  language surface, recorded per cell. Composition mapping mirrors the .NET twins' verified
  shapes (include-shares-scope = the DotLiquid/Scriban pattern from Phase 1 probe E).
- **Alternatives rejected.** Precomputing the area fragments into an ordered `Vec` and looping
  over values (drops the name→fragment lookup every .NET twin performs — weaker equivalence);
  Askama macros for the tile (documented call-syntax variance across versions; include is
  verified and matches the .NET scope-sharing twins); inlining the tile body into the loop
  (removes the per-call composition the workload owns).
- **Grounding.** `LiquidTemplates.cs`/`TwinContent.cs` *(read)*; Askama 0.16.0 book (include
  context access, method access via dot); Tera 2.0 docs (bracket notation, include semantics);
  [workloads.md — workload 6 verified mechanisms table](../phase-1-cross-stack-foundation/workloads.md#workload-6--fragment-heavy-raw).

### D8 — Idiomatic authoring standard applied (Q1.7/D16)
- **Decision.** Every idiomatic implementation file (template + its Rust declaration) carries a
  header comment citing the official doc pages its patterns follow (Askama book pages;
  keats.github.io/tera doc sections — the pages named per cell in
  [workload-ports.md](workload-ports.md)). Idiomatic structure: template inheritance for the two
  page-shaped workloads (composed-page, mixed-page), include-per-iteration for fragment-heavy,
  multi-line indented authoring throughout, engine-default escaping with `safe` only for
  composed-page's trusted fragments. Nothing is imported from third-party benchmark repos.
- **Rationale / Alternatives rejected / Grounding.** Q1.7 resolution verbatim, bound by
  [parity-contract-v2.md — idiomatic authoring standard](../phase-1-cross-stack-foundation/parity-contract-v2.md#idiomatic-track-gate);
  third-party import rejected there.

### D9 — Criterion configuration for the Windows/Ryzen protocol machine
- **Decision.** All timed benches run under one shared config constructed in each bench target:
  `Criterion::default().warm_up_time(Duration::from_secs(3)).measurement_time(Duration::from_secs(10)).sample_size(100).confidence_level(0.95).configure_from_args()`
  — defaults except `measurement_time` raised 5 s → 10 s. **The trailing `.configure_from_args()`
  is mandatory and must be last**: with `harness = false` Criterion parses the CLI arguments after
  `--` (e.g. `--noplot`, `--test`, `--save-baseline`, benchmark-name filters) *only* when the
  custom `main` calls it, and it must layer on top of the explicit builder so that pinned settings
  survive when no CLI flag overrides them while a supplied flag still wins (docs.rs
  `Criterion::configure_from_args`). Without it, `cargo bench -- --noplot` and `cargo bench -- --test`
  are silent no-ops — the full 10 s × 33 measurement would run under a `--test` smoke invocation.
  Runs are invoked `cargo bench -- --noplot` (no gnuplot/plotters artifacts; `estimates.json` is
  still written).
  Benchmark naming: group `controlled-<workload-id>` / `idiomatic-<workload-id>`, function
  `askama` / `tera`, so artifacts land at
  `target/criterion/<group>/<fn>/new/estimates.json`. The published statistic per cell is
  `mean.point_estimate` with `mean.confidence_interval.{lower_bound,upper_bound}` (95%) from that
  file — the D12 mapping, restated not varied. Timed closures are `b.iter(|| runner.render())`
  where `render()` returns the output `String` (Criterion routes the return through its sink;
  drop cost is inside the measurement for every engine equally, matching the .NET suites where
  the returned string's lifetime is likewise per-op). No process-priority manipulation and no
  core pinning: the protocol machine runs quiet and dedicated, and dispersion is published
  (Q2.1) so instability is visible rather than hidden.
- **Rationale.** Criterion has no Windows-specific guidance (verified — book/FAQ/tracker, spike
  D); the only documented noise levers are measurement time and a quiet machine. 10 s
  measurement doubles samples-per-estimate at ~13 min total added wall time across 33 timed
  cells — cheap insurance on a shared-scheduler OS. Everything else stays default because
  "standard harness, native posture" is the protocol's credibility argument.
- **Alternatives rejected.** Forcing BenchmarkDotNet-like methodology (rejected in Q2.1);
  `SetPriorityClass`/affinity via a `windows-sys` dependency (no evidence it is needed; adds an
  unauditable variable — see revisit trigger); larger `sample_size` (raises resample cost
  without addressing scheduler noise, which measurement time does).
- **Revisit trigger.** Any cell's published 95% CI half-width exceeding 5% of its mean →
  re-run the full suite with priority elevation added (one recorded environment change for the
  whole run, never per-cell).
- **Grounding.** Criterion 0.8.2 defaults and FAQ (spike D, docs.rs);
  [metrics-protocol.md — D12 mapping](../phase-1-cross-stack-foundation/metrics-protocol.md#wall-time-statistic-mapping-q21).

### D10 — Allocation instrumentation: `allocation-counter` 0.8.1 in a separate non-timed pass
- **Decision.** Per-render allocation figures come from `allocation-counter` 0.8.1 behind a cargo
  feature `alloc-count` that gates both the optional dependency and the `alloc_report` binary
  (`[features] alloc-count = ["dep:allocation-counter"]`; the binary is `required-features =
  ["alloc-count"]`). **No sub-feature forwarding and no user `#[global_allocator]` are involved or
  permitted:** the crate exposes no features of its own, and it declares `#[global_allocator]`
  internally, so *compiling the dependency in* (which the `alloc-count` feature does) is what
  installs the counting global allocator — adding a second `#[global_allocator]` in this crate would
  be a duplicate-registration compile error, and `allocation-counter/count-allocations` is not a
  real feature reference. For each of the 32 cells the binary: renders once (warm-up, uncounted),
  then `allocation_counter::measure(|| runner.render())` and prints `count_total`/`bytes_total` per
  render. The timed Criterion binaries are built **without** the
  feature — the default `System` allocator, no counting overhead in any timed number. Gates run
  in this pass too (D11 — no number without a green gate, allocation numbers included). Report
  placement: per-ecosystem table only, Tera as baseline (Phase 1 D13), verbatim
  non-comparability label adjacent.
- **Rationale.** Criterion has no `[MemoryDiagnoser]` analogue, so the plan delegates the
  mechanism to this spec. `allocation-counter` is purpose-built for count-a-closure, Windows-CI
  tested, and dependency-light; any custom `GlobalAlloc` skews timing, so a separate pass is
  mandatory, mirroring dhat's own feature-gating precedent.
- **Alternatives rejected.** `dhat` 0.3.3 (heap-profiler with documented extra slowdown "even
  more so on Windows"; wrong shape for a per-render count); hand-rolled counting allocator
  (reimplements `allocation-counter`'s nested-measurement/opt-out logic for zero gain);
  counting inside the Criterion process (contaminates the timed path).
- **Grounding.** Spike D §2 (source-read of `allocation-counter`, CI matrix; dhat docs);
  [metrics-protocol.md — metric rule 2 + label text](../phase-1-cross-stack-foundation/metrics-protocol.md#metric-rules);
  [Phase 1 D13](../phase-1-cross-stack-foundation/README.md#d13--presentation-rules-q22--a-q62-with-the-allocation-baseline-pinned).

### D11 — Parity before timing: gates run inside every bench binary and standalone
- **Decision.** `src/gates.rs` exposes `assert_controlled(cell)`/`assert_idiomatic(cell)` plus
  `assert_all()`. Wiring:
  1. Each Criterion bench target's `main` (custom, `harness = false`) first runs the gate for
     **every cell that target times** — `benches/controlled.rs` gates its 16 controlled cells
     (byte gate + N5 + encoded security floor), `benches/idiomatic.rs` its 16 verifier cells —
     and panics with the D-diagnostics message on any failure **before** constructing
     `Criterion`, so a failed gate produces no `target/criterion` output for that run.
  2. `benches/cold.rs` (Tera cold-parse, D12) gates nothing of its own but calls
     `assert_all()` first — cold numbers are also numbers.
  3. `cargo run --release --bin gate` runs `assert_all()` standalone (32 cells), prints one
     `[PASS]`/`[FAIL]` line per cell, exits 0 iff all pass — the reproduce-command's first step
     and the CI-of-the-future hook.
  4. The `alloc_report` binary calls `assert_all()` before measuring (D10).
  The controlled gate per cell: load `GoldenCorpus/<id>.golden.html` bytes → render → normalize
  (N1–N4, +N5 for encoded) → UTF-8 bytes compare; encoded cells additionally assert the security
  floor (raw `<script>alert(` count = 0 in **un-normalized** output; `&lt;script&gt;alert(`
  occurrences after N5 = the expected count, from the verifier's `required` entry for
  fortunes-encoded and ≥ 0 by data for encoded-loop). The idiomatic gate per cell: parse
  `<id>.verify.json` → run the verifier (values / ordered markers / forbidden / required over
  normalized output, forbidden also over raw output).
- **Rationale.** Contract §Controlled-track gate 2 requires the gate "in the same
  process/invocation that will produce the timed numbers"; a standalone verb alone would not
  satisfy it, and gate-in-`main` makes "no benchmark number exists for an ungated cell" a
  structural property rather than a procedural one.
- **Alternatives rejected.** Gating inside each Criterion `bench_function` closure (runs the
  compare on every iteration or interacts with warm-up; setup-time is the contract's intent);
  relying on the standalone `gate` bin only (a bench run could then be started without it).
- **Grounding.** [parity-contract-v2.md — controlled gate §2–5, idiomatic gate](../phase-1-cross-stack-foundation/parity-contract-v2.md#controlled-track-gate).

### D12 — Cold parse cost: one per-ecosystem Tera number; Askama disclosed as build-time
- **Decision.** `benches/cold.rs` times exactly one thing: constructing fresh `Tera` instances
  and registering the same template sets the three runtime instances hold (all 21 Tera template
  files across both tracks — the full parse set), reported as "Tera cold parse (all templates)" in the per-ecosystem section, labeled with the
  protocol's non-comparable cold-cost rule. Askama (like Heddle) has no runtime parse step; the
  report's environment/compilation-model disclosure states that Askama templates compile into
  the benchmark binary at `cargo build` time and that no cross-engine or cross-language
  cold-cost comparison is made (Q1.3).
- **Rationale.** Q1.3 resolution: per-ecosystem only, labeled non-comparable; the plan requires
  compilation-model disclosure so steady-state numbers aren't misread. One aggregate parse
  number is the minimal honest implementation — per-template cold numbers would invite exactly
  the unlike-things comparison Q1.3 forbids.
- **Alternatives rejected.** No cold number at all (the disclosure then has no quantitative
  anchor and the per-ecosystem allowance goes unused); measuring Askama's `cargo build` time as
  "cold cost" (compares a compiler run to a parser call — the Q1.3 anti-pattern verbatim).
- **Grounding.** [open-questions Q1.3](../../../plan/open-questions.md);
  [metrics-protocol.md — metric rule 3](../phase-1-cross-stack-foundation/metrics-protocol.md#metric-rules).

### D13 — Report: one `docs/benchmarks/<date>/` directory in the protocol shape
- **Decision.** The measurement run publishes exactly one new date-stamped directory:
  - `index.md` in the protocol's pinned section order (H1, intro + reproduce commands,
    `## Environment`, `## The workloads`, `## Results — what this run actually shows`,
    `## Files`). Environment block records: `rustc 1.97.1` + `cargo` version, crate pins
    (askama 0.16.0, tera 2.0.0, criterion 0.8.2, allocation-counter 0.8.1), Windows build, CPU,
    commit — same fenced shape as the 2026-07-18 report.
  - Two wall-time tables, one per track, captioned `controlled` / `idiomatic`. Rows per
    workload: `Heddle (reference — .NET 10, same machine, from <phase-1-run-date> run)` (wall
    time only, dashes in every other column), `Askama`, `Tera`. Columns: harness-native value
    (Criterion mean + 95% CI), ns/render, ratio vs the Heddle row. **The Heddle reference row
    appears in both track tables**: it is the mandated labeled excerpt (presentation rule 1) and
    the mandated ratio anchor (rule 2), and its label — not its table placement — carries the
    provenance; this spec reads rule 5's "numbers from different tracks are never mixed" as
    governing *engine result rows*, and records that reading as erratum candidate E1 for the
    protocol to confirm (D14). Until E1 is ruled on, both tables carry the row with a caption
    note naming the source run — the most reversible presentation (deleting a row is a
    one-line edit; re-deriving ratios is mechanical via `summarize`).
  - Per-ecosystem sections: allocation table (both engines, Tera baseline, verbatim label),
    Tera cold-parse number (D12), compilation-model disclosure, encoded-suite results adjacent
    to the verbatim confinement caveat.
  - `## Files` artifacts: `criterion/<group>/<fn>/estimates.json` copied per timed cell,
    `criterion-console.txt` (full `cargo bench` stdout), `alloc-report.txt`, `gate-report.txt`
    (the `--bin gate` output of the same run).
  - `src/bin/summarize.rs` generates the two wall-time tables: it reads every
    `target/criterion/*/**/new/estimates.json`, takes `mean.point_estimate` (ns) and the CI
    bounds, joins the Heddle reference values from a checked-in
    `benchmarks/rust/heddle-reference.toml` (workload id → ns, source-run date; transcribed once
    from the published Phase 1 run and reviewed in diff), and prints GitHub-flavored Markdown
    rows pasted into `index.md`.
  - Honest-reporting rules 1–6 apply verbatim; every workload where Heddle's reference number
    trails Askama or Tera is named in the results prose with its numbers, as prominently as any
    Heddle win.
- **Rationale.** Q2.2 = A and Q6.2 fix the row and the anchor; the protocol fixes the directory
  shape; `summarize` exists because hand-transcribing 66+ numbers across 32 cells is the one
  step of the pipeline with no gate on it — generation makes the tables mechanically faithful to
  the committed JSON artifacts.
- **Alternatives rejected.** Heddle row in the controlled table only (leaves the idiomatic
  table's mandated ratio column with no anchor — contradicts rule 2's "uniform across phases
  2–6"); re-measuring Heddle on the run date (rule 1 forbids re-measurement — excerpt only);
  committing Criterion's full `sample.json`/HTML tree (megabytes of derived artifacts; the
  estimates + console output are the numbers the report states).
- **Grounding.** [metrics-protocol.md — presentation rules, publication format, honest-reporting rules](../phase-1-cross-stack-foundation/metrics-protocol.md#presentation-rules-q22--a-q62);
  [docs/benchmarks/2026-07-18/index.md](../../../benchmarks/2026-07-18/index.md) *(read)*.

### D14 — Errata are recorded upstream via the amendments ledger; nothing is patched locally
- **Decision.** Two findings from this spec's verification are filed as entries in the cross-spec
  amendments ledger during implementation (WI11), against the Phase 1 spec. The ledger *mechanism*
  is defined in
  [spec-conventions — amendments](../../common/spec-conventions.md#amendments-during-implementation)
  and [cross-cutting-decisions — cross-spec amendments ledger](../../common/cross-cutting-decisions.md#cross-spec-amendments-ledger),
  but the accumulated **entries live in [`records.md`](../../records.md#cross-spec-amendments-ledger)**
  (the historical records; that section's convention is explicit that "a spec adding an entry
  appends it there in the same change"). The two entries, in the ledger's `Amendment | Made by |
  Amends` format:

  | Amendment | Made by | Amends |
  | --- | --- | --- |
  | **E1 (clarification requested; no Phase 1 text change).** Confirm that the mandated Heddle reference row may appear in the wall-time tables of *either* track (presentation rules 1–2 force it, since rule 2's ratio column must anchor to it) and that rule 5's "numbers from different tracks are never mixed" governs *engine result rows*, not the track-agnostic reference excerpt. | Phase 2 (rust) spec | metrics-protocol presentation rules 1–5 (clarification only — the *Amends* target is deliberately a request for a courtesy wording note; no normative rule is altered, so this cell names a clause to clarify, not a decision to overturn) |
  | **E2 (stale example; non-normative).** Parity-contract-v2 N5 rule 3's parenthetical "Askama's fixed `&#x27;`" and the Phase 1 README's external-reference line describe the legacy `djc/askama` escaper; askama-rs 0.16.0 emits the all-decimal family (`&#34; &#38; &#39; &#60; &#62;`). Every 0.16.0 spelling is already in N5's recognized set, so **no normative change** — the examples should name the decimal family (or both, dated). | Phase 2 (rust) spec | parity-contract-v2 N5 rule 3 example + Phase 1 README external-reference line |

  This spec proceeds on E1's reading (D13) and adjusts the published tables in a follow-up dated
  run only if the ruling goes the other way (published directories are immutable). Both the
  verification and adversary reviews of this spec independently confirmed the both-tables reading
  as correct (indeed the only internally consistent one under rule 2) and E2 as an evidence-backed
  non-normative erratum, so neither entry blocks DoR and no Phase 1 wording change is required to
  proceed. Any further ambiguity discovered during implementation follows the same path: ledger
  entry with evidence, no local workaround, per the plan's erratum rule.
- **Rationale.** The plan makes this phase the contract's first external consumer and its
  de-facto acceptance test; the ledger is the repo's sanctioned amendment mechanism
  (spec-conventions §Amendments during implementation), and Phase 1's text is explicitly not
  edited by this phase.
- **Alternatives rejected.** Editing Phase 1's documents directly (barred — consumed read-only);
  ignoring E2 because it is non-normative (phases 3–6 would inherit and possibly propagate the
  stale citation).
- **Grounding.** D4 verification; D13; [spec-conventions — amendments](../../common/spec-conventions.md#amendments-during-implementation).

## Implementation plan

Ordered; Phase 1's corpus, verifier JSON, and published protocol run must exist before WI4+ can
complete (WI1–WI3 are corpus-independent scaffolding and can start any time). WI4–WI6 are
**co-developed**: WI4/WI5 author the templates whose Done-when is checked by the `gate` binary that
WI6 builds, so the `cargo run --release --bin gate` reference in WI4/WI5 is satisfied as WI6 lands —
the gate is the executable spec that starts 32-red and greens cell-by-cell (Testing plan).

### WI1 — Workspace scaffold
- **Files.** New: `benchmarks/rust/Cargo.toml`, `Cargo.lock`, `rust-toolchain.toml`,
  `askama.toml`, `src/lib.rs` (module stubs), directory tree per D2; changed: `.gitattributes`
  (the two D2 lines, appended).
- **Change.** Single crate `heddle-bench-rust` (not a multi-crate workspace); dependency pins
  per D1; `[features] alloc-count = ["dep:allocation-counter"]`; three `[[bench]]` entries
  (`controlled`, `idiomatic`, `cold`) with `harness = false`; `[[bin]]` entries `gate`,
  `summarize`, and `alloc_report` (`required-features = ["alloc-count"]`).
- **Done when.** `cargo build --release` and `cargo build --release --features alloc-count`
  succeed on the pinned toolchain; `dotnet build -c Release` of the solution is unaffected.

### WI2 — Models and composed-page fragment data
- **Files.** New: `src/models.rs`, `data/composed-page/*` (per
  [workload-ports.md — model construction](workload-ports.md#model-construction-srcmodelsrs)).
- **Change.** Eight model builders behind `OnceLock`, `serde::Serialize` derives, fragment
  files copied byte-exactly from `TwinContent.cs` / `AreaComponent.cs`.
- **Done when.** Model unit tests pass (row counts, pinned strings — Testing plan); fragment
  files are non-empty except `area-6.html`.

### WI3 — Normalization, corpus access, verifier
- **Files.** New: `src/normalize.rs` (N1–N5 per D5), `src/corpus.rs` (path resolution from
  `CARGO_MANIFEST_DIR`, golden/verify-JSON loading), `src/verifier.rs` (idiomatic verifier per
  contract semantics, serde_json model of `<id>.verify.json`).
- **Done when.** Normalization and verifier unit tests pass, including the N5 edge vectors and
  the corruption-rejection calibration (Testing plan).

### WI4 — Controlled-track templates and runners
- **Files.** New: `templates/controlled/askama/*` (10 files: 8 workloads + composed-page layout
  + fragment tile), `templates/controlled/tera/*` (same 10), `src/engines/askama_controlled.rs`,
  `src/engines/tera_controlled.rs`.
- **Change.** Template texts normative per [workload-ports.md](workload-ports.md); Askama derive
  structs with the D3 escape modes; two Tera instances (raw autoescape-off, encoded default)
  built once via `OnceLock`, templates registered with `add_template_file`. Every runner exposes
  `pub fn render(&self) -> String` with parse/compile outside it.
- **Done when.** All 16 controlled cells pass `cargo run --release --bin gate` (WI6) — byte
  gate + N5 + security floor.

### WI5 — Idiomatic-track templates and runners
- **Files.** New: `templates/idiomatic/askama/*`, `templates/idiomatic/tera/*` (11 files each:
  8 workloads + the two inheritance bases + the fragment tile), `src/engines/askama_idiomatic.rs`,
  `src/engines/tera_idiomatic.rs`.
- **Change.** Docs-taught authoring per D8 with per-file doc-citation header comments; one
  default-escaping Tera instance; `safe` filters only in composed-page.
- **Done when.** All 16 idiomatic cells pass the verifier via `--bin gate`.

### WI6 — Gate wiring and gate binary
- **Files.** New: `src/gates.rs`, `src/bin/gate.rs`.
- **Change.** Per D11: cell registry (workload id × engine × track → render fn + gate kind),
  `assert_controlled`/`assert_idiomatic`/`assert_all`, failure messages per
  [Diagnostics](#diagnostics--error-surface); `gate` bin prints one line per cell, exit 0 iff
  32/32 pass.
- **Done when.** `cargo run --release --bin gate` prints 32 `[PASS]` lines, exit 0; corrupting
  one corpus byte locally flips the matching cell to `[FAIL]` with the pinned message shape
  (manual spot check, then revert).

### WI7 — Criterion benches
- **Files.** New: `benches/controlled.rs`, `benches/idiomatic.rs`, `benches/cold.rs`.
- **Change.** Custom `main` per D11 (gates first), shared D9 config **including the trailing
  `.configure_from_args()`** (so `--test`/`--noplot`/filters take effect — D9),
  group/function naming per D9, `b.iter(|| runner.render())`; `cold.rs` per D12.
- **Done when.** `cargo bench -- --test` actually runs in single-iteration smoke mode (each cell
  performs one iteration, not the 10 s measurement — proving `.configure_from_args()` is wired)
  and passes end-to-end; a deliberate gate failure (temporarily corrupted template) aborts before
  any Criterion output for that target.

### WI8 — Allocation pass
- **Files.** New: `src/bin/alloc_report.rs`.
- **Change.** Per D10: `assert_all()`, then per cell one warm-up render + one
  `allocation_counter::measure`, output one line per cell
  (`<track> <engine>/<workload>  allocs=<count_total>  bytes=<bytes_total>`). The binary first runs
  a **counting canary** — `allocation_counter::measure(|| { String::from("x").len() })` (or any
  render) must report `count_total > 0` — and aborts with a diagnostic if it reports zero, so a
  misconfigured allocator (which would silently emit an all-zero table) fails loudly instead of
  publishing garbage.
- **Done when.** `cargo run --release --features alloc-count --bin alloc_report` prints 32 data
  lines after 32 gate passes, **and every printed `allocs` value is > 0** (the counting canary
  passed and no cell reported the all-zero signature of a non-installed counter).

### WI9 — Summarize tool
- **Files.** New: `src/bin/summarize.rs`, `benchmarks/rust/heddle-reference.toml`.
- **Change.** Per D13: read estimates JSON, join reference TOML, emit the two Markdown tables
  (ns/render, 95% CI, ratio, track caption).
- **Done when.** Running it after a `cargo bench` produces tables whose every number matches the
  committed `estimates.json` values by inspection of at least two spot-checked cells.

### WI10 — Measurement run and report
- **Files.** New: `docs/benchmarks/<run-date>/index.md`, `criterion/**/estimates.json` copies,
  `criterion-console.txt`, `alloc-report.txt`, `gate-report.txt`.
- **Change.** On the protocol machine, in order: `cargo run --release --bin gate` (capture
  output) → `cargo bench -- --noplot` (capture console; artifacts under `target/criterion`) →
  `cargo run --release --features alloc-count --bin alloc_report` (capture) →
  `cargo run --release --bin summarize` → author `index.md` per D13 with the protocol's section
  order, label texts, and honest-reporting narrative.
- **Done when.** Every Success-criteria checkbox of the plan that concerns the report is
  satisfiable by pointing at the directory; a checklist review against
  [metrics-protocol.md — honest-reporting rules](../phase-1-cross-stack-foundation/metrics-protocol.md#honest-reporting-rules)
  passes; the run's crate/toolchain versions in the environment block match D1's pins.

### WI11 — Errata ledger entries and index row
- **Files.** Changed: `docs/spec/records.md` (append ledger entries E1, E2 to its
  `#cross-spec-amendments-ledger` table per D14 — this is where the accumulated entries live, not
  `common/cross-cutting-decisions.md`, which only defines the mechanism), `docs/spec/README.md`
  (master-index row for this spec, per the index-maintenance convention).
- **Done when.** Both entries appear as rows in `records.md#cross-spec-amendments-ledger` in the
  `Amendment | Made by | Amends` format, carry their evidence links, and the E1 row's `Amends`
  cell is marked clarification-only (empty-of-overturn by design); index row resolves.

## Public API / contract

No Rust library API is published: the crate is an unpublished benchmark harness
(`publish = false`); every `pub` item is internal to the harness binaries. The consumed and
produced contracts:

| Surface | Shape |
|---|---|
| `cargo run --release --bin gate` | Runs all 32 gates. Stdout: one `[PASS]`/`[FAIL]` line per cell (format in Diagnostics); exit 0 iff all pass. Filters: optional args `--track controlled\|idiomatic`, `--engine askama\|tera`, `--workload <id>` (AND-combined) |
| `cargo bench -- --noplot` | Gates (fatal on failure), then times 33 cells (32 render + 1 Tera cold-parse) under the D9 config; artifacts at `target/criterion/<group>/<fn>/new/estimates.json` |
| `cargo run --release --features alloc-count --bin alloc_report` | Gates, then prints 32 allocation lines (D10 format) |
| `cargo run --release --bin summarize` | Prints the two report tables as GitHub-flavored Markdown to stdout; reads `target/criterion` + `heddle-reference.toml`; exits non-zero if any expected cell's estimates file is missing |
| Corpus (consumed) | `src/Heddle.Performance/GoldenCorpus/<id>.golden.html` + `<id>.verify.json`, read-only, per Phase 1 [golden-corpus.md](../phase-1-cross-stack-foundation/golden-corpus.md) |
| Report (produced) | One immutable `docs/benchmarks/<date>/` directory per D13 |

Thread-safety: all model and engine singletons are `OnceLock`-initialized and immutable
thereafter; Criterion and the binaries drive renders single-threaded.

## Diagnostics / error surface

**HED\* compiler diagnostics: n/a** — this phase changes no compiler, grammar, or engine
surface; no diagnostic IDs are claimed and the registry is untouched.

Harness error surface (all host-side; every failure is fatal to its command — exit ≠ 0, no
numbers emitted):

| Surface | Trigger | Message shape |
|---|---|---|
| Controlled byte gate | normalized candidate bytes ≠ corpus bytes | `[FAIL] controlled <engine>/<workload>: byte mismatch (expected <n> bytes, actual <m>); first diff at <i>` + two `expected:`/`actual:` lines with a 120-char window (±40 chars context), `\n`-escaped — the contract §3 fields in the `ParityCheck.Describe` shape |
| Encoded security floor | raw `<script>alert(` found in un-normalized output, or post-N5 `&lt;script&gt;alert(` count ≠ expected | `[FAIL] security <engine>/<workload>: raw payload found at index <i>` / `…: escaped payload count expected <n>, found <m>` |
| Idiomatic verifier | any check misses | `[FAIL] idiomatic <engine>/<workload> verifier <value\|marker\|forbidden\|required>: expected <n> of "<needle>", found <m>` (markers report `marker "<needle>" not found after index <i>`) |
| Corpus access | golden/verify file missing or unreadable | `[FAIL] corpus <workload>: cannot read <path>: <io error>` (points at Phase 1's export tool as the fix) |
| Bench-time gating | any of the above inside a bench/alloc binary | same message via `panic!`, before any Criterion construction (D11) |
| `summarize` | missing estimates file for an expected cell | `summarize: missing target/criterion/<group>/<fn>/new/estimates.json — run cargo bench first` |
| Invalid UTF-8 in corpus file | N1 violation on the oracle side | `[FAIL] corpus <workload>: golden is not valid UTF-8` (cannot happen for candidate output — Rust `String`) |

## Testing plan

**TDD verdict.** As in Phase 1, the gates are the executable spec and run test-first by
construction: WI3's normalization/verifier tests are written against contract vectors before any
template exists; `--bin gate` starts 32-red after WI3 and turns green cell-by-cell through
WI4/WI5. Conventional `cargo test` covers the pure logic; the gate commands cover the cells.

**Named checks.**
- *Normalization unit tests* (`normalize.rs`): N2 CRLF/CR vectors; N3 collapses each of the six
  whitespace chars and multi-char runs between `><` to nothing; N3b removes every remaining
  whitespace run anywhere (e.g. a run inside element text, or at a `>\n/` non-tag boundary) to
  nothing — including a vector where one side has a run and the other has none (`>␠/` vs `>/`),
  which must compare equal; N4 trims only the six-char set (a leading U+00A0 survives); N5 vectors from the
  contract table — `&#x27;`→`&#39;`, `&#034;`→`&quot;`, `&#X3C;`→`&lt;`, `&apos;`→`&#39;`,
  no-rescan (`&amp;#39;` stays `&amp;#39;`), non-five-char NCR untouched (`&#43;` stays);
  full Askama-family vector `&#60;script&#62;alert(&#34;x&#34;);` → `&lt;script&gt;alert(&quot;x&quot;);`.
- *Verifier calibration* (`verifier.rs` tests + the corpus): the verifier accepts all eight
  committed goldens; per workload it rejects the Phase 1-defined corruptions (removed
  row/segment, swapped sections; + unescaped payload for the two encoded workloads) synthesized
  by the same rules `verify-corpus` uses — re-proving Phase 1's calibration through the **Rust**
  implementation (the definitions are proven upstream; this proves the port).
- *Model pins* (`models.rs` tests): row counts (5,000 / 200 / 48 / 36 / 12 / 5,000); mixed-page
  `on_sale` count = 12; conditional tier counts (50/50/50/50, `has_note` = 100,
  `is_active` = 160); fortunes row 11 = the exact payload string, row 12 = the Japanese string;
  encoded-loop row 0 tag = `tag-0&'0'`.
- *Gate commands*: `cargo run --release --bin gate` (32 `[PASS]`, exit 0) — the phase's
  acceptance check; `cargo bench -- --test` as the bench smoke.
- *Negative/security*: the security floor asserts on every encoded cell's output each gate run;
  verifier corruption tests are the spec-mandated negative tests.

**Measurement-run procedure (protocol run).** On the protocol machine, quiet session (no
foreground apps, no scheduled tasks due), AC power/high-performance plan as recorded in the
environment block: run the WI10 command sequence in order; if any gate fails, stop — fix,
re-gate, restart the sequence from the top (never splice artifacts from partial runs); the D9
revisit trigger (CI half-width > 5% of mean on any cell) forces one full re-run with the single
recorded environment change.

**Regression gate (before merge).**
1. `cargo fmt --check` and `cargo clippy --release -- -D warnings` in `benchmarks/rust/`.
2. `cargo test` — all unit tests above.
3. `cargo run --release --bin gate` — 32/32 `[PASS]` (requires the Phase 1 corpus committed).
4. `cargo bench -- --test` — smoke, gates included.
5. `dotnet build -c Release` + `dotnet test src/Heddle.Tests` — unchanged results, proving the
   phase touched no .NET behavior (only additive `.gitattributes` lines and new directories).
6. No diff under `src/` except none at all — this phase adds no .NET change; `git status` of
   `src/` is clean.

## Back-compat and migration

- **Nothing existing changes.** The phase adds `benchmarks/rust/**`, two `.gitattributes` lines,
  one report directory, two ledger entries, and one spec-index row. `Heddle.sln`, all
  `src/` projects, all published reports, and the corpus are byte-untouched — proven by
  regression-gate steps 5–6 and by the corpus files' unchanged hashes (`manifest.json` is not
  regenerated by this phase).
- **CI:** verified that no existing workflow glob reaches `benchmarks/` (Assumed state); no
  workflow is added or modified by this phase (a Rust CI job is out of scope — the gate bin is
  runnable locally and in any future workflow without change).
- **Contract v2 and corpus:** consumed read-only; the two D14 errata are additive ledger entries
  under the sanctioned amendment mechanism, not edits to Phase 1 text.
- **No breaking window engaged:** all changes are additive; the new directory is new surface.

## Performance considerations

- **What is timed:** `render()` only — parse/compile (Tera registration, Askama at build time),
  model construction, corpus loading, and gates all sit outside every timed closure. The
  returned `String`'s allocation and drop are inside the measurement for both engines equally
  (and correspond to what the .NET suites measure per op).
- **The harness cannot perturb the .NET numbers:** separate process, separate toolchain,
  separate directory; the protocol machine's published .NET numbers are excerpted, never re-run.
- **Encoded-loop scale:** ~1 MB output per render; at 10 s measurement time Criterion collects
  fewer, longer iterations (its automatic iteration scaling handles ms-scale functions; the
  same workload already runs under BenchmarkDotNet intra-.NET).
- **Allocation pass isolation:** the counting allocator exists only in the `alloc-count` build;
  timed binaries use `std::alloc::System` untouched (D10).
- **Gate cost:** 32 renders + compares + one corpus read per gated command, setup-time only —
  the same budget shape Phase 1 accepted for `AssertFresh`.

## Standards compliance

- **DRY:** one `models.rs` feeds all four engine modules; six of eight controlled template texts
  exist as one normative text instantiated per engine (workload-ports.md) — the physical
  duplication is forced by Askama's compile-time consumption, and the byte gate keeps the copies
  honest, the same argument Phase 1 used for `TwinContent`. The N5 table exists once in
  `normalize.rs` and once in the contract; the unit vectors tie them.
- **YAGNI:** no cross-language verifier runner, no CI workflow, no report auto-publisher (only
  `summarize`, which exists because 60+ hand-copied numbers have no gate); no `windows-sys`
  priority machinery without evidence (D9 trigger); single crate, not a workspace.
- **Open/closed:** the cell registry in `gates.rs` is the one growth seam — a future engine or
  workload adds a registry row and templates, touching no gate/bench logic.
- **Tests exempt from DRY:** per-workload model-pin tests deliberately restate expected counts
  as literals so a failing pin is diagnosable at a glance.

## Deferred items

| Item | Trigger |
|---|---|
| Tera `fast`/`fast_escape` feature measurement (opt-in performance mode; escaper spellings unverified against N5) | A user request to publish Tera's opt-in fast mode, which then requires verifying `pulldown_cmark_escape`'s exact entity spellings against the N5 table before any encoded cell uses it |
| Process-priority elevation / core affinity for Criterion runs | D9 revisit trigger: any published cell's 95% CI half-width > 5% of its mean |
| Whitespace-control markers in any template | D6 revisit trigger: a controlled-gate first-diff showing whitespace inside element text adjacent to a block tag |
| Tera components (`{% component %}`/`{% endcomponent %}`) for fragment-heavy | workload-ports.md fallback trigger: Tera 2.0's `{% include %}` not exposing the loop variable (byte gate decides). The component construct is verified to exist in 2.0 (Assumed state) and is the documented custom-context/partial-with-arguments mechanism — so the fallback rests on a real construct |
| Rust CI job running `--bin gate` | Any recurring-contributor workflow touching `benchmarks/rust/` (today the gate is a local/measurement-time command) |
| quicktemplate anything | **Not this phase** — Phase 6 owns quicktemplate's conditional inclusion and its whitespace-control confirmation (Phase 1 deferred-items table) |
| Second Rust engine (minijinja et al.) | User sign-off reopening the fixed two-engine cast (standing ruling) |

## External references

- Tera 2.0.0 — crate and re-verified API/source:
  <https://crates.io/crates/tera>, <https://docs.rs/tera/2.0.0/tera/struct.Tera.html>
  (`autoescape_on`, `set_escape_fn`), <https://docs.rs/tera/2.0.0/tera/fn.escape_html.html> +
  source view `src/tera/utils.rs.html#108-130` (five-character table, canonical spellings, `/`
  not escaped), <https://docs.rs/crate/tera/2.0.0/features> (zero default features),
  <https://keats.github.io/tera/> (whitespace control, include/context semantics, autoescaping,
  inheritance), <https://github.com/Keats/tera> (v2 rewrite + migration note)
- Askama 0.16.0 — crate, escaper source, book:
  <https://crates.io/crates/askama>, `askama-rs/askama` tag `v0.16.0` files
  `askama/src/filters/escape.rs` and `askama/src/html.rs` (all-decimal escape table, verified),
  `book/src/template_syntax.md` (whitespace control `-`/`~`/`+`, inheritance, include context
  access, `elif`/`else if`), `book/src/creating_templates.md` (`#[template]` attributes,
  `escape = "none"`): <https://github.com/askama-rs/askama>
- Criterion.rs 0.8.2 — defaults, analysis, FAQ:
  <https://docs.rs/criterion/latest/criterion/struct.Criterion.html>,
  <https://bheisler.github.io/criterion.rs/book/analysis.html>,
  <https://github.com/bheisler/criterion.rs/blob/master/book/src/faq.md>
- allocation-counter 0.8.1: <https://crates.io/crates/allocation-counter>,
  <https://github.com/fornwall/allocation-counter> (source + Windows CI matrix, spike D)
- dhat-rs (rejected alternative, Windows slowdown note): <https://docs.rs/dhat/latest/dhat/>
- Rust 1.97.1 announcement (toolchain pin basis — the pinned patch, an LLVM-miscompilation fix
  over 1.97.0): <https://blog.rust-lang.org/2026/07/16/Rust-1.97.1/>,
  <https://blog.rust-lang.org/2026/07/09/Rust-1.97.0/>
- Windows high-resolution timing (QPC) background for the D9 posture:
  <https://learn.microsoft.com/en-us/windows/win32/sysinfo/acquiring-high-resolution-time-stamps>
- Are-We-Fast-Yet controlled methodology: <https://github.com/smarr/are-we-fast-yet>
- Phase 1 spec artifacts consumed (normative):
  [parity-contract-v2.md](../phase-1-cross-stack-foundation/parity-contract-v2.md),
  [golden-corpus.md](../phase-1-cross-stack-foundation/golden-corpus.md),
  [metrics-protocol.md](../phase-1-cross-stack-foundation/metrics-protocol.md),
  [workloads.md](../phase-1-cross-stack-foundation/workloads.md)
- Repo conventions bound by this spec:
  [spec-conventions](../../common/spec-conventions.md),
  [testing-standards](../../common/testing-standards.md),
  [coding-standards](../../common/coding-standards.md),
  [cross-cutting-decisions](../../common/cross-cutting-decisions.md)
