# Area 04 — `Emit/NativeExpressionWriter.cs` + `Emit/MemberPathWriter.cs` vs runtime expression compilation

**Generator side:** `src/Heddle.Generator/Emit/NativeExpressionWriter.cs` (307 lines), `Emit/MemberPathWriter.cs` (68 lines), plus the expression-adjacent seams in `Emit/TemplateEmitter.cs` and `Binding/SymbolTypeResolver.cs`.
**Runtime side:** `src/Heddle/Runtime/Expressions/NativeExpressionCompiler.cs` (1207 lines), `MemberPathResolver.cs`, `NumericPromotion.cs`, `Runtime/Parameters/ModelParameter.cs`, `DynamicParameter.cs`, `Language/Expressions/ExpressionAstBuilder.cs`; spec in `docs/native-expressions.md`.

Findings ordered by drift risk.

---

## Finding 1 — Member-path property-visibility filter + hierarchy walk: two hand-written resolvers, already divergent

**Rule.** A path segment binds to a *property* (never field/method), ordinal case-sensitive, readable, not `[Hidden]`, getter public-or-internal; resolution walks base types/interfaces; a `dynamic` receiver splits into a dynamic hop; anything else is property-not-found (HED0001). This filter is the sandbox boundary (`docs/native-expressions.md:209-218`) — and it exists as two independent implementations.

- Generator: `Binding/SymbolTypeResolver.cs:153-188` (`ResolvePath`), `:190-217` (`FindProperty` — base + `AllInterfaces` walk), `:219-234` (`IsAccessible`); consumed by `Emit/NativeExpressionWriter.cs:153-198` and `Emit/TemplateEmitter.cs:1990-2004, :2164-2177`.
- Runtime: `Runtime/Expressions/MemberPathResolver.cs:61-62` (BindingFlags), `:68-93` (`TryResolve`), `:116-124` (`IsAccessible`); called from `NativeExpressionCompiler.cs:189-195, :226-233`.

**Verified live divergences:**
1. **`protected internal` getter** — generator accepts (`Accessibility.ProtectedOrInternal`, `SymbolTypeResolver.cs:225-226`); runtime rejects (`getter.IsAssembly || getter.IsPublic`, `MemberPathResolver.cs:123` — `IsAssembly` is false for `FamilyOrAssembly`).
2. **Inherited non-public members** — generator walks `BaseType` manually and accepts an `internal` property on a base class; `Type.GetProperty(name, flags)` doesn't surface inherited non-public properties → runtime says not-found.
3. **Interface hierarchies** — generator searches `AllInterfaces` (`:204-214`); reflection `GetProperty` on an interface doesn't search base interfaces → runtime fails on `IDerived : IBase` members.
4. **Statics** — both include static properties, but the runtime then throws `ArgumentException` from `Expression.MakeMemberAccess` (`ModelParameter.cs:41/49`, not a positioned error) while the generator emits `m.StaticProp` → CS0176 in the *consumer's* build. Different failure modes for the same template.
5. **`[Hidden]` matched by unqualified name** on the generator side (`SymbolTypeResolver.cs:229`) vs the real attribute type on the runtime side (`MemberPathResolver.cs:120`).
6. **Ambiguity** — `Type.GetProperty` can throw `AmbiguousMatchException` on `new`-shadowed properties; the generator deterministically takes the most-derived.

**Drift risk: highest.** Divergences 1–3 make the generator emit typed, compiling code for a member the runtime declares nonexistent — the precompiled template renders while the runtime-compiled one raises HED0001 (or vice versa). Not merely a byte-parity break: it changes whether the template compiles at all, and it silently bypasses the documented sandbox visibility contract. `MemberPathResolver`'s doc comment (`:17-22`) claims to be "the single source of member-path resolution semantics" — the generator is a second source.

**Extraction: shared spec-driven core + per-side adapters.** The decision logic is pure booleans; only the fact source is non-portable. A linked netstandard2.0 file: `enum MemberAccessibility`, `struct MemberFacts(canRead, accessibility, hasHidden, isStatic)`, `MemberVisibility.IsAccessible(in MemberFacts)`, plus a generic `MemberPathWalk<TType>(start, segments, ITypeModel<TType>)` where `ITypeModel` exposes `IsDynamic`/`TryFindProperty`/`BaseOf`/`Interfaces`. The base/interface walk order then lives once. (Corroborated by area 03, Finding 7.)

---

## Finding 2 — The "deviations from C#" set: runtime implements it by hand; the generator delegates to the consumer's compiler with no guard

**Rule.** `docs/native-expressions.md:194-207` lists seven points where the native tier deliberately does *not* match C#. The runtime implements every one explicitly. The generator's design premise (`NativeExpressionWriter.cs:11-18` — "the native tier is a strict C# subset, so operators emit verbatim") is only true where those deviations don't bite — and there is **no check for any of them**: `WriteBinary` (`:218-228`) emits `(left op right)` unconditionally.

- Generator: `NativeExpressionWriter.cs:90-109` (dispatch), `:200-216` (unary), `:218-238` (binary/ternary) — no operand typing consulted anywhere except member paths.
- Runtime: `NativeExpressionCompiler.cs:724-755` (arithmetic + string concat), `:778-798` (shift), `:800-836` (relational), `:838-888` (equality incl. `object.Equals` fallback `:879-884`), `:890-927` (bitwise incl. enum rules `:899-906`), `:929-951` (logical), `:953-994` (coalesce), `:1000-1087` (ternary arm unification).

**Verified concrete mismatches:**
- *Deviation 1* (`==`/`!=` on unrelated/mixed types → total `object.Equals`): generated `(a == b)` → **CS0019 in the consumer's build** — the generator breaks the user's compile for a template the runtime accepts.
- *Deviation 4* (enum arithmetic unsupported): runtime fails with a positioned error; generated `(EnumProp + 1)` is legal C# → renders. Opposite verdicts.
- *Deviation 5* (`enum & 0` literal case): runtime fails; C# accepts → renders.
- *Deviation 6* (user-defined implicit conversions not consulted): runtime promotion never sees them; the consumer's compiler does → different overload/result type.
- *Deviation 2* is the one the generator does implement (Finding 4 below).

**Drift risk: very high, asymmetric.** Half the cases silently render differently (byte-parity break); the other half turn a working template into a consumer build error — the worst failure mode for a source generator, because the degrade-to-dynamic escape hatch is never taken.

**Extraction: shared rule-table core (the highest-value extraction in this area).** Operator legality is expressible over a small kind lattice, not `Type`/`ITypeSymbol`: extract `NumericPromotion` into a shared `NumericKind`-keyed table (Finding 7), then a shared `NativeOperatorRules.Classify(ExprOperator, NumericKind left, NumericKind right, …) → Supported | RequiresRuntimeSemantics | NotDefined`. Runtime keeps building `Expression` from the classification; the generator consults the same table and returns null on non-`Supported` → degrades instead of emitting divergent or uncompilable C#. `ExprOperator` is already linked, so the table can live beside it with zero new plumbing.

---

## Finding 3 — Function overload selection: shared candidate table, two different resolution algorithms

**Rule.** A call binds against the registry's candidates; ranking is exact < widening < boxing-to-object, params-expanded second tier; ties are ambiguity errors. The *candidate set* is genuinely shared (`DefaultFunctionTable.Rows`, linked; mirrored 1:1 by `PrecompiledFunctions`); the *selection rule* is not.

- Generator: `NativeExpressionWriter.cs:111-151` — emits the call and delegates resolution to the consumer's C# compiler; the comment at `:116-118` asserts the C# result reproduces the runtime rank "by construction (differential-gated)".
- Runtime: `NativeExpressionCompiler.cs:389-398` (`BindOverload`), `:400-429` (`BindTier` + Pareto non-domination), `:431-443` (`Dominates`), `:445-490` (`TryRank`), `:493-519` (`ConversionRank`), `:526-546`.

**The assertion is false in general:** the runtime's rank vector is *flat* (all widenings rank 1, `ConversionRank:507-517`) while C# betterness prefers the closest target. With the shipped table (`DefaultFunctionTable.cs:68-89`): `min(1, 2u)` → runtime candidates `(long,long)`, `(double,double)`, `(decimal,decimal)` all rank `(1,1)` → non-dominated set of 3 → **ambiguity error**, while C# picks `Min(long,long)` and the generated code renders. Any host overload set with several widening-reachable targets hits the same class.

**Drift risk: high** for user-registered overloads (renders precompiled but errors at runtime, or picks a different overload → different numeric result). Currently masked for built-ins by the sample-based differential tests, not by construction.

**Extraction: shared rule-core.** `ConversionRank`/`TryRank`/`Dominates` are pure integer logic over parameter-type descriptors; recast over the shared `NumericKind` + a `TypeRef` descriptor (`DefaultFunctionRow` already carries metadata-name strings) and link. The generator uses it *as a guard*: resolve with the shared ranker, fall back when ambiguous or when the C#-visible candidate list can't be proven identical. Note the shared core must encode the **Heddle** rank, not C#'s.

---

## Finding 4 — Null-safe member-hop form (`default(T)` on a null receiver)

**Rule.** A hop off a value-typed receiver accesses directly; off a reference receiver it yields `default(propertyType)` when the receiver is null; there is no `?.` in the language because `.` is already null-safe (`docs/native-expressions.md:65-70`). Two hand-maintained encodings of one three-branch decision.

- Generator: `Emit/MemberPathWriter.cs:33-53` (branches `:38-49`), fed by `HopEmit` facts at `NativeExpressionWriter.cs:187-197`, duplicated at `TemplateEmitter.cs:2164-2177`; value/nullable classification at `SymbolTypeResolver.cs:236-244`.
- Runtime: `Runtime/Parameters/ModelParameter.cs:27-57` (`BuildNullSafePropertyChain`, branches `:39-50`), called from `NativeExpressionCompiler.cs:195, :233`.

The generator adds a fourth, runtime-absent case (splitting the reference branch into `?.` vs explicit conditional, `MemberPathWriter.cs:42-48`). The equivalence argument is sound today — but it is an *argument*, not shared code; both files' doc comments cross-reference each other by name (`MemberPathWriter.cs:6-12`), the classic sign of a rule that wants to be one artifact. Both sides also evaluate the receiver twice — matching *by coincidence of two independent choices*.

**Drift risk: high.** Any change to hop semantics must land in two files with unrelated shapes; a one-sided change silently changes rendered bytes for every null-containing model.

**Extraction: easy shared rule core.** `enum HopForm { Direct, NullConditional, NullDefaultConditional }` + `MemberHopRule.Form(bool receiverIsValueType, bool propertyIsNonNullableValueType)` in a linked file; runtime maps to `Expression`, generator maps to text. Fully Roslyn-free.

---

## Finding 5 — Operator symbol table + supported-operator whitelist: three copies of one list

**Rule.** `ExprOperator` X renders as C# lexeme Y; exactly these 19 binary / 4 unary operators are supported.

- Generator: `NativeExpressionWriter.cs:240-265` (`BinarySymbol`, `default: null` = unsupported), `:205-213` (unary `! - + ~`).
- Runtime: `NativeExpressionCompiler.cs:1153-1175` (`Symbol` — used for *error text*; lacks `AndAlso`/`OrElse`/`Coalesce`), dispatch `:684-722`, unary lexemes inlined at `:626, :632, :641, :664`, logical `:948`, coalesce `:964`.
- Third copy of the set: `Language/Expressions/ExprOperator.cs:7-34` (linked) + token mapping `ExpressionAstBuilder.cs:261-284`.

**Drift risk: medium-high.** Adding an operator = four touch points. The asymmetry hides drift: missing the generator's `BinarySymbol` = silent fallback; missing the runtime's `Symbol` = garbled error text only. A wrong lexeme in the generator (`&`/`&&` transposed) is a **silent miscompile** — valid C#, different semantics.

**Extraction: directly sharable, trivial.** A pure `OperatorLexeme.For(ExprOperator)` next to `ExprOperator.cs` — the `Heddle/Language/**` glob in `Heddle.Generator.csproj` picks it up with **zero csproj change**. Both sides call it; "supported" derives from the same table.

---

## Finding 6 — Literal → C# literal round-trip: inverse of the shared decoder, re-implemented on one side

**Rule.** A decoded literal round-trips to a C# literal preserving exact CLR type and value: suffixes `U/L/UL/F/D/M`, invariant culture, C# escape set for `string`/`char`.

- Generator: `NativeExpressionWriter.cs:269-288` (`LiteralFormatter.Format`), `:290-305` (`EscapeChar`), string case via `PieceWriter.Escape` (`Emit/PieceWriter.cs:23-52`); also used for prop defaults at `TemplateEmitter.cs:1531`.
- Runtime side of the rule: the *decoder* it must invert — `Language/Expressions/ExpressionAstBuilder.cs:293-347` (integer first-fit `int→uint→long→ulong`), `:372-409` (real: `F/D/M`/bare-double), `:246-259` (`Negate`), plus `DecodeString`/`DecodeChar`. The runtime consumer (`NativeExpressionCompiler.cs:138-145`) keeps the boxed value and never re-formats. Spec: `docs/native-expressions.md:72-87`.

**Two verified hazards:**
1. `float`/`double` use `ToString("R")` (`:283-284`). The generator runs inside the compiler process; under a .NET Framework host (VS/desktop `VBCSCompiler`) `"R"` is the well-known non-round-tripping format — a `double` literal can re-parse one ULP off. The runtime never re-formats → the precompiled expression computes a different value for the identical template, **non-reproducible across build hosts**, evading the differential harness unless a corpus template happens to hit a non-representable double. `G17`/`G9` is the correct inverse.
2. `EscapeChar` and `PieceWriter.Escape` are two escape tables in the same assembly that disagree (`\a \b \f \v` handled in one, `\u000x` in the other); the third copy is the decoder's escape set.

**Drift risk: medium-high** (silent numeric divergence, hard to reproduce). **Extraction: directly sharable.** `LiteralFormatter` is pure `object → string`, netstandard2.0-clean. Move under `Heddle/Language/Expressions/` (auto-linked) as the documented inverse of the decoder, and give the runtime a use for it (round-trip tests) so a decoder change breaks a shared test rather than only generated code.

---

## Finding 7 — C# implicit-numeric-conversion table duplicated in the emitter

- Generator: `TemplateEmitter.cs:1567-1605` (`IsImplicitNumericWidening` over `SpecialType`) + `:1607-1620` (`NumericKeyword`), used at `:1548-1560, :1790-1791`.
- Runtime: `Runtime/Expressions/NumericPromotion.cs:32-54` (`ImplicitNumeric` table), consumed by `ConversionRank` (`NativeExpressionCompiler.cs:507-517`) and `PropConversion`.

Diffed entry-by-entry: the tables **agree today** (incl. the `char` row and `float→double`). But `NumericPromotion.TryPromote` (`:60-115`) and `UnaryPromote` (`:118-124`) have **no** generator counterpart — which is exactly why Finding 2 exists.

**Drift risk: medium.** A one-sided edit changes which prop defaults/dynamic args precompile → boxed CLR type differs from `Convert.ChangeType` → parity break in prop-typed output.

**Extraction: easy shared table.** `enum NumericKind` + re-keyed table; `Type→kind` and `SpecialType→kind` adapters. **This single move unlocks Findings 2 and 3 — land it first.**

---

## Finding 8 — Dynamic-tier null-propagating chain

- Generator: `TemplateEmitter.cs:2150-2162` (`WriteDynamicPath`: `m == null ? (object)null : (object)(((dynamic)m).A?.B…)`) — doc comment claims byte-identity with `DynamicParameter`.
- Runtime: `Runtime/Parameters/DynamicParameter.cs:21-43` — per-hop `Condition(input == null, null, Dynamic(GetMember…))`.

Equivalent guard shapes, but the **binder context type differs**: the runtime passes `typeof(DynamicParameter)` (internal to `Heddle`, `:27`) while generated `(dynamic)` binds in the consumer assembly's context. For a model with an `internal` property the two tiers resolve differently (value on one side, `RuntimeBinderException`/null on the other).

**Drift risk: medium** (dynamic tier is the fallback path — it's what a degraded template renders). **Extraction: not code-sharable.** Best available: shared spec note + corpus test; or better, route generated dynamic hops through a small public `PrecompiledRuntime.DynamicMember(object, string)` helper in `Heddle`, so the binder-context choice exists **once** and the accessibility divergence disappears entirely.

---

## Finding 9 — Escape-set triplication (lower priority, generator-internal)

`NativeExpressionWriter.cs:290-305` (char), `PieceWriter.cs:23-52` (string), and the decoder's escape set (`docs/native-expressions.md:82`, `ExpressionAstBuilder.DecodeString/DecodeChar`) are three tables for one alphabet. Neither generator table guards lone surrogates in a *literal* (the guard exists only for the u8 twin, `PieceWriter.cs:16, :54-72`) — a lone-surrogate `char`/`string` literal is emitted raw into generated source. **Risk: low-medium** (exotic literals). **Extraction: directly sharable** — one `CSharpEscape` helper in the shared Language folder.

---

## Suggested sequencing (this area)

All of the following are Roslyn-free, netstandard2.0-clean, and — if placed under `src/Heddle/Language/**` — picked up by the existing csproj glob with no csproj edit:

1. `OperatorLexeme` (F5) — trivial; removes a silent-miscompile surface.
2. `NumericKind` + re-keyed promotion/conversion tables (F7) — the enabler.
3. `NativeOperatorRules.Classify` guard (F2) — biggest correctness win; the generator gains a real "degrade instead of emit" test for the deviation set.
4. `MemberFacts`/`MemberVisibility` + generic path walker (F1) — fixes three live divergences.
5. `MemberHopRule.Form` (F4); `LiteralFormatter` relocation + `G17`/`G9` fix (F6).
6. Shared rank core as a generator-side guard (F3); `PrecompiledRuntime.DynamicMember` (F8).

Findings 3 and 8 need a *behavior decision*; 1, 2, 4, 5, 6, 7, 9 are mechanical extractions under the stated constraints.
