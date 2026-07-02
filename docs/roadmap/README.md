# Heddle Evolution Roadmap

The phased plan for evolving the Heddle language, produced from the
[language re‑assessment](../assessment.md) (§6 "Recommendation") and the maintainer's
direction decisions. Each phase has its **own independent document** with a goal, a verified
design, back‑compat analysis, risks, **measurable success criteria**, and **concrete
validation scenarios**. Any phase can be picked up, executed, and shipped on its own.

> These pages are contributor material and are **not published** to the docs site (same
> treatment as `assessment.md`). `plan.md` in this folder is the original unsplit planning
> snapshot these documents were derived from; the per‑phase documents are authoritative.

## Phases

| # | Document | Goal (one line) | Grammar change | Depends on |
| --- | --- | --- | --- | --- |
| 1 | [Native expressions](phase-1-native-expressions.md) | Sandbox‑safe, Roslyn‑free expression tier: `@if(Count > 0)`, `@(Price * Quantity)` | Yes (CALL mode) | — |
| 2 | [Safe output default](phase-2-safe-output.md) | `OutputProfile.Html` encodes the unnamed `@(...)` by default; `raw` opt‑out | No | — |
| 3 | [Branching via context flow](phase-3-branching.md) | Optional `@elif`/`@else` extensions coordinating through a published local context — declarative, no keywords, no chaining | **No** | — (1 improves conditions) |
| 4 | [Ergonomics quick wins](phase-4-ergonomics.md) | `@for(5)` loop sugar, double‑render warning, directive line trimming | No | — (loop sugar nicer with 1) |
| 5 | [Named props & slots](phase-5-props-and-slots.md) | Definitions with typed named parameters and parameterized `@out()` slots | Yes (design‑first) | 1 (literals/args parsing) |
| 6 | [Editor tooling / LSP](phase-6-tooling-lsp.md) | Typed completion, diagnostics, go‑to‑definition in editors | No | Best after 1 & 5 settle the grammar |
| 7 | [Build‑time compilation](phase-7-build-time-compilation.md) | Source generator emitting C# at build time; Native AOT + trimming | No (new backend) | 1 (Roslyn‑free expressions) |
| 8 | [Streaming & async rendering](phase-8-streaming-async.md) | `TextWriter` / UTF‑8 `IBufferWriter<byte>` sinks, no full‑string materialization | No | — (7 amplifies it) |

## Ordering rationale

Phases 1–3 are fixed by the maintainer's direction decisions:

1. **Native expression language first** — it is the keystone: it decouples the common case
   from Roslyn, which simultaneously enables a credible sandbox, cheaper compiles, and the
   later build‑time/AOT backend (phase 7). Surface: **bare expressions in call parens**,
   extending today's sigil‑less member paths; the inner‑`@` form stays full C#. Operators
   match C#/C style completely (see the phase doc's precedence table); capability is limited
   to properties/indexers/operators/literals plus **registered whitelisted functions**.
2. **Safer output default second** — output profiles; the Html profile makes the unnamed
   `@(...)` encode, closing the XSS‑by‑default gap.
3. **Branching third** — `@elif`/`@else` as plain extensions coordinating through a
   **published local context** (no keywords, no chain syntax, no imperative statement
   structure; interleaved text keeps rendering normally). The publish/read mechanism is a
   general extension capability; branching is its first consumer.

Phases 4–8 are ordered by expected user value per unit of effort: cheap daily‑felt wins
first (4), then the component‑model completion users hit constantly (5), then the tooling
that dissolves the learning curve (6, deliberately after the grammar churn of 1 and 5),
then platform reach (7) and throughput API (8).

## Cross‑phase notes

- **Every phase document ends with an `## External grounding` section** — its load‑bearing
  technical claims were verified against primary sources (Microsoft Learn, ANTLR docs,
  engine documentation) in July 2026, with corrections applied where sources disagreed.
  Notable grounding outcomes: the phase 6 LSP library recommendation changed (the previous
  de‑facto choice is dormant), phase 7's type‑binding architecture was corrected (source
  generators cannot reflect over the compiling project's types), and phase 4's
  `@import`/`@<<` equivalence assumption proved false.
- **A second, .NET 10‑focused review round (July 2026)** added a
  `## .NET 10 opportunities (net10.0 target)` section to every phase document,
  version‑verified against official sources (`net10.0` is already in every project's TFM
  list). The meta‑finding: almost nothing warrants `#if NET10_0_OR_GREATER` — .NET 10 value
  reaches Heddle through the **runtime** (JIT escape analysis, the .NET 10
  `SearchValues`‑backed encoder scan, array de‑abstraction — all of which benefit any
  build running on the .NET 10 runtime) and through the **language** (`LangVersion` is
  `latest`, so C# 14 applies on all TFMs). Verified negatives were documented so no design
  relies on JIT rescue: context frames (phase 3), props arrays (phase 5), and the compiled
  delegate boxing (phase 1) all provably escape. Cross‑cutting actions surfaced: add a
  stable diagnostic‑ID scheme (e.g. `HED0001`) to `HeddleCompileError`/`Warning` before
  phase 4's first compiler warning ships; run a one‑time repo audit for C# 14
  first‑class‑span overload‑resolution changes; phase 7's `"…"u8` static‑piece emission is
  the roadmap's best cross‑phase optimization (UTF‑8 transcoding moves to build time).
- **Five direction decisions were ratified by the maintainer (July 2026)** and are recorded
  in the phase docs: the `[EncodeOutput]` encoder moves to a pluggable
  `HtmlEncoder` default in the 2.0 window (phase 2, echoed in phase 8); `@<<` and
  `@import()` stay two distinct documented imports (phase 4); the LSP server is a thin
  hand‑rolled layer over `StreamJsonRpc` (phase 6); missing/unknown/mistyped props are hard
  compile errors (phase 5); and **one‑shot tool execution (`dotnet tool exec`, .NET 10) is
  rejected for security** — an editor extension must never trigger download‑and‑execute
  from a package feed at launch; LSP distribution is explicit `dotnet tool install`
  (version‑pinned) or a server binary bundled in the .vsix (full rejection record in
  phase 6).

- Every phase is independently shippable; none blocks another outright. The only hard
  dependency is **7 → 1** (a build‑time backend needs expressions that don't require
  runtime Roslyn).
- Phases 1 and 5 change the grammar (`HeddleLexer.g4`/`HeddleParser.g4` + one
  `generate_cs.cmd` regen commit each). Phases 2, 3, 4, 6, 8 do not touch the grammar.
- Breaking changes are confined to explicit version windows: the phase 2 default flip
  (`Text` → `Html`) is the planned 2.0 breaking change; everything else is additive.

## When this roadmap goes live (pending integration steps)

These are intentionally **not** done yet (roadmap docs only, no changes outside this folder):

1. `docs/.vitepress/config.mts` — add `'roadmap/**'` to `srcExclude` (keeps these pages
   contributor‑only, like `assessment.md`).
2. `docs/assessment.md` §6 — add a link to this index and note that the chosen phase order
   (expressions → safe default → branching → value‑ordered rest) supersedes the assessment's
   suggested sequencing.
3. Verify `cd docs && npm run docs:build` passes and the roadmap pages are absent from
   `.vitepress/dist` (mind the Windows drive‑casing caveat; `preserveSymlinks` is already set).
