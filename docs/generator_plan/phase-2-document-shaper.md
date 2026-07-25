# Phase 2 — document shaper: fix the clamp drift, extract the shaping core

## Header

- **Status:** proposed — not started
- **Goal (one line):** One implementation of every byte-affecting document-shaping machine —
  `WidenToWholeLine` (drift fixed first, shippable alone), the five position-rebasing passes, the
  branch-set strip machine, generic piece slicing, and the region-fill matching rule — shared
  between `HeddleCompiler.CompileBody` and the generator via linked sources under
  `src/Heddle/Language/`, proven byte-neutral by the existing golden and differential suites.
- **Depends on:** No hard dependency for WI1–WI5 (the clamp fix and the core extraction stand
  alone). [Phase 1 — template emitter](phase-1-template-emitter.md) owns two co-owned seams this
  plan only consumes or forward-references: the zero-output attribute/marker design
  ([02 F4](../research/generator-code-sharing/02-document-shaper.md), consumed by WI7) and the
  `ParticipantScan` fix ([02 F6](../research/generator-code-sharing/02-document-shaper.md),
  referenced only). Phase 1's piece-emission work in turn consumes this phase's `SlicePieces<T>`
  (WI5) — declared order per [D5](../spec/common/cross-cutting-decisions.md#d5--implementation-follows-the-owning-plans-declared-order)
  puts WI5 before that Phase 1 item.
- **Changes an externally-visible contract:** no rendered-byte or diagnostic change. The WI1 clamp
  fix changes **which tier renders** certain templates (silently-degraded templates start
  precompiling); the Back-compat section analyzes each sub-case and shows why this needs no
  breaking window under [D2](../spec/common/cross-cutting-decisions.md#d2--breaking-changes-land-only-in-ratified-breaking-windows).
  Everything else is internal restructuring gated on byte-neutrality.

## Goal

The generator's `src/Heddle.Generator/Emit/DocumentShaper.cs` is, by its own header comment, "the
emitter-side reimplementation … of the back-end document-shaping machines that live in
`HeddleCompiler.CompileBody`" — 398 lines of hand-maintained copy of the code that decides every
static-piece boundary, and therefore every byte, of both render tiers. The research for this area
([02 — document shaper](../research/generator-code-sharing/02-document-shaper.md)) verified that
the copy has **already drifted**: the runtime's `WidenToWholeLine`
(`src/Heddle/Runtime/HeddleCompiler.cs:443-472`) carries a defensive bounds clamp the generator's
copy (`src/Heddle.Generator/Emit/DocumentShaper.cs:375-396`) lacks, and the resulting
`IndexOutOfRangeException` is swallowed by the generator's per-template
`catch (Exception)` (`src/Heddle.Generator/HeddleTemplateGenerator.cs:249-252`) — affected
templates silently lose precompilation with no diagnostic and no failing test. This is drift #1 in
the program-wide live-drift table
([07 — synthesis](../research/generator-code-sharing/07-recommendations.md)).

This phase does two separable things, in that order:

1. **Fix the live bug** — port the runtime's clamp into the generator's `WidenToWholeLine`,
   verbatim, with a fixture reproducing the overshooting-position input class the clamp was added
   for. This work item is independently shippable and precedes everything else.
2. **Remove the duplication class** — create the shared file `src/Heddle/Language/DocumentShaping.cs`
   (auto-linked into the generator by the existing `..\Heddle\Language\**\*.cs` glob at
   `src/Heddle.Generator/Heddle.Generator.csproj:50`) holding the trim predicate, the five
   position-rebasing machines, a safe `ApplyRemove`/`Replace` pair, the branch-set strip machine
   with a shared `BranchKind` enum (including `Participant`), and the generic `SlicePieces<T>`
   segmentation; plus `src/Heddle/Language/RegionFillResolver.cs` holding the four-step region-fill
   matching rule behind a verdict enum. Both call sites — `HeddleCompiler.CompileBody` and
   `DocumentShaper.Shape` — become thin drivers over the shared core, so the next offset-arithmetic
   fix can no longer land on one side only.

Throughout, **the runtime is the authoritative behavior**: where the copies disagree, the runtime's
semantics win (the clamp exists there for a reason — its comment documents the exact failure mode),
and the extraction itself must be provably byte-neutral: golden corpus, differential suite, and
generator snapshots unchanged before/after is an acceptance gate, not an aspiration.

## Non-goals / scope boundary

- **No behavior redesign.** Every machine moves with its current runtime semantics; no whitespace
  rule, trim predicate, or strip rule is improved, tightened, or "cleaned up" in flight. A
  behavioral idea discovered during extraction becomes a register entry, not a change.
- **The zero-output attribute/marker design is Phase 1's** ([02 F4](../research/generator-code-sharing/02-document-shaper.md)).
  This phase keeps `DocumentShaper.Shape`'s injected `isZeroOutput` delegate as the consumption
  seam and adds a lockstep guard over today's hardcoded four-name list
  (`src/Heddle.Generator/Emit/TemplateEmitter.cs:208-215`); replacing that list with the
  attribute-driven classifier is Phase 1 work that plugs into the seam this phase preserves.
- **The `NeedsLocals`/`HostsParticipant` leftmost-only bug is Phase 1's**
  ([02 F6](../research/generator-code-sharing/02-document-shaper.md), fixed by Phase 1's
  `ParticipantScan`). This phase does not touch `ScanHostsParticipant`
  (`src/Heddle.Generator/Emit/TemplateEmitter.cs:1404-1416`) or the element-walk site
  (`TemplateEmitter.cs:371-375`).
- **Emitter-side adoption of `RegionFillResolver` is Phase 1's.** This phase delivers the shared
  core and the runtime-side adoption (`BuildRegionFillScope`); rewiring
  `TryBuildGeneratorFillScope` (`TemplateEmitter.cs:1300-1339`) onto it is a Phase 1 item with a
  forward pointer here (non-normative, per [D5](../spec/common/cross-cutting-decisions.md#d5--implementation-follows-the-owning-plans-declared-order)).
- **No `ExStringBuilder` changes.** `src/Heddle/Strings/ExStringBuilder.cs` stays `unsafe`,
  runtime-only, and untouched; the shared file gets its own safe equivalents (D3). Its other
  consumers are out of scope.
- **No diagnostics changes.** All HED3001–HED3005 branch diagnostics, HED5019, and their message
  texts, positions, and trigger conditions are preserved exactly; the runtime's diagnostic
  machinery is re-hosted (as strip-machine observer callbacks), never re-worded. No new `HED*` ID
  is claimed; the [registry](../spec/common/cross-cutting-decisions.md#claimed-diagnostic-ids-registry)
  is untouched.
- **The generator's silent `catch (Exception)` degrade** (`HeddleTemplateGenerator.cs:249-252`)
  stays as-is. Making emitter defects visible (a build-time info/warning on degrade) is a
  diagnostics-area concern ([06](../research/generator-code-sharing/06-diagnostics-utilities.md))
  and is recorded as an open question for ownership, not silently absorbed here.
- **No grammar change, no public API change.** Everything added is `internal`; the grammar-stability
  gate applies in its default "no diff" form.

## Design direction

### D1 — Fix first: the clamp is ported verbatim, and ships independently of any extraction

- **Decision.** WI1 copies the runtime clamp — `StartIndex` clamped into `[0, document.Length]`,
  `endIndex` clamped to `≤ document.Length` and `≥ startIndex`, the `right >= document.Length` EOF
  test, and the **clamped-span** (not original-block) returns on both "not whole-line" exits — from
  `HeddleCompiler.WidenToWholeLine` (`src/Heddle/Runtime/HeddleCompiler.cs:443-472`) into
  `DocumentShaper.WidenToWholeLine` (`src/Heddle.Generator/Emit/DocumentShaper.cs:375-396`),
  making the two implementations extensionally equal on every input. It lands as its own change,
  before WI2+, so the live bug's fix is never hostage to the extraction's review.
- **Rationale.** This is the highest-priority drift in the whole program
  ([07 drift #1](../research/generator-code-sharing/07-recommendations.md)); the runtime is
  authoritative and its clamp comment documents the exact input class ("an earlier widened removal
  on the same line can leave a later block's stored position overshooting the (now shorter)
  working document"). Shipping the fix first also gives the extraction a clean baseline: after WI1,
  "extract" means "move identical code", with no behavior change hiding inside a move.
- **Alternatives rejected.** Fixing only inside the extraction (couples a one-screen bug fix to a
  multi-file refactor's timeline); clamping in the generator's callers instead of the predicate
  (diverges the two implementations further — the opposite of this program's point).
- **Grounding.** [02 F1](../research/generator-code-sharing/02-document-shaper.md); both sources
  re-read during planning — the three verified divergences (unclamped dereference at
  `DocumentShaper.cs:378` and `:384-394`; `==` vs `>=` EOF test at `:386` vs
  `HeddleCompiler.cs:462`; original-block vs clamped-span exits at `:381`/`:395` vs
  `HeddleCompiler.cs:457`/`:471`) all hold at head.

### D2 — Shared home: `src/Heddle/Language/DocumentShaping.cs`, linked by the existing glob

- **Decision.** All directly-sharable shaping code lands in one new file,
  `src/Heddle/Language/DocumentShaping.cs` (`internal static class DocumentShaping`, block-scoped
  namespace `Heddle.Language`), picked up by the existing generator link glob
  (`src/Heddle.Generator/Heddle.Generator.csproj:50`, `..\Heddle\Language\**\*.cs`) with **zero
  csproj edits**. Constraints honored by construction: netstandard2.0-clean (the generator's only
  TFM), no Roslyn types, no `unsafe`, no reference to any type outside the already-linked set
  (`ParseContext`, `OutputChain`, `RawOutputItem`, `DefinitionsBlock`, `BlockPosition`, plus BCL).
  `RegionFillResolver` gets its own sibling file, `src/Heddle/Language/RegionFillResolver.cs`
  (D8) — same linkage, same constraints.
- **Rationale.** The mechanism is proven — `DefinitionMaterializer.cs` and
  `RegionFillCandidate.cs` already live exactly this way, and the glob makes the sharing
  self-maintaining. A `Heddle.Shared` project was weighed at the synthesis level and deferred
  until the shared set outgrows the linked-file pattern
  ([07 — proposed layout](../research/generator-code-sharing/07-recommendations.md)); this phase
  adds two files and does not approach that trigger.
- **Alternatives rejected.** Explicit per-file `<Compile Include>` lines (the glob already exists;
  hand-maintained lines are how link sets rot); splitting each machine into its own file (seven
  tiny files whose pass ordering — itself a rule — would live nowhere).
- **Grounding.** Glob verified at `Heddle.Generator.csproj:50` (excludes only
  `DocumentParser.Runtime.cs`); `Heddle.Generator.csproj:4` confirms the single
  `netstandard2.0` TFM; `src/Heddle/Language/DefinitionMaterializer.cs` header documents the
  both-backends precedent.

### D3 — Safe `ApplyRemove`/`Replace` live in the shared file; `ExStringBuilder` stays runtime-only

- **Decision.** `DocumentShaping` carries its own safe pair:
  `ApplyRemove(BlockPosition, ref string) → int` implemented over `string.Remove` (one allocation —
  the same count as the unsafe path), and `Replace(int start, int length, string replacement,
  string source) → string` implemented as a single `Substring`/`Substring` concat (semantically
  equal to `ExStringBuilder.Replace`, `src/Heddle/Strings/ExStringBuilder.cs:325-358` — already
  proven by the generator's `ReplaceSpan` at `DocumentShaper.cs:184-185`). The shared passes call
  the safe pair on **both** sides; the runtime's compile path stops calling
  `ExStringBuilder.ApplyRemove`/`Replace` for shaping (its other consumers are untouched). No
  delegate parameterization.
- **Rationale.** `ExStringBuilder.cs` is `unsafe` and cannot be linked into the generator — this is
  the one concrete blocker the research identified for extracting the passes
  ([02 F2](../research/generator-code-sharing/02-document-shaper.md), minors). A delegate seam
  ("inject the remover") would keep two implementations alive to preserve a compile-path
  micro-difference; the coding standards' precedence puts simplicity above unmeasured performance,
  and this is compile-once code, not the render path. `string.Remove` makes `ApplyRemove`
  allocation-equivalent; only `ReplaceRawOutput`'s replace differs (a concat versus one fixed
  buffer), bounded by the raw-output count per template.
- **Alternatives rejected.** Delegate-injected remover (indirection that preserves the duplication
  the phase exists to kill); making `ExStringBuilder` safe or split (out of scope, touches a hot
  utility with other consumers); `StringBuilder`-based replace (more allocation than the concat
  for no gain).
- **Revisit trigger.** The WI9 benchmark gate (testing-standards gate 4) shows a compile-path
  regression beyond BenchmarkDotNet's reported error → ratify a maintainer-reviewed switch of the
  runtime driver to an injected `ExStringBuilder`-backed replace for the `ReplaceRawOutput` pass
  only, recorded in the owning spec.
- **Grounding.** `ExStringBuilder.cs:325-365` read (guarded `unsafe` copy loops;
  `Heddle.csproj:13` sets `AllowUnsafeBlocks` project-wide); [coding-standards — precedence](../spec/common/coding-standards.md#precedence-when-principles-conflict);
  memory note "don't trust local CLI measurements" — the gate runs under BenchmarkDotNet, not
  wall-clock spot checks.

### D4 — The five rebasing machines move wholesale; the drivers stay per-side; the pass order is a pinned contract

- **Decision.** `ShiftBySkippedTokens`, `TrimHiddenRemnantLines` (+ its `ShiftListsAfter` helper),
  `RemoveDefinitions`, `ReplaceRawOutput`, and `RemoveEmptyItem` move into `DocumentShaping` as
  `internal static` methods with the **runtime's** bodies (post-WI1 the copies are token-equal
  modulo brace style; the [extraction map](phase-2-document-shaper-extraction-map.md) pins each
  source pair). `WidenToWholeLine` moves with them. The two **drivers stay where they are**:
  `HeddleCompiler.CompileBody` keeps its sequence (`HeddleCompiler.cs:81-90`) because it interleaves
  runtime-only, byte-neutral diagnostic passes (`ScanBraceMisreads` at `:85`,
  `ScanHtmlContextLint` inside the chain loop) and decides zero-output *during* item compilation
  (`returnTypeChainedPrevious == null`, `:128-131`); `DocumentShaper.Shape`
  (`DocumentShaper.cs:49-81`) keeps its up-front sequence and its `Element`/`Result` surface. The
  shared pass order — shift → trim (when `TrimDirectiveLines`) → remove definitions → replace raw
  output → strip branch sets → remove zero-output chains — is documented in the shared file's
  header as a normative ordering contract, and a lockstep test asserts both drivers still invoke
  the shared passes in that relative order.
- **Rationale.** The passes are the knowledge that must never diverge (DRY as
  single-representation-of-knowledge, per the [coding standards](../spec/common/coding-standards.md#dry-applied));
  the drivers are legitimately different programs (one compiles items and raises diagnostics, one
  classifies eligibility for emission) and forcing them through one parameterized driver would need
  injection points for every runtime-only interleave — ceremony that obscures rather than shares.
  The pass ordering is itself a rule the research called out
  ([02 F2](../research/generator-code-sharing/02-document-shaper.md)), so it gets stated once and
  tested, not restated twice.
- **Alternatives rejected.** One shared `Shape` driver with hook delegates for the runtime's
  diagnostic interleaves (three hooks today, more tomorrow; the hooks would encode
  `CompileBody`'s structure into a shared signature); leaving `TrimHiddenRemnantLines` per-side
  because it composes `ShiftListsAfter` (they move together — the composition is part of the
  knowledge).
- **Grounding.** Pass tables and line ranges verified — see the
  [extraction map](phase-2-document-shaper-extraction-map.md); the copy-pasted bug-fix rationale
  comment (`DocumentShaper.cs:330-334` reproducing `HeddleCompiler.cs:519-528`) is the standing
  proof these are one piece of knowledge maintained twice.

### D5 — Branch strip: shared machine, shared `BranchKind` with `Participant`, diagnostics as an observer

- **Decision.** `DocumentShaping` gains the strip machine as the single implementation:
  - `internal enum BranchKind { Other, Opener, Continuation, Terminal, Participant }` — defined
    **only** in the shared file, replacing the generator's private four-value enum
    (`DocumentShaper.cs:87`) and the runtime's private `BranchBlockKind`
    (`HeddleCompiler.cs:182-189`). The runtime cannot gain a kind the generator misses, by
    construction.
  - `StripBranchSets(ParseContext, ref string workingDocument, Func<OutputChain, BranchKind>
    classify, IBranchStripObserver observer = null)` implementing the arm/collect/disarm state
    machine, `CollectGap`, and `ApplyGaps` with the runtime's semantics (including the
    non-positive-gap and bounds guards, `HeddleCompiler.cs:361-385`). `Participant` disarms —
    exactly the runtime's `HeddleCompiler.cs:285-288` — and is now an explicit case on both sides
    instead of falling into the generator's `default:` by accident.
  - `IBranchStripObserver` (or two delegate parameters if the interface reads heavier than it
    pays — the spec decides the exact shape) receiving the two event streams the runtime's
    diagnostics need: *block classified* `(chain, leftmostItem, kind)` in document order, and
    *gap collected* `(prevChain, nextChain, gap, gapText)`. The runtime's orphan state machine
    (HED3002/HED3003/HED3004), the `WarnIfMissingScopeChannel` HED3005 check, and the
    HED3001 non-whitespace-gap warning re-host as observer logic inside `HeddleCompiler` —
    verified sufficient because each is a pure function of those two event streams
    (`HeddleCompiler.cs:210-299` read: no diagnostic reads strip state beyond kind, leftmost,
    and gap text). The generator passes no observer.
  - Classification stays per-side behind the `classify` delegate: the runtime maps
    `TemplateFactory.TryGetExtensionType` + `GetBranchRole()` + `[ScopeChannel]`
    (`HeddleCompiler.cs:301-326`, role-wins-over-Participant preserved); the generator maps its
    injected `roleOf`/`isDefinition` (Roslyn-backed `ExtensionBinder`) **and adds the
    `Participant` mapping for `[ScopeChannel]` non-role extensions** — byte-neutral today (both
    `Participant` and `Other` disarm) but structurally aligned. The R8 definition-first guard
    stays inside each classifier. `BranchKind` deliberately does not depend on either `BranchRole`
    enum (`src/Heddle/Attributes/BranchRoleAttribute.cs:11` vs the generator mirror at
    `src/Heddle.Generator/Emit/ExtensionBinder.cs:10`), so this phase neither needs nor blocks the
    Tier-1 `BranchRole` link owned elsewhere.
- **Rationale.** The split the research verified ([02 F3](../research/generator-code-sharing/02-document-shaper.md))
  is deliberate — byte-affecting strip in both tiers, HED300x runtime-only — but the strip half is
  duplicated verbatim and the `Participant`/`Other` collapse is "benign only because both disarm
  today". Sharing the machine plus the enum makes the benign coincidence a checked invariant; the
  observer keeps the diagnostics exactly where the architecture wants them (runtime-only) without
  copying the machine they observe.
- **Alternatives rejected.** Sharing only a transition table (`(BranchKind, armed) → action`) and
  keeping two loop skeletons (still two machines to diverge); moving HED300x emission into the
  shared file behind "diagnostic sink" abstractions (drags `CompileScope`/warning types toward the
  shared surface for no generator benefit); leaving the generator's classifier without
  `Participant` (preserves the exact latent drift the research flagged).
- **Grounding.** [02 F3](../research/generator-code-sharing/02-document-shaper.md); runtime
  machine re-read in full (`HeddleCompiler.cs:210-408`), generator machine re-read in full
  (`DocumentShaper.cs:87-174`).

### D6 — `SlicePieces<T>`: one generic segmentation walk, consumed by both tiers (and Phase 1)

- **Decision.** `DocumentShaping` gains
  `SlicePieces<T>(IReadOnlyList<T> elements, Func<T, BlockPosition> position, string document,
  Action<string> onPiece, Action<T> onElement)` implementing the piece walk exactly as both copies
  have it: leading literal only when `StartIndex > offset`, element callback always, offset
  advanced past the element, trailing literal when `document.Length > offset`.
  `RuntimeDocument.GetDocumentPieces` (`src/Heddle/Runtime/RuntimeDocument.cs:95-130`) and the
  emitter's body walk (`src/Heddle.Generator/Emit/TemplateEmitter.cs:359-401`) both rewire onto
  it — the runtime builds its `DataProcessor` pairs in the callbacks, the emitter keeps its
  per-element work (profile lookup, `HostsParticipant`, `BuildCall`, early-out on degrade) in the
  `onElement` callback with its existing early-return semantics preserved (the spec pins how the
  degrade path exits the walk — an exception-free short-circuit variant returning `bool` from
  `onElement` is acceptable if the `Action` shape can't express it cleanly).
- **Rationale.** This is *the* byte-parity contract — the emitter's `P0..Pn` constants must equal
  the runtime's `DataProcessor.Piece` strings exactly
  ([02 F5](../research/generator-code-sharing/02-document-shaper.md)) — and Phase 1's emitter work
  builds on the same walk, which is why this phase owns the shared core. The generic-with-callbacks
  shape is the smallest thing both element types (`IDataProcessor` with `Position`;
  `DocumentShaper.Element`) can share without a common interface.
- **Alternatives rejected.** A returned piece list instead of callbacks (forces an intermediate
  allocation per body on the runtime path and per template in the generator, and cannot host the
  emitter's early-out); unifying the element types (would drag runtime types into the shared
  surface).
- **Grounding.** Both walks re-read and confirmed semantically identical including the
  `StartIndex == offset` no-leading-piece case and the trailing remainder
  (`RuntimeDocument.cs:101-128`; `TemplateEmitter.cs:365-366, 396, 399-400`).

### D7 — Region-fill matching: shared four-step rule, verdict enum, reactions stay per-side

- **Decision.** `src/Heddle/Language/RegionFillResolver.cs` holds the matching rule both backends
  currently duplicate around the already-shared `DefinitionMaterializer.Materialize` leaf:
  - `internal enum RegionFillVerdict { Matched, Dangling, Private, DefaultMissing }`
  - `Resolve(IReadOnlyList<RegionFillCandidate> candidates, ParseContext origin,
    TryLookupRegion lookup, DefinitionItem calleeDefinition, Action<RegionFillCandidate,
    RegionFillVerdict, DefinitionItem> onVerdict)` where
    `delegate bool TryLookupRegion(string name, out bool isPublic)` abstracts the region table.
    The shared core performs: origin-identity filter (`candidate.Origin != origin` → skip),
    region lookup (miss → `Dangling`), public gate (→ `Private`), region-default fetch from
    `calleeDefinition.Context?.DefinitionsBlock?.Definitions` (miss → `DefaultMissing`), and on
    success materializes via `DefinitionMaterializer.Materialize`, reporting
    `(candidate, Matched, materialized)`.
  - **Runtime adoption (this phase):** `BuildRegionFillScope`
    (`src/Heddle/Runtime/HeddleCompiler.cs:1686-1735`) becomes a driver: its lookup delegate wraps
    `ResolveRegionLayoutCached`/`RegionLayout.TryGet` (lazily, preserving the current
    resolve-only-on-first-origin-match behavior), and its verdict callback reproduces today's
    reactions exactly — `Dangling`/`DefaultMissing` keep the parse-emitted error, `Private`
    retracts and raises HED5019 once per candidate (`PrivateOverrideReported` unchanged),
    `Matched` retracts and stores the fill. `RegionLayout` itself does not move (it resolves
    `ExType` via reflection — verified at `src/Heddle/Runtime/Expressions/RegionLayout.cs`).
  - **Emitter adoption is Phase 1's** (forward pointer): `TryBuildGeneratorFillScope`'s reactions
    (`Dangling`/`Private`/`DefaultMissing` → un-precompile with reason; `Matched` → fill +
    `_consumedCandidates`) map one-to-one onto the verdicts.
- **Rationale.** The failure reactions differ **by design** (retract+HED5019 vs degrade-to-dynamic)
  and must stay per-side; the *decision* is one rule written twice with different table
  representations ([02 F7](../research/generator-code-sharing/02-document-shaper.md)). A verdict
  enum makes the safe-direction asymmetry explicit and future-proofs the real exposure the
  research named: if the runtime's region table ever stops being a flat scan, the generator
  inherits the change through the shared rule instead of silently rejecting fills the runtime
  accepts.
- **Alternatives rejected.** Sharing the lookup too (impossible — `RegionLayout` is
  reflection-bound); returning a verdict list instead of a callback (forces allocation and loses
  the runtime's lazy layout resolution); folding the reactions into the shared core behind more
  delegates (the reactions are the *sides'* identity — sharing them shares nothing and couples
  everything).
- **Grounding.** Both call sites re-read in full (`HeddleCompiler.cs:1686-1748` including
  `RetractCandidateError`; `TemplateEmitter.cs:1300-1339`); `DefinitionMaterializer.cs` header
  confirms the both-backends contract of the leaf.

### D8 — Zero-output classification: keep the seam, guard it, reference Phase 1

- **Decision.** `DocumentShaper.Shape`'s `isZeroOutput` delegate parameter is preserved unchanged
  as the consumption seam ([02 F4](../research/generator-code-sharing/02-document-shaper.md) is
  co-owned: Phase 1 designs the `[ZeroOutput]`/`[Directive]` attribute; this phase owns the
  shaper's consumption of the classification). Until Phase 1's classifier lands, this phase adds a
  **lockstep guard test** in `src/Heddle.Tests` pinning the generator's four-name list (`model`,
  `using`, `import`, `profile` — `TemplateEmitter.cs:208-215`) against the runtime ground truth:
  for each built-in extension, compile a minimal chain and assert
  (`returnTypeChainedPrevious == null`) ⇔ (name ∈ list), modeled on
  `src/Heddle.Tests/DefaultFunctionLockstepTests.cs` ("the table forgot a built-in is a red
  build"). When Phase 1's attribute ships, the guard's list side switches to the attribute-derived
  set and the hardcoded list dies in Phase 1's change, not this one.
- **Rationale.** The drift here is structurally one-way (a new zero-output built-in changes runtime
  output with zero generator signal); the shaper can't fix that — only the classification source
  can — but it can refuse to let the two sources disagree silently, today, with a cheap test.
- **Alternatives rejected.** Moving the four-name list into the shared file now (creates a third
  representation Phase 1 would immediately delete); blocking this phase on Phase 1's attribute
  (the shaping extraction doesn't need it).
- **Grounding.** Runtime computation verified at `HeddleCompiler.cs:101, 128-131`; generator list
  and injection verified at `TemplateEmitter.cs:208-215, 357-358`; lockstep precedent
  `DefaultFunctionLockstepTests.cs` read.

### D9 — Fates: `DocumentShaper.cs` becomes a thin adapter; `CompileBody`'s shaping half becomes calls

- **Decision.** `src/Heddle.Generator/Emit/DocumentShaper.cs` **survives as a thin adapter**: it
  keeps the `Element`/`Result` types, the `Shape` driver (sequencing shared passes, applying
  `isZeroOutput`, building the element list including the default-chain handling), and its
  generator-facing doc comment — but contains **no pass bodies**: `WidenToWholeLine`,
  `ApplyRemove`, `ReplaceSpan`, the five machines, `ClassifyBranch`'s enum, `StripBranchSets`,
  `CollectGap`, and `ApplyGaps` are deleted in favor of `DocumentShaping` calls (the classifier
  lambda mapping `roleOf`/`isDefinition` to `BranchKind` stays — it is generator representation,
  not shared knowledge). Symmetrically, `HeddleCompiler` deletes its private copies of
  `WidenToWholeLine`, `TrimHiddenRemnantLines`, `ShiftListsAfter`, `ShiftBySkippedTokens`,
  `RemoveDefinitions`, `ReplaceRawOutput`, `RemoveEmptyItem`, `BranchBlockKind`,
  `CollectGap`/`ApplyGaps`, and the strip loop's skeleton; `CompileBody`'s shaping half becomes
  shared-core calls with the diagnostics re-hosted per D5. The completion check is mechanical:
  after WI3–WI5, neither file contains a method body that also exists in `DocumentShaping`.
- **Rationale.** Deleting `DocumentShaper.cs` outright would push the `Element`/`Result` adapter
  surface and the eligibility-classification driver into `TemplateEmitter` — a worse home
  (single-responsibility: the emitter consumes shaping, it shouldn't host it). Keeping pass bodies
  "temporarily" is how this drift happened; the no-duplicate-bodies check makes the end state
  verifiable.
- **Alternatives rejected.** Deleting the adapter (moves, doesn't remove, the seam); keeping both
  copies with a comparison test instead of extraction ("test the twins" was the pre-research
  status quo — Finding 1 proves it insufficient, since no test exercised the overshoot class on
  the generator side).
- **Grounding.** [02 — headline and F2 extraction notes](../research/generator-code-sharing/02-document-shaper.md).

### D10 — Minor dispositions: delete `DocumentsCache.cs`; document (not fix) the empty-default-chain asymmetry

- **Decision.**
  1. `src/Heddle/Runtime/DocumentsCache.cs` — verified 100% commented-out (every line of the class
     body, lines 2 onward) — is **deleted** in WI8. Dead code in the runtime's shaping
     neighborhood is noise for every future reader of this area; git history keeps it.
  2. The empty-default-chain asymmetry — `DocumentShaper.cs:72-78` skips default chains with
     `Chain == null || Count == 0`, while `CompileBody` (`HeddleCompiler.cs:143-177`) still adds a
     zero-length `DocumentElement` when `chainedType` is non-null — is **left as-is and
     documented**: a code comment at both sites naming the counterpart, plus a characterization
     test pinning the runtime's current strategy selection for the affected shape (the extra
     zero-length element defeats the single-element fast path at `RuntimeDocument.cs:58-73` and
     participates in the `totalLength` full-optimize test at `:74-91` — strategy, not bytes;
     verified). Aligning the runtime (skipping empty default chains) is byte-neutral but changes
     strategy selection, i.e. performance behavior, and therefore needs benchmark evidence and a
     maintainer call — recorded as an open question, not smuggled into a byte-neutral refactor.
- **Rationale.** The deletion is the only change in this phase with zero behavioral surface at
  all; the asymmetry is real but its blast radius is strategy selection on an edge shape, and the
  phase's byte-neutrality gate must stay clean of deliberate behavior changes.
- **Alternatives rejected.** Reviving `DocumentsCache` (nothing references it; the caching
  conventions it sketched are superseded by `ResolveLayoutCached`-style keyed caches); fixing the
  asymmetry inside this phase (violates the extraction-is-byte-and-behavior-neutral discipline).
- **Grounding.** [02 — minors](../research/generator-code-sharing/02-document-shaper.md);
  `DocumentsCache.cs` and `RuntimeDocument.OptimizeCallTree` re-read.

### D11 — Byte-neutrality is an acceptance gate, not a review judgment

- **Decision.** Every work item after WI1 must show, in one combined run: full
  `src/Heddle.Tests` suite green on all TFMs with **zero golden diffs**; the precompiled-vs-dynamic
  differential contract green (`src/Heddle.Tests/PrecompiledRuntimeTests.cs` — "must render
  byte-identical to the dynamic engine — the differential contract" — plus
  `MultilineOverrideOffsetRegressionTests.cs`'s cross-file differential); the generator snapshot
  goldens (`src/Heddle.Generator.Tests/GeneratorSnapshotTests.cs`) unchanged — extraction must not
  alter one emitted byte of generated source, including every `P0..Pn` piece constant; and the
  grammar-stability check in its default no-diff form. WI1 is the only item licensed to change an
  observable outcome, and only the one analyzed in Back-compat (tier selection for the overshoot
  class).
- **Rationale.** "Refactor proven byte-identical" is this repo's established discipline (the
  testing standards' fix-forward rule; the benchmark program's gate-before-timing posture); the
  shaping code decides bytes, so nothing weaker is credible.
- **Alternatives rejected.** Relying on code review of the moves (Finding 1 is what review-only
  maintenance produced); regenerating any golden to absorb an extraction diff (explicitly a defect
  per [testing-standards — golden change policy](../spec/common/testing-standards.md#fixtures-and-goldens)).
- **Grounding.** [testing-standards — regression gates](../spec/common/testing-standards.md#regression-gates);
  suite roles verified by reading the named test files' headers.

## Dependencies & ordering

- **Depends on:** nothing merged — WI1 can start immediately. Cross-phase touchpoints:
  [Phase 1](phase-1-template-emitter.md) owns the zero-output attribute (WI7's guard hands over to
  it), the `ParticipantScan` fix (referenced, untouched here), and the emitter-side adoption of
  `RegionFillResolver` (WI6 leaves a forward pointer). The BranchRole Tier-1 link
  ([07 Tier 1](../research/generator-code-sharing/07-recommendations.md)) is deliberately **not** a
  dependency — `BranchKind` is self-contained (D5).
- **Unblocks:** Phase 1's piece-emission work (consumes `SlicePieces<T>`, WI5) and its
  `RegionFillResolver` emitter adoption (WI6); every later shaping-adjacent fix in either tier
  (lands once, in the shared file).
- **Internal ordering** — fix, pin, then move; each WI passes the D11 gate before the next starts:

### Work items

**WI1 — Clamp drift fix (independently shippable).**
*Files:* `src/Heddle.Generator/Emit/DocumentShaper.cs` (the `WidenToWholeLine` body only);
new fixture(s) under `src/Heddle.Tests/TestTemplate` (stem `shaper-clamp-*`); new generator test in
`src/Heddle.Generator.Tests`.
*Shape:* port the runtime body per D1, verbatim including the clamp comment (adapted). Test-first
(the TDD verdict below): a fixture reproducing the overshoot input class — two trim-eligible
removals sharing one line (a definition/import block followed on its line, with only
whitespace/tabs between, by a zero-output directive), under `TrimDirectiveLines` — derived from and
cross-checked against the runtime clamp's own comment; before the fix the generator degrades
(assert: no precompiled entry in the manifest for the fixture); after, it precompiles and the
precompiled render is byte-equal to the dynamic render. A unit `[Theory]` drives both
`WidenToWholeLine` implementations over a shared vector table (in-bounds, EOF, CR/CRLF/LF
terminators, zero-length probe, negative start, start past end, length past end) asserting equal
outputs — this theory is the extensional-equality pin that stays alive after extraction collapses
the two into one.
*Done when:* the new tests are green, the full D11 gate is green, and the fixture appears in the
`.gitattributes` LF pin set in the same change.

**WI2 — Characterization suite over the shaping machines (pre-extraction pins).**
*Files:* new `src/Heddle.Tests/DocumentShapingCharacterizationTests.cs` (+ shared vector data under
`src/Heddle.Tests/Data`); new twin in `src/Heddle.Generator.Tests` driving the same vectors through
`DocumentShaper.Shape` via the harness.
*Shape:* per-machine input/output pins for the five rebasing machines and the strip machine — a
`ParseContext`-level vector table (skipped tokens, definitions, raw outputs, chains with
enclosing/after/before positions relative to each removal; branch layouts covering
opener/continuation/terminal/other/participant transitions and right-to-left gap application) —
executed against **both call sites before the swap** and asserted equal (working document + all
rebased positions). These tests are written against the current code and must not change when WI3–
WI4 swap the internals: they are the definition of "byte-neutral" at machine granularity, per the
[extraction map](phase-2-document-shaper-extraction-map.md)'s pin list.
*Done when:* both twins are green against the pre-extraction code and cover every row of the
extraction map's pin column.

**WI3 — `Language/DocumentShaping.cs`: trim predicate, safe string ops, five machines; both sides swap.**
*Files:* new `src/Heddle/Language/DocumentShaping.cs`; `src/Heddle/Runtime/HeddleCompiler.cs` and
`src/Heddle.Generator/Emit/DocumentShaper.cs` (deletions + call rewires per D9).
*Shape:* per D2/D3/D4. The pass-order contract lands in the shared file header; the driver-order
lockstep test lands beside WI2's suite.
*Done when:* WI1's theory now targets the single shared `WidenToWholeLine` (plus one adapter-level
smoke per side); WI2's characterization twins are green **unchanged**; D11 gate green; no duplicate
method bodies remain (D9's mechanical check).

**WI4 — Branch strip machine + `BranchKind` + observer; runtime diagnostics re-hosted.**
*Files:* `src/Heddle/Language/DocumentShaping.cs` (machine + enum + observer shape);
`src/Heddle/Runtime/HeddleCompiler.cs` (`ProcessBranchSets` becomes classifier + observer over the
shared machine); `src/Heddle.Generator/Emit/DocumentShaper.cs` (classifier maps to `BranchKind`
incl. `Participant`).
*Shape:* per D5. The existing branch suites (`BranchSetCompilerTests`, `BranchingGoldenTests`,
`BranchRoleDriftDiagnosticTests` in both test projects, `BranchProtocolTests`,
`BranchConcurrencyTests`) are the behavior pins — all diagnostics keep IDs, texts, positions.
*Done when:* branch suites green unchanged; a new theory pins that a `[ScopeChannel]` non-role
extension classifies `Participant` on both sides and that both tiers render such layouts
byte-identically; D11 gate green.

**WI5 — `SlicePieces<T>`; both walks swap.**
*Files:* `src/Heddle/Language/DocumentShaping.cs`; `src/Heddle/Runtime/RuntimeDocument.cs`
(`GetDocumentPieces`); `src/Heddle.Generator/Emit/TemplateEmitter.cs` (the `:359-401` walk).
*Shape:* per D6.
*Done when:* generator snapshots byte-identical (the `P0..Pn` constants are the proof); full D11
gate green; render benchmarks show no allocation increase (the walk feeds compiled structures, not
the render loop, but the gate is cheap and the claim should be proven, not argued).

**WI6 — `Language/RegionFillResolver.cs`; runtime adoption.**
*Files:* new `src/Heddle/Language/RegionFillResolver.cs`;
`src/Heddle/Runtime/HeddleCompiler.cs` (`BuildRegionFillScope` becomes a driver).
*Shape:* per D7. `RegionTests` and the HED5019 negative tests are the pins (positioned
diagnostics, retraction behavior, `PrivateOverrideReported` once-only). Forward pointer comment at
`TryBuildGeneratorFillScope` naming the Phase 1 adoption item.
*Done when:* region suites green unchanged; a verdict-level unit theory covers all four verdicts
including the lazy-lookup behavior (no layout resolve when no candidate passes the origin filter);
D11 gate green.

**WI7 — Zero-output lockstep guard.**
*Files:* new test in `src/Heddle.Tests` (runtime ground truth side) with the generator's list
mirrored the way `DefaultFunctionLockstepTests` mirrors its table.
*Shape:* per D8.
*Done when:* the guard is green and demonstrably red when a name is added to either side alone
(verified by temporary mutation during development, then reverted).

**WI8 — Dispositions.**
*Files:* delete `src/Heddle/Runtime/DocumentsCache.cs`; comments at `DocumentShaper.Shape`'s
default-chain loop and `CompileBody`'s default-chain loop; characterization test for the
empty-default-chain strategy shape (D10).
*Done when:* solution builds on all TFMs; the characterization test pins today's strategy
selection.

**WI9 — Exit gate.**
One combined run of the full D11 gate plus testing-standards gate 4 (compile benchmarks in
`src/Heddle.Performance` before/after on the same machine — allocated bytes not increased, mean
within BenchmarkDotNet's reported error; the D3 revisit trigger arms here if red).

## Back-compat / impact

The parity contract this phase lives under: the precompiled tier must render **byte-identical** to
the dynamic tier (the differential contract, `PrecompiledRuntimeTests.cs`), and existing templates
render byte-identically across a release unless a ratified breaking window says otherwise
([D2](../spec/common/cross-cutting-decisions.md#d2--breaking-changes-land-only-in-ratified-breaking-windows),
[breaking-windows.md](../spec/common/breaking-windows.md)). Against that, the clamp fix's three
sub-cases ([02 F1](../research/generator-code-sharing/02-document-shaper.md)), each verified
against head source:

- **Sub-case 1 — unclamped dereference (`DocumentShaper.cs:378`, `:384-394`).** Requires an
  out-of-bounds stored position. Today: `IndexOutOfRangeException`, swallowed at
  `HeddleTemplateGenerator.cs:249-252` → the template silently renders through the dynamic tier.
  After WI1: the template precompiles; its bytes equal the dynamic tier's by the differential
  contract (same clamp, same downstream machines — post-WI1 the implementations are extensionally
  equal, pinned by the WI1 theory). **User-visible change: tier selection only** — same bytes,
  restored precompilation performance. Not a breaking change; no window needed.
- **Sub-case 2 — `right == Length` vs `right >= Length` (`DocumentShaper.cs:386` vs
  `HeddleCompiler.cs:462`).** `right > Length` is only reachable from an out-of-bounds
  `StartIndex + Length`; the generator then dereferences `document[right]` and throws → same
  swallow, same analysis as sub-case 1.
- **Sub-case 3 — original-block vs clamped-span "not whole-line" exits (`DocumentShaper.cs:381`,
  `:395` vs `HeddleCompiler.cs:457`, `:471`).** The research flags this as a potential **byte**
  divergence "where both survive". Verified against head: it cannot currently fire as wrong
  bytes. The clamp is the identity on in-bounds spans, so the exits differ only for out-of-bounds
  input — and there the generator either throws inside `WidenToWholeLine` (left-side dereference)
  or returns the overshooting original block, which the very next statement
  (`ApplyRemove` → `string.Remove`, `DocumentShaper.cs:180`, called from `:241`/`:284`) turns into
  an `ArgumentOutOfRangeException` — swallowed like the rest. So today's failure mode is **always
  silent degrade, never divergent bytes**, and WI1 converts degrade into byte-identical
  precompilation. *(Verify at implementation:* re-confirm no third caller of the generator's
  `WidenToWholeLine` exists beyond `RemoveDefinitions`, `RemoveEmptyItem`, and the bounds-guarded
  `TrimHiddenRemnantLines` probe — that set is what makes this argument airtight.*)*
- **Extraction (WI3–WI6):** byte- and diagnostic-neutral by the D11 gate — goldens, differential
  suite, generator snapshots, and every branch/region diagnostic pinned unchanged. No public API
  is added or changed (everything `internal`); no `TemplateOptions` surface is touched; the
  grammar is untouched.
- **`DocumentsCache.cs` deletion:** no references exist (fully commented out); binary-compat
  irrelevant (internal, dead).
- **Packaging/build:** the shared files ride the existing link glob — no csproj change, no new
  package content, generator still ships without any `Heddle.dll` reference (structural: the glob
  links sources, not the assembly).

## Risks & mitigations

| Risk | Mitigation | Size (S/M/L) |
|---|---|---|
| The extraction accidentally changes bytes in an untested template shape | WI2's machine-level characterization twins run against both call sites *before* the swap and must pass unchanged after; the D11 gate (goldens + differential + snapshots) backs them at template granularity; fix-forward rule — no golden is ever regenerated to absorb a diff | M |
| The WI1 fixture fails to reproduce the overshoot class (the input geometry is subtle) | The runtime clamp's comment pins the mechanism (earlier widened removal on the same line); the WI1 unit theory covers the out-of-bounds classes directly at the predicate level even if the template-level fixture needs iteration; both must exist before the fix per the TDD verdict | S |
| Re-hosting HED300x diagnostics as strip-machine observer perturbs a message, position, or ordering | The branch suites assert positioned diagnostics by ID; WI4 changes no message string, and the observer receives events in the same document order the inline code observed (same loop, same traversal) | M |
| The safe `Replace` regresses the runtime compile path measurably | Compile-path only (render path untouched); WI9 runs the Heddle.Performance benchmarks under BenchmarkDotNet per testing-standards gate 4 (not ad-hoc wall times); D3's revisit trigger ratifies an injected fast-path if red | S |
| Shared file accidentally grows a runtime or Roslyn dependency and breaks the generator build | Structural: the generator compiles the linked file in a netstandard2.0, no-Heddle.dll, no-unsafe context — any violation is a red build, not a latent defect; the WI3 done-check includes a clean generator build | S |
| `Participant` classification added to the generator changes strip behavior somewhere | It cannot today — `Participant` and the previous `default:` both disarm; the WI4 theory pins byte-equality of both tiers on `[ScopeChannel]` non-role layouts to make that checked rather than assumed | S |
| The `SlicePieces<T>` callback shape can't express the emitter's mid-walk degrade cleanly | D6 pre-authorizes the `bool`-returning `onElement` variant; the snapshot suite proves emitted-source neutrality either way | S |
| Phase-ordering friction: Phase 1 items land first and touch the same emitter lines | The only overlapping surface is `TemplateEmitter`'s walk (WI5) and fill scope (Phase 1's adoption); D5-conventions ordering plus small, per-WI merges keep rebases mechanical; co-owned seams (D8) are delegate-shaped precisely so either side can land first | M |

## Success criteria

Measurable, checkable statements a spec can turn into tests.

- [ ] The generator's and runtime's `WidenToWholeLine` are extensionally equal: a shared vector
      theory (in-bounds, EOF, CR/CRLF/LF, zero-length probe, negative start, start/length past
      end) passes against both before extraction and against the single shared implementation
      after.
- [ ] A checked-in fixture of the overshoot input class precompiles: the generator produces a
      manifest entry and generated source for it (today it silently degrades), and its
      precompiled render is byte-identical to its dynamic render.
- [ ] After WI3–WI5, `grep`-level check: no method body in
      `src/Heddle.Generator/Emit/DocumentShaper.cs` or `src/Heddle/Runtime/HeddleCompiler.cs`
      duplicates a `DocumentShaping` member; `DocumentShaper.cs` retains only `Element`/`Result`,
      the `Shape` driver, and classification adapters.
- [ ] `src/Heddle/Language/DocumentShaping.cs` and `RegionFillResolver.cs` compile in the
      generator via the existing glob with zero csproj edits, contain no Roslyn types, no
      `unsafe`, no runtime-assembly types beyond the already-linked parse model.
- [ ] `BranchKind` (with `Participant`) exists exactly once, in the shared file; both classifiers
      map into it; the branch suites pass unchanged with all HED3001–HED3005 IDs, texts, and
      positions intact.
- [ ] `RegionFillResolver` verdicts drive `BuildRegionFillScope` with behavior pinned unchanged:
      HED5019 raised once per private candidate, retraction semantics identical, dangling and
      default-missing candidates keep their parse errors; `RegionTests` green unchanged.
- [ ] The zero-output lockstep guard is green, and red under single-sided mutation of either the
      list or a built-in's classification.
- [ ] One combined run: full `src/Heddle.Tests` on all TFMs green with zero golden diffs;
      `PrecompiledRuntimeTests` + `MultilineOverrideOffsetRegressionTests` differentials green;
      `GeneratorSnapshotTests` byte-identical; grammar-stability no-diff; compile benchmarks
      within BenchmarkDotNet error with no allocation increase.
- [ ] `src/Heddle/Runtime/DocumentsCache.cs` no longer exists; the solution builds on all TFMs.
- [ ] The empty-default-chain characterization test pins current strategy selection, and both
      default-chain loops carry the cross-referencing comment.

## Validation scenarios

| Input | Expected outcome |
|---|---|
| The overshoot fixture (definition block + same-line zero-output directive, trimming on), built through the generator | Before WI1: no precompiled entry (silent degrade). After WI1: precompiled entry exists; precompiled and dynamic renders byte-identical |
| The same fixture rendered by the runtime alone, before vs after this phase | Byte-identical output — the runtime's behavior is the fixed point |
| The `TrimDirectiveLinesTests` torture rows and `ErgoTrimPreambleGoldenPair`, both tiers, after each WI | Green, byte-identical, zero golden diffs |
| A `ParseContext` characterization vector (WI2) run through the pre-extraction generator pass and the post-extraction shared pass | Identical working document and identical rebased positions for all three lists |
| A branch layout `@if` / non-role `[ScopeChannel]` extension / `@else` | Classified `Participant` on both sides; strip disarms; both tiers byte-identical; HED300x diagnostics unchanged |
| A call-site region override targeting a private region | Runtime: retract + HED5019, once, positioned at the override — identical to today; verdict path is `Private` |
| A candidate matching no region | Runtime: parse-emitted error kept, no retraction (verdict `Dangling`) — identical to today |
| A new built-in extension made zero-output on the runtime side only | WI7 lockstep guard turns red at build time |
| Any golden or snapshot diff appearing during WI3–WI6 review | Treated as a defect in the extraction (fix forward), never a golden regeneration |
| `dotnet build` of `Heddle.Generator` after adding the shared files | Green with zero csproj edits (glob pickup), no unsafe/Roslyn/runtime-type leakage |

## Open questions

1. **Empty-default-chain alignment (authority ambiguous).** The generator skips empty default
   chains; the runtime materializes a zero-length element that changes strategy selection (never
   bytes) for chained bodies. Which behavior is *intended* is not derivable from source — the file
   otherwise claims verbatim parity, but the runtime's element also predates the generator.
   Default carried by this plan: leave both, document, pin with a characterization test (D10).
   Trigger to revisit: benchmark evidence that skipping re-enables the single-element fast path on
   a real workload, plus a maintainer ruling on which side is canonical.
2. **Ownership of a degrade-visibility diagnostic.** The `catch (Exception)` swallow at
   `HeddleTemplateGenerator.cs:249-252` is what made Finding 1 silent; a build-time
   info/warning ("template X degraded to the dynamic path: <reason>") would make the whole class
   of regressions visible. It is a generator-diagnostics concern (HED70xx block,
   [06](../research/generator-code-sharing/06-diagnostics-utilities.md) territory) — this phase
   records the need and defers the decision to the diagnostics-owning phase rather than claiming
   an ID here.

## External grounding

| Claim | Source |
|---|---|
| The seven duplicated machines, the verified clamp divergence (all three sub-cases), the strip-machine split, the zero-output/list divergence, the piece-slicing twin, the region-fill duplication, and the minors | [02 — document shaper](../research/generator-code-sharing/02-document-shaper.md); all load-bearing line ranges re-verified against head during planning (see the [extraction map](phase-2-document-shaper-extraction-map.md)) |
| Program-wide priority (drift #1), the `Language/DocumentShaping.cs` layout, the linked-glob mechanism and its `Heddle.Shared` deferral, and the fix-first sequencing | [07 — synthesis](../research/generator-code-sharing/07-recommendations.md) |
| Runtime clamp semantics and its documented failure mode | `src/Heddle/Runtime/HeddleCompiler.cs:443-472` (`WidenToWholeLine` and its clamp comment) |
| Generator's unclamped copy and silent degrade path | `src/Heddle.Generator/Emit/DocumentShaper.cs:375-396`; `src/Heddle.Generator/HeddleTemplateGenerator.cs:249-252` |
| Link glob (zero-csproj-edit sharing) and the generator's netstandard2.0/no-Heddle.dll posture | `src/Heddle.Generator/Heddle.Generator.csproj:4, 50` |
| Unsafe blocker on `ExStringBuilder`; semantic equality of the safe replace | `src/Heddle/Strings/ExStringBuilder.cs:325-365`; `src/Heddle.Generator/Emit/DocumentShaper.cs:178-185` |
| Shared-core-with-both-backends precedent and the materialization leaf | `src/Heddle/Language/DefinitionMaterializer.cs` (header contract) |
| Lockstep-test precedent | `src/Heddle.Tests/DefaultFunctionLockstepTests.cs` |
| Differential and snapshot gates | `src/Heddle.Tests/PrecompiledRuntimeTests.cs`; `src/Heddle.Tests/MultilineOverrideOffsetRegressionTests.cs`; `src/Heddle.Generator.Tests/GeneratorSnapshotTests.cs` |
| Byte-change policy, breaking windows, golden change policy, benchmark gate | [cross-cutting D2](../spec/common/cross-cutting-decisions.md#d2--breaking-changes-land-only-in-ratified-breaking-windows); [breaking-windows.md](../spec/common/breaking-windows.md); [testing-standards](../spec/common/testing-standards.md); [coding-standards](../spec/common/coding-standards.md) |
