# Spec — remove legacy `@import()` and document the `ProcessData` string contract

**Status:** Specified — ready for implementation.

**Owning plan:** [import-removal-plan.md](import-removal-plan.md) — Phase 1 (*Remove the legacy
`@import` extension*) and Phase 2 (*Document the `ProcessData` string contract*), plus the resolved
register items Q1, Q2, and the spec-scope item S1.

**Common specs this binds to:** [spec-conventions](spec/common/spec-conventions.md),
[cross-cutting-decisions](spec/common/cross-cutting-decisions.md) (`D1`, `D2`, `D5`, the claimed
diagnostic-ID registry, the cross-spec amendments ledger), [testing-standards](spec/common/testing-standards.md),
[breaking-windows](spec/common/breaking-windows.md). Historical records:
[records.md](spec/records.md).

This is a compact single-document spec ([sanctioned shape](spec/common/spec-conventions.md#document-shapes-folder-layout-and-naming));
it carries all thirteen required content areas, merging *Design decisions* with the per-phase
requirement records and hoisting shared context into *Assumed state*.

---

## 1. Scope and goal

Two independent changes, surfaced by one review of the `@import`/`ProcessData` seam and shipped
together for convenience, not dependency (the declared order is Phase 1 then Phase 2; either may land
first — [D5](spec/common/cross-cutting-decisions.md#d5--implementation-follows-the-owning-plans-declared-order)).

- **Phase 1 (substantive).** Retire the vestigial `@import()` extension. A template that uses
  `@import()` no longer compiles: it produces a **single positioned removal error (`HED4003`, now an
  error)** at the call site, naming `@<<{{ path }}` and `@partial(){{ name }}` as the replacements.
  The name `import` is kept alive **only** as a tombstone so the error is a targeted migration
  signpost, never a generic "unknown extension" fallthrough. Every in-repo `@import` reference is
  swept — docs rewritten to `@<<`/`@partial`, `@import`-specific tests/fixtures deleted or repurposed
  as tombstone pins.

- **Phase 2 (docs-only).** Write down the unwritten contract the value/string rail already enforces:
  `ProcessData` must return a `string` for a textual value; a non-string is coerced to empty output
  on the value rail by a deliberate guard. No behavior change, no code change to the guard, no DEBUG
  assert — only documentation on the surface authors implement.

**Boundary.** Phase 1 does **not** touch `@<<{{ path }}` (the grammar-level composition import) or
`@partial()` — they are the replacements and stay exactly as they are. It is **not** a grammar
change: `@import` was never grammar, so the `.g4` and generated parser are untouched. Phase 2 does
**not** change the guard, `@out`, or any rendering path.

---

## 2. Assumed state (verified against source on this branch)

Every seam below was re-read against current source; where the plan's description drifted, the
correction is recorded here and carried into the decisions.

### 2.1 What `@import()` is today

- `@import` is an **extension**, not grammar:
  [`[ExtensionName("import")]`](../src/Heddle/Extensions/ImportExtension.cs) on `ImportExtension`.
  Its `InitStart` emits the current `HED4003` **warning**, then reads the target file and re-parses
  it into the extension body's own already-isolated context via
  `DocumentParser.Parse(document, ParseContext, CompileContext)`
  ([ImportExtension.cs](../src/Heddle/Extensions/ImportExtension.cs), `InitStart`).
- It **merges nothing**: a subsequent by-name call to an "imported" definition is unresolved
  (pinned by `ImportFormsPinningTests.I04_InlineImportMergesNothing`), and importing
  position-bearing content from a non-zero offset fails outright with *"Error while compiling
  import"* (`ImportFormsPinningTests.I05_InlineImportOfDefinitionLibraryFailsFromOffset`). Its only
  observable effect is that errors in the target file surface on the importing compile. This is the
  footgun being removed; there is no working capability to lose
  ([language-reference.md `## Imports`](language-reference.md), the `@import` paragraph).

### 2.2 The grammar seam the removal error hooks

- Grammar: `extension_id : ID ;` and `call : extension_id? OUT_PARAMSTART … OUT_PARAMEND …`
  ([HeddleParser.g4](../src/Heddle.Language/HeddleParser.g4), rules `extension_id`, `call`). So in
  the parse tree, an `Extension_idContext`'s parent is always the enclosing `CallContext`. `@import()`
  parses as an output chain whose leftmost call has `extension_id` text `"import"`.
- The generated hook `ExitExtension_id(HeddleParser.Extension_idContext)` exists on
  `HeddleParserBaseListener` ([HeddleParserBaseListener.cs](../src/Heddle.Language/generated/HeddleParserBaseListener.cs))
  and is **currently not overridden** in
  [HeddleMainListener](../src/Heddle/Language/HeddleMainListener.cs). `HeddleMainListener` already
  overrides `ExitImport_block` (the `@<<` seam) and tracks a per-body context stack
  (`_parserContextStack`, pushed/popped in `EnterSubtemplate`/`ExitSubtemplate`); `CurrentParseContext`
  is the top of that stack during the single `ParseTreeWalker` pass.
- **`ParseContext.Errors` is shared by reference down the whole context tree.** A sub-context is
  constructed with `Errors = parentContext?.Errors ?? new List<HeddleCompileError>()`
  ([ParseContext.cs](../src/Heddle/Language/ParseContext.cs), ctor). So an error added to *any*
  context's `Errors` (root, `@if`/`@for` body, output block, definition body) lands in the one list
  the root context holds. **This is why a parse-layer detection reaches both tiers for every call
  shape without special-casing** — the exact mechanism `HED4004` already relies on
  ([HeddleMainListener.ExitImport_block](../src/Heddle/Language/HeddleMainListener.cs) adds to
  `CurrentParseContext.Errors`).

### 2.3 Position of the current diagnostic

- The current `HED4003` warning is positioned at the extension's `Position`, which
  `TemplateFactory.Create` sets from the call's `OutputItem.Position`
  ([TemplateFactory.cs](../src/Heddle/Runtime/TemplateFactory.cs), `Create` → `resultExtension.Position = absoluteTextPosition`).
  That `OutputItem.Position` is `GetAbsoluteBlockPosition(callContext)` — `ParseContext.CreateItem`
  takes a `HeddleParser.CallContext` and stamps `GetAbsoluteBlockPosition(context)`
  ([ParseContext.cs](../src/Heddle/Language/ParseContext.cs), `CreateItem`). `GetAbsoluteBlockPosition(ParserRuleContext)`
  returns `new BlockPosition(context.Start.StartIndex, context.Stop.StopIndex − context.Start.StartIndex + 1)`
  — the whole-call span, offset-independent.
  **Consequence:** positioning the new parse-layer error at
  `GetAbsoluteBlockPosition((CallContext)extensionIdContext.Parent)` yields a byte-identical
  `BlockPosition` to today's warning. Verified.

### 2.4 Both-tier forwarding is shape-independent

- **Generator (precompiled) tier.** The generator forwards the whole `parseContext.Errors` list as
  build diagnostics, building a descriptor from each error's `DiagnosticId` at **Error** severity
  ([HeddleTemplateGenerator.cs](../src/Heddle.Generator/HeddleTemplateGenerator.cs), the
  `foreach (var error in parseContext.Errors)` loop). No per-tier special-casing is needed for a new
  error id.
- **Dynamic (runtime) tier.** `DocumentParser.Parse(document, compileContext, out cleanDocument)`
  copies `parseContext.Errors` into `compileContext.CompileErrors` from index 0
  ([DocumentParser.Runtime.cs](../src/Heddle/Language/DocumentParser.Runtime.cs), `Parse` →
  `CopyErrorsTo(context, compileContext, errorFrom: 0)`); `HeddleTemplate.Compile` then returns a
  failure result whenever `compileScope.CompileErrors.Count > 0`
  ([HeddleTemplate.cs](../src/Heddle/HeddleTemplate.cs), `Compile`).
- **The obsolete generator re-emit.** The generator *also* re-emits `HED4003` by scanning
  `parseContext.OutputChains` for a **leftmost, top-level** chain whose `ExtensionName == "import"`
  and reporting a **Warning** ([HeddleTemplateGenerator.cs](../src/Heddle.Generator/HeddleTemplateGenerator.cs),
  the `DiagnosticDescriptor legacyImport` loop). This scan structurally misses nested/consuming call
  shapes and is redundant once detection lives at the parse layer — it is deleted by this spec.

### 2.5 The duplicate-diagnostic hazard a tombstone closes

- On the dynamic tier, `HeddleCompiler.Compile` resolves each call's extension through
  `TemplateFactory.Create(templateName, …)`. If the name is not registered, `Create` catches the
  `KeyNotFoundException` and adds an **unstamped** generic error
  `"Cannot find extension <import>"` (no `DiagnosticId`)
  ([TemplateFactory.cs](../src/Heddle/Runtime/TemplateFactory.cs), `Create`, `catch (KeyNotFoundException)`).
  (`HED0002`/`ExtensionNotFound` is declared but *not* attached here — a pre-existing gap, out of
  scope.)
- So if `ImportExtension` were **deleted**, the dynamic tier would surface **two** diagnostics on any
  `@import()` (the parse-layer `HED4003` **and** the generic `Cannot find extension`), while the
  generator (parse-only, no `TemplateFactory.Create` step) would surface **one** — breaking the
  "identical on both tiers" requirement. Keeping `ImportExtension` registered as a no-op closes this.

### 2.6 The dead overload and the stale shim comment

- `DocumentParser.Parse(string, ParseContext, CompileContext)`
  ([DocumentParser.Runtime.cs](../src/Heddle/Language/DocumentParser.Runtime.cs)) has **exactly one**
  in-repo caller: `ImportExtension.InitStart` (verified by a whole-`src/Heddle` grep for
  `DocumentParser.Parse` / `.Parse(` — the only other three-argument call,
  [HeddleMainListener.cs](../src/Heddle/Language/HeddleMainListener.cs) line ~261, binds
  `(document, ParseContext, ParserSettings)`, a different overload). **Correction to the plan:** this
  overload is **public**, not "internal" — it appears in the public-API snapshot
  ([public-api-heddle.txt](../src/Heddle.Tests/TestTemplate/public-api-heddle.txt),
  `METHOD System.String Parse(System.String, Heddle.Language.ParseContext, Heddle.Runtime.CompileContext)`).
  Disposing it is therefore a public-surface reduction — a breaking change absorbed into the open 2.0 major
  window, licensed by [R1](#9-back-compat-and-migration).
- The `@<<` guard in `ExitImport_block` carries a comment describing "a top-level `@<<` inside a file
  pulled in by the legacy `@import()` shim" ([HeddleMainListener.cs](../src/Heddle/Language/HeddleMainListener.cs),
  the `ExitImport_block` offset-guard comment). That scenario cannot arise once `@import` no longer
  re-parses a file; the comment goes stale (the guard expression itself is unchanged).

### 2.7 The `ImportExtension` public surface stays

- `ImportExtension` is a **public** class in the public-API snapshot with `CTOR()`, `InitStart`,
  `ProcessData`, `RenderData` ([public-api-heddle.txt](../src/Heddle.Tests/TestTemplate/public-api-heddle.txt),
  the `TYPE Heddle.Extensions.ImportExtension …` block). The tombstone keeps the type and all three
  overrides with identical signatures, so those snapshot lines are **unchanged**.

### 2.8 Phase 2 seams

- The value/string rail coerces a non-string `ProcessData` result to empty:
  `as string ?? string.Empty` ([RuntimeDocument.cs](../src/Heddle/Runtime/RuntimeDocument.cs), the two
  value-rail sites) and `as string ?? element.Piece ?? string.Empty` at the third site (the
  `Piece`-first fallback is inert when a processor is present — piece and processor are exclusive — so
  it is equivalent for this contract).
- Built-in scalar **formatters** pre-stringify at their own boundary, so they are never dropped:
  `@int` returns `.ToString(invariant)` ([IntegerExtension.cs](../src/Heddle/Extensions/IntegerExtension.cs),
  `ProcessDataInternal` — the `AbstractHtmlExtension` template-method override of `ProcessData`),
  `@string` coerces to string, `@guid` returns `.ToString(format)`.
- `@out` on the value rail returns its chained value **raw**
  ([OutExtension.cs](../src/Heddle/Extensions/OutExtension.cs), `ProcessData` returns
  `scope.ChainedData`), so a non-string is dropped there today; only the render path stringifies, and
  only via a plain `chained?.ToString()` (not `Convert.ToString(invariant)`) — the code comment names
  the value-rail drop "a separate, pre-existing issue not addressed here"
  ([OutExtension.cs](../src/Heddle/Extensions/OutExtension.cs), `RenderData` and the adjacent comment).
- The base surface authors implement is `AbstractExtension.ProcessData`
  ([AbstractExtension.cs](../src/Heddle/Core/AbstractExtension.cs), `public abstract object ProcessData(in Scope scope);`),
  and the author-facing guide is [custom-extensions.md](custom-extensions.md).

---

## 3. Global invariants

- **G1 — one positioned diagnostic, identical on both tiers.** Any `@import()` call site produces
  exactly one diagnostic: the positioned `HED4003` removal **error**, byte-identical in id, message,
  and position on the dynamic and precompiled tiers, for **every** call shape (top-level, chained /
  consuming, and nested inside an `@if`/`@for` body, an output block, or a definition body).
- **G2 — never an unknown-extension fallthrough.** The removal error is never the generic
  `"Cannot find extension <import>"` diagnostic and never an `HED0002`; the `import` name stays
  registered so the dynamic tier resolves it.
- **G3 — replacements untouched.** `@<<` and `@partial()` compile, render byte-identically, and keep
  their diagnostics (including `HED4004` nesting) exactly as before.
- **G4 — grammar frozen.** No `.g4` edit, no `src/Heddle.Language/generated/` diff
  ([testing-standards grammar-stability gate](spec/common/testing-standards.md#regression-gates)).
- **G5 — grep-clean (executable gate).** After the sweep, a grep for `@import` across `docs/`,
  `samples/`, `src/`, and the test projects returns **only** removal/migration guidance and tombstone
  tests, once the following **complete, named** exclusion set is applied. Every excluded path is excluded
  for a stated reason so the grep can actually be run to green:
  - **Build / dependency output** (never source): `**/bin/**`, `**/obj/**`, `dist/`, `node_modules/`.
  - **Vendored / generated editor assets whose `@import` is CSS**, not Heddle's directive — broaden beyond
    the single csslint file to the whole vendored set: `docs/public/ace/mode-*.js`,
    `docs/public/ace/worker-*.js`, `docs/public/ace/snippets/*.js`, and
    [`csslint_email.js`](../src/Heddle.Language/js/src/mode/css/csslint_email.js) (these carry CSS
    `@import` / `"@import not allowed here."`, unrelated to the Heddle extension).
  - **Historical / amended records that deliberately retain `@import` deprecation-era text** (not live
    author guidance, not edited by this spec — D4c leaves them): `docs/improvement-plan.md`,
    `docs/improvement-spec.md` (the B3 warning decision), `docs/language-assessment.md` (describes
    `@import` as "still present").
  - **This spec and its own bookkeeping**: `docs/import-removal-spec.md`, the plan
    `docs/import-removal-plan.md`, and the spec meta-docs that record the amendment
    (`docs/spec/records.md`, `docs/spec/common/cross-cutting-decisions.md`, `docs/spec/README.md`).

  Outside those exclusions, the only surviving `@import` matches are: the rewritten `## Imports` /
  removal notes in `language-reference.md` and `built-in-extensions.md`, and the tombstone tests
  (`LegacyImportRemovalTests`, the generator/differential removal cases).

---

## 4. Design decisions

### D1 — detect the removal at the shared parse layer

**Decision.** Emit the removal error from a new `HeddleMainListener.ExitExtension_id` override, guarded
on `context.GetText() == "import"`, adding the error to `CurrentParseContext.Errors`. Concrete shape:

```csharp
public override void ExitExtension_id(HeddleParser.Extension_idContext context)
{
    if (context == null) throw new ArgumentNullException(nameof(context));
    if (context.GetText() != "import")
        return;

    // '@import' is removed. Raise the positioned HED4003 removal error at the shared parse layer so the
    // dynamic and precompiled tiers carry the identical diagnostic for every call shape (top-level,
    // chained/consuming, and nested in any subtemplate) — the ParseContext.Errors list is shared by
    // reference down the whole context tree, exactly as the @<< HED4004 detection relies on.
    var call = context.Parent as HeddleParser.CallContext;
    if (call == null)
        return;

    CurrentParseContext.Errors.Add(
        ("'@import' has been removed. Use '@<<{{ path }}' to share definitions and layouts across " +
         "files, or '@partial(){{ name }}' to embed another template's rendered output inline. " +
         "See docs/language-reference.md#imports--.")
        .ToError(CurrentParseContext.GetAbsoluteBlockPosition(call),
            HeddleDiagnosticIds.LegacyImportDirective));
}
```

**Rationale.** The parse layer is the only seam both tiers already share; `ParseContext.Errors` is
shared by reference across the context tree (§2.2), so this reaches every call shape and both tiers
with zero per-tier code — the proven `HED4004` pattern. The `ToError(BlockPosition, string)` overload
stamps the `DiagnosticId` ([CompileError.cs](../src/Heddle/Data/CompileError.cs)); the generator
forwards it at Error severity and the runtime adapter copies it into `CompileErrors` (§2.4).

**Alternatives rejected.**
- *Detect in `ImportExtension.InitStart`* — the generator's parse-only path never runs `InitStart`, so
  a precompiled `@import` would carry no diagnostic (the bug this spec fixes).
- *Detect only via the generator's leftmost/top-level `OutputChains` scan* — structurally blind to
  nested and consuming shapes (§2.4); deleted, not extended.

**Grounding.** §2.2, §2.4; [HeddleParserBaseListener.ExitExtension_id](../src/Heddle.Language/generated/HeddleParserBaseListener.cs);
[HeddleMainListener.ExitImport_block](../src/Heddle/Language/HeddleMainListener.cs).

### D2 — position on the whole-call span (position unchanged)

**Decision.** Position the removal error at `GetAbsoluteBlockPosition((CallContext)context.Parent)` —
the whole `@import(…)` call span — matching today's `HED4003` position exactly.

**Rationale.** The current warning is positioned at the call's `OutputItem.Position`, which is
`GetAbsoluteBlockPosition(callContext)` (§2.3). Walking from the `Extension_idContext` to its parent
`CallContext` reproduces the identical `BlockPosition`, so tests and tooling that anchor on the
`@import` call see no positional drift. `extension_id` only ever appears inside `call` (grammar §2.2),
so the parent is always a `CallContext`; the `as` cast is a defensive no-op that returns without
emitting if the tree is ever malformed.

**Alternatives rejected.** *Position on the `extension_id` token span* (just the word `import`) —
narrower than today's whole-call span; would change the pinned position and diverge from how every
other call-level diagnostic is placed.

**Grounding.** §2.3; [ParseContext.GetAbsoluteBlockPosition](../src/Heddle/Language/ParseContext.cs),
[ParseContext.CreateItem](../src/Heddle/Language/ParseContext.cs).

### D3 — tombstone the `import` name as a registered no-op

**Decision.** Keep `ImportExtension` registered (`[ExtensionName("import")]`) as a pure no-op:
`InitStart` reads no file, emits no warning, stamps no provenance, and returns `null` (directive-style,
no output element); `ProcessData` returns `null`; `RenderData` does nothing. Concrete `InitStart`:

```csharp
public override ExType InitStart(InitContext initContext, ExType dataType, ExType chainedType, ExType parent)
{
    // Tombstone (import-removal-spec D3): '@import' is removed. The positioned HED4003 removal error is
    // raised at the shared parse layer (HeddleMainListener.ExitExtension_id). This extension stays
    // registered ONLY so TemplateFactory.Create("import") resolves the name on the dynamic tier; without
    // it, Create adds a second, generic "Cannot find extension <import>" diagnostic, and the dynamic tier
    // would show two diagnostics where the generator shows one (breaking cross-tier identity). It reads no
    // file, merges nothing, stamps no provenance, and produces no output.
    return null;
}
```

**Rationale.** This closes the duplicate-diagnostic hazard (§2.5) so `HED4003` is the **sole**
diagnostic on both tiers (G1, G2). Returning `null` is the established directive-style contract (the
original `InitStart` also returned `null`) and, because a compile carrying `HED4003` fails before any
render (§2.4), the no-op never renders. Not reading the file also removes the offset-fragility re-parse
entirely — the latent defect the plan notes disappears with it. Keeping all three overrides preserves
the public-API snapshot byte-identically (§2.7).

**Alternatives rejected.**
- *Hard-delete `ImportExtension`* (Option B-analog) — reintroduces the generic
  `Cannot find extension <import>` on the dynamic tier alongside `HED4003` (§2.5), so the two tiers
  diverge; also drops the public type from the snapshot.
- *Special-case the name in `HeddleCompiler`* (Option B) — pushes name-specific knowledge into the
  compiler and needs its own suppression for the generic error; a registered no-op extension is the
  smaller, more local mechanism and reuses the existing registration path.

**Grounding.** §2.5, §2.7; [TemplateFactory.Create](../src/Heddle/Runtime/TemplateFactory.cs);
[HeddleTemplate.Compile](../src/Heddle/HeddleTemplate.cs); [AbstractExtension.InitStart](../src/Heddle/Core/AbstractExtension.cs).

### D4 — reuse `HED4003`, escalate warning → error (cross-spec amendment)

**Decision.** Reuse the existing `HeddleDiagnosticIds.LegacyImportDirective = "HED4003"` id, keeping
its constant name, but **change its severity from warning to error** and **repurpose its message** to
removal guidance. Ownership and semantics of `HED4003` move from
[improvement-spec B3](improvement-spec.md#b3--import-deprecation-warning-hed4003) (a positioned
*warning*) to this spec (a positioned *removal error*). This is a cross-spec **amendment**. Its meta-doc
edits are **applied at spec-authoring time** (the repo pattern — a spec records its registry claim and
ledger entry in the same change); WI-5 verifies them present and does not re-apply:

- (a) The claimed-ID **registry** row for `HED4003` in
  [cross-cutting-decisions.md](spec/common/cross-cutting-decisions.md#claimed-diagnostic-ids-registry)
  **already** points at this spec with the new semantics ("`@import()` removal error, positioned at
  the call; severity error").
- (b) An **amendments-ledger** entry is **already** appended to
  [records.md](spec/records.md#cross-spec-amendments-ledger) (*Amendment:* `HED4003` repurposed
  warning → removal error; *Made by:* this spec; *Amends:* improvement-spec B3 / registry `HED4003`) —
  the ledger is append-only, so it must not be duplicated.
- (c) [improvement-spec.md](improvement-spec.md) text is **not** edited — the current end-state of a
  decision is its spec plus the ledger.

**Rationale.** [D1](spec/common/cross-cutting-decisions.md#d1--stable-diagnostic-ids-hedxxxx) forbids
reusing a **shipped** id; the `HED4003` **warning** has **not** shipped — it lives only under
`[Unreleased]` (CHANGELOG) and appears in no release tag (`v1.0.0`/`v1.0.1` carry no `HED4003`; there is
no `v2.0.0` tag). A single id for "don't use `@import`" is simpler than minting a second id and retiring
the first, and warning-suppression continuity is a non-issue because the `HED4003` warning never shipped —
no consumer has a suppression to break (resolved Q1). Escalating severity in place is exactly why the id
encodes area, not severity (D1's rejected `HEDW`/`HEDE` alternative).

**Alternatives rejected.** *Mint a new `ImportDirectiveRemoved` id and retire `HED4003`* — declined:
adds a registry entry and a retired-id note for no benefit on an unreleased id.

**Grounding.** [D1](spec/common/cross-cutting-decisions.md#d1--stable-diagnostic-ids-hedxxxx); the
[registry](spec/common/cross-cutting-decisions.md#claimed-diagnostic-ids-registry) `HED4003` row; the
[amendments ledger](spec/common/cross-cutting-decisions.md#cross-spec-amendments-ledger) mechanism;
[records.md 2.0 record](spec/records.md#the-20-breaking-window--as-shipped-record). Resolved Q1
([plan register](import-removal-plan.md)).

### D5 — the removal-error message text (final, normative)

**Decision.** The `HED4003` message is exactly:

> `'@import' has been removed. Use '@<<{{ path }}' to share definitions and layouts across files, or '@partial(){{ name }}' to embed another template's rendered output inline. See docs/language-reference.md#imports--.`

**Rationale.** The message must (a) state the removal plainly, (b) name **both** replacements with
their real call syntax, and (c) point at the reference. `@<<{{ path }}` is the composition import
(grammar `import_block`, no parentheses); `@partial(){{ name }}` is the correct partial invocation —
the grammar requires the `()` before the `{{ name }}` body (a bare `@partial{{…}}` does not match the
`call` rule), so the message uses `@partial(){{ name }}`, not the plan's shorthand `@partial{{ name }}`.
The anchor `#imports--` is the slug the repo's own cross-links use for the `## Imports` heading
(built-in-extensions.md, coming-from-liquid.md, the `HED4004` registry row).

**Alternatives rejected.** *Keep the old "surprising isolation semantics" warning message* — it framed
`@import` as usable-but-discouraged; the removal error must read as terminal and name the alternatives.
*Write `@partial{{ name }}`* (no parens) — does not parse; corrected to `@partial(){{ name }}`.

**Grounding.** [HeddleParser.g4](../src/Heddle.Language/HeddleParser.g4) `call` / `import_block`;
[language-reference.md `## Imports`](language-reference.md); [coming-from-liquid.md](coming-from-liquid.md).

### D6 — delete the obsolete generator re-emit and the `InitStart` warning

**Decision.** Delete the generator's leftmost/top-level `HED4003` re-emit loop
([HeddleTemplateGenerator.cs](../src/Heddle.Generator/HeddleTemplateGenerator.cs), the
`DiagnosticDescriptor legacyImport` block over `parseContext.OutputChains`) and the warning emission in
`ImportExtension.InitStart` (folded into the D3 no-op). Detection is solely at the parse layer (D1).

**Rationale.** With D1 in place, both sites are redundant, and the generator scan is actively wrong for
nested/consuming shapes (§2.4). Leaving either would risk a *duplicate* `HED4003` on the generator tier
(the forwarded parse-layer error **plus** the scan) — the opposite failure of G1.

**Alternatives rejected.** *Keep or extend the generator's `OutputChains` scan* (fix it to cover nested/
consuming shapes instead of deleting it) — rejected: it would duplicate the parse-layer detection D1
already provides on both tiers, reintroducing the double-`HED4003` hazard and keeping a second, tier-local
detection site (violating the DRY single-seam goal of §11). *Keep the `InitStart` warning as a belt-and-
suspenders emitter* — rejected: the generator's parse-only path never runs `InitStart`, so it adds nothing
on that tier and, on the dynamic tier, would re-add the retired warning next to the new error.

**Grounding.** §2.4; [HeddleTemplateGenerator.cs](../src/Heddle.Generator/HeddleTemplateGenerator.cs).

### D7 — dispose the dead public overload and the stale shim comment

**Decision.**
- **Delete** the now-uncalled public overload
  `DocumentParser.Parse(string, ParseContext, CompileContext)`
  ([DocumentParser.Runtime.cs](../src/Heddle/Language/DocumentParser.Runtime.cs)) and update the
  public-API snapshot ([public-api-heddle.txt](../src/Heddle.Tests/TestTemplate/public-api-heddle.txt))
  by removing exactly its one line
  (`METHOD System.String Parse(System.String, Heddle.Language.ParseContext, Heddle.Runtime.CompileContext)`).
- **Rewrite** the stale `@import()`-shim comment on the `@<<` offset guard in
  `ExitImport_block` ([HeddleMainListener.cs](../src/Heddle/Language/HeddleMainListener.cs)) to drop
  the now-impossible "top-level `@<<` inside an `@import()`ed file" scenario, keeping the
  `Offset != 0` rationale (genuine in-file nesting) and the `Count > 1` disjunct as a defensive guard.
  **The guard expression is unchanged** (comment-only edit).

**Rationale.** The overload's sole caller (`ImportExtension.InitStart`) is removed by D3, so it is dead
(§2.6). Removing it is a public-surface reduction — a breaking change absorbed into the open 2.0 window
(R1). It is one line of the snapshot; the `ImportExtension` snapshot lines are untouched (§2.7). The shim comment describes a
scenario that cannot occur once `@import` no longer re-parses a file.

**Alternatives rejected.** *Keep the dead overload for snapshot stability* — considered (the brief
permits it), but the ratified plan directs disposing dead `@import`-only internals, and R1 licenses the
public removal; keeping it would leave a public method reachable by no caller and mislabel it as still
serving the `@import()` path. The one-line snapshot delta is expected and reviewed.

**Grounding.** §2.6; the caller-graph grep in §2.6.

### D8 — tombstone lifespan: indefinite within 2.x

**Decision.** The tombstone (registered no-op + positioned `HED4003` removal error) is kept
**indefinitely within 2.x**. No decay to a generic unknown-extension error is scheduled; no future
removal of the tombstone is planned in this initiative.

**Rationale.** Resolved Q2 (user, 2026-07-12). A permanent, targeted signpost costs a near-zero
name-comparison per parsed call and always beats a generic diagnostic for anyone who reaches for the
removed construct.

**Alternatives rejected.** *Let the tombstone decay to a generic unknown-extension error after a soak*
(schedule a future removal of the registered no-op) — rejected: it trades the targeted migration signpost
for the generic `Cannot find extension <import>` (the very fallthrough G2 forbids) with no offsetting
benefit, and adds a scheduled future edit. (Hard-deleting the extension now, or minting a fresh id, are
rejected under D3/D4.)

**Grounding.** Resolved Q2 ([plan register](import-removal-plan.md)).

### D9 — migration-sweep dispositions

**Decision.** Sweep every in-repo `@import` per §5.2 with the dispositions below; the sole surviving
references after the sweep are removal/migration guidance and tombstone tests (G5). Notable calls:

- **S1 (C19 branch-scan harness).** `BranchSetCompilerTests.C19` uses `@import(){{…}}` only as a
  non-branch "Other block" to prove the branch scan does not misfire across it. Re-express the vehicle
  with **`@date(D){{yyyy}}`** — an ordinary output-producing non-branch extension with a subtemplate
  body, the pattern C07 already uses ([BranchSetCompilerTests.C07](../src/Heddle.Tests/BranchSetCompilerTests.cs)),
  and the closest analog to the bodied `@import(){{…}}` vehicle. The property under test (a non-branch
  block between `@if` and `@else` draws no `HED3xxx`) is preserved; because the `@import` limitation is
  gone, the re-expressed case now also **compiles successfully**.
- **`ergo-import-inline.heddle`** is the only fixture containing `@import` (verified: a repo-wide
  `@import` grep over `**/*.heddle` matches this file only). **Delete** it and remove its name from the
  `ExpectedPrecompiled` set in
  [CorpusDifferentialTests](../src/Heddle.Generator.IntegrationTests/CorpusDifferentialTests.cs). The
  other `ergo-import-*.heddle` fixtures use `@<<` (`ergo-import-composition`, `ergo-import-library`,
  `ergo-import-empty`) or are diagnostic fixtures (`ergo-import-broken`) and are **kept**.

**Rationale.** These are the plan's ratified dispositions; the concrete `@date` vehicle and the
`ergo-import-inline`-only fixture finding are verified here so the implementer makes no choice.

**Marginal delta (noted).** The re-expressed C19 (`@date(D){{yyyy}}`) is essentially C07 minus its
interior whitespace, and it drops C19's original *"the non-branch block is `@import()`, whose `InitStart`
runs after `ProcessBranchSets`"* property — that property is inherently unreproducible once `@import` is
removed. What C19 actually pins — *a non-branch "Other" block between `@if` and `@else` draws no `HED3xxx`
and does not orphan the `@else`* — is preserved, and because the `@import` offset limitation is gone the
re-expressed case now also **compiles successfully** (a strict improvement over the old always-failing
compile). The `branch-import-else.heddle` fixture (its only content is `@else(){{2}}`; it holds no literal
`@import`) is **no longer referenced by C19** after the re-expression, but **stays** referenced by
[CorpusDifferentialTests](../src/Heddle.Generator.IntegrationTests/CorpusDifferentialTests.cs)
`ExpectedPrecompiled` and is unaffected by the sweep.

**Alternatives rejected.** *Pick a different non-branch vehicle for C19* (`@string`, `@money`, a bare
literal) — rejected: `@date(D){{yyyy}}` is the closest analog to the removed bodied `@import(){{…}}` (an
ordinary output-producing extension with a subtemplate body) and reuses the exact pattern C07 already
pins, minimizing churn. *Keep C19 on `@import()`* — impossible once the construct errors.

**Grounding.** §2 verifications; [BranchSetCompilerTests](../src/Heddle.Tests/BranchSetCompilerTests.cs)
(`C07`, `C19`); [CorpusDifferentialTests](../src/Heddle.Generator.IntegrationTests/CorpusDifferentialTests.cs);
the `@import`/`.heddle` greps.

### D10 — Phase 2: document the `ProcessData` string contract (docs-only, no assert)

**Decision.** State the value/string-rail contract on the base surface authors implement
(`AbstractExtension.ProcessData` XML doc) and in the author-facing guide
([custom-extensions.md](custom-extensions.md)): *return a `string` for a textual value; a non-string
yields empty output on the value rail — a deliberate guard (no internal `ToString()` leaks into
concatenated output) that also drops otherwise-meaningful boxed scalars there, so stringify at your own
boundary.* **No** guard change, **no** `@out` change, **no** DEBUG assert.

**Rationale.** The behavior is already correct and shipped (§2.8); built-in formatters pre-stringify so
they are safe, but the guard also drops a non-string an author might have meant to emit (`@out`'s raw
value-rail return is the in-tree witness). A doc closes the gap where a code change cannot — a
third-party extension is not in this repo. A DEBUG assert is rejected: it would false-positive on the
legitimate "render-only / no textual value" case the guard exists to serve (resolved in the plan).

**Alternatives rejected.** *Add a DEBUG assert on non-string results* — false-positives on render-only
extensions. *Change the guard to stringify* — would leak internal `ToString()` into concatenated
output, the exact hazard the guard prevents; also a byte-changing behavior change out of scope.

**Grounding.** §2.8; [RuntimeDocument.cs](../src/Heddle/Runtime/RuntimeDocument.cs) value-rail sites;
[OutExtension.cs](../src/Heddle/Extensions/OutExtension.cs); [AbstractExtension.cs](../src/Heddle/Core/AbstractExtension.cs).

---

## 5. Implementation plan

Declared order: **Phase 1 (WI-1 … WI-8)** then **Phase 2 (WI-9)**. Within Phase 1, the TDD verdict
(§8) puts the new-behavior tests first (WI-6a), then the code (WI-1 … WI-5), then the sweep/repurpose
(WI-6b … WI-8). **The four meta-doc amendment edits (registry row, both ledger entries, breaking-windows
withdrawal, README index row) are already applied in the working tree at spec-authoring time** — the repo
pattern is that a spec records its registry claim, ledger entry, and register withdrawal in the same change
as the spec. WI-5 therefore only **verifies** those four are present (does **not** re-apply them; the ledger
is append-only and must not gain duplicate rows) and lands the one still-pending code edit (the
`HeddleDiagnosticIds.cs` XML doc).

### Phase 1

**WI-1 — parse-layer detection.** Add the `ExitExtension_id` override (D1 shape) to
[HeddleMainListener.cs](../src/Heddle/Language/HeddleMainListener.cs), alongside `ExitImport_block`.
*Completion:* a top-level `@import(){{x}}` compile fails with a `HED4003` error positioned at the call;
the generated tier reports the same at build time.

**WI-2 — tombstone the extension.** Rewrite [ImportExtension.cs](../src/Heddle/Extensions/ImportExtension.cs)
to the D3 no-op: `InitStart` returns `null` with no file read / no warning / no provenance;
`ProcessData` returns `null`; `RenderData` is empty. Remove the now-unused `StampImportOrigin` helper,
the `ImportOrigin` plumbing, and the now-unused `using`s (`System.IO`, `Heddle.Language`, and any other
left dangling). *Completion:* the file compiles warning-clean; the dynamic tier shows **one**
diagnostic (`HED4003`), not a second `Cannot find extension <import>`.

**WI-3 — delete the obsolete generator scan.** Remove the `DiagnosticDescriptor legacyImport` re-emit
loop from [HeddleTemplateGenerator.cs](../src/Heddle.Generator/HeddleTemplateGenerator.cs) (D6).
*Completion:* a precompiled `@import()` yields exactly one `HED4003` **error** (forwarded from
`parseContext.Errors`), no warning; grep of the generator project for `HED4003` finds no literal.

**WI-4 — dispose dead internals.** Delete the `DocumentParser.Parse(string, ParseContext, CompileContext)`
overload from [DocumentParser.Runtime.cs](../src/Heddle/Language/DocumentParser.Runtime.cs); rewrite the
stale shim comment in [HeddleMainListener.ExitImport_block](../src/Heddle/Language/HeddleMainListener.cs)
(guard expression unchanged) (D7). Update
[public-api-heddle.txt](../src/Heddle.Tests/TestTemplate/public-api-heddle.txt) by removing the single
`Parse(System.String, Heddle.Language.ParseContext, Heddle.Runtime.CompileContext)` line. *Completion:*
solution builds; the public-API snapshot diff is exactly that one removed line (the `ImportExtension`
lines untouched).

**WI-5 — diagnostic doc + amendment bookkeeping.**

*Pending code edit (the only WI-5 code work):*
- Update the `HED4003` XML doc in [HeddleDiagnosticIds.cs](../src/Heddle/Data/HeddleDiagnosticIds.cs)
  (`LegacyImportDirective`) — still warning-era in code — to describe a **removal error** naming `@<<` and
  `@partial()`, positioned at the call.

*Already applied at spec-authoring time — **verify present, do NOT re-apply** (re-applying would double the
append-only ledger):*
- The `HED4003` **registry** row in
  [cross-cutting-decisions.md](spec/common/cross-cutting-decisions.md#claimed-diagnostic-ids-registry)
  already points at this spec (Owner → this spec; Notes → "`@import()` removal error … severity error;
  repurposed from the improvement-spec B3 deprecation warning").
- **Both** ledger entries in [records.md](spec/records.md#cross-spec-amendments-ledger) are already present:
  the `HED4003` warning → removal-error repurpose entry (D4b), and the `@import() removal` candidate-withdrawal
  entry.
- The `@import() removal` candidate row is already **withdrawn** from the
  [breaking-windows next-window register](spec/common/breaking-windows.md#next-window-candidate-register)
  (R1 pulls the removal into the open 2.0 window, so it no longer awaits a future breaking window).
- The [spec index README row](spec/README.md) already describes this initiative.

*Completion:* `HeddleDiagnosticIds.cs` XML doc describes the removal error; the four meta-doc edits above are
confirmed present (no duplicates added); the improvement-spec text is unchanged.

**WI-6 — repurpose the removal/tombstone tests** (WI-6a test-first, WI-6b after code).
- **`LegacyImportWarningTests`** ([src/Heddle.Tests](../src/Heddle.Tests/LegacyImportWarningTests.cs)) →
  rename to `LegacyImportRemovalTests` and repurpose from warning to **error**: read
  `CompileResult.Errors` (not `CompileWarnings`) filtered by
  `DiagnosticId == HeddleDiagnosticIds.LegacyImportDirective`; assert the exact D5 message, a positioned
  whole-call span, and severity error (presence in `Errors`). Add call-shape cases (§8). Assert the
  error is **not** the generic `Cannot find extension` diagnostic. `@<<` raises none.
- **`ImportFormsPinningTests`** ([src/Heddle.Tests](../src/Heddle.Tests/ImportFormsPinningTests.cs)) →
  **delete** the four `@import`-only cases `I04`–`I07` (they pinned file-reading / offset-failure
  behavior that no longer exists) and drop the `@import()` sentence from the class doc. **Keep** the
  `@<<` cases `I01`, `I02`/`I03`/`I08`, `I09` unchanged.
- **`ComposeImportNestingTests`** ([src/Heddle.Tests](../src/Heddle.Tests/ComposeImportNestingTests.cs)) →
  **delete** the two `@import()`-shim tests (`ComposeImport_TopLevelInsideImportShim_…`,
  `ComposeImport_ImportShimInsideIf_…`) and the now-unused `AssertNoRawException` helper. **Keep** the
  `@<<` nesting tests. The `regr-compose-shim.heddle` fixture stays (a standalone `@<<` corpus fixture).
- **`HeddleGeneratorTests.LegacyImportEmitsHed4003AtTheCallSite`**
  ([src/Heddle.Generator.Tests](../src/Heddle.Generator.Tests/HeddleGeneratorTests.cs)) → assert
  `DiagnosticSeverity.Error` (not Warning), the D5 message (`Contains("has been removed")` and the
  `#imports--` anchor), positioned at the call. **Keep** `ComposeImportEmitsNoHed4003InGenerator`.

*Completion:* the repurposed suites pass on all TFMs; no test references the old warning message.

**WI-7 — S1 branch-scan harness.** Re-express `BranchSetCompilerTests.C19`
([src/Heddle.Tests](../src/Heddle.Tests/BranchSetCompilerTests.cs)) as
`@if(A){{1}}@date(D){{yyyy}}@else(){{2}}` with a model carrying `A` and `D` (D9). Assert
`CompileResult.Success` and that `CompileResult.ErrorList` contains no `HED3*`. Rename/retitle the case
to "a non-branch Other block between `@if` and `@else` does not orphan the `@else`". *Completion:* C19
passes; it no longer references `@import`.

**WI-8 — fixture + doc + CHANGELOG sweep.**
- **Delete** [ergo-import-inline.heddle](../src/Heddle.Tests/TestTemplate/ergo-import-inline.heddle) and
  any sibling golden; remove its name from `ExpectedPrecompiled` and update the stale "legacy
  `@import()` shim" comment (the `regr-compose-shim.heddle` note) in
  [CorpusDifferentialTests](../src/Heddle.Generator.IntegrationTests/CorpusDifferentialTests.cs).
- **[language-reference.md](language-reference.md)** `## Imports` (≈ lines 962/989/997) — rewrite the
  `@import()` paragraph (and the "two independent import spellings" intro) to describe `@import()` as
  **removed**: any call site now produces the positioned `HED4003` error; use `@<<{{ path }}` to share
  definitions/layouts or `@partial()` to embed rendered output. Keep the `@<<` description and the
  `HED4004` note. **Also** (≈ line 1100) remove the `@import(){{…}}` entry from the whitespace-trimming
  "zero‑output directives" list (`@using`, `@model`, `@profile(){{…}}`, `@import(){{…}}`, …) — `@import`
  is no longer a live directive, so it must not be enumerated as one. This is a second live `@import`
  site in the file, distinct from the `## Imports` section.
- **[built-in-extensions.md](built-in-extensions.md)** — rewrite the `### import` section as a removal
  note (the name is a tombstone; `@import()` now errors with `HED4003`; point at `@<<`/`@partial`);
  drop the `import` rows from the two extension tables (the "directive (compile-time body)" table and
  the `ImportExtension` summary table); fix the "Blocks parsed in by the `@import()` extension" bullet
  and the "Unlike `import`, a partial produces output" line in the `partial` section.
- **[patterns.md](patterns.md)** — drop the `(not @import())` parenthetical (the guidance already
  steers to `@<<`).
- **[csharp-api.md](csharp-api.md)** — remove `@import` from the `TrimDirectiveLines` directive-kind
  enumeration in the options table.
- **[TemplateOptions.cs](../src/Heddle/Data/TemplateOptions.cs)** — remove `<c>@import</c>` from the
  `TrimDirectiveLines` XML-doc directive list (XML-doc only; no public-surface change).
- **[coming-from-liquid.md](coming-from-liquid.md)** — **verified already clean** (its `include`/`render`
  mapping references `@<<`/`@partial`, no `@import`); no change.
- **[CHANGELOG.md](../CHANGELOG.md)** — under `[Unreleased]`, replace the "Added: Compile warning
  `HED4003` …" bullet with a `### Removed` entry: the legacy `@import()` include is removed; any call
  site now produces the positioned `HED4003` error naming `@<<{{ path }}` and `@partial(){{ name }}`; the
  `import` name is retained only as a tombstone; `@<<`/`@partial` unaffected.

*Completion:* G5 grep-clean holds; docs build (`cd docs && npm run docs:build`) succeeds.

### Phase 2

**WI-9 — document the string contract** (D10).
- **[AbstractExtension.cs](../src/Heddle/Core/AbstractExtension.cs)** — replace the thin `ProcessData`
  XML doc with the contract: return a `string` for a textual value; the value rail coerces a non-string
  to empty (`as string ?? string.Empty`), a deliberate guard (no internal `ToString()` leaks into
  concatenated output) that also drops otherwise-meaningful boxed scalars, so stringify at your own
  boundary; return `string.Empty` for "no textual value here". (XML-doc only — the `abstract` signature
  is unchanged, so the public-API snapshot is unaffected.)
- **[custom-extensions.md](custom-extensions.md)** — in the render-time `ProcessData`/`RenderData`
  description and the "Implement both …" callout, add the same contract in author-facing prose.

*Completion:* both surfaces state the contract; `docs build` succeeds; no behavior change (no test
touches runtime output).

---

## 6. Public API contract

**No net public-API additions.** The only public-surface deltas are subtractive/neutral:

| Type / member | Change | Snapshot effect |
| --- | --- | --- |
| `Heddle.Extensions.ImportExtension` | Kept as a public no-op tombstone; `CTOR`, `InitStart`, `ProcessData`, `RenderData` retained with identical signatures (behavior changes; contract does not) | **Unchanged** |
| `Heddle.Language.DocumentParser.Parse(string, ParseContext, CompileContext)` | Removed (dead after the tombstone; D7) — licensed by R1 | One line removed |
| `Heddle.Data.HeddleDiagnosticIds.LegacyImportDirective` (`"HED4003"`) | Same constant/value; XML doc now describes a removal **error** (D4/D5) | Unchanged (value + name identical) |
| `AbstractExtension.ProcessData(in Scope)` | XML doc gains the string contract (D10); signature unchanged | Unchanged |

**Thread-safety.** Unchanged. `ImportExtension` becomes stateless (a pure no-op); the parse-layer
detection runs inside the existing single-threaded `ParseTreeWalker` pass.

---

## 7. Diagnostics

| ID | Severity | Trigger | Message | Position |
| --- | --- | --- | --- | --- |
| `HED4003` (`LegacyImportDirective`) | **Error** (was Warning) | Any `@import` call site — an `extension_id` whose text is `import` — detected at the parse layer (`HeddleMainListener.ExitExtension_id`), for every call shape and both tiers | *`'@import' has been removed. Use '@<<{{ path }}' to share definitions and layouts across files, or '@partial(){{ name }}' to embed another template's rendered output inline. See docs/language-reference.md#imports--.`* | Whole-call span — `GetAbsoluteBlockPosition((CallContext)context.Parent)` (identical to the pre-repurpose warning position) |

This is a **repurpose**, not a new claim (D4): the registry row for `HED4003` is amended in place and
the amendment is ledgered; no new id is allocated. `@<<` never triggers it (it parses via `import_block`,
which has no `extension_id`). The error is never the generic `Cannot find extension <import>` and never
`HED0002` (G2). `HED4004` (`@<<` nesting) is untouched.

---

## 8. Testing plan

**TDD verdict.** *Test-first* for the new `HED4003` removal-error behavior (WI-6a): the positioned-error
assertions on every call shape and the both-tier differential are written **failing** against the
pre-change engine, then WI-1…WI-5 make them green. *Test-with* for the subtractive sweep (deletions,
doc edits, snapshot line, S1 re-expression) — verified by the regression gate, not new red tests.

**Suite homes.** [src/Heddle.Tests](../src/Heddle.Tests) (xUnit, all TFMs incl. `net48` on Windows);
[src/Heddle.Generator.Tests](../src/Heddle.Generator.Tests) and
[src/Heddle.Generator.IntegrationTests](../src/Heddle.Generator.IntegrationTests) for the precompiled
tier.

**Named cases.**

- **Positioned `HED4003` error on every call shape** (`LegacyImportRemovalTests`, dynamic tier), each
  asserting a `CompileResult.Errors` entry with `DiagnosticId == LegacyImportDirective`, the exact D5
  message, a whole-call span (`Position.Length > 0`, starting at the `@import` call), and that the
  message text substring `Cannot find extension` is **absent** (G2):
  - top-level: `@import(){{ergo-import-library.heddle}}`
  - chained / consuming: `@out():import(){{ergo-import-library.heddle}}`
  - nested in `@if` body: `@if(true){{ @import(){{ergo-import-library.heddle}} }}`
  - nested in `@for` body: `@for(3){{ @import(){{ergo-import-library.heddle}} }}`
  - nested in an output block: `@out(){{ @import(){{ergo-import-library.heddle}} }}` — the `@out` body is
    an output-producing subtemplate context (the genuine output-block shape G1 enumerates)
  - nested in a definition body: inside a `@%…%@` definition — `@%<page>{{ @import(){{ergo-import-library.heddle}} }}%@@page()`
  - two call sites → two positioned errors at distinct offsets.
- **Both-tier identity (differential)** — the generator emits the same id/severity/position:
  `HeddleGeneratorTests.LegacyImportEmitsHed4003AtTheCallSite` asserts `DiagnosticSeverity.Error`, the
  D5 message, and the call-line position; a **nested** case
  (`@if(true){{ @import(){{lib.heddle}} }}`) is added to pin the shape the old generator scan missed;
  `CorpusDifferentialTests` classifies any `@import` template as a diagnostic fixture on both tiers
  (`ergo-import-inline.heddle` being deleted, no `@import` fixture remains — the differential parity is
  carried by the inline generator cases).
- **Replacements unaffected (G3)** — `@<<` and `@partial()` goldens byte-identical; `@<<` raises no
  `HED4003` (`LegacyImportRemovalTests` `@<<` case; `HeddleGeneratorTests.ComposeImportEmitsNoHed4003InGenerator`
  kept); `HED4004` nesting unchanged (`ComposeImportNestingTests` `@<<` cases kept).
- **Branch-scan coverage preserved (S1)** — `BranchSetCompilerTests.C19` re-expressed with
  `@date(D){{yyyy}}`, asserting compile success and no `HED3*`.

**Regression gate** ([testing-standards](spec/common/testing-standards.md#regression-gates), one
combined run): `dotnet build -c Release` (all TFMs); `dotnet test src/Heddle.Tests` (all TFMs, zero
failures, goldens byte-identical for untouched templates); **grammar-stability** (`src/Heddle.Language/generated/`
no diff — G4); **public-API snapshot** diff = exactly the one removed `DocumentParser.Parse` line;
**G5 grep-clean** for `@import`; `cd docs && npm run docs:build` (docs changed). No hot path is touched
(§9), so no benchmark run is required. No samples-gallery item — the change removes a construct and
documents a contract; it is not a new user-facing feature.

---

## 9. Back-compat and migration

**R1 — removing `@import` is a breaking change vs 1.x, absorbed into the open 2.0 major window.** This
supersedes [improvement-plan.md non-goals](improvement-plan.md) ("removal is a 3.0 decision"). `@import`
shipped as a **functional include** in the released 1.x line
(`v1.0.1:src/Heddle/Extensions/ImportExtension.cs` reads the target file and re-parses it via
`DocumentParser.Parse`), so removing it **is** a breaking change against 1.x. It is reconciled with
[D2](spec/common/cross-cutting-decisions.md#d2--breaking-changes-land-only-in-ratified-breaking-windows)
/ [breaking-windows](spec/common/breaking-windows.md) as follows, with evidence:

- **2.0 is the open, not-yet-released major breaking window.** The only release tags are `v1.0.0` and
  `v1.0.1`; there is **no** `v2.0.0` tag, and the CHANGELOG's `2.0.0` contents still sit under
  `[Unreleased]`. [D2](spec/common/cross-cutting-decisions.md#d2--breaking-changes-land-only-in-ratified-breaking-windows)
  sanctions a breaking change **inside** a ratified major window (one migration per window); 2.0 is that
  window and it has not shipped, so the `@import` removal lands **inside** the still-open 2.0 migration —
  the sanctioned home for a break, not a prohibited between-windows change. (The
  [2.0 as-shipped record](spec/records.md#the-20-breaking-window--as-shipped-record) is written as if
  2.0.0 had released; that framing predates this spec and is **not** relied on here — per the maintainer
  ruling of 2026-07-12 this spec treats 2.0 as the open window.)
- **The `HED4003` *warning* never shipped** — the fact D4's id-reuse relies on: the id lives only under
  `[Unreleased]` (CHANGELOG) and appears in no release tag, so escalating it breaks no *shipped diagnostic*
  contract. This licenses the id-reuse; it is **not** a claim that `@import` itself never shipped.
- Bookkeeping: the `@import() removal` candidate is **withdrawn** from the
  [next-window register](spec/common/breaking-windows.md#next-window-candidate-register) because the removal
  is **pulled into the open 2.0 window** — it no longer awaits a future window. Its former precondition
  ("the `HED4003` deprecation warning shipped and soaked for at least one minor cycle") is moot, since that
  warning never shipped and the break is now absorbed into 2.0. The withdrawal is recorded in the amendments
  ledger (applied at spec-authoring time; WI-5 verifies it). This is the register's sanctioned withdrawal
  path (an owning spec may withdraw a candidate, recording it in the ledger).

**Migration for authors.** `@import(){{ path }}` → `@<<{{ path }}` to share definitions/layouts, or
`@partial(){{ name }}` to embed rendered output; the `HED4003` error states both at the call site. The
CHANGELOG `[Unreleased]` entry is the migration note (WI-8).

**Preserved behavior (proven).** `@<<`/`@partial` goldens byte-identical and `HED4004` unchanged
(§8, G3); the `HED4003` **position** is byte-identical to the prior warning (D2 of this spec, §2.3); the
public-API snapshot changes by exactly one removed line, with `ImportExtension` intact (§6).

---

## 10. Performance considerations

No hot path is touched. The parse-layer detection adds one `context.GetText() == "import"` string
comparison per parsed `extension_id` during compilation (a cold, one-time path), and the tombstone
`InitStart` does strictly **less** work than before (no file I/O, no re-parse, no provenance stamping).
Rendering is untouched. No benchmark guard is required; the benchmark suite is not run for this change
([testing-standards](spec/common/testing-standards.md#regression-gates), benchmarks only when a hot path
is touched).

---

## 11. Standards compliance

- **YAGNI / subtractive.** The change is net-negative in surface: a removed construct, a deleted
  generator scan, a deleted dead overload, deleted `@import`-only tests. No new abstraction is
  introduced — the detection reuses the existing `ExitExtension_id` hook and the shared `ParseContext.Errors`
  channel, and the tombstone reuses the existing extension-registration path.
- **DRY.** One detection site (the parse layer) replaces two partial ones (the `InitStart` warning and
  the generator scan), consolidating the "don't use `@import`" knowledge to a single seam that both
  tiers already read.
- **Diagnostics discipline** ([D1](spec/common/cross-cutting-decisions.md#d1--stable-diagnostic-ids-hedxxxx)).
  The id is reused legitimately (unshipped), positioned, documented next to its neighbors
  (`HeddleDiagnosticIds`), and the registry/ledger are updated in the same change.
- **Balanced mode.** No SOLID abstraction is warranted (no second consumer of a new seam); the tombstone
  is the minimal mechanism that satisfies G1/G2 without a compiler special-case.

---

## 12. Deferred items / non-goals

- **`HED0002` (`ExtensionNotFound`) attachment at `TemplateFactory.Create`'s fallback** — declared but
  never attached (§2.5). Pre-existing, unrelated to `@import`; out of scope. *Trigger to revisit:* a
  diagnostics-coverage pass over `TemplateFactory`.
- **The value-rail non-string drop itself** (including `@out`'s raw value-rail return) — Phase 2
  documents it truthfully but does **not** fix it (D10). *Trigger:* a maintainer-ratified decision to
  make the rail stringify (a byte-changing behavior change needing its own window analysis).
- **Reserving the name `import` for user definitions.** With the tombstone, any `@import` call site —
  including a hypothetical by-name call to a user definition literally named `import` — receives the
  `HED4003` removal error (the name is deliberately reserved — a direct consequence of the D3 tombstone
  detecting on the `import` name text, and of the indefinite-lifespan decision D8). No in-repo test
  defines such a name; documented as an intended reservation, not a regression.
- **Removing the tombstone / decaying to unknown-extension** — explicitly **not** scheduled within 2.x
  (D8). *Trigger:* a future major that chooses to free the `import` name.
- **Grammar changes** — none; `@import` was never grammar (G4).

---

## 13. External references

- Repository plan: [import-removal-plan.md](import-removal-plan.md).
- Spec conventions, cross-cutting decisions, testing/breaking-windows standards:
  [spec/common/](spec/common/spec-conventions.md),
  [cross-cutting-decisions.md](spec/common/cross-cutting-decisions.md),
  [testing-standards.md](spec/common/testing-standards.md),
  [breaking-windows.md](spec/common/breaking-windows.md); historical records
  [records.md](spec/records.md).
- Amended decision (not edited): [improvement-spec B3](improvement-spec.md#b3--import-deprecation-warning-hed4003).
- Source seams (path + symbol): [HeddleMainListener](../src/Heddle/Language/HeddleMainListener.cs)
  (`ExitExtension_id`, `ExitImport_block`, `EnterSubtemplate`/`ExitSubtemplate`),
  [ParseContext](../src/Heddle/Language/ParseContext.cs) (`Errors` ctor sharing, `GetAbsoluteBlockPosition`,
  `CreateItem`), [ImportExtension](../src/Heddle/Extensions/ImportExtension.cs),
  [HeddleTemplateGenerator](../src/Heddle.Generator/HeddleTemplateGenerator.cs) (error-forward loop +
  `legacyImport` scan), [DocumentParser.Runtime](../src/Heddle/Language/DocumentParser.Runtime.cs),
  [TemplateFactory](../src/Heddle/Runtime/TemplateFactory.cs) (`Create`),
  [HeddleTemplate](../src/Heddle/HeddleTemplate.cs) (`Compile`),
  [HeddleDiagnosticIds](../src/Heddle/Data/HeddleDiagnosticIds.cs) (`LegacyImportDirective`),
  [CompileError](../src/Heddle/Data/CompileError.cs) (`ToError`),
  [AbstractExtension](../src/Heddle/Core/AbstractExtension.cs) (`ProcessData`),
  [RuntimeDocument](../src/Heddle/Runtime/RuntimeDocument.cs), [OutExtension](../src/Heddle/Extensions/OutExtension.cs),
  [HeddleParser.g4](../src/Heddle.Language/HeddleParser.g4) (`extension_id`, `call`, `import_block`).
- Microsoft Learn — [Breaking-change rules](https://learn.microsoft.com/en-us/dotnet/standard/library-guidance/breaking-changes).
