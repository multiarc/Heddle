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

## D10 — Documentation authority is mapped, and it is conditional

**Decision.** Two parts: *which* document arbitrates a claim, and *when* it gets to.

**(a) The mapping.** A claim block has exactly one normative home, and it is the home named in
the table below. Where a claim spans two homes, the one whose *diagnostics* the claim can produce
owns it — that is the tie-break, because a diagnostic has a registry owner and prose does not.

| Claim block | Normative home | Also gated by |
| --- | --- | --- |
| Native-expression semantics (operators, coercion, arity, overloads) | [native-expressions.md](../../native-expressions.md) | `HED1001`–`HED1017` registry rows |
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
that did both is recorded as [2.1 window item 10](../records.md#the-21-breaking-window--as-shipped-record).
`PrecompiledTemplates.Register` has the same shape. The engine ships no module initializer — one exists in
`src/`, in the generator integration suite, where it is a **test host** registering itself, which is the
pattern this decision prescribes rather than an exception to it.

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
| `HED2004` | Shipped in 2.0.0; this registry row is the live normative home | HTML-context encoding lint (`MissingContextEncoder`) — warning; bare `@(value)` in an attribute/`<script>`/URL position under an explicitly declared `Html` profile without the matching `@attr`/`@js`/`@url` encoder |
| `HED3001`–`HED3005` | [built-in-extensions.md](../../built-in-extensions.md#branch-sets) | Branch sets (incl. the `HED3005` drift warning) |
| `HED4001`–`HED4002` | [built-in-extensions.md](../../built-in-extensions.md) | Ergonomics (`range` step, double-render) |
| `HED4005` | Shipped in 2.0.0; this registry row is the live normative home | `{{ x }}`-in-text misread lint (`LiquidStyleInterpolationMisread`) — warning; a bare `{{ identifier }}` / `{{ dotted.path }}` in literal text, suggesting `@(…)` |
| `HED4003` | Shipped in 2.0.0 | `@import()` **removal error** — the legacy include is removed in 2.0.0; positioned at the call, severity error, naming `@<<`/`@partial`. The normative message/trigger/position live in [language-reference.md](../../language-reference.md#imports---) and the [2.0 window record](../records.md#the-20-breaking-window--as-shipped-record) (item 6) |
| `HED4004` | [language-reference.md](../../language-reference.md#imports---) | `@<<` composition import nested inside a subtemplate (not top-level); import skipped, positioned at the `@<<` directive |
| `HED4006` | [language-reference.md](../../language-reference.md#imports---) | `@<<` composition import cycle — an import reaches a document already being imported; the repeated import is skipped and the chain named, positioned at the `@<<` directive |
| `HED4007` | [language-reference.md](../../language-reference.md#imports---) | Expression, chain, or block nesting too deep to build; reported instead of exhausting the stack, positioned at the document start |
| `HED4008` | [language-reference.md](../../language-reference.md#imports---) | `@<<` composition imports expanded past the per-parse total — an acyclic graph that reaches the same document from several places re-parses it once per path and multiplies out; the remaining imports are skipped and the overflow is described once, positioned at the `@<<` directive that hit the bound |
| `HED5001`–`HED5018` | [language-reference.md](../../language-reference.md#props-nameprop-type--default) | Props & slots |
| `HED5019`–`HED5020` | Shipped in 2.0.0; this registry row is the live normative home | Named content regions (compile errors, fire only on the public-region surface): `HED5019` `RegionNotPublic` (a call-body override targets a callee's **private** region); `HED5020` `DuplicateRegionDeclaration` (two public regions with the same name — raised by upgrading the id-less `EnterDef` duplicate error). A region-override narrowing mismatch reuses the pre-existing id-less `WalkValidateDefinitionType` error (no new id); a typed-override member error reuses `HED0001` |
| `HED6xxx` | — reserved, none claimed | Tooling-only messages are not compile diagnostics |
| `HED7001`–`HED7016` | [precompilation.md](../../precompilation.md) | Generator build-time (incl. the `HED7016` drift warning) |
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

Entry format: **Amendment** (what changed, with evidence) | **Made by** (the amending
effort/spec) | **Amends** (the decision or artifact amended). Entries are append-only and
never renumbered or deleted.

The accumulated entries live in the
[historical records](../records.md#cross-spec-amendments-ledger); a spec adding an entry
appends it there in the same change.
