# Phase 6 — range-type

## Header

- **Status:** ready — Plan DoR
- **Goal (one line):** Replace the loop-model class `ForModel` with a proper, well-named immutable `Range` value type as the first-class result of `range(...)` and the model `@for` iterates, and delete `ForModel` entirely.
- **Depends on:** nothing (independent of the other phases; touches only the `range` built-in, its precompiled twin, and the `@for` extension).
- **Changes an externally-visible contract:** yes — this is a **ratified breaking change** (standing ruling [R5](common/standing-rulings.md), revised): it deletes the public type `Heddle.Models.ForModel` and changes the return type of the `range` built-in and its precompiled counterpart. Carries a migration note; lands in the 2.0 window.

## Goal

Today the `range(...)` built-in and the `@for` extension are wired to a public class `ForModel { Start?, Last, Step? }` at [src/Heddle/Models/ForModel.cs](../../src/Heddle/Models/ForModel.cs). That type reads as an internal loop-control record leaking into the public surface: it is named after the extension that consumes it, not after what it *is* (a numeric range), and it is a mutable reference class where the concept is an immutable value. The user's direction is direct — *"range needs to be its own type (C# range?) and not `ForModel`, it feels like a hardcoded crap … Range is range. ForModel just needs to map from this range"*, escalated to *"ForModel should not exist anymore, I don't care if it breaks code."*

This phase delivers a proper, immutable `Range` value type (start, last-exclusive, step) as the single first-class result of `range(...)` and the value `@for` iterates, and **deletes `ForModel`**. The user-visible outcome: `range(2, 10, 2)` yields a well-named range value instead of a `ForModel`; `@for(range(...))` and the bare-`int` form iterate exactly as before; and the public API no longer exposes a type named after a loop extension. The common templated cases — `@for(3)`, `@for(Count)`, `@for(range(2, 10, 2))` — keep working and keep rendering identical bytes; only the underlying type identity changes.

> **Provenance note.** This phase's scope is **user-added**, not drawn from [docs/language-assessment.md](../language-assessment.md). It arises from the user's direct instruction to remodel `range` as a first-class type and delete `ForModel`, and is ratified as a breaking change under the revised [R5](common/standing-rulings.md) window.

## Non-goals / scope boundary

- **The `@for` `int` form and its semantics are untouched.** `@for(n)` iterating `0…n-1`, and the `Last`-exclusive, `start >= last → empty` semantics, stay exactly as today ([ForIndexExtension.cs](../../src/Heddle/Extensions/ForIndexExtension.cs)).
- **No change to `range`'s step validation policy.** The positive-step rule is preserved verbatim on the new type: a zero/negative literal step stays compile error **HED4001**; a model-driven non-positive step still throws at render ([native-expressions.md#range](../native-expressions.md#range), [BuiltInFunctions.cs](../../src/Heddle/Runtime/Expressions/BuiltInFunctions.cs)).
- **No change to `@list`.** The element-loop extension is out of scope.
- **No rework of the function registry** or its resolution/freeze rules — only the two `range` overloads' return type changes.
- **Regions and extension parameters are out of scope** — the named-content-region (Phase 7) and extension-parameter (Phase 8) work is separate, later phases; this phase does not touch them.
- **No new `range` overloads or new numeric domains** (e.g. `long`/`decimal` ranges) — this is a type remap, not a feature expansion.

## Design direction

**Introduce a dedicated Heddle `Range` immutable value type; delete `ForModel`; remap both `range(...)` overloads and the `@for` model to it.**

The pivotal decision is the *type choice*, and the answer is a **dedicated Heddle `Range` value type carrying start / last-exclusive / step**, rather than reusing the BCL `System.Range`. The reason is concrete: `System.Range` (a `readonly struct` in `System`, holding a `Start` and `End` `Index`) has **no step** and uses **from-end** index semantics (the `^` operator), neither of which fits a counted, forward, step-controlled loop. Forcing Heddle's stepped range onto `System.Range` would mean carrying step out-of-band and constantly guarding against from-end indices — a worse contract than a purpose-built type. So Heddle owns the type. It is modeled as an **immutable value type** (mirroring `System.Range`'s own `readonly struct` shape and the concept's value semantics), which also matches how `@for` already flows a value type through its model in the `@for(int)` path — so a value-typed range introduces no new kind of boundary, only a new value on an existing one.

`@for` keeps accepting **both** the new `Range` and a bare `int`; the bare-`int` fast path is unchanged. `range(...)`'s two overloads return the new type; the precompiled twin ([PrecompiledFunctions.cs](../../src/Heddle/Precompiled/PrecompiledFunctions.cs)) and the precompiled function table ([DefaultFunctionTable.cs](../../src/Heddle/Precompiled/DefaultFunctionTable.cs)) change their declared return type in lockstep, so the dynamic and precompiled backends stay type-identical. The positive-step guard (HED4001 literal check + render-time throw) rides along on the new type — same message, same two enforcement layers.

Because the user ratified deleting `ForModel` outright ("I don't care if it breaks code"), the direction is a **hard delete, not a soft deprecation**: no transitional `ForModel` alias or `[Obsolete]` shim is kept. A shim was considered and rejected by that explicit ruling; keeping one would preserve exactly the "hardcoded crap" the user asked to remove. The trade-off — a compile break for the small set of callers/templates using `ForModel` directly — is accepted and covered by a migration note (see below), and the break is a **reviewed, deliberate golden update** (the public-API snapshot and precompiled-table goldens move), not a silent change, per the [R5 back-compat convention](common/standing-rulings.md#conventions-shared-by-all-phases).

Two smaller directions follow from the type choice. **Interop:** offer a minimal, one-directional `System.Range → Heddle Range` mapping (from-start endpoints, step 1) so a host can hand a plain `System.Range` to `@for`, while keeping the Heddle type canonical (Q2). **Rendering:** give the new type a readable `ToString()` so the standalone `@(range(...))` — today rendering the useless `Heddle.Models.ForModel` — becomes a human-readable range description, while docs still steer authors to `@for(...)` (Q3).

## Dependencies & ordering

Nothing must land first. This phase is self-contained: it edits the `range` built-in and its precompiled twin, the `@for` extension's declared model type, and deletes one model class, then updates the fixtures/goldens that pin those. It shares no code path with the other phases and unblocks nothing downstream (the later slot phases do not consume it). It can land independently, in the 2.0 breaking window.

## Back-compat / impact

This is a **breaking phase** governed by the revised [R5](common/standing-rulings.md) window and its migration-note requirement. The break is bounded and enumerable:

- **Templates using the C#-tier construction form** `@for(@new ForModel() { Last = …, Step = 3 })` — documented at [built-in-extensions.md](../built-in-extensions.md) — break at compile and must migrate to the new type / `range(...)`.
- **Host C# referencing `Heddle.Models.ForModel`** (constructing it, or handling the `range` return value / `PrecompiledFunctions.Range` result by that type) breaks and must switch to the new type.
- **Repository test fixtures and goldens** that name `ForModel` — the public-API snapshot ([public-api-heddle.txt](../../src/Heddle.Tests/TestTemplate/public-api-heddle.txt), which lists `TYPE Heddle.Models.ForModel` and the two `ForModel Range(...)` methods), the precompiled function table's declared return type, the `range` unit/lockstep tests, and the `@for`-sugar tests — are updated **as part of this change** (a reviewed diff, not incidental breakage).

**What does *not* break:** the common templated cases. `@for(3)`, `@for(Count)`, and `@for(range(2, 10, 2))` keep compiling and keep rendering **byte-identical** output — only the type flowing underneath changes. The loop mechanics (bounds, step, exclusivity, empty-on-`start >= last`) are unchanged, so the golden/differential render corpus does not move on any valid `@for`/`range` template. The **only** intentional rendered-byte change is the discouraged standalone `@(range(...))` form, whose `ToString()` improves from the useless `Heddle.Models.ForModel` to a readable range (Q3) — an explicitly-"useless" output the docs already steer away from.

**The dynamic == precompiled differential still holds.** The precompiled path's declared return type moves in lockstep with the dynamic built-in, so the two backends remain type-identical and byte-identical on every `@for`/`range` fixture.

**Migration note (to ship with the change).** `new ForModel { Start = s, Last = l, Step = st }` → `range(s, l, st)` (the blessed native-tier form); `new ForModel { Last = l }` → `range(0, l)` (or `@for(l)` for the bare count); the C#-tier `@for(@new ForModel(){ Last = …, Step = 3 })` → `@for(range(0, …, 3))` (or the new type's equivalent construction). Docs carrying `ForModel` — [native-expressions.md](../native-expressions.md), [built-in-extensions.md](../built-in-extensions.md), [language-reference.md](../language-reference.md) — are updated to the new type as a downstream doc edit, not part of this WHAT.

## Risks & mitigations

| Risk | Mitigation | Size (S/M/L) |
|---|---|---|
| Deleting a public type silently breaks an unenumerated caller. | The break surface is enumerated from a repo-wide audit (17 non-plan files reference `ForModel`); the public-API snapshot golden makes the surface change a reviewed diff; a migration note maps every old construction to the new form. | M |
| The dynamic and precompiled backends drift if only one side's return type is remapped. | The precompiled function table and precompiled `Range` methods are changed in the same phase and pinned by the dynamic == precompiled lockstep test; success criteria require that test to pass on the new type. | M |
| A value-typed range changes the rendered bytes of a valid `@for`/`range` template. | Loop mechanics are untouched; the only rendered-byte change is the discouraged standalone `@(range(...))` `ToString()`; the render/differential corpus must stay byte-identical on all `@for`/`range` fixtures as a measurable criterion. | S |
| The step-validation contract (HED4001 + render throw) regresses during the remap. | The positive-step rule is a preserved non-goal; both enforcement layers (literal HED4001, render-time throw with the shared message) are re-asserted on the new type by the existing range tests. | S |
| The name `Range` in `Heddle.Models` collides with `System.Range` when a host has both namespaces in scope. | Recorded as Q2 (name/namespace); the type stays fully qualified in generated/precompiled code, and the interop path is one-directional so the two never need to be written unqualified together. | S |

## Success criteria

Measurable, checkable statements a spec could turn into a test.

- [ ] `Heddle.Models.ForModel` no longer exists in the public API surface — it is absent from the snapshot at [public-api-heddle.txt](../../src/Heddle.Tests/TestTemplate/public-api-heddle.txt), and no reference to `ForModel` remains anywhere under `src/Heddle/**` (zero matches).
- [ ] `range(2, 10, 2)` returns the new `Range` type (not `ForModel`), and `@for(range(2, 10, 2)){{@out()}}` iterates `2, 4, 6, 8`.
- [ ] `@for(3)` renders `0,1,2` and `@for(Count)` iterates `0…Count-1` — byte-identical to today (the bare-`int` path is unchanged).
- [ ] `range(2, 10)` (two-arg) iterates `2…9` by step 1; `@for(range(5, 2))` renders empty (`start >= last`, like `@for(0)`).
- [ ] A positive **literal** step compiles; a zero or negative **literal** step is **HED4001**, positioned at the step argument.
- [ ] A model-driven non-positive step still throws `TemplateProcessingException` at render with the shared HED4001 message (both enforcement layers preserved).
- [ ] The precompiled `Range` methods ([PrecompiledFunctions.cs](../../src/Heddle/Precompiled/PrecompiledFunctions.cs)) and the precompiled function table's declared return type ([DefaultFunctionTable.cs](../../src/Heddle/Precompiled/DefaultFunctionTable.cs)) are the new `Range` type, and the dynamic == precompiled lockstep test ([DefaultFunctionLockstepTests.cs](../../src/Heddle.Tests/DefaultFunctionLockstepTests.cs)) passes.
- [ ] The dynamic == precompiled render differential passes on all `@for`/`range` fixtures with byte-identical output between backends; goldens move **only** where the type *name* surfaces (public-API/precompiled-table snapshots), never the rendered loop bytes of a valid template.
- [ ] `@for(range(2, 8, 3)){{[@out()]}}` still renders `[2][5]` (the documented start/step/last-exclusive example), unchanged.
- [ ] The new type has a readable `ToString()`: `@(range(2, 10, 2))` renders a human-readable range description, not `Heddle.Models.ForModel` (Q3 lean).
- [ ] A documented migration maps `new ForModel { Start = s, Last = l, Step = st }` to `range(s, l, st)` / the new type's construction.

## Validation scenarios

| Input | Expected outcome |
|---|---|
| `@for(range(2, 10, 2)){{@out()}}` | Renders `2468`; the value iterated is the new `Range` type. |
| `@for(range(2, 8, 3)){{[@out()]}}` | Renders `[2][5]` (start 2, step 3, last-exclusive) — byte-identical to today. |
| `@for(3){{<li>@out()</li>}}` | Renders `<li>0</li><li>1</li><li>2</li>` — bare-`int` path unchanged. |
| `@for(Count){{ … }}` with `Count = 4` | Iterates `0,1,2,3` — unchanged. |
| `@for(range(5, 2)){{x}}` | Renders empty (`start >= last`), like `@for(0)`. |
| `range(2, 10, 0)` (literal zero step) | Compile error **HED4001**, positioned at the step argument. |
| `range(2, 10, -2)` (literal negative step) | Compile error **HED4001**. |
| Model-driven `range(a, b, s)` with `s <= 0` at render | Throws `TemplateProcessingException` with the shared HED4001 message. |
| `@(range(2, 10, 2))` standalone | Renders a readable range description (Q3 lean), not `Heddle.Models.ForModel`. |
| Host C# referencing `Heddle.Models.ForModel` | Compile break (type deleted); migrates to the new type per the migration note. |
| `@for(@new ForModel(){ Last = n, Step = 3 })` (old C#-tier form) | Compile break; migrates to `@for(range(0, n, 3))`. |
| Public-API snapshot check | `Heddle.Models.ForModel` absent; the new `Range` type present; `range` overloads and `PrecompiledFunctions.Range` declare the new return type. |

## Open questions

None — resolved (see [open-questions.md](open-questions.md)). Every P6 question (Q1–Q3) is folded into the design direction, back-compat, and success criteria above (dedicated Heddle `Range` value type; `Heddle.Models.Range` with minimal in-phase `System.Range`→`Range` interop; a readable `ToString()`). Residual is spec-level (HOW) only: the exact grammar/shape realization of the new value type and the precise `ToString()` format.

## External grounding

| Claim | Source |
|---|---|
| `ForModel` is a public mutable class `{ int? Start; int Last; int? Step; }` in `Heddle.Models`. | [src/Heddle/Models/ForModel.cs](../../src/Heddle/Models/ForModel.cs) |
| The `range` built-in has two overloads (`range(int,int)`, `range(int,int,int)`) that return `ForModel`; the three-arg overload throws `TemplateProcessingException` at render for a non-positive step, sharing one message format with the literal check. | [src/Heddle/Runtime/Expressions/BuiltInFunctions.cs](../../src/Heddle/Runtime/Expressions/BuiltInFunctions.cs) |
| `@for` consumes `ForModel` **or** a plain `int` (`0…n-1`); it declares both via `[DataType]` and reads `Start ?? 0`, `Last`, `Step ?? 1`; the `int` path already flows a value type through the model. | [src/Heddle/Extensions/ForIndexExtension.cs](../../src/Heddle/Extensions/ForIndexExtension.cs) |
| `range` iterates `start…last-1` by step (default 1), `last` exclusive, `start >= last` renders empty; a zero/negative **literal** step is compile error **HED4001** (positioned at the step arg), and a render-time non-positive step throws with the same message; standalone `@(range(1,5))` renders `ForModel.ToString()` ("useless"). | [native-expressions.md#range](../native-expressions.md#range) |
| `@for` is documented with a `ForModel`-based model and the C#-tier construction form `@for(@new ForModel(){ Last = …, Step = 3 })`; `range` "builds a `ForModel`". | [built-in-extensions.md](../built-in-extensions.md) |
| The public-API snapshot pins `TYPE Heddle.Models.ForModel` and the two `Heddle.Models.ForModel Range(...)` methods — deleting the type is a reviewed golden change. | [public-api-heddle.txt](../../src/Heddle.Tests/TestTemplate/public-api-heddle.txt) |
| The precompiled path declares `range`'s return type as `Heddle.Models.ForModel` in the function table and exposes public `ForModel Range(...)` methods, kept in lockstep with the dynamic built-ins. | [DefaultFunctionTable.cs](../../src/Heddle/Precompiled/DefaultFunctionTable.cs), [PrecompiledFunctions.cs](../../src/Heddle/Precompiled/PrecompiledFunctions.cs) |
| The dynamic and precompiled function sets are asserted identical (including return type) by a lockstep test. | [DefaultFunctionLockstepTests.cs](../../src/Heddle.Tests/DefaultFunctionLockstepTests.cs) |
| **HED4001** is the allocated diagnostic id for the range non-positive-step error. | [HeddleDiagnosticIds.cs](../../src/Heddle/Data/HeddleDiagnosticIds.cs) |
| Deleting `ForModel` for a proper `Range` type is a ratified breaking change in the bounded 2.0 window, carrying a migration note and a deliberate golden update. | [common/standing-rulings.md — R5 (revised)](common/standing-rulings.md) |
| `System.Range` (the type-choice contrast) is a BCL `readonly struct` in `System` holding two `Index` values (`Start`/`End`) with from-end (`^`) semantics and **no step** — hence unfit as-is for a counted, stepped, forward loop. | .NET BCL: `System.Range` / `System.Index` (`System` namespace) |
