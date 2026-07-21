# Phase 5 — python (spec)

## Header

- **Status:** Specified — ready for implementation
- **Source plan:** [phase-5-python.md](../../../plan/phase-5-python.md)
  (standing rulings in the [plan index](../../../plan/README.md); resolved Q&A register in
  [open-questions.md](../../../plan/open-questions.md) — every resolution there is binding on
  this spec; the ones this phase consumes directly: Q5.1, Q5.2, Q2.2 = A, Q6.2, Q2.1, Q1.1,
  Q1.3, Q1.5, Q1.6, Q1.7)
- **Assumes merged:** [Phase 1 — cross-stack-foundation](../phase-1-cross-stack-foundation/README.md)
  in full — the exported golden corpus + manifest + `.verify.json` files under
  `src/Heddle.Performance/GoldenCorpus/`, [parity contract v2](../phase-1-cross-stack-foundation/parity-contract-v2.md),
  the [metrics & publication protocol](../phase-1-cross-stack-foundation/metrics-protocol.md),
  and the published Phase 1 protocol run under `docs/benchmarks/<date>/` (the source of this
  report's Heddle reference row). Harness code may be authored before the corpus lands, but no
  gate can pass and no number may be produced until it has.

| Document | Purpose |
|---|---|
| [README.md](README.md) (this file) | Entry document: assumed state, design decisions, implementation plan, error surface, testing plan |
| [templates.md](templates.md) | Normative template texts and authoring rules for both engines on both tracks, per workload, with the idiomatic track's per-implementation documentation citations |
| [harness.md](harness.md) | The pyperf harness: directory layout, pinned environment, gate runner, exact CLI invocations, the separate tracemalloc memory pass, cold-compile pass, and the report-format instantiation |

## Scope and goal

Deliver the Python leg of the cross-stack survey: **Jinja2 3.1.6** (credibility pick) and
**Mako 1.3.12** (performance pick) rendering all eight Phase 1 workloads on both fairness tracks
(controlled: byte gate against the golden corpus; idiomatic: the machine-checkable functional
verifier), measured with **pyperf 2.10.0** under **CPython 3.14.6** on the protocol machine (the
Windows/Ryzen 9 9950X box, Q1.6), with a **separate tracemalloc pass** producing the Q5.1 memory
metric, published as one immutable date-stamped report under `docs/benchmarks/<date>/` carrying
the labeled wall-time-only Heddle reference row (Q2.2 = A) and the Q5.2 stability-posture
disclosure.

Out of scope (plan non-goals, restated as boundaries only): any third Python engine (Django
templates, Chameleon, Genshi, `string.Template`, minijinja bindings — exclusion rationale lives
in the [plan document](../../../plan/phase-5-python.md#non-goals--scope-boundary), which the
report links); GIL/multi-threaded rendering; PyPy/GraalPy; any change to Phase 1 artifacts, the
Heddle engine, or published reports; any cross-ecosystem table beyond the Heddle reference row;
the Linux cross-check (Phase 8).

## Assumed state

Re-verified first-hand while authoring this spec; nothing below is trusted from the plan or the
research dossier alone. Evidence: spike C (cross-language escaper research), spike E (harness
research), and three **executed probes run for this spec** on the target OS (Windows 11) at the
pinned package versions — probe F (Mako/Jinja2 whitespace semantics + escaper spellings), probe
G (controlled-track authoring dry run through the N1–N5 pipeline against oracle-shaped strings),
probe H (partial/include mechanisms, both engines, both tracks).

| Seam | Verified state |
|---|---|
| Phase 1 artifacts this phase consumes | Corpus format/location, manifest fields, `.verify.json` schema, N1–N5 pipeline, untrusted-data alphabet, gates, exclusion policy, statistic mapping (pyperf → `mean`, std dev alongside), presentation rules, publication format — all as written in the Phase 1 spec documents *(read)*. The corpus directory does not exist yet at authoring time (Phase 1 is specified, not yet implemented); every path this spec cites is the Phase 1 spec's normative path |
| Jinja2 3.1.6 standalone defaults | `Environment(autoescape=False)` is the documented default ([API docs](https://jinja.palletsprojects.com/en/stable/api/)) and **probe F executed it**: default environment renders `&<>"'` untouched. `trim_blocks=False`, `lstrip_blocks=False`, `keep_trailing_newline=False` defaults confirmed against the same page |
| Jinja2 autoescape spellings | `Environment(autoescape=True)` (MarkupSafe 3.0.3) emits `&amp; &lt; &gt; &#34; &#39;` — **probe F executed**; `"` → `&#34;` diverges from the canonical `&quot;`, `'` → `&#39;` is already canonical; BMP ≥ U+0100 (`こんにちは`) passes through intact. Matches spike C item 7 (`markupsafe/_native.py` `_escape_inner`) |
| Mako 1.3.12 standalone default | No escaping: `default_filters=None` is internally `["str"]` ([filtering docs](https://docs.makotemplates.org/en/latest/filtering.html)) and **probe F executed it**: default `Template` renders `&<>"'` untouched |
| Mako escaping path | The `h` filter is `markupsafe.escape` (same docs page) — **probe F executed**: identical spellings to Jinja2 (`&#34;`/`&#39;` family); `default_filters=["h"]` applies engine-wide including through `<%include>` (probe H); `${x \| n}` bypasses it; `markupsafe.escape(42)` → `'42'` (ints safe under `h`, probe G) |
| No spelling configuration in either engine | Jinja2/MarkupSafe and Mako expose autoescape/filter on-off switches only — no hook to change the entity table (spike C items 7–8, re-checked against the API/filtering doc pages above). N5 must therefore be implemented in this phase's gate runner (D4) |
| Mako whitespace semantics (plan-flagged assumption — **now verified by execution**, probe F/G) | (1) A `%` control line **consumes its entire line including its newline** — control lines contribute zero bytes to output. The syntax docs state only that `%` "can appear anywhere on the line as long as no text precedes it; indentation is not significant"; the newline-consumption half is not spelled out in one doc sentence, so it is pinned here **by executed probe** at Mako 1.3.12. (2) Trailing `\` consumes the newline (documented + executed). (3) `<%def>`, `<%include>`, `<% %>` **tags do not consume their trailing newline** — it survives to output (probe F/H). (4) Literal `\r\n` in a template file passes through into output (probe F) — template files must be committed LF |
| Controlled authoring feasibility | **Probe G executed the full dry run**: a Jinja2 template in the D3 inline style and a Mako template in the D3 `%`-line style both render a conditional-heavy-shaped fragment that byte-equals the oracle shape after N1–N4 — the Mako output is byte-identical even **before** normalization. The encoded mini-dry-run (both engines, the encoded-loop row shape incl. the attribute position and `こんにちは`) byte-equals the canonical oracle shape after N5 |
| Partial mechanisms (fragment-heavy) | **Probe H executed**: Jinja2 `{% include %}` inside `{% for %}` sees the loop-local (`item`) — included templates access the active context; Mako requires explicit args: `<%include file="tile.mako" args="item=item"/>` + `<%page args="item"/>` in the partial (the `%for` loop variable is a generated-code local, **not** a context member — a plain `<%include>` cannot see it). Idiomatic variants verified: Jinja2 `{% from "tile" import tile %}` macro; Mako `<%namespace>` def import |
| pyperf 2.10.0 on Windows | No Windows `pyperf system tune` exists; the documented Windows accommodations are automatic `REALTIME_PRIORITY_CLASS` for workers and `--affinity` CPU pinning ("Even if no CPU is isolated, CPU pinning makes benchmarks more stable") — [system docs](https://pyperf.readthedocs.io/en/latest/system.html), spike E. **Source-read for this spec** (pyperf 2.10.0 `_cpu_utils.py`): on Windows both `--affinity` and the priority elevation go through **psutil** (`Process.cpu_affinity`, `Process.nice(REALTIME_PRIORITY_CLASS)` with `AccessDenied` swallowed) — psutil is a hard requirement of this harness. **Executed smoke run on this machine**: a pyperf `Runner` script with `--affinity=4 -o out.json` completed; `pyperf stats` reports mean ± std dev, median ± MAD, and percentiles |
| pyperf run shape defaults | `Runner(values=3, warmups=1, processes=20, loops=0, min_time=0.1)` — [API docs](https://pyperf.readthedocs.io/en/latest/api.html); loops auto-calibrated so a raw value takes ≥ 100 ms ([runner CLI docs](https://pyperf.readthedocs.io/en/latest/runner.html)). `bench_func` **rejects keyword arguments** (executed: `TypeError: unexpected keyword argument`) — render calls are registered as positional-arg callables. Timing is normalized per loop iteration (outer loops × `inner_loops`) |
| Timing and memory are separate passes | pyperf `--track-memory` is documented as "use the maximum RSS memory of the command **instead of** the time"; `--tracemalloc` records `tracemalloc_peak` metadata; tracemalloc instrumentation carries documented CPython-side overhead — so the memory pass must not share an invocation with the timing pass (spike E §4; CPython [tracemalloc docs](https://docs.python.org/3/library/tracemalloc.html)) |
| Version pins current at authoring | CPython 3.14.6 (current stable), Jinja2 3.1.6, Mako 1.3.12, pyperf 2.10.0 (all PyPI-verified, spike E); MarkupSafe 3.0.3 and psutil 7.2.2 verified as the current resolved transitive/support pins by installing into the probe venv on this machine |
| Repo layout | No top-level `benchmarks/` directory exists *(listed)*; `src/` is the .NET solution surface; `docs/benchmarks/<date>/` holds published reports (2026-07-11, 2026-07-18). Phase 1's D6 kept the corpus under `src/Heddle.Performance/GoldenCorpus/` and recorded a top-level-directory revisit trigger |

## Design decisions

Every plan lean, pin, and spec-territory delegation for this phase, closed. Workload-level
template texts are normative in [templates.md](templates.md); harness mechanics in
[harness.md](harness.md).

### D1 — Harness location: new top-level `benchmarks/python/`
- **Decision.** All Phase 5 code and templates live under **`benchmarks/python/`** (layout in
  [harness.md](harness.md#directory-layout)). This creates the top-level `benchmarks/` directory
  as the cross-phase convention for ecosystem harnesses (phases 2–6: `benchmarks/<ecosystem>/`).
  The golden corpus **stays** at `src/Heddle.Performance/GoldenCorpus/`; the Python gate runner
  reads it by repo-relative path exactly as Phase 1's corpus spec prescribes.
- **Rationale.** A Python harness cannot live inside the .NET solution surface (`src/` is
  project-per-directory .NET; a venv, requirements file, and `.py` sources there would pollute
  solution tooling), and `docs/` is publication-only. Phase 1 deliberately deferred the
  top-level-directory decision to the first ecosystem phase that needed it; this is that phase.
  The choice is reversible (moving a directory of scripts is one ordinary versioned change) and
  does not move the corpus, so Phase 1's D6 trigger ("a harness cannot conveniently consume the
  current path") is explicitly **not** fired — probe-verified `pathlib` access to
  `src/Heddle.Performance/GoldenCorpus/` from any repo path is trivial.
- **Alternatives rejected.** `src/Heddle.Performance/python/` (mixes ecosystems into the .NET
  project directory; solution tooling and `.csproj` globbing would see it);
  `docs/benchmarks/python/` (docs tree is immutable published output, not code);
  per-phase scattered locations (Phase 7 needs one predictable root to cite reproduce commands
  from).
- **Grounding.** Repo layout (Assumed state); [Phase 1 D6](../phase-1-cross-stack-foundation/README.md#d6--the-corpus-stores-the-normalized-oracle-under-srcheddleperformancegoldencorpus)
  and its deferred-items trigger.

### D2 — Environment pins and the venv/requirements story
- **Decision.** The run executes on **CPython 3.14.6, 64-bit, python.org Windows installer**, in
  a venv at `benchmarks/python/.venv` (git-ignored) created by `py -3.14 -m venv .venv`, with
  **`benchmarks/python/requirements.txt`** pinning exactly:
  `Jinja2==3.1.6`, `Mako==1.3.12`, `MarkupSafe==3.0.3`, `pyperf==2.10.0`, `psutil==7.2.2`.
  No other packages. The report's environment block records `python -VV`, `pip freeze` output,
  the OS build, CPU model, and repo commit, per the protocol.
- **Rationale.** All five pins verified current (Assumed state). psutil is not optional on this
  platform: pyperf's Windows `--affinity` and `REALTIME_PRIORITY_CLASS` paths import it
  (source-read, Assumed state). Exact `==` pins + committed requirements file are what make the
  published reproduce command deterministic at the published commit (plan success criterion).
- **Alternatives rejected.** CPython 3.13.x (in bugfix support but not the current stable; the
  plan's audience-relatability argument wants the current interpreter); hash-pinned
  `--require-hashes` requirements (adds ceremony the .NET side doesn't carry; version pins +
  recorded `pip freeze` give the same auditability here); conda/uv environments (introduces a
  second toolchain to document; stdlib venv is the CPython-documented default).
- **Grounding.** Spike E §4–5 (PyPI/endoflife primary checks); probe venv install on this
  machine; pyperf `_cpu_utils.py` source read.

### D3 — Controlled-track authoring rules, pinned per engine
- **Decision.** Controlled templates are authored in exactly one style per engine, normative
  texts in [templates.md](templates.md):
  - **Jinja2** — all control tags (`{% %}`) and expressions written **inline** in the literal
    HTML, exactly mirroring the Phase 1 Liquid twin bodies; line breaks appear **only in
    inter-tag positions** (between a `>` and the next `<`); the environment is constructed with
    the **library defaults pinned explicitly in code**: `trim_blocks=False`,
    `lstrip_blocks=False`, `keep_trailing_newline=False`; the `{%- -%}` / `{%+ +%}` markers are
    **not used** anywhere. Rationale: with inline authoring nothing needs trimming — every
    whitespace byte the template can emit sits between tags, where N3 collapses it; pinning the
    defaults in code makes the fairness configuration visible and immune to future library
    default changes.
  - **Mako** — `%` **control lines** for all control flow (each on its own line — they
    contribute zero output bytes, verified); literal/content lines end with a trailing `\`
    wherever the line break would otherwise land **inside element text** (backslash consumes
    the newline, documented + verified); `<%include>`/`<%page>`/`<%def>` tags are followed by
    `\` when the byte after them must not be a newline (these tags do **not** consume their
    trailing newline, probe-verified); `<% %>` Python blocks are not used in controlled
    templates (same newline leak, and no workload needs them).
  - **Both** — template files are committed with `.gitattributes` rules
    `benchmarks/python/templates/** text eol=lf` (Mako passes a literal `\r\n` through to
    output, probe F; N2 would erase it, but LF-pinning keeps the checked-in bytes equal to the
    authored bytes on every platform).
- **Rationale.** Probe G executed both styles through the N1–N5 pipeline against oracle-shaped
  strings and both pass; the Mako style is byte-identical to the oracle even before
  normalization, which makes gate failures maximally diagnostic. Byte-gate risk was assessed
  low by the plan; these two styles close it.
- **Alternatives rejected.** Jinja2 `trim_blocks=True`/`lstrip_blocks=True` or `{%- -%}`
  authoring (unneeded once tags are inline; two whitespace mechanisms in one suite invite
  authoring drift between workloads); Mako inline expression-conditionals
  (`${'x' if c else 'y'}` — moves control flow out of template constructs and into Python
  expressions, breaking the equivalently-authored requirement); Mako `<% %>` code blocks
  (newline leak, probe F).
- **Grounding.** Probes F/G (executed, Assumed state); Jinja2 template-designer
  [whitespace-control docs](https://jinja.palletsprojects.com/en/stable/templates/#whitespace-control);
  Mako [syntax docs](https://docs.makotemplates.org/en/latest/syntax.html);
  [parity-contract-v2 normalization pipeline](../phase-1-cross-stack-foundation/parity-contract-v2.md#normalization-pipeline).

### D4 — Raw suites run both engines' untouched non-escaping defaults; encoded suite pins one engine-level escaping configuration each
- **Decision.**
  - **Raw suites (workloads 1–6, both tracks):** Jinja2 runs a plain `Environment` with
    `autoescape=False` (the standalone default, pinned explicitly in code); Mako runs
    `TemplateLookup` with `default_filters` left at its `None` default (internally `["str"]`).
    **No escape-bypass syntax appears in any raw template** (`|safe`, `Markup`, `${x | n}` are
    all absent). The report states this fairness property in the sentence pinned in
    [harness.md](harness.md#report-format-instantiation): both raw-track ports run each engine's
    untouched default output path — no bypass configuration exists to disclose.
  - **Encoded suite (workloads 7–8, both tracks):** Jinja2 runs a **separate**
    `Environment(autoescape=True)` (controlled track; the idiomatic track uses
    `select_autoescape` — D5); Mako runs a **separate** `TemplateLookup(default_filters=["h"])`
    (controlled track; the idiomatic track uses `<%page expression_filter="h"/>` — D5). Both are
    engine-level configurations of the engines' own documented escaping paths; no per-expression
    escape filter appears in controlled encoded templates, mirroring how the Handlebars.Net twin
    escapes via engine configuration rather than template syntax.
  - **Entity-spelling reconciliation (Q1.1):** neither engine offers spelling configuration
    (Assumed state), so per the Q1.1 resolution's ordering — configuration where offered,
    normalization otherwise — this phase's gate runner **implements contract v2's N5**
    canonicalization for encoded-suite candidates. Concretely the only N5 rewrite these engines
    trigger is `&#34;` → `&quot;` (MarkupSafe's `"` spelling); `&#39;` is already canonical.
    The runner implements the full closed N5 table regardless (contract conformance, and it
    protects against a future MarkupSafe spelling change).
- **Rationale.** The plan's "fairness property no other ecosystem gets for free" claim is now
  verified fact, not dossier assumption: both defaults executed (probe F) and both documented
  (Jinja2 API docs: `autoescape` default `False`; Mako filtering docs: default `["str"]`).
  Engine-level escaping config keeps encoded templates structurally identical to raw templates
  except for the two contexts' content, so the encoded suite measures escaping, not template
  divergence. Probe G validated both escapers against the Phase 1 untrusted-data-alphabet
  payloads (XSS string, Japanese runs, all five specials incl. the attribute position) through
  N5 to canonical-oracle equality.
- **Alternatives rejected.** Per-expression `| e` (Jinja2) / `${x | h}` (Mako) in controlled
  encoded templates (template-syntax asymmetry vs the engine-level Jinja2 autoescape; also
  easier to misauthor — one missed filter is exactly the plan's validation scenario);
  normalizing `&#39;` → some other canonical (contract fixes the canonical set; not this
  phase's call); a custom Mako filter emitting `&quot;` directly (would swap
  `markupsafe.escape` for bespoke code and stop measuring Mako's real escaping path).
- **Grounding.** Probes F/G (executed);
  [Jinja2 API docs](https://jinja.palletsprojects.com/en/stable/api/#autoescaping),
  [Mako filtering docs](https://docs.makotemplates.org/en/latest/filtering.html);
  [contract v2 N5](../phase-1-cross-stack-foundation/parity-contract-v2.md#entity-canonicalization-n5)
  and rule 3 (phases whose engines have no spelling configuration implement N5 in their gate
  runner — this phase is a named instance);
  [open-questions Q1.1](../../../plan/open-questions.md).

### D5 — Idiomatic track: constructs and doc citations pinned per engine (Q1.7)
- **Decision.** Idiomatic implementations are authored in-repo from official documentation
  patterns, one shared long-lived engine object per engine (Jinja2: one `Environment` with
  `FileSystemLoader` and `select_autoescape()`, encoded templates named `<id>.html` so
  autoescaping engages by extension, raw templates named `<id>.jinja` so it does not; Mako: one
  `TemplateLookup(directories=[...])`, encoded templates carrying
  `<%page expression_filter="h"/>`). Layout-shaped workloads use each engine's inheritance
  mechanism (`{% extends %}` / `<%inherit>`), the repeated tile uses the documented reusable
  unit (Jinja2 macro via `{% from ... import %}`; Mako `<%def>` via `<%namespace>`). Whitespace
  is authored naturally (indented, multi-line) — the idiomatic gate is the functional verifier,
  which normalizes. The full per-workload construct table **with the doc page cited per
  implementation** (the Q1.7 evidence discipline; the citations also go into each template
  file's header comment per contract v2's idiomatic authoring standard) is in
  [templates.md](templates.md#idiomatic-track).
- **Rationale.** Q1.7 resolution verbatim, made concrete: every construct in the table is the
  one its engine's own docs present for that job, and every mechanism was executed (probe H:
  Jinja2 macro import, Mako namespace-def import, Mako inherit; probe F: `<%inherit>`;
  select_autoescape is the API docs' "recommended way to configure autoescaping").
- **Alternatives rejected.** Reusing controlled templates with relaxed whitespace (measures the
  same authoring twice — the idiomatic track exists to measure the practitioner experience);
  importing simonw-study or other third-party templates (barred by Q1.7).
- **Grounding.** Probe H (executed); doc citations per workload in
  [templates.md](templates.md#idiomatic-track);
  [contract v2 idiomatic gate](../phase-1-cross-stack-foundation/parity-contract-v2.md#idiomatic-track-gate);
  [open-questions Q1.7](../../../plan/open-questions.md).

### D6 — Model data is regenerated in Python from the pinned formulas; the byte gate is the transcription check
- **Decision.** `benchmarks/python/runner/data.py` constructs all eight workloads' models as
  plain `dict`s with the Phase 1 snake_case keys, module-level (materialized once per process):
  workloads 4–8 from the exact generation formulas pinned in
  [workloads.md](../phase-1-cross-stack-foundation/workloads.md); the three anchors by
  transcription from the existing C# sources
  ([SubstitutionContent.cs](../../../../src/Heddle.Performance/Runners/SubstitutionContent.cs),
  [LoopContent.cs](../../../../src/Heddle.Performance/Runners/LoopContent.cs),
  [TwinContent.cs](../../../../src/Heddle.Performance/Runners/TwinContent.cs) +
  `AreaComponent.Areas` in
  [AreaComponent.cs](../../../../src/Heddle.Performance/TestSuite/Extensions/AreaComponent.cs)).
  Composed-page fragments (sections, components, the seven ordered area fragments) are
  transcribed as Python string constants. No numeric formatting is needed for any model value:
  the numeric fields (e.g. `Price`) are ints and render via `str(int)` (invariant by
  construction), and the one field that looks numeric but isn't —
  `SubstitutionContent.Rating` — is itself a pinned string literal (`"4.8"`) transcribed
  verbatim, not a formatted float.
- **Rationale.** This is the same posture the .NET twins take (TwinContent doc comment: parity
  is enforced by the gate, not by trusting transcription) — any transcription error fails the
  controlled byte gate loudly before any timing, and the idiomatic verifier's exact-count
  checks catch it on the idiomatic track too.
- **Alternatives rejected.** Exporting model data from .NET as JSON (a new Phase 1 artifact —
  out of scope for this phase and unnecessary given the gate); computing the composed-page
  fragments by scraping the golden file (circular — the gate would then verify the oracle
  against itself).
- **Grounding.** [workloads.md](../phase-1-cross-stack-foundation/workloads.md) pinned models;
  `TwinContent.cs` *(read)*; gate mechanics in
  [contract v2](../phase-1-cross-stack-foundation/parity-contract-v2.md#controlled-track-gate).

### D7 — Gate runner: contract v2 + manifest verification + security floor, executed inside every pyperf worker
- **Decision.** `benchmarks/python/runner/gates.py` implements: N1 (strict UTF-8 decode, BOM
  survives), N2–N4 with whitespace = the contract's closed six-character set, plus the N3b
  whitespace-run removal (to nothing, applied to both sides at comparison — the 2026-07-20
  maintainer step, so any whitespace-only divergence, run-length or presence/absence, passes),
  N5 as the full closed replacement table (encoded suites
  only), SHA-256 verification of each consumed corpus
  file against `manifest.json` (mismatch = abort: the checkout is corrupt or stale), byte
  comparison with first-diff index + ±40-char excerpt on failure (the `Describe` shape), and
  the encoded-suite security floor (raw `<script>alert(` zero times in un-normalized output;
  escaped form present the expected number of times). Each pyperf bench script runs the gate
  for **all cells it registers, at module top level, before any `bench_func` registration** —
  pyperf re-executes the script in every worker process, so the gate provably runs in the same
  process that produces every timed number (contract: "in the same process/invocation", the
  Python analogue of the intra-.NET `[GlobalSetup]` assert). A failed gate raises `SystemExit(1)`
  before any benchmark registers; no numbers can be emitted for that suite. Gate cost is
  setup-time only (one render + compare per cell per worker; the largest, encoded-loop, is a
  ~1 MB render — tens of milliseconds, outside all timed regions).
- **Rationale.** Contract conformance with zero trust in process reuse; the redundant gating in
  every worker is the cheapest mechanism that satisfies the contract's wording exactly.
- **Alternatives rejected.** Gating once in the parent process only (workers are fresh
  processes; the contract's "same process" wording would be satisfied only by convention, not
  construction); gating lazily inside the timed callable (contaminates timing); skipping
  manifest hash verification (a smudged/stale corpus checkout would gate against wrong bytes
  and mis-blame the engines).
- **Grounding.** [contract v2 controlled gate](../phase-1-cross-stack-foundation/parity-contract-v2.md#controlled-track-gate)
  items 2–5; pyperf worker model (runner docs; observed in the executed smoke run's
  per-process spawns).

### D8 — pyperf run shape: five Runner scripts, library defaults, `--affinity=4`, JSON outputs
- **Decision.** Five pyperf `Runner` scripts (one per engine × track for render timing, plus one
  cold-compile script — D10): `bench_jinja2_controlled.py`, `bench_jinja2_idiomatic.py`,
  `bench_mako_controlled.py`, `bench_mako_idiomatic.py`, `bench_cold_compile.py`. Benchmark
  names are `<engine>/<track>/<workload-id>` (cold pass: `<engine>/cold-compile/<workload-id>`).
  Run shape is **pyperf's CPython defaults, pinned as the decision**: 20 processes × 3 values ×
  1 warmup, loops auto-calibrated to ≥ 100 ms per raw value (`min_time` default). Each
  invocation runs from an **elevated PowerShell** (so psutil's `REALTIME_PRIORITY_CLASS`
  elevation is actually granted rather than silently downgraded) with
  **`--affinity=4`** and `-o results/<script>.json`. Render callables are registered
  positional-only (`runner.bench_func(name, template_render, context_dict)` — kwargs are
  rejected, executed evidence). One render = one full workload output string from the cached
  template, model prebuilt at module level — exactly the protocol's metric-rule definition.
  Affinity rationale: the 9950X exposes logical CPUs 0–31 with SMT siblings enumerated in
  adjacent pairs (2n, 2n+1); CPU 4 is the first thread of physical core 2 on CCD0 — off core 0
  (system/interrupt traffic), one fixed CCD (no cross-CCD cache migration), one logical CPU (the
  worker is single-threaded). *(Verify at implementation: confirm with Sysinternals `coreinfo`
  that logical CPUs 4/5 are SMT siblings of one physical core on CCD0 on this box; if the
  enumeration differs, substitute the first logical CPU of the third physical core and record
  the substitution in the report's environment block.)*
- **Rationale.** Q2.1's ruling keeps each harness native — pyperf's default shape (60 values
  from 20 fresh processes) *is* the harness-standard methodology the protocol's credibility
  argument rests on; `--affinity` is pyperf's own documented no-isolation stability measure
  (Q5.2's "concrete stability settings"). The executed smoke run proves the exact flag set
  works end-to-end on this OS.
- **Alternatives rejected.** `--rigorous` (40 processes) as the default (doubles run time;
  adopted only under the D9 disclosure posture's documented trigger, never silently);
  `--fast` (fewer values — wrong direction on the noisiest machine in the program); one
  mega-script for all 32 render cells (every worker would gate all 32 cells — 8× the setup
  waste — and a single engine's failure would block the other's numbers); custom
  `add_cmdline_args` plumbing to select engine/track (five plain scripts are simpler and each
  is independently re-runnable).
- **Grounding.** [Runner CLI docs](https://pyperf.readthedocs.io/en/latest/runner.html) (defaults,
  `--affinity`, `-o`); executed smoke run (Assumed state); pyperf `_cpu_utils.py` source read
  (psutil path, `AccessDenied` swallowed — hence the elevated shell);
  [metrics-protocol metric rule 1](../phase-1-cross-stack-foundation/metrics-protocol.md#metric-rules).

### D9 — Stability posture and dispersion reporting (Q5.2, Q2.1)
- **Decision.** One measurement pass at the D8 settings. The report's environment block carries
  the **stability-posture disclosure text pinned verbatim in
  [harness.md](harness.md#report-format-instantiation)**: Windows has no pyperf system-tune
  equivalent; settings applied are REALTIME priority + single-CPU affinity; dispersion must be
  read before precision is assumed. Every published number carries pyperf's dispersion: the
  results tables print **mean ± std dev** (the Q2.1-mapped statistic pair) **and median**, per
  benchmark, extracted from the JSON via `pyperf stats` (the simonw-study mean/median/std
  transparency model, which the plan names as this report's model). Any pyperf instability
  warning ("the benchmark result may be unstable") emitted for a published benchmark is quoted
  in the report next to that benchmark's row — disclosed, never suppressed, and **no selective
  re-running until favorable** (a re-run happens only for a whole suite, only for a stated
  operational reason — e.g. a background process discovered mid-run — and the report says so).
  Documented escalation trigger: if more than half the benchmarks in any one suite carry the
  instability warning, the suite is re-run once with `--rigorous` and **that** run is published
  (with the setting recorded); the original is discarded whole, not cherry-picked.
- **Rationale.** Q5.2's resolution verbatim (accept the machine, disclose posture + dispersion);
  the plan's validation scenario demands disclosure-not-suppression; a whole-suite re-run rule
  with a pre-committed trigger is what keeps an unstable-run response from becoming selective
  re-measurement.
- **Alternatives rejected.** Publishing best-of-N runs (silent re-run-until-favorable —
  forbidden); omitting median (the plan explicitly names mean/median/std as the model).
- **Grounding.** [open-questions Q5.2](../../../plan/open-questions.md); executed `pyperf stats`
  output shape (Assumed state); plan §Risks row 1 and validation scenarios.

### D10 — Cold parse/compile pass (Q1.3): per-ecosystem, non-comparable, both engines
- **Decision.** `bench_cold_compile.py` measures, per workload, per engine, the cold
  template-construction cost: Jinja2 `Environment.from_string(source)` (parse + compile to
  Python bytecode, no bytecode cache configured) and Mako `Template(text=source)` (parse +
  compile to a Python module, in-memory, no `module_directory`), each on the controlled-track
  source text loaded into a string beforehand. Results appear in the report in their own
  per-ecosystem section labeled non-comparable (Q1.3 verbatim), never beside the Heddle row.
- **Rationale.** The plan explicitly routes "Mako's module compilation and Jinja2's bytecode
  compilation" to per-ecosystem reporting under Q1.3; measuring construction-from-string is the
  operation both engines define identically.
- **Alternatives rejected.** Disk-cache-inclusive variants (`module_directory`, Jinja2 bytecode
  cache — adds filesystem noise and measures deployment configuration, not the engine);
  skipping the cold pass (the plan names it as reported content).
- **Grounding.** [open-questions Q1.3](../../../plan/open-questions.md);
  [Mako usage docs](https://docs.makotemplates.org/en/latest/usage.html);
  [Jinja2 API docs](https://jinja.palletsprojects.com/en/stable/api/).

### D11 — Memory metric (Q5.1): tracemalloc single-render boundaries, separate pass
- **Decision.** A standalone script `mem_tracemalloc.py` (never pyperf) produces the Q5.1
  headline figure. Per engine × track × workload cell it spawns **one fresh child process**
  which: builds the engine object, template, and model; performs **one untimed warm-up render**
  (populates engine caches) that is gated exactly as the bench scripts' warm-up gate is (D7:
  `assert_parity` on the controlled track, `assert_verified` on the idiomatic track — before
  any tracing starts, so a corrupted template can never enter the traced measurement loop);
  calls `gc.collect()`; `tracemalloc.start()`; then runs **R = 100
  measured repetitions**, each repetition being exactly:
  `gc.collect()` → `base = tracemalloc.get_traced_memory()[0]` → `tracemalloc.reset_peak()` →
  `out = render()` → `(cur, peak) = tracemalloc.get_traced_memory()` → record
  `allocated_i = peak − base` and `retained_i = cur − base` → `del out`.
  The **headline "allocated bytes per render (peak live)" = mean of `allocated_i`**, published with median
  and std dev over the 100 repetitions; `retained_i` (≈ the output string) is published as a
  secondary column for context. Results go to `results/memory.json` and the report's
  Python-only memory table, which carries the protocol's verbatim allocation label, the
  explicit not-cross-comparable statement, and the **method note pinned verbatim in
  [harness.md](harness.md#memory-pass)** (stating precisely: tracemalloc-traced Python
  allocations only; per-render high-water delta over a pre-render baseline; not allocator
  throughput, not RSS).
- **Rationale.** Q5.1 resolution delegates exact instrumentation to this spec. tracemalloc
  tracks *live* traced allocations (current + peak); a cumulative allocated-bytes counter does
  not exist in CPython's tracemalloc API — so the closest honest realization of
  "allocated-bytes-per-render" is the traced high-water mark one render creates above its own
  baseline, measured with single-render boundaries. Single-render boundaries (rather than N
  renders divided by N) are load-bearing: intermediates freed inside a render mean peak does
  **not** scale with N, so an N-render peak divided by N would systematically understate — the
  per-render division in this design is division by exactly 1, by construction. The separate
  pass is architecturally required (Assumed state: `--track-memory` replaces time; tracemalloc
  overhead is CPython-documented) — no timing number is ever produced with tracemalloc active.
- **Alternatives rejected.** pyperf `--track-memory` (max RSS of the whole process — a
  process-peak figure, not per-render, and it replaces timing; Q5.1 option B, rejected by
  ruling); pyperf `--tracemalloc` (records `tracemalloc_peak` process-wide metadata — same
  per-render attribution problem); snapshot `compare_to` diffing (measures net retained only —
  misses the transient churn that *is* the render cost); N-renders-divided-by-N (understates,
  above).
- **Grounding.** [open-questions Q5.1](../../../plan/open-questions.md); CPython
  [tracemalloc docs](https://docs.python.org/3/library/tracemalloc.html)
  (`get_traced_memory` = current **and peak** size of traced blocks; `reset_peak`); pyperf
  [CLI docs](https://pyperf.readthedocs.io/en/latest/cli.html) (`--track-memory` semantics);
  spike E §4.

### D12 — Idiomatic verifier and self-calibration in Python
- **Decision.** `benchmarks/python/runner/verify.py` implements the contract's idiomatic gate
  exactly (N1–N4 (+N5 encoded) normalize; `values` exact counts; `markers` strictly ordered;
  `forbidden` zero in raw **and** normalized output; `required` min-counts; failure reports
  kind + entry + expected/found), driven by the Phase 1 `<id>.verify.json` files. A
  `python -m runner.selftest` command re-runs the Phase 1 calibration in Python: each verifier
  must accept its golden corpus entry and reject the same synthesized corruptions Phase 1
  defines (two per raw workload, three per encoded workload), proving the Python
  implementation is conformant before it gates anything.
- **Rationale.** The verifier is this phase's only idiomatic gate; a non-conformant
  reimplementation would silently weaken it. Reusing the corruption recipes gives an executable
  conformance proof against committed artifacts, with no new fixtures.
- **Alternatives rejected.** Trusting the reimplementation without calibration (the exact
  failure mode Phase 1's calibration deliverable exists to prevent); invoking the .NET
  `verify-corpus` from Python (checks the C# implementation, not this one).
- **Grounding.** [contract v2 idiomatic gate](../phase-1-cross-stack-foundation/parity-contract-v2.md#idiomatic-track-gate);
  [golden-corpus verification](../phase-1-cross-stack-foundation/golden-corpus.md#verification).

### D13 — Report instantiation (Q2.2 = A, Q6.2, honest-reporting rules)
- **Decision.** One new `docs/benchmarks/<yyyy-MM-dd>/` directory in the protocol's exact
  `index.md` shape, with the Python specifics pinned in
  [harness.md](harness.md#report-format-instantiation): the labeled **Heddle reference row**
  per workload (excerpted from the published Phase 1 protocol run, label format per protocol
  rule 1, dashes in all non-wall-time cells); wall-time tables print pyperf's native µs/ms
  **and** ns per render, with the **ratio column anchored to the Heddle row** (Q6.2); tables
  are track-labeled; **Jinja2 is the within-ecosystem baseline for all non-cross-comparable
  metrics** (memory, cold compile — Phase 1 D13's credibility-pick rule); the verbatim
  allocation label and encoded confinement caveat appear at their protocol-mandated positions;
  the memory and cold-compile sections are Python-only and labeled non-comparable; the
  stability-posture disclosure (D9) sits in the environment block; the **Phase 8 forward note**
  (pinned text in harness.md) states the later Ubuntu 24.04 cross-check on the same machine is
  published separately and never merged or cross-compared; the results narrative opens with
  the cross-runtime framing sentence (AOT-compiled .NET engine vs interpreted CPython engines —
  a cross-runtime gap measurement, not an engine craftsmanship contest), reports every
  Python-narrowing or inversion (Mako-vs-Jinja2 internal ranking, per-workload shape effects,
  bulk-output amortization on `large-loop`/`encoded-loop`) as prominently as any Heddle win,
  and links the plan document for the Django/legacy/native-extension exclusion rationale
  rather than re-arguing it.
- **Rationale.** Q2.2 = A and Q6.2 resolutions verbatim; the framing duties are plan success
  criteria this spec turns into a report checklist (testing plan).
- **Alternatives rejected.** Re-measuring Heddle for the reference row (protocol rule 1: an
  excerpt, never a re-measurement); Mako as non-comparable-metric baseline (Phase 1 D13 fixed
  the credibility pick program-wide).
- **Grounding.** [metrics-protocol presentation rules](../phase-1-cross-stack-foundation/metrics-protocol.md#presentation-rules-q22--a-q62)
  and publication format; [open-questions Q2.2, Q6.2](../../../plan/open-questions.md);
  plan §Design direction (framing duty) and success criteria.

## Implementation plan

Ordered; exact paths; each item's completion check is executable. Template texts and per-file
details are normative in [templates.md](templates.md) and [harness.md](harness.md).

### WI1 — Harness skeleton, environment, data module
- **Files.** New: `benchmarks/python/requirements.txt`, `benchmarks/python/README.md` (pointer
  to this spec + setup/run commands), `benchmarks/python/runner/__init__.py`,
  `benchmarks/python/runner/data.py`; changed: `.gitignore` (add `benchmarks/python/.venv/`,
  `benchmarks/python/results/`), `.gitattributes` (add
  `benchmarks/python/templates/** text eol=lf`).
- **Change.** D1/D2/D6 as specified; `data.py` exposes `MODELS: dict[str, dict]` keyed by
  workload id.
- **Done when.** `py -3.14 -m venv .venv` + `pip install -r requirements.txt` succeeds;
  `python -c "from runner.data import MODELS; print(sorted(MODELS))"` lists the eight ids;
  spot-counts match workloads.md (36 products, 200 rows, 48 tiles, 12 fortunes, 5000 encoded
  rows).

### WI2 — Gate runner and verifier
- **Files.** New: `benchmarks/python/runner/gates.py`, `benchmarks/python/runner/verify.py`,
  `benchmarks/python/runner/selftest.py`, `benchmarks/python/runner/gate_all.py`.
- **Change.** D7/D12 as specified (N1–N5, manifest SHA-256, security floor, verifier, corruption
  calibration); `gate_all.py` is the per-track (`--track controlled|idiomatic`) gate-sweep CLI
  that WI3/WI4/WI5's done-when checks invoke.
- **Done when.** `python -m runner.selftest` exits 0 against the committed Phase 1 corpus: all
  eight verifiers accept their goldens, all corruptions rejected with the correct check kind;
  the N5 unit check canonicalizes the full recognized-spelling table (incl. leading-zero and
  hex-case variants) and nothing else.

### WI3 — Controlled-track templates and engine wiring
- **Files.** New: `benchmarks/python/templates/jinja2/controlled/*` and
  `benchmarks/python/templates/mako/controlled/*` (per [templates.md](templates.md), incl. the
  `layout`/`tile` partials), `benchmarks/python/runner/engines.py` (the four engine-object
  constructors: jinja2-raw, jinja2-encoded, mako-raw, mako-encoded, plus template loading per
  track).
- **Change.** D3/D4 as specified.
- **Done when.** A gate sweep (`python -m runner.gate_all --track controlled`) reports 16/16
  cells PASS (2 engines × 8 workloads) against the corpus; encoded cells additionally pass the
  security floor.

### WI4 — Idiomatic-track templates
- **Files.** New: `benchmarks/python/templates/jinja2/idiomatic/*`,
  `benchmarks/python/templates/mako/idiomatic/*` (per [templates.md](templates.md), with the
  doc-citation header comment in every file).
- **Change.** D5 as specified.
- **Done when.** `python -m runner.gate_all --track idiomatic` reports 16/16 verifier PASS;
  every template file's header comment carries ≥ 1 official doc URL.

### WI5 — pyperf bench scripts
- **Files.** New: `benchmarks/python/bench_jinja2_controlled.py`,
  `bench_jinja2_idiomatic.py`, `bench_mako_controlled.py`, `bench_mako_idiomatic.py`,
  `bench_cold_compile.py`.
- **Change.** D8/D10 as specified: top-level gate asserts, then `bench_func` registrations with
  `<engine>/<track>/<id>` names.
- **Done when.** Each script runs to completion with
  `--affinity=4 -o results/<name>.json` from an elevated shell; `pyperf stats` on each JSON
  shows the expected benchmark names and 60 values each; deliberately corrupting one template
  byte makes the script exit 1 with a first-diff excerpt and produce no JSON.

### WI6 — Memory pass
- **Files.** New: `benchmarks/python/mem_tracemalloc.py`.
- **Change.** D11 as specified; output `results/memory.json` with per-cell
  `{allocated: {mean, median, std}, retained: {mean, median, std}, repetitions: 100}`.
- **Done when.** The script completes for all 32 cells; for `encoded-loop` the retained mean is
  within a few percent of the corpus entry's byte length (sanity anchor); re-running produces
  means within run-to-run noise (no monotonic drift — proves no leak into the measurement
  loop); deliberately corrupting one template byte makes the affected cell's warm-up gate fail
  (exit 1) before tracing starts, mirroring WI5's negative check.

### WI7 — Measurement run and report
- **Files.** New: `benchmarks/python/runner/report_table.py`, `docs/benchmarks/<run-date>/index.md`,
  the five pyperf JSONs, `memory.json`, and the generated results tables (`report_table.py`
  output pasted in).
- **Change.** Execute [harness.md — run protocol](harness.md#run-protocol) end-to-end on the
  protocol machine; author `index.md` per D13.
- **Done when.** The D13/report checklist in the [testing plan](#testing-plan) passes item by
  item; the corpus `generatingCommit` of every gated entry is an ancestor of the published
  run's commit.

### WI8 — Index and cross-links
- **Files.** Changed: `docs/spec/README.md` (master-index row for this phase, per the
  index-maintenance convention).
- **Change.** Additive row only.
- **Done when.** Links resolve from the master index to this folder's three documents.

## Public API / contract

No .NET or Python public API is created: every Python module is an unpublished benchmark
harness (no package metadata, never uploaded). The externally consumed surface is
artifact-shaped:

| Artifact | Consumer | Normative definition |
|---|---|---|
| `results/*.json` (pyperf) + `results/memory.json` | WI7's report; Phase 7 ingestion (wall-time rows only) | [harness.md](harness.md) |
| `docs/benchmarks/<date>/` report | Phase 7 consolidated report; readers | [metrics-protocol](../phase-1-cross-stack-foundation/metrics-protocol.md) + D13 |
| `benchmarks/python/` as reproduction artifact | any reader at the published commit | [harness.md — run protocol](harness.md#run-protocol) |

Thread-safety: not applicable — every script is single-threaded by design (plan non-goal:
GIL/concurrency out of scope); the shared `Environment`/`TemplateLookup` objects are used from
one thread only.

## Diagnostics / error surface

**HED\* compiler diagnostics: n/a** — no compiler, grammar, or engine surface changes; the
registry is untouched.

| Surface | Exit | Message shape | Trigger |
|---|---|---|---|
| Controlled gate (bench script top level / `gate_all`) | `SystemExit(1)`, no JSON written | `GATE FAIL <engine>/<track>/<workload>: first diff at byte <i> (expected <n> bytes, got <m>)` + ±40-char excerpt, `\n`-escaped | normalized candidate bytes ≠ corpus entry |
| Manifest verification | `SystemExit(1)` | `CORPUS FAIL <workload>: sha256 mismatch vs manifest.json — checkout is stale or corrupt` | corpus file hash ≠ manifest hash |
| Security floor | `SystemExit(1)` | `SECURITY FAIL <engine>/<track>/<workload>: raw "<script>alert(" found <M> times (expected 0)` | raw payload present in un-normalized encoded output |
| Idiomatic verifier | `SystemExit(1)` | `VERIFY FAIL <engine>/<workload> <value\|marker\|forbidden\|required>: expected <N> of "<needle>", found <M>` | any verifier check misses |
| `runner.selftest` | exit 1 | `[FAIL] <workload> calibration: corruption '<kind>' was NOT rejected` | a corruption the verifier accepted, or a golden it rejected |
| pyperf instability | exit 0 (warning) | pyperf's own `WARNING: the benchmark result may be unstable` | pyperf statistics check — **disclosed in the report per D9, never suppressed** |
| `mem_tracemalloc.py` cell failure | exit 1 | `MEM FAIL <engine>/<track>/<workload>: <exception>` | child process error, incl. a warm-up-render gate failure (D7/D11) or other exception; no partial memory.json is written |

## Testing plan

**TDD verdict.** The gates are the executable spec and run test-first by construction: WI2's
selftest must pass against the committed Phase 1 corpus before any template exists; WI3/WI4
turn `gate_all` from red to green cell by cell; every bench script re-runs its gates in every
worker. No pytest suite is added — the harness's own gate/selftest commands are its tests, the
same verdict the intra-.NET benchmark project operates under, and this phase adds no code to
any shipped library.

**Named checks.**
- `python -m runner.selftest` — verifier conformance calibration (accept 8 goldens; reject
  2×6 + 3×2 = 18 corruptions with correct kinds).
- `python -m runner.gate_all --track controlled|idiomatic` — 16-cell gate sweeps.
- Negative check (WI5): one-byte template corruption ⇒ exit 1, no JSON.
- Sanity anchor (WI6): encoded-loop retained-mean ≈ corpus byteLength.
- N5 unit check: full recognized-spelling table canonicalizes; sixth-character spellings
  (e.g. `&#43;`) pass through untouched.

**Report checklist (WI7 gate — every item checked against the published index.md).**
1. Environment block: OS build, CPU, CPython 3.14.6, Jinja2 3.1.6 / Mako 1.3.12 /
   MarkupSafe 3.0.3 / pyperf 2.10.0 / psutil 7.2.2, commit, elevated-shell + `--affinity=4` +
   priority note, stability-posture disclosure text (D9), reproduce commands.
2. Heddle reference row present, labeled per protocol, wall-time-only, sourced from the Phase 1
   run's published numbers; ratio column anchored to it (Q6.2); ns/render normalization present.
3. Every table names its track; no controlled/idiomatic mixing.
4. Allocation label verbatim beside all memory data; encoded caveat verbatim beside encoded
   results; memory + cold-compile sections Python-only and labeled non-comparable; memory
   method note verbatim (D11); no table juxtaposes Python memory with another runtime.
5. Raw-track fairness sentence present (D4); zero raw `<script>alert(` in any published
   encoded artifact.
6. Cross-runtime framing sentence opens the narrative; Mako-vs-Jinja2 and shape-effect
   findings in prose; every Python narrowing/inversion named with numbers, as prominently as
   Heddle wins; no universal-superiority claim.
7. Phase 8 forward note present (never merged/cross-compared).
8. Exclusion rationale linked to the plan document, not re-argued.
9. Dispersion (mean ± std, median) present for every published number; any pyperf instability
   warnings quoted (D9).
10. All scripts/templates needed to reproduce are committed at the published commit.

## Back-compat and migration

- **Nothing existing changes behavior.** No .NET code, no Heddle engine surface, no existing
  benchmark, template, or published report is touched. `.gitignore`/`.gitattributes` gain
  additive lines only.
- **New surface:** the top-level `benchmarks/` directory (D1) and the maintained
  reproduction obligation for `benchmarks/python/` at the published commit — the same
  obligation every ecosystem phase carries.
- **Corpus consumed read-only** (Q1.5 posture): if the corpus is regenerated later, the next
  Python run simply re-gates against the current corpus; nothing in this phase pins corpus
  versions.
- **No breaking window engaged** — everything is additive.

## Performance considerations

- The harness measures; it must not distort. All model data and engine/template objects are
  module-level (constructed once per worker, outside timed regions); gates run at setup time
  only; the timed callable is exactly one cached-template render returning the full output
  string.
- tracemalloc never runs in any timing process (D11); `--affinity`/priority settings apply to
  timing workers via pyperf itself.
- The encoded-loop cell renders ~1 MB per call in Python — loop calibration handles it
  (expected single-digit loops per 100 ms value); memory-pass repetitions hold one output alive
  at a time (`del` between reps), bounding the pass at ~1 MB live output.
- Benchmark count: 48 pyperf benchmarks (32 render + 16 cold) × ~21 process spawns each ≈ a
  run in the tens of minutes — accepted; no parallelism is permitted (single pinned CPU by
  design).

## Standards compliance

- **DRY:** normalization, gating, verification, and model data each live in exactly one Python
  module consumed by all five bench scripts and the memory pass; template sources are the only
  deliberate duplication (per-engine authoring is the experiment itself).
- **YAGNI:** no shared cross-ecosystem harness framework is built (each phase implements the
  contract against the exported artifacts — Phase 1's recorded posture); no report-generation
  pipeline beyond one table-printing helper; no hash-pinned pip ceremony (D2).
- **Tests-exempt-from-DRY analogue:** the five bench scripts repeat the
  gate-register-run shape verbatim per engine×track — a failing suite must be diagnosable at a
  glance and independently re-runnable.

## Deferred items / non-goals

| Item | Trigger |
|---|---|
| Third Python engines (Django templates, Chameleon, Genshi, minijinja bindings) | User sign-off reopening the cast (standing ruling); rationale stays in the [plan](../../../plan/phase-5-python.md#non-goals--scope-boundary) |
| Free-threaded CPython / multi-threaded rendering | Out of scope by plan decision (not deferred — recorded here for completeness) |
| PyPy/GraalPy runs | A user request for a runtime comparison; would be a new phase, not an amendment |
| Jinja2 bytecode cache / Mako `module_directory` warm-start variants | A reader/maintainer question about deployment-configured cold costs that D10's in-memory numbers cannot answer |
| `--rigorous` as the standard shape | The D9 escalation trigger firing on a majority of any suite |
| Ubuntu 24.04 re-run of this suite | Phase 8 (user ruling Q5.2) — the harness is authored OS-portably (pathlib, no shell-outs) so Phase 8 reuses it unchanged except the affinity/priority mechanics |

## External references

- Jinja2 API (Environment defaults, autoescape, `select_autoescape`):
  <https://jinja.palletsprojects.com/en/stable/api/>
- Jinja2 template designer docs (whitespace control, inheritance, macros/import, include):
  <https://jinja.palletsprojects.com/en/stable/templates/>
- MarkupSafe `_escape_inner` spellings:
  <https://github.com/pallets/markupsafe/blob/main/src/markupsafe/_native.py>
- Mako syntax (control lines, backslash continuation): <https://docs.makotemplates.org/en/latest/syntax.html>
- Mako filtering (`h` = `markupsafe.escape`, `default_filters`, `expression_filter`):
  <https://docs.makotemplates.org/en/latest/filtering.html>
- Mako usage (`Template`, `TemplateLookup`): <https://docs.makotemplates.org/en/latest/usage.html>
- Mako inheritance / defs / namespaces:
  <https://docs.makotemplates.org/en/latest/inheritance.html>,
  <https://docs.makotemplates.org/en/latest/defs.html>,
  <https://docs.makotemplates.org/en/latest/namespaces.html>
- pyperf runner CLI and API (defaults, `--affinity`, `-o`):
  <https://pyperf.readthedocs.io/en/latest/runner.html>,
  <https://pyperf.readthedocs.io/en/latest/api.html>
- pyperf system tuning (Windows posture, REALTIME_PRIORITY_CLASS, affinity advice):
  <https://pyperf.readthedocs.io/en/latest/system.html>
- pyperf CLI (`--track-memory` replaces time; `--tracemalloc`):
  <https://pyperf.readthedocs.io/en/latest/cli.html>
- CPython tracemalloc (`get_traced_memory`, `reset_peak`, overhead):
  <https://docs.python.org/3/library/tracemalloc.html>
- simonw minijinja-vs-jinja2 study (Oct 2025) — the transparency model (published scripts,
  mean/median/std): simonw/research repository, minijinja-vs-jinja2
- Marr, Daloze & Mössenböck, *Are We Fast Yet?* (DLS 2016) — controlled-track methodology:
  <https://github.com/smarr/are-we-fast-yet>
- Repo conventions bound by this spec:
  [spec-conventions](../../common/spec-conventions.md),
  [testing-standards](../../common/testing-standards.md),
  [coding-standards](../../common/coding-standards.md)
