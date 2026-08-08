# Shared-source architecture — generator ↔ engine

The rules under which the build-time generator and the runtime engine share code. They were
established by the generator ↔ engine code-sharing program (its collapsed decision record is in
[cross-cutting-decisions.md](cross-cutting-decisions.md#program-record--generator--engine-code-sharing-closed));
this document is the standing home for the architecture itself. Any future work that adds,
moves, or edits shared source follows these rules.

## The two program-wide principles

- **The match principle.** The runtime dynamic engine is the primary source of truth; the
  generator must match its validation rules, errors and throws, behaving as if it were part of
  the dynamic engine. The generator cannot always surface the same *warnings* through the same
  channel; such differences may legitimately exist, but matching is the goal and **errors always
  match**. When aligning drift, the runtime's observable behavior is normative — except where a
  ratified decision records the runtime itself as the defect, and anything that would *widen*
  behavior (member visibility, overload semantics, the coercion rail) is a
  [breaking-windows](breaking-windows.md) candidate, not a drift fix.
- **The fallback-legitimacy principle.** Catch-and-degrade is legitimate only for a small,
  researched set of conditions (stale cached data, genuine change-tracking logic); everything
  else is an error that must surface — thrown or reported, never silently degraded. The closed
  taxonomy lives in [precompilation.md](../../precompilation.md#which-fallbacks-are-legitimate);
  widening it means amending the taxonomy, not adding a catch.

## Hard constraints on shared source

- Shared code is **`netstandard2.0`-clean**, contains **no Roslyn types**
  (`Microsoft.CodeAnalysis`), and assumes nothing beyond netstandard2.0's core surface.
- **The generator never references `Heddle.dll`.** Everything it knows about the runtime it
  knows from linked source and symbol metadata.
- Per-side differences enter through **injected delegates** (`Func<string,bool>`-style
  predicates) or the `ITypeFacts<TType>` / `IMemberFacts` seams. The Roslyn adapter lives in
  `Heddle.Generator`; the reflection adapter lives in `Heddle`; the shared file contains
  neither.
- Shared files are **IO-free and diagnostics-free**: shared parse helpers return a parse-error
  *signal*, never a diagnostic type
  ([HeddleBuildOptions.cs](../../../src/Heddle/Precompiled/HeddleBuildOptions.cs) is the model).
  Direct file IO in the generator is a banned-API violation (RS1035) —
  `Heddle.Generator.csproj` sets `EnforceExtendedAnalyzerRules=true` — which is also why the
  content hash is defined over decoded text.
- Where the two sides genuinely operate on different representations (parse tree vs. compiled
  instances; `ISymbol` vs. `Type`), do **not** force a leaky abstraction: pin the rule as
  **linked data plus a lockstep test** (the `DefaultFunctionLockstepTests` pattern), so a
  unilateral change turns a silent byte divergence into a red test naming the rule.

## Linked-`Compile` placement conventions

- Files under `src/Heddle/Language/**` are auto-linked into the generator by the existing
  csproj glob (`..\Heddle\Language\**\*.cs`, excluding `DocumentParser.Runtime.cs`) — zero
  csproj edits.
- `Data/`, `Precompiled/` and `Helpers/` placements each add exactly one
  `<Compile Include="..\Heddle\…" Link="Shared\…" />` line in
  [Heddle.Generator.csproj](../../../src/Heddle.Generator/Heddle.Generator.csproj), following
  the "Shared front-end sources" precedent already in that file.
- Rule tables belong beside the enums they interpret (`Data/`); rules whose inputs are parse
  types belong under `Language/**`.
- One artifact, one owner: a shared file is landed by exactly one side and consumed by the
  other — never forked, never hand-copied.

## The shared surface (as landed)

Rule cores and tables under `src/Heddle/Language/**` (`ParticipantScan`, `SlotRules`,
`CallTargetRules`, `BodyModelRules`, `DocumentShaping` incl. `SlicePieces<T>`,
`RegionFillResolver`, `BranchSetLint`, `OutputLints`, `CompileWarningFactory`,
`HeddleDiagnosticProjection`, `Expressions/**` incl. `EmbeddedCSharpNames`, the
`NumericKind`/conversion tables, `NativeOperatorRules`, `OverloadRank`, `LiteralFormatter`,
`CSharpEscape`, `Members/**` incl. `MemberPathWalk`, `MemberVisibility`, `MemberHopRule`, and
`Binding/**`); precompiled-contract helpers under `src/Heddle/Precompiled/` (`AqnFormatter`,
`ContentHash`, `PrecompiledSchema`, `TemplateKey` + `TryMakeRelative`/`ToPath`/
`TemplateExtension`, `HeddleBuildOptions`); data tables under `src/Heddle/Data/`
(`HeddleDiagnosticCatalog`, `LineIndex`, `OutputProfileRules`, `RenderTypeRules`); and
`src/Heddle/Helpers/CSharpTypeNames.cs`. Shared test-input wiring:
`src/TestCorpus/TestCorpus.props` + `TestCorpusIndex` + `CorpusIntent.cs` (wiring only; the
templates stay in `src/Heddle.Tests/TestTemplate/`).

## Diagnostic catalog and projection — single source

- [HeddleDiagnosticCatalog.cs](../../../src/Heddle/Data/HeddleDiagnosticCatalog.cs) is one
  shared, netstandard2.0, zero-Roslyn data table beside the already-linked
  `HeddleDiagnosticIds.cs`; the generator **projects** it into `DiagnosticDescriptor`s rather
  than hand-building them. The catalog row is the single place severity lives.
- [HeddleDiagnosticProjection.cs](../../../src/Heddle/Language/HeddleDiagnosticProjection.cs)
  is the one drain rule over parse **and** compile channels — a neutral
  `(Id, Message, Fix, IsWarning, Offset, Length, ImportOrigin)` stream; host policies (LSP
  import re-anchoring, the generator's region-fill retract pre-filter, `HeddleCompileResult`
  rendering) stay host-side and layer on top.
- The permanent gate is `DiagnosticIdTests` plus its generator-side twin: constants
  completeness; the three-way code check (const ⇄ catalog row ⇄ descriptor
  `(Id, Title, DefaultSeverity)`); the docs-registry gates parsing the actual markdown tables;
  catalog round-trip on message-format arity; projection equivalence across all three hosts
  (`DiagnosticCorpusVectors`); the `LineIndex` goldens.
- Type-name aliasing is single-sourced in
  [CSharpTypeNames.cs](../../../src/Heddle/Helpers/CSharpTypeNames.cs); the generator's
  `SymbolTypeResolver.Keywords` is an adapter whose key set is asserted equal to the shared
  alias list minus one *named* exclusion (`dynamic`, which has no `SpecialType`).

## `LineIndex` — the `\n`-only rule

Normative, documented on [LineIndex.cs](../../../src/Heddle/Data/LineIndex.cs) itself:

> A line starts at offset 0 and after each `'\n'`; `'\r'` is never a terminator by itself; a
> `'\r'` adjacent to a `'\n'` belongs to the line that `'\n'` terminates; offsets and columns
> count UTF-16 code units.

Accessors: `OffsetToZeroBased`, `OffsetToOneBased`, `ZeroBasedToOffset` (the LSP's lenient
inverse), `LineCount`, `LineStart(i)`. `LineMapper` and `LineMap` are thin wrappers;
`HeddleCompileResult` fills `LinePosition` from the shared index. Rationale: two of the three
prior implementations already used this rule, and both external consumers (Roslyn `#line` /
`SourceText`, LSP UTF-16 positions) specify it; lone-`\r` as a terminator was rejected.

## Template identity

Template identity and naming has one owner —
[D8](cross-cutting-decisions.md#d8--template-identity--naming-policy-has-one-owner) — and
shared code participates through the `TemplateKey` helpers only; no second key grammar may
exist on either side.
