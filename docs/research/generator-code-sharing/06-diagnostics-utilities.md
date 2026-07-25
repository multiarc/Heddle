# Area 06 — Diagnostics, emit utilities, and third copies across the repo

**Part A:** `src/Heddle.Generator/Diagnostics/GeneratorDiagnostics.cs`, `Emit/CodeWriter.cs`, `Emit/PieceWriter.cs`, `Emit/LineMapper.cs` vs engine diagnostics.
**Part B:** repo-wide sweep for third copies (`Heddle.Tool`, `Heddle.LanguageServices`, tests).

Findings ordered by drift risk.

---

## F1. Content-hash rule: two independent SHA-256/hex implementations that already differ in input domain

(Also reported independently from the pipeline angle — see [05-pipeline-config.md](05-pipeline-config.md) F1; both agents converged on this as a top finding.)

- Generator: `src/Heddle.Generator/HeddleTemplateGenerator.cs:412-421` (`ComputeContentHash` — hashes Roslyn-**decoded** text, BOM stripped, via `Encoding.UTF8.GetBytes`; hex via `b.ToString("x2", InvariantCulture)`). Input from `AdditionalText.GetText().ToString()` (`:64-75`).
- Runtime: `src/Heddle/Precompiled/PrecompiledGauntlet.cs:186-205` (`HashFile` — hashes the **raw file stream** — plus `HashBytes` and a duplicated `ToHex` loop). Consumers: `CheckStaleness` (`:164-183`), emitted at `TemplateEmitter.cs:2529, :2554`.

**Drift risk: highest — the two sides hash different byte sequences today.** A `.heddle` file with a UTF-8 BOM or UTF-16 encoding hashes differently, so `CheckStaleness` reports stale for an unmodified file → silent permanent fallback. No test pins generator output against `PrecompiledGauntlet.HashFile` (`HashFile` is exercised only against itself in `src/Heddle.Tests/PrecompiledGauntletTests.cs:202`).

**Extraction: directly sharable.** `Heddle/Precompiled/ContentHash.cs` (`HashText`/`HashBytes`/`HashFile` + one `ToHex`), linked beside `TemplateKey.cs`. Extraction forces the BOM/decoding decision to be answered once. Precedent for a lockstep gate exists: `src/Heddle.Tests/DefaultFunctionLockstepTests.cs`.

---

## F2. HED diagnostic ID → message/severity/title: maintained in four places; the forwarding path already drops IDs

**Rule.** Every Heddle diagnostic has a stable `HEDxxxx` ID, fixed severity, and message template; the ID a user sees must be the same whether it came from the build tier, run tier, or editor.

**Copies:**
- `src/Heddle/Data/HeddleDiagnosticIds.cs:10-211` — IDs only, as consts; messages/severities live inline at each raise site (e.g. `HeddleCompiler.cs:341-349`, `PropLayout.cs:105-155`, `ProfileExtension.cs:32-36`).
- `src/Heddle.Generator/Diagnostics/GeneratorDiagnostics.cs:13-139` — the HED7xxx block as full Roslyn `DiagnosticDescriptor`s: a second, structurally different registry.
- `docs/spec/common/cross-cutting-decisions.md:141-168` — the normative registry table.
- `docs/precompilation.md:190-205` — a second doc table of HED7001–HED7016.
- `src/Heddle.Tests/DiagnosticIdTests.cs:47-72` — a hardcoded literal set asserted against `HeddleDiagnosticIds` — but covering only HED0xxx–HED5xxx; nothing gates HED7xxx, and nothing checks either code side against the docs registry.

**Concrete drift already present:**
- **HED7017 exists in code and the spec registry but is missing from `docs/precompilation.md`'s table** — the doc the registry points at as the block's home.
- **The forwarded-warning path loses the ID and the fix:** `HeddleTemplateGenerator.cs:325-330` reports every front-end warning as `HED7013` (`ForwardedWarning`), discarding `warning.DiagnosticId` and `HeddleCompileWarning.Fix` — while the *error* path directly above (`:318-322`) synthesizes a descriptor from `error.DiagnosticId`. A HED2004/HED3005/HED4005 warning surfaces with its stable ID in the LSP but as HED7013 in the build, and `#pragma`-style suppression by real ID doesn't work at build time.
- Descriptors for forwarded IDs are constructed ad hoc per diagnostic (`:318-321`) — severity/title for every runtime ID decided implicitly in one expression, no table.

**Message-template twins (same fault, two wordings):**

| Rule | Runtime copy | Generator copy |
|---|---|---|
| Member path not resolvable on typed model | `MemberPathResolver.cs:77-82` (HED0001) | `GeneratorDiagnostics.cs:72-75` (HED7008) via `SymbolMemberResolver.cs:44-60` |
| Branch continuation/terminal without `[ScopeChannel]` | `HeddleCompiler.cs:332-350` (HED3005 + Fix) | `GeneratorDiagnostics.cs:121-128` (HED7016) + `ExtensionBinder.cs:131-134, 220-230` |
| Malformed `[Prop]` declaration | `PropLayout.cs:83-160` — five IDs, five messages | `TemplateEmitter.cs:824-930` — whole validation order re-implemented, collapsed into one HED7017 with its own fault vocabulary |

**Drift risk: very high** for the ID/severity plumbing (already broken for warnings); **high** for the twins — the reserved-name set `{"out","this"}` and the five-step validation order exist twice with no shared constant.

**Extraction: needs a thin adapter.** A shared netstandard2.0 `HeddleDiagnosticCatalog.cs` — pure data, `id → (title, messageFormat, defaultSeverity)` beside `HeddleDiagnosticIds` (already linked). The generator projects a row to `DiagnosticDescriptor` (a 5-line factory replacing `:318-321`); the runtime formats through `CompileError.ToError`; the LSP reads `Id`. Minimum viable step: share the reserved-name set + fault-order constant, and add a registry↔`GeneratorDiagnostics` reflection test mirroring `DiagnosticIdTests`.

---

## F3. Diagnostic projection (collect → severity → id → fix → position) implemented twice, inconsistently

**Rule.** A host surfaces Heddle diagnostics by draining `ParseContext.Errors`/`Warnings` and `CompileContext.CompileErrors`/`CompileWarnings`, mapping `HeddleCompileWarning` → warning severity and everything else → error, carrying `DiagnosticId` and `Fix`, positioned at `Position.StartIndex`/`Length`.

- `src/Heddle.LanguageServices/DocumentAnalyzer.cs:106-147` — the complete rule: subtype→severity (`:116-118`), Fix (`:119`), ID passthrough (`:134`), dedupe (`:109,114`), import re-anchoring (`:126-133`), all four sources (`:137-145`).
- `src/Heddle.Generator/HeddleTemplateGenerator.cs:312-330` — parse sources only; severity by *collection membership* not subtype; no Fix; ID dropped for warnings; plus a generator-only retract rule (`:308-315`).
- `src/Heddle/Data/HeddleCompileError.cs:29-45` + `HeddleCompileResult.cs:47-73` — a third rendering (`"[{pos}]{id}: {msg}"`), which is what `Heddle.Tool` surfaces (`src/Heddle.Tool/HeddleRenderer.cs:36-37`).

**Drift risk: high** (already divergent per F2). Any new diagnostic channel added to `CompileContext` gets picked up by the LSP and missed by the generator.

**Extraction: directly sharable.** A `HeddleDiagnosticProjection` over the already-linked front-end types yielding a neutral `(Id, Message, Fix, IsWarning, Offset, Length, Origin)` struct. `Heddle.LanguageServices` project-references `Heddle` (with `InternalsVisibleTo` — `src/Heddle/Properties/AssemblyInfo.cs:10`); the generator consumes via linked `<Compile>`.

---

## F4. Line-start index and offset→(line, column) mapping: three implementations, three conventions

- `src/Heddle.Generator/Emit/LineMapper.cs:9-53` — index `:14-25`, binary search `:28-52`; **1-based** (for `#line`).
- `src/Heddle.LanguageServices/LineMap.cs:12-70` — character-identical index loop `:17-30`, search `:36-54`; **0-based** (LSP); plus inverse `PositionToOffset` `:57-69`.
- `src/Heddle/Data/HeddleCompileResult.cs:22-37, :54-73` — a third index built by `document.Split('\n')` with a **different `\r` rule** (`:29-30`), searched via `Array.BinarySearch` + `~index - 1`, 1-based with an in-line clamp.

**Drift risk: high — the copies are not equivalent today.** `LineMap`'s doc comment (`:9-10`) claims to match "the engine's line splitting"; the engine's actual rule (`HeddleCompileResult:24-34`) handles `\r` differently. The same offset can render as a different column in a build error, an LSP squiggle, and `CompileResult.ToString()`.

**Extraction: directly sharable.** One netstandard2.0 `LineIndex` (int[] + binary search, zero deps) with explicit `ZeroBased`/`OneBased` accessors and one documented `\r` rule; `LineMapper` and `LineMap` become 5-line wrappers; `HeddleCompileResult` drops its private table. Natural home: beside the already-linked `LinePosition.cs`.

---

## F5. CLR type → C# alias name ("friendly name"): five tables across four projects

- `src/Heddle/Runtime/Expressions/FunctionEntry.cs:85-95` — `FriendlyName(Type)` for HED1012/HED1013 signature text.
- `src/Heddle.LanguageServices/Completion/CompletionProvider.cs:198-211` — near-verbatim copy (identical nine `if` lines; exists solely because `FunctionEntry.FriendlyName` is private).
- `src/Heddle/Helpers/TypeNameHelper.cs:120-172` — the full `system.int32 → int` switch, plus a C# keyword list `:8-100` and verbatim-identifier escaping `:229-251`.
- `src/Heddle/Helpers/ReflectionHelper.cs:32-49` — `CSharpTypes`, alias→`Type`, 16 entries incl. `dynamic`.
- `src/Heddle.Generator/Binding/SymbolTypeResolver.cs:45-54` — `Keywords`, alias→`SpecialType`, 15 entries (no `dynamic`).

**Drift risk: medium-high.** Key sets already differ; adding e.g. `nint` to one silently diverges what a template can *write*, what the build tier can *bind*, and what error text *displays*.

**Extraction: mixed.** The four reflection-side copies → one directly-sharable `CSharpTypeNames` static (alias↔`Type`, netstandard2.0). The symbol-side map needs a trivial adapter, with the *alias key list* as the shared single source.

---

## F6. Path relativization / root-stripping / template-file-path composition: four rules, inconsistent case handling

- Canonical, already shared: `src/Heddle/Precompiled/TemplateKey.cs:47-140` (linked).
- `HeddleTemplateGenerator.cs:481-490` — `Relative(path, root)`: prefix test **OrdinalIgnoreCase**, feeds `TemplateKey.TryNormalize`, which is **ordinal**. A case-differing root yields a case-mangled leading key segment — precisely the shadowing hazard HED7003 (`GeneratorDiagnostics.cs:104-107`) warns about.
- `src/Heddle.LanguageServices/DocumentAnalyzer.cs:269-296` — `RenderPath`: same intent, different mechanics (`Path.GetFullPath` both sides, OrdinalIgnoreCase, trim, slash-normalize), used for user-visible `ImportedFrom`.
- `src/Heddle/Runtime/TemplateResolver.cs:159-166` — a third normalizer: `Path.HasExtension` (vs `TemplateKey`'s `last.IndexOf('.') < 0` at `TemplateKey.cs:126-128`), `Contains("..")` (vs per-segment rejection), and `/`→`\` — the *opposite* separator direction from every other copy.
- Template file path composed twice: `Data/TemplateOptions.cs:162` (naive concat, consumed by `PartialExtension.cs:67` for the `ImportOrigin` the LSP displays) vs `FileReader.cs:37-40` (`Path.Combine`). The comment at `HeddleTemplate.cs:357-359` ("so the two can never diverge **again**") records this pair already caused a bug once.

**Drift risk: medium-high.** **Extraction: directly sharable** — add `TemplateKey.Relativize(path, root)` with a documented case policy to the already-linked file; point the generator's `Relative` and the LSP's `RenderPath` at it. `TemplateOptions.FullPath` → delegate to the `FileReader` rule (one-line, same-assembly).

---

## F7. Generated entry-class name sanitization: one production copy, two test mirrors

- Production: `HeddleTemplateGenerator.cs:426-471` (`SanitizeName`).
- `src/Heddle.Generator.IntegrationTests/DifferentialHarness.cs:294-335` — `SanitizeKey`, a line-for-line mirror (its own doc comment admits it).
- `src/Heddle.Generator.IntegrationTests/PartialTests.cs:38-53` — `Sanitize`, a simplified variant — already partially divergent.

**Drift risk: medium.** A naming-rule change makes the differential harness silently degrade (`FindEntryPoint` → null, `DifferentialHarness.cs:283-292`) — the tests that exist to catch build/run divergence become the thing that drifts.

**Extraction: trivial.** `SanitizeName` is already `internal static`; add `Heddle.Generator.IntegrationTests` to `InternalsVisibleTo` (`Heddle.Generator.csproj:32`) and delete both mirrors.

---

## F8. Output-profile / expression-mode string parsing: four parsers, four default policies

- `src/Heddle/Extensions/ProfileExtension.cs:24-37` — `@profile()`: OrdinalIgnoreCase `text`/`html`; unknown → **HED2001, template does not compile**.
- `src/Heddle.Generator/Emit/TemplateEmitter.cs:418-427` — the emitter's `@profile()` scan: same match; unknown value **silently ignored**.
- `src/Heddle.Generator/Pipeline/ConfigReader.cs:28-36, :47-58` — MSBuild options; unknown → HED7009; defaults `Html`/`Native`.
- `src/Heddle.LanguageServices/WorkspaceConfig.cs:43-51, :70-78` — `.heddle-lsp.json`: unknown silently defaults to `Native`; `outputProfile` recognizes only `"html"`, everything else → `Text` (a *different default* from the generator's `Html`).

**Drift risk: medium.** (a) Valid-value sets are literals in four places. (b) The policies genuinely disagree: `@profile(hmtl)` is a hard failure on the dynamic tier but silently precompiled with the previous profile by the emitter — the build tier appears to accept a template the run tier rejects (worth a targeted test).

**Extraction: directly sharable.** Shared `TryParseProfile`/`TryParseExpressionMode` beside the enums in `Heddle/Data`; each host keeps its own default-and-report policy while sharing the accepted-token set.

---

## F9. C# literal escaping tables — generator-internal only (negative cross-tier result)

- `Emit/PieceWriter.cs:23-52` (string form: `\n \r \t \0 \a \b \f \v`, `\uXXXX` < 0x20) vs `Emit/NativeExpressionWriter.cs:290-304` (char form — **omits** `\a \b \f \v`). Both valid C#; divergence cosmetic.
- **No runtime twin exists:** the runtime's C# tier generates code through the `.tcs` Heddle templates (`Runtime/CSharpContext.cs`, `LanguageTemplates/*.tcs`), not a StringBuilder writer, and has no escaping table. `CodeWriter.cs` (39 lines) has no duplicate anywhere in the repo. The classic "escaping table maintained across build and run tiers" finding does **not** apply here.

**Risk: low. Extraction:** one `Escape(char)` core inside the generator.

---

## F10. Lone-surrogate scan: two copies inside the generator

`Emit/PieceWriter.cs:54-72` (`HasLoneSurrogate` → bool) vs `Emit/TemplateEmitter.cs:448-469` (`IndexOfFirstLoneSurrogate` → index) — identical loop body; `TemplateEmitter.cs:445-447` calls one and then re-scans with the other. `HasLoneSurrogate(s) == (IndexOf(s) >= 0)` is currently a coincidence, not a guarantee. **Risk: low. Extraction:** keep the index version, derive the bool.

---

## Negative results (checked; no duplication found)

- **HTML/attribute encoding tables:** the only entity table is `src/Heddle/Extensions/ContextEncoders.cs:49-53`; the generator emits *calls* into the runtime encoders rather than re-tabulating entities.
- **`CodeWriter`:** generator-unique (see F9).
- **`Heddle.Tool`:** `Program.cs` (arg parsing) and `HeddleRenderer.cs` (JSON→`ExpandoObject`) re-implement nothing; the tool *consumes* the shared `HeddleCompileResult` rendering (a third consumer for F3, not a third copy).
- **Invariant-culture formatting:** used consistently per-site; no competing helper to unify.

---

## Suggested consolidation order (this area)

1. `ContentHash` (F1) — shared file + build/run lockstep test; smallest change, largest correctness payoff.
2. Diagnostic catalog + the forwarded-warning ID/Fix loss (F2, F3) — fix `HeddleTemplateGenerator.cs:325-330` first as a standalone bug, then extract the catalog.
3. `LineIndex` (F4) — reconcile the three `\r` rules while extracting.
4. `TemplateKey.Relativize` + case policy (F6).
5. `CSharpTypeNames` (F5), `SanitizeName` mirrors (F7), profile/mode parsing (F8).
6. Generator-internal tidy-ups (F9, F10).

F1, F3, F4, F6, F7 are directly sharable under the constraints; F2 and F5 need a thin Roslyn-boundary adapter; only the HED7017/`PropLayout` validator (part of F2) is a genuine refactor rather than a lift-and-shift.
