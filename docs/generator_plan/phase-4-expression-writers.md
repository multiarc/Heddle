# Phase 4 — expression writers

## Header

- **Status:** proposed — not started
- **Goal (one line):** One set of shared, Roslyn-free rule tables (numeric kinds, operator
  legality, member visibility, hop form, literal formatting, overload rank) under
  `src/Heddle/Language/**` so the generator's expression emitters and the runtime's
  `NativeExpressionCompiler` can no longer disagree — with the two live bugs (the `ToString("R")`
  literal round-trip and the unguarded binary-operator emission) fixed first, independently of any
  extraction.
- **Depends on:** nothing for the fix-first group (WI1–WI2 ship standalone). The shared-artifact
  work depends only on the existing linked-source mechanism
  (`src/Heddle.Generator/Heddle.Generator.csproj:50` — the `..\Heddle\Language\**\*.cs` glob,
  already proven by `ExprOperator` and the parse front end). Coordination points: **phase 3**
  co-owns the member-visibility core (this phase builds the core and adopts it in the runtime's
  `MemberPathResolver`; phase 3 adopts it in `SymbolTypeResolver`) and the `NumericKind` tables
  built here are consumed by **phases 1 and 3**; the `DynamicMember` routing (WI9) needs the
  manifest schema-version constants **phase 5** extracts (`PrecompiledSchema.cs`), so WI9 lands
  with or after phase 5's constants.
- **Changes an externally-visible contract:** narrowly, and only in the parity-restoring
  direction. (1) The interim emit guard and the G17/G9 fix change precompiled output **only where
  it currently diverges from the runtime** — every byte-visible change restores the documented
  "precompiled renders byte-identically to runtime" contract, which
  [breaking-windows.md](../spec/common/breaking-windows.md) treats as fix-forward, not a window
  item. (2) One additive public API (`PrecompiledRuntime.DynamicMember`). (3) One generated-code
  shape change (dynamic hops route through that helper), gated behind a manifest schema-version
  bump so older runtimes fall back cleanly (see Back-compat). Nothing here lands in a breaking
  window; nothing changes the behavior of templates whose two tiers already agree.

Supplementary document: [phase-4-expression-writers-rule-tables.md](phase-4-expression-writers-rule-tables.md)
— the normative `NumericKind` mapping tables, the operator classification decision table (the
seven spec deviations as data), the interim emit-guard whitelist, the hop-form rule, the operator
lexeme table, and the unified C# escape set. Those tables are the spec of record for the shared
artifacts; tests derive from them.

## Goal

The research for this area ([04 — expression writers](../research/generator-code-sharing/04-expression-writers.md))
found nine places where `Emit/NativeExpressionWriter.cs` + `Emit/MemberPathWriter.cs` re-implement,
by hand, semantics the runtime's `NativeExpressionCompiler`/`MemberPathResolver` also implement by
hand — and verified live divergence in the two worst of them. This phase turns that document into
a resolution plan with two distinct deliverable groups.

**Group one — fix the two shipped bugs, before and independent of any extraction.**

First, the literal round-trip hazard ([04 F6](../research/generator-code-sharing/04-expression-writers.md)):
`LiteralFormatter` formats `float`/`double` with `ToString("R")`
(`src/Heddle.Generator/Emit/NativeExpressionWriter.cs:283-284`, verified). The generator runs
inside the compiler process, and under a .NET Framework host (VS, desktop `VBCSCompiler`) `"R"` is
the documented non-round-tripping format — a `double` literal can re-parse one ULP off. The
runtime never re-formats: `NativeExpressionCompiler` keeps the decoder's boxed value. So the same
template can compute a different value precompiled than at runtime, *dependent on which machine
built it*, and the differential harness only catches it if a corpus template happens to hit a
non-representable double. The fix is `G17`/`G9`, the round-trip-guaranteed formats on every
supported host, plus a decoder↔formatter round-trip test that runs on `net48` — the exact TFM
where `"R"` misbehaves.

Second, the unguarded operator emission ([04 F2](../research/generator-code-sharing/04-expression-writers.md)):
[docs/native-expressions.md](../native-expressions.md) (deviations list, `:194-207`) names seven
deliberate points where the native tier does **not** match C#. The runtime implements all seven
explicitly; the generator's `WriteBinary` (`src/Heddle.Generator/Emit/NativeExpressionWriter.cs:218-228`,
verified) emits `(left op right)` unconditionally, consulting no operand types at all. The
consequences are asymmetric and both bad: `==` on mixed/unrelated types produces **CS0019 in the
consumer's build** for a template the runtime accepts (the worst failure mode a source generator
has), while enum arithmetic and `enum & 0` produce valid C# that *renders* where the runtime
raises a positioned error — opposite verdicts, silent divergence. The full cure is the shared
classification table (group two), but that table needs `NumericKind` first; the interim mitigation
is a conservative, generator-local emit guard that degrades every binary (and, with the same
machinery, unary/ternary) emission whose operand kinds are not provably inside a closed
identical-in-C# whitelist. Degrading is always safe: `Write` returning null routes the template to
the dynamic tier, where the runtime's own compiler — the semantics of record — evaluates the
expression.

**Group two — the shared artifacts.** All Roslyn-free, netstandard2.0-clean, and (except the one
public runtime helper) placed under `src/Heddle/Language/**` so the existing csproj glob links
them into the generator with zero csproj edits, exactly as `ExprOperator` is linked today:

- `NumericKind` + the re-keyed implicit-numeric-conversion and promotion tables
  ([04 F7](../research/generator-code-sharing/04-expression-writers.md)) — **the enabler**. The
  runtime's `Type`-keyed table (`src/Heddle/Runtime/Expressions/NumericPromotion.cs:32-54`,
  verified) and the generator's `SpecialType`-keyed twin
  (`src/Heddle.Generator/Emit/TemplateEmitter.cs:1567-1605`) agree entry-for-entry today, but
  `TryPromote`/`UnaryPromote` have no generator counterpart — which is exactly why F2 exists.
  This phase owns the shared table; phases 1 and 3 consume it for prop defaults and binding.
- `Language/Expressions/OperatorLexeme.cs` ([04 F5](../research/generator-code-sharing/04-expression-writers.md))
  — one `ExprOperator → lexeme` table replacing the three copies (generator `BinarySymbol`,
  runtime `Symbol`, and the AST builder's token mapping), killing the silent-miscompile surface
  of a transposed `&`/`&&`.
- `NativeOperatorRules.Classify` ([04 F2](../research/generator-code-sharing/04-expression-writers.md))
  — the deviations-from-C# set as a decision table over operand kinds, returning
  `Supported | RequiresRuntimeSemantics | NotDefined`. The generator emits only on `Supported`
  and degrades otherwise; the runtime keeps building `Expression` trees but gains a lockstep
  assertion that its verdicts match the table. This replaces the interim guard.
- The member-path core ([04 F1](../research/generator-code-sharing/04-expression-writers.md)) —
  `MemberFacts`/`MemberVisibility` with a shared `MemberAccess` enum, plus a generic
  `MemberPathWalk<TType>` over an `ITypeModel<TType>` facts adapter. Six verified divergences
  live here (protected-internal getters, inherited non-public members, base-interface members,
  statics, `[Hidden]` matched by unqualified name, `new`-shadowing ambiguity), and the runtime's
  own doc comment claims to be "the single source of member-path resolution semantics"
  (`src/Heddle/Runtime/Expressions/MemberPathResolver.cs:17-22`, verified) while the generator is
  a second source. This phase owns the core and the runtime `MemberPathResolver` adoption;
  phase 3 adopts it in `SymbolTypeResolver`. The *policy ruling* on which side's visibility
  behavior is correct is OQ1.
- `MemberHopRule.Form` ([04 F4](../research/generator-code-sharing/04-expression-writers.md)) —
  the three-branch null-safe hop decision (`Direct | NullConditional | NullDefaultConditional`)
  as one function, mapped to `Expression` trees by `ModelParameter` and to text by
  `MemberPathWriter`, replacing two cross-referencing doc comments with shared code.
- `LiteralFormatter` relocated under `Language/Expressions/` as the documented inverse of the AST
  decoder, plus one shared `CSharpEscape`
  ([04 F6](../research/generator-code-sharing/04-expression-writers.md),
  [04 F9](../research/generator-code-sharing/04-expression-writers.md)) — collapsing the three
  disagreeing escape tables (`EscapeChar`, `PieceWriter.Escape`, the decoder's set) into one, and
  closing the lone-surrogate-literal hole.
- The shared overload-rank core ([04 F3](../research/generator-code-sharing/04-expression-writers.md))
  — `ConversionRank`/`TryRank`/`Dominates` (verified flat-rank at
  `src/Heddle/Runtime/Expressions/NativeExpressionCompiler.cs:493-519`) recast over
  `NumericKind` + type-name descriptors, used by the generator **as an emit guard**: the comment
  at `NativeExpressionWriter.cs:116-118` claims the consumer's compiler reproduces the runtime
  rank "by construction", and that claim is false — `min(1, 2u)` is a runtime ambiguity error
  (three non-dominated widening candidates) while C# happily picks `Min(long,long)` and renders.
  Which semantics wins is OQ2.
- `PrecompiledRuntime.DynamicMember(object, string)` ([04 F8](../research/generator-code-sharing/04-expression-writers.md))
  — one public helper unifying the dynamic-binder context. Today the runtime binds dynamic hops
  in `Heddle`'s context (`typeof(DynamicParameter)`,
  `src/Heddle/Runtime/Parameters/DynamicParameter.cs:28`, verified) while generated `(dynamic)`
  code binds in the consumer assembly's context (`TemplateEmitter.cs:2150-2162`) — internal
  properties resolve on one side and not the other. Routing generated hops through the helper
  makes the context choice exist once. Which context is *correct* is OQ3.

## Non-goals / scope boundary

- **No expression-semantics changes.** Every artifact encodes the semantics
  [docs/native-expressions.md](../native-expressions.md) documents and the runtime ships. Where
  the two tiers disagree today, the generator moves to the runtime's verdict (or degrades); no
  template whose tiers already agree renders differently. Any *widening* of behavior surfaced by
  OQ1–OQ3 (e.g. accepting `protected internal` getters, adopting C# betterness) is recorded as a
  candidate in the [breaking-windows register](../spec/common/breaking-windows.md), never done
  here.
- **No generator adoption of the member core in `SymbolTypeResolver`** — that is phase 3's work
  item, against the core this phase ships. This phase's generator-side member change is limited
  to what its own emit guard needs.
- **No consumption changes in `TemplateEmitter`'s prop-default path** — deleting
  `IsImplicitNumericWidening`/`NumericKeyword` (`TemplateEmitter.cs:1567-1624`) in favor of the
  shared table is phase 1's adoption; this phase only guarantees the shared table exists and is
  proven equivalent (the lockstep test covers both existing copies).
- **No `Heddle.Shared` project.** The linked-`<Compile>` mechanism is the ratified pattern
  ([07 — shared-code layout](../research/generator-code-sharing/07-recommendations.md)); this
  phase adds at most eight shared files, well under the ~20-file revisit threshold.
- **No Roslyn types in shared files, no `Heddle.dll` reference from the generator** — hard
  constraints carried from the existing architecture. Facts adapters (Roslyn on the generator
  side, reflection on the runtime side) stay in their own assemblies.
- **No new diagnostics surface.** Degrading to the dynamic tier is the generator's existing,
  documented reaction to unsupported constructs (`NativeExpressionWriter.cs:16-17`); the guard
  makes more expressions take that path but claims no new `HED7xxx` ID. (A future
  "degraded because of operator semantics" info-level diagnostic is a spec-time option, noted in
  Risks; it is not required by this plan.)
- **No changes to the function registry, the built-in set, or `DefaultFunctionTable`** — the
  candidate *set* is already shared; only the *selection rule* is in scope (F3).
- **No gauntlet redesign.** The structural gap that `PrecompiledGauntlet` does not cover
  expression semantics ([07 — live drift](../research/generator-code-sharing/07-recommendations.md))
  is mitigated here by differential corpus entries, not by rearchitecting the gauntlet.

## Design direction

### D1 — Authority order: spec → runtime → generator; the generator degrades, never diverges

For expression semantics the specification ([docs/native-expressions.md](../native-expressions.md))
is authoritative; where the spec is silent or ambiguous, the runtime implementation is the
semantics of record; the generator's only permitted reactions are (a) emit code provably
byte-equivalent to the runtime result or (b) return null and degrade to the dynamic tier, where
the runtime's own compiler evaluates the expression. Every shared table in this phase encodes
**Heddle** semantics — the seven documented deviations, the flat overload rank, the null-safe hop
— never C#'s, because the consumer's C# compiler is precisely the component whose opinions must
stop leaking into rendered output. **Rationale:** this is the design premise the writer's own doc
comment states ("degrades the template to the dynamic path", `NativeExpressionWriter.cs:16-17`)
but only enforces for member paths and unknown node types today; extending it to operator
semantics is what closes F2. **Alternative rejected:** teaching the generator to reproduce
runtime semantics in emitted C# for the deviation cases (e.g. emitting `object.Equals(a, b)` for
mixed-type equality) — possible for some cases, but each is a hand-written re-implementation of
exactly the kind this initiative exists to remove; degrade first, upgrade to `Supported` rows
individually later if profiling ever justifies it.

### D2 — Fix-first: `G17`/`G9` literal round-trip (independently shippable)

`LiteralFormatter.Format` changes `f.ToString("R", …)` → `f.ToString("G9", …)` and
`d.ToString("R", …)` → `d.ToString("G17", …)` (`NativeExpressionWriter.cs:283-284`). `G17`/`G9`
are the shortest formats with a documented round-trip guarantee on **all** build hosts including
.NET Framework; `"R"` is documented as unreliable for `double` precisely there. The runtime never
re-formats (the compiler consumes the decoder's boxed value directly), so the formatter's only
correctness requirement is `decode(format(v)) == v` bit-for-bit — ugly-but-exact output like
`0.10000000000000001` is acceptable in generated source. A randomized-plus-corner-case round-trip
test (decoder ↔ formatter, fixed seed, includes `net48` in its TFM matrix) pins the inverse
property permanently; it lands in the runtime test suite once WI7 relocates the formatter, and in
the generator suite until then. **Rationale:** smallest possible diff for a verified
non-reproducible-across-hosts numeric divergence
([07 drift #9](../research/generator-code-sharing/07-recommendations.md)). **Alternative
rejected:** waiting for the WI7 relocation to fix it in the new home — the bug is live and the
fix is two tokens; fix-first is the ratified sequencing
([07 — recommended sequencing](../research/generator-code-sharing/07-recommendations.md)).

### D3 — Fix-first: interim conservative emit guard (generator-local, replaced by D6)

Until the shared classification table exists, `NativeExpressionWriter` gains a private
`Estimate(ExprNode) → EmitEstimate` — a coarse static-kind estimator returning
`(kind, isNullable)` where kind is one of the numeric primitives, `Bool`, `Char`, `String`,
`NullLiteral`, `Enum`, `Other`, or `Unknown`:

- literals → the decoded CLR type's kind;
- member paths → the resolved `ITypeSymbol`'s `SpecialType` (with `Nullable<T>` unwrapped via the
  existing `IsNonNullableValueType` logic); enum-typed and non-primitive results map to
  `Enum`/`Other`;
- built-in calls → the shared return kind when **every** `DefaultFunctionTable` row for that name
  agrees on `ReturnTypeName` (verified: rows carry return types —
  `src/Heddle/Precompiled/DefaultFunctionTable.cs`, `DefaultFunctionRow.ReturnTypeName`), so
  `len(s) > 0` keeps precompiling; multi-return-type names (`min`, `abs`, …) and export calls →
  `Unknown`;
- unary/binary/ternary sub-expressions → the promoted result kind when computable from operand
  kinds, else `Unknown`.

`WriteBinary`, `WriteUnary`, and `WriteTernary` then emit **only** when the operator + operand
estimates fall inside the closed whitelist specified normatively in the
[rule-tables supplement](phase-4-expression-writers-rule-tables.md#interim-emit-guard-whitelist)
— in outline: arithmetic/relational/bitwise only over known numeric/bool kinds with a legal
Heddle promotion; string `+` only with a known-kind partner; equality only within one category
(numeric×numeric legal-promotion, bool×bool, string×string, or null-literal against a
reference/nullable kind); `&&`/`||` only on non-nullable `bool`; `??` only with a
provably-nullable left and category-matching right; ternary only with a non-nullable `bool`
condition and same-kind arms; anything touching `Enum`, `Other`, or `Unknown`, and every illegal
promotion pair, degrades. **Rationale:** this converts the entire CS0019 consumer-build-break
class and the silently-divergent-render class into fallback-to-dynamic *now*, with a fully
generator-local change (no shared files, no runtime change) that WI5 deletes wholesale. The
whitelist deliberately over-degrades (`DateTime` subtraction, user-operator types, enum equality
all fall back) — a precompilation-*coverage* cost with zero output change, since the dynamic tier
renders the same bytes for every expression both tiers accept. The mandate's core is the binary
class; unary and ternary ride along because they share the estimator and have the same
unconditional-emit defect (`WriteUnary`/`WriteTernary`, `NativeExpressionWriter.cs:200-238`).
**Alternatives rejected:** guarding *only* equality between non-primitive operands (leaves enum
arithmetic and `enum & 0` rendering opposite verdicts — half of F2's verified mismatches);
building the interim guard directly on a first-cut `NativeOperatorRules` (couples the
independently-shippable fix to the F7 enabler and to shared-file review, for scaffolding that D6
replaces anyway).

### D4 — Placement: linked sources under `src/Heddle/Language/**`, adapters per side

All shared artifacts are internal types in new files under `src/Heddle/Language/Expressions/`
(operator/numeric/literal artifacts, beside the already-linked `ExprOperator.cs`) and a new
`src/Heddle/Language/Members/` folder (member-path artifacts) — both picked up by the existing
recursive glob (`Heddle.Generator.csproj:50`) with **zero csproj edits**. Shared files are
netstandard2.0-clean, reference no Roslyn type and no runtime-only Heddle type; they compile into
both assemblies as `internal`, the `ExprOperator` precedent. Per-side facts adapters stay
unshared: the generator keeps a small `SpecialType → NumericKind` / `ISymbol → MemberFacts`
adapter beside `SymbolTypeResolver` (Roslyn types allowed there; phase 3 extends it), and the
runtime keeps `Type → NumericKind` / `PropertyInfo → MemberFacts` adapters — the `Type`-keyed one
may live in the shared file itself since `System.Type` is BCL. The one exception to
"everything under `Language/**`" is the public `PrecompiledRuntime.DynamicMember` helper (D11),
which is runtime API by nature and lives with `PrecompiledRuntime`
(`src/Heddle/Precompiled/PrecompiledRuntime.cs`). **Rationale:** the mechanism is proven, has
zero plumbing cost, and keeps the "generator references no Heddle.dll" constraint intact —
generated *code* referencing `global::Heddle.Precompiled.*` at consumer-compile time is the
established pattern (`PrecompiledFunctions` shims, `NativeExpressionWriter.cs:150`).
**Alternative rejected:** a `Heddle.Shared` source-only project —
[07](../research/generator-code-sharing/07-recommendations.md) reserves it for a shared set past
~20 files; this phase adds at most eight.

### D5 — `NumericKind` and the re-keyed tables (F7 — the enabler)

`Language/Expressions/NumericKind.cs` defines `enum NumericKind` (the twelve numeric primitives
plus `None`) and a static `NumericTable` exposing `IsImplicit(NumericKind, NumericKind)` (the
§10.2.3 widening table), `TryPromote(NumericKind, NumericKind, out NumericKind)` (including the
two illegal-mix rules: `decimal` with `float`/`double`, `ulong` with signed integral),
`UnaryPromote`, `IsIntegral`, `IsSigned`, and `FromClrType(Type)`. The table content is the
normative data in the [supplement](phase-4-expression-writers-rule-tables.md#numerickind-and-the-conversion-tables),
transcribed from `NumericPromotion.cs:32-54` and diffed against `TemplateEmitter.cs:1567-1605`
(the research verified they agree today — this phase pins that agreement before it can rot).
During the transition, `NumericPromotion` keeps its public shape but delegates to the shared
table, and an exhaustive lockstep test asserts old-vs-new equality over all 13×13 kind pairs for
`IsImplicitNumeric`/`TryPromote`/`UnaryPromote` — the test the task list requires, and the same
pattern `DefaultFunctionLockstepTests` established. Once phases 1 and 3 adopt the table, the
runtime's private table literal is deleted and the lockstep test retires with it.
**Rationale:** F2's classifier and F3's ranker are both expressible only over a kind lattice, not
over `Type`/`ITypeSymbol` — this single move unlocks both
([04 F7](../research/generator-code-sharing/04-expression-writers.md): "land it first").
**Alternative rejected:** keying the shared table on CLR type names (strings) — allocation-free
enum keys are cheaper, exhaustive-testable, and independent of both fact sources.

### D6 — `NativeOperatorRules.Classify` (F2 — the real guard)

`Language/Expressions/NativeOperatorRules.cs` implements
`Classify(ExprOperator op, in OperandKind left, in OperandKind right) → OperatorVerdict` with
`enum OperatorVerdict { Supported, RequiresRuntimeSemantics, NotDefined }` and
`readonly struct OperandKind` (`NumericKind` + category `Bool/Char/String/Enum/NullLiteral/
Reference/Other/Unknown` + `IsNullable`). The decision table — normative in the
[supplement](phase-4-expression-writers-rule-tables.md#operator-classification-table) — encodes
the seven documented deviations ([docs/native-expressions.md](../native-expressions.md),
deviations 1–7) plus the runtime's operator dispatch behavior
(`NativeExpressionCompiler.cs:724-994`): mixed/unrelated equality → `RequiresRuntimeSemantics`
(the runtime's total `object.Equals`), enum arithmetic and the `enum & 0` case → `NotDefined`
(runtime positioned error), user-conversion-bearing operand types → `RequiresRuntimeSemantics`,
`bool?` logicals → `NotDefined`, illegal promotions → `NotDefined`, and the plain
primitive-lattice cases → `Supported`. Consumption: the generator replaces the D3 interim guard —
it feeds `OperandKind`s from its Roslyn facts adapter and emits only on `Supported`; `NotDefined`
and `RequiresRuntimeSemantics` both degrade (the dynamic tier then either evaluates with runtime
semantics or raises the runtime's own positioned error — matching verdicts by construction). The
runtime does **not** restructure its compile path in this phase; it gains a test-suite lockstep
sweep asserting that, over a generated sample of the kind lattice, `Classify`'s verdict predicts
the compiler's actual outcome (`Supported`/`RequiresRuntimeSemantics` → compiles;
`NotDefined` → positioned error). **Rationale:** highest-value extraction in the area
([04 F2](../research/generator-code-sharing/04-expression-writers.md)); the generator finally
gets a real "degrade instead of emit" test for the deviation set, and the table sits beside
`ExprOperator` with zero plumbing. **Alternative rejected:** making the runtime dispatch *from*
the table in the same change — a large, behavior-neutral refactor of a 1200-line compiler; the
lockstep sweep buys the drift protection now, and dispatch unification can follow as its own
mechanical change once the table has soaked.

### D7 — Member-path core (F1) and the error-shape normalizations

`Language/Members/` gains: `enum MemberAccess` (`Public, Internal, ProtectedOrInternal,
Protected, ProtectedAndInternal, Private`), `readonly struct MemberFacts` (`CanRead`,
`MemberAccess Access`, `HasHidden`, `IsStatic`), `static MemberVisibility.IsAccessible(in
MemberFacts)` — the **one** policy point where OQ1's ruling is encoded — and
`MemberPathWalk<TType>` over `interface ITypeModel<TType>` (`IsDynamic`,
`TryFindProperty(TType, string, out TProp facts+type)`, `BaseOf`, `Interfaces`), which owns the
walk order (most-derived-first through bases, then base-interface closure for interface roots)
once. Adapter rules the shared core makes explicit: `[Hidden]` is matched by **full metadata
name** (`Heddle.Attributes.HiddenAttribute`) in both adapters — the generator's unqualified-name
match (`SymbolTypeResolver.cs:229`) is corrected by phase 3's adoption; static properties are
**not accessible** (`MemberFacts.IsStatic` → false), which converts the runtime's unpositioned
`ArgumentException` from `Expression.MakeMemberAccess` (`ModelParameter.cs:41`) and the
generator's consumer-side CS0176 into the same positioned property-not-found; `new`-shadowed
properties resolve deterministically to the most-derived accessible one, replacing the runtime's
possible `AmbiguousMatchException` (`Type.GetProperty`, `MemberPathResolver.cs:77`). This phase
adopts the core in `MemberPathResolver.TryResolve`/`GetVisibleProperties` behind a reflection
`ITypeModel` (making the "single source" doc claim at `MemberPathResolver.cs:17-22` true again);
phase 3 adopts it in `SymbolTypeResolver.ResolvePath`/`FindProperty`. Until phase 3 lands, the
accept/reject *policy* in `MemberVisibility` reproduces the runtime's current observable behavior
(the OQ1 recommendation), so the runtime adoption is behavior-preserving except for the two
error-shape fixes above. **Rationale:** fixes three live sandbox-contract divergences by
construction, in the one place the sandbox filter is documented to live
([docs/native-expressions.md](../native-expressions.md), sandbox section). **Alternative
rejected:** fixing the six divergences point-wise in both resolvers — six paired edits with no
guarantee the seventh divergence isn't being written at the same time.

### D8 — `MemberHopRule.Form` (F4)

`Language/Members/MemberHopRule.cs`: `enum HopForm { Direct, NullConditional,
NullDefaultConditional }` and `Form(bool receiverIsValueType, bool propertyIsNonNullableValueType)`
per the [supplement's table](phase-4-expression-writers-rule-tables.md#member-hop-form).
`MemberPathWriter.Write` maps `HopForm` to text (its current three branches,
`MemberPathWriter.cs:38-49`) and `ModelParameter.BuildNullSafePropertyChain` maps it to
`Expression` shapes (`ModelParameter.cs:39-50`, where `NullConditional` and
`NullDefaultConditional` share one `Condition`/`Default` encoding) — replacing the two files
whose doc comments currently cross-reference each other by name as their only equivalence
guarantee. Byte-neutral by definition: the rule returns exactly the branch each side already
takes; existing goldens prove it. **Rationale:** any future hop-semantics change becomes one
edit; today's equivalence stops being an *argument* and becomes shared code
([04 F4](../research/generator-code-sharing/04-expression-writers.md)).

### D9 — `LiteralFormatter` relocation and the unified `CSharpEscape` (F6, F9)

`LiteralFormatter` moves to `Language/Expressions/LiteralFormatter.cs` (namespace
`Heddle.Language.Expressions`; the generator copy in `Heddle.Generator.Emit` is deleted, call
sites re-point — `NativeExpressionWriter.cs:95` and `TemplateEmitter.cs:1531`), documented as the
inverse of the AST decoder (`Language/Expressions/ExpressionAstBuilder.cs:293-409` — integer
first-fit and real-suffix decoding, verified) with the D2 `G17`/`G9` formats carried over. A new
`Language/Expressions/CSharpEscape.cs` provides the single escape implementation for `char` and
`string` C# literals — the union of today's three tables (`EscapeChar`'s set, `PieceWriter.Escape`'s
set including `\a \b \f \v`, and the decoder's documented escape set,
[docs/native-expressions.md](../native-expressions.md) literals table), normative rows in the
[supplement](phase-4-expression-writers-rule-tables.md#c-escape-set) — and escapes **lone
surrogates as `\uXXXX`** instead of emitting them raw (closing F9's hole; today only the u8 twin
is guarded, `PieceWriter.cs:16,54-72`). `PieceWriter.Escape` and `LiteralFormatter` both delegate
to it. Relocation means the runtime assembly now compiles the formatter too — deliberately: the
decoder↔formatter round-trip test moves into the runtime suite, so a future decoder change breaks
a shared test rather than only generated code. String-literal output is byte-identical to today's
`PieceWriter.Escape` for all previously-emittable inputs (the union table only *adds* handling
for characters one table missed), so goldens do not move. **Rationale:**
[04 F6/F9](../research/generator-code-sharing/04-expression-writers.md) — directly sharable, and
the round-trip test is only enforceable where both sides can see it.

### D10 — Shared overload-rank core as a generator emit guard (F3)

`Language/Expressions/OverloadRank.cs` recasts `ConversionRank`/`TryRank`/`Dominates`
(`NativeExpressionCompiler.cs:431-519`) as pure integer logic over parameter descriptors
(`NumericKind` + CLR-type-name strings — the shape `DefaultFunctionRow` already carries) encoding
the **Heddle** rank: exact 0 / widening-reference-lifting 1 (flat) / boxing 2, params-expanded
+1, Pareto non-domination, ambiguity on a non-singleton front. The runtime delegates its three
private methods to it (lockstep-tested during transition, same pattern as D5). The generator uses
it as an emit guard in `WriteCall` (`NativeExpressionWriter.cs:111-151`): resolve the call
against the known candidate rows with the shared ranker; **degrade** when the ranker reports
ambiguity or when any argument kind is `Unknown`; when it resolves uniquely, emit the call with
**explicit casts to the chosen overload's parameter types**, which pins the consumer's C#
compiler to the same overload by making it an exact match — eliminating the class where C#
betterness silently picks a different overload than Heddle's flat rank (verified false-claim
comment at `NativeExpressionWriter.cs:116-118`; `min(1, 2u)` renders precompiled today but is a
runtime ambiguity error). Subject to OQ2's ruling (the recommendation keeps Heddle's rank as the
semantics of record, making this design final). **Rationale:**
[04 F3](../research/generator-code-sharing/04-expression-writers.md) — the drift is currently
masked for built-ins only by sample-based differential tests; cast-pinning makes the "by
construction" claim actually true. **Alternative rejected:** degrading every multi-overload call
— kills precompilation for `min`/`max`/`round` in common typed cases the ranker resolves fine.

### D11 — `PrecompiledRuntime.DynamicMember` and the schema story (F8)

A public static helper `PrecompiledRuntime.DynamicMember(object receiver, string name)` (in
`src/Heddle/Precompiled/PrecompiledRuntime.cs`, XML-doc'd, thread-safe via an internal
per-name-per-type bound-callsite cache) becomes the single implementation of a dynamic member
hop; per OQ3's recommendation it reproduces `DynamicParameter`'s exact semantics — null receiver
propagates null, binder context is the `Heddle` assembly (`DynamicParameter.cs:21-44`). The
generator's `WriteDynamicPath` (`TemplateEmitter.cs:2150-2162`) changes from the `(dynamic)` cast
chain — which binds in the *consumer's* context and therefore sees internal members the runtime
tier doesn't — to chained `DynamicMember` calls, making the accessibility divergence disappear
structurally. Because this changes generated-code shape against a runtime API that must exist at
render time, emission is **gated on the manifest schema version**: the generator emits
`DynamicMember` routing only alongside the schema-version bump coordinated with phase 5's
`PrecompiledSchema.cs` constants (today hand-typed as `MinSupportedSchemaVersion = 1` /
`MaxSupportedSchemaVersion = 2` at `src/Heddle/Precompiled/PrecompiledTemplates.cs:20-21`), so an
older `Heddle.dll` loading a newer precompiled assembly rejects it at registration and falls back
to runtime compilation instead of throwing `MissingMethodException` mid-render. The helper itself
ships earlier (additive public API); only the *routing* waits for the schema gate.
**Rationale:** [04 F8](../research/generator-code-sharing/04-expression-writers.md) — the rule
isn't code-sharable, so the fix is making the choice exist once, behind the versioning mechanism
that exists for exactly this. **Alternative rejected:** spec-note + corpus test without routing
(documents the divergence without removing it — the research's own "or better" fork).

## Dependencies & ordering

Work items in implementation order. WI1 and WI2 are the fix-first group — independently
shippable, in any order, before everything else. WI3+ follow the research area's ratified
sequencing ([04 — suggested sequencing](../research/generator-code-sharing/04-expression-writers.md)).

| WI | Delivers | Depends on |
| --- | --- | --- |
| WI1 | D2: `G17`/`G9` fix + decoder↔formatter round-trip test (incl. `net48`) | — |
| WI2 | D3: interim emit guard (`Estimate` + whitelist) + the F2 differential corpus entries (mixed-type equality, enum arithmetic, `enum & 0`, user implicit conversion) | — |
| WI3 | D4+F5: `Language/Expressions/OperatorLexeme.cs`; generator `BinarySymbol`/unary switch deleted, runtime `Symbol` delegates; supported-set test derived from the table | — |
| WI4 | D5: `NumericKind` + `NumericTable`; `NumericPromotion` delegates; exhaustive lockstep test | — |
| WI5 | D6: `NativeOperatorRules.Classify` + generator adoption (interim guard from WI2 deleted); runtime lockstep sweep; corpus entries re-pointed at the table | WI3, WI4 |
| WI6 | D7+D8: `Language/Members/` core (`MemberAccess`, `MemberFacts`, `MemberVisibility`, `MemberPathWalk<TType>`, `MemberHopRule`); runtime `MemberPathResolver` + `ModelParameter`/`MemberPathWriter` adoption; visibility conformance corpus (shared data file, reflection side) | OQ1 ruling |
| WI7 | D9: `LiteralFormatter` relocation + `CSharpEscape`; round-trip test moves to the runtime suite; lone-surrogate corpus entry | WI1 |
| WI8 | D10: `OverloadRank` shared core; runtime delegation + lockstep; generator cast-pinned emit guard; `min(1, 2u)` corpus entry | WI4, OQ2 ruling |
| WI9 | D11: `DynamicMember` helper (API first), then generator routing behind the schema bump; internal-property corpus entry | OQ3 ruling; phase 5's `PrecompiledSchema` constants for the routing step |

Cross-phase edges:

- **Phase 1** consumes WI4's table (replacing `IsImplicitNumericWidening`/`NumericKeyword`,
  `TemplateEmitter.cs:1567-1624`) and WI7's `CSharpEscape` (via `PieceWriter`); nothing in phase 1
  blocks this phase.
- **Phase 3** consumes WI4 and adopts WI6's core in `SymbolTypeResolver` (including the
  full-name `[Hidden]` fix at `SymbolTypeResolver.cs:229`); the OQ1 ruling must be jointly
  ratified before either side's adoption lands, so the two adapters encode one policy.
- **Phase 5** owns the `PrecompiledSchema.cs` constants extraction; WI9's routing step lands with
  or after it and shares its schema-version bump.

## Back-compat / impact

Analyzed item-by-item against [breaking-windows.md](../spec/common/breaking-windows.md) (windows
are for byte- or behavior-breaking changes to templates whose behavior is *correct* today) and
the precompiled parity contract (precompiled output must match runtime output byte-for-byte):

- **WI1 (G17/G9).** Generated literal *text* changes for some floats/doubles; rendered output
  changes **only** on build hosts where `"R"` mis-round-tripped — i.e. only where precompiled
  output already diverged from the runtime. Parity restoration; fix-forward, not a window item.
  Any golden that pinned an `"R"`-formatted literal changes under the golden-change policy
  (spec-backed reason, diff-reviewed).
- **WI2/WI5 (emit guards).** Three populations: (a) templates that today break the consumer's
  build (CS0019 class) start building and render via the dynamic tier — strict improvement;
  (b) templates that today render *divergent* bytes precompiled now render runtime-identical
  bytes — byte-visible **only where output was previously divergent**, i.e. parity restoration;
  (c) templates whose expressions are correct today but fall outside the interim whitelist lose
  precompilation of that expression (dynamic-tier evaluation) — output-identical, a
  startup/render-perf cost only, and WI5 restores most of that coverage. No window item in any
  population. The benchmark gate (testing standards) runs on WI2/WI5 since the render path is
  touched for population (c).
- **WI3/WI4/WI8-runtime/WI7 (table extractions with delegation).** Behavior-preserving by
  lockstep test; goldens byte-identical; the regression gate's grammar-stability check applies
  (no grammar change licensed).
- **WI6 (member core, runtime adoption).** Accept/reject behavior preserved under the OQ1
  recommendation. Two deliberate error-shape changes: static-property access becomes a positioned
  property-not-found instead of an unpositioned `ArgumentException`
  (`ModelParameter.cs:41`), and `new`-shadowed lookups resolve most-derived instead of possibly
  throwing `AmbiguousMatchException`. Both convert crashes into the documented diagnostic surface
  — the coding-standards' collect-don't-throw rule — and neither changes any successfully
  rendering template. Documented in the change notes; no window.
- **WI9 (`DynamicMember`).** The helper is additive public API (XML docs + docs-page touch per
  coding standards; `TemplateOptions` untouched). The routing change alters generated-code shape:
  a newer precompiled assembly against an older runtime would fault at render, so routing is
  schema-gated (D11) — the older runtime's schema check rejects at registration and falls back,
  the mechanism built for exactly this (`PrecompiledTemplates.cs:74-79`). Byte-visible render
  changes occur only for internal-property dynamic hops, where the two tiers currently disagree —
  parity restoration again. The schema bump itself is phase 5's coordinated change and is
  additive for hosts (fallback, never failure).

## Risks & mitigations

| Risk | Mitigation | Size (S/M/L) |
| --- | --- | --- |
| The interim guard (WI2) over-degrades a hot expression shape in real templates (precompilation-coverage regression, e.g. an export-function call inside a comparison) | The whitelist is data in the supplement and reviewed against the differential corpus + existing test templates before merge; built-in calls with uniform return types stay emittable; WI5 restores coverage with the real table; benchmark gate quantifies the interim cost | M |
| The classification table (WI5) encodes a deviation wrongly — guard passes something divergent or blocks something identical | The runtime lockstep sweep asserts table-verdict ↔ actual-compiler-outcome agreement over a generated kind-lattice sample; each of the seven deviations has a named differential corpus entry; the table rows cite the runtime dispatch lines they encode | M |
| Runtime `MemberPathResolver` adoption (WI6) silently changes accept/reject for some reflection corner (e.g. generic base properties, explicit interface implementations) | Adoption is behind the visibility conformance corpus (shared `(type, member, expected)` data file run against the reflection adapter) plus the full existing member-tier suite on all TFMs incl. `net48`; the OQ1 recommendation pins "reproduce current runtime behavior" as the acceptance bar | M |
| Shared files break the netstandard2.0/no-Roslyn constraint accidentally (a `using` slips in) | Shared files compile in the runtime's netstandard2.0 target by construction (they live in `src/Heddle/`); the generator build fails on any Heddle-runtime-type reference; review checklist item per file | S |
| Cast-pinned emission (WI8) changes arithmetic results where the cast itself converts (e.g. `min(1, 2u)` → `min((long)1, (long)2u)`) | That conversion is exactly the runtime's `ConvertTo` behavior for the chosen overload (`NativeExpressionCompiler.cs:526-546`) — pinning reproduces it; the differential corpus overload-tie entries assert precompiled == runtime for resolvable cases and degraded-for-ambiguous cases | S |
| `DynamicMember` routing ships without the schema gate and faults on older runtimes | WI9 is split: helper API first, routing only lands with phase 5's schema constants and bump; the plan's ordering makes the unsafe combination unbuildable | S |
| Lockstep tests ossify the wrong behavior (delegation forced to match a bug) | Lockstep tests are transition scaffolding with a named retirement point (when the private copy is deleted); genuine behavior corrections (statics, ambiguity, G17) are excluded from lockstep scope and pinned by their own tests instead | S |
| Phase-3 coordination slips and the two member adapters encode different OQ1 policies | The policy lives in one shared function (`MemberVisibility.IsAccessible`); adapters supply facts only — a divergent policy is structurally impossible once both sides adopt; until phase 3 adopts, the conformance corpus documents the generator's known deltas as expected-fail rows | M |
| Silent degrade makes diagnosing "why didn't this precompile" harder for template authors | Existing behavior (degrade is already the writer's contract); a future info-level diagnostic naming the degrading operator is noted as a spec-time option, deliberately out of scope here | S |

## Success criteria

Measurable, checkable statements a spec can turn into tests.

- [ ] `LiteralFormatter` formats `double` with `G17` and `float` with `G9`; no `ToString("R")`
      remains anywhere in `src/Heddle.Generator/`; the decoder↔formatter round-trip test (fixed
      seed + corner cases: non-representable doubles, subnormals, `±0`, max/min, `NaN`/infinity
      exclusions per the decoder's range rules) is green on **all** test TFMs including `net48`.
- [ ] For each of the four F2 mismatch classes — mixed-type equality, enum arithmetic,
      `enum & 0`, user-defined implicit conversion — a differential corpus template exists whose
      precompiled and runtime outputs are byte-identical (both render the same bytes or both
      raise the same-ID positioned error), and whose generated code contains **no** raw C#
      operator for the guarded expression.
- [ ] The consumer-build-break class is closed: the mixed-type-equality corpus project compiles
      cleanly (no CS0019) with the generator enabled.
- [ ] Exactly one `ExprOperator → lexeme` table exists (`OperatorLexeme`); `BinarySymbol`
      (`NativeExpressionWriter.cs:240-265`) and the runtime `Symbol` copy are deleted/delegating;
      a test derives the supported-operator set from the table and cross-checks it against every
      `ExprOperator` member (a new enum member without a table row fails the test).
- [ ] `NumericKind`/`NumericTable` exist under `Language/Expressions/`; the exhaustive 13×13
      lockstep test against `NumericPromotion` is green; `NumericPromotion`'s public behavior is
      unchanged (full runtime suite green).
- [ ] `NativeOperatorRules.Classify` exists; the generator emits binaries/unaries/ternaries only
      on `Supported`; the interim guard code from WI2 is deleted; the runtime lockstep sweep
      (table verdict ↔ compiler outcome) is green.
- [ ] `MemberPathResolver.TryResolve` and `GetVisibleProperties` are implemented via
      `MemberPathWalk` + `MemberVisibility` over a reflection facts adapter; the full member-tier
      and native-expression suites are green on all TFMs; static-property access and
      `new`-shadowed lookups produce positioned diagnostics/deterministic resolution (negative
      tests assert `HED*` ID + position per testing standards, never bare failure).
- [ ] The visibility conformance corpus (shared data: `protected internal`, inherited
      non-public, base-interface member, static, `[Hidden]` incl. a foreign
      `*.HiddenAttribute`, `new`-shadowing) runs green against the reflection adapter, with the
      generator's pre-phase-3 deltas recorded as expected-fail rows that phase 3 flips.
- [ ] `MemberPathWriter` and `ModelParameter.BuildNullSafePropertyChain` both branch on
      `MemberHopRule.Form`; all existing goldens byte-identical.
- [ ] One `CSharpEscape` implementation serves `LiteralFormatter` and `PieceWriter`; a
      lone-surrogate `char`/`string` literal is escaped as `\uXXXX` in generated source (corpus
      entry compiles and renders identically in both tiers); goldens byte-identical.
- [ ] The runtime's `ConversionRank`/`TryRank`/`Dominates` delegate to `OverloadRank`
      (lockstep-tested); the generator emits registry calls cast-pinned to the shared-ranker
      choice or degrades on ambiguity; the `min(1, 2u)` corpus entry shows identical verdicts in
      both tiers (both degrade to the runtime's ambiguity error), and a resolvable-tie entry
      (e.g. `min(1, 2)`) renders identically.
- [ ] `PrecompiledRuntime.DynamicMember` exists with XML docs; generated dynamic paths route
      through it (schema-gated per D11); the internal-property dynamic-hop corpus entry renders
      identically precompiled vs runtime; a precompiled assembly with the bumped schema loaded by
      an older-schema runtime falls back with the existing `SchemaVersionUnsupported` reason and
      renders correctly.
- [ ] Regression gate per testing standards: full solution build, full test suite on all TFMs,
      goldens byte-identical outside the spec-backed changes above, grammar-stability check
      clean, benchmarks run for WI2/WI5 (render path) with no allocation increase.

## Validation scenarios

| Input | Expected outcome |
| --- | --- |
| Template with `@(Name == Count)` (string vs int) built by the generator | Expression degrades to the dynamic tier; consumer project compiles; precompiled and runtime renders byte-identical (runtime `object.Equals` semantics) |
| Template with `@(Status + 1)` where `Status` is an enum property | No arithmetic emitted; both tiers produce the runtime's positioned error verdict (or both render, if a future table row supports it) — never opposite verdicts |
| Template with `@(Flags & 0)` on an enum property | Degrades; runtime semantics (positioned error per deviation 5) hold in both tiers |
| Model type with a user-defined implicit conversion used in a binary | Degrades; no consumer-compiler conversion is consulted; outputs byte-identical |
| `@(min(1, 2u))` with the default registry | Generator degrades (shared ranker reports a 3-way non-dominated tie); runtime raises its ambiguity error — same verdict both tiers; `@(min(1, 2))` resolves, emits cast-pinned, renders identically |
| A `double` literal whose shortest form doesn't survive `"R"` on .NET Framework, built on a `net48` msbuild host | Generated literal re-parses to the identical bit pattern; precompiled output equals runtime output; round-trip test covers the value class |
| A model with an `internal` property reached through a dynamic hop | After WI9: both tiers resolve through `DynamicMember` with one binder context — identical result; before WI9: corpus entry documents the divergence as a known-fail pinning the OQ3 decision |
| A model whose property getter is `protected internal` | Both tiers give the OQ1-ruled verdict (recommendation: both reject with the property-not-found diagnostic); conformance corpus row proves the reflection adapter; phase 3 flips the generator row |
| A template using a static property in a member path | Positioned property-not-found diagnostic from the runtime (no `ArgumentException`); generator (post phase-3) reports the twin diagnostic instead of emitting CS0176-bound code |
| A `char` literal containing a lone surrogate | Emitted as `\uXXXX`; generated source compiles; renders identically in both tiers |
| A new `ExprOperator` enum member added without touching the tables | The derived-set test and the classification-table completeness test both fail — the four-touch-point drift of F5 is structurally impossible |
| The 13×13 numeric-kind lockstep sweep, and the operator-classification lockstep sweep | Green before any consumer switches; deleting the private `NumericPromotion` table after phases 1/3 adopt retires the lockstep test |

## Open questions

Three genuine behavior decisions, as flagged by the research; each carries a recommendation this
plan is written against. They are plan-level maintainer rulings — the spec must close them as
decision records.

**OQ1 — Member-visibility policy: which side is correct?** ([04 F1](../research/generator-code-sharing/04-expression-writers.md);
co-owned with phase 3.) The runtime rejects `protected internal` getters
(`getter.IsAssembly || getter.IsPublic`, `MemberPathResolver.cs:123` — `IsAssembly` is false for
`FamilyOrAssembly`), does not surface inherited non-public or base-interface members through
`Type.GetProperty`, and matches `[Hidden]` by real attribute type; the generator accepts
`ProtectedOrInternal` (`SymbolTypeResolver.cs:224-225`), walks bases and `AllInterfaces`, and
matches `[Hidden]` by unqualified name. The runtime's doc comment claims to be the single source
of these semantics; [docs/native-expressions.md](../native-expressions.md) defines the sandbox as
"the member-tier visibility and `[Hidden]` filter" without enumerating the corners.
**Recommendation:** rule the runtime's observable accept/reject behavior normative for every
divergence — it is the shipped sandbox boundary that existing renders depend on, and narrowing
beats widening for a security filter; the generator (phase 3) conforms downward. Error-shape
fixes (statics → positioned not-found, shadowing → deterministic most-derived) are in scope now
as crash-to-diagnostic corrections; any *widening* (accepting `protected internal` per a
plain-English reading of "public-or-internal", surfacing base-interface members) goes to the
breaking-windows candidate register with a spec-wording update, not into this phase. The ruling
must be recorded once and encoded once, in `MemberVisibility.IsAccessible`.

**OQ2 — Overload-selection semantics: Heddle flat rank or C# betterness?**
([04 F3](../research/generator-code-sharing/04-expression-writers.md).) The runtime's rank vector
is flat (all widenings rank 1, `NativeExpressionCompiler.cs:507-517`), so several
widening-reachable candidates tie into an ambiguity error where C# betterness picks the closest
target (`min(1, 2u)`). The published docs promise only "exact match over widening over boxing"
([docs/native-expressions.md](../native-expressions.md), registered-functions section) — the flat
rank satisfies the documented contract. **Recommendation:** keep Heddle's flat Pareto rank as the
semantics of record; the shared `OverloadRank` core encodes it, the generator conforms via
degrade-on-ambiguity + cast-pinned emission (D10). Adopting C# betterness instead would change
runtime behavior (ambiguity errors becoming silent picks) — a behavior change that belongs in the
breaking-windows register if ever wanted, and it would re-couple Heddle semantics to C#'s exactly
where this initiative decouples them.

**OQ3 — Dynamic-binder context: whose visibility does the dynamic tier use?**
([04 F8](../research/generator-code-sharing/04-expression-writers.md).) The runtime binds with
`typeof(DynamicParameter)` — `Heddle`'s context, so a consumer-assembly `internal` property is
invisible; generated `(dynamic)` code binds in the consumer's context, where it is visible. A
secondary tension: the *typed* tier's reflection filter accepts `internal` getters regardless of
assembly, so no single existing behavior is fully self-consistent. **Recommendation:**
`DynamicMember` reproduces the runtime tier's current behavior (bind in `Heddle`'s context,
null-propagating), because cross-tier parity with the shipped fallback path is the contract this
phase restores; the typed-vs-dynamic visibility asymmetry is documented in the helper's XML docs
and filed as a spec-clarification candidate alongside OQ1's register entry. Sub-decision folded
in: routing is schema-gated (D11) rather than emitted unconditionally — ratifying the
recommendation ratifies the gate.

## External grounding

| Claim | Source |
| --- | --- |
| `WriteBinary`/`WriteUnary`/`WriteTernary` emit with no operand typing consulted; `Write` returns null to degrade | `src/Heddle.Generator/Emit/NativeExpressionWriter.cs:90-109, 200-238` (`WriteBinary` at `:218-228`) — verified in-repo for this plan |
| `LiteralFormatter` uses `ToString("R")` for `float`/`double`; per-char escape table separate from `PieceWriter.Escape` | `NativeExpressionWriter.cs:269-305` (`:283-284` verified); `src/Heddle.Generator/Emit/PieceWriter.cs:23-52`, lone-surrogate guard only for the u8 twin at `:16, 54-72` — verified |
| The runtime never re-formats literals; decoder does first-fit integer typing and suffix-driven real typing | `src/Heddle/Language/Expressions/ExpressionAstBuilder.cs:293-347, 372-409` — verified |
| Runtime numeric tables and promotion; the two illegal mixes | `src/Heddle/Runtime/Expressions/NumericPromotion.cs:32-54` (table, verified), `:60-124` (`TryPromote`/`UnaryPromote`) |
| Generator's duplicate widening table agrees entry-for-entry today; no `TryPromote` counterpart | `src/Heddle.Generator/Emit/TemplateEmitter.cs:1567-1624` — verified; diff recorded in [04 F7](../research/generator-code-sharing/04-expression-writers.md) |
| Flat conversion rank / Pareto tier binding; `min(1, 2u)` tie | `src/Heddle/Runtime/Expressions/NativeExpressionCompiler.cs:387-519` (`ConversionRank` at `:493-519` verified); candidate rows with return types at `src/Heddle/Precompiled/DefaultFunctionTable.cs` (verified — `DefaultFunctionRow.ReturnTypeName`) |
| The false "by construction" overload claim in the generator | `NativeExpressionWriter.cs:116-118` — verified |
| Runtime member filter (`IsAssembly \|\| IsPublic`), `GetProperty`-based walk, "single source" doc claim | `src/Heddle/Runtime/Expressions/MemberPathResolver.cs:17-22, 61-93, 116-124` — verified |
| Generator member filter accepts `ProtectedOrInternal`; `[Hidden]` matched by unqualified name; base + `AllInterfaces` walk | `src/Heddle.Generator/Binding/SymbolTypeResolver.cs:153-234` (`:224-225`, `:229` verified) |
| Null-safe hop encodings on both sides; static-receiver `ArgumentException` path | `src/Heddle.Generator/Emit/MemberPathWriter.cs:33-53`; `src/Heddle/Runtime/Parameters/ModelParameter.cs:32-57` (`:41`) — verified |
| Dynamic binder context asymmetry | `src/Heddle/Runtime/Parameters/DynamicParameter.cs:21-44` (`:28` verified); `TemplateEmitter.cs:2150-2162` (verified) |
| Linked-source mechanism and the `Language/**` glob; `ExprOperator` already linked | `src/Heddle.Generator/Heddle.Generator.csproj:50-51` — verified; `src/Heddle/Language/Expressions/ExprOperator.cs` |
| Schema-version gate constants and fallback reason | `src/Heddle/Precompiled/PrecompiledTemplates.cs:20-21, 74-79`; `PrecompiledFallbackReason.SchemaVersionUnsupported` — verified |
| The seven deviations, the sandbox definition, null-safe `.`, literal set, overload-rank promise | [docs/native-expressions.md](../native-expressions.md) — deviations list, sandbox section, "Why there is no `?.`", literals table, registered-functions section |
| Findings F1–F9, verified divergences, and the area's sequencing | [04 — expression writers](../research/generator-code-sharing/04-expression-writers.md) |
| Tier model, live-drift table rows 4/5/9/14, cross-area layout and sequencing | [07 — recommendations](../research/generator-code-sharing/07-recommendations.md) |
| Fix-forward vs breaking-window rules; golden-change policy; test/gate requirements | [breaking-windows.md](../spec/common/breaking-windows.md), [coding-standards.md](../spec/common/coding-standards.md), [testing-standards.md](../spec/common/testing-standards.md) |
| `G17`/`G9` round-trip guarantee vs `"R"` unreliability on .NET Framework | Microsoft Learn — standard numeric format strings ("R" remarks; G17/G9 round-trip guidance) |
