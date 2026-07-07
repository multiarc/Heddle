# Phase 4 specification — ergonomics quick wins

Status: **Specified — ready for implementation.**
Roadmap source: [phase 4 — ergonomics quick wins](../../roadmap/phase-4-ergonomics.md).
Prior phases assumed merged
([D5, sequential order](../common/cross-cutting-decisions.md#d5--sequential-implementation-order)):
[phase 1 — native expressions](../phase-1-native-expressions/README.md) (`@for(5)` literals
fold to `ConstantParameter`, the `FunctionRegistry`/`BuiltInFunctions` seam that `range`
joins, and the `HEDxxxx` plumbing — `DiagnosticId`, `HeddleDiagnosticIds`,
`ToError(position, id)`), [phase 2 — safe output](../phase-2-safe-output/README.md)
(`OutputProfile` on `TemplateOptions` identity, the copy-constructor completeness test this
phase's new option inherits, the body-form `@profile(){{html}}` directive that joins the
trim-eligible set, and the `TemplateResolver` profile-suffixed cache key this phase
extends), and [phase 3 — branching](../phase-3-branching/README.md) (the
`ProcessBranchSets` strip/orphan scan whose position in the compile pipeline this phase's
trimming must compose with). This phase claims IDs from the `HED4xxx` block per
[cross-cutting D1](../common/cross-cutting-decisions.md#d1--stable-diagnostic-ids-hedxxxx)
and contributes the third ratified breaking change — `TrimDirectiveLines` default-on — to
the [single 2.0 window](../common/cross-cutting-decisions.md#d2--the-single-20-breaking-window).

This spec is a single document — no supplementary files.

## Scope and goal

Four independent quality-of-life items, zero grammar change
(`src/Heddle.Language/generated/` must have no diff):

1. **`@for` sugar** — `@for(5)` and `@for(Count)` iterate 0…n−1 without embedded C#;
   `range(start, last[, step])` joins the default function registry; zero/negative step is
   an error, negative count renders empty.
2. **Double-render warning** — a definition with a default output (`-> chain`) that is
   *also* called by name renders twice; the compiler now says so with a positioned warning.
3. **`TrimDirectiveLines`** — an opt-in `TemplateOptions` switch (default-on at 2.0) that
   makes whole-line directives swallow their line, removing the `@\` preamble noise.
4. **Two documented imports** — `@<<{{path}}` and `@import(){{path}}` are pinned by
   characterization fixtures and documented as distinct (maintainer-ratified: no
   delegation, no behavior change).

## Assumed state

Every seam below was **re-verified against the current source** (July 2026); searches
excluded `bin/`, `obj/`, `node_modules/`, `generated/`. Corrections to roadmap claims are
marked ⚠ and closed in the decision records. Where a seam is created by a prior phase's
spec (not yet in source), the binding is to that spec's pinned contract.

| Seam | Verified state |
| --- | --- |
| [ForIndexExtension](../../../src/Heddle/Extensions/ForIndexExtension.cs) | `[ExtensionName("for")]` + `[DataType(typeof(ForModel))]`. `InitStart` compiles the body against `parent` with chained type `int` (the index rides the chained channel — `@out()` renders it). Both `ProcessData` and `RenderData` guard `scope.ModelData is ForModel` and loop `for (i = Start ?? 0; i < Last; i += Step ?? 1)` — `Last`-exclusive, exactly as the roadmap states. ⚠ Refinement: the "renders empty for any other input" claim holds only on **dynamic** scopes; on typed scopes a non-`ForModel` parameter is already a positioned compile error via `CheckTypes` (below) — see D1. |
| [ForModel](../../../src/Heddle/Models/ForModel.cs) | `int? Start`, `int Last`, `int? Step` — confirmed. |
| [DataTypeAttribute](../../../src/Heddle/Attributes/DataTypeAttribute.cs) | `[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]` — stacking a second `[DataType]` works, as the roadmap verified. |
| [HeddleCompiler.CreateExtension / CheckTypes](../../../src/Heddle/Runtime/HeddleCompiler.cs) | `CreateExtension` (line 436) collects `[DataType]` attributes with `GetAttributes<DataTypeAttribute>(true)` and calls `CheckTypes` (line 577), which errors `Return Type is {0} but any of [{1}] expected.` when the value's type is assignable to none of them (`IsAssignableFrom` via [TypeExtension.IsType](../../../src/Heddle/Helpers/TypeExtension.cs); nullables unwrapped first, dynamic always passes). This is the seam that makes `@for(Name)` a typed error and `@for(Count)` pass once `int` is declared. |
| [HeddleCompiler.Compile](../../../src/Heddle/Runtime/HeddleCompiler.cs) | Line 22. Preprocessing order: `ShiftBySkippedTokens` (line 28) → `RemoveDefinitions` (29) → `ReplaceRawOutput` (30) → *(phase 3 inserts `ProcessBranchSets` here)* → the `OutputChains` compile loop (32), which calls `RemoveEmptyItem` (55–58, method at 97) for every chain whose threaded return type ends `null` → the `DefaultChains` loop (65), whose elements get `BlockPosition(workingDocument.Length, 0)` — **default outputs render at document end**, resolving definitions against the final layered state. |
| [HeddleCompiler.RemoveEmptyItem / RemoveDefinitions](../../../src/Heddle/Runtime/HeddleCompiler.cs) | Both remove a span via `ExStringBuilder.ApplyRemove` ([ExStringBuilder.cs](../../../src/Heddle/Strings/ExStringBuilder.cs) line 341) and shift later `OutputChains` (and, for definitions, `RawOutputItems`) back by the removed length in a reverse loop. These are the exact two seams trimming widens (D8). |
| [HeddleCompiler.CompileItem](../../../src/Heddle/Runtime/HeddleCompiler.cs) | Line 254. Resolves `definitionItem = parseContext.GetDefenition(name)` against the **chain's own context** — an isolated snapshot taken when the chain was walked (`ParseContext.CreateOutputChain` line 288 calls `IsolateContextWithTree()`), so each call site sees the definition layering as of its document position. Results are memoized in `CompileContext.CompiledItems` — one compile (and thus one warning) per `OutputItem`. |
| [ParseContext.CreateDefinition](../../../src/Heddle/Language/ParseContext.cs) | Line 146. Builds the `DefinitionItem` and, when the declaration carries `-> chain`, an `OutputChain` via `CreateOutputChain(defOutChain?.chain(), definitionName)` (line 301) whose **first unnamed call is renamed to the definition name** (`CreateItem`'s `callNameOverride`, line 483) — the default chain is a synthetic self-call. [HeddleMainListener.EnterDef](../../../src/Heddle/Language/HeddleMainListener.cs) (line 46) adds that chain to `ParseContext.DefaultChains`. A full override (`<a:a>`) with a default chain is already an error (`Default output call chain can't be used in fully overriden definitions`, line 208). |
| [DefinitionItem](../../../src/Heddle/Language/DefinitionItem.cs) | Copy ctor (isolation snapshots) copies name/template/type/position/context; `OverrideWith` (full overrides) mutates the registry entry in place, demoting the old state to `BaseDefinition`. Neither records whether the declaration carried `->` — D4 adds that. |
| [HeddleMainListener.ExitImport_block](../../../src/Heddle/Language/HeddleMainListener.cs) | Line 203 — the **`@<<` implementation**, entirely at walk time: reads the file, parses it into an isolated context, merges definitions and default chains back, re-bases the imported output chains as **zero-length blocks at the import position** (line 223), and records the `@<<{{…}}` block's own span in `DefinitionsBlock.Positions` (line 227) — so the import block text is excised by `RemoveDefinitions`, which is what makes it trim-eligible (D7). Imported *static text* never transfers — only chains and definitions. |
| [ImportExtension](../../../src/Heddle/Extensions/ImportExtension.cs) | The **`@import()` implementation**, at compile time: `InitStart` first compiles its body (the path) via `base.InitStart`, then parses the file into `initContext.ParseContext` and returns `null` (block removed via `RemoveEmptyItem`). ⚠ That context is the **body's own isolated snapshot** (`OutputItem.Context`, attached in `ExitSubtemplate` line 196, passed as `extensionItem.Context ?? parseContext` in `CompileItem`), whose own `HeddleCompiler.Compile` has **already run** inside `base.InitStart`, and whose `DefinitionsBlock` is a snapshot copy ([DefinitionBlock](../../../src/Heddle/Language/DefinitionBlock.cs) copies entries, no parent chaining). Consequence, verified end-to-end in D11: nothing `@import()` parses ever reaches the importing document — only the shared error/warning lists (`ParseContext` ctor lines 36–37) surface imported-file problems. Nothing in the repo exercises `@import(` (verified — the only textual match is an unrelated Ace demo asset). |
| [HeddleLexer.g4](../../../src/Heddle.Language/HeddleLexer.g4) | `fragment EAT_WS: WS_START WS*` where `WS_START: '@\\'` — the `@\` token consumes itself **plus all following whitespace including newlines**, routed to the hidden channel (`SKIP_WS` and per-mode variants). Hidden tokens never reach `tree.GetText()`, so the working document is already `@\`- and comment-free; their original spans are recorded as `SkippedTokens` ([DocumentParser](../../../src/Heddle/Language/DocumentParser.cs) lines 84–87) and `ShiftBySkippedTokens` re-aligns positions. A comment token (`COMMENT_BLOCK`) consumes through `*@` but **not** its line's newline — a comment-only line leaves a bare newline in the working document (D6's remnant-line rule exists because of this). |
| [TemplateOptions](../../../src/Heddle/Data/TemplateOptions.cs) | Current source: two ctors + copy ctor; `Equals` compares `FileNamePostfix`/`TemplateName`/`RootPath`; `GetHashCode` hashes `TemplateName`. Phases 1–2 (merged per D5) add `ExpressionMode`/`Functions` (copied, not in identity), `OutputProfile` (copied **and** in identity), the `ProvideLanguageFeatures` copy fix, and the reflection completeness test `TemplateOptionsCompletenessTests` (phase 2 D7) — `TrimDirectiveLines` lands on top of that state (D9). |
| [TemplateResolver](../../../src/Heddle/Runtime/TemplateResolver.cs) | Phase 2 D8 (merged) gives it the `(rootPath, checkFileChange, defaultProfile)` ctor, the private `CacheKey(fullPath, profile)` helper used at every probe/write, and per-operation effective-profile threading. D10 extends exactly that surface. |
| [RuntimeDocument](../../../src/Heddle/Runtime/RuntimeDocument.cs) | `GetDocumentPieces` (line 91) slices static text around chain positions in the final working document — a span removed (or widened-removed) before construction can never render; an all-directives document yields `Empty == true` and renders nothing. No change needed here. |
| Warnings plumbing | Compile-stage warnings go to [`CompileContext.CompileWarnings`](../../../src/Heddle/Runtime/CompileContext.cs) (public, shared by reference into child contexts at line 80). `HeddleCompileResult.Success` is computed from **errors only** ([HeddleTemplate.Compile](../../../src/Heddle/HeddleTemplate.cs) lines 242–271) — new warnings cannot break `Assert.True(result.Success)` tests. [`HeddleCompileWarning`](../../../src/Heddle/Data/HeddleCompileWarning.cs) carries `Fix`. |
| Existing corpus | ⚠ The roadmap's claim that the double-render warning "must fire zero times across the existing fixture corpus" is **false**: [vc-test.heddle](../../../src/Heddle.Tests/TestTemplate/vc-test.heddle) declares `<default> -> ()` (line 149) and calls `@default()` by name twice (lines 189, 206) — deliberately, to exercise override layering between renders; its golden contains the triple render. No other fixture in `src/Heddle.Tests/TestTemplate` or `src/Heddle.Performance/TestTemplates` combines `->` with a by-name call (verified by sweep of every `->` definition name against every call). Closed in D5. |
| Runtime error type | [`TemplateProcessingException`](../../../src/Heddle/Exceptions/TemplateProcessingException.cs) — the established render-time error (phase 3 uses it for the runtime orphan `@else`); `range`'s runtime step error reuses it (D3). |

## Design decisions

Numbering is local to this spec; cross-cutting decisions are cited by link. Every roadmap
lean, ratified item, and validation-scenario commitment for this phase is closed below.

### D1 — `@for` accepts `int`: second `[DataType]`, allocation-free loop, empty on non-positive count

**Decision.** `ForIndexExtension` gains `[DataType(typeof(int))]` alongside the existing
`[DataType(typeof(ForModel))]` (stacking verified legal). `ProcessData` and `RenderData`
normalize into three locals **without allocating a `ForModel`**. The rewritten methods in
full — the loop bodies are byte-for-byte today's (`ExStringBuilder` + `GetInnerResult` on
the process path, `RenderInnerResult` on the render path, verified against current
source), which is what WI3's "`ExStringBuilder` shape preserved" check pins:

```csharp
public override object ProcessData(in Scope scope)
{
    int start, last, step;
    if (scope.ModelData is ForModel model)
    {
        start = model.Start ?? 0;
        last = model.Last;
        step = model.Step ?? 1;
    }
    else if (scope.ModelData is int count)
    {
        start = 0;
        last = count;
        step = 1;
    }
    else
    {
        return string.Empty;
    }

    var builder = new ExStringBuilder();
    for (int i = start; i < last; i += step)
    {
        var parentData = scope.Parent(i);
        builder.Append(GetInnerResult(parentData));
    }

    return builder.ToString();
}

public override void RenderData(in Scope scope)
{
    int start, last, step;
    if (scope.ModelData is ForModel model)
    {
        start = model.Start ?? 0;
        last = model.Last;
        step = model.Step ?? 1;
    }
    else if (scope.ModelData is int count)
    {
        start = 0;
        last = count;
        step = 1;
    }
    else
    {
        return;
    }

    for (int i = start; i < last; i += step)
    {
        var parentData = scope.Parent(i);
        RenderInnerResult(parentData);
    }
}
```

(The current source tests `!(scope.ModelData is ForModel)` and returns early; the rewrite
inverts that into the `is`-chain above. `InitStart` is untouched — it already compiles the
body against `parent` with chained type `int` regardless of the model's shape.)

The `ForModel` test stays first, so existing templates execute the identical type-test
sequence. `int n` behaves exactly as `ForModel { Last = n }`: 0…n−1, `Last`-exclusive.
`@for(0)` and `@for(-3)` render empty (loop condition false immediately) — the ratified
negative-count decision. Boxed `int?` values arrive as boxed `int` or `null` (`null` → the
empty fall-through); `CheckTypes` unwraps `int?` so `@for(NullableCount)` compiles. Other
numeric types (`long`, `short`, `double`) stay compile errors — `int` is the one declared
bridge type, matching `ForModel`'s own fields.
**Correction to the roadmap's back-compat row:** "previously rendered empty (silent
no-op)" held only for dynamic scopes; on typed scopes `@for(intValue)` was already a
positioned compile error from `CheckTypes` (`Return Type is System.Int32 but any of
[Heddle.Models.ForModel] expected.`). Both directions are strictly error/empty → works.
Because this phase's negative tests assert that `CheckTypes` message by ID, the message is
"touched" per [cross-cutting D1](../common/cross-cutting-decisions.md#d1--stable-diagnostic-ids-hedxxxx)
and claims **HED0004** (text unchanged).
**Rationale.** The inline normalization keeps the render path allocation-free
([performance rules](../common/coding-standards.md#performance-rules-the-render-path-is-hot));
a `new ForModel { Last = n }` per render would be a per-iteration heap allocation in
nested loops.
**Alternatives rejected.** Wrapping `int` into a `ForModel` at compile time (requires a
parameter-rewriting seam for one extension; the runtime test is two instructions); a
`[DataType(typeof(long))]` third bridge (no scenario — `Count` properties are `int`;
widening silently truncates or surprises).

### D2 — `range(start, last[, step])` joins the default function registry

**Decision.** `BuiltInFunctions` (phase 1, `src/Heddle/Runtime/Expressions/BuiltInFunctions.cs`)
gains two overloads registered in `FunctionRegistry.Default` under the name `range`:

| Signature | Behavior |
| --- | --- |
| `range(int start, int last) → ForModel` | `new ForModel { Start = start, Last = last }` — step 1, `last`-exclusive |
| `range(int start, int last, int step) → ForModel` | as above with `Step = step`; `step <= 0` throws at render when not caught statically (D3) |

`@for(range(2, 10, 2))` therefore threads `ExType == ForModel` into the existing
`[DataType(typeof(ForModel))]` path with zero further changes; `start >= last` renders
empty, matching `@for(0)`.

**Overflow semantics, pinned.** `range` itself performs no arithmetic — both overloads
only copy their arguments into `ForModel` fields, so every `int` argument value is
accepted and nothing can overflow at call time. Iteration arithmetic belongs to
`ForIndexExtension`'s pre-existing loop, which compiles **unchecked** (verified:
`src/Heddle/Heddle.csproj` sets no `CheckForOverflowUnderflow`, so the C# default
applies): when `i + step` exceeds `int.MaxValue`, `i` wraps negative and iteration
continues until some visited value satisfies `i >= last`. Two consequences, stated here so
the docs and tests do not rediscover them:

- With `step == 1` the index visits every value from `start` up and lands on `last`
  exactly, so `@for(n)` (always step 1) and two-argument `range(a, b)` terminate for
  **all** argument values, including `last == int.MaxValue` (2 147 483 647).
- With `step > 1`, termination is guaranteed only when the exit window
  `[last, int.MaxValue]` is at least `step` values wide (`last <= int.MaxValue − step + 1`);
  above that, start/step alignment decides, and a miss loops forever —
  `range(0, 2147483647, 2)` visits only even numbers, `2 147 483 647` is odd and is the
  sole `int >= last`, so the loop **never terminates**.

This is byte-identical to what the C# tier already produces today for
`@for(@new ForModel() { Last = 2147483647, Step = 2 })` — `range` introduces no new
failure mode, and neither D3 layer fires (the step is positive). Guarding it inside the
extension is the same pre-existing-behavior change already deferred for `Step <= 0`; the
[deferred item](#deferred-items) covers both shapes under one trigger.

Registering `range` **amends phase 1 D13's frozen seventeen-name set to
eighteen**: "frozen" there governs instance immutability and the 1.x contract of the
*shipped* set, not eternal membership — additive growth in a later phase is the registry's
sanctioned evolution path, and the phase 1 docs table is updated in the same change (WI8).
A host that already registers its own `range` keeps working: an exact-signature
registration replaces the built-in, a different signature adds an overload (phase 1 D12).
No one-argument `range(last)` overload ships — `@for(5)` already covers it.
A standalone `@range(1, 5)` call compiles via phase 1's function-carrier path and renders
`ForModel.ToString()` — legal, useless, documented in one line on the phase 1 docs page.
**Rationale.** Roadmap-ratified with ecosystem precedent: Jinja2 ships `range([start, ]stop[, step])`
as a default global and Liquid loops over literal ranges; `ForModel` stays the sole bridge
type per the roadmap's .NET 10 note (`System.Range` has no step and carries from-end
semantics `ForModel` does not model).
**Alternatives rejected.** A `range` *extension* (the function registry is the ratified
seam for value-producing names, and expression contexts resolve registry-only —
phase 1 D11); a one-arg overload (redundant surface); returning a lazily-iterated
sequence type (a second iteration protocol beside `ForModel` for zero gain).
**Grounding.** [Jinja2 template designer docs — `range`](https://jinja.palletsprojects.com/en/stable/templates/),
[Liquid iteration tags](https://shopify.github.io/liquid/tags/iteration/) (carried from the
roadmap, re-checked).

### D3 — Step validation: compile error when the literal is visible, `TemplateProcessingException` otherwise

**Decision.** Two layers, both ratified July 2026 (`step == 0` error, `step < 0` error):

- **Compile time.** In `NativeExpressionCompiler`'s `CallNode` path, immediately after
  overload binding selects one of the two built-in `Range` `MethodInfo`s (reference
  comparison — host-replaced `range` registrations are exempt): if the three-argument
  form's step argument is a `LiteralNode` with an `int` value, or a
  `UnaryNode(Negate | UnaryPlus)` over one, evaluate that literal; a value `<= 0` emits
  error **HED4001** positioned at the step argument node and fails the compile. This
  mirrors phase 1's HED1015 mechanics exactly (static validation of literal arguments to a
  specific built-in). More complex constant shapes (`2 - 2`) fall through to the runtime
  check — partial subtree folding stays deferred per phase 1 D17.
- **Render time.** `BuiltInFunctions.Range(int, int, int)` throws
  `TemplateProcessingException` with the HED4001 message text when `step <= 0`. The exact
  guard, pinned (message-only constructor — the type's three constructors are `()`,
  `(string)`, `(string, Exception)`, verified; render-time exceptions carry no position
  and no `HED` prefix, the phase 3 precedent):

  ```csharp
  if (step <= 0)
      throw new TemplateProcessingException(string.Format(CultureInfo.InvariantCulture,
          "Function 'range' requires a positive step, but {0} was supplied — a zero or negative step never terminates the loop.",
          step));
  ```

  Verbatim thrown messages: step `0` → `Function 'range' requires a positive step, but 0
  was supplied — a zero or negative step never terminates the loop.`; step `-1` → the same
  text with `-1` (invariant-culture integer formatting, so no locale can change the
  digits or the sign character). R03 asserts this text exactly. This is a
  **recorded, sanctioned exception** to phase 1 D13's built-ins-never-throw rule: every
  other misuse of a built-in has a harmless value-shaped answer (`""`, clamped substrings),
  but a non-positive step has none — `ForIndexExtension`'s `Last`-exclusive loop would
  either never terminate (hang, unbounded output) or silently render nothing, both bugs,
  never intent. Throwing is the only correct behavior.

Direct `ForModel` construction with `Step <= 0` through the C# tier
(`@for(@new ForModel { Last = 3, Step = 0 })`) keeps its pre-existing behavior (hang or
empty) — guarding the extension itself would change the behavior of existing templates
outside any ratified window; it is a [deferred item](#deferred-items) with a trigger.
**Rationale.** The roadmap decision verbatim, with the enforcement points pinned to the
verified seams; scoping the static check to the built-in `MethodInfo` keeps host overrides
of `range` fully in charge of their own validation (the registry is the trust boundary).
**Alternatives rejected.** Validating inside `ForIndexExtension` at render (changes
pre-existing `ForModel` behavior; also fires after the model was built, losing the
argument position); clamping step to 1 (silent wrong output); making the runtime error a
compile-collected diagnostic (render-time errors are exceptions by the
[error-handling standards](../common/coding-standards.md#error-handling-and-diagnostics) —
compile diagnostics cannot exist for values known only at render).

### D4 — Double-render warning: declaration flag + self-call marker, warned at the call site

**Decision.** Three small pieces:

1. `DefinitionItem` gains `public bool HasDefaultOutput { get; internal set; }` — set by
   `ParseContext.CreateDefinition` to `chain != null` (a default chain was actually
   produced) in both branches, copied by the isolation copy constructor, and **left
   untouched by `OverrideWith`**: a full override (`<a:a>`) cannot carry `->` (existing
   error), but the base declaration's default chain remains in `DefaultChains` and renders
   the *final layered* definition at document end — so the registry entry keeps its `true`
   and calls after the override still warn, which is correct because the double render
   still happens.
2. `OutputItem` gains `internal bool IsDefaultChainSelfCall`, set by
   `ParseContext.CreateItem` when `callNameOverride` is applied (the unnamed first call of
   a `-> chain`, the only call site that passes it — verified) and copied by the isolation
   constructor. This exempts the default chain's own synthetic self-call from the warning
   without threading pass-state through `CompileItem` — and deliberately does **not**
   exempt other definition calls nested inside a default chain, which are real additional
   renders.
3. `HeddleCompiler.CompileItem`, immediately after definition resolution: when
   `definitionItem != null && definitionItem.HasDefaultOutput && !extensionItem.IsDefaultChainSelfCall`,
   add warning **HED4002** to `compileScope.CompileContext.CompileWarnings`, positioned at
   `extensionItem.Position` (the call), with the definition's declared position embedded
   in the message (both positions referenced, per roadmap success criterion 2). The
   `CompiledItems` memoization guarantees exactly one warning per call site; N call sites
   produce N warnings (each is a real extra render).

Override layering falls out of the verified snapshot mechanics: each call site resolves
against its chain's isolated context, so a call **before** a full override sees the base
instance (`HasDefaultOutput == true` → warns, and renders the base — a true double render
together with the default chain's final-layer render), and a call **after** sees the
mutated entry (still `true` → warns — also a true double render). Calling a *derived*
definition (`<b:a>` where only `a` has `->`) does not warn: `b`'s own flag is false and
`a`'s default chain renders `a`, not `b` — no double render of `b` exists.
**Rationale.** Roadmap design ("warn based on the definition instance resolved at that
call site"), made concrete against the verified `DefaultChains` machinery; read-only
analysis — a warning cannot change output.
**Alternatives rejected.** Scanning `parseContext.DefaultChains` by name at each call
(default chains live on the declaring context; imported and body-scoped layouts make the
lookup context-dependent — the per-instance flag travels with exactly the object the call
resolved); threading an `inDefaultChain` flag through `CompileItem`/`CompileParameterChain`
(wrongly suppresses warnings for *other* definitions called inside a default chain);
warning severity error (the render is well-defined and vc-test proves intentional uses
exist).

### D5 — Corpus correction: the warning has two known true positives, and the gate says so

**Decision.** The regression requirement is restated with evidence: the full-corpus scan
must produce **exactly two HED4002 warnings, both in `vc-test.heddle`, at the `@default()`
calls (lines 189 and 206), and zero anywhere else** in `src/Heddle.Tests` and
`src/Heddle.Performance` templates. ⚠ This corrects the roadmap's regression requirement
("must fire zero times across the entire existing fixture corpus — any hit is a false
positive by definition"): the sweep in [Assumed state](#assumed-state) shows vc-test
*deliberately* combines `<default> -> ()` with two by-name calls to pin override layering
across three renders — the warning firing there is a **true positive** describing exactly
what the golden shows. `VcGenerateTest` keeps passing unchanged because
`HeddleCompileResult.Success` ignores warnings (verified); the test gains one added
assertion pinning the two warning positions so the scan expectation is executable, and the
fixture is otherwise untouched (it is now also the standing proof that the warning does
not alter output).
**Rationale.** Fix-forward honesty: silencing the fixture (editing it) would destroy its
layering coverage; whitelisting "any hits" would gut the scan. Naming the two expected
hits keeps the scan exact.
**Alternatives rejected.** Editing vc-test to avoid the warning (loses the triple-render
layering pin; also golden churn); suppression machinery for expected warnings (new
mechanism for one fixture — YAGNI).

### D6 — Trimming semantics: the whole-line rule, pinned to the character

**Decision.** With `TemplateOptions.TrimDirectiveLines == true`, a removed span is widened
to its whole line iff the block occupies the line by itself, evaluated against the
**current working document at the moment of removal**. Exact predicate, implemented once
as `HeddleCompiler.WidenToWholeLine(BlockPosition block, string document)`:

- **Left:** scan left from `block.StartIndex` over U+0020 (space) and U+0009 (tab) only.
  Whole-line iff the scan reaches index 0 or a character that is `\n` or `\r`. The scanned
  leading whitespace joins the removed span (the line's indentation is swallowed).
- **Right:** scan right from the block end over space/tab only. Whole-line iff the scan
  reaches end-of-document, or a line terminator — consumed as exactly one of `\r\n`
  (pair), `\n`, or `\r`. Trailing spaces/tabs and that one terminator join the removed
  span.
- Any other character on either side (including U+00A0, form feed, vertical tab — they
  are content, not indentation) → the predicate fails and the block is removed exactly as
  today, span unchanged. Failure is always safe: trimming never removes non-whitespace.
- **Both sides must pass** before either side widens — a passing left with a failing
  right (or vice versa) leaves the span exactly as it was. The left-hand terminator is
  **never consumed**: it ends the *previous* line, which keeps trimming strictly
  one-line-per-block (only the right-hand terminator — at most one — is swallowed).

The normative implementation (allocation-free; a returned span equal to the input means
"not whole-line, remove as today"):

```csharp
private static BlockPosition WidenToWholeLine(BlockPosition block, string document)
{
    int left = block.StartIndex;                       // will become the widened start
    while (left > 0 && (document[left - 1] == ' ' || document[left - 1] == '\t'))
        left--;
    if (left != 0 && document[left - 1] != '\n' && document[left - 1] != '\r')
        return block;                                  // content on the left — unchanged

    int right = block.StartIndex + block.Length;       // first index after the block
    while (right < document.Length && (document[right] == ' ' || document[right] == '\t'))
        right++;
    if (right == document.Length)
        return new BlockPosition(left, right - left);  // EOF is a valid terminator
    if (document[right] == '\r')
    {
        right += right + 1 < document.Length && document[right + 1] == '\n' ? 2 : 1;
        return new BlockPosition(left, right - left);  // CRLF pair or bare CR
    }
    if (document[right] == '\n')
        return new BlockPosition(left, right + 1 - left);
    return block;                                      // content on the right — unchanged
}
```

`block.Length == 0` (the remnant-line probe below) needs no special case — the left and
right scans meet across the empty span and the predicate degenerates to "is this line
whitespace-only".

Consequences, each pinned by a torture row (testing plan): a multi-line block (`@% … %@`
spanning lines) trims when its *first* line-prefix and *last* line-suffix are whitespace —
the predicate is per-block, not per-line; two eligible blocks sharing one line compose —
when the first is removed the second becomes whole-line in the updated document and
swallows the terminator, so a line consisting only of removed blocks vanishes entirely;
`@\` idempotence is structural — `@\` and everything it ate are absent from the working
document, so a directive followed by `@\` is simply not whole-line and nothing extra is
removed (no double-trim possible); blank lines the author wrote stay (no block, no trim).

**Comment remnant lines.** A whole-line comment leaves a bare terminator in the working
document (lexer-verified). To honor the roadmap scenario "preamble (`@using` + `@model` +
comment) renders with no blank lines", a new step `TrimHiddenRemnantLines` (D8, step 2)
maps each `SkippedTokens` entry to its clean-document position (original start minus the
summed lengths of prior hidden tokens — the list is in document order) and applies
`WidenToWholeLine` to the **zero-length** span at that position: it widens only when the
remnant line is whitespace-only, which removes comment-only lines and is a no-op for every
`@\` remnant (their lines retain content by construction). Processed in reverse document
order, skipping positions inside the previously removed span (multiple comments on one
line remove it once). This is a recorded refinement of the roadmap's seam list — the
roadmap's mechanism paragraph named only the block removals, but its validation scenario
requires comment lines to trim, and Jinja2 applies `trim_blocks`/`lstrip_blocks` to
comment tags too.
**Rationale.** The rule is Jinja2's `trim_blocks` ("the first newline after a block is
removed") plus `lstrip_blocks` ("leading spaces and tabs are stripped from the start of a
line to a block"), both opt-in and off by default there exactly as here, unified under
Razor's design sentence for the same problem — "removing whitespace for any non-HTML
content that occupies the entire line" — and extended by allowing trailing spaces/tabs
before the terminator (an invisible-character difference that would otherwise defeat the
feature; the torture corpus pins it). Space/tab-only horizontal scanning matches Jinja's
documented set verbatim.
**Alternatives rejected.** Per-tag trim markers (Liquid `{%- -%}`, Handlebars `~`, Go
`{{- -}}`) — Heddle already has `@\`, which stays; the option removes the need for it in
the common case rather than adding a second marker; trimming Unicode whitespace classes
(silently eats meaningful NBSP content; no peer engine does); a post-compile regex over
the final document (cannot know which line held a directive versus authored text).
**Grounding.** [Jinja2 API — `trim_blocks`/`lstrip_blocks`, defaults `False`](https://jinja.palletsprojects.com/en/stable/api/);
[Liquid — whitespace control (per-tag hyphens, opt-in)](https://shopify.github.io/liquid/basics/whitespace/);
[aspnet/Razor#485 — "smart about removing whitespace for any non-HTML content that occupies the entire line"](https://github.com/aspnet/Razor/issues/485);
[Razor syntax reference](https://learn.microsoft.com/en-us/aspnet/core/mvc/views/razor?view=aspnetcore-10.0);
[Blazor 5.0 — insignificant whitespace trimmed at compile time](https://learn.microsoft.com/en-us/dotnet/core/compatibility/aspnet-core/5.0/blazor-components-trim-insignificant-whitespace)
(the ship-opt-in-then-flip-default precedent for the 2.0 window).

### D7 — The eligible set is mechanism-based, and pinned

**Decision.** Trimming applies to exactly the spans the compiler already removes from the
working document, and nothing else:

| Eligible | Removal mechanism | Members |
| --- | --- | --- |
| Zero-output chains | `RemoveEmptyItem` (threaded return type `null`) | `@using(){{…}}`, `@model(){{…}}`, `@import(){{…}}`, phase 2's body-form `@profile(){{…}}`, and any custom extension whose `InitStart` returns `null` (the general rule, documented in `custom-extensions.md`) |
| Definition blocks | `RemoveDefinitions` | `@% … %@` (single- and multi-line) |
| Parse-time imports | `RemoveDefinitions` (span recorded in `DefinitionsBlock.Positions`, verified) | `@<<{{path}}` |
| Comment-only remnant lines | `TrimHiddenRemnantLines` (D6) | whole-line `@* … *@` |

Explicitly **not** eligible: `@param(...)` (its `InitStart` returns `dataType` — the block
stays in the document and renders nothing by itself; its lines keep needing `@\`, noted in
docs), raw blocks (`@{ … }@`, `@:` — they *produce* output via `ReplaceRawOutput`), branch
blocks and every other rendering chain, `-> chain` default outputs (they occupy no
document span of their own), and interleaved text stripped by phase 3 (owned by its strip
machine). Phase 2's `@profile` qualifies precisely because its spec (phase 2 D2) pinned
the body form whose `InitStart` returns `null` — the paren form `@profile(html)` remains
the discouraged error path and never reaches removal.
**Rationale.** Keying on the removal mechanism instead of a name list means the eligible
set can never drift from what actually vanishes from the document — the roadmap's own
"blocks removed via `RemoveEmptyItem` … plus definitions and imports" phrasing, made
closed under custom extensions.
**Alternatives rejected.** A name whitelist (`using`, `model`, …) — diverges the moment a
host writes a directive-style extension, and requires the compiler to know names it
otherwise never inspects; trimming `@param` too (a behavior change for a block that stays
in the document — its removal is a separate design question with a deferred trigger).

### D8 — Where trimming runs: widen at the removal sites; the pipeline order is pinned

**Decision.** Trimming is **not a pass** — it is span-widening applied inside the two
existing removal helpers plus the one new remnant step, leaving the pinned
`HeddleCompiler.Compile` order (with phase 3 merged). Every step below is a member of
[HeddleCompiler](../../../src/Heddle/Runtime/HeddleCompiler.cs) — including
`ProcessBranchSets`, which phase 3's spec pins there ("called once per `Compile`
invocation between `ReplaceRawOutput` and the chain-compile loop"):

1. `ShiftBySkippedTokens` — positions aligned to the clean document.
2. **`TrimHiddenRemnantLines`** (new; only when the option is on) — comment-only lines
   removed; shifts `OutputChains`, `DefinitionsBlock.Positions`, and `RawOutputItems`
   using the same reverse-loop pattern `ShiftBySkippedTokens` already applies to those
   three lists.
3. `RemoveDefinitions(parseContext, ref workingDocument, bool trimDirectiveLines)` — each
   definition/import span is passed through `WidenToWholeLine` first; the existing
   `ApplyRemove` + reverse shift loops run on the (possibly widened) span, comparing
   against its boundaries.
4. `ReplaceRawOutput` — unchanged.
5. `ProcessBranchSets` (phase 3) — unchanged, and unaffected: it sees a document in which
   definition and import lines are already gone (they always were — only the widths
   changed) with all chain positions consistently shifted.
6. The `OutputChains` compile loop, where
   `RemoveEmptyItem(parseContext, blockPosition, ref workingDocument, bool trimDirectiveLines)`
   widens each zero-output chain's span at the moment its compile determines removal. Only
   `OutputChains` need shifting at this stage (skipped tokens, definitions, and raw spans
   are already consumed — verified). `OutputItem.Position` diagnostics coordinates are
   never touched, preserving the existing position discipline.
7. The `DefaultChains` loop — no trimming (synthetic end-of-document blocks).

Composition with phase 3, pinned by fixture: a directive between branch blocks is an
Other block that ends stripping adjacency (phase 3 D10) *before* trimming runs; trimming
then removes the directive's own line, and whatever inter-branch newlines remain are gap
text owned by the strip machine's rules — the two removals cannot overlap because strip
gaps lie strictly between branch blocks and a directive block ends the set. Whitespace-only
gaps shrunk by step 2 (a comment line between branches) still strip silently. `Compile`
reads the flag once from `compileScope.Options.TrimDirectiveLines`; child compiles
(bodies, partials, `@import` targets) inherit it through the options copy constructor, so
whole-line directives inside subtemplate bodies trim identically.
**Rationale.** The widening reuses the exact `ApplyRemove` + reverse-shift machinery that
already owns document surgery (the same reuse argument phase 3 recorded for its strip
machine); a separate post-pass would need its own position bookkeeping and could not see
per-chain removal decisions, which only exist mid-loop.
**Alternatives rejected.** Trimming before `ProcessBranchSets` for chains (impossible —
removal eligibility is known only when the chain's threaded type resolves during compile);
trimming inside `RuntimeDocument.GetDocumentPieces` (slicing must stay a pure projection;
it has no knowledge of why a gap exists); a lexer-level solution (the grammar is frozen
this phase, and the lexer cannot consult options).

### D9 — `TrimDirectiveLines` on `TemplateOptions`: copied, in identity, default `false` until 2.0

**Decision.** `public bool TrimDirectiveLines { get; set; }` — initialized explicitly to
`false` in both constructors (the 2.0 flip is a two-line greppable change, phase 2 D11's
pattern), copied in the copy constructor (the completeness invariant; phase 2's reflection
test `TemplateOptionsCompletenessTests` covers the new property automatically via its
`bool` synthesizer — no test change needed, referenced as the guard), and **participating
in `Equals`/`GetHashCode`**:

```csharp
// Equals gains:        && other.TrimDirectiveLines == TrimDirectiveLines
// GetHashCode becomes: ((((TemplateName?.GetHashCode() ?? 0) * 397) ^ (int) OutputProfile) * 397)
//                          ^ (TrimDirectiveLines ? 1 : 0)
```

Identity participation follows phase 2 D6's recorded reasoning verbatim: a property joins
identity iff it changes rendered bytes for identical inputs. `OutputProfile` did;
`TrimDirectiveLines` does (whitespace differs); `ExpressionMode`/`Functions` still do not
(they select how a template compiles, not what identical compiles produce). At 2.0 the
default flips to `true` as one of the three bundled breaking changes
([cross-cutting D2](../common/cross-cutting-decisions.md#d2--the-single-20-breaking-window));
the migration note is "set `TrimDirectiveLines = false` to keep 1.x whitespace
byte-exact". No other valve ships (mirroring phase 2's ratified no-global-valve stance).
**Rationale.** Consistency with the shipped identity semantics is itself the requirement —
two options-keyed caches distinguishing profiles but not trimming would serve
wrong-whitespace templates, the exact defect class phase 2 D8 fixed.
**Alternatives rejected.** Keeping it out of identity "because it is just whitespace"
(goldens are byte-compared for a reason; whitespace is output); an enum
(`TrimMode { None, DirectiveLines }`) anticipating finer modes (no second mode has a
scenario — YAGNI; a future mode can widen the surface additively).

### D10 — Resolver: trim joins the cache key; a fourth scalar ctor; prototype ctor stays rejected

**Decision.** `TemplateResolver` (as shaped by phase 2 D8) changes twice:

- `CacheKey` becomes `fullPath + "|" + profile + (trimDirectiveLines ? "|trim" : string.Empty)`
  at every probe and write; the per-operation effective value is
  `context?.Options.TrimDirectiveLines ?? _trimDirectiveLines`, threaded exactly like the
  effective profile.
- A new constructor
  `public TemplateResolver(string rootPath, bool checkFileChange, OutputProfile defaultProfile, bool trimDirectiveLines)`;
  the phase 2 three-parameter ctor chains to it with `false`, the original two-parameter
  ctor keeps chaining with (`Text`, `false`). The field is applied at the three
  resolver-built options sites.

Phase 2's deferred trigger for a prototype-options constructor ("a second
resolver-configured option beyond the profile") has now **fired and is evaluated**: the
prototype ctor is still rejected. Two scalars remain a manageable surface, and the
divergence problem phase 2 named is unchanged — a prototype `TemplateOptions` carries
resolver-owned fields (`FileNamePostfix`, `RootPath`, `TemplateName`) that would need
documented ignore rules, a foot-gun bigger than the problem. The item is re-deferred with
the trigger sharpened to *a third* resolver-configured option.
**Rationale.** Same correctness argument as phase 2 D8: a cache key blind to an
output-changing option silently serves wrong bytes across contexts.
**Alternatives rejected.** Prototype-options ctor (above — recorded evaluation of a fired
trigger, not a silent skip); leaving the cache key alone because trimming is 1.x-rare
(the defect is silent and permanent once a host mixes contexts; rarity is not a defense).

### D11 — Two imports, verified: `@<<` composes; `@import()` parses into an unreachable context

**Decision.** Both implementations ship unchanged (maintainer-ratified: keep both, no
delegation, no behavior change), pinned by characterization fixtures written **before**
the docs (the roadmap's TDD verdict for 4.4). The verified behavioral contrast the docs
must state:

| Behavior | `@<<{{path}}` (parse-time) | `@import(){{path}}` (compile-time) |
| --- | --- | --- |
| When it runs | During the walk (`ExitImport_block`) | During `InitStart` of the `import` extension |
| Path resolution | `Path.Combine(Options.RootPath, path)` — relative to the template root; an absolute path wins per `Path.Combine` semantics | Identical: `Path.Combine(Options.RootPath, body text)` (both verified in source — the one dimension where the forms genuinely agree) |
| Definitions | Merged into the importing document — callable after the import line. A name already defined **before** the import is a compile error (`The definition <name> with the same name already exists`): the imported file's `EnterDef` runs against the isolation snapshot, which starts as a copy of the importing document's registry — `@<<` composes libraries, it cannot silently override host definitions | Parsed into the `@import` body's already-compiled isolated context — **never visible** to the importing document; a subsequent call fails with `Cannot find extension <name>` (HED0002) |
| Imported top-level output chains | Re-based as zero-length blocks and **rendered at the import position** | Added to the same dead context — never compiled, never rendered |
| Imported default chains (`->`) | Carried into the importing document's `DefaultChains` | Dead, as above |
| Imported static text | Dropped (only chains and definitions transfer) | Dropped |
| Errors in the imported file | Surface on the importing compile | Surface on the importing compile (shared error list — the one observable effect beyond block removal) |
| Missing file | Compile **fails**, but ⚠ not at the import position: the `FileNotFoundException` escapes the walk and is caught by `HeddleTemplate.Compile`'s outermost `catch (Exception)`, which records the BCL exception message at `default(BlockPosition)` — position `0:0`. Nothing positions walk-time exceptions today; pinned as-is (I09), a positioning fix would be a behavior change outside this phase's ratified scope | Compile error `Error while compiling import` **at the call position** (`item.Position`) — the `Compile` chain-loop catch around `CompileItem`; the `FileNotFoundException` rides along as `Exception` |
| The import block itself | Excised via `RemoveDefinitions`; trim-eligible (D7) | Excised via `RemoveEmptyItem`; trim-eligible (D7) |

⚠ This **corrects the roadmap's description** of `@import()` as "the inline-parse variant
(the file is parsed directly into the live context)": the context it parses into is the
body's isolated snapshot whose own compile has already finished, and `DefinitionBlock`
copies entries with no parent chaining — the evidence chain is in
[Assumed state](#assumed-state) and the fixtures make it executable. The correction
changes the *documentation content*, not the ratified action: both forms stay, behavior
frozen by the fixtures, and the published guidance becomes — `@<<{{path}}` is **the**
composition import (definition libraries, layout sharing); `@import(){{path}}` is a
compile-time include that today validates the target file (its errors surface) without
merging anything, kept for compatibility; new templates should always use `@<<`. The
false claims in [built-in-extensions.md](../../built-in-extensions.md) ("the extension
behind the `@<<` sugar", `import` section) and
[language-reference.md](../../language-reference.md) ("imports are handled by the `import`
extension machinery", Imports section) are removed in WI8.
**Rationale.** Characterization before documentation is the only honest sequence for an
unexercised code path; the ratified keep-both decision explicitly traded convergence work
for documentation, and documentation must describe verified behavior, not the roadmap's
pre-verification sketch.
**Alternatives rejected.** Delegating `@import()` to the `@<<` machinery (explicitly
rejected by the maintainer's ratified decision — a behavior change); documenting
`@import()` as an alias or near-equivalent (the grounding round proved equivalence false,
and this spec's verification shows even "inline parse" oversells it); removing or
obsoleting `@import()` now (public surface removal belongs to a ratified window — a
deferred item records the trigger).

### D12 — Docs deliverable: which pages change and what they say

**Decision.** One docs work item (WI8) updates:

- [language-reference.md](../../language-reference.md) — the *Imports* section (line
  ~772) is rewritten around the D11 table's author-facing rows (two distinct forms, when
  to choose which — always `@<<` for sharing; the `@import` machinery sentence deleted);
  the *Whitespace trimming `@\`* section (line ~845) gains the `TrimDirectiveLines`
  subsection (whole-line rule, comment lines, `@\` staying for mid-line control, 2.0
  default note); the *Default output `-> chain`* section (line ~492) gains the
  double-render warning note; a counted-loop example joins the loop material.
- [built-in-extensions.md](../../built-in-extensions.md) — the `for` entry documents the
  `int` model and `range(...)`; the `import` entry's "sugar" sentence is replaced by the
  D11 contrast and a pointer to `@<<`.
- `docs/native-expressions.md` (phase 1's page, created by its D19) — `range` joins
  the built-ins table with the step rules and the standalone-call note.
- [csharp-api.md](../../csharp-api.md) — `TemplateOptions.TrimDirectiveLines` row (with
  identity participation), the new `TemplateResolver` constructor.
- [custom-extensions.md](../../custom-extensions.md) — one paragraph: extensions whose
  `InitStart` returns `null` participate in directive-line trimming automatically (D7's
  general rule).
- [patterns.md](../../patterns.md) — the *Whitespace: when to bother with `@\`* recipe is
  rewritten around the option; *An entry layout that renders itself* gains the
  don't-also-call-it warning cross-reference; *Local helper definitions* gains the
  `@<<`-library sharing recipe (the roadmap's "recipes use `@<<` for definition
  libraries" item — the page currently has no import recipe at all, so this is an
  addition, not a correction).

**Rationale.** Matches the API-docs rule in the
[coding standards](../common/coding-standards.md#api-design-and-compatibility); every page
touched has a concrete stale or missing claim identified during verification.
**Alternatives rejected.** A dedicated imports page (section-sized material; phase 2's
precedent of section-level docs applies).

## Implementation plan

Work items in execution order; each runs the
[canonical loop](../common/testing-standards.md#the-canonical-loop). "Check" is the
completion gate. The four features are independent — WI3–WI6 may land as separate PRs,
but the phase exits on one combined run (WI7).

**WI1 — Failing executable spec + characterization capture.**
Files: `src/Heddle.Tests/ForSugarTests.cs` (the F-matrix below),
`src/Heddle.Tests/RangeFunctionTests.cs` (step matrix incl. negatives),
`src/Heddle.Tests/DoubleRenderWarningTests.cs` (W-rows),
`src/Heddle.Tests/TrimDirectiveLinesTests.cs` (T-torture rows),
`src/Heddle.Tests/ImportFormsPinningTests.cs` (I-rows — **written green against current
behavior first**, per the characterization verdict), the model
`src/Heddle.Tests/Data/ErgoForData.cs` (`public int Count { get; set; }`), fixtures
`ergo-for.heddle`, `ergo-trim-preamble.heddle`, `ergo-double-render.heddle`,
`ergo-import-library.heddle`, `ergo-import-composition.heddle`, `ergo-import-inline.heddle`
under `src/Heddle.Tests/TestTemplate/` (contents pinned in the testing plan's named-assets
list; goldens captured in WI7, except the import pair whose goldens pin *current* behavior
and are captured now).
Check: import pinning tests green against today's engine; every other new test fails for
the right reason (type error, missing member, unknown function, whitespace present).

**WI2 — Options and resolver plumbing (D9, D10).**
Files: `src/Heddle/Data/TemplateOptions.cs` (property, both ctors, copy ctor,
`Equals`/`GetHashCode`), `src/Heddle/Runtime/TemplateResolver.cs` (fourth ctor, chained
defaults, `_trimDirectiveLines`, `CacheKey` extension, effective-value threading).
Check: `TemplateOptionsCompletenessTests` green (automatic coverage of the new property);
new identity rows green (differ only by trim → unequal, distinct hashes); resolver
serves distinct instances per trim value with no cache collision.

**WI3 — `@for` int handling (D1).**
Files: `src/Heddle/Extensions/ForIndexExtension.cs` (second attribute + both loop
rewrites), `src/Heddle/Data/HeddleDiagnosticIds.cs` (`ReturnTypeMismatch = "HED0004"`),
`src/Heddle/Runtime/HeddleCompiler.cs` (`CheckTypes`' `ToError` call gains the ID —
message text byte-identical).
Check: F01–F06 green with `AllowCSharp = false`; F07 asserts HED0004 + position; existing
suite green byte-identical (ForModel path untouched, `ExStringBuilder` shape preserved).

**WI4 — `range` function (D2, D3).**
Files: `src/Heddle/Runtime/Expressions/BuiltInFunctions.cs` (two `Range` overloads +
runtime guard), `src/Heddle/Runtime/Expressions/FunctionRegistry.cs` (`Default` gains
`range`), `src/Heddle/Runtime/Expressions/NativeExpressionCompiler.cs` (the literal-step
static check beside the HED1015 machinery), `src/Heddle/Data/HeddleDiagnosticIds.cs`
(`RangeStepNotPositive = "HED4001"`).
Check: F08–F10 green; R-negatives assert HED4001 ID + step-argument position for literal
and sign-prefixed-literal steps; the model-driven-step row asserts
`TemplateProcessingException` at render with the same message; a host-overridden `range`
row proves the static check does not fire for non-built-in bindings.

**WI5 — Double-render warning (D4, D5).**
Files: `src/Heddle/Language/DefinitionItem.cs` (`HasDefaultOutput` + copy-ctor line),
`src/Heddle/Language/ParseContext.cs` (`CreateDefinition` sets it; `CreateItem` marks the
self-call), `src/Heddle/Language/OutputItem.cs` (`IsDefaultChainSelfCall` + copy),
`src/Heddle/Runtime/HeddleCompiler.cs` (the `CompileItem` check),
`src/Heddle/Data/HeddleDiagnosticIds.cs` (`DefinitionRendersTwice = "HED4002"`),
`src/Heddle.Tests/HeddleTemplateTests.cs` (`VcGenerateTest` gains the two-warning
assertion per D5).
Check: W-rows green (single warning per call site, both positions referenced, self-call
exempt, layering rows both directions, derived-name negative); the corpus scan reports
exactly the two vc-test hits.

**WI6 — Directive-line trimming (D6–D8).**
Files: `src/Heddle/Runtime/HeddleCompiler.cs` (`WidenToWholeLine`,
`TrimHiddenRemnantLines`, flag-taking `RemoveDefinitions`/`RemoveEmptyItem` signatures,
`Compile` wiring).
Check: T-torture rows green (LF/CRLF/CR, indentation, trailing spaces, shared lines,
`@\` idempotence, comment lines, multi-line definitions, `@<<` lines, `@profile` lines,
`@param` non-eligibility, EOF, blank-line preservation, body-nested directives,
branch-adjacent directive composition); the full existing suite green **byte-identical
with the option off** — any diff is a widening bug by definition.

**WI7 — Goldens, corpus scan, combined gate.**
Files: goldens `generated-ergo-for.html`, `generated-ergo-trim-on.html`,
`generated-ergo-trim-off.html` (the golden pair — roadmap criterion 3),
`generated-ergo-double-render.html`, plus the WI1 import goldens
`generated-ergo-import-composition.html` / `generated-ergo-import-inline.html`.
Check: the full [regression gate](../common/testing-standards.md#regression-gates) in one
combined run — build all TFMs (`netstandard2.0;net6.0;net8.0;net10.0`, tests add `net48`
on Windows); full suite zero failures; every pre-existing golden byte-identical (options
off); **grammar stability** — `src/Heddle.Language/generated/` has no diff;
`TextRenderBenchmarks` before/after on one machine (the `ForIndexExtension` rewrite
touches the render path — allocated bytes must not increase, mean within reported error);
the D5 warning scan; roadmap success criteria 1–5 asserted together.

**WI8 — Published docs (D12).**
Files: `docs/language-reference.md`, `docs/built-in-extensions.md`,
`docs/native-expressions.md`, `docs/csharp-api.md`, `docs/custom-extensions.md`,
`docs/patterns.md` per D12.
Check: `cd docs && npm run docs:build` passes (uppercase-drive cwd on Windows;
`preserveSymlinks` already set); no dangling anchors; the import section's claims match
the I-row fixtures line for line.

## Public API contract

All additions are additive
([API design rules](../common/coding-standards.md#api-design-and-compatibility)); XML docs
required on every member. Thread-safety notes inline.

```csharp
namespace Heddle.Data
{
    public class TemplateOptions
    {
        /// <summary>When true, whole-line directives (@using, @model, @profile, @import,
        /// definitions, @&lt;&lt; imports, whole-line comments, and any extension block
        /// removed at compile time) swallow their line — leading indentation, trailing
        /// spaces, and one line terminator. Default: false in 1.x; flips to true in the
        /// 2.0 window. Participates in Equals/GetHashCode — trimming changes output
        /// bytes, so it keys template caches. Compile-time only; never read at render.</summary>
        public bool TrimDirectiveLines { get; set; }
    }

    public static class HeddleDiagnosticIds
    {
        public const string ReturnTypeMismatch     = "HED0004";
        public const string RangeStepNotPositive   = "HED4001";
        public const string DefinitionRendersTwice = "HED4002";
    }
}

namespace Heddle.Runtime
{
    public class TemplateResolver
    {
        /// <summary>Creates a resolver whose resolver-built templates compile under the
        /// given default profile and trimming setting; per-call CompileContext options
        /// override both. The existing constructors keep (Text, false).</summary>
        public TemplateResolver(string rootPath, bool checkFileChange,
            OutputProfile defaultProfile, bool trimDirectiveLines);
    }
}

namespace Heddle.Language
{
    public class DefinitionItem
    {
        /// <summary>True when this declaration layer carried a default output
        /// ('-> chain'). Preserved across full overrides — the default chain declared by
        /// an earlier layer keeps rendering at document end, so the double-render
        /// warning (HED4002) stays accurate. Set by the parser; read-only for hosts.</summary>
        public bool HasDefaultOutput { get; internal set; }
    }
}

namespace Heddle.Extensions
{
    [ExtensionName("for")]
    [DataType(typeof(ForModel))]
    [DataType(typeof(int))]   // new — @for(5) / @for(Count): iterate 0…n−1
    public class ForIndexExtension : AbstractExtension
    {
        // Signatures unchanged; ProcessData/RenderData additionally accept a boxed int
        // model per D1. Stateless — safe for concurrent renders, as today.
    }
}
```

The default function registry's documented content grows by one name: `range(start, last)`
and `range(start, last, step)` returning `ForModel` (D2) — surfaced through the existing
`FunctionRegistry.Default` XML docs and the phase 1 docs page, not a new API symbol.

Internal additions (listed for the implementer): `OutputItem.IsDefaultChainSelfCall`
(internal, copied on isolation), `BuiltInFunctions.Range` ×2,
`HeddleCompiler.WidenToWholeLine` + `TrimHiddenRemnantLines` (private static),
flag-taking `RemoveDefinitions`/`RemoveEmptyItem` signatures, `TemplateResolver`'s
`_trimDirectiveLines` field and extended `CacheKey`.

Thread-safety summary: every addition is compile-time state or an immutable attribute;
`ForIndexExtension` stays instance-stateless (locals only); no render-path type gains a
field. No new concurrency surface exists, so no new parallel-render test is mandated by
the [standards' concurrency rule](../common/testing-standards.md#test-authoring-conventions).

## Diagnostics

Claimed from the `HED4xxx` block plus one assigned-as-touched core ID
([registry](../common/cross-cutting-decisions.md#d1--stable-diagnostic-ids-hedxxxx));
consistent with prior claims (`HED0001`–`0003`, `HED1001`–`1017`, `HED2001`–`2003`,
`HED3001`–`3004` — verified against all three prior specs). Positions are absolute
template positions (`OutputItem.Position` / expression-node positions), matching every
existing diagnostic. `<…>` are message parameters.

| ID | Severity | Message text | Trigger / position |
| --- | --- | --- | --- |
| HED0004 | Error | `Return Type is <type> but any of [<expected>] expected.` *(pre-existing text, unchanged — ID assigned because this phase changes the trigger set for `for` and asserts it by ID)* | `CheckTypes` mismatch — notably `@for(Name)` with a `string` member, `@for(3.5)`, and any `[DataType]`-guarded extension fed an incompatible typed value. Position: the call (`extensionItem.Position`). |
| HED4001 | Error | `Function 'range' requires a positive step, but <step> was supplied — a zero or negative step never terminates the loop.` | The step argument of the built-in three-argument `range` is a literal (or sign-prefixed literal) `<= 0` (D3). Position: the step argument node. The identical condition reached only at render (model-driven step) throws `TemplateProcessingException` with the same message — no ID, per the phase 3 precedent for runtime errors. |
| HED4002 | Warning | `Definition '<name>' (declared at <declPosition>) has a default output ('->') and is also called by name — it renders twice.` `Fix`: `Remove the '->' from the definition, or remove this call.` | `CompileItem` resolves a by-name call to a definition whose `HasDefaultOutput` is true and the call is not the default chain's own synthetic self-call (D4). Position: the call site; `<declPosition>` is `DefinitionItem.Position` (`start:length`, relative to the declaring scope — top-level declarations are absolute). One warning per distinct call site (memoized). |

Two formatting pins for the parameters above:

- `<declPosition>` in HED4002 is produced by `BlockPosition.ToString()` — verified
  `"{StartIndex}:{Length}"` ([BlockPosition.cs](../../../src/Heddle/Strings/Core/BlockPosition.cs)),
  so a definition at offset 312 spanning 9 characters reads `312:9`.
- HED4001's runtime mirror is `TemplateProcessingException` whose `Message` is the
  HED4001 text with `<step>` substituted under `CultureInfo.InvariantCulture` — verbatim
  for step `0`: `Function 'range' requires a positive step, but 0 was supplied — a zero
  or negative step never terminates the loop.` No `HED` prefix, no position (the
  message-only constructor; D3 pins the guard). R03 asserts this string exactly.

## Testing plan

Instantiates the [testing standards](../common/testing-standards.md); suite homes,
fixture conventions (`ergo-` stem, sibling goldens, line-ending normalization when
*reading* fixtures — torture rows with load-bearing CRLF/LF live as inline `[Theory]`
strings instead), and gates apply as written there.

**TDD verdict (carried from the roadmap, unweakened).** Yes across the board: the 4.1
decision matrix (zero step → error, negative step → error, negative count → empty) lands
as failing tests before the extension changes; 4.3's torture rows are written first
*because* it is the risk item — each whitespace interaction is a red test before the span
arithmetic exists (and the corpus is shared with phase 3's stripping suite, which
exercises the same document-surgery seam); 4.4 is characterization testing by definition —
current behavior of both import forms is captured green *before* the docs that describe
it; 4.2's fixtures precede the compiler check.

**`@for`/`range` matrix** (`ForSugarTests` / `RangeFunctionTests`; all rows run with
`AllowCSharp = false`):

| # | Template / setup | Expected |
| --- | --- | --- |
| F01 | `@for(3){{x}}` | `xxx` (roadmap criterion 1) |
| F02 | `@for(3){{<i>@out()</i>}}` | `<i>0</i><i>1</i><i>2</i>` (index on the chained channel, as today) |
| F03 | `@for(Count){{x}}`, `Count = 4` (typed model) | `xxxx` |
| F04 | `@for(Count)`, `Count = 0` | empty |
| F05 | `@for(-3){{x}}` | empty (negative count = no iterations) |
| F06 | `@for(NullableCount){{x}}`, `int? = null` / `= 2` | empty / `xx` |
| F07 | **Negative** `@for(Name)`, `Name : string` | HED0004 with the call position |
| F08 | `@for(range(2, 8, 3)){{@out() }}` | `2 5 ` (start/step honored) |
| F09 | `@for(range(5, 5))` / `@for(range(7, 3))` | empty (stop-exclusive; start ≥ last) |
| F10 | `@for(@new ForModel() { Last = 2 })` with `AllowCSharp = true` | unchanged behavior (ForModel path untouched) |
| R01 | **Negative** `@for(range(1, 5, 0))` | HED4001 at the step literal |
| R02 | **Negative** `@for(range(5, 1, -1))` | HED4001 (sign-prefixed literal) |
| R03 | **Negative** `range(1, 5, Step)` with model `Step = 0` | `TemplateProcessingException` at render, HED4001 message text |
| R04 | Host registry replaces `range(int,int,int)`; template uses step `0` | no HED4001 (static check is built-in-only); host function's own behavior governs |

**Double-render rows** (`DoubleRenderWarningTests` + `ergo-double-render.heddle`):

| # | Scenario | Expected |
| --- | --- | --- |
| W01 | `<card> -> ()` + one `@card()` call | exactly one HED4002 at the call, message names `card` and the declaration position; output renders the definition twice (behavior unchanged — golden pins it) |
| W02 | Remove the `->` | no warning |
| W03 | Remove the call | no warning (the default chain's self-call is exempt) |
| W04 | Two `@card()` calls | two warnings, one per call site |
| W05 | Base `<a> -> ()`, call, then full override `<a:a>` | warning at the call (resolves the base layer) |
| W06 | Base `<a> -> ()`, full override `<a:a>`, then call | warning at the call (`HasDefaultOutput` survives `OverrideWith` — the base's default chain still renders at document end) |
| W07 | `<a> -> ()`, derived `<b:a>` (different name), call `@b()` | no warning (`b` has no default output; no double render of `b` exists) |
| W08 | Corpus scan | exactly two warnings, both in `vc-test.heddle` at the `@default()` calls; zero elsewhere (D5) |

**Trim torture rows** (`TrimDirectiveLinesTests`; inline templates, explicit `\n`/`\r\n`;
every row also runs with the option **off** asserting byte-identical current output):

| # | Template (escaped) | On → output |
| --- | --- | --- |
| T01 | `"@using(){{System.Linq}}\nX"` | `X` |
| T02 | `"@using(){{System.Linq}}\r\nX"` | `X` (CRLF pair swallowed) |
| T03 | `"  @model(){{T}}\nX"` | `X` (leading indentation swallowed) |
| T04 | `"@model(){{T}}  \t\nX"` | `X` (trailing spaces/tabs before the terminator swallowed) |
| T05 | `"A @using(){{S}}\nX"` | `A \nX` (not whole-line — block removal only, exactly as today) |
| T06 | `"@using(){{A}}@model(){{B}}\nX"` | `X` (shared line composes — second block becomes whole-line after the first is removed) |
| T07 | `"@using(){{S}}@\\\nX"` | `X` (idempotent — `@\` already ate the terminator; nothing extra removed) |
| T08 | `"@using(){{S}}\n@* note *@\n@model(){{T}}\nX"` | `X` (comment-only remnant line removed by `TrimHiddenRemnantLines`) |
| T09 | `"@model(){{T}} @* note *@\nX"` | `X` (comment excised by the lexer; remaining whitespace + terminator trims) |
| T10 | `"@%<d>{{y}}%@\nX"` | `X` (single-line definition block) |
| T11 | `"@%\n<d>{{y}}\n%@\nX"` | `X` (multi-line block — predicate is per-block: first-line prefix and last-line suffix are whitespace) |
| T12 | `"@<<{{ergo-import-library.heddle}}\nX"` | `X` (parse-time import line) |
| T13 | `"@profile(){{html}}\n@(V)"` | encoded `V` only (phase 2 body-form directive trims) |
| T14 | `"@param(Name)\nX"` | `\nX` (not eligible — block stays, renders nothing, line intact) |
| T15 | `"X\n@using(){{S}}"` | `X\n` → `X` plus no trailing residue (EOF terminator case) |
| T16 | `"@using(){{S}}\n\nX"` | `\nX` (the authored blank line is preserved — only the directive's own line trims) |
| T17 | `"@if(F){{a}}\n@using(){{S}}\n@else(){{b}}"` | directive line swallowed; the surviving inter-branch newline follows phase 3's rules (set ended at the directive — gap text renders); no HED3001/HED3003 |
| T18 | `"@if(F){{\n  @using(){{S}}\n  body}}"` | body renders `  body` — trimming applies inside subtemplate bodies via the inherited option |
| T19 | `"@using(){{S}}\rX"` | `X` (bare CR terminator) |

**Import pinning rows** (`ImportFormsPinningTests`; `ergo-import-library.heddle` contains
a definition `<lib_badge>{{<b>lib</b>}}`, one line of static text `STATIC-IN-LIB`, one
top-level output chain `@lib_badge()`, and a `<lib_footer>{{F}} -> ()` default):

| # | Scenario | Pinned current behavior |
| --- | --- | --- |
| I01 | `@<<{{lib}}` then `@lib_badge()` | badge renders — definitions merged |
| I02 | `@<<{{lib}}` alone | lib's own `@lib_badge()` chain renders **at the import position**; `F` renders at document end (default chain carried) |
| I03 | `@<<{{lib}}` | `STATIC-IN-LIB` absent — imported static text never transfers |
| I04 | `@import(){{lib}}` then `@lib_badge()` | `Cannot find extension <lib_badge>` (HED0002) — nothing merged |
| I05 | `@import(){{lib}}` alone | renders nothing; no lib output, no `F` |
| I06 | `@import(){{broken}}` (syntax error in target) | importing compile fails with the imported file's syntax errors (shared error list) |
| I07 | `@import(){{missing}}` | compile error `Error while compiling import` at the call position |
| I08 | Goldens for I02/I05 | `generated-ergo-import-composition.html` / `generated-ergo-import-inline.html` — the side-by-side pair (roadmap criterion 4) |
| I09 | `@<<{{missing.heddle}}` (inline template, no fixture — a failing compile carries no golden) | compile fails; the single error sits at position `0:0` (`default(BlockPosition)` — the walk-time `FileNotFoundException` reaches `HeddleTemplate.Compile`'s outer catch, D11's corrected row). The assertion is `Success == false` + the `0:0` position, **not** the BCL message text (Heddle does not own that string) |

**Named test assets** (all in [src/Heddle.Tests](../../../src/Heddle.Tests)): the five
test classes above plus the fixtures below. Fixture contents are normative; byte strings
are written with explicit escapes and assume the standards' LF normalization on read.
Goldens for the new features are asserted against the byte strings stated here; the two
import goldens are **captured** (characterization) — the derivations below say what the
capture must produce, and a deviation at capture time is investigated against D11's
evidence chain before the golden is committed (fix-forward), never papered over.

*`ergo-for.heddle`* → `generated-ergo-for.html`. Model: `ErgoForData`
(`public int Count { get; set; }`, new file `src/Heddle.Tests/Data/ErgoForData.cs`),
rendered with `Count = 2`, `AllowCSharp = false`, trimming off:

```heddle
@using(){{Heddle.Tests.Data}}
@model(){{ErgoForData}}
@for(3){{<li>@out()</li>}}
@for(Count){{<i>@out()</i>}}
@for(range(2, 8, 3)){{[@out()]}}
```

Expected golden bytes (the two directive lines leave their bare terminators — trimming is
off in this fixture; that is deliberate, so the `@for` golden cannot mask a trim bug):
`\n\n<li>0</li><li>1</li><li>2</li>\n<i>0</i><i>1</i>\n[2][5]\n`.

*`ergo-trim-preamble.heddle`* → `generated-ergo-trim-on.html` +
`generated-ergo-trim-off.html` (the golden pair — criterion 3). The realistic preamble:
`@using` ×2, a whole-line comment, `@model`, a definition block, then markup. Rendered
with a fresh `TestDataStructure` (nothing dereferences it):

```heddle
@using(){{Heddle.Tests.Data}}
@using(){{System.Linq}}
@* page metadata *@
@model(){{TestDataStructure}}
@%
<badge>
{{<b>NEW</b>}}
%@
<h1>@badge()</h1>
```

Off-golden bytes — one bare terminator per preamble line, exactly today's output (the
comment line's `\n` survives because the lexer excised only the hidden token; the other
four are the removal remainders): `\n\n\n\n\n<h1><b>NEW</b></h1>\n`.
On-golden bytes — the two `@using` lines and the `@model` line trim as whole-line
zero-output chains (D7), the comment remnant trims via `TrimHiddenRemnantLines` (D6), the
multi-line definition block trims via the widened `RemoveDefinitions` (D8 step 3):
`<h1><b>NEW</b></h1>\n`. The off-golden is byte-identical to pre-phase rendering of the
same fixture — asserted by running it once against the option-less pipeline shape (the
T-rows' off-assertions generalized to a file fixture).

*`ergo-double-render.heddle`* → `generated-ergo-double-render.html` (trimming off; one
HED4002 expected at the `@card()` call):

```heddle
@%
<card> -> ()
{{CARD}}
%@
@card()
```

Expected golden bytes: `\nCARD\nCARD` — the definition block leaves its terminator, the
by-name call renders `CARD` in place, and the default chain renders `CARD` once more at
document end (after the trailing `\n`). The golden is the standing proof that the warning
does not alter output (D5's role for vc-test, in miniature).

*`ergo-import-library.heddle`* — the shared import target (never rendered directly; no
golden of its own):

```heddle
@%
<lib_badge>
{{<b>lib</b>}}
<lib_footer> -> ()
{{F}}
%@
STATIC-IN-LIB
@lib_badge()
```

*`ergo-import-composition.heddle`* → `generated-ergo-import-composition.html` (I01–I03,
I08):

```heddle
BEFORE
@<<{{ergo-import-library.heddle}}
AFTER
@lib_badge()
```

Expected capture: `BEFORE\n<b>lib</b>\nAFTER\n<b>lib</b>\nF` — reading order: the
library's own `@lib_badge()` chain re-based to the import position (first `<b>lib</b>`),
the `@<<` line's terminator surviving as the `\n` before `AFTER` (trimming off — the
block span alone is excised), the importing document's own call (second `<b>lib</b>`),
and the carried `<lib_footer>` default chain (`F`) at document end. `STATIC-IN-LIB`
must be absent (I03).

*`ergo-import-inline.heddle`* → `generated-ergo-import-inline.html` (I05, I08):

```heddle
BEFORE
@import(){{ergo-import-library.heddle}}
AFTER
```

Expected capture: `BEFORE\n\nAFTER\n` — the `@import` block is excised via
`RemoveEmptyItem` (its terminator survives), and nothing from the library renders: no
`<b>lib</b>`, no `F`, no `STATIC-IN-LIB`. The I04/I06/I07/I09 negatives use inline
templates in `ImportFormsPinningTests` (failing compiles carry no goldens).

**Regression gate** (one combined run): build all TFMs; full suite (all TFMs, `net48` on
Windows) zero failures; every pre-existing golden byte-identical with all new options off
(criterion 5); grammar stability — `generated/` has no diff; `TextRenderBenchmarks`
before/after (render path touched by the `ForIndexExtension` rewrite — allocations not
increased, mean within reported error); the W08 corpus scan; docs build after WI8.
Roadmap success criteria map: F01/F03 (1), W01–W03 (2), the trim golden pair (3),
I01–I09 + the WI8 docs section (4), the untouched-suite gate (5).

**Deferred phase 9 gallery item** (recorded per the standards): no new sample — the
`ssr-aspnetcore` and `definition-library` gallery demos are upgraded to use `@for` sugar
and `TrimDirectiveLines` so the gallery goldens exercise both end-to-end (the roadmap's
recorded phase 9 note). Owned by this phase, delivered when the phase 9 harness exists.

## Back-compat and migration

Everything is additive in 1.x; this phase contributes one change to the
[2.0 window](../common/cross-cutting-decisions.md#d2--the-single-20-breaking-window).

| Surface | Behavior | Proof / note |
| --- | --- | --- |
| All existing templates, options off | Byte-identical output; identical type-test sequence in `ForIndexExtension` | Full suite + goldens (gate); the ForModel arm stays first (D1) |
| `@for(intValue)` | Typed scopes: HED0004 error → iterates; dynamic scopes: silent empty → iterates | Error/empty → works; release note (D1's corrected framing) |
| `@for` with any other type | Still a positioned compile error, now carrying HED0004 | F07 |
| `range` in the default registry | New name; a host's own `range` registration replaces (exact signature) or overloads (different signature) — no collision failure mode | Phase 1 D12 semantics; R04 |
| Double-render warning | Warning only — zero behavior change; hosts treating warnings as errors will surface the cleanup the warning points at (the roadmap's stated intent) | W01 golden; D5's vc-test evidence that output is unchanged |
| `TrimDirectiveLines = false` (default) | Whitespace byte-exact | T-rows' off-assertions + the off-golden |
| `TrimDirectiveLines = true` | Only whitespace around eligible whole-line blocks changes | T-rows; D7's closed eligible set |
| `TemplateOptions.Equals`/`GetHashCode` | Now also include `TrimDirectiveLines` | Release note for hosts keying dictionaries on options (same class as phase 2's D6 note) |
| `TemplateResolver` | Existing ctors intact; new overload; cache keys gain a trim segment (old keys unreadable only across upgrade, which empties an in-memory cache — no persisted format exists) | D10 |
| Both import forms | Behavior frozen by characterization fixtures; docs corrected | I01–I09 |
| 2.0 window | `TrimDirectiveLines` default flips to `true`, bundled with the phase 2 profile flip and encoder swap; migration note: set it `false` to keep 1.x whitespace byte-exact | [Cross-cutting D2](../common/cross-cutting-decisions.md#d2--the-single-20-breaking-window); the golden pair pre-measures the churn |

## Performance considerations

- **Render path.** `ForIndexExtension` is the only render-path touch: the ForModel arm
  keeps today's single type test; the int arm adds one `is int` test only on the path
  that previously returned empty; the normalization allocates nothing (D1). Guarded by
  the `TextRenderBenchmarks` run in the gate. `range` executes as one registry call
  producing one `ForModel` per render of that parameter — the same object the C#-tier
  spelling allocated. Trimming and the warning do not exist at render time.
- **Compile path.** Trimming adds, per removed block, a bounded character scan over one
  line (a plain `char` loop shared by all four TFMs — the roadmap's .NET 10 review
  verified `SearchValues`/`IndexOfAnyExcept` are unavailable on `netstandard2.0`/`net6.0`
  and unwarranted here; no `#if`, per
  [cross-cutting D7](../common/cross-cutting-decisions.md#d7--net-10-stance-no-conditional-compilation-by-default)),
  plus one pass over `SkippedTokens` per body. The warning check is two boolean reads per
  compiled item. All noise against reflection/Roslyn costs; the standing compile-cost
  runners in [src/Heddle.Performance/Runners](../../../src/Heddle.Performance/Runners)
  remain available.
- **Resolver.** One conditional string concat per cache probe (compile frequency).
- No `[MethodImpl(AggressiveInlining)]` additions — nothing here is a hot tiny transform
  with a benchmark justifying it.

## Standards compliance

The [balanced-mode](../common/coding-standards.md#solid-dry-and-yagni-in-balanced-mode)
calls actually made in this phase:

- **DRY where knowledge must not diverge:** the whole-line predicate exists once
  (`WidenToWholeLine`) and serves all three trimming sites — divergent line rules between
  definitions and directives would be a user-visible inconsistency, not a style issue.
  Document surgery reuses `ApplyRemove` + the existing shift loops rather than a second
  position-arithmetic implementation (the same call phase 3 recorded).
- **YAGNI cutting scope:** no `range(last)` overload, no trim-mode enum, no prototype
  resolver ctor (a *fired* trigger evaluated and re-deferred with reasons — D10), no
  `ForModel` step guard, no `@param` trimming, no warning-suppression mechanism — each in
  [Deferred items](#deferred-items) with a trigger.
- **Open/closed at the sanctioned seams:** `range` arrives through the function registry
  (phase 1's ratified open point) and `int` support through the existing `[DataType]`
  metadata — no compiler special cases for either; trimming keys on the removal
  *mechanism*, so custom directive-style extensions participate without a new plugin
  surface (D7).
- **Correctness and honesty over a tidy gate:** D5 corrects the roadmap's zero-hit scan
  claim with evidence instead of editing the fixture that disproves it — fix-forward
  applied to the spec itself.
- **Security posture unchanged:** nothing here widens template-reachable execution; the
  one new throwing path (D3) exists to *prevent* an unbounded-output hang, and the static
  check deliberately defers to host-registered `range` implementations at the documented
  registry trust boundary.

## Deferred items

| Item | Trigger to revisit |
| --- | --- |
| `ForIndexExtension` loop guards for pre-existing pathological `ForModel` values — C#-tier `Step <= 0` (hang/empty, D3) and the unchecked-overflow non-termination when `last > int.MaxValue − step + 1` with a misaligned `step > 1` (D2) | A real-template report; guarding changes existing behavior, so it needs its own ratification |
| `range(last)` single-argument overload | User confusion reports; `@for(n)` covers the case (D2) |
| Trimming for `@param(...)` lines (block currently stays in the document) | A design that removes bodiless no-op blocks generally; would widen D7's mechanism rule |
| Per-tag trim markers beyond `@\` (Liquid-style) | Author demand for one-sided control that neither `@\` nor the option expresses (D6) |
| Prototype-options `TemplateResolver` constructor | A **third** resolver-configured option (trigger re-armed after this phase's evaluation — D10) |
| Delegating or obsoleting `@import()` | Maintainer re-ratification; the current decision is keep-both-document-difference (D11) |
| Warning-suppression/severity configuration for `HED4002`-class lints | Phase 6 tooling, where per-diagnostic severity mapping is the natural home (D5) |
| IDs for remaining untouched pre-existing diagnostics | The phase that next touches each message ([cross-cutting D1](../common/cross-cutting-decisions.md#d1--stable-diagnostic-ids-hedxxxx)) |
| Gallery upgrades (`ssr-aspnetcore`, `definition-library`) | Phase 9 harness (testing plan) |

## External references

Primary sources verified for this spec (July 2026); the roadmap's grounding set was
re-checked where load-bearing.

- [Jinja2 API — `trim_blocks` / `lstrip_blocks` / `keep_trailing_newline`](https://jinja.palletsprojects.com/en/stable/api/) — re-verified quotes: "the first newline after a block is removed"; "leading spaces and tabs are stripped from the start of a line to a block"; both default `False` (the opt-in precedent for D6/D9).
- [Jinja2 template designer documentation](https://jinja.palletsprojects.com/en/stable/templates/) — `range([start, ]stop[, step])` default global; whitespace-control behavior applies to comment tags (D2, D6).
- [Liquid — whitespace control](https://shopify.github.io/liquid/basics/whitespace/) — per-tag hyphen opt-in, "any line of Liquid … will still print a blank line" (the rejected per-tag alternative in D6).
- [Liquid — iteration tags](https://shopify.github.io/liquid/tags/iteration/) — literal-range loops (D2's precedent, carried).
- [aspnet/Razor#485](https://github.com/aspnet/Razor/issues/485) — "We've decided to be smart about removing whitespace for any non-HTML content that occupies the entire line" (the whole-line rule's design precedent, D6).
- [Razor syntax reference for ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/mvc/views/razor?view=aspnetcore-10.0) — directive/whitespace behavior context (D6).
- [Breaking change: Blazor — insignificant whitespace trimmed at compile time](https://learn.microsoft.com/en-us/dotnet/core/compatibility/aspnet-core/5.0/blazor-components-trim-insignificant-whitespace) — a mainstream engine shipping compile-time whitespace trimming as a default-flip with an opt-out — the 2.0-window shape of D9.
- [Go text/template](https://pkg.go.dev/text/template) — `{{-`/`-}}` trimming (carried from the roadmap's verified set).
- [What's new in .NET libraries for .NET 10](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/libraries) — carried verification that nothing in this phase is .NET 10-specific (plain `char` loop decision, performance section).

Internal findings are grounded in the repo sources cited throughout
[Assumed state](#assumed-state) — notably `ForIndexExtension`/`ForModel`/`DataTypeAttribute`
(the loop and attribute stacking), `HeddleCompiler` (`CheckTypes`, the removal/shift
machinery, `DefaultChains` end-of-document rendering), `ParseContext.CreateDefinition`
(the self-call override), `HeddleMainListener.ExitImport_block` vs `ImportExtension`
(the two import implementations and the D11 reachability analysis), `HeddleLexer.g4`
(`EAT_WS`, comment tokens), and `vc-test.heddle` + its golden (the D5 corpus correction).
