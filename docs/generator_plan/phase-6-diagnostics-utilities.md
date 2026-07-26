# Phase 6 — diagnostics, line mapping, and shared utility tables

## Header

- **Status:** **implemented (2026-07-26)** — WI1–WI8 landed in the first pass (2026-07-25),
  WI9–WI11 and the `CSharpTypeNames`↔`NumericKind` reconciliation in the second, once phases 1, 4
  and 5 had shipped the artifacts they own (`OutputProfileRules`, `CSharpEscape`,
  `HeddleBuildOptions` / `TemplateKey.TryMakeRelative`). Q6.2's full options parity shipped with
  WI9, including the LSP default-profile flip to `Html`. See
  [Implementation record](#implementation-record). All open questions resolved
  (user, 2026-07-25) and folded in (see *Open questions* and the [register](open-questions.md))
- **Goal (one line):** One diagnostic identity across build tier, run tier, and editor — a shared
  diagnostic catalog and projection, one line-index rule, and one copy each of the small utility
  tables (C# type names, name sanitization, profile/mode tokens, escape/surrogate scans) — opened
  by an independently shippable fix group for the forwarded-warning ID/Fix loss, the HED7017
  registry-doc gap, and the line-index `\r` mismatch.
- **Depends on:** [Phase 1 — template emitter](phase-1-template-emitter.md) (the
  `OutputProfileRules` parse functions this phase's WI9 adopts), [Phase 3 — binding
  layer](phase-3-binding-layer.md) (owns the parse/binding-direction type-spelling parser that
  borders WI7), [Phase 4 — expression writers](phase-4-expression-writers.md) (owns the shared
  `CSharpEscape` that WI11 coordinates with), [Phase 5 — pipeline &
  config](phase-5-pipeline-config.md) (owns the `TemplateKey.Relativize` extension WI10 adopts,
  owns the shared `HeddleBuildOptions` names/defaults table WI9 sources its option names and
  defaults from — a dependency the Q6.2 ruling strengthens — and owns `ContentHash` — excluded
  here). **WI1–WI8 have no cross-phase dependency and can start
  immediately**; only WI9–WI11 bind to other phases' artifacts, per
  [D5](../spec/common/cross-cutting-decisions.md#d5--implementation-follows-the-owning-plans-declared-order).
- **Changes an externally-visible contract:** yes, three diagnostics-surface deltas, none of them
  rendered-output bytes: (a) forwarded front-end warnings surface at build time under their **real**
  `HED` IDs instead of collapsing to `HED7013` — which changes what a user's `NoWarn`/`#pragma`
  suppresses; (b) `HeddleCompileResult` column reporting changes for documents with `\n\r`
  sequences or CRLF blank lines when the three line indexes reconcile; (c) the LSP's default
  output profile aligns with the engine's (`Html`), surfacing `HED2004`-class lints in editors
  that never configured `outputProfile` — and the LSP config surface widens to full
  runtime-options parity (additive keys; see D10). All three are analyzed against
  [breaking-windows.md](../spec/common/breaking-windows.md) in *Back-compat / impact*; all three
  are **ratified fix-not-window** (user rulings, 2026-07-25 — see *Open questions*).

## Goal

This phase resolves research area [06 — diagnostics, emit utilities, and third
copies](../research/generator-code-sharing/06-diagnostics-utilities.md) (findings F2–F10; F1,
`ContentHash`, is phase 5's). The through-line of the area is *diagnostic identity*: the
[D1 rule](../spec/common/cross-cutting-decisions.md#d1--stable-diagnostic-ids-hedxxxx) says a
Heddle diagnostic has one stable `HEDxxxx` ID with fixed severity and message, and the ID a user
sees must be the same whether it arrives from the build-time generator, the runtime compiler, the
CLI tool, or the language server. Today that rule is maintained in four unsynchronized places and
is already violated on two axes ([06 F2](../research/generator-code-sharing/06-diagnostics-utilities.md),
[06 F3](../research/generator-code-sharing/06-diagnostics-utilities.md)):

- **The forwarded-warning path discards identity.** `src/Heddle.Generator/HeddleTemplateGenerator.cs:325-330`
  reports every front-end warning through the fixed `GeneratorDiagnostics.ForwardedWarning`
  descriptor (`HED7013`), discarding `warning.DiagnosticId` and `HeddleCompileWarning.Fix` — while
  the error path directly above (`:312-323`) synthesizes a per-ID descriptor from
  `error.DiagnosticId` and falls back to `HED7012` only when the ID is null. The class's own doc
  comment (`GeneratorDiagnostics.cs:6-7`) and the shipped docs
  (`docs/precompilation.md:202`) both state the contract the code violates: "an ID-less forwarded
  error is wrapped as HED7012/HED7013". *Verification correction to the research:* the only
  parse-channel warning producer today is the id-less SLL-fallback warning
  (`src/Heddle/Language/DocumentParser.cs:66-73` — which carries a `Fix` that the collapse also
  drops); the id-carrying warnings the research names (HED2004 at
  `src/Heddle/Runtime/HeddleCompiler.cs:1279-1284`, HED4005 at `:624-629`, HED3005 at `:342-350`)
  live in the **compile channel**, which the generator never drains at all
  ([06 F3](../research/generator-code-sharing/06-diagnostics-utilities.md)). So the live loss
  today is the `Fix`; the ID collapse is a loaded gun that fires the moment any id-carrying
  warning reaches the generator's drain — and the projection unification (D5) makes that drain
  channel-complete, so both defects are fixed by construction together.
- **The registries have already drifted.** `HED7017` exists in code
  (`GeneratorDiagnostics.cs:136-139`) and in the normative registry
  ([cross-cutting-decisions.md, registry rows](../spec/common/cross-cutting-decisions.md#claimed-diagnostic-ids-registry))
  but is missing from the `docs/precompilation.md:191-205` table — the very document the registry
  row points at as the block's home. Nothing gates this: `src/Heddle.Tests/DiagnosticIdTests.cs:46-72`
  asserts a hardcoded HED0xxx–HED5xxx set against `HeddleDiagnosticIds` only — no HED7xxx, no
  check against `GeneratorDiagnostics`, no check against either docs table.
- **Positions disagree.** Three line-start indexes with two `\r` rules
  ([06 F4](../research/generator-code-sharing/06-diagnostics-utilities.md)): the generator's
  `LineMapper.cs:14-52` (1-based) and the LSP's `LineMap.cs:17-53` (0-based) share an identical
  `\n`-only index loop, while `src/Heddle/Data/HeddleCompileResult.cs:22-37` builds a third index
  via `Split('\n')` with a leading-`\r` offset bump (`:29-30`) — despite `LineMap.cs:9-10`
  claiming to match "the engine's line splitting". The same offset can render as a different
  column in a build error, an LSP squiggle, and `CompileResult.ToString()`.

The deliverables: a **fix-first bug group** (D1) shippable before any extraction; the shared
**`HeddleDiagnosticCatalog`** data table with the generator's `DiagnosticDescriptor` factory
(D4 — schema in the supplement
[phase-6-diagnostics-utilities-catalog.md](phase-6-diagnostics-utilities-catalog.md)); the shared
**`HeddleDiagnosticProjection`** drain (D5); the shared **`LineIndex`** (D6); the utility-table
consolidations **`CSharpTypeNames`** (D7), `TemplateKey.Relativize` adoption (D8), `SanitizeName`
test-mirror removal (D9), profile/mode token adoption (D10), and the generator-internal
escape/surrogate folding (D11); and the **registry/consistency test suite** (D12) that makes every
one of these a permanently gated invariant instead of a convention.

## Non-goals / scope boundary

- **No `ContentHash` work.** [06 F1](../research/generator-code-sharing/06-diagnostics-utilities.md)
  converged with [05 F1](../research/generator-code-sharing/05-pipeline-config.md); the extraction
  and the build/run lockstep test are owned by phase 5. This phase's registry tests do not touch
  hashing.
- **No new diagnostic IDs and no renumbering.** Every ID this phase touches already exists; the
  registry's "an ID once shipped is never reused or renumbered" rule is load-bearing for the whole
  design. `HED6xxx` stays reserved-unclaimed: the LSP's config-parse complaints (D10) remain
  tooling-only log output, not compile diagnostics, exactly as the registry annotates.
- **No Roslyn code-fix provider.** `HeddleCompileWarning.Fix` surfaces at build time as message
  text (D3), not as a `CodeFixProvider` — the generator ships no analyzer assembly pairing today,
  and inventing one is a separate initiative. Revisit trigger: an IDE-integration initiative that
  ships a Heddle analyzer.
- **The parse functions for profiles/modes are not written here.** `TryParseProfile`/
  `TryParseExpressionMode` belong to phase 1's `OutputProfileRules` (per
  [07 — Tier 2](../research/generator-code-sharing/07-recommendations.md)); this phase owns their
  adoption in the LSP and the cross-host *policy* reconciliation decision (D10). The emitter's
  silent-ignore of `@profile(<unknown>)` (`src/Heddle.Generator/Emit/TemplateEmitter.cs:418-427`)
  is phase 1's fix; D10 records the pointer, not the change.
- **The `TemplateKey.Relativize` extension itself is not written here.** Phase 5 owns extending
  the linked `TemplateKey.cs`; this phase owns the LSP `RenderPath` and `TemplateOptions.FullPath`
  adoptions (D8) and nothing in the generator's `Relative`/`DeriveKey` (phase 5's F3/F6 seam).
- **The type-spelling parser is not moved here.** Phase 3 owns splitting the reflection-free
  generic/array/tuple spelling parser out of `ReflectionHelper`; this phase owns only the
  alias↔`Type` and display tables plus the symbol-side alias adapter (D7), with the alias key
  list as the agreed shared boundary between the two phases.
- **No wholesale migration of runtime raise sites to catalog-formatted messages.** The catalog's
  `MessageFormat` column is populated and consumed only where a second consumer exists (the
  HED7xxx block and the F2 twin vocabularies) — see D4 and OQ3. Rewriting ~100 interpolated raise
  sites for symmetry is exactly the "hasty unification" the
  [coding standards](../spec/common/coding-standards.md#dry-applied) warn against.
- **No behavior change for rendered output.** Every change in this phase lives on the
  diagnostics/tooling surface or inside generated-source *text* proven render-equivalent by the
  differential harness. "Existing suite byte-identical" is an acceptance criterion, not a risk.

## Design direction

### D1 — The fix-first bug group ships first, independently of every extraction

**Decision.** Three defects ship as ordinary bug fixes (WI1–WI4) before any shared file is
created, in this order: the registry/consistency tests (WI1, written failing — they are the
executable spec for the group), the `docs/precompilation.md` HED7017 row (WI2), the
forwarded-warning ID/Fix loss (WI3), and the line-index reconciliation (WI4). Each is
independently shippable and none depends on the catalog, the projection, or any other phase.

**Rationale.** [07's sequencing](../research/generator-code-sharing/07-recommendations.md) puts
"bug fixes first (no extraction needed)" ahead of all extraction tiers, and the
[testing standards' canonical loop](../spec/common/testing-standards.md#the-canonical-loop)
demands the failing executable spec first — WI1's registry test is precisely the test that would
have caught the HED7017 gap ([07, test guardrails](../research/generator-code-sharing/07-recommendations.md)).
Landing fixes before extraction also keeps each diff reviewable as *behavior change* or *code
motion*, never both at once.

**Alternatives rejected.** Folding the fixes into the catalog extraction (couples a user-visible
behavior change to a refactor, making the golden/diagnostic diffs unattributable); fixing
HED7017's doc row without the registry test (repeats the failure mode that produced the gap).

### D2 — Forwarded warnings carry their real front-end ID at build time; HED7013 is reserved for id-less warnings

**Decision.** `HeddleTemplateGenerator.cs:325-330` changes to mirror the error path's shape: when
`warning.DiagnosticId` is non-null, the report uses a warning-severity descriptor for **that ID**
(post-D4, produced by the catalog factory; in the WI3 interim, synthesized inline exactly as
`:318-321` does for errors, with `DiagnosticSeverity.Warning`); only an id-less warning falls back
to `GeneratorDiagnostics.ForwardedWarning` (`HED7013`). The same rule applies to `HED7012` on the
error path, which already implements it.

**Rationale.** This is the design decision the area turns on, and three arguments close it in the
same direction. *Suppression ergonomics:* Roslyn suppression — `#pragma warning disable HEDxxxx`,
`<NoWarn>`, editorconfig severity mapping — keys on the diagnostic ID; under the collapse, a user
cannot suppress one Heddle lint at build time without silencing every forwarded warning, and
suppressing by the real ID (the ID the LSP shows them for the same squiggle) silently does
nothing. *Documented contract:* `GeneratorDiagnostics.cs:6-7` and `docs/precompilation.md:202`
already describe HED7013 as the wrapper for warnings "carrying no id" — the code is the outlier,
which makes this a defect fix against shipped documentation, not a contract change. *Symmetry:*
the error path one loop above already forwards real IDs; two rules for one seam is exactly the
drift D1-the-registry exists to prevent.

**Ratified and generalized — the early-surfacing principle (user, 2026-07-25, Q6.1).** The
ruling confirms D2 as a fix and broadens it into a stated program principle: *if a diagnostic
can surface early, it must* — on **both** tiers, runtime and generator alike. D2 is that
principle applied to build-time forwarding; the principle also governs any future decision about
which diagnostics forward, through which channel, and at what severity (an option that surfaces
a diagnostic earlier is preferred over one that defers it). The migration-note line for the
stale `NoWarn HED7013` edge stays (see *Back-compat / impact*).

**Alternatives rejected.** Keeping `HED7013` and embedding the real ID in the message text
(unsuppressable individually; diverges from both the LSP and the error path; turns the ID into
prose). Reporting both the real-ID diagnostic and a HED7013 wrapper (double noise per warning,
and `NoWarn HED7013` would then half-work — the worst of both). Making build-time forwarding
opt-in via an MSBuild property (a speculative knob;
[YAGNI applied](../spec/common/coding-standards.md#yagni-applied) — no scenario in this plan
needs the old behavior).

### D3 — Build-time severity and Fix mapping for forwarded runtime IDs

**Decision.** The severity of a forwarded diagnostic at build time is the catalog row's
`DefaultSeverity` (D4) — which for every current warning ID is `Warning`; the generator never
escalates a runtime warning to a build error or vice versa. Until the catalog lands (WI3 interim),
severity is derived exactly as the LSP does it (`DocumentAnalyzer.cs:116-118`): subtype
`HeddleCompileWarning` → warning, everything else → error — replacing the generator's current
severity-by-collection-membership. `HeddleCompileWarning.Fix`, when present, is appended to the
build message as a trailing sentence (`"{message} Fix: {fix}"`), for both id-carrying and id-less
warnings; errors have no `Fix` property and are unaffected.

**Rationale.** Severity is a property of the diagnostic, not of the surface
([D1's rationale](../spec/common/cross-cutting-decisions.md#d1--stable-diagnostic-ids-hedxxxx)
explicitly rejected severity-encoding precisely because severity must survive reclassification in
one place) — the catalog row is that one place. The `Fix` is user-facing remediation text the LSP
already shows (`DocumentAnalyzer.cs:119`); losing it at build time means the surface with the
*least* interactive tooling gets the *least* help. Message-suffix is the only Fix channel Roslyn
offers without a code-fix provider (a non-goal).

**Alternatives rejected.** Dropping Fix at build time as today (the defect being fixed); a
separate companion Info diagnostic carrying the fix (two diagnostics per warning, unsuppressable
as a unit); `DiagnosticDescriptor.Description` (invisible in CLI build output, where the fix is
most needed).

### D4 — `HeddleDiagnosticCatalog`: one shared data table; the generator projects it to descriptors

**Decision.** New shared file `src/Heddle/Data/HeddleDiagnosticCatalog.cs` — netstandard2.0, pure
data, zero Roslyn — beside the already-linked `HeddleDiagnosticIds.cs`
(`Heddle.Generator.csproj:57`), linked into the generator with one `<Compile Include>` line. Each
row: `Id → (Title, DefaultSeverity, MessageFormat?)`, with `HeddleDiagnosticSeverity { Error,
Warning }` as a new two-value enum in `Heddle.Data`. Content authority: **the runtime's raise-site
message and severity are the default authority** for every row; for the HED7xxx block (which the
runtime never raises) the existing `GeneratorDiagnostics` descriptors are the source. The
generator side gains a small factory (`Diagnostics/GeneratorDiagnostics.cs` rewired):
`Descriptor(id)` builds the Roslyn `DiagnosticDescriptor` from the catalog row (title, severity,
category `"Heddle.Precompile"`), and the existing named descriptors become
catalog-projected — equal by construction, not by parallel maintenance. The forwarded-diagnostic
paths (`:312-330`) call `ForwardDescriptor(id, isWarning)` which uses the catalog row when one
exists and a passthrough `"{0}"` format (the front end has already formatted the message). The
`MessageFormat` column is populated **only for rows with a second consumer**: the HED7xxx block,
and the F2 twin vocabularies (member-path HED0001/HED7008, branch-channel HED3005/HED7016, the
`[Prop]` fault list HED5007–HED5015/HED7017 — including the shared reserved-name set
`{"out","this"}` currently duplicated at `src/Heddle/Runtime/Expressions/PropLayout.cs:115` and
`src/Heddle.Generator/Emit/TemplateEmitter.cs:850` and the five-step fault order both sides
re-implement). Other rows carry `Title` and `DefaultSeverity` only, so no second copy of message
prose is created. The LSP consumes the catalog directly (project reference + `InternalsVisibleTo`,
`src/Heddle/Properties/AssemblyInfo.cs:10`) to attach human titles to `Diagnostic.code` values —
additive. Full schema, row inventory, and factory signatures:
[phase-6-diagnostics-utilities-catalog.md](phase-6-diagnostics-utilities-catalog.md).

**Rationale.** [06 F2](../research/generator-code-sharing/06-diagnostics-utilities.md) counts four
maintenance sites for id→(message, severity, title); the catalog collapses the two code sites into
one data table plus one thin Roslyn projection (the hard constraint — no Roslyn in shared files,
no `Heddle.dll` reference from the generator — is satisfied because the catalog is data and only
the factory touches `DiagnosticDescriptor`). Scoping `MessageFormat` to consumed rows is the
[DRY-as-knowledge](../spec/common/coding-standards.md#dry-applied) call: the knowledge that must
exist once is the id→severity/title mapping and the twin fault vocabulary — not every message
string, whose single owner today is its raise site and should remain so until a second consumer
appears (OQ3 records the revisit trigger).

**Alternatives rejected.** Full raise-site migration to `Catalog.Format(id, args)` (~100 sites of
churn to eliminate drift that has never been observed *within* the runtime; the observed drift is
cross-tier, which the twins-only scope covers). A `Heddle.Shared` project
([07's layout note](../research/generator-code-sharing/07-recommendations.md) — linked files are
the proven precedent; revisit past ~20 shared files). Reusing Roslyn's `DiagnosticSeverity` in the
shared file (breaks the no-Roslyn constraint on the runtime side).

### D5 — `HeddleDiagnosticProjection`: one drain rule, host policies stay host-side

**Decision.** New shared file `src/Heddle/Language/HeddleDiagnosticProjection.cs` (under
`Language/**`, so the generator's existing glob `Heddle.Generator.csproj:50` links it with zero
csproj edits): a neutral readonly struct `(Id, Message, Fix, IsWarning, Offset, Length,
ImportOrigin)` plus static drains over the already-linked front-end types — `Drain(ParseContext)`
and `Drain(ParseContext, CompileContext)` — implementing the complete rule the LSP owns today
(`DocumentAnalyzer.cs:106-148`): all four sources, severity by subtype, `Fix` carried, `Id`
passthrough, reference-dedupe. Host policies remain host-side, layered on the neutral stream: the
LSP keeps import re-anchoring and `RenderPath` rendering (`:125-132`); the generator keeps its
region-fill retract pre-filter (`HeddleTemplateGenerator.cs:304-315`) as a predicate applied
before reporting, maps `(Offset, Length)` through `ToLocation`, and reports via D2/D3;
`HeddleCompileResult` keeps its `"[{pos}]{id}: {msg}"` rendering
(`src/Heddle/Data/HeddleCompileError.cs:29-45`) — which `Heddle.Tool` consumes unchanged
(`src/Heddle.Tool/HeddleRenderer.cs`, a consumer, not a copy, per the research's negative
results).

**Rationale.** [06 F3](../research/generator-code-sharing/06-diagnostics-utilities.md): the drain
rule exists completely in the LSP, partially in the generator (parse sources only, severity by
collection, no Fix), and as a third rendering in `HeddleCompileResult` — and any new diagnostic
channel added to `CompileContext` today gets picked up by the LSP and silently missed by the
generator. Once the generator (in any later phase) runs compile-channel stages, the shared drain
means channel-completeness by construction — this is the structural half of the D2 fix.

**Alternatives rejected.** Making the projection apply host policy via flags (turns a data drain
into a policy switchboard; three hosts, three policies, kept where each is testable). Placing it
in `Data/` (needs `ParseContext`/`CompileContext`, which live in the `Language` linked set —
`Data/` would still work but `Language/**` gets the free glob link and matches the types' home).

### D6 — `LineIndex`: one index, `ZeroBased`/`OneBased` accessors, one documented `\r` rule

**Decision.** New shared file `src/Heddle/Data/LineIndex.cs` (netstandard2.0, `int[]` + binary
search, zero dependencies), linked into the generator beside the already-linked `LinePosition.cs`
(`Heddle.Generator.csproj:58`). **The canonical rule — documented on the type:** a line starts at
offset 0 and after each `'\n'`; `'\r'` is never a terminator by itself; a `'\r'` adjacent to a
`'\n'` belongs to the line that `'\n'` terminates; offsets and columns count UTF-16 code units.
This is the rule `LineMapper` and `LineMap` already share, and it agrees with Roslyn's
`SourceText` line semantics for `\n`/`\r\n` input (the `#line` consumer) and LSP's utf-16
positions. Accessors: `OffsetToZeroBased(offset)`, `OffsetToOneBased(offset)`,
`ZeroBasedToOffset(line, character)` (the LSP's lenient inverse, `LineMap.cs:57-69`), plus
`LineCount` and `LineStart(i)`. Adoption: `LineMapper` and `LineMap` become thin wrappers (their
public shapes unchanged); `HeddleCompileResult` drops `_positions`/`_lineOffsetsSearch`
(`HeddleCompileResult.cs:12-13, 22-37, 54-73`) and fills `LinePosition` from the shared index —
**removing the leading-`\r` offset bump** (`:29-30`), the one behavioral delta (see Back-compat).

**Rationale.** [06 F4](../research/generator-code-sharing/06-diagnostics-utilities.md): the three
copies are not equivalent today, and `LineMap`'s doc comment claims an equivalence with the engine
that does not hold. The `\n`-only rule wins because two of three implementations already use it,
both external consumers (Roslyn `#line`, LSP) specify it, and the third's deviation (bumping past
a leading `'\r'` after `Split('\n')`) fires only on `\n\r` sequences and CRLF blank lines — an
artifact of the split-based construction, not a documented choice.

**Alternatives rejected.** Adopting the `HeddleCompileResult` rule (would change columns in build
errors *and* LSP squiggles — the two high-traffic surfaces — to match the low-traffic one);
treating lone `'\r'` as a terminator (classic-Mac line endings; no consumer requests it, and it
would diverge from Roslyn/LSP for `\r\n` interiors).

### D7 — `CSharpTypeNames`: the alias tables exist once; the symbol side adapts

**Decision.** New shared file `src/Heddle/Helpers/CSharpTypeNames.cs` (netstandard2.0, one
`<Compile Include>` link line for the generator): the alias↔`System.Type` map (today
`ReflectionHelper.CSharpTypes`, `src/Heddle/Helpers/ReflectionHelper.cs:32-49`, 16 entries incl.
`dynamic`), the `Type`→alias display function (today quadruplicated:
`FunctionEntry.FriendlyName`, `src/Heddle/Runtime/Expressions/FunctionEntry.cs:85-95`;
`CompletionProvider.Friendly`, `src/Heddle.LanguageServices/Completion/CompletionProvider.cs:198-211`
— a verbatim copy that exists only because `FriendlyName` is private; and the
`system.*`→keyword switch in `src/Heddle/Helpers/TypeNameHelper.cs:120-172`), and — as the shared
single source — the **alias key list**. The generator's
`SymbolTypeResolver.Keywords` (`src/Heddle.Generator/Binding/SymbolTypeResolver.cs:45-54`,
15 entries, no `dynamic`) becomes an adapter: a Roslyn-side `alias → SpecialType` map whose key
set is asserted (by the WI7 test) to equal `CSharpTypeNames`' alias list minus the documented
symbol-side exclusion (`dynamic`, which has no `SpecialType` and is intentionally unbindable on
the build tier — the exclusion is a named constant, not an omission). The four reflection-side
call sites delegate; `CompletionProvider.Friendly`'s copy is deleted. Boundary with phase 3: that
phase's spelling parser *consumes* the alias map from this file; this phase does not touch
`ReflectionHelper.cs:319-410`.

**Rationale.** [06 F5](../research/generator-code-sharing/06-diagnostics-utilities.md): five
tables, four projects, key sets already differing; adding `nint`/`nuint` to one today silently
diverges what a template can write, what the build tier binds, and what error text displays. A
lockstep-keyed adapter turns that three-way divergence into a compile-visible test failure.

**Alternatives rejected.** A `TFacts` abstraction unifying `Type` and `SpecialType` behind one
interface (Tier-3 machinery for what is, on this seam, two dictionaries and a key-equality test);
making `FunctionEntry.FriendlyName` public instead of extracting (fixes one of five copies).

### D8 — `TemplateKey.Relativize` adoption in the LSP; `TemplateOptions.FullPath` delegates to the `FileReader` rule

**Decision.** When phase 5's `TemplateKey.Relativize(path, root)` (with its documented case
policy) lands, the LSP's `RenderPath` (`src/Heddle.LanguageServices/DocumentAnalyzer.cs:269-293`)
reduces to `Path.GetFullPath` both sides + `TemplateKey.Relativize`, keeping only its
LSP-specific fallback (absolute path, `\`→`/`) — deleting the third hand-rolled
prefix-strip. Independently (same-assembly, no phase dependency), `TemplateOptions.FullPath`
(`src/Heddle/Data/TemplateOptions.cs:162`, naive `RootPath + TemplateName + FileNamePostfix`)
delegates to the `Path.Combine` rule `FileReader.GetFileName` already uses
(`src/Heddle/FileReader.cs:37-40`) — the pair whose previous divergence the comment at
`HeddleTemplate.cs:357-359` records as an already-shipped bug ("so the two can never diverge
again").

**Rationale.** [06 F6](../research/generator-code-sharing/06-diagnostics-utilities.md): four
relativization rules with inconsistent case handling; the LSP's copy feeds the user-visible
`ImportedFrom`, and `FullPath` feeds the `ImportOrigin` the LSP displays via
`PartialExtension.cs:67` — both are presentation-path fixes with no render-byte impact.

**Alternatives rejected.** Adopting `Relativize` in the generator's `Relative` here (phase 5 owns
that seam together with `DeriveKey`'s diagnostics — splitting one function between two phases
guarantees a merge conflict); leaving `FullPath` as-is because the delta is small (the recorded
prior bug is the evidence that "small" was already wrong once).

### D9 — `SanitizeName` mirrors die via `InternalsVisibleTo`

**Decision.** Add `Heddle.Generator.IntegrationTests` to the generator's `InternalsVisibleTo`
items (`Heddle.Generator.csproj:32` carries the pattern; the test project is signed with the same
`heddle.snk` — verified, `Heddle.Generator.IntegrationTests.csproj:8-9` — so the public-key form
is copy-exact). Delete both mirrors: `DifferentialHarness.SanitizeKey`
(`src/Heddle.Generator.IntegrationTests/DifferentialHarness.cs:294-335`, a line-for-line copy by
its own admission) and `PartialTests.Sanitize`
(`src/Heddle.Generator.IntegrationTests/PartialTests.cs:38-53`, already partially divergent);
both call the production `HeddleTemplateGenerator.SanitizeName`
(`HeddleTemplateGenerator.cs:426-471`, already `internal static`).

**Rationale.** [06 F7](../research/generator-code-sharing/06-diagnostics-utilities.md): a naming
rule change currently makes the differential harness silently degrade (`FindEntryPoint` → null) —
the tests that exist to catch build/run divergence become the thing that drifts. Trivial, no
production change.

**Alternatives rejected.** Keeping the mirrors as an "independent oracle" (an oracle that must be
hand-synchronized is a second copy, not an oracle; the harness's job is differential
*render* checking, not naming-rule checking).

### D10 — Full runtime-options parity in the LSP config; profile/mode token adoption, with the policy reconciliation recorded

**Decision.** When phase 1's `OutputProfileRules.TryParseProfile`/`TryParseExpressionMode` land,
`WorkspaceConfig` (`src/Heddle.LanguageServices/WorkspaceConfig.cs:43-51, :70-78`) parses through
them, deleting its private token matching. The cross-host **policies** reconcile as follows —
each host keeps its own *reaction*, the *token set* and the *defaults* unify:
- **Runtime** (`ProfileExtension.cs:24-37`): unknown → `HED2001` compile error. Unchanged.
- **Generator MSBuild** (`ConfigReader.cs:28-36`): unknown → `HED7009` + documented default
  (`Html`/`Native`). Unchanged in reaction; token matching moves to the shared functions.
  (Phase 5's `HeddleBuildOptions` work owns any further ConfigReader reshaping.)
- **LSP**: today an absent/unknown `outputProfile` yields `Text` — a different default from the
  generator's and, post-2.0 flip (`docs/precompilation.md:58` — "generator defaults track the
  engine defaults"), from the engine's. Reconciliation: absent key → **`Html`** (align with
  engine/generator); unknown token → keep the default and emit a tooling-side log line naming the
  accepted tokens (no `HED` id — `HED6xxx` stays reserved, per the registry). Same pattern for
  `expressionMode` (its `Native` default already agrees; unknown token gains the log line instead
  of silent fallback).
- **Emitter** `@profile()` scan: phase 1's item; pointer only (see Non-goals).

**Full options parity (user ruling, 2026-07-25, Q6.2).** The LSP follows the same configuration
surface the runtime permits and **wires all options** — not just the default profile.
`.heddle-lsp.json` keys map 1:1 to the option names in phase 5's shared `HeddleBuildOptions`
names/defaults table (camelCase, `Heddle` prefix dropped; the table records the key beside the
MSBuild property name so the mapping is data, not convention — today's shipped keys already
follow the rule, with `rootPath` kept as the established `TemplateOptions.RootPath` spelling of
`HeddleTemplateRoot`); defaults follow the runtime's, sourced from the same table. Enumerated
against `src/Heddle/Data/TemplateOptions.cs` (verified 2026-07-25):

- **Wired today** (`WorkspaceConfig.cs:31-52`, `DocumentAnalyzer.BuildTemplateOptions`,
  `DocumentAnalyzer.cs:91-104`): `rootPath` (→ `RootPath`; LSP-resolved against the workspace
  root), `outputProfile` (→ `OutputProfile`; default flips to `Html` per this D-item),
  `expressionMode` (→ `ExpressionMode`, default `Native`), `fileNamePostfix`
  (→ `FileNamePostfix`, default empty), plus the LSP-specific `assemblies` key — the config-file
  representation of `TemplateOptions.Functions` and extension exports via the one-shot export
  scan (object-valued options have no literal JSON form).
- **Added for parity:** `trimDirectiveLines` (→ `TrimDirectiveLines`, default `true`) and
  `maxRecursionCount` (→ `MaxRecursionCount`, default `100`) — both compile-affecting options
  the analyzer currently leaves at constructor defaults with no way to configure, so a workspace
  that compiles with either non-default today cannot make the editor match the build of record.
- **Documented exclusions** (each named in the WI9 completeness test, not silently absent):
  `TemplateName` (per-document identity, derived per file), `Functions` (object-valued —
  represented by `assemblies`), `Data` (render input), `EnableFileChangeCheck` (render-cache
  invalidation), `PrecompiledMismatchPolicy`, `RenderBudget`, `ValidateModelType` (render-time
  failure handling and limits — never read by analysis), `Encoder` (render-time encoding,
  object-valued, no lint depends on it), `AllowCSharp` (obsolete bridge — the key is
  `expressionMode`), `ProvideLanguageFeatures` (always `true` in the LSP — it *is* the
  analyzer's operating mode, `DocumentAnalyzer.cs:95`). The generator-only
  `HeddleEmitUtf8Pieces` row of the shared table has no analysis meaning and gets no key.
- **Gate:** WI9 adds a completeness test over `TemplateOptions`' public properties — every
  property is either wired to a key or on the exclusion list, in the pattern of the repo's
  existing `TemplateOptions` completeness test
  ([coding standards](../spec/common/coding-standards.md#api-design-and-compatibility)) — so a
  future runtime option must be wired or explicitly excluded, never forgotten.

This strengthens WI9's cross-phase dependency: beyond phase 1's parse functions, WI9 now binds
to phase 5's `HeddleBuildOptions` table (phase 5 WI5) as the single source of option names and
defaults.

**Rationale.** [06 F8](../research/generator-code-sharing/06-diagnostics-utilities.md): four
parsers, four policies, and one genuinely wrong default — an LSP workspace without an explicit
`outputProfile` today lints as `Text` while the build and runtime treat the same templates as
`Html`, so the editor never shows the `HED2004` encoding lints the build of record would produce.
Per-host reactions stay because they are genuinely different contracts (a template author's typo
is a compile error; a build property typo is a build diagnostic; a workspace-config typo must
never break editing).

**Alternatives rejected.** Unifying the reaction policy too (an LSP that hard-errors on config
typos stops providing diagnostics entirely — strictly worse for the user); keeping the LSP `Text`
default for back-compat (preserves a silent divergence from the surface of record; the visible
delta is new *true-positive* lints, handled in Back-compat).

### D11 — Generator-internal escape/surrogate folding

**Decision.** Inside the generator (no shared file): one `Escape(char c, bool inCharLiteral)`
core in `PieceWriter` that both the string form (`Emit/PieceWriter.cs:23-52`) and
`NativeExpressionWriter.EscapeChar` (`Emit/NativeExpressionWriter.cs:290-304`) consume — the char
form gains the `\a \b \f \v` escapes it currently lacks (cosmetic in emitted *source text*,
render-equivalent by definition since both spellings denote the same char values; the
differential harness gates it). For surrogates: keep `IndexOfFirstLoneSurrogate`
(`Emit/TemplateEmitter.cs:451-469`), move it beside the escape core, and derive
`HasLoneSurrogate(s) => IndexOfFirstLoneSurrogate(s) >= 0`, deleting the duplicated loop at
`PieceWriter.cs:54-72` — turning today's coincidence of equivalence into a definition.
**Coordination with phase 4:** if phase 4's shared `CSharpEscape` (inside `LiteralFormatter`)
lands first, `PieceWriter`'s core folds into it instead of existing separately; the phase that
lands second performs the fold. This phase owns the PieceWriter-side folding either way.

**Rationale.** [06 F9/F10](../research/generator-code-sharing/06-diagnostics-utilities.md): both
findings are low-risk generator-internal tidy-ups; the research's negative result (no runtime
escaping twin exists — the runtime's C# tier generates through `.tcs` templates) is what keeps
this generator-internal rather than shared.

**Alternatives rejected.** Promoting the escape table to a shared file now (no runtime consumer
exists — a seam without a second consumer, against the
[precedence rules](../spec/common/coding-standards.md#precedence-when-principles-conflict));
keeping both surrogate loops with a test asserting equivalence (tests should pin contracts, not
prop up duplication).

### D12 — The registry/consistency test suite: three-way code gate plus docs gates

**Decision.** `DiagnosticIdTests` grows into the area's permanent gate (same file,
`src/Heddle.Tests/DiagnosticIdTests.cs`, plus a generator-side twin where Roslyn types are
needed):
1. **Constants completeness** — the existing HED0xxx–HED5xxx reflection check extends to every
   `HeddleDiagnosticIds` const (the hardcoded expected set at `:48-61` gains the 7xxx/9xxx rows
   as they gain constants — see the supplement's row inventory for the HED7xxx constants this
   phase adds to `HeddleDiagnosticIds` so the generator block is reflectable like every other).
2. **Three-way code check** — every `HeddleDiagnosticIds` const has exactly one
   `HeddleDiagnosticCatalog` row; every `GeneratorDiagnostics` descriptor's `(Id, Title,
   DefaultSeverity)` equals its catalog row (by construction after D4 — the test guards against
   regression to hand-built descriptors); no catalog row lacks a const.
3. **Docs-registry gates** — a test parses the two normative markdown tables — the
   [claimed-ID registry](../spec/common/cross-cutting-decisions.md#claimed-diagnostic-ids-registry)
   and the `docs/precompilation.md` HED7xxx table — via a strict `| HEDxxxx |` row scan (files
   located from the test source's `[CallerFilePath]`, robust across TFMs incl. net48) and asserts:
   every code ID falls inside a claimed registry range/row; every HED7xxx code ID has a
   `precompilation.md` row (**red today: HED7017** — this is WI1's failing spec); no doc row
   names an ID that no code raises.
4. **Catalog round-trip** — every row's `MessageFormat`, where present, is well-formed
   (placeholders contiguous `0..n-1`) and its arity matches the consuming raise sites — enforced
   by the descriptor projection for HED7xxx and by the twin-vocabulary fixtures for the F2 twins
   (supplement, §Tests).
5. **Projection equivalence** — over a fixture corpus of failing/warning templates (one per
   diagnostic block, including an import-origin case and a region-fill retract case), the LSP
   drain, the generator drain, and the `HeddleCompileResult` drain agree on the
   `(Id, IsWarning, Offset, Length)` multiset after applying each host's declared policy deltas
   (generator: retract-filtered; LSP: import re-anchored entries compared on origin site).
6. **LineIndex goldens** — table-driven `[Theory]` over LF, CRLF, lone-`\r`, `\n\r`, mixed, and
   empty documents pinning `(offset → zero-based, one-based)` triples, plus the LSP inverse's
   clamping; `LineMapper`/`LineMap`/`HeddleCompileResult` wrappers assert agreement with the
   shared index on the same vectors.

**Rationale.** Every consolidation in this phase is only as durable as its gate; the HED7017 gap
existed precisely in the one direction (`code → docs`) nothing checked. Parsing the actual
markdown keeps the docs registry normative-in-fact, not normative-by-convention — the same
philosophy as the repo's `TemplateOptions` completeness test
([coding standards](../spec/common/coding-standards.md#api-design-and-compatibility)).

**Alternatives rejected.** A hardcoded docs-mirror set in the test (recreates the drift the test
exists to kill — the doc file is the fixture); gating docs in CI lint scripts outside the test
suite (splits the gate from the suite that runs on every TFM and every dev machine).

## Dependencies & ordering

- **Phase 0 posture (landed):** every test in this phase runs under the gauntlet-crossing guardrails — see the
  [precompiled-tier posture](../spec/common/testing-standards.md#precompiled-tier-posture) rule. This phase owns
  no quarantined phase 0 fixture.

Work items in dependency order. WI1–WI8 are self-contained within this phase; WI9–WI11 bind to
other phases' shipped artifacts per
[D5](../spec/common/cross-cutting-decisions.md#d5--implementation-follows-the-owning-plans-declared-order)
and may be reordered after WI8 without penalty if those phases land in a different sequence.

| WI | Work | Implements | Depends on |
|---|---|---|---|
| WI1 | Registry/consistency tests, written failing: docs-registry gates (red on HED7017), HED7xxx constants completeness, LineIndex golden vectors (red against `HeddleCompileResult`) | D12 (1, 3, 6) | — |
| WI2 | `docs/precompilation.md` HED7017 row (+ correcting the `:202` HED7012/HED7013 row's wording if D2 ratification adjusts it) | D1 | WI1 red |
| WI3 | Forwarded-warning fix at `HeddleTemplateGenerator.cs:325-330`: real IDs, subtype severity, Fix suffix (inline interim shape) | D2, D3 | WI1 |
| WI4 | `Data/LineIndex.cs` + wrapper adoption in `LineMapper`, `LineMap`, `HeddleCompileResult`; goldens green | D6, D12 (6) | WI1 |
| WI5 | `Data/HeddleDiagnosticCatalog.cs` + severity enum + HED7xxx constants in `HeddleDiagnosticIds` + generator descriptor factory rewiring (WI3's inline synthesis replaced); three-way + round-trip tests green | D4, D12 (2, 4) | WI3 |
| WI6 | `Language/HeddleDiagnosticProjection.cs` + LSP/generator adoption + projection-equivalence corpus test | D5, D12 (5) | WI5 |
| WI7 | `Helpers/CSharpTypeNames.cs` + four reflection-side adoptions + symbol-adapter key-lockstep test | D7 | — (parallel any time; phase 3 consumes it later) |
| WI8 | `InternalsVisibleTo` + delete both `SanitizeName` mirrors | D9 | — |
| WI9 | `WorkspaceConfig` full options parity: adoption of `TryParseProfile`/`TryParseExpressionMode`, default alignment, log-line policy, the `trimDirectiveLines`/`maxRecursionCount` keys, names/defaults sourced from `HeddleBuildOptions`, and the wired-or-excluded completeness test | D10 | **Phase 1** `OutputProfileRules` shipped; **Phase 5** `HeddleBuildOptions` (its WI5) shipped |
| WI10 | LSP `RenderPath` → `TemplateKey.Relativize`; `TemplateOptions.FullPath` → `FileReader` rule (the `FullPath` half has no external dependency and may ship with WI8) | D8 | **Phase 5** `Relativize` shipped (RenderPath half only) |
| WI11 | Escape core fold + surrogate-scan dedupe; differential harness green | D11 | Coordination with **Phase 4** `CSharpEscape` (fold direction decided by land order) |

Cross-phase coordination summary: **phase 1** owns the parse functions WI9 adopts and the
emitter's `@profile` silent-ignore fix; **phase 3** owns the spelling parser that will consume
WI7's alias tables; **phase 4** owns `CSharpEscape` (WI11 folds into it if it exists first);
**phase 5** owns `TemplateKey.Relativize`, `ContentHash`, `DeriveKey`, and `HeddleBuildOptions`
(whose names/defaults table WI9 consumes for the LSP's full options parity, per the Q6.2
ruling).
No file in this phase is written by another phase; the shared-file link lines this phase adds to
`Heddle.Generator.csproj` are `Data/HeddleDiagnosticCatalog.cs`, `Data/LineIndex.cs`,
`Helpers/CSharpTypeNames.cs` (the projection rides the existing `Language/**` glob).

## Back-compat / impact

Analyzed against [D2](../spec/common/cross-cutting-decisions.md#d2--breaking-changes-land-only-in-ratified-breaking-windows)
/ [breaking-windows.md](../spec/common/breaking-windows.md). **No change in this phase alters
rendered template output**; every delta is on the diagnostics/tooling surface. The
breaking-window policy governs byte- and behavior-breaking changes to templates and hosts; the
verdict for each delta below is **fix, not window item**, with the reasoning stated —
**ratified by the user rulings of 2026-07-25** (see *Open questions*). None of these rows is
proposed for the next-window candidate register.

- **Forwarded-warning IDs (D2/WI3).** A build warning that previously surfaced as `HED7013` will
  surface under its real front-end ID once such warnings exist in the forwarded stream (today's
  stream carries only the id-less SLL warning, which keeps `HED7013` — so the day-one visible
  change is limited to the appended Fix text). Consumer impact when it does bite:
  `NoWarn`/`#pragma` on `HED7013` stops matching those warnings; suppression by the real ID (the
  ID the LSP already shows) starts working. Under `TreatWarningsAsErrors` with a stale
  `NoWarn HED7013`, a build could newly fail — this is the one hard edge, and it gets a
  migration-note line ("if you suppressed HED7013 wholesale, suppress the specific IDs or keep
  both during transition"). Verdict: **fix — ratified (user, 2026-07-25, Q6.1)**, under the
  early-surfacing principle recorded in D2; the shipped documentation
  (`GeneratorDiagnostics.cs:6-7`, `docs/precompilation.md:202`) already promises this behavior;
  aligning code to documented contract is the defect-fix side of the breaking-change rules, and
  the additive-window rule ("new options default to current behavior") is not implicated because
  no option changes.
- **Fix text in build messages (D3/WI3).** Message text of forwarded warnings gains a trailing
  `Fix:` sentence. Message text is not a stable API; any test or log-scraper matching exact
  warning text sees a diff. Verdict: fix; noted in release notes.
- **`HeddleCompileResult` columns (D6/WI4).** Removing the leading-`\r` bump changes
  `LinePosition.Offset` (and line attribution at the `\r` character itself) for documents
  containing `\n\r` sequences or CRLF blank-line splits — the low-traffic surface deliberately
  chosen (D6) so that build-error and LSP positions (the high-traffic surfaces) are unchanged.
  `CompileResult.ToString()` output for such documents shifts by one column on affected lines;
  `Heddle.Tool` output inherits the same delta. Existing goldens: any golden capturing
  `ToString()` over an affected document changes once, review-attributed to WI4 per the
  [golden change policy](../spec/common/testing-standards.md#fixtures-and-goldens). Verdict: fix —
  the current behavior contradicts `LineMap`'s documented equivalence claim and is an
  implementation artifact, not a documented contract.
- **LSP default output profile and options parity (D10/WI9).** Workspaces without
  `outputProfile` in `.heddle-lsp.json` start receiving `Html`-profile lints (`HED2004` class)
  in the editor — new true positives matching what the build of record already enforces. No
  build behavior changes. The parity keys (`trimDirectiveLines`, `maxRecursionCount`) are
  purely additive: an absent key keeps the runtime default the analyzer already used implicitly,
  so existing workspaces see no delta from them. Verdict: fix — **ratified (user, 2026-07-25,
  Q6.2)**, editor-only, alignment with the 2.0 default flip; release-note line documents
  `"outputProfile": "text"` as the opt-out.
- **`TemplateOptions.FullPath` (D8/WI10).** Composed paths gain proper separator handling via
  `Path.Combine`; strings shown as `ImportOrigin`/`ImportedFrom` may change separators for hosts
  that set `RootPath` without a trailing separator — previously those produced a defective
  concatenation, which is the recorded prior bug. Render resolution is unaffected
  (`FileReader.GetFileName` already used `Path.Combine`).
- **Generated source text (D11/WI11).** `NativeExpressionWriter` char literals for `\a \b \f \v`
  change spelling from `\uXXXX` to short escapes — generated-source cosmetics; compiled behavior
  identical; differential harness and "existing suite byte-identical" (rendered output) gate it.
- **Public API surface.** New shared types (`HeddleDiagnosticCatalog`,
  `HeddleDiagnosticSeverity`, `LineIndex`, `CSharpTypeNames`, the projection struct) ship
  `internal` — consumers are the generator (linked sources), the LSP (`InternalsVisibleTo`), and
  tests. Nothing public is added or changed, so no XML-doc/published-docs obligations trigger
  beyond the `precompilation.md` fix itself.

## Risks & mitigations

| Risk | Mitigation | Size (S/M/L) |
|---|---|---|
| The docs-gate tests (D12.3) are brittle against markdown re-formatting of the registry tables | Strict, documented row grammar (`\| HEDxxxx… \|` first-cell match only); failure message prints the expected grammar and the offending line; the tables are already machine-regular and the gate makes them contractually so | S |
| Catalog rows drift from raise-site prose for rows where `MessageFormat` is not consumed | By design those rows carry no message text at all (title + severity only) — there is no second copy to drift; the twins and HED7xxx, where text *is* duplicated knowledge, are the consumed rows with arity/equality gates | M |
| Rewiring `GeneratorDiagnostics` descriptors through the catalog changes descriptor identity Roslyn-side (e.g. title text differences surfacing in IDE listings) | WI5's three-way test asserts `(Id, Title, DefaultSeverity)` equality between old literals and catalog rows before the literals are deleted — transcription first, deletion second, in one reviewed diff | S |
| `TreatWarningsAsErrors` + stale `NoWarn HED7013` breaks a consumer build after D2 (when id-carrying warnings enter the forwarded stream) | Migration-note line in the shipping release's notes (the D2 delta is documented-contract alignment; the note is the [window-policy](../spec/common/breaking-windows.md) deliverable shape applied voluntarily to a fix) | M |
| The `HeddleCompileResult` line-rule delta invalidates an unknown number of checked-in goldens or downstream log parsers | Pre-measure: WI1's golden vectors run against the current implementation to enumerate affected shapes (`\n\r`, CRLF blank lines) before WI4 lands; goldens change once, attributed, per the fix-forward rule | S |
| Projection adoption in the generator accidentally widens its drain to compile-channel sources it cannot yet position/report correctly | WI6 keeps the generator on `Drain(ParseContext)` only; the `Drain(ParseContext, CompileContext)` overload is consumed by the LSP alone until a later phase gives the generator compile-channel stages — the seam exists, the behavior change does not | S |
| Cross-phase file collisions (phase 4's `CSharpEscape`, phase 1's `OutputProfileRules`, phase 5's `TemplateKey`/`HeddleBuildOptions`) land in overlapping files or duplicate helpers | File ownership table in *Dependencies & ordering*; WI9–WI11 explicitly bind to shipped artifacts, and the D11 fold direction is decided by land order with the second-lander performing the fold | M |
| `InternalsVisibleTo` addition fails on signing mismatch | Verified up front: the integration-test project signs with the same `heddle.snk` (`Heddle.Generator.IntegrationTests.csproj:8-9`); the key text is copied from the existing `:32` entry | S |
| net48 test leg cannot locate the docs files for D12.3 | Anchor via `[CallerFilePath]` of the test source (compile-time constant, TFM-independent), not `AppContext.BaseDirectory` probing | S |

## Success criteria

Measurable, checkable statements a spec can turn into tests.

- [ ] A front-end warning carrying a `DiagnosticId` reaches MSBuild output under that ID with
      warning severity; `#pragma warning disable <that ID>` and `<NoWarn>` suppress it; only
      id-less warnings surface as `HED7013`. The warning's `Fix`, when present, appears in the
      build message.
- [ ] `docs/precompilation.md`'s HED7xxx table lists every ID the generator can raise, HED7017
      included, and the D12.3 docs gate fails the suite if the two ever diverge again — in either
      direction, for any future ID.
- [ ] One `LineIndex` implementation backs `LineMapper`, `LineMap`, and `HeddleCompileResult`;
      the golden `[Theory]` over LF/CRLF/lone-`\r`/`\n\r`/mixed/empty documents passes with
      identical `(line, column)` for the same offset across all three surfaces (modulo the
      documented 0-based/1-based accessor difference).
- [ ] Every `HeddleDiagnosticIds` constant (now including the HED7xxx block) has exactly one
      catalog row; every `GeneratorDiagnostics` descriptor equals its catalog projection; every
      consumed `MessageFormat` is well-formed with raise-site-matching arity — all enforced in
      `dotnet test` on all TFMs.
- [ ] The projection-equivalence corpus shows LSP, generator, and `CompileResult` drains agreeing
      on `(Id, IsWarning, Offset, Length)` per fixture after declared host policies; adding a
      diagnostic to any front-end channel with a missing catalog row or a host that drops it
      turns the suite red.
- [ ] `grep`-level duplication checks: the reserved-name set `{"out","this"}`, the C# alias
      list, the `Type`→alias display switch, `SanitizeName`, and the lone-surrogate loop each
      exist exactly once in the repo (tests or structure make the second copy impossible, per
      D4/D7/D9/D11).
- [ ] The LSP under a config-less workspace produces the same profile-dependent lint set the
      generator produces for the same template; an unknown `outputProfile` token yields a logged
      message naming the accepted tokens and never a crash or silent `Text`.
- [ ] Every analysis-applicable `TemplateOptions` option is wired through `.heddle-lsp.json`
      (`rootPath`, `outputProfile`, `expressionMode`, `fileNamePostfix`, `trimDirectiveLines`,
      `maxRecursionCount`, plus the LSP-specific `assemblies`) with its key name and default
      sourced from the shared `HeddleBuildOptions` table; every other `TemplateOptions` property
      is on D10's named exclusion list; the WI9 completeness test fails when a new
      `TemplateOptions` member is neither wired nor excluded, and the defaults it asserts equal
      the runtime's.
- [ ] Full regression gate green in one combined run per the
      [testing standards](../spec/common/testing-standards.md#regression-gates): solution build,
      all-TFM tests, grammar-stability (no grammar change licensed here), goldens byte-identical
      except the WI4-attributed `ToString()` set, and no benchmark regression (no render hot path
      is touched; the compile-path additions are measured once to confirm noise-level).

## Validation scenarios

| Input | Expected outcome |
|---|---|
| A template triggering an id-carrying front-end warning, built via the generator with `<NoWarn>$(that id)</NoWarn>` | Build succeeds with the warning suppressed; removing the NoWarn shows the warning under its own ID with the Fix sentence; `NoWarn HED7013` does not affect it |
| A template that trips the SLL parse fallback (id-less warning with Fix) | Build shows `HED7013` with the Fix text appended — the id-less wrapper contract of `precompilation.md:202` holds |
| Delete the HED7017 row from `docs/precompilation.md` (or add a bogus `HED7099` row) | D12.3 docs gate fails naming the missing/spurious ID and the file |
| Add a new const to `HeddleDiagnosticIds` without a catalog row | D12.2 three-way test fails naming the ID |
| The same failing template analyzed by the LSP, the generator, and `HeddleCompiler`+`CompileResult` | One `(Id, IsWarning, Offset, Length)` verdict per diagnostic across all three, with the LSP's import re-anchoring and the generator's retract filter as the only declared deltas |
| An error at a given offset in a CRLF document with blank lines, rendered via build diagnostic, LSP squiggle, and `CompileResult.ToString()` | Same line and column everywhere (1-based vs 0-based per surface convention); the pre-WI4 `ToString()` off-by-one on such documents is demonstrably gone |
| `min(1, 2u)`-style hover/completion text, an HED1012 signature error, and the generator's symbol binding for `nint` (a deliberately unlisted alias) | All three surfaces agree because they read one alias table; the symbol adapter's key-lockstep test names any set divergence including the documented `dynamic` exclusion |
| A workspace with no `.heddle-lsp.json` opening a template with a bare `@(value)` in an attribute under `Html`-default | The editor shows HED2004 exactly as the build would; adding `"outputProfile": "text"` removes it |
| A `.heddle-lsp.json` with `"trimDirectiveLines": false` and `"maxRecursionCount": 3` | The analyzer's `TemplateOptions` carries exactly those values — analysis matches a runtime compile under the same options wherever the compile path reads them; an absent key yields the runtime default from the shared table |
| Rename `SanitizeName`'s mapping rule experimentally | `Heddle.Generator.IntegrationTests` fails to compile or its differential tests fail loudly — no silent `FindEntryPoint` → null degradation |
| A `.heddle` static piece containing `\v` and a lone surrogate | Emitted string and char literals spell escapes identically via the one core; HED7005 fires from the single surrogate scan; rendered bytes unchanged |

## Open questions

**All three are resolved (user, 2026-07-25)** — recorded in the
[open-questions register](open-questions.md) as Q6.1–Q6.3 and folded into this plan. None
remain open; per the
[no-open-questions discipline](../spec/common/spec-conventions.md#no-open-questions), the
elaborating spec records them as closed decisions.

- **OQ1 / Q6.1 — Ratify D2 as a fix, not a breaking-window item.** **Resolved (user,
  2026-07-25): fix, confirmed — and broadened into a stated principle.** The user's ruling: "If
  we can surface diagnostics early — we must do that; this is relevant for both runtime and for
  generator." Diagnostics surface as early as possible on **both** tiers; the principle is
  recorded in D2 and guides any future forwarding/severity decision. WI3 ships whole (real IDs +
  Fix forwarded at build time); the migration-note line for the stale `NoWarn HED7013` edge
  stays (*Back-compat / impact*).
- **OQ2 / Q6.2 — LSP default-profile alignment (D10).** **Resolved (user, 2026-07-25): align —
  and go further.** The LSP follows the same configuration surface the runtime permits and
  wires **all** options: full parity with the runtime option set and defaults, with names and
  defaults sourced from phase 5's shared `HeddleBuildOptions` table and `.heddle-lsp.json` keys
  mapping 1:1 to the shared names. D10 and WI9 are expanded accordingly (option enumeration,
  exclusion list, completeness gate, strengthened phase-5 dependency); the opt-out
  (`"outputProfile": "text"`) exists either way.
- **OQ3 / Q6.3 — Catalog `MessageFormat` end-state.** **Resolved (user, 2026-07-25):
  recommendation applied** — consumed rows only (HED7xxx + F2 twins); runtime raise sites keep
  their inline text as the single owner. No design change. The revisit triggers stand: the next
  initiative that adds a diagnostic *area* (a natural moment to route new raise sites through
  `Catalog.Format` from day one), or a second observed intra-runtime message drift.

## External grounding

| Claim | Source |
|---|---|
| Forwarded-warning path drops `DiagnosticId` and `Fix`; error path synthesizes per-ID descriptors | `src/Heddle.Generator/HeddleTemplateGenerator.cs:312-330` *(verified)*; [06 F2](../research/generator-code-sharing/06-diagnostics-utilities.md) |
| HED7013's documented contract is "carrying no id" | `src/Heddle.Generator/Diagnostics/GeneratorDiagnostics.cs:6-7` *(verified)*; `docs/precompilation.md:202` *(verified)* |
| Only parse-channel warning producer today is the id-less SLL fallback (with Fix); HED2004/HED4005/HED3005 are compile-channel (`CompileWarnings`), never drained by the generator | `src/Heddle/Language/DocumentParser.cs:66-73`, `src/Heddle/Runtime/HeddleCompiler.cs:342-350, :624-629, :1279-1284` *(verified — correction to the research's example, recorded per [spec-conventions](../spec/common/spec-conventions.md))* |
| HED7017 in code and registry, missing from the precompilation doc table (HED7001–HED7016 only) | `GeneratorDiagnostics.cs:136-139`, [registry rows](../spec/common/cross-cutting-decisions.md#claimed-diagnostic-ids-registry), `docs/precompilation.md:191-205` *(all verified)* |
| Existing ID test covers HED0xxx–HED5xxx only, nothing gates HED7xxx or docs | `src/Heddle.Tests/DiagnosticIdTests.cs:46-72` *(verified)* |
| Three line indexes; `HeddleCompileResult`'s leading-`\r` bump vs the shared `\n`-only rule; `LineMap`'s stale equivalence claim | `src/Heddle.Generator/Emit/LineMapper.cs:14-52`, `src/Heddle.LanguageServices/LineMap.cs:9-10, :17-53`, `src/Heddle/Data/HeddleCompileResult.cs:22-37, :54-73` *(all verified)*; [06 F4](../research/generator-code-sharing/06-diagnostics-utilities.md) |
| LSP owns the complete drain rule; generator partial; `CompileResult`/Tool third rendering | `src/Heddle.LanguageServices/DocumentAnalyzer.cs:106-148`, `src/Heddle/Data/HeddleCompileError.cs:29-45`, `src/Heddle.Tool/HeddleRenderer.cs` *(verified)*; [06 F3](../research/generator-code-sharing/06-diagnostics-utilities.md) |
| Five C#-alias tables; key sets differ (`dynamic` present reflection-side, absent symbol-side); `CompletionProvider` copy exists because `FriendlyName` is private | `src/Heddle/Runtime/Expressions/FunctionEntry.cs:85-95`, `src/Heddle.LanguageServices/Completion/CompletionProvider.cs:198-211`, `src/Heddle/Helpers/ReflectionHelper.cs:32-49`, `src/Heddle/Helpers/TypeNameHelper.cs:120-172`, `src/Heddle.Generator/Binding/SymbolTypeResolver.cs:45-54` *(all verified)*; [06 F5](../research/generator-code-sharing/06-diagnostics-utilities.md) |
| `RenderPath` hand-rolls relativization; `FullPath` naive concat vs `FileReader`'s `Path.Combine`; prior divergence recorded as a shipped bug | `src/Heddle.LanguageServices/DocumentAnalyzer.cs:269-293`, `src/Heddle/Data/TemplateOptions.cs:162`, `src/Heddle/FileReader.cs:37-40`, `HeddleTemplate.cs:357-359` *(verified)*; [06 F6](../research/generator-code-sharing/06-diagnostics-utilities.md) |
| `SanitizeName` internal static; both test mirrors exist; test project signed with the same key | `HeddleTemplateGenerator.cs:426-471`, `src/Heddle.Generator.IntegrationTests/DifferentialHarness.cs:294-335`, `PartialTests.cs:38-53`, `Heddle.Generator.IntegrationTests.csproj:8-9`, `Heddle.Generator.csproj:32` *(verified)*; [06 F7](../research/generator-code-sharing/06-diagnostics-utilities.md) |
| Four profile/mode parsers with divergent defaults; LSP defaults `Text` while generator/engine default `Html` post-2.0; `Heddle.Tool` parses no profile/mode (consumer only) | `src/Heddle/Extensions/ProfileExtension.cs:24-37`, `src/Heddle.Generator/Emit/TemplateEmitter.cs:418-427`, `src/Heddle.Generator/Pipeline/ConfigReader.cs:28-36`, `src/Heddle.LanguageServices/WorkspaceConfig.cs:43-51, :70-78`, `docs/precompilation.md:58`, `src/Heddle.Tool/Program.cs` *(all verified)*; [06 F8](../research/generator-code-sharing/06-diagnostics-utilities.md) |
| Escape-table and surrogate-scan duplication is generator-internal; no runtime twin exists | `src/Heddle.Generator/Emit/PieceWriter.cs:23-72`, `Emit/NativeExpressionWriter.cs:290-304`, `Emit/TemplateEmitter.cs:445-469` *(verified)*; [06 F9/F10](../research/generator-code-sharing/06-diagnostics-utilities.md) |
| Reserved-name set `{"out","this"}` duplicated across tiers; five-fault `[Prop]` validation re-implemented | `src/Heddle/Runtime/Expressions/PropLayout.cs:104-160`, `src/Heddle.Generator/Emit/TemplateEmitter.cs:819-930` *(verified)* |
| Linked-`<Compile>` sharing mechanism and existing linked set (IDs, `LinePosition`, `TemplateKey`, `Language/**` glob) | `Heddle.Generator.csproj:50-62` *(verified)*; [07 — proposed layout](../research/generator-code-sharing/07-recommendations.md) |
| `InternalsVisibleTo` for the LSP on the runtime assembly | `src/Heddle/Properties/AssemblyInfo.cs:10` *(verified)* |
| Consolidation order and the drift-#8/#10 rankings this phase resolves | [07 — confirmed live drift, recommended sequencing](../research/generator-code-sharing/07-recommendations.md) |
| The LSP wires four analysis-applicable options plus `assemblies`; `TrimDirectiveLines`/`MaxRecursionCount` sit at ctor defaults with no config key; `ProvideLanguageFeatures` is hardwired `true`; the full `TemplateOptions` surface behind D10's enumeration | `src/Heddle/Data/TemplateOptions.cs`, `src/Heddle.LanguageServices/WorkspaceConfig.cs:18-63`, `src/Heddle.LanguageServices/DocumentAnalyzer.cs:91-104` *(verified 2026-07-25)* |
| ID registry authority, amendments mechanism, breaking-window policy, testing loop | [cross-cutting-decisions.md](../spec/common/cross-cutting-decisions.md), [breaking-windows.md](../spec/common/breaking-windows.md), [testing-standards.md](../spec/common/testing-standards.md) |
| The Q6.1–Q6.3 rulings this plan folds | [open-questions register](open-questions.md) *(user, 2026-07-25)* |

Supplement: [phase-6-diagnostics-utilities-catalog.md](phase-6-diagnostics-utilities-catalog.md) —
the diagnostic-catalog schema, row inventory, factory signatures, and test mechanics backing D4
and D12.

*(Verify at implementation:* all line numbers above were pinned against the working tree on
2026-07-25; re-confirm before relying on them — the anchor symbols are named beside each.*)*

## Implementation record

**Partial landing, 2026-07-25 — WI1–WI8 only.** WI9 (LSP options parity), WI10 (`RenderPath` /
`TemplateOptions.FullPath`) and WI11 (escape/surrogate fold) are **not** in this landing; the
deferred remainder and its blockers are named in the *Status* header. `dotnet build Heddle.sln -c
Debug` green; `dotnet test Heddle.sln -c Debug` green across `Heddle.Tests`,
`Heddle.Generator.Tests`, `Heddle.LanguageServices.Tests`, `Heddle.Generator.IntegrationTests` and
`Heddle.Tool.Tests`, with the three remaining phase-1/3 quarantined skips (×2 TFMs) untouched.

### Where the work landed

| WI | Files |
| --- | --- |
| WI1 | `src/Heddle.Tests/LineIndexVectors.cs` (the golden table, linked into the generator and LSP suites), `LineIndexTests.cs` (red on 11 vectors against the pre-extraction `HeddleCompileResult`), `DiagnosticIdTests.ConstantsAndTheClaimedIdRegistryAgree` (`[CallerFilePath]`-anchored claimed-registry gate, both directions) |
| WI2 | **no change needed** — `docs/precompilation.md` already carries the `HED7017` row, and the code↔registry↔docs lockstep that would have caught the gap already exists (`PipelineDiagnosticsTests.EveryGeneratorDiagnosticIdIsClaimedInTheRegistryAndListedInTheDocsTable`). Both landed with phase 5; the plan's "red today: HED7017" is stale. WI1's runtime-side gate is the direction that was still unguarded and is now closed |
| WI3 | `HeddleTemplateGenerator.ParseAndReport`; `GeneratorDiagnostics.Forwarded(id, isWarning)` + `ForwardedMessage(message, fix)`; `src/Heddle.Generator.Tests/ForwardedDiagnosticTests.cs` |
| WI4 | new `src/Heddle/Data/LineIndex.cs` + csproj link; `HeddleCompileResult`'s `Split('\n')` table and leading-`\r` bump deleted; `Emit/LineMapper.cs` and `LanguageServices/LineMap.cs` reduced to wrappers; `LineMapperAgreementTests.cs`, `LineMapAgreementTests.cs` |
| WI5 | new `src/Heddle/Data/HeddleDiagnosticCatalog.cs` (+ `HeddleDiagnosticSeverity`, `HeddleDiagnosticInfo`, `PropFaults`) + csproj link; 23 `HED7xxx` constants added to `HeddleDiagnosticIds` (public, additive — golden updated); `GeneratorDiagnostics` descriptors become `FromCatalog` projections; `PrecompiledGauntlet`/`PrecompiledTemplates` id consts point at the shared ones; reserved-name set consumed by `ParseContext`, `PropLayout` and `TemplateEmitter`; `src/Heddle.Tests/DiagnosticCatalogTests.cs`, `src/Heddle.Generator.Tests/DiagnosticCatalogTests.cs` |
| WI6 | new `src/Heddle/Language/HeddleDiagnosticProjection.cs` (parse channels, glob-linked) + `HeddleDiagnosticProjection.Runtime.cs` (compile channels, excluded via a widened `**\*.Runtime.cs` exclude); generator and `DocumentAnalyzer.ProjectDiagnostics` both drain through it; `src/Heddle.Tests/DiagnosticProjectionTests.cs` |
| WI7 | new `src/Heddle/Helpers/CSharpTypeNames.cs` + csproj link; `ReflectionHelper.CSharpTypes` deleted (the resolver calls `TryGetType`), `FunctionEntry.FriendlyName` and `CompletionProvider.Friendly` delegate, `TypeNameHelper`'s dead `system.*` switch removed; `SymbolTypeResolver.Keywords` documented as the adapter; `CSharpTypeNamesTests.cs`, `AliasTableLockstepTests.cs` |
| WI8 | `Heddle.Generator.csproj` `InternalsVisibleTo` for `Heddle.Generator.IntegrationTests`; `DifferentialHarness.SanitizeKey` and `PartialTests.Sanitize` reduced to calls on `HeddleTemplateGenerator.SanitizeName` |

### Corrections to the plan, recorded per [spec-conventions](../spec/common/spec-conventions.md)

- **The HED7017 doc gap is already closed** (WI2 above), and so is the `code → docs` direction of
  D12.3 for the `HED7xxx` block. The plan's D12.3 bullet and WI1 row ("red today: HED7017") were
  written against a tree that predates phase 5.
- **The "compile channel the generator never drains" claim holds, with one sharpening.** Verified
  against the current source: `ParseContext.Warnings` has exactly one producer, the id-less SLL
  fallback at `DocumentParser.cs:66-73`, which carries a `Fix`; every id-carrying warning
  (`HED1016`, `HED2002`, `HED2003`, `HED2004`, `HED3001`, `HED3002`, `HED3004`, `HED3005`,
  `HED4002`, `HED4005`, `HED5011`) is added to `CompileWarnings`, and the generator runs no
  compile-channel stage. So the day-one user-visible delta of WI3 is the appended `Fix` sentence,
  exactly as *Back-compat* predicted; the real-ID rule is armed, not yet fired. WI6 deliberately
  keeps the generator on the parse-channel overload (the risk table's mitigation), so this stays
  true after the projection landed.
- **`HED0002` (`ExtensionNotFound`) has no raise site.** The constant and the registry row exist;
  nothing in `src/` assigns it. Catalogued as an error like its siblings; worth a look from
  whichever phase touches extension resolution.
- **`TypeNameHelper.GetBaseTypeOutput`'s alias switch was unreachable**, not merely duplicated: it
  switches on `Type.Name.ToLowerInvariant()` against `"system.int32"`-style keys, and `Type.Name`
  is never namespace-qualified — `ExType(typeof(int)).ToString()` returns `Int32` today, verified
  by probe before the change. The dead branch was **removed** rather than repaired: this spelling
  feeds `ExType.ToString()` through error text and generated code, so making it a keyword mapping
  is a behavior change for the owner of type-name formatting (phase 3) to make deliberately. The
  research's "five tables" count is right; one of the five was never consulted.
- **`MessageFormat` scope, as shipped.** Q6.3's "consumed rows only" resolves in practice to the
  `HED70xx` block alone: those rows are formatted by the descriptor projection. The F2 twin
  vocabularies did *not* migrate to catalog formatting — unifying `HED0001`/`HED7008`,
  `HED3005`/`HED7016` and `HED5007`–`HED5015`/`HED7017` wording means restructuring the two
  `[Prop]` validators and the member-path resolvers, which is phase 3's `PropLayoutCore<TType>` +
  fault-enum and member-path-core work. What *did* ship from that group is the shared
  `PropFaults.ReservedNames` set (three consumers, one list); `PropFaults.FaultOrder` is left to
  phase 3, because an ordered fault list with no consumer is the second copy D4 exists to prevent.
  A gate (`OnlyRowsWithASecondConsumerCarryMessageProse`) pins the scope so widening it is a
  decision, not a drift.

### Deltas a user can see

- **Forwarded warnings gain their `Fix`** (`"{message} Fix: {fix}"`). Today that is exactly the
  SLL-fallback warning, which keeps `HED7013` because it carries no id.
- **Forwarded diagnostics carry their real id when they have one**, on the warning path as well as
  the error path, at their subtype's severity. No shipped warning exercises this yet (see the
  correction above), so the migration note for a stale `NoWarn HED7013` remains a forward-looking
  deliverable.
- **`HeddleCompileResult` columns** for `"\n\r"` sequences and CRLF blank lines. Measured, not
  estimated: of the eight golden documents, five (LF, plain CRLF, lone-`'\r'`, no-EOL, empty) were
  already canonical and are unchanged; three shifted — `"a\r\n\r\nb"` reported offset 5 (the `b`)
  as `2,1` and now reports `3,0`; `"a\n\rb"` reported offset 3 as `2,0` and now reports `2,1`;
  `"a\nb\r\n\rc\n"` reported offset 5 as `2,3` and now reports `3,0`. Build-tier and editor
  positions are unchanged, as D6 intended. No existing golden captured an affected document, so
  the "goldens change once" allowance was not spent.
- **Signature/completion text** for the aliases the two hand-written copies omitted: `uint`,
  `byte`, `char`, `float`, `short`, `ushort`, `sbyte`, `ulong` now display as their C# keyword
  rather than their CLR name in `HED1012`/`HED1013` text and completion lists — the "all three
  surfaces agree because they read one alias table" outcome the Validation-scenarios table asks
  for. No test or golden pinned the old spelling.

### Byte-neutrality

No rendered-output path was touched. The generator's diff is confined to diagnostic reporting, the
`LineMapper` wrapper (pinned value-for-value by `LineMapperAgreementTests` over the shared golden
vectors) and the reserved-name predicate (same set, same order). The differential, corpus-render,
resolver-sweep and snapshot suites in `Heddle.Generator.IntegrationTests` and
`Heddle.Generator.Tests` are green with zero fallback events.

### Concurrency note

Phase 4 was landing `PrecompiledRuntime.DynamicMember` and the schema-version bump during this
pass. Two shared artifacts move with it and are **not** this phase's to update: the
`PrecompiledSchema` version in the generator snapshots (`schemaVersion: 2 → 3`) and the three
`public-api-heddle.txt` rows for `DynamicMember` / `DynamicMemberRoutingSchemaVersion` /
`EmitsDynamicMemberRouting`. The golden's phase-6 delta — the 23 additive `HED7xxx` constants — is
applied.

---

## Implementation record — second pass (2026-07-26): WI9, WI10, WI11 + the alias/numeric reconciliation

The first pass's record above stands unchanged. This pass lands the deferred remainder, which was
waiting on artifacts other phases own; all of them have since shipped. `dotnet build Heddle.sln -c
Debug` green; `dotnet test Heddle.sln -c Debug` green — **4808 passed, 0 failed, 0 skipped**
(4680 before, +128 from this pass's new tests × their TFM legs). The program's quarantine register
is still empty.

### Where the work landed

| WI | Files |
| --- | --- |
| WI9 | `src/Heddle.LanguageServices/WorkspaceConfig.cs` (rewritten: shared parsers, six keys, config-message channel), `HeddleLanguageServiceOptions.cs` (+`TrimDirectiveLines`, `MaxRecursionCount`, `ConfigurationMessages`; profile/mode defaults re-sourced from `HeddleBuildOptions`), `DocumentAnalyzer.BuildTemplateOptions`, `src/Heddle.LanguageServer/LspServer.cs` (`LogConfigurationMessages`), `editors/vscode/package.json` + `src/extension.ts` (mirror settings), `docs/editor-support.md`; gates in `src/Heddle.LanguageServices.Tests/WorkspaceOptionParityTests.cs` and `WorkspaceConfigTests.cs` |
| WI10 | `src/Heddle/Data/TemplateOptions.cs` (`FullPath` → `Path.Combine`), `src/Heddle/FileReader.cs` (`GetFileName` → `_options.FullPath`; the duplicated `_templateName` field deleted), `src/Heddle.LanguageServices/DocumentAnalyzer.cs` (`RenderPath` → `TemplateKey.TryMakeRelative`); pins in `src/Heddle.Tests/TemplateOptionsFullPathTests.cs` and `src/Heddle.LanguageServices.Tests/RenderPathTests.cs` |
| WI11 | `src/Heddle/Language/Expressions/CSharpEscape.cs` (gains `IndexOfLoneSurrogate`; `HasLoneSurrogate` derives from it), `src/Heddle.Generator/Emit/PieceWriter.cs` (`Escape`/`HasLoneSurrogate`/`IndexOfLoneSurrogate` deleted), `Emit/TemplateEmitter.cs` (10 call sites re-pointed); pin in `src/Heddle.Generator.Tests/LoneSurrogateScanTests.cs` |
| Reconciliation | `src/Heddle/Helpers/CSharpTypeNames.cs` (`SymbolExcludedAlias` → `DynamicAlias`, doc corrected), `src/Heddle.Tests/CSharpTypeNamesTests.cs`, two new arms in `src/Heddle.Generator.Tests/AliasTableLockstepTests.cs` |

### The LSP default-profile decision (Q6.2)

**Aligned: the editor's default output profile is now `Html`,** the engine's and the build tier's.
The plan flagged this as a decision separable from adopting the shared parsers, so it is recorded
as one. Three things decide it in the same direction. It is the *documented* default —
`docs/precompilation.md`'s "generator defaults track the engine defaults" and
`TemplateOptions.OutputProfile`'s own XML doc both say `Html` since 2.0; the LSP was the only
surface holding a different opinion, and holding it silently. The delta is **new true positives**:
the editor starts showing exactly the `HED2004`-class lints the build of record already emits for
the same files — under `Text` the editor was not being conservative, it was hiding real build
warnings. And the opt-out is one line (`"outputProfile": "text"`), which is *also* what such a
workspace should be passing its host, so the fix surfaces a latent config gap rather than creating
one.

**User-visible back-compat consequence.** A workspace with no `outputProfile` in
`.heddle-lsp.json` (and a VS Code user who never set `heddle.compile.outputProfile`) sees new
diagnostics in the editor on next upgrade. No build behavior changes, no rendered byte changes, and
nothing new can fail a build. Documented in `docs/editor-support.md` as a warning callout naming
the opt-out. The VS Code extension's mirror default moved with it — it forwards every setting
including unset ones, so leaving it at `"text"` would have pinned the old behavior for every VS
Code user regardless of the server's default.

### WI9 — the completeness gate, as it stands

`WorkspaceOptionParityTests` walks `TemplateOptions`' public instance properties and requires each
to be *wired* or *named* — six wired, eleven excluded, and the gate goes red on a seventeenth
property that is neither. Companion arms assert the key-naming rule (`ConfigKey` = the option's own
name, camelCased), that the reader's key set is exactly the wired set plus `assemblies`, that the
key names agree with `HeddleBuildOptions`' `Heddle`-stripped MSBuild property names, and that an
absent key yields the value a freshly constructed `TemplateOptions` carries.

- **Wired:** `rootPath`, `outputProfile`, `expressionMode`, `fileNamePostfix` (already there;
  profile default flipped, both now parsing through `OutputProfileRules`), plus the two parity
  additions `trimDirectiveLines` and `maxRecursionCount`. Booleans and integers accept their JSON
  kind *or* the string spelling the MSBuild property accepts, through
  `HeddleBuildOptions.TryReadBool`/`TryReadPositiveInt`.
- **Excluded, with the reason each carries in the test:** `TemplateName` (per-document identity),
  `FullPath` (computed, not an input), `Functions` (object-valued — `assemblies` is its config
  form), `Data` (render input), `EnableFileChangeCheck` (render-cache invalidation),
  `PrecompiledMismatchPolicy` (the analyzer never consults the precompiled registry),
  `RenderBudget`, `ValidateModelType`, `Encoder` (render-time limits/handling/encoding),
  `AllowCSharp` (obsolete bridge — `expressionMode` is the key), `ProvideLanguageFeatures` (the
  analyzer's operating mode, hardwired `true`). `FullPath` is an addition to D10's list: it is a
  public property, so the reflection walk sees it, and "computed" is a reason like any other.
- **Reaction policy, unchanged from D10:** an unknown token or a wrong JSON kind keeps the default
  and adds a line to `HeddleLanguageServiceOptions.ConfigurationMessages`, which `LspServer`
  forwards to `window/logMessage`. No `HED` id was minted; `HED6xxx` stays reserved-unclaimed and
  `HED7025` is still the next free generator id.

### Corrections to the plan, recorded per [spec-conventions](../spec/common/spec-conventions.md)

- **WI11's fold was almost entirely already done, in the right direction.** Phase 4 landed
  `CSharpEscape` *and* retired `NativeExpressionWriter.EscapeChar` and `PieceWriter`'s escape
  table; phase 1 landed `PieceWriter.IndexOfLoneSurrogate`. What was actually left was the D11
  sentence the first pass could not act on: the *index*-returning scan still lived generator-side
  next to the shared bool-returning one, i.e. the two loops D11 exists to collapse were simply in
  two different assemblies instead of two different methods. The fold moved the index form into
  `CSharpEscape` and **defined** `HasLoneSurrogate(s) => IndexOfLoneSurrogate(s) >= 0`. The
  generator copies are now dead and deleted: `PieceWriter` retains only `EmitPiece`, and the
  emitter's ten `PieceWriter.Escape` call sites read `CSharpEscape.StringLiteral` directly, so the
  generator has no name of its own for either rule. Repo-wide there is now **exactly one**
  string-escape implementation, one char-escape implementation and one lone-surrogate loop, all in
  `CSharpEscape` — closing the *Success criteria* grep-level check for the escape/surrogate row.
- **Phase 3's `SymbolExcludedAlias` claim holds in full, and the constant was a misnomer.**
  Verified against source: `SymbolTypeResolver.Keywords` carries all sixteen aliases including
  `dynamic → SpecialType.System_Object`, `CSharpTypeNames.Aliases` maps `dynamic → typeof(object)`,
  and `AliasTableLockstepTests` already had the value-agreement third arm plus a
  `DynamicIsNoLongerAnExclusion` fact. But the shared file still *called* the constant
  `SymbolExcludedAlias` and documented the symbol side as "`AliasNames` minus the exclusion" — a
  stale statement of a removed policy, in the file whose whole job is to be the one true statement.
  Renamed to `DynamicAlias` with the reason it is still special (it shares `typeof(object)`, so the
  *display* direction skips it) and the class doc corrected to say the key sets are equal in full.
- **The alias↔`NumericKind` boundary had no gate at all**, which is what the first pass suspected.
  Two arms added to `AliasTableLockstepTests`: every alias classifies to the *same* `NumericKind`
  from its CLR type (`NumericTable.FromClrType`) and from its `SpecialType`
  (`SymbolFacts.ToNumericKind`); and every non-`None` `NumericKind` is reachable from some alias.
  Measured today: the twelve numeric kinds and the twelve numeric aliases correspond exactly, with
  `bool`/`object`/`string`/`dynamic` as the four non-numeric aliases. An `nint`/`nuint` addition to
  either side alone now fails the build tier's test suite, which is the property the first pass
  asked for.
- **`FullPath`'s divergence was live, not theoretical.** Measured before the change:
  `RootPath="/a/b"`, `TemplateName="t"`, `FileNamePostfix=".heddle"` produced `"/a/bt.heddle"`
  while `FileReader.GetFileName()` opened `"/a/b/t.heddle"` — the exact defect shape the
  `HeddleTemplate` comment records as an already-shipped bug. It is now literally one rule:
  `GetFileName()` returns `_options.FullPath`, and `FileReader`'s duplicate `_templateName` field
  is gone.
- **`RenderPath`'s prefix strip carried a defect the plan did not name.** The legacy body is
  transcribed verbatim into `RenderPathTests` as the characterization oracle; eight vectors agree
  with the new body and two deltas are measured and pinned. The interesting one:
  `StartsWith(rootFull)` with no separator check rendered `/rootx/a.heddle` under root `/root` as
  the *relative key* `x/a.heddle` — the editor displaying a file outside the workspace as if it
  were inside it. `TemplateKey.TryMakeRelative` requires the separator. The second delta is the
  root path itself, which used to render as the empty string and now renders absolute.

### Deltas a user can see

- **Editor lints under the `Html` profile by default** — see the decision section above.
- **Two new `.heddle-lsp.json` keys** (`trimDirectiveLines`, `maxRecursionCount`) and two new VS
  Code settings. Purely additive: an absent key is the runtime default the analyzer already used
  implicitly.
- **A bad `.heddle-lsp.json` value now says so.** Previously an unknown `outputProfile` silently
  became `Text` and an unknown `expressionMode` silently became `Native`; both now keep the
  configured default and log a line naming the accepted values.
- **`ImportOrigin`/`ImportedFrom` strings** for hosts that set `RootPath` without a trailing
  separator gain the separator (`"/a/bt.heddle"` → `"/a/b/t.heddle"`), and an import origin outside
  the workspace root is displayed absolute instead of as a bogus relative key.
- **No generated-source delta at all.** The `\a \b \f \v` char-literal spelling change D11
  predicted was already spent by phase 4's `CSharpEscape` landing; WI11 as executed here moves no
  emitted byte.

### Byte-neutrality

No rendered-output path and no emitted-source path was touched. WI11 is a pure relocation of a
scan whose twelve-vector output table was pinned against the pre-fold body first and re-asserted
against the post-fold one (`LoneSurrogateScanTests`); the only behavioral difference is that
`HasLoneSurrogate(null)` returns `false` instead of throwing, and no caller passes `null`. WI9 and
WI10 are editor- and presentation-surface only. Evidence: the eight
`GeneratorSnapshotTests.*.verified.txt` files are untouched (no `.received.*` produced), and the
differential, corpus-render-parity, resolver-sweep and hosted-registry suites in
`Heddle.Generator.IntegrationTests` are green with zero fallback events, on both TFM legs.

### Not implemented, and why

- **The compile-channel drain (`README`'s known program-level gap) is still open.** Nothing in
  WI9–WI11 touches the generator's drain, so the eleven id-carrying warnings still never reach a
  build diagnostic. Closing it is not a phase-6 remainder: it needs the generator to *run*
  compile-channel stages, which is pipeline work no phase 0–6 schedules.
- **`PropFaults.FaultOrder` and the twin-vocabulary message unification** remain phase 3's, per the
  first pass's hand-off. Unchanged here.
