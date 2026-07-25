# Area 02 — `Emit/DocumentShaper.cs` vs runtime document composition

**Generator side:** `src/Heddle.Generator/Emit/DocumentShaper.cs` (398 lines), plus the shaping-adjacent parts of `Emit/TemplateEmitter.cs`.
**Runtime side:** `src/Heddle/Runtime/HeddleCompiler.cs` (`CompileBody` and its shaping helpers), `src/Heddle/Runtime/RuntimeDocument.cs`.

**Headline:** the entire byte-affecting half of `HeddleCompiler.CompileBody` exists twice as hand-maintained copies. `DocumentShaper`'s own header comment admits it ("The emitter-side reimplementation … of the back-end document-shaping machines that live in `HeddleCompiler.CompileBody`"). **One copy has already drifted** (finding 1). Region-fill / import / definition materialization is *not* duplicated — that core was already extracted into `Heddle/Language/DefinitionMaterializer.cs` and `RegionFillCandidate.cs` (both source-linked into the generator) — so the remaining duplication concentrates in document shaping and in the matching/eligibility rules *around* the extracted core.

---

## Finding 1 — `WidenToWholeLine`: ALREADY DRIFTED (highest risk)

**Rule.** The whole-line trim predicate: a removed span is widened to swallow its whole line iff only spaces/tabs sit to its left back to a line terminator/BOF, and only spaces/tabs then one terminator (or EOF) to its right. It is the single predicate behind three removals (`RemoveDefinitions`, `RemoveEmptyItem`, `TrimHiddenRemnantLines`) and therefore directly decides the bytes of every static piece around a directive line.

- Generator: `src/Heddle.Generator/Emit/DocumentShaper.cs:375-396`
- Runtime: `src/Heddle/Runtime/HeddleCompiler.cs:443-472`

**Verified divergence — the runtime has a defensive clamp the generator copy lacks.** Runtime `HeddleCompiler.cs:445-451` clamps `StartIndex` into `[0, document.Length]` and `endIndex` to `<= document.Length` (comment: *"an earlier widened removal on the same line can leave a later block's stored position overshooting the (now shorter) working document. Never dereference past its end."*). Consequences:

1. `DocumentShaper.cs:378` reads `document[left - 1]` from an unclamped `block.StartIndex`; `DocumentShaper.cs:384-394` scans from an unclamped `block.StartIndex + block.Length`. On the input class the runtime clamp was added for, the generator throws `IndexOutOfRangeException`.
2. Runtime `HeddleCompiler.cs:462` tests `right >= document.Length`; generator `DocumentShaper.cs:386` tests `right == document.Length`. When `right > Length` the generator dereferences `document[right]` and throws.
3. The "not whole-line" exits differ: runtime returns the **clamped** span (`HeddleCompiler.cs:457, 471`); generator returns the **original** block (`DocumentShaper.cs:381, 395`). Where both survive, the removed length differs → different working document → different pieces.

**Drift consequence.** The generator exception is swallowed at `src/Heddle.Generator/HeddleTemplateGenerator.cs:249-252` (`catch (Exception) { /* degrade to the dynamic path */ }`), so this manifests as **silent, unreported loss of precompilation** for affected templates — no diagnostic, no test failure, the perf benefit quietly vanishes. If only case (3) fires, it becomes a byte divergence instead.

**Extraction.** Same imperative algorithm; pure `(BlockPosition, string) → BlockPosition`; no types beyond the already-linked `BlockPosition`. **Directly sharable today as a linked `<Compile>` item — the single cheapest, highest-value extraction in this area.**

---

## Finding 2 — The five position-rebasing machines: verbatim clones

**Rule.** Four passes mutate the working document and rebase the three offset-keyed lists (`ParseContext.OutputChains`, `DefinitionsBlock.Positions`, `RawOutputItems`) with a three-way classification: a block *enclosing* the removed span keeps its start and loses `seed` from its length; a block wholly after moves back by `seed`; a block wholly before is untouched (and terminates the reverse loop via `break`). The pass **ordering** is itself a rule.

| Pass | Generator | Runtime |
|---|---|---|
| Pipeline order | `DocumentShaper.cs:55-60` | `HeddleCompiler.cs:81-90` |
| `ShiftBySkippedTokens` | `DocumentShaper.cs:187-233` | `HeddleCompiler.cs:655-725` |
| `TrimHiddenRemnantLines` | `DocumentShaper.cs:295-328` | `HeddleCompiler.cs:484-517` |
| `ShiftListsAfter` | `DocumentShaper.cs:335-373` | `HeddleCompiler.cs:529-567` |
| `RemoveDefinitions` | `DocumentShaper.cs:235-259` | `HeddleCompiler.cs:727-764` |
| `ReplaceRawOutput` | `DocumentShaper.cs:261-278` | `HeddleCompiler.cs:634-653` |
| `RemoveEmptyItem` | `DocumentShaper.cs:280-293` | `HeddleCompiler.cs:410-431` |

Token-for-token identical (modulo brace style and `ExStringBuilder.ApplyRemove` vs the local `ApplyRemove`). Even the bug-fix rationale comment was copy-pasted: `DocumentShaper.cs:330-334` reproduces `HeddleCompiler.cs:519-528` nearly word for word.

**Drift risk.** Any offset-arithmetic fix landing on one side only changes the working document → every static piece boundary → precompiled vs dynamic output byte-for-byte. The asymmetry makes it dangerous: `HeddleCompiler.cs` is a 104 KB file where these methods sit among unrelated type-resolution machinery, with nothing linking them to `DocumentShaper`. Finding 1 proves the copy already broke once.

**Extraction.** Same imperative algorithm, pure over `ParseContext` + `string` + `BlockPosition` — all already linked. Move wholesale into a new shared file (e.g. `src/Heddle/Language/DocumentShaping.cs`) added to the linked `<Compile>` set, with runtime `CompileBody` calling the shared methods. **Only blocker:** `ExStringBuilder.ApplyRemove`/`Replace` (`src/Heddle/Strings/ExStringBuilder.cs:325-365`) live in an `unsafe` file the generator cannot link. Resolve by parameterizing the shared core on a small `ApplyRemove` delegate, or adding a safe `ApplyRemove`/`Replace` pair in the shared file (the generator's `DocumentShaper.cs:178-185` `Substring`-concat version is already semantically equal to `ExStringBuilder.Replace`).

---

## Finding 3 — Branch-set adjacency strip: same machine, split diagnostics

**Rule.** A document-ordered state machine over `OutputChains`: an Opener arms `stripPrev`; a Continuation collects the gap `[prevEnd, nextStart)` and re-arms; a Terminal collects and disarms; anything else disarms. Collected gaps are removed right-to-left, shifting chains after each gap. Plus the R8 "definition-first guard": a leftmost name resolving to an in-scope definition is never a branch keyword.

- Generator: `DocumentShaper.cs:87-174` (`ClassifyBranch` 89-105, `StripBranchSets` 107-142, `CollectGap` 144-156, `ApplyGaps` 158-174)
- Runtime: `HeddleCompiler.cs:210-299` (`ProcessBranchSets`), `301-326` (`Classify`), `361-385` (`CollectGap`), `387-408` (`ApplyGaps`)

The split is deliberate and documented (`DocumentShaper.cs:9-15`): only the byte-affecting half runs in the generator; HED3001–HED3005 diagnostics stay runtime-side. But the strip half is duplicated verbatim, and the two `Classify` implementations differ structurally: the runtime resolves roles via `TemplateFactory.TryGetExtensionType` + `GetBranchRole()` and has a fifth `Participant` kind (`HeddleCompiler.cs:322-323`, R10) that also disarms (`HeddleCompiler.cs:285-288`); the generator resolves via the injected `roleOf` (Roslyn `ExtensionBinder`) and has no `Participant` kind — a `[ScopeChannel]` non-role extension lands in `default:` (`DocumentShaper.cs:135-137`), which happens to disarm too.

**Drift risk.** The `Participant`/`Other` collapse is benign *only because both disarm today*. If the runtime ever gives `Participant` different strip behavior, the generator silently keeps old semantics → byte divergence in exactly the `@if`/`@elif`/`@else` layouts that matter most.

**Extraction.** Same rule, different representation — shared core with adapters. The strip machine + `CollectGap` + `ApplyGaps` are directly sharable if the core takes two delegates (`Func<string,bool> isDefinition`, `Func<string, BranchKind>` classify) — exactly the shape `DocumentShaper.Shape` already exposes (`DocumentShaper.cs:49-51`). The `BranchKind` enum (including `Participant`) would live in the shared file so the runtime cannot add a kind the generator silently misses.

---

## Finding 4 — Zero-output classification: hardcoded name list vs computed null return type

**Rule.** Which chains are removed from the document (and whose lines are swallowed when trimming) rather than rendered.

- Generator: `src/Heddle.Generator/Emit/TemplateEmitter.cs:208-215` — hardcoded four-name list (`model`, `using`, `import`, `profile`), injected into `DocumentShaper.Shape` as `isZeroOutput` (`TemplateEmitter.cs:357-358`, consumed at `DocumentShaper.cs:66-69, 74-77`).
- Runtime: `HeddleCompiler.cs:101, 128-131` — *computed*: `returnTypeChainedPrevious == null` after `CompileItem` walks the chain, i.e. driven by each extension's `InitStart` returning a null `ExType` (e.g. `src/Heddle/Extensions/ModelExtension.cs:11`).

**Drift risk.** High and structurally one-way: any new zero-output built-in (or an existing extension becoming zero-output under some option) changes runtime output with **zero** compile-time signal in the generator. The precompiled template then emits a stray blank line the dynamic path removes — or silently un-precompiles, depending on whether `ExtensionBinder` fails on the name.

**Extraction.** Same rule, different representation — not directly sharable (the runtime answer comes from reflection over `IExtension`). Correct fix is a shared *rule table*: a `ZeroOutputExtensions` list in a linked shared file the runtime asserts against, or better, a `[ZeroOutput]`/`[Directive]` attribute the generator's `ExtensionBinder` reads symbolically — it already reads `BranchRole` and `[ScopeChannel]` that way (`src/Heddle.Generator/Emit/ExtensionBinder.cs:96-99, 129`). The attribute route removes the duplication entirely and matches the existing `RoleOf` single-source design (`TemplateEmitter.cs:472-475`).

---

## Finding 5 — Piece slicing: `GetDocumentPieces` reimplemented in the emitter

**Rule.** Walk surviving elements in document order; when `element.Position.StartIndex > offset`, emit the literal `document[offset .. StartIndex)`; advance `offset` past the element; after the loop, emit the trailing literal if any.

- Generator: `TemplateEmitter.cs:359-401` (piece emission at 365-366 and 399-400, offset advance at 396)
- Runtime: `src/Heddle/Runtime/RuntimeDocument.cs:95-130`

Semantically identical, including the "no leading piece when `StartIndex == offset`" and trailing-remainder cases. This is *the* byte-parity contract: the emitter's `P0..Pn` constants must equal the runtime's `DataProcessor.Piece` strings exactly.

**Drift risk.** Direct byte divergence for any slicing change. Currently moderate — both copies are simple and stable.

**Extraction.** Same imperative algorithm over different element types (`IDataProcessor` vs `DocumentShaper.Element`). Sharable as a small generic core, e.g. `SlicePieces<T>(IReadOnlyList<T> elements, Func<T, BlockPosition> pos, string document, Action<string> onPiece, Action<T> onElement)`. No Roslyn, no runtime types — netstandard2.0-clean.

---

## Finding 6 — `NeedsLocals` vs `HostsParticipant`: same rule, generator's version is narrower (behavioral bug risk)

**Rule.** A body must be provisioned with a `ScopeLocals` frame iff it statically contains a `[ScopeChannel]` participant; nested bodies are separate documents and do not contribute.

- Runtime: `RuntimeDocument.cs:132-190` — `ComputeNeedsLocals` → `ChainNeedsLocals` (**all** items of the chain, `RuntimeDocument.cs:159`) → `ItemNeedsLocals`, which additionally **unwraps `ExtensionParameterCarrier`** (`RuntimeDocument.cs:172-176`) and **recurses through `ChainedParameter` into nested items/chains** (`RuntimeDocument.cs:178-187`). Consumed at `src/Heddle/HeddleTemplate.cs:319`.
- Generator: `TemplateEmitter.cs:371-375` (element loop) and `TemplateEmitter.cs:1404-1416` (`ScanHostsParticipant`); consumed at `TemplateEmitter.cs:564, 596, 618, 1137-1138, 2362`.

**Verified divergence.** Both generator sites inspect **only the leftmost item** of each chain (`chain.Chain[0]`, `TemplateEmitter.cs:371, 1410`) and never recurse into chained parameters or unwrap the carrier. A `[ScopeChannel]` extension that is not leftmost sets `NeedsLocals=true` at runtime but leaves `HostsParticipant=false` in the generator.

**Drift consequence.** Not a whitespace issue: the precompiled body renders without a locals frame, so a branch continuation/terminal cannot read branch state — a behavioral divergence between tiers. (The generator comment at `TemplateEmitter.cs:372-373` reasons about *over*-provisioning being harmless; leftmost-only is an *under*-provision.)

**Extraction.** Same rule, different representation (compiled `TemplateItem`/`TemplateChain` vs parse-model `OutputChain`). Hoist the *traversal shape* (all items, recurse into chained parameters) into a shared parse-model walker over `OutputChain`/`OutputItem` (both linked) parameterized by `Func<string,bool> isParticipant`; at minimum, add a parity test pinning the two.

---

## Finding 7 — Region-fill call-site matching: shared leaf, duplicated eligibility rules

**Rule.** At a definition call site: for each `RegionFillCandidate` whose `Origin` equals the caller content's `OriginIdentity`, look up `candidate.Name` in the callee's region table; no match ⇒ dangling; private region ⇒ rejected; public region ⇒ fetch the region default from `def.Context.DefinitionsBlock.Definitions[name]` and materialize.

- Generator: `TemplateEmitter.cs:1300-1339` (`TryBuildGeneratorFillScope`)
- Runtime: `HeddleCompiler.cs:1686-1735` (`BuildRegionFillScope`), region table via `RegionLayout.Resolve` at `src/Heddle/Runtime/Expressions/RegionLayout.cs:51-86`

**What's already shared (good):** the materialization leaf — both call `DefinitionMaterializer.Materialize(candidate, regionDefault)` (`HeddleCompiler.cs:1731`, `TemplateEmitter.cs:1334`); `src/Heddle/Language/DefinitionMaterializer.cs:10-32` documents itself as consumed by both backends. Import resolution is unified through `ParserSettings.ImportReader` (`src/Heddle/Language/ParserSettings.cs:29-40`), template-key normalization through linked `TemplateKey.TryNormalize` (`HeddleTemplateGenerator.cs:274, 478`).

**What remains duplicated:** the surrounding four-step matching rule — origin-identity filter, region lookup, public/private gate, region-default fetch — written twice with different table representations: runtime resolves through cached `RegionLayout` (`HeddleCompiler.cs:1704`, cache at `:1664-1675`); generator does a linear ordinal scan over `def.Regions` (`TemplateEmitter.cs:1316-1324`). Failure reactions differ by design: runtime retracts/raises HED5019 (`HeddleCompiler.cs:1708-1720, 1744-1748`); generator returns `false` → un-precompile (`TemplateEmitter.cs:1326-1331`).

**Drift risk.** Moderate and asymmetric in a safe direction (generator mismatch degrades to dynamic path rather than wrong bytes). Real exposure: silent precompilation loss if the runtime's region table ever stops being a flat scan (inheritance via `BaseDefinition`, aliasing, case rules) — the generator's flat loop would reject fills the runtime accepts, with no diagnostic.

**Extraction.** Same rule, different representation. Feasible: the matching loop is pure over `DefinitionItem`/`RegionDeclaration`/`RegionFillCandidate`/`ParseContext` — all in `Heddle/Language/` and already linked. A shared `RegionFillResolver.Match(candidates, origin, regions, definitionsBlock)` returning a per-candidate verdict enum (`Matched`/`Dangling`/`Private`/`DefaultMissing`) lets each side keep its own reaction (retract+HED5019 vs un-precompile) while sharing the decision. `RegionLayout` itself cannot move (resolves `ExType` via `ReflectionHelper`), but the lookup it wraps can.

---

## Minor / noted, not worth extracting alone

- **Empty default chains.** `DocumentShaper.cs:72-78` skips default chains with `Chain == null || Count == 0`; the runtime (`HeddleCompiler.cs:143-177`) has no such skip and still adds a zero-length `DocumentElement`. That element renders nothing but contributes to `OptimizeCallTree`'s `totalLength == document.Length` test (`RuntimeDocument.cs:74-91`), i.e. strategy selection — not bytes. Low risk; an unannounced behavioral difference in a file that otherwise claims verbatim parity.
- **`ApplyRemove`/`ReplaceSpan`** (`DocumentShaper.cs:178-185`) reimplement `ExStringBuilder.ApplyRemove`/`Replace` (`ExStringBuilder.cs:325-365`). Semantically equal; unavoidable today because `ExStringBuilder.cs` is `unsafe`. This is the concrete constraint on extracting Finding 2 — solve it there.
- **`Runtime/DocumentsCache.cs` is entirely dead code** (one commented-out block, lines 1-92). The generator's per-run body cache (`TemplateEmitter.cs:1228-1247`, keyed `name@position@contextOffset#fillsDigest`) matches the runtime's `ResolveLayoutCached`/`ResolveRegionLayoutCached` keying *convention* (`HeddleCompiler.cs:1667`) — shared convention, not shared code; not worth unifying.
- **No duplication** with `ScopeMap`/`TemplateResolver`/`FileReader`: the generator resolves from `AdditionalFiles` keys (`HeddleTemplateGenerator.cs:473-490`), a genuinely different concern from `TemplateResolver`'s probe ladder (`TemplateResolver.cs:11-14, 182-208`).

## Suggested order of work (this area)

1. **Finding 1** — fix the drift now (generator lacks the runtime clamp), then extract `WidenToWholeLine` as the first linked shared source. Smallest change; removes a live bug.
2. **Finding 2** — move the five rebasing machines + pipeline order into the same shared file; add a safe `ApplyRemove`/`Replace` there so `ExStringBuilder` stays runtime-only. Bulk of the 398 lines; a pure lift.
3. **Finding 6** — behavioral, not cosmetic; fix the leftmost-only scan regardless of extraction.
4. **Findings 3, 4, 5, 7** — rule-table/adapter work; sequence by appetite.
