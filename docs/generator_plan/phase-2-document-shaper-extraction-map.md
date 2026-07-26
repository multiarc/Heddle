# Phase 2 supplement — pass-by-pass extraction map

Supplement to [Phase 2 — document shaper](phase-2-document-shaper.md) (D4/D5/D6/D7 and WI2/WI3).
It pins, per machine: the two source locations being merged, the verified deltas between them at
the time of planning, the shared-file destination, the post-extraction call sites, and the
characterization pins WI2 must cover before the swap. Line numbers were verified against head
while planning and are anchored to member names — re-confirm positions at implementation
(*(Verify at implementation)* applies to every line number below; the member names are the stable
anchors).

Authority rule (restated from the plan): where the two copies differ, the **runtime body** is the
one that moves; the generator's copy is deleted. After WI1, the only remaining differences are
brace style and the string-op callee (`ExStringBuilder.*` vs local safe ops), both erased by the
move.

## The pipeline and its drivers

| Step | Runtime driver (`HeddleCompiler.CompileBody`, `src/Heddle/Runtime/HeddleCompiler.cs:76-180`) | Generator driver (`DocumentShaper.Shape`, `src/Heddle.Generator/Emit/DocumentShaper.cs:49-81`) | Shared? |
|---|---|---|---|
| 1 | `ShiftBySkippedTokens` (`:81`) | `ShiftBySkippedTokens` (`:55`) | Pass shared (WI3) |
| 1a | `ScanBraceMisreads` (`:85`) — HED4005 lint, byte-neutral | — | No — runtime-only diagnostics, stays in the driver |
| 2 | `TrimHiddenRemnantLines` when trimming (`:86-87`) | same (`:56-57`) | Pass shared (WI3) |
| 3 | `RemoveDefinitions` (`:88`) | same (`:58`) | Pass shared (WI3) |
| 4 | `ReplaceRawOutput` (`:89`) | same (`:59`) | Pass shared (WI3) |
| 5 | `ProcessBranchSets` (`:90`) — strip + HED3001–HED3005 + orphan machine | `StripBranchSets` (`:60`) — strip only | Machine shared, diagnostics become runtime observer (WI4) |
| 6 | zero-output removal *interleaved with* item compilation: `returnTypeChainedPrevious == null` → `RemoveEmptyItem` (`:128-131`); `ScanHtmlContextLint` on the producing branch (`:137`) | up-front eligibility via injected `isZeroOutput` → `RemoveEmptyItem` (`:66-69`) | `RemoveEmptyItem` pass shared (WI3); classification stays per-side (plan D8) |
| 7 | default chains: compiled; empty chain + non-null `chainedType` yields a zero-length element (`:143-177`) | default chains with `Chain == null \|\| Count == 0` skipped (`:72-78`) | Aligned in WI8 — the generator stops skipping and models the runtime's zero-length element (plan D10, Q2.1 resolved: runtime is the source of truth) |

The relative order of the shared passes (1 → 2 → 3 → 4 → 5 → 6) is the normative ordering contract
written into `DocumentShaping`'s header; the WI3 lockstep test asserts both drivers preserve it.

## Machine-by-machine map

| Machine | Generator source | Runtime source (authoritative) | Shared destination (`src/Heddle/Language/DocumentShaping.cs`) | Verified deltas before extraction | Post-extraction call sites |
|---|---|---|---|---|---|
| `WidenToWholeLine` | `DocumentShaper.cs:375-396` | `HeddleCompiler.cs:443-472` | `DocumentShaping.WidenToWholeLine(BlockPosition, string)` | **Live drift** (plan D1/WI1): generator lacks the `[0, Length]` clamp (`:447-451`), uses `right == Length` not `right >= Length` (`:386` vs `:462`), returns the original block instead of the clamped span on both not-whole-line exits (`:381`,`:395` vs `:457`,`:471`). Erased by WI1 before the move | `RemoveDefinitions`, `RemoveEmptyItem`, `TrimHiddenRemnantLines` (all inside the shared file after WI3) |
| `ApplyRemove` / `Replace` | `ApplyRemove`/`ReplaceSpan`, `DocumentShaper.cs:178-185` (safe) | `ExStringBuilder.ApplyRemove`/`Replace`, `src/Heddle/Strings/ExStringBuilder.cs:360-365` / `:325-358` (`unsafe`) | Safe pair per plan D3 (`ApplyRemove` over `string.Remove`; `Replace` as substring concat) | Semantically equal (research-verified); differ only in allocation profile. `ExStringBuilder` is unlinkable (`unsafe`) — the reason the safe pair exists | Every shared pass; `ExStringBuilder` keeps its other runtime consumers untouched |
| `ShiftBySkippedTokens` | `DocumentShaper.cs:187-233` | `HeddleCompiler.cs:655-725` | `DocumentShaping.ShiftBySkippedTokens(ParseContext)` | Token-equal modulo braces. Three-way classification (enclosing loses `seed` length; wholly-after shifts back; wholly-before `break`s) over `OutputChains`, `DefinitionsBlock.Positions`, `RawOutputItems`, reverse order | `CompileBody` step 1; `Shape` step 1 |
| `TrimHiddenRemnantLines` | `DocumentShaper.cs:295-328` | `HeddleCompiler.cs:484-517` | `DocumentShaping.TrimHiddenRemnantLines(ParseContext, ref string)` | Token-equal modulo braces. Clean-start mapping, reverse traversal, already-removed-span skip, zero-length probe through `WidenToWholeLine` | `CompileBody` step 2; `Shape` step 2 (both under `TrimDirectiveLines`) |
| `ShiftListsAfter` | `DocumentShaper.cs:335-373` (+ copied rationale comment `:330-334`) | `HeddleCompiler.cs:529-567` (+ original rationale `:519-528`) | `DocumentShaping.ShiftListsAfter(ParseContext, BlockPosition, int)` | Token-equal modulo braces; the duplicated bug-fix comment is kept **once**, in the shared file | `TrimHiddenRemnantLines` only |
| `RemoveDefinitions` | `DocumentShaper.cs:235-259` | `HeddleCompiler.cs:727-764` | `DocumentShaping.RemoveDefinitions(ParseContext, ref string, bool trimDirectiveLines)` | Token-equal modulo braces and callee (`ApplyRemove`). Shift predicate compares against **original** block bounds (`>= defStart + defLen`) — preserved verbatim; this is where the pre-WI1 overshoot is minted (widened `seed` > original length can shift a following block past the shortened document) | `CompileBody` step 3; `Shape` step 3 |
| `ReplaceRawOutput` | `DocumentShaper.cs:261-278` | `HeddleCompiler.cs:634-653` | `DocumentShaping.ReplaceRawOutput(ParseContext, ref string)` | Token-equal modulo braces and callee (`ExStringBuilder.Replace` vs `ReplaceSpan`) | `CompileBody` step 4; `Shape` step 4 |
| `RemoveEmptyItem` | `DocumentShaper.cs:280-293` | `HeddleCompiler.cs:410-431` | `DocumentShaping.RemoveEmptyItem(ParseContext, BlockPosition, ref string, bool)` | Token-equal modulo braces and callee; shift predicate `StartIndex > blockPosition.StartIndex` on the **original** position — preserved verbatim | `CompileBody` step 6 (inside the chain-compile loop); `Shape` step 6 |
| Strip machine | `StripBranchSets` `DocumentShaper.cs:107-142`; `ClassifyBranch` `:89-105`; private enum `:87` | `ProcessBranchSets` `HeddleCompiler.cs:210-299`; `Classify` `:301-326`; `BranchBlockKind` `:182-189` | `DocumentShaping.StripBranchSets(ParseContext, ref string, Func<OutputChain, BranchKind>, observer)` + `internal enum BranchKind` | Structural: (a) generator enum lacks `Participant` — runtime classifies `[ScopeChannel]` non-role as `Participant` (`:322-323`, role wins per `:313-320`) which disarms (`:285-288`); generator's `default:` (`:135-137`) also disarms — byte-equal today, aligned explicitly by WI4. (b) runtime interleaves HED3001–HED3005 + the orphan machine — re-hosted as observer per plan D5. (c) R8 definition-first guard present on both (`:96-97` via delegate; `:306-307` via `DefenitionExists`) — stays in each classifier | `CompileBody` step 5 (with observer); `Shape` step 5 (no observer) |
| `CollectGap` | `DocumentShaper.cs:144-156` | `HeddleCompiler.cs:361-385` | private to the shared machine | Runtime adds the HED3001 non-whitespace-gap warning (`:372-381`) — moves to the observer's *gap collected* event (which receives `gapText`); arithmetic + guards (`gapLength <= 0`, bounds) token-equal | shared machine only |
| `ApplyGaps` | `DocumentShaper.cs:158-174` | `HeddleCompiler.cs:387-408` | private to the shared machine | Token-equal modulo braces and callee (right-to-left removal, shift-after per gap; runtime carries the explanatory comment `:393-394` — kept once) | shared machine only |
| Piece slicing | element walk `src/Heddle.Generator/Emit/TemplateEmitter.cs:359-401` (pieces at `:365-366`, `:399-400`; advance at `:396`) | `RuntimeDocument.GetDocumentPieces`, `src/Heddle/Runtime/RuntimeDocument.cs:95-130` | `DocumentShaping.SlicePieces<T>(...)` per plan D6 | Semantically identical walks over different element types (`DocumentShaper.Element` vs `IDataProcessor`); the emitter interleaves per-element work (profile flip, `HostsParticipant`, `BuildCall` with degrade early-out) — hosted in the `onElement` callback | `RuntimeDocument.GetDocumentPieces`; the `TemplateEmitter` body walk; Phase 1's piece-emission item |
| Region-fill matching | `TryBuildGeneratorFillScope`, `TemplateEmitter.cs:1300-1339` | `BuildRegionFillScope`, `HeddleCompiler.cs:1686-1735` (authoritative), retraction `:1744-1748`, layout cache `:1664-1675` | `src/Heddle/Language/RegionFillResolver.cs` per plan D7 | Same four-step rule, different tables (cached `RegionLayout.TryGet` vs linear ordinal scan over `def.Regions`) and different reactions **by design**: runtime keeps/retracts errors + HED5019; generator returns `false` → un-precompile. Both already share `DefinitionMaterializer.Materialize` (`:1731` / `:1334`) | Runtime `BuildRegionFillScope` (this phase, WI6); `TryBuildGeneratorFillScope` (Phase 1, forward pointer) |

## Not moving, and why

| Item | Disposition |
|---|---|
| `ScanBraceMisreads` (`HeddleCompiler.cs:590-632`), `ScanHtmlContextLint`, `WarnIfMissingScopeChannel`, orphan state machine, `IsEmptyParameter` | Runtime-only diagnostics; byte-neutral; stay in `HeddleCompiler` (the branch-set ones re-hosted as strip observer logic, unchanged in text/ID/position) |
| `RegionLayout` (`src/Heddle/Runtime/Expressions/RegionLayout.cs`) | Resolves `ExType` via reflection — unlinkable; abstracted behind the `TryLookupRegion` delegate |
| `ExStringBuilder` (`src/Heddle/Strings/ExStringBuilder.cs`) | `unsafe`, unlinkable; untouched; shaping passes stop calling it (plan D3) |
| Zero-output classification sources (four-name list `TemplateEmitter.cs:208-215`; runtime `returnTypeChainedPrevious == null` `HeddleCompiler.cs:128-131`) | Co-owned with Phase 1 (attribute design); this phase keeps the `isZeroOutput` seam and adds the WI7 lockstep guard |
| `ScanHostsParticipant` (`TemplateEmitter.cs:1404-1416`) and the element-walk participant flag (`:371-375`) | Phase 1 (`ParticipantScan`) — referenced, untouched |
| Default-chain handling (`DocumentShaper.cs:72-78` vs `HeddleCompiler.cs:143-177`) | No shared-file move — the loop stays in each driver — but the asymmetry is removed in WI8: the generator aligns to the runtime's zero-length element, characterization-pinned as **matched** (plan D10, Q2.1 resolved) |
| `Runtime/DocumentsCache.cs` | Deleted (plan D10) — fully commented out, zero references |

## Characterization pins (WI2 must cover every row)

Each pin is a `ParseContext`-level vector executed against **both** call sites pre-swap and against
the shared core post-swap, asserting the working document and every rebased position
(`OutputChains`, `DefinitionsBlock.Positions`, `RawOutputItems`) byte-for-byte / value-for-value:

1. **Shift, three-way:** a skipped token enclosed by a chain; a chain wholly after; a chain wholly
   before (loop `break` reached); same three for a definition position and a raw-output item;
   multiple skipped tokens processed in reverse.
   *(Restoration audit 2026-07-26: as landed, this pin covered the three classes but none of their
   **boundaries**, and six single-comparison mutants survived it. `Pin1_…ClassificationBoundaries`
   adds one row per equality boundary across all three lists.)*
2. **Trim remnants:** whole-line comment remnant (removed); `@\` remnant with content (no-op);
   two comments on one line (removed once via the already-removed-span skip); remnant inside a
   definition block (the `ShiftListsAfter` enclosing case — the documented historical bug).
   *(Restoration audit: the "two comments on one line" row did **not** constrain the
   already-removed-span guard — the second probe declines on its own once the line regains content.
   A three-blank-line row now does. The `ShiftListsAfter` **definitions** enclosing arm was pinned;
   the **chain** arm was not — `Pin2_ShiftListsAfter_EnclosingAndShiftBoundaries` adds it.)*
3. **Widen predicate:** the WI1 vector table (in-bounds whole-line with LF / CRLF / bare CR / EOF
   terminators; content-left and content-right rejections; zero-length probe; the three
   out-of-bounds classes).
4. **Remove definitions:** trimming on/off; widened removal followed by a later same-line block
   (the overshoot mint — post-WI1 both sides clamp identically); shift-predicate boundary cases
   (`StartIndex == defStart + defLen` shifts; one less does not).
5. **Raw output splice:** replacement shorter and longer than the span; chain exactly at the
   splice boundary (`>` predicate, not `>=`).
6. **Remove empty item:** trimming on/off; widened removal with a following chain inside the
   widened whitespace tail.
7. **Strip machine:** opener→continuation→terminal (two gaps, right-to-left application);
   opener→other (disarm, no gap); orphan continuation (gap behavior only — diagnostics asserted
   separately by the existing branch suites); `Participant` between opener and continuation
   (disarms — no gap across it); zero/negative gap (imported zero-length blocks); gap bounds
   guard; definition-shadowed keyword (R8 — classified `Other`).
   *(Restoration audit: this pin's literals were hand-derived **after** the move, and its
   `Participant` row cannot catch the divergence it was written for — inside the strip machine
   `Participant` and `Other` are extensionally equal (both `stripPrev = null`), so no strip-level
   assertion can separate them. The constraint moved to where it exists: enum arity/names, and an
   observer event-stream pin asserting the reported **kind** and the classified → gap → completed
   ordering the re-hosted HED300x diagnostics depend on. The strip behaviour of `Participant`
   remains unpinnable, and now says so.)*
8. **Piece slicing:** element at offset 0 (no leading piece); adjacent elements (no inter-piece);
   trailing remainder; empty element list (single whole-document piece); zero-length element.
9. **Region-fill verdicts:** matched public; dangling; private; default-missing; candidate from a
   different origin (filtered, no lookup performed — pins the lazy layout resolution);
   multiple candidates with mixed verdicts.
10. **Default chains:** empty default chain with non-null chained type — unlike pins 1–9, this
    pin lands with WI8 (the alignment), not pre-swap: it asserts the **matched** behavior — both
    sides model the runtime's zero-length element (plan D10, Q2.1 resolved) — and turns red if
    either side reintroduces the skip.
    *(Restoration audit: as landed, only the **generator** half was pinned
    (`DocumentShaperAdapterTests`), so a runtime-side reintroduction was invisible. The runtime half
    is not reachable as a machine-level vector, so it is pinned over the driver bodies —
    `DocumentShapingPassOrderLockstepTests.NeitherDriverSkipsAnEmptyDefaultChain`.)*
