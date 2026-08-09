# Cross-cutting decisions

Shared technical decisions that every spec — current and future — relies on. Each is a
closed decision in the [spec-conventions](spec-conventions.md#required-structure-of-a-spec)
format; specs link here instead of restating. Numbering is stable — specs cite `D1`,
`D2`, … This document also carries the condensed records of *completed* work: the
[release records](#release-records--as-shipped) and the collapsed
[program records](#program-record--generator--engine-code-sharing-closed) of the two
finished initiatives, folded in when the append-only historical record was retired
(full text: `git show c4691266:docs/spec/records.md`).

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
window** live in [breaking-windows.md](breaking-windows.md), along with the running
record of the current open window; completed windows are condensed into the
[release records](#release-records--as-shipped) once reconciled against the shipped
source.

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

## D10 — Documentation authority is mapped, and it is conditional

**Decision.** Two parts: *which* document arbitrates a claim, and *when* it gets to.

**(a) The mapping.** A claim block has exactly one normative home, and it is the home named in
the table below. Where a claim spans two homes, the one whose *diagnostics* the claim can produce
owns it — that is the tie-break, because a diagnostic has a registry owner and prose does not.

| Claim block | Normative home | Also gated by |
| --- | --- | --- |
| Native-expression semantics (operators, coercion, arity, overloads) | [native-expressions.md](../../native-expressions.md) | `HED1001`–`HED1018` registry rows |
| Built-in extensions, output profiles, branch sets | [built-in-extensions.md](../../built-in-extensions.md) | `HED2001`–`HED4005` registry rows |
| Template syntax and parse shapes | [language-reference.md](../../language-reference.md) | parser tests |
| Extension authoring, `Scope` channels, carrier transparency | [custom-extensions.md](../../custom-extensions.md) | [D6](#d6--the-scope-publishread-channel-is-public-api) |
| Precompiled tier: manifest, gauntlet, fallback taxonomy, schema | [precompilation.md](../../precompilation.md) | `HED7xxx`/`HED71xx` registry rows, [D8](#d8--template-identity--naming-policy-has-one-owner) |
| Template identity, keys, registered names | [D8](#d8--template-identity--naming-policy-has-one-owner)'s named owner | `HED7002`–`HED7004`, `HED7018`, `HED7028`, `HED7104` |
| Host/embedding API surface | [csharp-api.md](../../csharp-api.md) | the public-API golden |
| Breaking-change process, schema-version policy | [breaking-windows.md](breaking-windows.md) | window as-shipped records |
| Test posture, single-sourcing, regression gates | [testing-standards.md](testing-standards.md) | — |

Anything **not** in a listed block has no normative document: the implementations are the
authority, and the runtime dynamic engine is the tie-break between them (the match principle). A
document acquires a block by being added to this table, not by asserting authority in its own prose.

**(b) The condition.** A normative home outranks the implementations **for a claim that carries a
verification marker or is covered by a gate**. An unmarked, ungated claim is *evidence of intent,
not an authority*: a contradiction between it and both implementations agreeing is resolved by
investigating and recording the outcome — never by editing code to match the sentence. Marked
claims use the footer form *"Verified against source at `<commit>` (`<date>`); claims marked ✓ are
gated by `<test>`."*

**Non-retroactive.** Decisions already ratified against the unconditional convention stand as
ratified; this narrowing applies to resolutions taken after it. Re-opening them would relitigate
outcomes on a rule that did not exist when they were taken.

**Rationale.** When two tiers disagree, some document has to break the tie, and for expression
semantics the specification genuinely is the better arbiter than either implementation. But
unconditional authority over prose that nothing checks is a defect-generating mechanism: it lets a
stale sentence order a code change. The condition keeps the convention where it earns its keep (two
tiers disagree) and removes its teeth where it was dangerous (both tiers agree and the doc is
simply out of date) — which does not make stale prose unfixable, only non-executable.

The mapping is here rather than in a program plan because it is the part a future reader most needs
and the part that was previously spread across a registry column, a plan bullet, and an unstated
convention. A reader aligning drift has to know which sentence is allowed to win *before* deciding
what to change.

## D11 — The engine does not decide which assemblies are loaded

**Decision.** Choosing *which* assemblies exist, and *when* they are registered, belongs to the
integration layer, never to the engine. Concretely, in order of force:

- **The engine must not load assemblies, and must take nothing from one it did not.** No discovery
  walk, no module initializer, no `DependencyContext` closure walk, and nothing an assembly is
  merely *referenced by* is pulled in. Whatever the engine acts on, the host loaded or handed it —
  the shape `PrecompiledTemplates.Register(assembly)` already has.
  <br>Enumerating what the host has *already* loaded is not loading, and is how observation works
  (see below): `AppDomain.CurrentDomain.GetAssemblies()` is read on the resolution and registration
  paths, deliberately. An earlier wording of this bullet forbade that enumeration outright while the
  paragraph below prescribed it — a contradiction that would have led anyone resolving drift against
  it to delete observation and reintroduce load-order dependence.
- **The engine may report.** A registration that creates a potential conflict draws a diagnostic —
  a warning, or an exception where the situation is genuinely unresolvable. Reporting is the
  engine's business; deciding for the host is not.
- **The engine may suggest, in documentation only.** A host that wants ordering guarantees can be
  offered an architectural pattern in prose, and nothing stronger.

A question of the form "is this a host configuration error or a legitimate late-binding outcome?"
is therefore not the engine's to answer: answering it means deciding for the host in what order
assemblies may register. Where removing a discovery walk leaves a real need with no API, that is a
**missing extension point to add**, not a reason to keep the walk.

**Rationale.** Maintainer ruling, 2026-07-26 (Q8.34), on the Razor precedent: discovery-by-default
is how a library acquires a framework's worth of coupling to one hosting model. It also bounds the
observability question that raised it — a registered `Name` that stops resolving because an
unrelated assembly loaded is reported once through `OnFallback` (`HED7104`) and no further:
`PrecompiledMismatchPolicy.Strict` deliberately does not extend here, because `Strict`'s subject is
degradation to the dynamic tier and no degradation occurs.

**How the engine obtains its assembly set.** Two sources, and no third:

- **Observation.** Assemblies the host has already loaded **into the default load context** are visible
  for type resolution and C#-tier metadata, and remain so however late they load — the engine re-checks
  the set rather than holding a snapshot, so load order does not decide what resolves. Observing decides
  nothing: an assembly is there or it is not. Excluded, deliberately: a dynamic assembly, an assembly in
  a collectible or custom context (observing one would pin a context the host expects to unload — this
  is what excludes an `Assembly.Load(byte[])` result, which the runtime places in its own context), and
  the engine's **own** emitted expression assemblies, which would otherwise accumulate one per compiled
  C# expression. **`Assembly.Location` is deliberately not the test:** it is empty for the whole
  application in a single-file or WASM publish, so filtering on it made the engine observe nothing at
  all there — a defect an adversarial review reproduced against a real single-file host.
- **Registration.** `HeddleTemplate.Register(assembly)` for anything else, and it is the **only**
  source of `[ExportExtensions]`: extension **name ownership** is decided exclusively by assemblies a
  host names. A loaded-but-unregistered assembly cannot take a name, so it cannot collide with an
  unrelated claimant and throw `TemplateOverrideException` out of a type initializer.

`AssemblyHelper` declares no static constructor and calls no `Assembly.Load`; the removal of the walk
that did both is recorded as
[2.1 window item 10](breaking-windows.md#current-window--21-open-as-implemented-pending-release).
`PrecompiledTemplates.Register` has the same shape. The engine ships no module initializer — one exists in
`src/`, in the generator integration suite, where it is a **test host** registering itself, which is the
pattern this decision prescribes rather than an exception to it.

## Release records — as shipped

Condensed from the retired append-only historical record (full text:
`git show c4691266:docs/spec/records.md`). The open **2.1** window's running record lives in
[breaking-windows.md § Current window](breaking-windows.md#current-window--21-open-as-implemented-pending-release)
until reconciled at release; only closed windows are condensed here.

**The 2.0 breaking window** — closed; `v2.0.0` released 2026-07-19, reconciled against the
shipped source per breaking-windows policy rule 5; migration note shipped as the release's
[CHANGELOG](../../../CHANGELOG.md) entry:

1. `OutputProfile` default flipped `Text` → `Html` — the unnamed `@(...)` encodes by default.
2. `TemplateOptions.Encoder` seam added (`System.Text.Encodings.Web.TextEncoder`, span/UTF-8
   paths); the default (`null`) keeps the 1.x `WebUtility.HtmlEncode` bytes. The byte-changing
   default *swap* stays a [next-window candidate](breaking-windows.md#next-window-candidate-register).
3. `TrimDirectiveLines` default flipped `false` → `true`.
4. `AllowCSharp` marked `[Obsolete]`, behavior unchanged (the bridge over `ExpressionMode` keeps
   working); removal is a next-window candidate.
5. Generator MSBuild defaults follow the engine: `HeddleOutputProfile=Html`,
   `HeddleTrimDirectiveLines=true`.
6. Legacy `@import()` removed — a call site fails with one positioned `HED4003` error naming
   both replacements (`@<<{{ path }}` / `@partial(){{ name }}`); the `import` name is kept as a
   registered no-op tombstone so the diagnostic is a targeted migration signpost.
7. `Heddle.Models.ForModel` deleted; `range(...)`, `PrecompiledFunctions.Range` and `@for`
   remodeled onto the immutable `Heddle.Models.Range` readonly struct (only rendered-byte
   effect: range stringification renders the readable call form).

Explicitly excluded from the window: the default-encoder swap, `[NotEncode]` type deletion,
`TypeForwardedTo` hygiene, async render APIs, any grammar change beyond the tombstone.

A lesson the record's appended correction preserves: the claim "precompiled assemblies from 1.x
fall back under 2.0" described a case that cannot occur — precompilation shipped *in* 2.0.0, so
no 1.x manifest ever existed. What is true is the rationale: the engine-version and schema gates
exist so stale generated code cannot mask a byte-changing window. The claim survived because it
was reasoned from the gate's existence rather than the release history — exactly the failure mode
policy rule 5's as-shipped reconciliation exists to catch.

**Diagnostic registry corrections.** 2026-07: `HED3005` (branch drift warning) and `HED7016`
(its build-time twin) shipped in 2.0.0 with the `[BranchRole]` work but were never recorded in
the registry; both verified in source and recorded.

## Program record — generator ↔ engine code-sharing (closed)

The generator ↔ engine code-sharing program is complete: all phases implemented, quarantine
register empty, suite grown 2630 → 5348 passed / 0 failed / 0 skipped. Decisions with ongoing
force are collapsed below with their origin ids; the sharing-architecture rules the program
established live in [shared-source-architecture.md](shared-source-architecture.md), and its
process legacy in [review-protocol.md](review-protocol.md) and
[findings-register.md](findings-register.md). Full phase documents:
`git show c4691266:docs/generator_plan/`.

**Tier parity, degrade discipline, and emitted-code contracts**

- Precompiled-tier fallback in tests is a failure unless a fixture explicitly opts out; end-to-end tests render real generator output through registration → resolver → gauntlet under `Strict` plus a `FallbackGuard` sentinel. (generator phase 0 D1/D2, E8)
- Fallback is exercised only by tests that declare it (`ExpectDegrade` / `FallbackGuard.Expect`, which must stay the exhaustive greppable list), and a guarded fixture red on a known owned defect is quarantined with an owner-naming `Skip` — never weakened, never orphaned. (generator phase 0 D5/D6, E8)
- Extraction and test-corpus migration are byte-neutral **by acceptance gate** — goldens, differential suites, Verify snapshots unchanged with zero fallback events; regenerating a golden to absorb the diff is a defect, not a fix. (generator cross-phase note, phase 2 D11, phase 7 D9)
- For expression semantics the generator has exactly two permitted reactions: emit code provably byte-equivalent to the runtime result, or return null and degrade to the dynamic tier. Shared tables encode Heddle semantics, never C#'s. (generator phase 4 D1)
- A uniquely resolved overload is emitted with explicit casts to the chosen parameter types (pinning the consumer's C# compiler); ambiguity or any `Unknown` argument kind degrades. Heddle's flat Pareto rank remains the semantics of record. (generator phase 4 D10, OQ2/Q4.2)
- The generated body shape is contract: document-ordered piece/processor alternation; the value path coerces every result `as string ?? string.Empty` in the three-case concat shape, and the runtime's strategy short-circuits are byte-equivalent optimizations of that one rail. Any change to the rail lands runtime + emitted `Execute` + the spec section in **one** landing. (generator phase 1 D13, Q1.2 joint-land rule)
- The shared shaping pass order — shift → trim → remove definitions → replace raw output → strip branch sets → remove zero-output chains — is a normative ordering contract; drivers stay per-side, with a lockstep test on the relative order. (generator phase 2 D4)
- A template's staleness identity is SHA-256 of its **decoded text re-encoded as UTF-8 without BOM**, on both tiers; a BOM-less non-UTF-8 file is documented as outside the contract. (generator phase 5 D1)
- Key derivation has two documented comparison domains: the root-prefix test is `OrdinalIgnoreCase` (filesystem paths), the key itself preserves case and compares `Ordinal` — both derived from the shared `TemplateKey` helpers. (generator phase 5 D2)
- Every resolver arm consults the registry first, in a three-tier ladder (registry → cache → disk, each in location order, tier beating location), and build side and resolver side derive keys through the same shared code — no second key grammar. (generator phase 5 D11, Q5.2)
- Everything the build refuses on purpose reaches the runtime as a **return-shaped** refusal (marker entry or no entry), never an exception; an exception escaping the emitter is a defect reported per-template as `HED7020` with the pass continuing, and refusals are collected across sibling elements but never emitted past. (generator phase 5 D12a, Q8.19)
- The legitimate-degrade set is a closed, enumerated taxonomy (stale content, stale import, unsupported function, options mismatch); every other reason is must-surface, and widening the list means amending the taxonomy in [precompilation.md](../../precompilation.md), not adding a catch. (generator phase 5 D12b, Q2.2)
- The manifest schema number tracks **breakage, not releases** — the full rule, including the fixture-proved additivity obligation, is breaking-windows policy rule 7. (Q8.35)
- Build-root vs runtime-root agreement stays a host responsibility (deployed templates mirror their build-time root-relative layout); no per-request probe is added — the staleness check already degrades safely. (generator phase 5 D10)

**Diagnostic identity and build-time surfacing**

- Diagnostic IDs are claimed once, centrally, at spec-authoring time in registry order; IDs written in plan text are placeholders and must not be copied into code. (generator cross-phase note, D1)
- Same fact, same id: the generator forwards the engine's compile-time id rather than claiming a `HED7xxx` twin; a twin is claimed only where no engine counterpart exists to forward, and an id once listed is never renumbered. (E16; restated in the registry preamble below)
- The generator forwards as build **errors** exactly the native-expression refusals it can prove the engine repeats, composing the engine's own sentence; what it cannot prove — an `Unknown` operand estimate, a runtime-owned operator verdict, an unreproducible type spelling, an undeclared model type — keeps degrading silently, deliberately. (E17)
- If a diagnostic can surface early it must, on both tiers: forwarded warnings carry their real front-end id (the wrapper id is only for id-less ones), severity comes from the catalog row and is never escalated or downgraded, and the `Fix` text rides the build message. (generator phase 6 D2/D3, Q6.1)
- Nothing forwarded at build time may be wider than what the run tier raises for the same bytes (gated by `NothingIsForwardedThatTheRunTierWouldNotRaise`); the orphan-`@else` **error** is deliberately still run-tier-only, pinned by the skipped red `CompileChannelDrainTests.ATemplateTheEngineRefusesIsNotSilentlyPrecompiled`. (generator program-level gap, closed)
- `HED7006` fires only when a name resolves to nothing under the runtime's own discovery rule; a name that resolves but is not bindable by the generator degrades with a recorded reason instead. (generator phase 3)
- `Name` is additive and never an override: the template keeps its key and gains the name, keys resolve before names, and a name whose spelling is taken is dropped and reported while the key is unaffected — a broken addition costs the addition and nothing more. (Q8.25; landing recorded in the 2.1 window record)

**Test inputs, corpus, and documentation currency**

- A template shape that more than one tier verifies exists exactly once as a shared corpus entry with a declared intent row (build-tier classification, how it may be exercised, a mandatory `Why`), and membership is gated by **set equality** against that table — never a count and never a floor. (generator phase 7 D3/D5, E9)
- Share only what is genuinely shared: a fixture whose tiers are fed different inputs and asserted against different outputs is two tests, stays inline and is not relocated; the driver for sharing is the generator-matches-runtime requirement, recorded per fixture. (Q8.42, Q8.44)
- Shared test **inputs** are read from the consumer's own output directory, never by traversal into a sibling project's `bin`; a build copy is not a second home, so inputs stay in their tracked folder. A compiled sibling assembly is an **output** — permitted, but the read must be build-ordered and fail loudly on a miss. (E9, Q8.40)
- The corpus directory is input only: no test writes into it (written artifacts live outside the glob), and encoding is declared and gated — goldens carry no BOM, a template carries one iff its intent row says so, and the file-backed sweep stages each entry's real encoding. (generator phase 7 D7, Q7.3)
- Documentation examples are not single-sourced from the test corpus, and no fixture may claim to be a document's bytes; the residual staleness risk is carried by the currency rule instead. (Q8.10)
- A docs sweep corrects documents, never code: doc-right/code-wrong is escalated as a question with a stated default, and a genuine both-defensible disagreement is recorded in the doc as a known limitation. (generator phase 8 D2; D10 above is the authority rule this pairs with)
- A change that alters observable behaviour names the documents describing it in the same landing and either updates them or records why not; four surfaces are gated (diagnostic ids off the registry's owner column, option names/defaults and editor mirrors, doc-mentioned public API ⊆ the API golden one-way, citations resolving over published docs and specs) and everything else is **dated** by a per-document verification footer, with the non-gateable residue enumerated. (E11, generator phase 8 D4/D5/D6/D8/D11; the added section lives in [testing-standards.md](testing-standards.md))
- Cite a path plus an anchor symbol rather than a bare line number wherever the citation must survive edits to its target. (generator phase 8 D8)
- A behavioural gate is coupled to the semantics it gates — when the semantics are re-derived, the gate must be re-derived or it silently stops gating; and a build-surface contract verified only through injected analyzer-config values is unverified, so something must actually evaluate MSBuild. (generator standing lesson)

## Program record — cross-stack benchmarks (closed)

The cross-stack benchmark program is complete: eight workloads, six ecosystems, sixteen engines,
gates green in every harness, report published at `docs/benchmarks/<date>/`. Decisions with
ongoing force are collapsed below with their origin ids. The operational contract the harnesses
implement (workloads, parity contract, golden corpus, metrics protocol, per-ecosystem harness
docs) lives in [`benchmarks/docs/`](../../../benchmarks/docs/README.md). Full phase documents:
`git show c4691266:docs/spec/cross-stack-benchmarks/`.

**Parity, corpus, and gate invariants**

- The normalization pipeline is the closed list N1–N5 including N3b, which removes every whitespace run from *both* oracle and candidate at comparison time, so any whitespace-only divergence passes; whitespace is the six ASCII chars TAB/LF/VT/FF/CR/SPACE, and a BOM survives N1 and fails. (benchmarks phase 1 D8, Q1.2)
- The controlled gate runs before any timing in the same process that produces the numbers; a failed gate aborts with no numbers emitted for that suite. (parity contract, phase 2 D11)
- N5 entity canonicalization is scoped to the literal five characters `& < > " '`, encoded suite only; engine configuration is preferred to normalization wherever the engine offers it. (benchmarks phase 1 D2, D3)
- Encoded-suite untrusted data stays inside the pinned alphabet: ASCII printable minus `+`, `=`, `` ` ``, never containing `&#`, plus BMP U+0100–U+FFFF; Latin-1 supplement and astral characters are forbidden. (benchmarks phase 1 D4)
- The encoded gate additionally asserts the security floor — zero raw `<script>alert(` in un-normalized output, escaped form at the expected count — as defense in depth against a corrupted oracle. (parity contract, controlled gate 5)
- The idiomatic track's only bar is the machine-checkable verifier (values / ordered markers / forbidden / required, matched on the whitespace-stripped projection), calibrated to accept its golden and reject a removed row and two swapped sections — plus an unescaped payload for encoded workloads. (benchmarks phase 1 D10)
- Idiomatic implementations are authored in-repo from each engine's official documentation with the doc URLs cited in a per-file header comment; never imported from third-party benchmark repos. (benchmarks phase 1 D16, Q1.7)
- Only non-whitespace divergence triggers exclusion: the controlled cell prints `excluded — documented evidence` with a link to the divergent bytes and authoring attempts, the engine stays in the idiomatic track, and no replacement engine enters without user sign-off; nothing is ever curve-graded. (benchmarks phase 1 D11, phase 4 D16)
- A workload that cannot pass the intra-.NET gate across all twins is redesigned or dropped *before* corpus export — no ecosystem ever ports an ungated workload. (parity contract, exclusion policy 3)
- The corpus stores the normalized oracle as UTF-8, no BOM, no trailing newline, pinned `-text` in `.gitattributes`, with per-entry `byteLength`/`sha256`/`generatingCommit`/`generatedUtc`; regeneration is an ordinary versioned change at a clean commit, and a dirty tree needs `--allow-dirty` and is stamped `+dirty`. (benchmarks phase 1 D6, D7)
- Razor is a full byte-parity twin under the composed-page gate authored against the shared fixtures — no non-parity row may sit inside a protocol suite, and no twin may carry a hand-duplicated fixture copy. (benchmarks E5)

**Workload and model authoring (the 2026-08-08 wave)**

- `composed-page` is a genuine full-page layout workload built on the layout-as-definition idiom with a live `@out()` body slot (both tracks carrying the same body), and `fragment-heavy` is four dispatched fragment kinds with one nesting level; this supersedes the D5/Q1.4 anchor-freeze. (benchmarks E20)
- The model-preparation tier carries DATA only — every derived display string is composed template-side as literal-plus-substitution; zero-padded identity names and encoded-suite untrusted payloads are the two deliberate exceptions that stay model-side. (benchmarks E21)
- No text blobs in the C# tier at all: templates hold ALL formatting and text, chrome living in a definition-only fragment library imported by the layout — supersedes E20's hybrid blob/structured split. (benchmarks E22)
- Every nav/text value is sanitized to the workload data rules (`&`→`and`; apostrophes, ™ and accents dropped — [workloads.md](../../../benchmarks/docs/workloads.md) rule 4), asserted at model construction, because default-escaping engines would otherwise double-escape. (benchmarks E20)
- Each engine's entry template is named for its workload; Heddle's `home.heddle` became `composed-page.heddle` and the `"home"` special-casing is gone everywhere. (benchmarks E26)
- All fifteen engine ports (five .NET twins, ten ecosystem engines) landed 2026-08-08 on native layout mechanisms and native dispatch chains, all gates green. (benchmarks E23)

**Harness, layout, and toolchain conventions**

- Each ecosystem harness lives at top-level `benchmarks/<ecosystem>/`. (benchmarks phase 2 D2)
- The .NET leg is `benchmarks/dotnet/`, a single unsigned `net10.0` verb-dispatching harness reaching the engine through its public surface only; `src/Heddle.Performance` is deleted and nothing may cite its code. (benchmarks E12)
- The top level of each track's engine template folder holds ONLY the eight runnable entry templates; layouts, chrome fragments, partials and definition libraries live in `shared/` or the engine-native equivalent. (benchmarks E24)
- Every engine keeps its two tracks in `controlled/` and `idiomatic/` subfolders, the package/folder path carrying the track rather than a name prefix. (benchmarks E27, completing E24)
- The measurement budget unit is ONE ENGINE — 16 cells, eight workloads × two tracks — at ~10 min `short` and ~30 min `baseline`, with every leg resized to it and each leg spending the increment on its own dominant variance term. (benchmarks E6, E13, E14)
- Every suite's `short` profile executes at least FIVE measurement samples per run, and `baseline` scales through the same job rather than beside it. (benchmarks E28)
- The Node pin is the major line `24.x` with `engine-strict` left ON; the exact version a run used is recorded in `toolchain.json` and the report environment block, and comparisons quote the recorded version, never the pin. (benchmarks E18, overriding phase 4 D2)
- The CPython pin is the minor line `3.14.x` under the same posture; a non-3.14 interpreter warns and records a delta. (benchmarks E19)
- `gate-precompiled` runs strict 8/8 with three-sink byte parity — the encoded pair's dynamic fallback is closed by pinned step-back encoders plus an `Html`-profile satellite assembly — and coverage is always discovered from the manifest, never assumed. (benchmarks E25, superseding the coverage notes in E20/E22)
- Statistic selection is fixed per harness (BenchmarkDotNet `Mean`, Criterion mean, JMH `Score`, mitata `avg`, pyperf `mean`, benchstat `sec/op`) and ecosystem harnesses may pin versions but never vary it; dispersion is always published alongside. (benchmarks phase 1 D12, Q2.1)
- JS bench bodies must materialise output (`flatten(render(...))` via `%FlattenString`), and the materialisation check — implied throughput against the 50 B/ns ceiling, fatal also if the flatten primitive fell back — runs alongside the deopt check, which alone detects only total elimination. (benchmarks E4, phase 4 D12)
- Stability triggers are pre-committed per harness and never satisfied by re-running until favorable: JS 5-run RSD ≤5% publish / 5–10% disclose / >10% blocks publication; Go benchstat >±5% forces a recorded re-run; Python quotes instability warnings and re-runs a whole suite once at `--rigorous`. (benchmarks phase 4 D13, phase 6 D9, phase 5 D9)

**Publication and honest reporting**

- Wall time per render is the only cross-language-comparable number, measured on the cached-template path with template and model built outside the loop. (metrics protocol, rule 1)
- Allocation/GC and cold parse/compile are per-ecosystem only, labeled non-comparable, and never appear as a cross-language column or in a table juxtaposing two runtimes. (metrics protocol rules 2–3, phase 1 D14, phase 7 D7)
- Non-Heddle engines are never ranked or compared across ecosystems; the single sanctioned exception is the per-workload cross-stack ranked table carrying evidence-class and implied-throughput columns, and even then no geomean, points total, medal count or overall score exists and the prose stays Heddle-anchored. (benchmarks phase 1 D13/Q6.2, phase 7 D6 as amended)
- Every per-ecosystem report carries a labeled wall-time-only Heddle reference row excerpted from the protocol run with the ratio column anchored to it; non-comparable metrics anchor to the ecosystem's credibility pick; every table names its track and tracks are never mixed. (benchmarks phase 1 D13, presentation rules 1–5)
- Honest-reporting rules 1–6 are protocol: no universal-superiority claims, losses named as prominently as wins with numbers, dated/hardware-specific figures with a reproduce command, verbatim labels including the encoded-suite confinement caveat adjacent to every encoded result, no numbers from a gate-failed suite, and excluded cells never blank. (metrics protocol, phase 7 D12)
- Runs publish as immutable `docs/benchmarks/<yyyy-MM-dd>/` directories (corrections get a new date, never an edit), and `docs/benchmarks/` keeps only the latest run's report — citations of removed runs are de-linked with their visible text preserved, never repointed at a run that never measured them. (phase 7 D2, benchmarks E15)
- The consolidated report recomputes nothing: every figure is a verbatim excerpt of a published source table with unit conversion and a Heddle-anchored ratio the only permitted arithmetic, the newest protocol run per ecosystem is aggregated, and defects escalate to the owning harness rather than being patched in the report. (benchmarks phase 7 D1, D4, D14)
- Report workload order is presentation-only — tier 1 (below the LOH line as UTF-16) before tier 2, ascending by rendered size, derived in `consolidate.py` — and the implied-throughput numerator is rendered size, not the normalized golden. (benchmarks E7)
- All cross-compared runs execute on the one recorded Windows 11 / Ryzen 9 9950X box with a required environment block; the Ubuntu 24.04 cross-check is published separately, never merged with Windows numbers, with no Windows-attributed absolute value or time unit in it and tooling that stores ratios and dispersions rather than absolute times. (benchmarks phase 1 D14/Q1.6, phase 8 D14/D17, Q5.2)
- Cross-OS comparison uses harness-native dimensionless relative dispersion, never compared across harnesses, against fixed material-divergence thresholds (rank flip resolved on both OSes; gap movement > max(D, 0.05) with D a linear sum; dispersion-character ratio ≥2 with max ≥0.01) — and every cell's values and verdict are published regardless, "material" controlling prominence only. (benchmarks phase 8 D15, D16)
- Errata and cross-phase overrides are recorded through the amendment mechanism (see [§ Cross-spec amendments ledger](#cross-spec-amendments-ledger)); ratified prose stays as written and nothing is patched locally in a consuming document. (benchmarks phase 2 D14, phase 7 D14)

## Claimed diagnostic IDs (registry)

The live allocation state of the [D1](#d1--stable-diagnostic-ids-hedxxxx) blocks. A spec
claiming a new ID updates this table in the same change; an ID once listed is never
reused or renumbered. Message texts, triggers, and position semantics live in the owning
spec's *Diagnostics* section — this table is the collision guard and lookup index.
Corrections to past allocations are recorded in the
[release records](#release-records--as-shipped), never rewritten here silently.

**Same fact, same id.** When the build tier refuses a template for a fact the engine also
diagnoses at compile time, it **forwards the engine's id** (via the forwarded-descriptor
mechanism the parse channel already uses) rather than claiming a `HED7xxx` twin. A twin id is
claimed only where no engine-compile-time counterpart exists to forward — a build capability
notice (`HED7031`), a fact the engine discovers at a different stage, or a fault only a build
has. The existing twins (`HED7011`/`HED4009`, `HED7024`/`HED5019`, …) predate this rule and
keep their ids — an id once listed is never renumbered — but every **new** diagnostic follows
it, so the projection corpus can pin one id per fact instead of a mapping. Recorded as
amendment E16 (folded into the program records above).

| IDs | Owner | Notes |
| --- | --- | --- |
| `HED0001`–`HED0003` | Core engine | Pre-existing diagnostics (resolver / legacy shapes / syntax listener) |
| `HED0004` | Core engine | Pre-existing `CheckTypes` return-type message |
| `HED0005` | Core engine | The compile-item catch-all — one call in the document threw while being compiled for a reason no other diagnostic covers. Positioned at the call, carrying the exception; the text names the call and the fault. Before it, this class of failure reached callers with no id at all |
| `HED1001`–`HED1018` | [native-expressions.md](../../native-expressions.md) | Native-expression tier. `HED1018` (constant division by zero, error on **both** tiers) is the first id governed by the same-fact-same-id rule above: the generator **forwards** it rather than claiming a `HED7xxx` twin. Amendment E17 extends the forwarded set across the block: `HED1003`, `HED1004`, `HED1005`, `HED1007`, `HED1008`, `HED1009`, `HED1010` and `HED1011` also fire at **build** as errors forwarded from the generator, each carrying the engine's own sentence, wherever the generator proves the engine's refusal (unprovable shapes keep degrading silently) |
| `HED2001`–`HED2003` | [built-in-extensions.md](../../built-in-extensions.md#html-encoding) | Output profiles |
| `HED2004` | Shipped in 2.0.0; this registry row is the live normative home | HTML-context encoding lint (`MissingContextEncoder`) — warning; bare `@(value)` in an attribute/`<script>`/URL position under an explicitly declared `Html` profile without the matching `@attr`/`@js`/`@url` encoder |
| `HED3001`–`HED3005` | [built-in-extensions.md](../../built-in-extensions.md#branch-sets) | Branch sets (incl. the `HED3005` drift warning) |
| `HED4001`–`HED4002` | [built-in-extensions.md](../../built-in-extensions.md) | Ergonomics (`range` step, double-render) |
| `HED4005` | Shipped in 2.0.0; this registry row is the live normative home | `{{ x }}`-in-text misread lint (`LiquidStyleInterpolationMisread`) — warning; a bare `{{ identifier }}` / `{{ dotted.path }}` in literal text, suggesting `@(…)` |
| `HED4003` | Shipped in 2.0.0 | `@import()` **removal error** — the legacy include is removed in 2.0.0; positioned at the call, severity error, naming `@<<`/`@partial`. The normative message/trigger/position live in [language-reference.md](../../language-reference.md#imports---) and the [2.0 release record](#release-records--as-shipped) (item 6) |
| `HED4004` | [language-reference.md](../../language-reference.md#imports---) | `@<<` composition import nested inside a subtemplate (not top-level); import skipped, positioned at the `@<<` directive |
| `HED4006` | [language-reference.md](../../language-reference.md#imports---) | `@<<` composition import cycle — an import reaches a document already being imported; the repeated import is skipped and the chain named, positioned at the `@<<` directive |
| `HED4007` | [language-reference.md](../../language-reference.md#imports---) | Nesting too deep to build, reported instead of exhausting the stack. Two cases, both counted rather than measured so a template behaves the same on every host: expression, chain, or block nesting past the parse-depth bound, positioned at the document start; and `@<<` composition imports nested past the import-depth bound, positioned at the `@<<` directive that exceeded it, with that import skipped |
| `HED4008` | [language-reference.md](../../language-reference.md#imports---) | `@<<` composition imports expanded past the per-parse total — an acyclic graph that reaches the same document from several places re-parses it once per path and multiplies out; the remaining imports are skipped and the overflow is described once, positioned at the `@<<` directive that hit the bound |
| `HED4009` | [language-reference.md](../../language-reference.md#imports---) | `@<<` composition import naming a file that cannot be read — missing, unreadable, or a path the platform rejects; the import is skipped and the read failure described, positioned at the `@<<` directive. Before it, the read threw out of the tree walk, which is outside the parser's guard, so an editor analysing a buffer mid-rename published nothing at all |
| `HED5001`–`HED5018` | [language-reference.md](../../language-reference.md#props-nameprop-type--default) | Props & slots |
| `HED5019`–`HED5020` | Shipped in 2.0.0; this registry row is the live normative home | Named content regions (compile errors, fire only on the public-region surface): `HED5019` `RegionNotPublic` (a call-body override targets a callee's **private** region); `HED5020` `DuplicateRegionDeclaration` (two public regions with the same name — raised by upgrading the id-less `EnterDef` duplicate error). A region-override narrowing mismatch reuses the pre-existing id-less `WalkValidateDefinitionType` error (no new id); a typed-override member error reuses `HED0001` |
| `HED6xxx` | — reserved, none claimed | Tooling-only messages are not compile diagnostics |
| `HED7001`–`HED7016` | [precompilation.md](../../precompilation.md) | Generator build-time (incl. the `HED7016` drift warning) |
| `HED7014` | [precompilation.md](../../precompilation.md) | Generator build-time **warning** — a called function neither the default table nor any `[ExportFunctions]` reference binds, **and** in a call shape a late-bound site cannot serve either. The second clause is the row: until late binding landed, the id named any function the build could not bind, including a delegate-only registration, which is precisely the case a `PrecompiledFunctionSite` now resolves at first render through the engine's own ranker. What is left is the genuinely unrankable call — chiefly an argument whose static type has no build-time answer, so no overload can be selected against it and inventing one would pick an overload the engine never picks. Severity, position and the marker-entry outcome are unchanged; the population narrowed |
| `HED7015` | [precompilation.md](../../precompilation.md) | Generator build-time **warning** — a bound extension *outside the engine assembly* overrides `InitStart`/`CompleteInit` that **this build has not read**, so the build tier has only the *base* behaviour to emit. Not-read is the trigger, not un-evaluable — and today it is true of every extension outside the engine, the build reading no compile-time hook it did not write itself (the opt-in that ran a referenced extension's hook out of band was deleted with the mechanism behind it). **Severity was Error until the hook-probing program (step 11a)**, and the change is the point of the row: the fault is a property of a third-party extension the generator cannot reason about, so its cost is that call site's tier, not the consumer's build — the same answer `HED7014` and `HED7030` already give for their causes, and the direction the "degrade rather than error" ruling for same-compilation extensions requires. The id is deliberately **kept** rather than folded into the catch-all `HED7031` degrade: naming the extension and the hook is the one fact the author can act on, and ids are never reused or renumbered. A `[BranchRole]` custom branch extension is exempt — overriding `InitStart` is its canonical shape, so it degrades with no diagnostic at all |
| `HED7017` | Shipped in 2.0.0; this registry row is the live normative home | Generator build-time twin for a malformed extension `[Prop]` parameter declaration (Error), incl. an inherited-`[Prop]` re-declaration widening. Extension parameters reuse call-time `HED5001`–`HED5004` and declaration-side `HED5007`/`HED5008`/`HED5009`/`HED5010`/`HED5015`, and *additively relax* `HED5005` only (no new call-time id) |
| `HED7018` | [precompilation.md](../../precompilation.md) | Generator build-time **warning** — a template outside `HeddleTemplateRoot` carrying no explicit `Key` metadata registers under a flattened filename key that no root-relative lookup can hit (generator↔engine code-sharing program, phase 5 D3) |
| `HED7019` | [precompilation.md](../../precompilation.md) | Generator build-time **warning** — the `Heddle` engine assembly is not visible among the compilation's references (aliased/embedded/ILMerged), so the manifest records the generator's own version as `engineVersion` (generator↔engine code-sharing program, phase 5 D6) |
| `HED7020` | [precompilation.md](../../precompilation.md) | Generator build-time **error** — the template emitter threw, i.e. a generator defect: every intentional refusal is return-shaped, so an exception must surface instead of degrading silently (generator↔engine code-sharing program, phase 5 D12a) |
| `HED7021` | [precompilation.md](../../precompilation.md) | Generator build-time **error** — an `[assembly: ExportFunctions(...)]` container that is not a public static class: the runtime throws `ArgumentException` from `FunctionRegistry.RegisterFrom`, so under the match principle the build fails the same way instead of silently skipping the container (generator↔engine code-sharing program, phase 3 / Q3.6) |
| `HED7022` | [precompilation.md](../../precompilation.md) | Generator build-time **error** — an `@profile(){{…}}` value that is neither `text` nor `html`: the runtime rejects the template with `HED2001`, while the emitter used to ignore the directive and pre-compile output the dynamic tier would never produce (generator↔engine code-sharing program, phase 1 D3) |
| `HED7023` | [precompilation.md](../../precompilation.md) | Generator build-time **error** — a type name several types answer to, unsettled by the template's `@using` imports: the runtime raises "the type name is ambigous" for the same input, so the build tier matches instead of picking a candidate (generator↔engine code-sharing program, phase 3 / Q3.5) |
| `HED7024` | [precompilation.md](../../precompilation.md) | Generator build-time **error** — a call-site fill of a region the definition declares private: the build-tier twin of `HED5019`, reproducing the runtime's retract-and-raise reaction to the `Private` region-fill verdict (generator↔engine code-sharing program, phase 1 D7 / Q1.3) |
| `HED7025` | [precompilation.md](../../precompilation.md) | Generator build-time **error** — a function call the shared overload ranker **proved** illegal: an ambiguous flat-Pareto front (`HED1013`) or no applicable overload (`HED1012`), over arguments the operand estimator could type. The generator used to compute that verdict and report nothing, so a provably illegal template built green and failed at first render. Reported only when no argument estimate is `Unknown` — an argument the generator cannot describe proves nothing and still degrades silently (generator↔engine code-sharing program, phase 4 D10 / Q8.1) |
| `HED7026`–`HED7027` | — **deliberately unclaimed**, ids stay free | Claimed on paper by a ruling that was then withdrawn on assessment (Q8.14): a use-site error and a declaration-site analyzer warning for an extension carrying both `[EncodeOutput]` and `[NotEncode]`. The combination is **not declarable** — `NotEncodeAttribute` is `AttributeTargets.Property` and `EncodeOutputAttribute` is `AttributeTargets.Class`, so co-declaring them on one type is `CS0592`, a C# compiler *error* in the extension author's own build, which is the surface the analyzer was to occupy at a stronger severity. In forged or IL-authored metadata both tiers evaluate the same `RenderTypeRules.Derive` and both answer `RenderType.Raw`, so no tier disagrees and there is nothing for a use-site error to close. **Reopening condition:** if `NotEncodeAttribute`'s targets ever widen, the contradiction becomes declarable and both ids become required; `ContradictoryEncodingAttributeTests` (build tier) and `TheNotEncodeVetoRowIsUnreachableFromAnyDeclaration` (run tier) are written to redden at that moment |
| `HED7028` | [precompilation.md](../../precompilation.md) | Generator build-time **warning** — an `@<<` import names a template by its registration key while that template also carries a `Name` item metadatum. `Name` is *additive* (Q8.25), so both spellings resolve and this is advice on the preferred one, not a fault: the first implementation made `Name` an override and broke path imports outright. A genuinely new fault class — every other `HED70xx` key diagnostic reports something unusable, this one reports something that works (generator↔engine code-sharing program, phase 5 / Q8.25) |
| `HED7029` | — **deliberately unclaimed**, id stays free | Reserved by the Q8.28 ruling for faults exposed by validating a `Precompile="false"` item's key/name metadata, then not needed: a malformed `Key`, a malformed `Name` and an already-taken `Name` are all instances of `HED7004`'s "this item's explicit key metadata is unusable" class, and the import advisory is `HED7028`. Two further candidates were considered and declined — "this item declares `Name` but is opted out, so the name is a build-time import spelling only" (that pairing is the feature's *intended* shape, so a warning on it is noise) and the cross-assembly name/key collision, which the build tier structurally cannot see and which took the **runtime** id `HED7104` instead |
| `HED7030` | [precompilation.md](../../precompilation.md) | Generator build-time **warning** — a type the engine binds by reflection but generated code in the consumer's assembly may not name: a model type, a member on one, or a bound host extension: an `internal` type or an `internal` member in a *referenced* assembly. Roslyn imports from metadata only what the importing assembly could legally name, so the emitter's symbol model shows an internal member as simply absent, and the build reported the same `HED7008` **error** it reports for a typo — a failed build over a template the engine renders. Distinguishing them needs a second view of the same references opened with `MetadataImportOptions.All`: a member the engine's own visibility policy accepts *there* and not here is hidden, not missing, and the template degrades to the dynamic tier under this warning instead. An internal model **type** never reached `HED7007` at all — types are imported from metadata regardless of accessibility — so the emitter pre-compiled a cast it could not write and the consumer's build failed on a wall of `CS0122` against generated `.g.cs`, with no Heddle id and no `.heddle` position. One id covers both: one situation, one shape. The same id, and the same degrade, later took `[Obsolete(…, error: true)]` on a model type or member: reflection ignores `[Obsolete]` outright, so the engine renders while every generated mention of the name is a `CS0619` in the consumer's build — the same class of "a name the emitter may spell and the consumer's compiler will reject". The **warning** form is deliberately not degraded: it is a note to the author rather than a refusal, taking every deprecated model off the precompiled tier would be a large silent cost, and the generated file's blanket `#pragma warning disable` already keeps `CS0618` out of the consumer's build. The same id later took a third position, the **bound extension's own type**: the engine's discovery filters a type on the extension interface and the name attribute and instantiates it with `Activator.CreateInstance`, so a non-public or error-obsolete extension registers and renders, while the field declaration and the `new` the emitter writes for it are `CS0122`/`CS0619`. The question is asked once, before any of the three writers allocates a field, so a role extension routed through the branch-emission path is covered by the same check. It is deliberately **not** asked of an extension's declared `[Prop]` type: nothing on the parameter path spells one |
| `HED7031` | [precompilation.md](../../precompilation.md) | Generator build-time **warning** — the catch-all for an emitter decline that has no more specific channel: embedded C# outside `FullCSharp` expression mode, or a call site that full-overrides a definition's body region. The diagnostic carries the decline's **class** beside its sentence, in the `HeddleRefusalCategory` diagnostic property, off the emitter's `RefusalCategory` enum (seventeen members, three of them the only legitimate end-state kinds — a CLR/csc wall, a value the build cannot know, an engine failure the degrade reproduces — and the rest operational groupings, each the retire-target of a capability). A category is not a second id: it is not registry-claimed, it is not surfaced as a `HED` number, and it exists so a coverage regression is measurable by class rather than by message substring, which is what `DifferentialHarness.ExpectDegrade(gen, key, category, reason)` pins. The id exists because the decline was previously **silent and unobservable**. The emitter computed an `UnsupportedReason` on every such path; `HeddleTemplateGenerator`'s `if (result.Emitted) … else if (result.IsMarker)` had no final `else`, so the reason was discarded and the template produced no source, no manifest row and no diagnostic. The only external evidence was a manifest missing a row nobody was counting, which meant a project could believe a template was precompiled while the engine served it from the dynamic path on every request — the failure mode `HED7014` and `HED7030` each avoid for *their* causes by degrading loudly. Deliberately a **warning** and not an error: falling back is a supported mode and output is unaffected, since both tiers are parity-checked. Projects for which precompilation is a requirement rather than an optimisation promote it with MSBuild's own `<WarningsAsErrors>HED7031</WarningsAsErrors>` rather than a second Heddle-specific switch threaded through `GlobalConfig`. The run-time half of the same signal is the benchmark harness's `gate-precompiled` verb, which fails when any protocol workload lacks a real precompiled entry |
| `HED7032` | [precompilation.md](../../precompilation.md) | Generator build-time **error** — a template carrying both an in-file `@model` directive and `ModelType` item metadata whose spellings resolve to **different types**. The runtime reads only the directive, so quietly preferring either spelling would let the two tiers type one template differently; agreement is required instead (equal spellings, or different spellings resolving to the same symbol, raise nothing). A metadata spelling that resolves to nothing is not this fault — it draws the same `HED7007`/`HED7023` family a non-resolving directive spelling draws. A build-only id under the same-fact-same-id rule: the engine never sees item metadata, so no engine-compile-time counterpart exists to forward |
| `HED7033` | [precompilation.md](../../precompilation.md) | Generator build-time **warning** — a bound extension declares `[PrecompileUnsupported]`, the extension author's own statement that its compile-time behaviour cannot be reproduced from a static initializer. The declared reason is carried **verbatim** into the message: the sentence a template author can act on is the extension author's, and paraphrasing it would put the build between them. Cost is **one call site** — the call binds dynamically (it renders by compiling its own source text at first render) while the rest of the template stays precompiled — which is why this is not a second spelling of `HED7031`: that id reports a template leaving the tier, this one reports a call leaving it. Its primary population is a hook that walks the enclosing document through `InitContext.ParseContext.Tokens`/`SubContexts`: the token stream *is* the enclosing document's parse tree, no call site can carry one, and a synthesized parse context is the only place the binding seam presents an empty member rather than an absent one — so it is the one thing a hook can be told about itself that the seam would otherwise get silently wrong. Read on **both** sides, and that is the row rather than an implementation note: the build reads the declaration off the symbol, and `PrecompiledRuntime.Init` reads it off the **live** type, so an extension package that adds the declaration after a consumer's assembly was built still falls back instead of binding through a seam its author has disowned. Deliberately a warning and not silence: a declaration that costs a tier should be visible to the template author who is paying for it, and `<WarningsAsErrors>` is available to a project for which it is not acceptable |
| `HED7034` | [precompilation.md](../../precompilation.md) | Generator build-time **note** (`Info`) under `HeddleObserveEngine=Auto`, and the same id as an **error** under `Strict` — the build could not observe a real engine compile of the templates it is emitting, so a body whose model type only an extension's hook can supply is emitted type-agnostically instead of with a direct cast. It is deliberately not a warning in either mode: observation is a **typing optimisation**, the template still precompiles through the engine's own zero-allocation accessors, and the rendered bytes are identical either way — so the default severity has to be quieter than `HED7031`, which reports a template genuinely leaving the tier. `Strict` exists for the opposite reason: a CI leg that cannot observe would otherwise emit **different sources** from a developer machine that can, silently, and a project that cares about that asks for the failure rather than diffing generated code. Reported **once per compilation**, not once per template — a build has one fact to learn here, not one per file — and suppressed entirely under `Auto` when no observe directory was configured at all, because a build that never had the option is not a build that tried and failed. The third `HeddleDiagnosticSeverity` member exists for this row and only for it: every other catalogued id describes a template, and this one describes the build |
| `HED7101`–`HED7103` | [precompilation.md](../../precompilation.md) | Runtime registration/fallback |
| `HED7104` | [precompilation.md](../../precompilation.md) | Precompiled-runtime registration **warning** — a template's registered `Name` could not become a lookup spelling: another *registered* template already answers to it (as its key or as its own name), or the name is not a spelling the shared `TemplateKey` rule accepts at all (Q8.32(b) — one id for both, because from the host's side they are one situation and the remedy is the same). Reported through `OnFallback` (`PrecompiledFallbackReason.RegisteredNameUnavailable`), never a throw: the template stays registered under its key and only the addition is lost. A **runtime** id because the collision spans assemblies — within one compilation the build tier reports the same fault as `HED7004`, but a referenced assembly's manifest rows live in a `GetTemplates` method body, i.e. IL rather than symbol metadata, so nothing at build time can see them (generator↔engine code-sharing program, phase 5 / Q8.30) |
| `HED8xxx` | — reserved, none claimed | Sink APIs throw host errors, no compile diagnostics |
| `HED9001` | Engine feature switches | C# tier disabled by the trimming feature switch (internal ID, not public `HeddleDiagnosticIds` surface) |

## Cross-spec amendments ledger

The amendment mechanism. Later work may amend an earlier spec's recorded artifact; the
amendment is proposed as a ledger entry (with the evidence that forced it), ratified by
the maintainer, and **implemented by the effort that makes it** — the earlier spec's text
is never silently retrofitted. The current end-state of any decision is therefore *its
spec plus the ledger*.

**The accumulated ledger (E1–E28) is closed.** Both programs that fed it are complete;
every entry with ongoing force is folded into the program records above under its
original id, and the full entries remain readable at
`git show c4691266:docs/spec/records.md`. The mechanism survives the file: a future
amendment is recorded directly in the owning spec (or here, for cross-cutting facts) as a
dated note carrying the same three parts — what changed with evidence, who made it, what
it amends.
