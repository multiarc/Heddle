# Cross-cutting decisions

Shared technical decisions that every spec — current and future — relies on. Each is a
closed decision in the [spec-conventions](spec-conventions.md#required-structure-of-a-spec)
format; specs link here instead of restating. Numbering is stable — specs cite `D1`,
`D2`, … Records of *completed* work (per-window records, the accumulated amendments
ledger, registry corrections) live in the [historical records](../records.md), not here.

## D1 — Stable diagnostic IDs (`HEDxxxx`)

**Decision.** `HeddleCompileError` and `HeddleCompileWarning` carry a stable string
diagnostic ID of the form `HED` + four digits (e.g. `HED1003`), surfaced in `ToString()`
and available to tooling (the LSP maps it to `Diagnostic.code`). IDs are allocated in
**feature-area blocks** — the first digit names the area:

| Block | Feature area |
| --- | --- |
| `HED0001`–`HED0999` | Core engine (pre-existing errors/warnings, assigned as they are touched) |
| `HED1xxx` | Expressions (native tier, expression modes) — [native-expressions.md](../../native-expressions.md) |
| `HED2xxx` | Output profiles & encoding — [built-in-extensions.md](../../built-in-extensions.md#html-encoding) |
| `HED3xxx` | Branching / branch sets — [built-in-extensions.md](../../built-in-extensions.md#branch-sets) |
| `HED4xxx` | Template semantics & ergonomics (double-render, `range`, deprecations) |
| `HED5xxx` | Props & slots — [language-reference.md](../../language-reference.md#props-nameprop-type--default) |
| `HED6xxx` | Tooling / LSP (reserved — tooling-only messages are not compile diagnostics) |
| `HED7xxx` | Build-time generator (`HED70xx`) and precompiled runtime registration/fallback (`HED71xx`) — [precompilation.md](../../precompilation.md) |
| `HED8xxx` | Streaming & sinks (reserved — sink APIs throw host errors, no compile diagnostics) |
| `HED9xxx` | Feature switches / integration |

Every spec's *Diagnostics* section claims concrete IDs from the matching block with
message text and trigger condition, and records the claim in the
[registry](#claimed-diagnostic-ids-registry) in the same change; an ID once shipped is
never reused or renumbered.

**Rationale.** Stable codes must exist before any new compiler warning ships; per-area
blocks let independent specs allocate without collisions. **Alternatives rejected:**
sequential global numbering (couples spec authoring order); severity-encoded prefixes
like `HEDW`/`HEDE` (severity is already a property; encoding it in the ID breaks when
severity is reclassified).

## D2 — Breaking changes land only in ratified breaking windows

**Decision.** Byte- or behavior-breaking changes ship only inside a **ratified breaking
window** (a major release), absorbed as **one** migration per window; everything between
windows is additive-only (new options default to current behavior, new extensions, new
overloads). The window policy — contents ratification, execution order, the migration-note
deliverable, as-shipped verification — and the **register of candidates for the next
window** live in [breaking-windows.md](breaking-windows.md); completed windows are
recorded in the [historical records](../records.md).

**Rationale.** Maintainer-ratified (July 2026); one migration per window is cheaper for
users than a trickle. **Alternatives rejected:** per-change majors (version churn);
silent behavior change in a minor (violates the
[breaking-change rules](https://learn.microsoft.com/en-us/dotnet/standard/library-guidance/breaking-changes)).

## D3 — One-time audits for platform/language surface changes

**Decision.** When a change to the platform or language surface (a `LangVersion` bump, a
TFM change, a compiler-behavior change such as new implicit conversions altering overload
resolution) can affect existing code repo-wide, exactly **one** repository audit runs —
as the kickoff work item of the effort making the change — and its findings (or the
explicit "no impact" result) are recorded in that effort's assumed-state section. Later
work does not repeat the audit.

**Rationale.** Such changes apply repo-wide the moment they land; auditing once, in the
first work item that needs it, makes the audit unmissable without repeating it per spec.

## D4 — UTF-8 static-piece emission (`"…"u8`) is precompiled-only

**Decision.** Emitting template static text as `"…"u8` UTF-8 literals belongs to the
build-time generator only (`HeddleEmitUtf8Pieces`, [precompilation.md](../../precompilation.md)),
designed so the `IBufferWriter<byte>` sink consumes those pieces without transcoding.
The runtime engine keeps `string`/`char` internals and does not pre-build UTF-8 caches.

**Rationale.** The optimization only pays where static pieces are known at build time.
**Alternative rejected:** UTF-8 caching of static pieces in the runtime document (adds
dual-encoding complexity to every render path for a win the precompiled path gets for
free).

## D5 — Implementation follows the owning plan's declared order

**Decision.** Every spec initiative declares its work-item/phase order in its owning
plan, and implementation follows it: item *N* may assume items 1..*N*−1 merged and bind
to their public APIs; nothing depends on later items (forward pointers are non-normative
notes). Amendments discovered during implementation flow through the
[amendments ledger](#cross-spec-amendments-ledger) with maintainer ratification. (Full
rule in [spec-conventions](spec-conventions.md#dependency-order).)

**Rationale.** Declared order removes conditional designs ("if item 1 shipped, then…")
and lets later specs bind to concrete earlier APIs.

## D6 — The `Scope` publish/read channel is public API

**Decision.** The local-context channel (`Scope.Publish` / `Scope.TryRead` over a
lazily-created per-body frame — see [Scope.cs](../../../src/Heddle/Data/Scope.cs) and
[custom-extensions.md](../../custom-extensions.md)) is **public** extension API. It is
the sanctioned way for sibling extensions to coordinate declaratively (branch sets are
its first consumer). Any spec introducing a new reserved key records it in that spec.

**Rationale.** A public channel turns branching from a special case into the first
consumer of a general capability (open/closed applied at the right seam). Reserved-key
hygiene keeps independent consumers collision-free.

## D7 — .NET 10 stance: no conditional compilation by default

**Decision.** New code does **not** add `#if NET10_0_OR_GREATER` paths unless a spec
records a verified, benchmarked win that cannot be had otherwise. .NET 10 value arrives
via the runtime (JIT escape analysis, encoder scan improvements, array de-abstraction)
and the language (C# 14 on all TFMs). Designs must not rely on JIT rescue for
allocations that provably escape (verify escape behavior per design — e.g. per-body
context frames, per-call argument arrays, delegate boxing).

**Rationale.** Version-verified platform review; conditional TFM forks multiply the test
matrix for near-zero benefit here.

## D8 — Template identity & naming policy has one owner

**Decision.** The consolidated template identity policy (file-driven templates key on
the full resolver-relative path; non-file templates use integration-owned keys via an
open key-policy seam; uniqueness enforced for pre-compilation only; discovery is public
API) is owned by the precompilation surface — documented in
[precompilation.md](../../precompilation.md) and implemented under
[src/Heddle/Precompiled](../../../src/Heddle/Precompiled). Any spec that needs a
template's identity (tooling, samples, resolvers) references that policy rather than
inventing its own.

**Rationale.** One owner prevents divergent notions of "template name"; precompilation
is where identity is load-bearing (assembly-embedded keys must match runtime lookups).

## D9 — Spec pages stay unpublished

**Decision.** `docs/spec/**` is contributor material excluded from the published docs
site (via `srcExclude` in `docs/.vitepress/config.mts` — verify the exclusion covers
this folder whenever the site config changes). Whether top-level working documents
(assessments, plans, single-document specs under `docs/`) are published is a
per-document maintainer call recorded in the site config; by default they are not in
the nav.

**Rationale.** Specs serve implementers, not template authors; keeping them out of the
site keeps the published docs an author/integrator surface.

## Claimed diagnostic IDs (registry)

The live allocation state of the [D1](#d1--stable-diagnostic-ids-hedxxxx) blocks. A spec
claiming a new ID updates this table in the same change; an ID once listed is never
reused or renumbered. Message texts, triggers, and position semantics live in the owning
spec's *Diagnostics* section — this table is the collision guard and lookup index.
Corrections to past allocations are recorded in the
[historical records](../records.md#diagnostic-registry-corrections), never rewritten here
silently.

| IDs | Owner | Notes |
| --- | --- | --- |
| `HED0001`–`HED0003` | Core engine | Pre-existing diagnostics (resolver / legacy shapes / syntax listener) |
| `HED0004` | Core engine | Pre-existing `CheckTypes` return-type message |
| `HED1001`–`HED1017` | [native-expressions.md](../../native-expressions.md) | Native-expression tier |
| `HED2001`–`HED2003` | [built-in-extensions.md](../../built-in-extensions.md#html-encoding) | Output profiles |
| `HED3001`–`HED3005` | [built-in-extensions.md](../../built-in-extensions.md#branch-sets) | Branch sets (incl. the `HED3005` drift warning) |
| `HED4001`–`HED4002` | [built-in-extensions.md](../../built-in-extensions.md) | Ergonomics (`range` step, double-render) |
| `HED4003` | [import-removal-spec](../../import-removal-spec.md#7-diagnostics) | `@import()` **removal error** — the legacy include is removed; positioned at the call, severity error, naming `@<<`/`@partial` (repurposed from the improvement-spec B3 deprecation *warning*; see the [amendments ledger](../records.md#cross-spec-amendments-ledger)) |
| `HED4004` | [language-reference.md](../../language-reference.md#imports--) | `@<<` composition import nested inside a subtemplate (not top-level); import skipped, positioned at the `@<<` directive |
| `HED5001`–`HED5018` | [language-reference.md](../../language-reference.md#props-nameprop-type--default) | Props & slots |
| `HED6xxx` | — reserved, none claimed | Tooling-only messages are not compile diagnostics |
| `HED7001`–`HED7016` | [precompilation.md](../../precompilation.md) | Generator build-time (incl. the `HED7016` drift warning) |
| `HED7101`–`HED7103` | [precompilation.md](../../precompilation.md) | Runtime registration/fallback |
| `HED8xxx` | — reserved, none claimed | Sink APIs throw host errors, no compile diagnostics |
| `HED9001` | Engine feature switches | C# tier disabled by the trimming feature switch (internal ID, not public `HeddleDiagnosticIds` surface) |

## Cross-spec amendments ledger

The amendment mechanism. Later work may amend an earlier spec's recorded artifact; the
amendment is proposed as a ledger entry (with the evidence that forced it), ratified by
the maintainer, and **implemented by the effort that makes it** — the earlier spec's text
is never silently retrofitted. The current end-state of any decision is therefore *its
spec plus the ledger*.

Entry format: **Amendment** (what changed, with evidence) | **Made by** (the amending
effort/spec) | **Amends** (the decision or artifact amended). Entries are append-only and
never renumbered or deleted.

The accumulated entries — an append-only log that includes retired initiatives — live in
the [historical records](../records.md#cross-spec-amendments-ledger); a spec adding an
entry appends it there in the same change.
