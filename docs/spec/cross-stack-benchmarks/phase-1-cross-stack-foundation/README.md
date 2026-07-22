# Phase 1 — cross-stack-foundation (spec)

## Header

- **Status:** Specified — ready for implementation
- **Source plan:** [phase-1-cross-stack-foundation.md](../../../plan/phase-1-cross-stack-foundation.md)
  (standing rulings in the [plan index](../../../plan/README.md); resolved Q&A register in
  [open-questions.md](../../../plan/open-questions.md) — every resolution there is binding on
  this spec)
- **Assumes merged:** nothing (Phase 1 depends on nothing; it builds on the shipped intra-.NET
  suite as-is)

| Document | Purpose |
|---|---|
| [README.md](README.md) (this file) | Entry document: assumed state, design decisions, implementation plan, gates |
| [workloads.md](workloads.md) | The eight workloads: exact template shapes per engine, exact model data, expected output characteristics, owned dimensions |
| [parity-contract-v2.md](parity-contract-v2.md) | Parity contract v2 full text: normalization pipeline, entity canonicalization, untrusted-data alphabet, controlled/idiomatic gates, exclusion policy |
| [golden-corpus.md](golden-corpus.md) | Golden corpus: on-disk format, manifest, export tool, verification commands, verifier definitions, regeneration policy |
| [metrics-protocol.md](metrics-protocol.md) | Metrics & publication protocol full text: statistic mapping, presentation rules, machine record, report format, honest-reporting rules |

## Scope and goal

Turn the intra-.NET benchmark credibility work into the foundation phases 2–6 build on: (1) five
new workloads — three raw (`mixed-page`, `conditional-heavy`, `fragment-heavy`) and a
two-workload encoding-ON suite (`fortunes-encoded`, `encoded-loop`) — implemented and
parity-proven inside the existing intra-.NET suite (Heddle + Fluid, Scriban, DotLiquid,
Handlebars.Net) with the three existing workloads retained byte-unchanged as anchors; (2) the
golden oracle corpus exported with recorded byte length, SHA-256, and generating commit;
(3) parity contract v2; (4) the metrics & publication protocol, exercised once by publishing an
eight-workload intra-.NET run under `docs/benchmarks/<date>/`.

Out of scope: any cross-language port (phases 2–6), any Heddle engine change, any new .NET
competitor engine, any retroactive edit to published reports, and intra-.NET idiomatic-track
implementations (see [Deferred items](#deferred-items)).

## Assumed state

Re-verified against current source and by two executed probes; not trusted from the plan.
Spike references: spike A (repo state), spike B (executed .NET escaper probe), spike C
(cross-language escaper research) — evidence gathered for this spec; every claim below marked
*(read)* or *(executed)* was additionally re-verified first-hand while authoring.

| Seam | Verified state |
|---|---|
| Parity mechanism | `Runners/ParityCheck.cs` *(read)* — `Twins()`/`TwinsSubstitution()`/`TwinsLoop()` yield `(Name, Func<string> Render)` per workload (Scriban under `#if !NET6_0`); `Assert()`/`AssertSubstitution()`/`AssertLoop()` normalize via `TwinContent.Normalize` and throw `InvalidOperationException` with a `Describe()` first-diff excerpt; `Report*()` return `(bool AllMatch, string Text)` with `[PASS]`/`[FAIL]` lines. Private helpers `AssertAgainst`/`ReportAgainst` already generalize the pattern — the five new workloads reuse them (D9 extends both helpers' *comparison* with the N3b whitespace strip on both sides — a verdict-preserving loosening that leaves every existing anchor's outcome unchanged) |
| Normalization v1 | `Runners/TwinContent.cs`, member `Normalize` *(read)* — exactly three steps: `\r\n`→`\n` then `\r`→`\n`; compiled regex `>\s+<` → `><`; `Trim()`. Nothing else |
| CLI verbs | `Program.cs` *(read)* — `parity` verb runs the three `Report*` calls, exits 0 iff all pass; other args route to `BenchmarkSwitcher`; no args runs `TextRenderBenchmarks`. No `export-corpus`/`verify-corpus` verbs exist |
| Golden/corpus infrastructure | **None exists** in `src/Heddle.Performance` (spike A §7 — searched; the parity check is runtime-comparative, no on-disk golden). The export tool is net-new work. `Heddle.Tests` has an unrelated golden-file pattern (`ContextEncodingGoldenTests.cs`) usable as a precedent only |
| Heddle oracle runner pattern | `Runners/SubstitutionHeddleTest.cs` *(read)* — `TemplateOptions("trivial-substitution") { FileNamePostfix=".heddle", RootPath="TestTemplates", OutputProfile=OutputProfile.Text, ExpressionMode=ExpressionMode.Native, ProvideLanguageFeatures=false }`, typed `CompileContext`. New Heddle runners clone this shape |
| `OutputProfile` | `src/Heddle/Data/OutputProfile.cs` *(read)* — `Text = 0` (raw), `Html = 1` (unnamed `@(…)` HTML-encodes, `@raw` opts out). Encoded oracles set `Html` explicitly |
| Heddle escaper spellings | Text context: `HtmlEncodedRenderer` → `WebUtility.HtmlEncode` (no custom `TemplateOptions.Encoder` configured in the perf suite); attribute context: `ContextEncoders.EscapeAttribute` escapes exactly `& < > " '`. Both emit `&amp; &lt; &gt; &quot; &#39;`; they diverge outside that set (WebUtility escapes U+00A0–U+00FF and surrogate pairs; `EscapeAttribute` does not) — spike B *(executed probe)*, pinned by `src/Heddle.Tests/OutputProfileEncodingTests.cs` |
| Twin escape paths | Fluid `\| escape`, Scriban `html.escape`, DotLiquid `\| escape`/`\| h` all emit the identical five-entity set with BMP ≥ U+0100 passthrough; Handlebars.Net default `{{ }}` leaves `'` unescaped and decimal-escapes non-ASCII — spike B *(executed probe at pinned versions)* |
| Handlebars.Net custom encoder | *(executed, probe D, this spec)* — `HandlebarsConfiguration.TextEncoder` exists in 2.1.6, type `HandlebarsDotNet.ITextEncoder` (default `HtmlEncoderLegacy`); interface members `Encode(StringBuilder, TextWriter)`, `Encode(string, TextWriter)`, `Encode<T>(T, TextWriter) where T : IEnumerator<char>`. A custom five-entity implementation renders `&<>"' こんにちは <script>alert('xss')</script>` **byte-identically** to `WebUtility.HtmlEncode` in both text and attribute positions |
| Twin include/branch constructs | *(executed, probe E, this spec, at pinned versions)* — Fluid `{% include 'tile' with item %}` binds the row to a variable named `tile` inside the partial ✔; DotLiquid `{% include 'tile' %}` shares the enclosing scope (`item` visible) ✔, while `{% include … with <Hash> %}` mis-renders (enumerates the Hash — must not be used); Scriban `{{ include 'tile' }}` shares scope ✔ while positional-arg `$0` does **not** bind ✔(negative); chained `elsif`/`else if`/`{{else if}}` work in Fluid, DotLiquid, Scriban, and Handlebars.Net ✔ |
| Existing templates | `TestTemplates/trivial-substitution.heddle` *(read)* is the single `<article>…</article>` line only — **spike A correction**: spike A's quotation showed a leading "`@list — no; actual content:`" line that is not in the file. `large-loop.heddle` and `home.heddle` *(read)* match spike A |
| Benchmark classes / GlobalSetup gating | Each render benchmark's `[GlobalSetup]` calls its `ParityCheck.Assert*` before timing (`TextRenderBenchmarks.cs` `Setup`, `SubstitutionRenderBenchmarks.cs`, `LoopRenderBenchmarks.cs`) — spike A §1, consistent with the `ParityCheck` doc comment *(read)* |
| Packages / TFMs | `Heddle.Performance.csproj`: `net6.0;net8.0;net10.0`; BenchmarkDotNet 0.15.0 (net6.0) / 0.15.8 (net8.0/net10.0); Fluid.Core 2.31.0, Scriban 7.2.5 (`#if !NET6_0`), DotLiquid 2.3.197, Handlebars.Net 2.1.6 — spike A §5 |
| Published-report shape | `docs/benchmarks/2026-07-18/index.md` *(read)* — H1, intro + reproduce command, `## Environment` fenced block, `## The workloads`, `## Results — what this run actually shows`, `## Files`; artifacts are the BenchmarkDotNet md/csv/html triplet per suite. The protocol formalizes exactly this |
| Cross-language escaper landscape | Spike C (primary sources): two spelling families (decimal/named: Tera, Eta, Jinja2/Mako, Thymeleaf, JTE, Go, templ; hex: Askama, Handlebars-JS); broader-set offenders Go `html/template` (`+`) and Handlebars-JS (`` ` ``, `=`); JTE has **no** inline trim syntax (build-flag only) and templ has **no** whitespace control at all — corrections to dossier assumptions, owned by phases 3/6, reflected here only in the untrusted-data alphabet and exclusion policy |

## Design decisions

Every plan open question, lean, and pin touching this phase, closed. (Q1.1–Q1.7, Q2.1, Q2.2,
Q6.2 arrive already user-resolved; the decisions below bind their resolutions to concrete
mechanisms and close the spec-territory details the plan delegated.)

### D1 — The five new workloads and their exact shapes
- **Decision.** The workload set is fixed at eight as pinned in
  [workloads.md](workloads.md): anchors 1–3 byte-unchanged; `mixed-page` (36 products, full HTML
  skeleton, page- and row-level conditionals), `conditional-heavy` (200 rows × one four-way
  branch chain on precomputed booleans + two toggles), `fragment-heavy` (48 invocations of one
  `tile` partial receiving the current row), `fortunes-encoded` (12 pinned rows incl. the
  TechEmpower XSS payload and Japanese string, text context only), `encoded-loop` (5,000 rows,
  escapables in every cell, text + attribute contexts). Model data is pinned to exact strings
  and generation formulas; template texts are normative per engine.
- **Rationale.** Each workload owns exactly the dimension the plan's table assigns it; branching
  on precomputed booleans keeps the four-way chain inside vanilla Handlebars (no equality
  helpers — common-denominator constraint) and measures dispatch on identical data everywhere;
  the fragment-heavy constructs are the ones probe E verified per engine.
- **Alternatives rejected.** Integer-comparison branching (needs non-denominator helpers in
  Handlebars); Scriban positional include-args via `$0` (probe E: does not bind against
  dictionary rows); DotLiquid `include with` (probe E: enumerates a Hash argument);
  Fortunes-verbatim `4.33e+67` (the `+` would structurally exclude Go `html/template` later —
  D4).
- **Grounding.** Probe E results (Assumed state); plan workload table;
  [workloads.md](workloads.md).

### D2 — Entity canonicalization is scoped to the five-character set (Q1.1)
- **Decision.** Contract v2 adds normalization step N5 (encoded suite only): a closed
  replacement table canonicalizing every recognized spelling of `& < > " '` to
  `&amp; &lt; &gt; &quot; &#39;` — Heddle's own spellings, so the oracle is already canonical.
  The step is scoped to the **literal five characters**; spellings of any other escaped
  character are never canonicalized. Engine configuration is preferred to normalization wherever
  offered (intra-.NET this is fully achieved — D3 — so the intra-.NET gate implements no N5 code
  at all). Full table and matching rules in
  [parity-contract-v2.md](parity-contract-v2.md#entity-canonicalization-n5).
- **Rationale.** Q1.1 resolution verbatim, plus spike B's finding that Heddle's own two encoders
  disagree outside the five-character set — scoping to "whatever the engine escapes" would
  inherit that intra-engine inconsistency, so the character set is pinned literally.
- **Alternatives rejected.** Canonicalizing all numeric character references (would silently
  reconcile genuine broader-set divergence — e.g. Go's `&#43;` — that must surface as evidence);
  configuration-only with no N5 (Askama and Handlebars-JS have no spelling configuration — spike
  C — so phases 2/4 would be stuck).
- **Grounding.** Spike B §(c) (executed); spike C spelling-family table (primary sources);
  [open-questions Q1.1](../../../plan/open-questions.md).

### D3 — Handlebars.Net encoded twins use a custom ITextEncoder
- **Decision.** The encoded-suite Handlebars.Net twins render **double-mustache** under
  `Handlebars.Create(new HandlebarsConfiguration { TextEncoder = new FiveEntityTextEncoder() })`,
  where `FiveEntityTextEncoder` (new file `Runners/FiveEntityTextEncoder.cs`, internal,
  implements `HandlebarsDotNet.ITextEncoder`) escapes exactly `&`→`&amp;`, `<`→`&lt;`,
  `>`→`&gt;`, `"`→`&quot;`, `'`→`&#39;` and passes everything else through. No exclusion, no
  normalization carve-out.
- **Rationale.** Handlebars.Net's default encoder leaves `'` unescaped and decimal-escapes
  non-ASCII (spike B, executed) — a genuine gate-breaking divergence. Q1.1 prefers engine
  configuration; probe D **executed** the mechanism and confirmed byte-identity with the
  WebUtility family on the exact payload set. Raw-suite Handlebars twins keep triple-mustache,
  unchanged.
- **Alternatives rejected.** Excluding the Handlebars.Net encoded cells (configuration exists
  and works — exclusion would be unjustified); normalizing its output (would need a
  four-character carve-out for the unescaped `'` — beyond spelling canonicalization, that is
  divergence, not spelling); `SafeString`/helper workarounds (change what is measured).
- **Grounding.** Probe D output (Assumed state row "Handlebars.Net custom encoder"); spike B
  §(d).

### D4 — The encoded-suite untrusted-data alphabet
- **Decision.** Encoded-suite untrusted values are restricted to: ASCII printable minus `+`,
  `=`, `` ` ``, never containing the substring `&#`; plus BMP U+0100–U+FFFF (no surrogates);
  Latin-1 supplement U+00A0–U+00FF and astral characters forbidden. Recorded in the contract
  ([untrusted-data alphabet](parity-contract-v2.md#untrusted-data-alphabet)) with a per-exclusion
  grounding table; both encoded workloads' pinned data complies.
- **Rationale.** This is the closure of the plan's flagged "context-confinement assumption":
  spike B proved the assumption holds only for the five-character set (Heddle's own two encoders
  split on Latin-1; WebUtility escapes astral pairs), and spike C identified the broader-set
  offenders (`+` in Go, `` ` ``/`=` in Handlebars-JS). Constraining the data — disclosed in the
  contract — is what makes one oracle satisfiable by every cast escaper without per-engine
  carve-outs.
- **Alternatives rejected.** Unrestricted data + wider normalization (reconciles real divergence
  invisibly — anti-credibility); restricting to pure ASCII (loses the UTF-8-intact dimension the
  Fortunes shape exists to test).
- **Grounding.** Spike B §(b)/(c) (executed); spike C §(b); golang/go#42506.

### D5 — The composed-page anchor keeps its fragment-sequence shape (Q1.4)
- **Decision.** `composed-page` is exported to the corpus exactly as it renders today; the
  Runners README fidelity note and `@<<` root-cause text are copied **verbatim** into
  `GoldenCorpus/README.md`. The mid-size mixed page carries realistic-full-page duty.
- **Rationale.** Q1.4 resolution verbatim; changing the shape requires an engine change ruled
  out of scope.
- **Alternatives rejected.** New full-page composition workload (engine change, out of scope);
  compiling `layout.heddle` as entry point (drops the `<body:body>` override and forces twins to
  embed layout HTML verbatim — parity-drift risk documented in the Runners README).
- **Grounding.** [Runners README fidelity note + root cause](../../../../src/Heddle.Performance/Runners/README.md);
  [open-questions Q1.4](../../../plan/open-questions.md).

### D6 — The corpus stores the normalized oracle under `src/Heddle.Performance/GoldenCorpus/`
- **Decision.** One `<id>.golden.html` per workload containing the **normalized** Heddle output
  as UTF-8 without BOM and without an appended trailing newline; `manifest.json` records
  `byteLength`, `sha256`, `generatingCommit`, `generatedUtc` per entry; `.gitattributes` pins
  `*.golden.html -text` (byte-exact round-trip) in the same change. Location is beside the
  generator; full format in [golden-corpus.md](golden-corpus.md).
- **Rationale.** The gate everywhere is `strip(normalize(candidate)) == strip(oracle)` — the N1–N5
  pipeline on the candidate, then N3b (whitespace removal) applied to both sides at comparison;
  storing the readable normalized form means consumers implement the pipeline once, for their own
  output, and strip whitespace from both their output and the loaded oracle. `-text` (not `eol=lf`)
  because the files intentionally end without a newline and must never be smudged. The location
  avoids inventing a top-level directory (reversible: moving the corpus later is an ordinary
  versioned change under D7).
- **Alternatives rejected.** Raw render bytes (every consumer would also need Heddle-side
  normalization of the oracle); a new top-level `benchmarks/` directory (bigger repo-structure
  decision than this phase needs; revisit trigger recorded in golden-corpus.md); storing hashes
  only without content (phases 2–6 need the bytes to diff against).
- **Grounding.** [testing-standards — fixtures and goldens](../../common/testing-standards.md#fixtures-and-goldens);
  spike A §7 (no existing mechanism).

### D7 — Corpus regeneration is an ordinary versioned change (Q1.5)
- **Decision.** No corpus version numbers, no bump ceremony, no mandatory re-run of shipped
  ecosystems' gates. Regeneration = run `export-corpus` at a clean commit, review diff, commit.
  The export tool refuses a dirty tree without `--allow-dirty` (which stamps `+dirty`, flagged
  by `verify-corpus`), keeping `generatingCommit` honest.
- **Rationale.** Q1.5 resolution — the user explicitly rejected pinning ceremony; git versioning
  plus per-entry commit + hash gives full auditability.
- **Alternatives rejected.** Version-bump + re-run-all-gates ceremony (rejected by user ruling);
  no dirty-tree guard (would let `generatingCommit` record a commit that cannot reproduce the
  bytes — breaks the plan's reproducibility success criterion).
- **Grounding.** [open-questions Q1.5](../../../plan/open-questions.md) (recommended default
  rejected by user, 2026-07-20).

### D8 — Normalization v2: v1's three steps + defined encoding + portable whitespace definition + N3b run-collapse
- **Decision.** The v2 pipeline is the closed list N1–N5 (including the inserted step **N3b**) of
  [parity-contract-v2.md](parity-contract-v2.md#normalization-pipeline): N1 UTF-8 (no BOM;
  a BOM survives and fails), N2–N4 = v1's three steps verbatim (they define the stored oracle),
  **N3b = the whitespace-run collapse the maintainer added 2026-07-20** — every whitespace run
  anywhere is removed **to nothing** (not to a space) from **both** the normalized oracle and the
  candidate at comparison time, so the gate compares only non-whitespace bytes — N5 = D2's
  canonicalization (encoded only). *Whitespace* is pinned to the six ASCII characters
  TAB/LF/VT/FF/CR/SPACE for N3/N3b/N4 cross-language use.
- **N3b — maintainer decision (2026-07-20).** Rationale: it reconciles Q1.2's whitespace-only-passes
  ruling with the closed-list principle by a single uniform, disclosed step (not a per-phase fork,
  not a curve). Because it removes each run entirely rather than collapsing it to a single space,
  its ratified effect is that **any** whitespace-only divergence — run-length change **or**
  presence-vs-absence — passes the controlled gate program-wide, and the exclusion path applies only
  to non-whitespace divergence; it closes the Phase 6 D13 escalation (option A). N3b is a
  comparison-time projection applied to both sides, **not** baked into the exported oracle, so the
  stored goldens keep their readable N1–N5 bytes and no shipped raw golden is disturbed.
- **Rationale.** V1 carries forward unchanged for raw suites (plan requirement); "byte-identical"
  needs one encoding definition to mean the same thing on every runtime; `\s` differs across
  regex engines (.NET Unicode vs Go RE2 ASCII), so the portable closed set is spelled out — it
  is output-equivalent for every workload because no template emits non-ASCII whitespace between
  tags.
- **Alternatives rejected.** Adopting Unicode whitespace cross-language (Go's stdlib regex
  cannot express it directly and no workload needs it); stripping BOMs (hides a real engine
  difference).
- **Grounding.** `TwinContent.Normalize` *(read)*; .NET `\s` vs RE2 `\s` semantics (regex
  documentation, External references).

### D9 — Intra-.NET controlled gate = live twin parity + corpus freshness, both in GlobalSetup
- **Decision.** The five new workloads extend `ParityCheck` with `Twins<W>()`, `Assert<W>()`,
  `Report<W>()` using the existing `AssertAgainst`/`ReportAgainst` helpers, whose **comparison
  applies N3b**: after `TwinContent.Normalize` shapes both the twin and the live-Heddle output
  into the readable N1–N4 form (for the diff excerpt), the whitespace strip is applied to **both**
  sides and only the non-whitespace bytes are compared — exactly as the contract's controlled gate
  requires *in every ecosystem*
  ([controlled-track gate](parity-contract-v2.md#controlled-track-gate)). Every render
  benchmark's `[GlobalSetup]` calls its `Assert<W>()` **and** `GoldenCorpus.AssertFresh("<id>")`
  (live Heddle vs committed corpus — this freshness check stays **byte-exact on the stored N1–N5
  form**, since it compares Heddle's own output to its own committed oracle), so timing can start
  only when twins match Heddle *and* Heddle matches the committed oracle. The `parity` CLI verb
  reports all eight workloads.
- **Rationale.** Twin-vs-live comparison is the proven v1 mechanism; applying N3b at that
  comparison makes the intra-.NET admission gate identical to the measurement gate every ecosystem
  is graded against (the twins are the controlled track — Vocabulary — so they run the controlled
  gate). It is a **pure loosening** over v1's byte-exact-after-N2/N3/N4 comparison: every existing
  twin is already byte-identical after N2/N3/N4, so stripping whitespace from two equal strings
  leaves them equal — no shipped anchor's pass/fail changes. The freshness assert closes the one
  gap the corpus introduces (a stale committed oracle) at setup-time cost only.
- **Alternatives rejected.** Comparing twins directly against corpus files instead of live
  Heddle (would pass even when the engine has drifted from the corpus — freshness must be its
  own check); a CI-only freshness check (the gate must sit in front of *every* timed run, per
  the contract); making the intra-.NET twin comparison **stricter** than the measurement gate
  (byte-exact after N2/N3/N4, no N3b) — it would be the program's only non-uniform gate and could
  force a redesign of a workload whose twins differ only in whitespace even though a cross-language
  port with the identical whitespace divergence would pass (over-strict admission, rejected in
  favor of one uniform gate).
- **Grounding.** `ParityCheck.cs` structure *(read)*;
  [parity-contract-v2.md — controlled-track gate](parity-contract-v2.md#controlled-track-gate).

### D10 — Idiomatic verifier: C# single source, exported JSON, calibration in `verify-corpus`
- **Decision.** Verifier check definitions live once in `Runners/IdiomaticChecks.cs` (values /
  ordered markers / forbidden / required, per
  [golden-corpus.md](golden-corpus.md#idiomatic-verifier-definitions)); `export-corpus` writes
  them as `<id>.verify.json` for cross-language consumption; `verify-corpus` proves each
  verifier accepts its golden and rejects its canonical corruptions — removed row and swapped
  sections for every workload, plus an unescaped-payload corruption for encoded workloads only
  (two corruptions per raw workload, three per encoded workload).
- **Rationale.** The plan's success criterion demands a verifier that demonstrably rejects
  corrupted output; a single C# source with exported JSON avoids double-maintenance while giving
  phases 2–6 a language-neutral artifact.
- **Alternatives rejected.** Hand-authored JSON (counts drift from models); verifier tests in
  `Heddle.Tests` (would couple the test project to the benchmark project; the CLI calibration is
  the same proof, run by the same gate command).
- **Grounding.** Plan success criteria and validation scenarios;
  [parity-contract-v2.md — idiomatic-track gate](parity-contract-v2.md#idiomatic-track-gate).

### D11 — Exclusion policy encodes Q1.2 with named structural exclusions
- **Decision.** As written in
  [parity-contract-v2.md — exclusion policy](parity-contract-v2.md#exclusion-policy): structural
  exclusions (Pug, Slim, Haml, maud) documented and never curve-graded; whitespace-only
  divergence passes by construction (N3b — the 2026-07-20 maintainer decision — removes every
  whitespace run to nothing from both sides at comparison, so **any** whitespace-only divergence,
  run-length or presence/absence, passes); only **non-whitespace** divergence beyond normalization →
  idiomatic track retained, controlled cell marked `excluded — documented evidence`, replacement
  engines require user sign-off; a workload that cannot pass intra-.NET is redesigned or dropped
  before corpus export.
- **Rationale.** Q1.2 resolution verbatim, with the 2026-07-20 maintainer decision that a single
  uniform N3b whitespace-run collapse (to nothing) is the disclosed mechanism by which
  "whitespace-only passes" is honored program-wide (closing the Phase 6 D13 escalation, option A).
- **Alternatives rejected.** Curve-graded inclusion, silent replacement engines (both barred by
  standing rulings).
- **Grounding.** [open-questions Q1.2](../../../plan/open-questions.md).

### D12 — Wall-time statistic mapping (Q2.1)
- **Decision.** Fixed per-harness table in
  [metrics-protocol.md](metrics-protocol.md#wall-time-statistic-mapping-q21): BenchmarkDotNet
  `Mean`, Criterion.rs mean estimate, JMH avgt `Score`, mitata `avg`, pyperf `mean`, benchstat
  `sec/op`; each with its harness-native dispersion published alongside; ecosystem specs pin
  versions/settings but cannot vary the statistic selection.
- **Rationale.** Q2.1 resolution verbatim: harness-default point estimates keep each harness
  native and credible; the mapping documented once.
- **Alternatives rejected.** One statistical methodology forced on all harnesses (explicitly
  rejected in the resolution).
- **Grounding.** [open-questions Q2.1](../../../plan/open-questions.md); harness docs (External
  references).

### D13 — Presentation rules (Q2.2 = A, Q6.2) with the allocation baseline pinned
- **Decision.** As written in
  [metrics-protocol.md — presentation rules](metrics-protocol.md#presentation-rules-q22--a-q62):
  labeled wall-time-only Heddle reference row in every per-ecosystem report, excerpted from this
  phase's protocol run; ratio column anchors to that row; non-Heddle engines never ranked across
  ecosystems; and — the one detail Q6.2 left to the spec — **within-ecosystem baselines for
  non-cross-comparable metrics anchor to the ecosystem's credibility pick** (Tera, Thymeleaf,
  Handlebars, Jinja2, html/template), while intra-.NET reports keep Heddle as baseline.
- **Rationale.** Q2.2/Q6.2 resolutions verbatim; the credibility pick is the stable,
  always-present engine in each cast pair, giving phase 7 uniform within-ecosystem tables.
- **Alternatives rejected.** Per-phase baseline choice (Q6.2 option B — inconsistent columns);
  performance pick as baseline (the pick most likely to hit an exclusion cell — e.g. templ —
  would strand its baseline duty).
- **Grounding.** [open-questions Q2.2, Q6.2](../../../plan/open-questions.md).

### D14 — Machine, publication format, honest reporting, cold-cost rule (Q1.6, Q1.3)
- **Decision.** All phase 1–7 cross-compared runs on the Windows 11 / Ryzen 9 9950X box with a
  required environment block; publication as immutable `docs/benchmarks/<yyyy-MM-dd>/`
  directories in the pinned `index.md` shape; honest-reporting rules 1–6 and the two verbatim
  label texts (allocation non-comparability; encoded confinement caveat) as written in
  [metrics-protocol.md](metrics-protocol.md); cold parse/compile per-ecosystem only.
- **Rationale.** Q1.6 and Q1.3 resolutions verbatim; the report shape formalizes what
  2026-07-11/2026-07-18 already do, so the protocol's first exercise is continuous with the
  published history.
- **Alternatives rejected.** Linux box (severs continuity — rejected in Q1.6; Linux is Phase 8,
  separate); free-form report layout (phases 2–6 need one shape to fill in).
- **Grounding.** [open-questions Q1.3, Q1.6, Q5.2](../../../plan/open-questions.md);
  `docs/benchmarks/2026-07-18/index.md` *(read)*.

### D15 — Phase 1 ships the controlled track only, intra-.NET
- **Decision.** The intra-.NET suite is, by construction, the controlled track (four twins under
  the byte gate); Phase 1 implements **no** intra-.NET idiomatic-track template variants. The
  idiomatic deliverables of this phase are the verifier definitions, their calibration, and the
  authoring standard (D16) — the artifacts phases 2–6 need.
- **Rationale.** The dual tracks answer a cross-ecosystem question; intra-.NET the Heddle-vs-twin
  comparison is already published and parity-gated, and no plan success criterion asks for .NET
  idiomatic implementations. YAGNI cuts the scope.
- **Alternatives rejected.** Authoring idiomatic .NET twins now (no consumer until/unless .NET
  is ever presented as an ecosystem row in phase 7's idiomatic tables — recorded as the revisit
  trigger in [Deferred items](#deferred-items)).
- **Grounding.** Plan success criteria (none requires intra-.NET idiomatic implementations);
  plan §Design direction (tracks defined for "every ecosystem", i.e. phases 2–6).

### D16 — Idiomatic authoring standard (Q1.7)
- **Decision.** Idiomatic implementations are authored in-repo following each engine's official
  documentation patterns, with the doc pages cited in a header comment per implementation file;
  never imported from third-party benchmark repos. Recorded in the contract
  ([idiomatic-track gate](parity-contract-v2.md#idiomatic-track-gate)) as binding on phases 2–6.
- **Rationale / Alternatives rejected / Grounding.** Q1.7 resolution verbatim; third-party
  import rejected there ([open-questions Q1.7](../../../plan/open-questions.md)).

## Implementation plan

Ordered; each item lists exact paths, the shape of the change, and its completion check. All new
runner/content classes follow the existing file-per-concern layout of
`src/Heddle.Performance/Runners/`.

### WI1 — Models and Heddle oracles for the five new workloads
- **Files.** New: `src/Heddle.Performance/Runners/MixedContent.cs`, `ConditionalContent.cs`,
  `FragmentContent.cs`, `FortunesContent.cs`, `EncodedLoopContent.cs`;
  `src/Heddle.Performance/TestTemplates/mixed-page.heddle`, `conditional-heavy.heddle`,
  `fragment-heavy.heddle`, `fortunes-encoded.heddle`, `encoded-loop.heddle`;
  `src/Heddle.Performance/Runners/MixedHeddleTest.cs`, `ConditionalHeddleTest.cs`,
  `FragmentHeddleTest.cs`, `FortunesHeddleTest.cs`, `EncodedLoopHeddleTest.cs`.
- **Change.** Content classes: the pinned models of [workloads.md](workloads.md) — typed model +
  static `Shared` instance + `SharedDictionary` (lowercase snake_case keys, Fluid/Scriban/
  Handlebars) + `SharedDotLiquid` (`DotLiquid.Hash`) materialized once. Templates: the normative
  texts of workloads.md. Heddle runners: clone `SubstitutionHeddleTest` (public sealed, ctor
  compiles once, `public string Render()`), `OutputProfile.Text` for the three raw /
  `OutputProfile.Html` for the two encoded workloads.
- **Done when.** Each `Render()` produces output containing its workload's distinctive markers
  (spot-run via a temporary `parity`-style call or debugger); the encoded oracles contain
  `&lt;script&gt;alert(` / `&#39;` and no raw payload; solution builds on all three TFMs.

### WI2 — Raw-suite twins (mixed-page, conditional-heavy, fragment-heavy)
- **Files.** New: `Runners/MixedLiquidTemplates.cs`, `ConditionalLiquidTemplates.cs` (each a
  shared Fluid+DotLiquid source), `FragmentLiquidTemplates.cs` (distinct Fluid and DotLiquid
  sources — fragment-heavy's include syntax and in-partial variable names differ per engine,
  probe E) and per-engine twins
  `Mixed{Fluid,Scriban,DotLiquid,Handlebars}Test.cs`,
  `Conditional{Fluid,Scriban,DotLiquid,Handlebars}Test.cs`,
  `Fragment{Fluid,Scriban,DotLiquid,Handlebars}Test.cs` (Scriban files `#if !NET6_0` at the
  registration sites, matching existing pattern).
- **Change.** Twin templates per [workloads.md](workloads.md) (fragment-heavy uses exactly the
  probe-E-verified constructs; raw path on every engine — Fluid `Render(ctx)` no encoder,
  Scriban/DotLiquid default, Handlebars triple-mustache). Each twin: parse/compile once in ctor,
  `public string Render()`.
- **Done when.** For each of the three workloads, all four twins pass the intra-.NET controlled
  gate against the Heddle oracle (D9: `TwinContent.Normalize` then the N3b whitespace strip on both
  sides — the twins are authored to `TwinContent.Normalize`-equal Heddle, the stronger bar, so they
  pass), checked in WI4's parity run.

### WI3 — Encoded-suite twins and the Handlebars encoder
- **Files.** New: `Runners/FiveEntityTextEncoder.cs`;
  `Runners/FortunesLiquidTemplates.cs`, `EncodedLoopLiquidTemplates.cs`;
  `Fortunes{Fluid,Scriban,DotLiquid,Handlebars}Test.cs`,
  `EncodedLoop{Fluid,Scriban,DotLiquid,Handlebars}Test.cs`.
- **Change.** `FiveEntityTextEncoder : HandlebarsDotNet.ITextEncoder` — internal sealed;
  implements all three `Encode` members by per-char switch emitting
  `&amp;/&lt;/&gt;/&quot;/&#39;`, passthrough otherwise (probe D shape). Encoded Handlebars
  twins call `Handlebars.Create(new HandlebarsConfiguration { TextEncoder = new FiveEntityTextEncoder() })`
  and use double-mustache; Fluid/DotLiquid twins use `| escape`, Scriban `| html.escape`, per
  the workload tables.
- **Done when.** Both encoded workloads: all four twins pass the intra-.NET controlled gate
  against the Heddle oracle (D9 — `TwinContent.Normalize` then N3b strip both sides; the twins are
  authored to normalize-equal Heddle, the stronger bar); a search of every twin's raw output finds
  zero `<script>alert(`.

### WI4 — ParityCheck + CLI extension to eight workloads
- **Files.** Changed: `Runners/ParityCheck.cs`, `Program.cs`.
- **Change.** Add `TwinsMixed/TwinsConditional/TwinsFragment/TwinsFortunes/TwinsEncodedLoop`
  (Scriban entries `#if !NET6_0`) and the ten `Assert*/Report*` wrappers delegating to the
  existing `AssertAgainst`/`ReportAgainst` (signatures unchanged). Those two helpers' **comparison
  body** gains the N3b step: strip every whitespace run from both `TwinContent.Normalize` outputs
  before the ordinal compare (the `Describe` excerpt still renders the readable normalized form),
  so all eight workloads gate on non-whitespace bytes — uniform with the contract and the phase
  2–6 runners; this is verdict-preserving on the three anchors (already byte-identical after
  N2/N3/N4). `parity` verb prints all eight reports and exits 0 iff all pass.
- **Done when.** `dotnet run -c Release --project src/Heddle.Performance -f net10.0 -- parity`
  prints eight blocks, all `[PASS]`, exit 0 — and the composed-page block still reports
  55,456 raw / 34,837 normalized chars (anchor regression pin); same on `-f net8.0`; `-f net6.0`
  passes with Scriban absent.

### WI5 — Benchmark classes for the five new workloads
- **Files.** New: `src/Heddle.Performance/MixedRenderBenchmarks.cs`,
  `ConditionalRenderBenchmarks.cs`, `FragmentRenderBenchmarks.cs`,
  `FortunesRenderBenchmarks.cs`, `EncodedLoopRenderBenchmarks.cs`.
- **Change.** Clone the host-free `SubstitutionRenderBenchmarks` shape: `[MemoryDiagnoser]`;
  `[GlobalSetup]` constructs the five runners and calls the workload's `ParityCheck.Assert*`;
  `RenderHeddle` `[Benchmark(Baseline = true)]`; `RenderFluid`, `RenderScriban`
  (`#if !NET6_0`), `RenderDotLiquid`, `RenderHandlebars` `[Benchmark]`; each method returns the
  rendered string (BenchmarkDotNet consumes the return value).
- **Done when.** `dotnet run … -- --filter *MixedRenderBenchmarks*` (each class) starts, passes
  its GlobalSetup gate, and produces a results table.

### WI6 — Golden corpus: export tool, verifier, freshness wiring
- **Files.** New: `Runners/GoldenCorpus.cs`, `Runners/IdiomaticChecks.cs`,
  `src/Heddle.Performance/GoldenCorpus/README.md` (+ the exported corpus files and manifest);
  changed: `Program.cs` (verbs `export-corpus [--allow-dirty]`, `verify-corpus`),
  `.gitattributes` (corpus pin rules per [golden-corpus.md](golden-corpus.md#on-disk-format)),
  the eight render-benchmark classes' `[GlobalSetup]` (add
  `GoldenCorpus.AssertFresh("<workload-id>")` after the parity assert — the three existing
  classes receive this additive line; nothing else in them changes).
- **Change.** As specified in [golden-corpus.md](golden-corpus.md): eight-entry registry;
  normalize-and-write export with SHA-256 + `git rev-parse HEAD` stamping + dirty-tree refusal;
  `AssertFresh` (render, normalize, byte-compare, throw `InvalidOperationException` with the
  `Describe`-style excerpt on mismatch); `IdiomaticChecks` tables transcribed from
  golden-corpus.md; `verify-corpus` = freshness + calibration (accept golden, reject each
  workload's corruptions — two per raw workload, three per encoded workload). `GoldenCorpus/README.md` carries the corpus contract summary, the
  verbatim composed-page fidelity note (D5), and links to this spec's contract docs.
- **Done when.** `export-corpus` at a clean commit writes 8 goldens + 8 verify files + manifest;
  a second export at the same commit is byte-identical except `generatedUtc`; `verify-corpus`
  exits 0 with all `[PASS]` and all corruption rejections; committing the corpus then re-running
  any benchmark class passes its `AssertFresh`.

### WI7 — Documentation cross-links
- **Files.** Changed: `src/Heddle.Performance/Runners/README.md` (append a short "Contract v2"
  section: one paragraph stating v1 remains authoritative for the intra-.NET raw suites and
  linking to `docs/spec/cross-stack-benchmarks/phase-1-cross-stack-foundation/parity-contract-v2.md`
  and the corpus directory); `docs/spec/README.md` (master-index row for this initiative, per
  the index-maintenance convention).
- **Change.** Additive prose only; no existing sentences edited.
- **Done when.** Links resolve; v1 text untouched above the appended section.

### WI8 — The protocol's first exercise: eight-workload run, published
- **Files.** New: `docs/benchmarks/<run-date>/index.md` + the BenchmarkDotNet md/csv/html
  triplets for all eight suites (`TextRenderBenchmarks`, `SubstitutionRenderBenchmarks`,
  `LoopRenderBenchmarks`, and the five WI5 classes).
- **Change.** Run each suite `-c Release`, `net10.0`, on the protocol machine; copy artifacts;
  author `index.md` in the exact
  [publication format](metrics-protocol.md#publication-format) — including the environment
  block, per-workload dimension statements, the allocation label, the encoded caveat beside the
  encoded-suite results, and explicit reporting of every workload where any twin beats Heddle on
  any metric.
- **Done when.** The directory exists with all artifacts; `index.md` satisfies every
  honest-reporting rule (checklist review against metrics-protocol.md §Honest-reporting rules);
  the corpus `generatingCommit` for all entries is an ancestor of the published run's commit.

## Public API / contract

No public .NET API is added or changed: every new type in `Heddle.Performance` is `internal` (or
`public` only within the unpublished benchmark exe, matching existing runner classes — the
project ships no package). Thread-safety: all new content classes expose only immutable static
data materialized in static initializers; runners are single-threaded benchmark fixtures, as
today.

The **externally consumed contract** this phase creates is artifact-shaped, not API-shaped:

| Artifact | Consumers | Normative definition |
|---|---|---|
| `src/Heddle.Performance/GoldenCorpus/*.golden.html` + `manifest.json` | phases 2–6 gate runners | [golden-corpus.md](golden-corpus.md) |
| `GoldenCorpus/*.verify.json` | phases 2–6 idiomatic verifiers | [golden-corpus.md](golden-corpus.md#idiomatic-verifier-definitions) |
| Parity contract v2 | phases 2–7 | [parity-contract-v2.md](parity-contract-v2.md) |
| Metrics & publication protocol | phases 2–8 | [metrics-protocol.md](metrics-protocol.md) |
| `docs/benchmarks/<date>/` first protocol run | Heddle reference rows in phases 2–6 | [metrics-protocol.md](metrics-protocol.md#the-protocols-first-exercise-this-phase) |

## Diagnostics / error surface

**HED\* compiler diagnostics: n/a** — this is a benchmark workstream; it changes no compiler,
grammar, or engine surface, so no diagnostic IDs are claimed and the
[registry](../../common/cross-cutting-decisions.md#claimed-diagnostic-ids-registry) is untouched.
(The encoded templates are authored with `@attr` in attribute positions precisely so the
existing `HED2004` lint has nothing to warn about.)

The phase's own error surface (all host-side, none compile-time):

| Surface | Type / exit | Message shape | Trigger |
|---|---|---|---|
| Benchmark-time parity gate | `InvalidOperationException` from `[GlobalSetup]` (BenchmarkDotNet aborts the run; no timings produced) | `Parity twin '<name>' diverged from Heddle. first diff at index <i> (of exp <n>/act <m>).\n    expected: ...<120-char excerpt>...\n    actual:   ...` (existing `Describe` shape, unchanged) | any twin's normalized output differs from live Heddle's in a **non-whitespace** byte (the comparison strips whitespace from both via N3b — D9; whitespace-only divergence passes) |
| Corpus freshness gate | `InvalidOperationException` from `[GlobalSetup]` | `Corpus entry '<workload>' is stale: live Heddle output diverged from GoldenCorpus/<file>. first diff at index <i> …` (same excerpt shape) | live normalized Heddle output ≠ committed corpus bytes |
| `parity` verb | exit 1 | `[FAIL] <engine>  first diff at index …` lines + `PARITY FAILED.` per workload block (existing shape, now eight blocks) | any twin mismatch |
| `export-corpus` dirty tree | exit 1, no files written | `export-corpus: working tree is dirty; commit first or pass --allow-dirty.` | `git status --porcelain` non-empty without `--allow-dirty` |
| `verify-corpus` | exit 1 | `[FAIL] <workload> freshness: …` / `[FAIL] <workload> verifier <value\|marker\|forbidden\|required>: expected <N> of "<needle>", found <M>` / `[FAIL] <workload> calibration: corruption '<removed-row\|reordered\|unescaped>' was NOT rejected` | freshness mismatch, verifier miss, or a corruption the verifier accepted |
| Encoded security floor | included in the encoded workloads' verifier `forbidden` check and gate | `[FAIL] <workload> verifier forbidden: expected 0 of "<script>alert(", found <M>` | raw payload present in any output |

## Testing plan

**TDD verdict.** The gates *are* the executable spec, and they run test-first by construction:
for each workload the Heddle oracle + `ParityCheck` assertions are written before the twins,
so `-- parity` starts red per twin and turns green as each twin is authored (WI2/WI3); the
corpus is exported only after WI4's all-green run; `verify-corpus`'s calibration (accept golden
/ reject corruptions) is implemented with `IdiomaticChecks` and must pass before the corpus
commit. No conventional xUnit additions: this phase adds no code to `src/Heddle`
(`Heddle.Tests` is unaffected), and the benchmark project's gates are its test suite — the same
verdict the existing suite operates under.

**Named checks (fixtures/goldens/benchmarks).**
- Goldens: the eight `GoldenCorpus/<id>.golden.html` files (+ manifest) — golden-change policy
  applies: they change only via `export-corpus` at a clean commit with the diff reviewed (D7).
- Gate commands: `-- parity` (eight blocks), `-- verify-corpus` (freshness + calibration), both
  exit-code-gated.
- Benchmarks: the five new `*RenderBenchmarks` classes plus the three existing ones — all eight
  carry the parity + freshness GlobalSetup gate.
- Negative/security: the calibration corruptions (removed row / reordered sections / unescaped
  payload per encoded workload) are the spec-mandated negative tests; the Fortunes rule
  (`<script>alert(` zero occurrences) is asserted for every engine's output, not only Heddle's.

**Regression gate (one combined run before merge).**
1. `dotnet build -c Release` — whole solution, all TFMs.
2. `dotnet test src/Heddle.Tests` — full suite, all TFMs (proves the phase touched no engine
   behavior; zero engine-side diffs expected).
3. `dotnet run -c Release --project src/Heddle.Performance -f net10.0 -- parity` — eight
   `ALL TWINS MATCH.` blocks; composed-page still 55,456 raw / 34,837 normalized chars, and the
   trivial-substitution/large-loop blocks report the same counts as before the change (anchor
   byte-stability proof).
4. Same `parity` run on `-f net8.0` and `-f net6.0` (net6.0 without Scriban rows).
5. `… -- verify-corpus` — exit 0.
6. Grammar-stability: no diff under `src/Heddle.Language/generated/` (this spec declares no
   grammar change).
7. WI8's published run is the phase's benchmark evidence; no existing benchmark baseline is
   allowed to regress by more than BenchmarkDotNet's reported error (the three anchor suites are
   re-measured in the same published run; nothing in this phase touches a Heddle hot path, so
   any regression is environmental and re-run).

## Back-compat and migration

- **Anchors byte-unchanged — proven,** not assumed: WI4's completion check pins the
  composed-page 55,456/34,837 counts and the other anchors' counts to their pre-change values,
  and regression-gate step 3 re-proves it in the final combined run. No anchor template, model,
  runner, or benchmark file is edited except the additive `AssertFresh` line in three
  `[GlobalSetup]` methods (WI6) — which changes no rendered byte.
- **Contract v1 remains valid** for the existing intra-.NET raw suites; the Runners README keeps
  its text and gains only an appended pointer (WI7). V2 is a superset: raw workloads never
  exercised N1's BOM rule or N5, and the N3b comparison strip V2 adds to the intra-.NET twin gate
  (D9) is a pure loosening — every existing twin is already byte-identical after N2/N3/N4, so
  stripping whitespace from both sides leaves the verdict unchanged — so their pass/fail status is
  identical under both.
- **Razor twin:** untouched, still outside every gate (`ParityCheck.Twins()` continues not to
  yield it), not part of the cross-stack contract.
- **Published reports:** `docs/benchmarks/2026-07-11/` and `2026-07-18/` untouched; WI8 creates
  a new date.
- **No breaking window needed:** every change is additive
  ([D2 — breaking windows](../../common/cross-cutting-decisions.md#d2--breaking-changes-land-only-in-ratified-breaking-windows)
  is not engaged); the new external contract (corpus + v2) is new surface, not a change to
  existing surface.

## Performance considerations

- **No Heddle engine code is touched**; no engine hot path changes. The phase's own runtime
  additions are setup-time only: each benchmark suite pays one extra Heddle render + byte
  compare (`AssertFresh`) in `[GlobalSetup]`, outside all timed regions.
- Models for all five new workloads are materialized once in static initializers (the
  `LoopContent.Shared` discipline), so no benchmark op allocates model data; benchmark methods
  return the rendered string so BenchmarkDotNet's consumer sees it (no dead-code elimination).
- The encoded suite intentionally measures the escaping path — that *is* the metric; the
  confinement caveat (D14) governs its interpretation.
- The `encoded-loop` corpus entry (~1 MB) and its per-render output are the largest artifacts;
  BenchmarkDotNet allocation columns will reflect the output size for every engine equally.
- Guarding benchmarks: the eight suites themselves; WI8's run is the recorded baseline for
  future phases.

## Standards compliance

- **DRY where knowledge lives twice:** the verifier check tables exist once in
  `IdiomaticChecks.cs` and are *exported* to JSON, not hand-maintained twice (D10); Fluid and
  DotLiquid keep sharing one Liquid source per workload (existing `LiquidTemplates` pattern).
- **YAGNI applied:** no intra-.NET idiomatic track (D15), no corpus versioning machinery (D7 —
  also a user ruling), no cross-language verifier runner shipped from this phase (each ecosystem
  implements the contract against the exported JSON; a shared runner would be speculative
  abstraction with zero current consumers).
- **Open/closed at the right seam:** `ParityCheck` grows by addition (new `Twins*`/`Assert*`
  wrappers over the existing private helpers, whose signatures are unchanged); benchmark classes
  clone the established host-free shape rather than growing a configurable mega-harness.
- **Tests exempt from DRY:** the five benchmark classes deliberately repeat the
  setup/assert/render pattern per workload — a failing suite must be diagnosable at a glance,
  per the [coding standards](../../common/coding-standards.md).

## Deferred items

| Item | Trigger |
|---|---|
| Intra-.NET idiomatic-track implementations (D15) | .NET being presented as an ecosystem row in phase 7's idiomatic tables, or a user request for a .NET idiomatic comparison |
| Moving the corpus out of `src/Heddle.Performance/GoldenCorpus/` | A phase 2–6 spec demonstrating its harness cannot conveniently consume the current path (then: one ordinary versioned move under D7) |
| A .NET implementation of N5 entity canonicalization | Any intra-.NET engine's default escaping spellings changing on a package upgrade (today all five configured paths are canonical byte-for-byte — D3) |
| quicktemplate whitespace-control confirmation (spike C could not confirm either way) | Phase 6 spec authoring, which owns quicktemplate's conditional inclusion |
| Thymeleaf controlled-track feasibility evidence | Phase 3 spec (sequenced first there by plan); this phase only supplies the D11 policy it will apply |
| Consolidated multi-suite report tooling (auto-generating `index.md` from artifacts) | A second protocol run making the manual copy step demonstrably error-prone |
| Linux (Ubuntu 24.04) cross-check | Phase 8, after phases 2–7 ship (user ruling Q5.2) |

## External references

- TechEmpower FrameworkBenchmarks — Fortunes workload shape and required markup:
  <https://github.com/TechEmpower/FrameworkBenchmarks> (template-only variant discussed in issue
  #10345, never built)
- Marr, Daloze & Mössenböck, *Cross-Language Compiler Benchmarking: Are We Fast Yet?* (DLS
  2016) — controlled-track methodology: <https://github.com/smarr/are-we-fast-yet>
- BenchmarkDotNet docs (statistics, `[MemoryDiagnoser]`, artifacts): <https://benchmarkdotnet.org/>
- Criterion.rs analysis process (estimates and CIs):
  <https://bheisler.github.io/criterion.rs/book/analysis.html>
- JMH (OpenJDK) — benchmark modes and score/error reporting: <https://github.com/openjdk/jmh>
- mitata: <https://github.com/evanwashere/mitata>; pyperf:
  <https://pyperf.readthedocs.io/>; benchstat:
  <https://pkg.go.dev/golang.org/x/perf/cmd/benchstat>
- Handlebars.Net (`HandlebarsConfiguration.TextEncoder`, `ITextEncoder`):
  <https://github.com/Handlebars-Net/Handlebars.Net> — mechanism verified by executed probe D
  (Assumed state)
- Go `html/template` escaping tables and the `+` decision:
  <https://github.com/golang/go/blob/master/src/html/template/html.go>,
  <https://github.com/golang/go/issues/42506>
- Handlebars-JS `escapeExpression` table:
  <https://github.com/handlebars-lang/handlebars.js/blob/master/lib/handlebars/utils.js>
- Askama escaper (`&#x27;`):
  <https://github.com/askama-rs/askama> (`askama_escape`, `impl Escaper for Html`)
- MarkupSafe `_escape_inner` (`&#34;`/`&#39;`):
  <https://github.com/pallets/markupsafe/blob/main/src/markupsafe/_native.py>
- unbescape `escapeHtml4Xml` (Thymeleaf's default text escaping):
  <https://github.com/unbescape/unbescape>
- .NET `Regex` character classes (`\s` is Unicode in .NET) vs RE2/Go (`\s` ASCII):
  <https://learn.microsoft.com/en-us/dotnet/standard/base-types/character-classes-in-regular-expressions>,
  <https://github.com/google/re2/wiki/Syntax>
- `System.Net.WebUtility.HtmlEncode` behavior:
  <https://learn.microsoft.com/en-us/dotnet/api/system.net.webutility.htmlencode> — exact
  spellings pinned by executed probe (spike B) and
  [OutputProfileEncodingTests](../../../../src/Heddle.Tests/OutputProfileEncodingTests.cs)
- Repo conventions bound by this spec:
  [spec-conventions](../../common/spec-conventions.md),
  [testing-standards](../../common/testing-standards.md),
  [coding-standards](../../common/coding-standards.md),
  [cross-cutting-decisions](../../common/cross-cutting-decisions.md)
