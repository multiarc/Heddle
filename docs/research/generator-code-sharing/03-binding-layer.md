# Area 03 — Binding layer: `ExtensionBinder` + `Binding/*` (ISymbol) vs runtime reflection binding

**Generator side:** `src/Heddle.Generator/Emit/ExtensionBinder.cs` (363 lines), `Binding/SymbolTypeResolver.cs` (249), `Binding/SymbolMemberResolver.cs` (62), `Binding/FunctionExportResolver.cs` (140), plus the binding-consuming parts of `Emit/TemplateEmitter.cs`.
**Runtime side:** `src/Heddle/Runtime/Expressions/*` (`PropLayout`, `PropConversion`, `NumericPromotion`, `MemberPathResolver`, `FunctionRegistry`, `NativeExpressionCompiler`), `Runtime/TemplateFactory.cs`, `Helpers/TypeExtension.cs`, `Helpers/ReflectionHelper.cs`, `Precompiled/*`.

This is the area with the most "same rule, two type systems" duplication: the generator answers over `ISymbol` the same questions the runtime answers over `System.Type`. Findings ordered by drift risk.

**Coverage note:** findings 4, 5, 6, 7, and the render-type half of 9 are **not covered by `PrecompiledGauntlet`** — drift there produces silent wrong rendered output, not a fallback. Findings 1–3 are gauntlet-covered, so drift degrades to permanent silent un-precompilation (plus one false build error in finding 3).

---

## Finding 1 — AQN-sans-version formatting: the manifest's identity string produced by two unrelated formatters

**Rule.** Every manifest row the gauntlet compares against the live runtime (`PrecompiledExtensionBinding.ExtensionTypeName`, `PrecompiledFunctionBinding.TargetTypeName`) is `"<CLR full type name>, <assembly simple name>"`. The generator builds it from Roslyn's `FullyQualifiedFormat` string minus `global::` plus `ContainingAssembly.Identity.Name`; the runtime builds `type.FullName + ", " + type.Assembly.GetName().Name`. These agree **only** for non-nested, non-generic namespace types.

- Generator: `src/Heddle.Generator/Emit/ExtensionBinder.cs:211-216`; identical five lines re-typed at `Binding/FunctionExportResolver.cs:123-127`; third and fourth copies of the `global::` strip at `Emit/TemplateEmitter.cs:750-753, 790-793`; also `Emit/NativeExpressionWriter.cs:144`.
- Runtime: `src/Heddle/Precompiled/PrecompiledGauntlet.cs:207-212` (`AqnSansVersion`), used at `:87, :96, :141, :150-151`; pinned constant twin at `Precompiled/DefaultFunctionTable.cs:40`.

**Drift risk: highest.** For a nested type Roslyn yields `Ns.Outer.Inner` vs reflection's `Ns.Outer+Inner`; for a generic container `Ns.C<T>` vs ``Ns.C`1``. The strings never match → `ExtensionBindingMismatch`/`FunctionBindingMismatch` on **every render of every template** touching that extension — permanent silent fallback (or a hard `PrecompiledMismatchException` under a strict mismatch policy). The build warns about nothing.

**Extraction.** Shared rule-core with a tiny record: `AqnFormatter.Format(string ns, IReadOnlyList<string> nestingChainOutermostFirst, int arity, string assemblyName)` — zero Roslyn/reflection surface, links like `DefaultFunctionTable`. Each side's mapping to pieces is ~5 lines.

---

## Finding 2 — `[ExportFunctions]` discovery: container eligibility, method eligibility, name normalization, precedence/overload merge

**Rule (runtime).** Container must be a public static class *or the export throws*; methods are `Public|Static|DeclaredOnly`, `IsSpecialName` skipped, and each must pass `Register(string, MethodInfo)` — not an open generic, not `void`, no `ref`/`out`/pointer params, else `ArgumentException`; name = `Name.ToLowerInvariant()`; registration **merges** — a second container exporting the same name *adds overloads* (replace only on identical signature).

**Rule (generator).** Container skipped *silently* if not public static; every `MethodKind.Ordinary` public static member becomes a function; **first container to claim a name keeps it exclusively**; overload choice left to the C# compiler within that one container.

- Generator: `Binding/FunctionExportResolver.cs:99-138` (`AddContainer` — eligibility 101-103, method filter 108-118, name 115, precedence 129-137); assembly enumeration order `:54-80`; dependent emission at `Emit/NativeExpressionWriter.cs:117-151`.
- Runtime: `src/Heddle/Runtime/Expressions/FunctionRegistry.cs:127-156` (`RegisterContainer`), `:76-96` (`Register` — the four eligibility rejections with no generator counterpart), `:182-200` (`AddOrReplace` merge rule), `:113-125` (`RegisterFrom`); overload selection at `Runtime/Expressions/NativeExpressionCompiler.cs:329-361`.

**Drift risk: very high, partly not fallback-safe.**
- *Overload-count mismatch:* the generator's `overloadCounts` (`FunctionExportResolver.cs:117`) counts methods the runtime refuses to register. The gauntlet compares counts exactly (`PrecompiledGauntlet.cs:147-158`, fails on `>` and `<`) — one `void Log(string)` helper in an export container permanently un-precompiles every template calling any function from it.
- *Cross-container merge:* two containers exporting `slugify` merge at runtime; the generator binds only the first. Emitted C# may pick a different overload or fail to compile; `PrecompiledGauntlet.cs:136-145` sees a live target not in `recordedTargets` → permanent mismatch.
- *Silent-skip vs throw:* an ineligible container is a hard `ArgumentException` at runtime but a silent no-op in the generator (masks a host configuration error until first render).
- `MethodKind.Ordinary` vs `!IsSpecialName` are close but not identical relations — another independent transcription.

**Extraction.** Shared rule-core over an `ExportedMethodFacts` record (`isStatic, accessibility, isOpenGeneric, returnsVoid, hasByRefOrPointerParam, isSpecialName`), name rule, and a shared merge routine producing the manifest rows the gauntlet consumes. Worth doing first in this area — the gauntlet's numeric comparison makes any disagreement immediately load-bearing.

---

## Finding 3 — Extension discovery: what counts as an extension, name inheritance, registration precedence

**Rule (runtime).** `t.IsImplement<IExtension>()` (transitive) **and** `IsHaveAttribute<ExtensionNameAttribute>(true)` — *inherit: true*, so a subclass inherits its base's names; ordering by `[DataType]`/`[ChainedType]` interface-ness; `AddExtensions` sorts `Replace` last; on collision the incumbent is replaced when `Replace` or `incumbent.IsAssignableFrom(candidate)`, else `TemplateOverrideException`.

**Rule (generator).** Must be a non-abstract class *deriving from `AbstractExtension`* with a **declared-only** `[ExtensionName]`; first registration wins in assembly-enumeration order.

- Generator: `Emit/ExtensionBinder.cs:191-239` (`InspectType` — shape filter 196, name read 199-209, precedence 230-238), `:340-346` (`DerivesFrom`), `:162-166` (assembly order); consequence site `Emit/TemplateEmitter.cs:650-654` (HED7006 at Error severity, `Diagnostics/GeneratorDiagnostics.cs:44-48`).
- Runtime: `Runtime/TemplateFactory.cs:195-213` (`LoadExtensions`), `:84-114` (`AddExtensions` override policy), `:41-70` (`ObtainExtensions`); inherit-true attribute read `Helpers/TypeExtension.cs:82-88`; `Attributes/ExtensionNameAttribute.cs:8-9`.

**Drift risk: very high — includes a false build error.**
- A host extension implementing `IExtension` directly (not via `AbstractExtension`) registers at runtime but is **invisible** to `ExtensionBinder`; for a bodied call the emitter raises `HED7006` as an *error* — **the build breaks on a template the runtime renders fine**. The only non-fallback-safe drift in this area.
- Inherited `[ExtensionName]`: `class MyIf : IfExtension` registers under `"if"` at runtime and takes over the name via `IsAssignableFrom`; the generator's declared-only `GetAttributes()` (`:200`) misses it → manifest says `IfExtension`, live says `MyIf` → `ExtensionBindingMismatch` on every render. Note the file *does* base-chain-walk for `[BranchRole]`, `[ScopeChannel]`, `[Prop]` (`:249, :266, :285`) — the name read being declared-only looks like an oversight, not a decision.
- `[ExtensionReplace]` and the `IsAssignableFrom` override rule have no generator counterpart at all (acknowledged at `ExtensionBinder.cs:234-235`).
- Tie-breaks are unrelated: runtime `[DataType]`/`[ChainedType]` ordering vs generator assembly-enumeration order.

**Extraction.** Shared rule-core over an `ExtensionCandidate` record (name list, assembly, assignability-to-incumbent callback); the assignability edge needs Finding 6's `ITypeFacts`. The name-inheritance half is a trivial base-chain walk directly sharable once expressed over an abstract "layers, outermost first" sequence.

---

## Finding 4 — Extension `[Prop]` layout: decode, validation order, index assignment (silent wrong-output risk)

**Rule.** Walk the extension's base-type chain outermost-first; per layer, per declaration, in order: reject null/empty/whitespace name; reject `out`/`this` reserved names; reject a same-level duplicate; reject an unusable type; if the name exists from a base layer, keep the base's slot index, require the re-declared type assignable to the inherited one, re-apply the default; otherwise append a new slot at `slots.Count`. The resulting index order is the **wire format** between the generator's frozen `object[]` prototype / `string[] parameterNames` and the runtime's `ExtensionParameterCarrier` + `ExtensionParameterMap`.

- Generator: `Emit/ExtensionBinder.cs:274-338` (`ReadPropParameters`), `:24-59` (`PropParameter` incl. the `Level` channel); `Emit/TemplateEmitter.cs:824-935` (`ResolveExtensionPropLayout` — the validation/indexing twin); wire-format emitters `:800-810`, `:777-798`.
- Runtime: `Runtime/Expressions/PropLayout.cs:83-179` (`ResolveFromExtension`), `:254-285` (`ApplyDefault`), `:181-252` (`Resolve`, the definition-side twin); index-order consumers `Core/ExtensionParameterCarrier.cs:56-70`, `Data/ExtensionParameterMap.cs:15-24`, `Precompiled/PrecompiledPropSetter.cs:13-26`.

**Drift risk: very high and NOT gauntlet-covered.** There is no manifest row for prop layouts (`PrecompiledGauntlet.cs:21-56` checks options, extension identity, functions, staleness — nothing about parameter indices). If the two layer walks disagree about slot order, a precompiled template writes prop values into the **wrong slots** — silent wrong output, not a fallback. Already-visible differences: runtime stops the layer walk at `typeof(object)` (`PropLayout.cs:92`) while the generator walks unconditionally (`ExtensionBinder.cs:285`); runtime accumulates declaration errors and continues, generator breaks at first fault (`TemplateEmitter.cs:844-905`); unusable-type predicates differ (`PropLayout.cs:134` vs `ExtensionBinder.cs:304-307`); default-application order differs (`TemplateEmitter.cs:869-889`).

**Extraction.** The sequencing algorithm is type-agnostic once `IsUsableType`, `IsAssignableTo`, and default conversion are hoisted behind an interface: `PropLayoutCore.Build(IEnumerable<PropDecl<TType>>, ITypeFacts<TType>, IFaultSink)`. **Highest payoff in this area** — the only duplicated rule whose failure is wrong rendered output.

---

## Finding 5 — Prop-default conversion set and the implicit-numeric-widening table

**Rule (D10 rule 4).** Identity; boxing to `object`; implicit numeric widening; `T`→`T?` lift and widen-then-lift; `Nullable<S>`→`Nullable<W>`; reference assignability; null literal legal only for reference/`Nullable<T>` targets. Plus the value rule: a widened default is stored boxed **as the target's underlying type** (`Convert.ChangeType`), which the generator must reproduce as a C# cast so the boxed CLR type matches.

- Generator: `Emit/TemplateEmitter.cs:942-973` (`DefaultConvertible`), `:975-982` (`NullDefaultLegal`), `:1567-1602` (`IsImplicitNumericWidening` — the whole C# §10.2.3 table hand-transcribed over `SpecialType`, 36 comparisons), `:1504-1561` (`TryFormatPropValue`), `:1607-1620` (`NumericKeyword`); fed from `Emit/ExtensionBinder.cs:317-332`.
- Runtime: `Runtime/Expressions/PropConversion.cs:24-53` (`CanConvertTypes`), `:60-80` (`TryConvertLiteral` incl. null rule), `:87-100` (`ConvertValue`); `Runtime/Expressions/NumericPromotion.cs:32-54` (10-entry `ImplicitNumeric` dictionary); call site `PropLayout.cs:261-285`.

**Drift risk: high, not gauntlet-covered.** A disagreement changes the *contents* of the frozen prototype — different default value or a different boxed CLR type than `Convert.ChangeType` produces (observable via `is`/`as`/unbox in user extensions). Adding `nint`/`nuint` to one table is a one-line change nobody would mirror. Already-present asymmetry: `PropConversion.CanConvertTypes` allows `Nullable<S>`→`Nullable<W>` (`:44-46`); `DefaultConvertible` has no source-nullable branch (currently unreachable via attribute defaults).

**Extraction: mostly directly sharable data.** A shared `PrimitiveKind` enum + `PrimitiveConversions.IsImplicitNumeric(kind, kind)` table, linked like `DefaultFunctionTable`; runtime maps `Type`→kind, generator maps `SpecialType`→kind (12-case switch). `NullDefaultLegal` is directly sharable given two booleans. The reference-assignability arm needs Finding 6's abstraction. **Cheapest high-value extraction in the whole report.**

---

## Finding 6 — Assignability relation (`IsAssignableFrom`) reproduced over symbols

**Rule.** "Is the re-declared prop type assignable to the inherited one?" Runtime: `inherited.IsAssignableFrom(reDeclared)` verbatim. Generator: reconstructs the relation from `ClassifyConversion` plus two hand-written corrections where Roslyn's classification and the CLR relation disagree over nullables (`int`→`int?` assignable-but-`ImplicitNullable`; `int?`→`IComparable` boxing-in-Roslyn but not-assignable-at-runtime).

- Generator: `Emit/TemplateEmitter.cs:993-1025` (`RedeclarationAssignable`), `:965-971` (second partial encoding inside `DefaultConvertible`); `Binding/SymbolTypeResolver.cs:236-244` (`IsNonNullableValueType` — third spelling of the `Nullable<T>` test; uses `ConstructedFrom` where `TemplateEmitter` uses `OriginalDefinition`).
- Runtime: `Helpers/TypeExtension.cs:14-23` (`IsType`); call sites `PropLayout.cs:148, :223`; same relation again in registration precedence at `TemplateFactory.cs:99`.

**Drift risk: high** — same silent-wrong-layout consequence as Finding 4. The generator's doc comment enumerates the two corrections it knows about; a third disagreement (variance with value-type arguments, `ValueTuple` conversions) would only be found by differential testing.

**Extraction.** Cannot share imperative code — the relation *is* the type graph. Needs `ITypeFacts<TType>` (`IsAssignableFrom`, `TryGetNullableUnderlying`, `IsInterface`, `IsValueType`, `PrimitiveKind`); the Roslyn adapter is where the nullable corrections live, stated once next to the relation they correct.

---

## Finding 7 — Member-path resolution filter (properties only, visibility, `[Hidden]`, dynamic hop)

**Rule.** Walk dotted segments off a start type; a `dynamic` receiver ends the walk as a legal dynamic hop; otherwise find a *property* by exact ordinal name that is readable, not `[Hidden]`, getter public-or-internal; anything else fails (HED0001 dynamically, HED7008 at build).

- Generator: `Binding/SymbolTypeResolver.cs:153-188` (`ResolvePath`), `:190-217` (`FindProperty` — base + interface-inheritance walk), `:219-234` (`IsAccessible`); `Binding/SymbolMemberResolver.cs:45-60`; `Emit/TemplateEmitter.cs:2260-2286`; `Emit/NativeExpressionWriter.cs:153-180`.
- Runtime: `Runtime/Expressions/MemberPathResolver.cs:61-93` (`TryResolve`), `:116-124` (`IsAccessible`), `:100-113` (`GetVisibleProperties`); dynamic-ness via `Helpers/TypeExtension.cs:234-236`, `Data/ExType.cs:13-22`.

**Drift risk: medium-high, not gauntlet-covered.** Three verified disagreements, all in the direction "generator more permissive → precompiled succeeds where dynamic errors":
1. **`protected internal` getters:** generator accepts `Accessibility.ProtectedOrInternal` (`SymbolTypeResolver.cs:224-226`); reflection's `getter.IsAssembly || getter.IsPublic` (`MemberPathResolver.cs:123`) is false for `FamORAssem`.
2. **Interface-inheritance walk:** `FindProperty` searches `AllInterfaces` (`:204-214`); `Type.GetProperty` on an interface does not walk base interfaces.
3. **`[Hidden]` matched by unqualified name** (`SymbolTypeResolver.cs:229` — any `HiddenAttribute` from any namespace hides at build time); runtime matches the real `Heddle.Attributes.HiddenAttribute` (`MemberPathResolver.cs:120`).

The stated invariant at `SymbolMemberResolver.cs:13-16` ("symbol `Failed` implies runtime fails") holds for diagnostics — but the *emission* path uses the same resolver, so extra permissiveness becomes emitted typed code, not just a suppressed error.

**Extraction.** The walk (segment loop, dynamic-hop split, failure index) is directly sharable; the per-step lookup and accessibility predicate need `IMemberFacts<TType, TMember>`. The accessibility decision table is expressible over a shared `MemberAccess` enum both sides map into — which is where the `ProtectedOrInternal` divergence would have been forced into the open.

---

## Finding 8 — Model type-name resolution (`:: T`, prop type names, slot types)

**Rule.** Resolve a template-spelled type name against C# keyword aliases and the template's `@using` namespaces.

- Generator: `Binding/SymbolTypeResolver.cs:45-55` (`Keywords` table), `:57-101` (`ResolveModelType` — `?` suffix, keywords, metadata name, usings, implicit `System`/`System.Collections.Generic`), `:110-151` (`TypeNameExistsAnywhere` — the HED7007 guard that exists *because* the resolvers differ); call sites `TemplateEmitter.cs:139, :1393, :1470, :1872`.
- Runtime: `Helpers/ReflectionHelper.cs:32-49` (`CSharpTypes` keyword table — **includes `"dynamic"`, absent from the generator's**), `:291-315` (`ResolveType` — tuple/array/generic dispatch), `:162-246` (`ResolveSimpleType` — AQN with `.`→`+` retry, ambiguity errors), `:55-101` (`Reconfigure` — global name index); entry `Data/ExType.cs:30-39`.

**Drift risk: medium; mostly fallback-safe, one real edge.** The generator supports none of `List<int>` / `T[]` / `(int, string)` / dotted-nested spellings — those degrade safely but mean whole feature areas silently never precompile. The risky edge: the runtime resolves a bare short name by scanning every loaded assembly and **throws "ambiguous"** on ties (`ReflectionHelper.cs:235-241`); the generator picks the first `@using` match then falls back to implicit `System`/`System.Collections.Generic` — namespaces the runtime does *not* treat as implicit. `:: List` or a short name colliding with a `System` type can bind to **different types on the two tiers**, and emitted typed code then types member hops off the wrong type.

**Extraction.** Keyword table = directly sharable data (`string → PrimitiveKind`). The spelling parser (`ReflectionHelper.cs:319-410` — `ExtractGenericArguments`, `SplitTopLevelArguments`, `TryFindMatchingAngleBracket`) is **directly sharable imperative code with zero reflection in it** — linkable today, and would immediately give the generator generic/array/tuple model types. The lookup ordering/ambiguity policy is a rule-core over `ITypeLookup.TryResolve(metadataName)`.

---

## Finding 9 — Inherited-attribute reads: `[BranchRole]`, `[ScopeChannel]`, `[EncodeOutput]`, `[NotEncode]`, derived render type

**Rule.** Each attribute is `Inherited = true` → "does this type or any base carry it". Derived: render type = `HasEncodeOutput ? (HasNotEncode ? Raw : Encode) : Raw`; a `[ScopeChannel]` extension's body needs a locals frame; a Continuation/Terminal without `[ScopeChannel]` is the D-ROLE-5 drift warning.

- Generator: `Emit/ExtensionBinder.cs:10` (**a hand-mirrored `BranchRole` enum** duplicating `Heddle.Attributes.BranchRole`'s numeric values), `:241-257` (`ReadBranchRole` + range guard), `:259-272` (`HasAttribute` base-chain walk), `:116-117, :225-228, :131-134` (participant/drift → HED7016); `Emit/TemplateEmitter.cs:757-770` (`DerivedRenderTypeLiteral`).
- Runtime: `Helpers/TypeExtension.cs:64-88, :90-101`; `Runtime/HeddleCompiler.cs:1962-1965` (the exact render-type expression), `:300-325, :331-345` (D-ROLE-5 twin); `Runtime/RuntimeDocument.cs:168-178`.

**Drift risk: medium, with one silent-wrong-output path.** The render-type expression is duplicated verbatim; one-sided change means a precompiled `[EncodeOutput]` extension stops encoding or double-encodes — a **security-relevant** output difference with no manifest row and no gauntlet check. The `BranchRole` value mirror is a cross-assembly numeric contract enforced only by a comment.

**Extraction.** Predicates are pure over per-layer booleans → directly sharable rule-core. The `BranchRole` enum should simply be a **linked `<Compile Include>` of the runtime's enum**, exactly like `LinePosition`/`TemplateKey` — removes the mirror entirely; **the single cheapest fix in this report.**

---

## Extraction classes (this area)

**Directly sharable today (one `<Compile Include>` line each, no abstraction):**
- `Heddle.Attributes.BranchRole` enum (kills the mirror at `ExtensionBinder.cs:10`)
- Implicit-numeric-widening table + C# keyword-alias table over a shared `PrimitiveKind` (Findings 5, 8)
- The type-name spelling parser (`ReflectionHelper.cs:319-410`) (Finding 8)
- AQN formatting from `(namespace, nesting chain, arity, assembly)` (Finding 1)
- Reserved prop names / null-default legality / render-type derivation predicates (Findings 4, 5, 9)
- Export eligibility predicate + name normalization + merge bookkeeping over a facts record (Finding 2)

**Needs a shared rule-core with `ITypeFacts`/`IMemberFacts` adapters (reflection adapter in Heddle, Roslyn adapter in the generator, neither leaking into the shared file):**
- Prop-layout sequencing and index assignment (Finding 4) — highest payoff; disagreement = silent wrong output
- Assignability relation incl. nullable corrections (Finding 6)
- Member-path walk + accessibility decision table (Finding 7)
- Model type lookup ordering/ambiguity policy (Finding 8)
- Extension discovery predicate + precedence table (Finding 3)
