# Phase 6 — range-type (spec)

## Header

- **Status:** Specified — ready for implementation
- **Source plan:** [phase-6-range-type.md](../../plan/phase-6-range-type.md) (Plan DoR; P6-Q1..Q3 resolved in [open-questions.md](../../plan/open-questions.md))
- **Assumes merged:** nothing. This phase is self-contained (plan *Dependencies & ordering*): it edits the `range` built-in, its precompiled twin, the `@for` extension's declared model type, and deletes one model class. It consumes no other phase's deliverable and unblocks none. It coordinates with no sibling phase.

| Document | Purpose |
|---|---|
| this document | Specifies the whole of Phase 6: the immutable `Heddle.Models.Range` value type, the `System.Range → Range` interop, the readable `ToString()`, the deletion of `ForModel`, the dynamic/precompiled/lockstep remap, and every fixture/golden/doc migration. |

This spec is self-contained given the [common specs](../common/spec-conventions.md); it restates the minimal context it needs. Source citations give `path` + member name; load-bearing line numbers carry their anchor symbol and are marked *(verify at implementation — line numbers drift)*.

## Scope and goal

Replace the loop-model class `Heddle.Models.ForModel` with a purpose-built, immutable **`Heddle.Models.Range` value type** (start, last-exclusive, step) as the single first-class result of the `range(...)` built-in and the value `@for` iterates, and **delete `ForModel` outright**. This is a **ratified breaking change** in the still-open 2.0 window (standing ruling [R5 revised](../../plan/common/standing-rulings.md#standing-rulings-user-set)); it carries a migration note and moves two goldens as reviewed diffs.

In scope: the new value type and its members (constructors, value equality, `ToString`); a minimal one-directional `System.Range → Heddle.Models.Range` interop; remapping both `range` overloads, `PrecompiledFunctions.Range`, and the `DefaultFunctionTable` return-type constant in lockstep; rewiring `ForIndexExtension`'s `[DataType]` and field reads; deleting `ForModel`; and migrating the 17 enumerated non-plan references (production, tests, fixtures, docs).

**Out of scope** (plan *Non-goals*): the `@for(int)` fast path and its `0…n-1` semantics; `range`'s step-validation *policy* (HED4001 literal + render throw — preserved verbatim, not extended); `@list`; the function-registry resolution/freeze rules; new `range` overloads or numeric domains (`long`/`decimal`); regions (Phase 7) and extension parameters (Phase 8). This spec adds **no new `HED*` diagnostic** — the positive-step guard reuses **HED4001** (see [Diagnostics](#diagnostics--error-surface)).

## Assumed state

Re-verified against current source on branch `feature/lang_asmt`. Line anchors are *(verify at implementation)*.

| Seam | Verified state |
|---|---|
| `ForModel` shape | [`Heddle.Models.ForModel`](../../../src/Heddle/Models/ForModel.cs) is a **public mutable class** `{ int? Start; int Last; int? Step; }` (auto-props, get/set). This is the type deleted. |
| `range` built-in | [`BuiltInFunctions`](../../../src/Heddle/Runtime/Expressions/BuiltInFunctions.cs): `internal static ForModel Range(int start, int last)` (≈162) `=> new ForModel { Start = start, Last = last }`; `internal static ForModel Range(int start, int last, int step)` (≈170-176) throws `TemplateProcessingException` when `step <= 0` via `string.Format(InvariantCulture, RangeStepMessageFormat, step)` **before** constructing. `RangeStepMessageFormat` (≈158-159) is the single shared message. `CreateEntries()` binds `"range"` to both overloads by explicit `MethodInfo` (≈214-215). File imports both `using System;` and `using Heddle.Models;`. |
| Literal HED4001 site | [`NativeExpressionCompiler`](../../../src/Heddle/Runtime/Expressions/NativeExpressionCompiler.cs) (≈370-379): when `chosen.Method == RangeThreeArgMethod` and arg 3 is a literal int `step <= 0`, `Fail(call.Arguments[2].Position, HeddleDiagnosticIds.RangeStepNotPositive, string.Format(InvariantCulture, BuiltInFunctions.RangeStepMessageFormat, step))`. Scoped to the built-in `MethodInfo` by reference — a host-replaced `range` is exempt. |
| `@for` extension | [`ForIndexExtension`](../../../src/Heddle/Extensions/ForIndexExtension.cs): `[DataType(typeof(ForModel))]` then `[DataType(typeof(int))]` (≈10-11). `ProcessData`/`RenderData` do `if (scope.ModelData is ForModel model) { start = model.Start ?? 0; last = model.Last; step = model.Step ?? 1; } else if (scope.ModelData is int count) { start = 0; last = count; step = 1; } else return …;` then `for (int i = start; i < last; i += step) { probe?.TickDeadline(); … }`. File imports `using Heddle.Models;`, **no** `using System;`. |
| Precompiled twin | [`PrecompiledFunctions`](../../../src/Heddle/Precompiled/PrecompiledFunctions.cs) (≈63-64): `public static ForModel Range(int start, int last) => BuiltInFunctions.Range(start, last);` and the 3-arg twin — one-line delegations. File imports `using Heddle.Models;`, **no** `using System;`. |
| Precompiled table | [`DefaultFunctionTable`](../../../src/Heddle/Precompiled/DefaultFunctionTable.cs): `private const string ForModel = "Heddle.Models.ForModel";` (≈50); two rows `new DefaultFunctionRow("range", "Range", new[] { Int, Int }, ForModel)` / `{ Int, Int, Int }, ForModel)` (≈88-89). 35 rows total; compiled into **both** `Heddle` and `Heddle.Generator`. `PrecompiledFunctionBinding` records only `Name`/`TargetTypeName`/`OverloadCount` — **not** per-overload return type — so no generator/manifest golden surfaces the return type string. |
| Lockstep gate | [`DefaultFunctionLockstepTests`](../../../src/Heddle.Tests/DefaultFunctionLockstepTests.cs): `Signature(name, params, returnType)` includes the return type (≈20-21). `TableEqualsRegistryBothWays` asserts 35 rows and both-way set equality (≈42-51). `ShimHasExactlyOneMethodPerRow` binds each shim method by param types and asserts `method.ReturnType.FullName == row.ReturnTypeName` (≈53-65). `ShimHasNoExtraOverloads` counts declared public statics (≈68-74). `ResolveType` fallback is `Type.GetType(fullName) ?? typeof(ForModel).Assembly.GetType(fullName)` (≈24) — the `typeof(ForModel)` reference must move. File imports `using System;` **and** `using Heddle.Models;`. The three assertions stay green iff the table const and the shim signatures move **together**. |
| `FunctionRegistry.EnumerateOverloads` | [`FunctionRegistry`](../../../src/Heddle/Runtime/Expressions/FunctionRegistry.cs) (≈164-171) yields `(Name, Method, ParameterTypes, ReturnType)`; the lockstep registry side reads `o.ReturnType.FullName`. `FunctionEntry.FromMethod` captures `method.ReturnType` ([`FunctionEntry`](../../../src/Heddle/Runtime/Expressions/FunctionEntry.cs) ≈50). So changing the two `BuiltInFunctions.Range` return types automatically moves the registry-side signature. Doc comment (≈23) names `Heddle.Models.ForModel`. |
| Public-API snapshot | [`PublicApiSurfaceTests.DumpPublicSurface`](../../../src/Heddle.Tests/PublicApiSurfaceTests.cs) emits `TYPE <full>` + ` : <BaseType>` when non-`object`, ` impl <ifaces>` **only for `Heddle`-namespaced interfaces**, then sorted `CTOR/METHOD/PROP/FIELD` lines (`DeclaredOnly`, non-special-name methods only — operators and property accessors are `IsSpecialName` and excluded). Golden pinned byte-exact **on net8.0+** (older TFMs get the lighter non-empty check). Golden [`public-api-heddle.txt`](../../../src/Heddle.Tests/TestTemplate/public-api-heddle.txt): `TYPE Heddle.Models.ForModel` + `CTOR()` + 3 props (≈621-625); `TYPE Heddle.Precompiled.PrecompiledFunctions` with two `METHOD Heddle.Models.ForModel Range(...)` (≈664-666). `PrecompiledExtensionBinding` shows the value-type precedent `: System.ValueType` (≈638). |
| `template.heddle` fixture | [`template.heddle`](../../../src/Heddle.Tests/TestTemplate/template.heddle) line 52: `@for(@new ForModel() { Last = model.Products.Count(), Step = 3 })` — a **C#-tier** construction (uses LINQ `.Count()`). It has `@using(){{Heddle.Models}}` (≈31) and `@using(){{System.Linq}}` (≈30). Its golden is [`generated.html`](../../../src/Heddle.Tests/TestTemplate/generated.html), asserted byte-exact by `HeddleTemplateTests` (`reader = File.OpenText("TestTemplate/generated.html"); Assert.Equal(expected, actual)`, ≈775-784). |
| Allocation guard | [`ForSugarAllocationTests`](../../../src/Heddle.Tests/ForSugarAllocationTests.cs): `IntForLoopAllocatesNoMoreThanTheForModelPath` compares the int-`@for` path against the C#-tier `@new ForModel(){ Last = model.Count }` path over 200 renders, asserting `intAlloc <= modelAlloc + 4096` via `GC.GetAllocatedBytesForCurrentThread()`. The model-arm template + doc comment name `ForModel`. |
| Other test references | [`ForSugarTests`](../../../src/Heddle.Tests/ForSugarTests.cs) F10 (`@new ForModel() { Last = 2 }`, ≈101); [`RangeFunctionTests`](../../../src/Heddle.Tests/RangeFunctionTests.cs) `HostRange` returns `ForModel` (≈23-24) and `RangeJoinsDefaultRegistryWithBothOverloads` comment (≈83); [`BranchIsolationTests`](../../../src/Heddle.Tests/BranchIsolationTests.cs) `ForHolder { public ForModel Range { get; set; } }` (≈21) and `new ForModel { Last = 2 }` (≈105). No test asserts the literal string `"Heddle.Models.ForModel"` as rendered output (grep-verified). |
| Diagnostic id | [`HeddleDiagnosticIds`](../../../src/Heddle/Data/HeddleDiagnosticIds.cs): `RangeStepNotPositive = "HED4001"` (≈107); doc comment on `ReturnTypeMismatch` (HED0004, ≈22-24) names `ForModel`. |
| Docs referencing `ForModel` | [native-expressions.md](../../native-expressions.md#range) (`range` table row + the `range` paragraph, ≈138-152); [built-in-extensions.md](../../built-in-extensions.md) (the inner-`@` FullCSharp callout ≈14, the `for` section ≈202-235, and the `for` capability-table row ≈571); [language-reference.md](../../language-reference.md) (embedded-C# example ≈471). These three **published author docs** are the doc migration surface. The **spec-set** docs that *document* this deletion — [post-2.0/README.md](README.md), [common/cross-cutting.md](common/cross-cutting.md), this spec, the four `docs/plan/**` files, and the [records.md](../records.md) ledger row appended in WI10 — legitimately contain `ForModel` and are **not** migration targets (excluded from the WI9 grep; see [Back-compat](#back-compat-and-migration)). |
| `System.Range` availability | `System.Range`/`System.Index` are BCL types added in .NET Core 3.0 / .NET Standard 2.1. **They do not exist on `netstandard2.0`.** The `Heddle` library targets `netstandard2.0;net6.0;net8.0;net10.0` — so any member typed in `System.Range` compiles only on the non-`netstandard2.0` TFMs and needs an `#if` guard. `System.HashCode` is likewise `netstandard2.1+` — unavailable on `netstandard2.0`. |

## Design decisions

Every P6 plan question, lean, and pin appears here as a **closed** decision. No open questions remain.

### D1 — A dedicated Heddle `Range` immutable value type, not `System.Range` (closes P6-Q1)
- **Decision.** Introduce `public readonly struct Range` in namespace `Heddle.Models`, carrying three **non-nullable** `int` properties — `Start`, `Last` (exclusive), `Step` — with construction-time defaults (Start `0`, Step `1`). It is the sole result type of the `range(...)` built-in and the value type `@for` iterates.
- **Rationale.** The concept is a counted, forward, step-controlled range — a value, not a mutable loop-control record. A `readonly struct` gives value semantics and matches how `@for` already flows a value type (`int`) through its model, introducing no new boundary kind. `System.Range` is unfit: it is `{ Index Start; Index End; }` with **no step** and **from-end (`^`) semantics**, and it does not even exist on `netstandard2.0` (Assumed state) — forcing Heddle's stepped range onto it would carry step out-of-band and constantly guard from-end indices, a strictly worse contract. The user ratified deleting `ForModel` outright ("I don't care if it breaks code").
- **Non-nullable fields (reconciling `ForModel`'s `Start?`/`Step?`).** `ForModel` used `int? Start`/`int? Step` so `@for` supplied defaults via `Start ?? 0` / `Step ?? 1`. The new type bakes those defaults into its **constructors** instead, so the fields are plain `int` and `@for` reads them directly (`model.Start`, `model.Last`, `model.Step` — no `??`). This removes the optionality that made the type read as loop-control leakage. The `range(...)` built-in always supplies an explicit start (both overloads take `start`), so `Start`'s default only surfaces on direct/`default` construction.
- **Alternatives rejected.** *Reuse `System.Range`* — no step, from-end semantics, absent on `netstandard2.0` (P6-Q1 grounding). *Keep `ForModel` with an `[Obsolete]` alias / soft deprecation* — explicitly rejected by the user's ratified "delete it"; a shim would preserve the exact "hardcoded crap" being removed and add a permanent public alias. *Nullable `int?` fields on the new type* — reproduces the leaky optionality the remodel exists to remove and forces `@for` to keep coalescing.
- **Grounding.** [ForModel.cs](../../../src/Heddle/Models/ForModel.cs); [ForIndexExtension.cs](../../../src/Heddle/Extensions/ForIndexExtension.cs); plan *Design direction*; [R5 revised](../../plan/common/standing-rulings.md#standing-rulings-user-set).

### D2 — Final shape of `Heddle.Models.Range` (constructors, equality, hashing, `netstandard2.0`)
- **Decision.** The type is:

  ```csharp
  namespace Heddle.Models
  {
      public readonly struct Range : System.IEquatable<Range>
      {
          public Range(int start, int last) : this(start, last, 1) { }
          public Range(int start, int last, int step) { Start = start; Last = last; Step = step; }

          public int Start { get; }   // inclusive lower bound
          public int Last  { get; }   // exclusive upper bound
          public int Step  { get; }   // positive increment (default 1)

          public bool Equals(Range other) => Start == other.Start && Last == other.Last && Step == other.Step;
          public override bool Equals(object obj) => obj is Range other && Equals(other);
          public override int GetHashCode() { unchecked { int h = Start; h = (h * 397) ^ Last; h = (h * 397) ^ Step; return h; } }
          public static bool operator ==(Range left, Range right) => left.Equals(right);
          public static bool operator !=(Range left, Range right) => !left.Equals(right);

          public override string ToString() { /* D4 format */ }

          // D3 interop — non-netstandard2.0 TFMs only
      }
  }
  ```
- **Rationale.** Value equality over the three fields is the natural contract for an immutable value; `IEquatable<Range>` avoids boxing in equality-heavy uses. The **manual** hash (`* 397` fold) is used deliberately because `System.HashCode.Combine` is unavailable on `netstandard2.0`; the fold compiles and behaves identically on every TFM and needs no `#if` (honoring [D7](../common/cross-cutting-decisions.md#d7--net-10-stance-no-conditional-compilation-by-default) — no conditional compilation for equality). `readonly struct` + auto-get-only props are `netstandard2.0`-legal under `LangVersion latest`. Block-scoped namespace and Allman braces match repo style ([coding standards](../common/coding-standards.md#repository-style-inferred-preserved)).
- **No constructor step validation.** The 3-arg constructor does **not** throw on `step <= 0`. The positive-step contract is preserved **exactly where it lives today** (D6): the literal HED4001 check in `NativeExpressionCompiler` and the render-time throw in `BuiltInFunctions.Range(int,int,int)`. `ForModel` never validated in its members either (a host could already build `new ForModel { Step = 0 }`), and a non-positive step feeding `@for` is bounded by the per-iteration `IBudgetProbe.TickDeadline()` (Assumed state). Adding constructor validation would be **new** behavior beyond the preserved policy and is explicitly a plan non-goal; it is listed as a [deferred item](#deferred-items) with a revisit trigger.
- **Alternatives rejected.** *`System.HashCode.Combine`* — not on `netstandard2.0`; would force an `#if`. *`record struct`* — synthesizes members but its generated `ToString` (`Range { Start = …, … }`) is not the readable form D4 requires, and it would still need a hand-written `ToString` override plus the interop; a plain `readonly struct` is clearer here. *Throwing constructor* — expands the step-validation policy (non-goal) and diverges from `ForModel`'s member behavior.
- **Grounding.** [coding standards — target frameworks](../common/coding-standards.md#language-and-target-frameworks); Assumed state (`System.HashCode`/`System.Range` `netstandard2.0` gap); [ForIndexExtension.cs](../../../src/Heddle/Extensions/ForIndexExtension.cs) (`TickDeadline`).

### D3 — Minimal one-directional `System.Range → Heddle.Models.Range` interop (closes P6-Q2)
- **Decision.** Ship a single public **static factory** on the `Range` type, guarded to the TFMs where `System.Range` exists:

  ```csharp
  #if !NETSTANDARD2_0
  /// <summary>Maps a from-start <see cref="System.Range"/> to a step-1 Heddle range.</summary>
  public static Range FromSystemRange(System.Range range)
  {
      if (range.Start.IsFromEnd || range.End.IsFromEnd)
          throw new System.ArgumentException(
              "A from-end index ('^') has no meaning for a counted Heddle range; supply from-start endpoints.",
              nameof(range));
      return new Range(range.Start.Value, range.End.Value, 1);
  }
  #endif
  ```
  The type name stays **canonical and fully qualified** (`Heddle.Models.Range`) in all generated and precompiled code and at every migrated call site (D5).
- **Rationale.** The interop is tiny and public-API, so per P6-Q2 it belongs **on the type it converts to** (no separate converter class, no deferral). A **named static factory** (not an implicit/explicit operator) is chosen because (a) the conversion is partial — it drops from-end capability and forces step 1 — and Framework Design Guidelines say conversion **operators must not throw**, whereas a factory legitimately may; (b) a named method is discoverable and, being non-special-name, is **pinned by the public-API snapshot** (operators are `IsSpecialName` and would escape it); (c) `throw` on a from-end index is a **host-programming error**, which the [coding standards](../common/coding-standards.md#error-handling-and-diagnostics) say throws (`ArgumentException`) rather than degrading. The `#if !NETSTANDARD2_0` guard is mandatory because `System.Range` is absent on `netstandard2.0` (Assumed state); this is the sanctioned `#if` case the standards call out, and the resulting per-TFM surface difference is already precedented (the snapshot golden is captured on net8.0+, `ScopeRenderer.TotalLength` is likewise TFM-conditional).
- **Alternatives rejected.** *`implicit`/`explicit operator`* — an operator that throws violates FDG and would not appear in the public-API snapshot. *A separate `RangeInterop`/extension class* — over-abstraction (YAGNI); one converter, one type, put it on the type. *Bidirectional interop (`Range → System.Range`)* — out of scope (P6-Q2 is one-directional; `System.Range` cannot represent a step, so the reverse is lossy and unneeded).
- **Grounding.** [P6-Q2](../../plan/open-questions.md#p6--range-type-phase-6); [FDG — conversion operators](https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/operator-overloads); Assumed state (`netstandard2.0` gap; snapshot captured net8.0+).

### D4 — Readable `ToString()` format (closes P6-Q3; the sole intentional rendered-byte change class)
- **Decision.** `ToString()` mirrors the `range(...)` call form, invariant-culture, omitting a step of 1:

  ```csharp
  public override string ToString()
  {
      var ci = System.Globalization.CultureInfo.InvariantCulture;
      return Step == 1
          ? string.Format(ci, "range({0}, {1})", Start, Last)
          : string.Format(ci, "range({0}, {1}, {2})", Start, Last, Step);
  }
  ```
  **Exact strings** (the required cases):
  | Value | `ToString()` |
  |---|---|
  | `range(2, 10, 2)` → `new Range(2, 10, 2)` | `range(2, 10, 2)` |
  | `range(2, 10)` → `new Range(2, 10)` (step 1) | `range(2, 10)` |
  | an empty range, e.g. `range(5, 2)` → `new Range(5, 2)` | `range(5, 2)` |
- **Rationale.** The call-mirroring form is self-documenting and round-trips to the syntax the author would write, a strict improvement over today's useless default `"Heddle.Models.ForModel"` (Object's type-name `ToString`). Invariant culture matches every other built-in (`BuiltInFunctions` uses `CultureInfo.InvariantCulture` throughout). Empty ranges render by the same rule with no special marker — uniform, deterministic, and the standalone `@(range(...))` form is discouraged anyway (docs still steer to `@for`).
- **Scope of the byte change — one `ToString` change class, at every stringification site.** The improved `ToString()` is the phase's **only** intentional rendered-byte change, but it is *not* confined to standalone `@(range(...))`. Any site that stringifies the value observes it, because `Heddle.Models.Range` (like the deleted `ForModel`) is **not** `IFormattable`: `BuiltInFunctions.Str` renders `Convert.ToString(value, InvariantCulture)` ([BuiltInFunctions.cs](../../../src/Heddle/Runtime/Expressions/BuiltInFunctions.cs) ≈115-116) and `BuiltInFunctions.Format(object, string)` falls through to `Str` for non-`IFormattable` values (≈82-99). So `@(str(range(...)))` and `@(format(range(...), …))` in the Native tier, and FullCSharp string concatenation/interpolation of a range, **all** previously rendered `Heddle.Models.ForModel` and now render the readable `range(...)` form. This is one benign, additive-quality change class: no test pins the old useless string at *any* of these sites (Assumed state — grep-verified: no test asserts the literal `"Heddle.Models.ForModel"` as rendered output), so nothing but the new behavior depends on it. It is enumerated in [Back-compat](#back-compat-and-migration) and pinned by a `str(range(...))` fixture row ([WI8](#wi8--new-differential-fixture-for-forrange)).
- **Alternatives rejected.** *`[2, 10) step 2`* / *`2..10 step 2`* — readable but does not mirror the author's syntax and invents notation. *Appending `(empty)`* — extra branch, breaks the round-trip mirror, and the discouraged standalone form does not warrant it. *Always emitting the third arg (`range(2, 10, 1)`)* — noisier for the common step-1 case.
- **Grounding.** [P6-Q3](../../plan/open-questions.md#p6--range-type-phase-6); [native-expressions.md#range](../../native-expressions.md#range) ("renders `ForModel.ToString()` (useless)"); [BuiltInFunctions.cs](../../../src/Heddle/Runtime/Expressions/BuiltInFunctions.cs) (invariant-culture convention).

### D5 — Collision handling: the Heddle type is always fully qualified
- **Decision.** Every reference to the new type — in production code, generated/precompiled code, tests, and fixtures — is written **`Heddle.Models.Range`** (fully qualified), never a bare `Range`, in any file that also imports (or may import) `System`.
- **Rationale.** On net6.0/net8.0/net10.0 a file with both `using System;` and `using Heddle.Models;` makes bare `Range` **ambiguous** with `System.Range` (a real compile error — e.g. `DefaultFunctionLockstepTests`, `RangeFunctionTests`, and `BuiltInFunctions` all import both). Fully qualifying is the deterministic, TFM-independent fix and satisfies the plan's Q2 directive to "keep the Heddle type canonical/fully-qualified in generated+precompiled code." Method/const identifiers named `Range` are unaffected (only the *type* collides). Where a file's `using Heddle.Models;` becomes unused after qualification, it is removed only if nothing else in the file needs it (minimal-diff rule).
- **Alternatives rejected.** *`using HRange = Heddle.Models.Range;` aliases* — extra ceremony per file; fully qualifying is simpler and matches the generated-code rule. *Relying on `using` order / no `System` import* — fragile; a later `using System;` addition would silently break the build.
- **Grounding.** Assumed state (import lists per file); plan risk table (name collision, size S); plan *Design direction* (Q2 fully-qualified).

### D6 — HED4001 preserved on the new type; no new diagnostic id
- **Decision.** The positive-step contract rides onto `Range` **unchanged, at both existing layers**, and **no new `HED*` id is allocated — HED4001 is reused**:
  1. **Compile (literal):** the `NativeExpressionCompiler` check (scoped to the built-in three-arg `MethodInfo` by reference) still raises **HED4001** positioned at the step argument, with `BuiltInFunctions.RangeStepMessageFormat`.
  2. **Render (model-driven):** `BuiltInFunctions.Range(int, int, int)` still throws `TemplateProcessingException` with the same shared message **before** constructing the `Range`.
  The `RangeStepMessageFormat` constant is untouched; both layers keep sharing it verbatim.
- **Rationale.** The step-validation policy is a preserved plan non-goal; the change is a type remap, not a policy change. Reusing HED4001 keeps the registry stable (an id once shipped is never reused/renumbered — [D1](../common/cross-cutting-decisions.md#d1--stable-diagnostic-ids-hedxxxx)). Keeping the guard in `BuiltInFunctions.Range` (not the constructor, per D2) means the throw fires only through the built-in, leaving a host-replaced `range` governing its own rules (existing `RangeFunctionTests.R04` behavior).
- **Alternatives rejected.** *A new id for the render throw* — the render throw already intentionally carries **no id** (it is a `TemplateProcessingException`, not a compile diagnostic); adding one is scope creep. *Moving the guard into the constructor* — see D2 (expands policy, diverges from `ForModel`).
- **Grounding.** [NativeExpressionCompiler.cs](../../../src/Heddle/Runtime/Expressions/NativeExpressionCompiler.cs) (≈370-379); [BuiltInFunctions.cs](../../../src/Heddle/Runtime/Expressions/BuiltInFunctions.cs) (≈158-176); [HeddleDiagnosticIds.cs](../../../src/Heddle/Data/HeddleDiagnosticIds.cs) (≈104-107); plan *Non-goals*.

### D7 — Lockstep remap: both backends move together
- **Decision.** Move the return type on all four coupled sites **in one change** so the lockstep gate stays green:
  1. `BuiltInFunctions.Range(int, int)` and `Range(int, int, int)` → return `Heddle.Models.Range`.
  2. `PrecompiledFunctions.Range(int, int)` and `Range(int, int, int)` → return `Heddle.Models.Range` (still one-line delegations).
  3. `DefaultFunctionTable` const `ForModel = "Heddle.Models.ForModel"` → renamed `Range = "Heddle.Models.Range"`; both `range` rows reference it.
  4. `DefaultFunctionLockstepTests.ResolveType` fallback `typeof(ForModel)` → `typeof(Heddle.Models.Range)`.
  The registry side updates automatically because `FunctionEntry.FromMethod` captures `method.ReturnType` and `EnumerateOverloads` yields it (Assumed state) — so once (1) lands, `RegistrySignatures()` reports `Heddle.Models.Range`, matching the table.
- **Rationale.** `TableEqualsRegistryBothWays` and `ShimHasExactlyOneMethodPerRow` compare **return type included** in the signature; they pass iff the dynamic built-in, the shim, and the table all name the same type. Moving them together keeps the dynamic == precompiled type-identity the plan requires. `ShimHasNoExtraOverloads` and the 35-row count are unaffected (overload *count* unchanged).
- **Alternatives rejected.** *Moving one side first* — a red lockstep build (the gate's purpose). *Changing the row count* — no overloads added/removed.
- **Grounding.** [DefaultFunctionLockstepTests.cs](../../../src/Heddle.Tests/DefaultFunctionLockstepTests.cs); [FunctionEntry.cs](../../../src/Heddle/Runtime/Expressions/FunctionEntry.cs); [DefaultFunctionTable.cs](../../../src/Heddle/Precompiled/DefaultFunctionTable.cs).

### D8 — `@for` reads the value type directly
- **Decision.** `ForIndexExtension` declares `[DataType(typeof(Heddle.Models.Range))]` (first) then `[DataType(typeof(int))]` (unchanged). Both `ProcessData` and `RenderData` change their first arm to:

  ```csharp
  if (scope.ModelData is Heddle.Models.Range model)
  {
      start = model.Start; last = model.Last; step = model.Step;   // no ?? — non-nullable fields
  }
  else if (scope.ModelData is int count) { start = 0; last = count; step = 1; }
  else { return string.Empty; /* or return; in RenderData */ }
  ```
  The loop body, the `IBudgetProbe` probe, and the `int` fast path are untouched.
- **Rationale.** A boxed `Range` in `scope.ModelData` (an `object`) is unboxed once per render by the `is` pattern into a local `Range model`; the field reads are direct. The `Range` arm stays **first** so the type-test order (and the allocation-guard test's premise) is preserved. `[DataType(typeof(Range))]` type-checks via `Type.IsInstanceOfType`/assignability (`DataTypeAttribute` stores a `Type`; value types are valid), so `range(...)`'s `Heddle.Models.Range` result is assignable and a non-`Range`/non-`int` value is still a positioned **HED0004** (`ForSugarTests.F07` unchanged).
- **Alternatives rejected.** *Keeping `?? 0`/`?? 1`* — the new fields are non-nullable (D1); coalescing is dead code. *Reordering the arms* — would change the type-test sequence the allocation test pins.
- **Grounding.** [ForIndexExtension.cs](../../../src/Heddle/Extensions/ForIndexExtension.cs); [DataTypeAttribute.cs](../../../src/Heddle/Attributes/DataTypeAttribute.cs); [ForSugarTests.cs](../../../src/Heddle.Tests/ForSugarTests.cs) F07.

### D9 — Both the `ForModel` deletion and the D4 rendered-byte change land in the still-open 2.0 breaking window
- **Decision.** Two breaking effects of this phase are recorded together as new items in the **still-open 2.0 breaking window** ([D2](../common/cross-cutting-decisions.md#d2--breaking-changes-land-only-in-ratified-breaking-windows), [breaking-windows.md](../common/breaking-windows.md)): **(a)** deleting the public type `Heddle.Models.ForModel` (an API/type/compile break), and **(b)** the [D4](#d4--readable-tostring-format-closes-p6-q3-the-sole-intentional-rendered-byte-change-class) rendered-byte change (the improved `Heddle.Models.Range.ToString()`, observed at every stringification site — standalone `@(range(...))`, `str`/`format`, FullCSharp interpolation). Membership is recorded by **this decision record** (the owning record the window policy requires), and in the **same change** the implementer appends one **amendments-ledger** entry to [records.md — cross-spec amendments ledger](../records.md#cross-spec-amendments-ledger) naming both effects and pointing at this spec (see [WI10](#wi10--breaking-window-bookkeeping--migration-note)). The migration note ([Back-compat and migration](#back-compat-and-migration)) is the window's per-item deliverable, folded into the release CHANGELOG.
- **Maintainer ruling (2026-07-15 — closed).** The maintainer ruled the 2.0 breaking window **OPEN** for this phase and confirmed **both** effects (a) and (b) land in 2.0 under R5 revised: 2.0 is imminent/unreleased, and a fundamentally-better breaking change — including a bounded rendered-byte improvement — may still be added to the open window. This closes the only governance question the phase raised (whether the D4 byte change is licensed in the open 2.0 window); **no open governance item remains**.
- **Rationale (window open at code level).** The repository is tagged only `v1.0.0` and `v1.0.1` — **no `2.0.0` tag exists** (verified: `git tag` lists `v1.0.0`, `v1.0.1`; `git describe` → `v1.0.1-14-g702a26d0`). 2.0 is therefore unreleased and the window is open for **both** break classes above. The D4 byte change is narrowly bounded: it touches only the stringification of a range value, which today renders the useless `Heddle.Models.ForModel`; **no golden moves for it** (no test pins the old string at any site — grep-verified, Assumed state / [D4](#d4--readable-tostring-format-closes-p6-q3-the-sole-intentional-rendered-byte-change-class)) and the discouraged standalone/stringified form was always steered away from in favour of `@for`. It is a strict readability improvement carried under a migration note — exactly the "fundamentally improves the engine or syntax" case R5 revised opens the window for.
- **records.md reconciliation (owned elsewhere — already reconciled to window-OPEN).** [records.md](../records.md) now records the 2.0 window as **open**: its as-shipped header states the `v2.0.0` tag is not yet cut and "the 2.0 major breaking window remains **open**" (records.md ≈L16-22), and the encoder default-swap is deferred to the next-window register on an **unmet Encoder-pin precondition** — explicitly *not* on any window closure (≈L55-60). This reconciliation (removing the earlier premature "window closed" framing) was performed by the **common-docs judge** under the 2026-07-15 maintainer ruling — a separate agent — **not** by this spec, and it is consistent with this D9's open-window premise. This spec therefore **does not depend on and does not edit** records.md; it records its own window membership solely through the sanctioned append-only amendments ledger. The `records.md` as-shipped record table is **append-only and must not be edited**; the ledger append is the sanctioned post-hoc mechanism (append-in-the-same-change).
- **Alternatives rejected.** *Deferring D4's `ToString` to a "3.0" register (as the encoder default-swap was)* — rejected by the 2026-07-15 ruling: unlike the encoder swap (which changes bytes for valid, relied-upon, common templates and moves goldens), D4 changes only useless/discouraged stringification that no golden pins, so it is re-licensed inside the open 2.0 window rather than deferred. *Editing the 2.0 as-shipped record table* — forbidden (append-only). *Opening a new "3.0" window for the deletion* — the 2.0 window is not tagged/closed; R5 revised places this item in the 2.0 window explicitly. *A next-window-register row in `breaking-windows.md`* — that register is for the **next** window's candidates, not the open one; this item is ratified for the current window, so it is recorded as an owning decision + ledger entry.
- **Grounding.** Maintainer ruling 2026-07-15 (window open; both effects land in 2.0); `git tag` / `git describe` (only `v1.0.1`); [R5 revised](../../plan/common/standing-rulings.md#standing-rulings-user-set); [breaking-windows.md — policy](../common/breaking-windows.md#policy-applies-to-every-window); [cross-spec amendments ledger](../common/cross-cutting-decisions.md#cross-spec-amendments-ledger); [P5-Q1](../../plan/open-questions.md#p5--docs--benchmark-credibility-phase-5); [D4](#d4--readable-tostring-format-closes-p6-q3-the-sole-intentional-rendered-byte-change-class) (byte-change scope); Assumed state (no test pins the old string).

## Implementation plan

Ordered so the build/lockstep stays green at each committed step. TDD verdict per [Testing plan](#testing-plan): **test-with** for the type remap (existing tests are updated in the same commit as the code they pin — a deletion cannot be red-first), **test-first** for the two genuinely new behaviors (`ToString` and the `FromSystemRange` interop).

### WI1 — Add `Heddle.Models.Range`
- **Files.** Add `src/Heddle/Models/Range.cs`.
- **Change.** The `readonly struct Range : System.IEquatable<Range>` of D2 + the D4 `ToString` + the D3 `#if !NETSTANDARD2_0` `FromSystemRange`. Block-scoped namespace, Allman braces, XML `<summary>` on the type and public members. No `using System;` at file top needed if `System.IEquatable`/`System.Range`/`System.Globalization.CultureInfo` are written qualified (avoids any bare-`Range` question); or import `System` and write the struct name unqualified within its own file (self-reference is unambiguous). Keep the interop last, behind the guard.
- **Done when.** `Heddle` compiles on all four TFMs (interop absent on `netstandard2.0`); new unit tests for equality, `ToString` (D4 exact strings), and `FromSystemRange` (incl. the from-end `ArgumentException`) are green.

### WI2 — Remap the dynamic built-in
- **Files.** `src/Heddle/Runtime/Expressions/BuiltInFunctions.cs`.
- **Change.** Both `Range` overloads return `Heddle.Models.Range`: `internal static Heddle.Models.Range Range(int start, int last) => new Heddle.Models.Range(start, last);` and the 3-arg keeps the `step <= 0` guard + shared message, then `return new Heddle.Models.Range(start, last, step);`. Fully qualify (D5, file imports `System`). Remove `using Heddle.Models;` if now unused.
- **Done when.** `Heddle` compiles; `RangeFunctionTests` R01–R04 pass (HED4001 literal, sign-prefixed literal, model-driven render throw, host-replaced exemption).

### WI3 — Remap the precompiled shim + table + lockstep resolver
- **Files.** `src/Heddle/Precompiled/PrecompiledFunctions.cs`, `src/Heddle/Precompiled/DefaultFunctionTable.cs`, `src/Heddle.Tests/DefaultFunctionLockstepTests.cs`.
- **Change.** Shim: both `Range` methods return `Heddle.Models.Range` (one-line delegations). Table: rename const `ForModel` → `Range` with value `"Heddle.Models.Range"`; both `range` rows reference it. Lockstep test: `ResolveType` fallback `typeof(ForModel)` → `typeof(Heddle.Models.Range)`.
- **Done when.** `DefaultFunctionLockstepTests` (all four facts) green; row count still 35; dynamic and table return types agree on `Heddle.Models.Range`.

### WI4 — Rewire `@for`
- **Files.** `src/Heddle/Extensions/ForIndexExtension.cs`.
- **Change.** Per D8: `[DataType(typeof(Heddle.Models.Range))]` first; both methods' first arm reads `model.Start/Last/Step` directly (drop `??`).
- **Done when.** `ForSugarTests` F01–F10 and `BranchIsolationTests` E7 pass (after WI7 fixture edits); `@for(range(...))` iterates correctly.

### WI5 — Delete `ForModel` and scrub production doc-comments
- **Files.** Delete `src/Heddle/Models/ForModel.cs`. Edit `src/Heddle/Runtime/Expressions/FunctionRegistry.cs` (≈23 `<see cref>` → `Heddle.Models.Range`), `src/Heddle/Data/HeddleDiagnosticIds.cs` (≈24 doc comment `ForModel` → `Range`).
- **Done when.** `grep -r ForModel src/Heddle/**` returns **zero** matches (success criterion 1); `Heddle` compiles.

### WI6 — Regenerate the public-API golden (reviewed diff)
- **Files.** `src/Heddle.Tests/TestTemplate/public-api-heddle.txt`.
- **Change.** Remove the `TYPE Heddle.Models.ForModel` block (CTOR + 3 props). Add, in ordinal position, the new value-type block (net8.0+ capture — the golden's pinned TFM):
  ```
  TYPE Heddle.Models.Range : System.ValueType
    CTOR(System.Int32, System.Int32)
    CTOR(System.Int32, System.Int32, System.Int32)
    METHOD Heddle.Models.Range FromSystemRange(System.Range)
    METHOD System.Boolean Equals(Heddle.Models.Range)
    METHOD System.Boolean Equals(System.Object)
    METHOD System.Int32 GetHashCode()
    METHOD System.String ToString()
    PROP System.Int32 Last {get;}
    PROP System.Int32 Start {get;}
    PROP System.Int32 Step {get;}
  ```
  (No ` impl` clause: `IEquatable<Range>` is BCL, not `Heddle`-namespaced, so `DumpPublicSurface` omits it. `op_Equality`/`op_Inequality` are `IsSpecialName` and excluded.) Change the two `PrecompiledFunctions` lines to `METHOD Heddle.Models.Range Range(System.Int32, System.Int32)` and `…Range(System.Int32, System.Int32, System.Int32)`. Re-sort member lines ordinally; let the test's `.actual` output drive the exact bytes.
- **Done when.** `PublicApiSurfaceTests.HeddlePublicSurfaceMatchesGolden` green on net8.0+; the diff is exactly the ForModel→Range surface move.

### WI7 — Migrate test fixtures and templates (byte-identity preserved)
- **Files.** `src/Heddle.Tests/ForSugarTests.cs`, `src/Heddle.Tests/ForSugarAllocationTests.cs`, `src/Heddle.Tests/RangeFunctionTests.cs`, `src/Heddle.Tests/BranchIsolationTests.cs`, `src/Heddle.Tests/TestTemplate/template.heddle`.
- **Change.**
  - `ForSugarTests` F10: `@new ForModel() { Last = 2 }` → `@new Heddle.Models.Range(0, 2)` (still C#-tier; keep `@using(){{Heddle.Models}}` or rely on the fully-qualified name). Output `<i>0</i><i>1</i>` unchanged. Update the class doc comment `ForModel` → `Range`.
  - `ForSugarAllocationTests`: model-arm template `@new ForModel(){ Last = model.Count }` → `@new Heddle.Models.Range(0, model.Count)`; rename the test `IntForLoopAllocatesNoMoreThanTheForModelPath` → `…ThanTheRangeModelPath`; and replace **every** remaining `ForModel` token in the file — the `<see cref="Heddle.Models.ForModel"/>` doc comment, the method-name mentions, the `Assert.True` **failure-message string** (≈L58), and the inline comments (≈L47/L55/L56) — with `Heddle.Models.Range` / the renamed method (these test-side strings are outside the WI5 `src/Heddle/**` production zero-grep, so they do not affect that success criterion, but the whole file is migrated so no `ForModel` mention remains). The `intAlloc <= modelAlloc + 4096` assertion holds (a boxed `Range` struct per render vs a boxed `int` — see [Performance](#performance-considerations)).
  - `RangeFunctionTests`: `HostRange` return type + body → `public static Heddle.Models.Range HostRange(int start, int last, int step) => new Heddle.Models.Range(start, last, 1);`; update the `RangeJoinsDefaultRegistryWithBothOverloads` comment ("produce `ForModel`" → "produce `Heddle.Models.Range`").
  - `BranchIsolationTests`: `public ForModel Range { get; set; }` → `public Heddle.Models.Range Range { get; set; }`; `new ForModel { Last = 2 }` → `new Heddle.Models.Range(0, 2)`. E7 output `NONEXNONEX` unchanged.
  - `template.heddle` line 52: `@for(@new ForModel() { Last = model.Products.Count(), Step = 3 })` → `@for(@new Heddle.Models.Range(0, model.Products.Count(), 3))`. **C#-tier retained** (the fixture uses LINQ `.Count()`, which native `range(...)` cannot host — HED1003). `range(0, N, 3)` ≡ `ForModel { Start→0, Last=N, Step=3 }`, so `generated.html` is **byte-identical** and is **not** edited.
- **Done when.** `ForSugarTests`, `ForSugarAllocationTests`, `RangeFunctionTests`, `BranchIsolationTests`, and `HeddleTemplateTests` (the `generated.html` golden) all pass; `generated.html` shows **no** diff.

### WI8 — New differential fixture for `@for(range(...))`
- **Files.** Add `src/Heddle.Tests/TestTemplate/range-for.heddle` + sibling golden `generated-range-for.html` (LF-pinned — the existing [`.gitattributes`](../../../.gitattributes) rule `src/Heddle.Tests/TestTemplate/** text eol=lf` (L18) **already** covers this directory; **verify** the new fixture/golden resolve to LF on a Windows checkout — **no new `.gitattributes` root is required**). Add a fact exercising it through **both** backends (dynamic + precompiled) if the differential harness is table-driven; otherwise a golden fact plus the existing dynamic==precompiled differential corpus picks it up.
- **Change.** Fixture body renders `@for(range(2, 10, 2)){{@out()}}` → `2468`, plus `@for(range(2, 8, 3)){{[@out()]}}` → `[2][5]`, plus `@for(range(5, 2)){{x}}` → empty, plus one **stringification** row `@(str(range(2, 10, 2)))` → `range(2, 10, 2)` (pins the D4 `ToString` change class at a `str(...)` site — see [Back-compat](#back-compat-and-migration) — and, being routed through both tiers, verifies `PrecompiledFunctions.Str(new Heddle.Models.Range(...))` renders identically to the dynamic `Str`), so the golden pins the plan's validation rows. Assert dynamic and precompiled render **byte-identical**.
- **Done when.** The fixture golden matches on all TFMs; the dynamic==precompiled differential is byte-identical on it; the `@(str(range(...)))` row renders `range(2, 10, 2)` (was `Heddle.Models.ForModel`).

### WI9 — Migrate the published docs
- **Files.** `docs/native-expressions.md`, `docs/built-in-extensions.md`, `docs/language-reference.md`.
- **Change.** Replace every `ForModel` mention with the `Heddle.Models.Range` model: in `built-in-extensions.md`, the inner-`@` FullCSharp callout (≈14, `@for(@new ForModel…)` → `@for(@new Heddle.Models.Range(0, …)…)`), the `range` table row and paragraph ("returns a `Heddle.Models.Range { Start, Last, Step }`"; standalone `@(range(1, 5))` now "renders `range(1, 5)`", not "`ForModel.ToString()` (useless)"); the `for` section ("The model is a `Heddle.Models.Range { Start, Last, Step }`"; the `for` capability-table row `Heddle.Models.Range` / `int`); the C#-tier examples `@new ForModel() {…}` → `@new Heddle.Models.Range(0, …, 3)`. Keep the authoring guidance steering to `@for(...)`.
- **Done when.** No `ForModel` remains in the **published author docs** — `git grep -n ForModel -- 'docs/*.md'` (i.e. `native-expressions.md`, `built-in-extensions.md`, `language-reference.md`) returns **zero**. The grep is scoped to the migration surface and **excludes** `docs/plan/**` and `docs/spec/**` — the spec-set docs ([post-2.0/README.md](README.md), [common/cross-cutting.md](common/cross-cutting.md), this spec, and the [records.md](../records.md) ledger row appended in [WI10](#wi10--breaking-window-bookkeeping--migration-note)) legitimately mention `ForModel` to **document** the deletion and are not migration targets — and the generated `docs/.vitepress/dist/**` (rebuilt by docs:build). `cd docs && npm run docs:build` succeeds.

### WI10 — Breaking-window bookkeeping + migration note
- **Files.** `docs/spec/records.md` (append one amendments-ledger entry only — **do not touch** the 2.0 as-shipped record table); the release CHANGELOG entry (migration note).
- **Change.** Append a [cross-spec amendments ledger](../records.md#cross-spec-amendments-ledger) row: *Amendment* — "`Heddle.Models.ForModel` deleted; `range(...)`/`@for` remodeled onto the new `Heddle.Models.Range` value type; the new type's readable `ToString()` changes range stringification bytes (D4). Both effects added to the still-open 2.0 breaking window under R5 revised and the 2026-07-15 maintainer ruling (2.0.0 not yet tagged)"; *Made by* — this Phase 6 spec; *Amends* — the 2.0 window contents. Ship the [migration note](#back-compat-and-migration) in the CHANGELOG. **Do not** edit the `records.md` 2.0 as-shipped record table (it is append-only) — its window-status wording already reads **open**, reconciled by the common-docs judge under the 2026-07-15 ruling (D9); the amendments-ledger append below is this spec's only edit to records.md. Update the [spec master index](../README.md) row for this spec in the same change ([spec-conventions — index maintenance](../common/spec-conventions.md#index-maintenance)).
- **Done when.** The ledger entry and CHANGELOG note are present; the as-shipped record table is unchanged.

## Public API / contract

**New — `Heddle.Models.Range` (`public readonly struct`, `IEquatable<Range>`):**

| Member | Signature | Semantics |
|---|---|---|
| ctor | `Range(int start, int last)` | Step defaults to `1`. |
| ctor | `Range(int start, int last, int step)` | Verbatim field assignment; **no** step validation (D2/D6). |
| prop | `int Start { get; }` | Inclusive lower bound. |
| prop | `int Last { get; }` | **Exclusive** upper bound (`start >= last` ⇒ empty). |
| prop | `int Step { get; }` | Increment; `1` by default. |
| method | `bool Equals(Range other)` / `override bool Equals(object)` / `override int GetHashCode()` | Value equality over `(Start, Last, Step)`; `netstandard2.0`-safe manual hash. |
| operator | `static bool operator ==` / `!=` | Value equality (excluded from the public-API snapshot as `IsSpecialName`). |
| method | `override string ToString()` | D4 format: `range(Start, Last)` or `range(Start, Last, Step)`, invariant culture. |
| method | `static Range FromSystemRange(System.Range range)` | **net6.0/net8.0/net10.0 only** (`#if !NETSTANDARD2_0`); from-start endpoints, step 1; throws `ArgumentException` on a from-end (`^`) index. |

**Changed:** `Heddle.Precompiled.PrecompiledFunctions.Range(int, int)` and `Range(int, int, int)` now return `Heddle.Models.Range`. **Removed:** `Heddle.Models.ForModel` (entire public type).

**Thread safety.** `Range` is an immutable value type — inherently thread-safe; no per-render or shared mutable state. `ForIndexExtension` gains no state (a `Range` boxed into `Scope.ModelData` lives in the per-render `Scope` lineage, honoring the "no mutable per-render state on extension instances" rule). `FromSystemRange` is a pure static.

## Diagnostics / error surface

**No new diagnostic id.** The one relevant diagnostic is reused verbatim.

| ID | Message | Trigger | Position |
|---|---|---|---|
| **HED4001** (reused — *no new id*) | `Function 'range' requires a positive step, but {step} was supplied — a zero or negative step never terminates the loop.` (`BuiltInFunctions.RangeStepMessageFormat`, unchanged) | Compile: built-in three-arg `range` with a statically-visible non-positive literal (or sign-prefixed literal) step, bound by `MethodInfo` reference (host-replaced `range` exempt). | The step argument (`call.Arguments[2].Position`). |
| *(no id — render throw)* | Same shared message, via `TemplateProcessingException`. | Render: the built-in three-arg `range` reaches `step <= 0` from a model-driven value. | n/a (host exception, not a compile diagnostic). |

The new type raises **no** diagnostics of its own; `@for` type-mismatch remains the pre-existing **HED0004** (a non-`Range`/non-`int` model).

## Testing plan

**TDD verdict.** *Test-first* for the two new behaviors — `Range.ToString()` (D4 exact strings) and `Range.FromSystemRange` (mapping + from-end `ArgumentException`) — written failing before WI1 lands. *Test-with* for the remap/deletion: the existing `ForModel`-referencing tests are edited in the same commit as the code they pin (a type deletion cannot be expressed as a red-first test), then must pass unchanged in intent.

**Fixtures / goldens / unit rows (one per success + validation criterion):**

| Scenario | Asset / test | Expected |
|---|---|---|
| Range iteration | `range-for.heddle` (WI8) `@for(range(2, 10, 2)){{@out()}}` | `2468` |
| Documented start/step/last-exclusive (bracket-joined) | `range-for.heddle` (WI8) `@for(range(2, 8, 3)){{[@out()]}}` | `[2][5]` |
| Start/step honored (space-joined) | `ForSugarTests.F08` (`@for(range(2, 8, 3)){{@out() }}`) | `2 5 ` |
| Bare-int path unchanged | `ForSugarTests.F01/F02/F03` (`@for(3)`, `@for(Count)`) | `xxx` / `<i>0</i><i>1</i><i>2</i>` / `xxxx` |
| Empty range | `ForSugarTests.F09` (`@for(range(5, 5))`, `@for(range(7, 3))`) + fixture `@for(range(5, 2))` | empty |
| Two-arg step 1 | fixture/unit `@for(range(2, 10))` | `2…9` |
| Literal zero/negative step ⇒ HED4001 | `RangeFunctionTests.R01/R02` | positioned HED4001 at the step arg |
| Model-driven non-positive step ⇒ throw | `RangeFunctionTests.R03` | `TemplateProcessingException`, shared message |
| Host-replaced `range` exempt | `RangeFunctionTests.R04` | compiles, no HED4001 |
| `ToString` | new unit test (D4 rows) | `range(2, 10, 2)` / `range(2, 10)` / `range(5, 2)` |
| `ToString` change class at a `str(...)` site | `range-for.heddle` (WI8) `@(str(range(2, 10, 2)))`, both tiers | `range(2, 10, 2)` (was `Heddle.Models.ForModel`) |
| Value equality / hash | new unit test | `new Range(2,10,2) == new Range(2,10,2)`; `!= new Range(2,10)`; equal hashes |
| `System.Range` interop | new unit test (net6.0+) | `FromSystemRange(2..10)` ⇒ `range(2, 10)`; `^` index ⇒ `ArgumentException` |
| Host C# on `ForModel` breaks | compile-break, covered by the migration note (no test — deleted type) | — |
| Old C#-tier form migrates | `ForSugarTests.F10` / `template.heddle` (WI7) | byte-identical output |
| Public-API snapshot | `PublicApiSurfaceTests` (WI6) | `ForModel` absent; `Range` present; `PrecompiledFunctions.Range` returns `Heddle.Models.Range` |
| Lockstep | `DefaultFunctionLockstepTests` (all four facts) | green on `Heddle.Models.Range`, 35 rows |
| Dynamic == precompiled differential | `range-for.heddle` through both backends | byte-identical |
| Branch-state isolation over `@for(range)` | `BranchIsolationTests.E7` | `NONEXNONEX` |
| Allocation invariant | `ForSugarAllocationTests` (renamed) | `intAlloc <= modelAlloc + 4096` |

**Regression gate** ([testing standards](../common/testing-standards.md#regression-gates), one combined run): `dotnet build -c Release` (all TFMs); `dotnet test src/Heddle.Tests` (all TFMs incl. `net48` — where `Range` still compiles and `System.Range` interop is absent) with **the lockstep tests, the public-API snapshot, and the byte-identity/differential corpus** all green and every non-intentional golden byte-identical; grammar-stability check (no grammar change); the affected benchmark (see [Performance](#performance-considerations)); `docs:build` (WI9 touched docs). The new `range-for.heddle`/`generated-range-for.html` fall under the **existing** `src/Heddle.Tests/TestTemplate/** text eol=lf` pin (`.gitattributes` L18) — no new root; verify LF on a Windows checkout. **Sample gallery:** the standalone-`ToString` change is user-visible only on the discouraged form; the differential fixture WI8 is the demo item — no new gallery sample warranted (the `@for`/`range` behavior is byte-unchanged).

## Back-compat and migration

**What is proven byte-identical (the common cases):**
- `@for(3)` and `@for(Count)` — the `int` fast path is literally untouched (D8); `ForSugarTests.F01–F06` and `LanguageServiceBenchmarks` (`@for(3)`) pin it.
- `@for(range(2, 10, 2))`, `@for(range(2, 8, 3))`, `@for(range(5, 2))` — loop mechanics (bounds, step, exclusivity, empty-on-`start >= last`) are unchanged; only the underlying type identity changes. The dynamic==precompiled differential (WI8) and `ForSugarTests.F08/F09` prove byte-identity, including the `generated.html` integration golden which does **not** move.
- The **only** intentional rendered-byte change is one `ToString` change class (D4): the improved `Heddle.Models.Range.ToString()`, observed at **every** stringification site — standalone `@(range(...))`, `@(str(range(...)))`, `@(format(range(...), …))` (Native tier — `Range` is not `IFormattable`, so `BuiltInFunctions.Format` falls through to `Str` = `Convert.ToString(…, InvariantCulture)`), and FullCSharp string concatenation/interpolation. All previously rendered the useless `Heddle.Models.ForModel` and now render the readable `range(2, 10, 2)` form. No test asserted the old string at any site (grep-verified), so nothing but the new `ToString` unit test and the WI8 `str(range(...))` differential row depends on it.

**Break surface — the 17 enumerated non-plan `ForModel` references** (each with its disposition). The **17** below are the *migration* break surface: production `src/**`, the tests, the two golden/fixture assets, and the three **published author docs**. A raw repo grep additionally matches the docs that **document** this deletion rather than migrate for it — the four `docs/plan/**` files, [post-2.0/README.md](README.md), [common/cross-cutting.md](common/cross-cutting.md), this spec, and the [records.md](../records.md) ledger row (WI10) — **none** of which are edited/counted here (they are excluded from the [WI9](#wi9--migrate-the-published-docs) grep and success criterion):

| # | File | Change |
|---|---|---|
| 1 | `src/Heddle/Models/ForModel.cs` | **Deleted** (WI5). |
| 2 | `src/Heddle/Extensions/ForIndexExtension.cs` | `[DataType]` → `Range`; direct field reads (WI4/D8). |
| 3 | `src/Heddle/Runtime/Expressions/BuiltInFunctions.cs` | Both `Range` overloads return `Heddle.Models.Range` (WI2). |
| 4 | `src/Heddle/Precompiled/PrecompiledFunctions.cs` | Both shim methods return `Heddle.Models.Range` (WI3). |
| 5 | `src/Heddle/Precompiled/DefaultFunctionTable.cs` | Const + two rows → `"Heddle.Models.Range"` (WI3). |
| 6 | `src/Heddle/Data/HeddleDiagnosticIds.cs` | Doc comment `ForModel` → `Range` (WI5). |
| 7 | `src/Heddle/Runtime/Expressions/FunctionRegistry.cs` | Doc-comment `<see cref>` → `Range` (WI5). |
| 8 | `src/Heddle.Tests/DefaultFunctionLockstepTests.cs` | `typeof(ForModel)` → `typeof(Heddle.Models.Range)` (WI3). |
| 9 | `src/Heddle.Tests/ForSugarAllocationTests.cs` | Template + doc comment + method name + failure-message string (≈L58) + inline comments (≈L47/L55/L56) — all `ForModel` tokens (WI7). |
| 10 | `src/Heddle.Tests/ForSugarTests.cs` | F10 template + doc comment (WI7). |
| 11 | `src/Heddle.Tests/BranchIsolationTests.cs` | `ForHolder.Range` type + construction (WI7). |
| 12 | `src/Heddle.Tests/RangeFunctionTests.cs` | `HostRange` return + body + comment (WI7). |
| 13 | `src/Heddle.Tests/TestTemplate/public-api-heddle.txt` | **Golden** move: remove `ForModel`, add `Range`, change `PrecompiledFunctions.Range` return (WI6). |
| 14 | `src/Heddle.Tests/TestTemplate/template.heddle` | Line 52 C#-tier construction → `Heddle.Models.Range` (WI7); `generated.html` unchanged. |
| 15 | `docs/native-expressions.md` | `range` row + paragraph, standalone-render note (WI9). |
| 16 | `docs/built-in-extensions.md` | inner-`@` FullCSharp callout (≈14) + `for` section + capability-table row + C#-tier example (WI9). |
| 17 | `docs/language-reference.md` | Embedded-C# example (WI9). |

**Migration note (window deliverable — CHANGELOG).** `Heddle.Models.ForModel` is deleted; **every** construction *and* **every** member read migrates mechanically to `Heddle.Models.Range`. `ForModel`'s fields were `int? Start`, `int Last`, `int? Step` (defaults supplied by the consumer via `?? 0` / `?? 1`); `Range`'s are **non-nullable** `int Start`, `int Last`, `int Step` (D1/D2) with the defaults baked into its constructors (Start `0`, Step `1`). So there are two mechanical changes — rewrite the construction, and strip nullable member access.

*Construction — every `ForModel` object-initializer shape maps to a positional `range(...)` / `new Heddle.Models.Range(...)` call* (`Range`'s fields are get-only, so there is **no** object-initializer/settable-property form — a `ForModel { … }` initializer must become a **constructor call**, supplying `0` for an omitted `Start` and `1` for an omitted `Step` explicitly):
- `new ForModel { Start = s, Last = l, Step = st }` → `range(s, l, st)` (native tier) **or** `new Heddle.Models.Range(s, l, st)` (C# tier).
- `new ForModel { Start = s, Last = l }` (no `Step`) → `range(s, l)` / `new Heddle.Models.Range(s, l)` (step defaults to 1).
- `new ForModel { Last = l, Step = st }` (no `Start`) → `range(0, l, st)` / `new Heddle.Models.Range(0, l, st)`.
- `new ForModel { Last = l }` (no `Start`, no `Step`) → `range(0, l)` / `new Heddle.Models.Range(0, l)` (or `@for(l)` for the bare count).
- `@for(@new ForModel(){ Last = n, Step = 3 })` → `@for(range(0, n, 3))`; when the bound needs C# (e.g. LINQ `.Count()`), `@for(@new Heddle.Models.Range(0, n, 3))`.

*Member access on a `range` / `PrecompiledFunctions.Range` result (or any host-handled `ForModel` value):*
- Switch the declared type `Heddle.Models.ForModel` → `Heddle.Models.Range` (fully qualified; mind the `System.Range` collision — D5).
- `Start` and `Step` are now non-nullable `int`, so **remove every nullable operation** on them: `result.Start ?? 0` → `result.Start`; `result.Step ?? 1` → `result.Step`; `result.Step.HasValue` → (always true — drop the guard); `result.Step.Value` / `result.Step.GetValueOrDefault()` → `result.Step`. Leaving a `??` / `.HasValue` / `.Value` / `.GetValueOrDefault()` on the now-`int` field is a **compile error** (`??` on non-nullable `int` is CS0019; `.HasValue`/`.Value` do not exist on `int`), so this strip is **required, not optional**. `Last` was already `int` and is unchanged.

*Interop:*
- `System.Range` → `Heddle.Models.Range.FromSystemRange(r)` (net6.0+; from-start endpoints only, step 1).

*LSP tooling surface (informational — no action):* the `range` completion/hover **signature detail** is computed from the function registry's return type at runtime (`CompletionProvider.FormatSignature`/`Friendly`), so it updates automatically from a `ForModel`-based to a `Range`-based signature. It carries **no** `ForModel` token (hence it is not one of the 17 migration-surface files) and no fixture/golden pins it, so there is nothing to edit or migrate.

*Rendered output (benign — no action needed):* any template that stringifies a range — standalone `@(range(...))`, `@(str(range(...)))`, `@(format(range(...), …))`, or FullCSharp concatenation/interpolation — now renders `range(2, 10, 2)` instead of the useless `Heddle.Models.ForModel`. This is the sole intentional rendered-byte change (D4), licensed in the open 2.0 window (D9). No migration is required; it is a readability improvement, and the standalone/stringified `range(...)` form was always discouraged in favour of `@for`.

**Window membership** recorded per D9 (owning decision + amendments-ledger append; the still-open 2.0 window; `records.md` as-shipped table untouched).

## Performance considerations

`@for` is a hot render path; the value type must not regress allocation.

- **No new per-iteration allocation.** A `Range` in `Scope.ModelData` (an `object`) is boxed **once per render** and unboxed **once** by the `is Range model` pattern before the loop; the loop reads `Start/Last/Step` from a stack local. This mirrors `ForModel`'s once-per-render heap object — the per-iteration cost (the `TickDeadline` probe + body) is unchanged.
- **Per-render allocation is flat or lower.** Dynamic path: `BuiltInFunctions.Range` returns a struct, boxed once when it enters the model pipeline — versus `new ForModel` (one heap object) before. Precompiled path: `PrecompiledFunctions.Range` returns a struct, boxed once where a reference was passed before — a single ~24–32-byte box, not a per-iteration cost. Both are ≤ the prior class allocation.
- **Guarding test/benchmark.** `ForSugarAllocationTests.IntForLoopAllocatesNoMoreThanTheRangeModelPath` (renamed, WI7) is the allocation proof — `intAlloc <= modelAlloc + 4096` over 200 renders via `GC.GetAllocatedBytesForCurrentThread()`; a boxed `Range` on the model arm keeps the assertion true. `LanguageServiceBenchmarks` (`[MemoryDiagnoser]`, renders `@for(3)`) guards the unchanged bare-int hot path — allocated bytes must not increase, mean time within reported error. Run both before/after on the same machine as the gate (a hot path is touched).

## Standards compliance

- **Correctness/back-compat (precedence 1).** Every valid `@for`/`range` template renders byte-identically; the one intentional byte change (standalone `@(range(...))` `ToString`) is ratified by the R5 window. The public-API snapshot and lockstep gate make the surface move a **reviewed** diff, not silent breakage.
- **Simplicity (precedence 3) beats abstraction (5).** One struct, one factory on it (no converter class — YAGNI), the step guard left exactly where it lives (no new validation layer). DRY is honored where knowledge must not diverge: `RangeStepMessageFormat` stays the single message across both enforcement layers; the `DefaultFunctionTable`/shim/registry return type moves as one coupled edit (the lockstep gate is the DRY enforcement).
- **`netstandard2.0` discipline.** Manual hash (no `System.HashCode`) and the `#if !NETSTANDARD2_0` interop are the two BCL-gap accommodations; no other `#if` is introduced ([D7](../common/cross-cutting-decisions.md#d7--net-10-stance-no-conditional-compilation-by-default) respected — the guard is a genuine platform gap, not a perf fork).
- **API design.** Immutable value type with value equality + a non-throwing-in-the-common-case `ToString`; the only throwing member (`FromSystemRange` on a from-end index) is a host-programming error (`ArgumentException`), matching the error-handling rule. No `TemplateOptions` addition, so the completeness invariant is untouched.

## Deferred items

| Item | Trigger |
|---|---|
| Constructor-level `step > 0` validation on `Range` | A reported case where a host builds a non-positive-step `Range` and feeds `@for` a `Last > Start` range (today bounded only by the render deadline). Revisit as an additive guard (throwing ctor) in a future window; out of this phase's preserved-policy scope. |
| Bidirectional / `Range → System.Range` interop | A concrete host need to hand a Heddle range to BCL range-consuming APIs. Lossy (no step) — deferred until asked. |
| `long`/`decimal` range domains, extra `range` overloads | A template needing 64-bit or non-integer counted loops. Plan non-goal (type remap, not feature expansion). |
| `@for` accepting `System.Range` directly via `[DataType(typeof(System.Range))]` | Only if hosts commonly pass `System.Range`; blocked on `netstandard2.0` (type absent) and would need a per-TFM `[DataType]`. Interop factory covers the need for now. |

## External references

- [.NET — `System.Range`](https://learn.microsoft.com/en-us/dotnet/api/system.range) and [`System.Index`](https://learn.microsoft.com/en-us/dotnet/api/system.index) (from-end `^` semantics, no step; `netstandard2.1`+).
- [.NET — `System.HashCode`](https://learn.microsoft.com/en-us/dotnet/api/system.hashcode) (`netstandard2.1`+ — hence the manual fold).
- [Framework Design Guidelines — conversion operators](https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/operator-overloads) (operators must not throw → the named factory choice, D3).
- [Breaking changes and .NET libraries](https://learn.microsoft.com/en-us/dotnet/standard/library-guidance/breaking-changes) (the window discipline).
- Repo: [ForModel.cs](../../../src/Heddle/Models/ForModel.cs), [BuiltInFunctions.cs](../../../src/Heddle/Runtime/Expressions/BuiltInFunctions.cs), [ForIndexExtension.cs](../../../src/Heddle/Extensions/ForIndexExtension.cs), [PrecompiledFunctions.cs](../../../src/Heddle/Precompiled/PrecompiledFunctions.cs), [DefaultFunctionTable.cs](../../../src/Heddle/Precompiled/DefaultFunctionTable.cs), [DefaultFunctionLockstepTests.cs](../../../src/Heddle.Tests/DefaultFunctionLockstepTests.cs), [PublicApiSurfaceTests.cs](../../../src/Heddle.Tests/PublicApiSurfaceTests.cs), [HeddleDiagnosticIds.cs](../../../src/Heddle/Data/HeddleDiagnosticIds.cs), [NativeExpressionCompiler.cs](../../../src/Heddle/Runtime/Expressions/NativeExpressionCompiler.cs).
- Governance: [R5 revised](../../plan/common/standing-rulings.md#standing-rulings-user-set), [D1](../common/cross-cutting-decisions.md#d1--stable-diagnostic-ids-hedxxxx)/[D2](../common/cross-cutting-decisions.md#d2--breaking-changes-land-only-in-ratified-breaking-windows), [breaking-windows.md](../common/breaking-windows.md), [coding standards](../common/coding-standards.md), [testing standards](../common/testing-standards.md).
