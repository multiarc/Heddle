# Area 01 — `Emit/TemplateEmitter.cs` vs runtime rendering, encoding, and compilation

**Generator side:** `src/Heddle.Generator/Emit/TemplateEmitter.cs` (2618 lines — the main code emitter).
**Runtime side:** `src/Heddle/Runtime/HeddleCompiler.cs`, `Runtime/RuntimeDocument.cs`, `Runtime/Expressions/*`, `Extensions/*`, `Data/*`, `Precompiled/*`.

Kind legend used below:
- **(A) Same rule, different representation** — `ISymbol` vs `Type`, emitted C# text vs executed code. Imperative code can't be shared; needs a shared *rule table / spec* (data + a small pure predicate over a neutral abstraction).
- **(B) Same rule, same representation** — genuinely sharable as a linked `<Compile>` item today (operates on strings, already-linked parse-tree types, or plain enums).

Findings ordered by drift risk.

---

## Finding 1 — HTML-encode decision for the unnamed carrier + the `@profile()` flip (highest risk: encoding correctness)

**Rule.** For a bodiless unnamed `@(...)` carrier, the effective output profile at that document position decides both the bound extension type and render type: `Html` → `EmptyHtmlExtension` + `RenderType.Encode`; `Text` → `EmptyExtension` + `RenderType.Raw`. A bodied `@(X){{…}}` is a raw rescoping container, never redirected. The running profile starts at the compile-time `OutputProfile` and flips in document order at each `@profile(){{html|text}}` (ordinal-case-insensitive match on exactly `"text"`/`"html"`; unknown value is an error, no flip).

- Generator: `TemplateEmitter.cs:121` (`IsHtml`), `:123-128, :132`, `:349-404` (save/restore per body walk at `:353-355, :368-369, :402`), `:406-435` (`MapProfilePerChain`), `:510-522` (bodiless-vs-bodied decision), `:2179-2202` (`AllocateEmptyExtension` — type + `RenderType` literal + manifest binding name `"html"` vs `""`).
- Runtime: `src/Heddle/Runtime/HeddleCompiler.cs:1460-1474` (`UnnamedCarrierName`), `:1565-1567`; `src/Heddle/Extensions/ProfileExtension.cs:19-53` (the flip + string match + unknown-value error); `Runtime/CompileContext.cs:131,143,163,217-221` (profile lineage); `Extensions/EmptyHtmlExtension.cs:7-8`.

**Drift risk: highest.** The only rule in the file that directly decides *whether output is HTML-encoded*. Divergence silently produces unencoded output on the precompiled tier (an XSS-class regression) or double/missing entities. Structural difference already visible: the runtime mutates `CompileContext.OutputProfile` in place per child context; the emitter does explicit save/restore plus a per-chain map — any future change to whether a nested body's flip leaks to later siblings lands on one side only.

**Extraction: partially (B).** The pure parts — `TryParseProfile(string, out OutputProfile)` and `ResolveUnnamedCarrier(OutputProfile, bool hasBody) → (name, RenderType)` — are string+enum logic, sharable in a linked `Data/OutputProfileRules.cs`. `OutputProfile`/`RenderType` are trivial enums, **not linked today** — link them. The plumbing (per-chain map vs context lineage) stays (A) and needs a written ordering spec.

Related divergence (also see [06-diagnostics-utilities.md](06-diagnostics-utilities.md) F8): the runtime raises HED2001 on an unknown `@profile` value; the emitter's scan at `:418-427` **silently ignores** it and keeps the running profile.

---

## Finding 2 — Static-piece / literal coalescing (offset walk) and the Render/Execute strategy shape

**Rule.** A body is a document-ordered alternation of literal pieces and processors: walk elements by position, emit `document.Substring(offset, start - offset)` when an element starts past the running offset, then the processor, advance offset; emit the tail. Render path writes pieces straight to the renderer; value path coerces each processor result with `as string ?? string.Empty` and concatenates in order.

- Generator: `TemplateEmitter.cs:357-404` (walk; head piece `:365-366`, advance `:396`, tail `:399-400`), `:437-442` (`AddPiece`), `:2402-2493` (`EmitBodyClass` — render `:2416-2441`, execute `:2455-2486`; coercion at `:2475`, `string.Concat`/single/empty cases `:2481-2486`).
- Runtime: `Runtime/RuntimeDocument.cs:95-130` (`GetDocumentPieces` — the identical walk), `:51-93` (`OptimizeCallTree`), `:234-342` (the four strategies; the `as string ?? string.Empty` rail at `:243, :284, :318`).

**Drift risk: very high** — every byte of every template flows through this. Asymmetries: `NormalStrategy.Execute` (`:318`) uses `element.Processor?.ProcessData(scope) as string ?? element.Piece ?? string.Empty` while the generated `Execute` inlines `P{n}` constants; the runtime's `canDoFullOptimize`/`DocumentStrategy` short-circuits (`:59-91, :251-266`) have no generator twin. `OutExtension.cs:128-133` explicitly flags the non-string-drop rail as slated to change — generated `Execute` bodies would keep the old behavior.

**Extraction.** The offset walk is **(B)** and the single most mechanically sharable item in the file: pure `(string, spans) → segments`; `BlockPosition` is already linked. The strategy shape (coercion rail, concat order) is **(A)** — pin as spec + differential test.

---

## Finding 3 — Body model-typing table for built-in body-hosting extensions

**Rule.** Which model a nested body is typed by, per host: `@if`/`@ifnot`/`@elif`/`@elseif`/`@else` → enclosing model; `@for` → enclosing model with `int` on the chained channel; `@list` → the `IEnumerable<T>` element type (dynamic when non-generic/dynamic); definition body → its `:: T`; caller content → `slotType ?? dataType`; region body → declared `:: T` or (bare) the enclosing model, borrowing the enclosing component's prop layout.

- Generator: `TemplateEmitter.cs:545-569` (branch trio), `:571-600` (`@list`; body forced dynamic at `:587-588`), `:602-622` (`@for`), `:1079-1096` + `:1383-1402` (`DefinitionBodyContext`), `:1112-1135` (caller content), `:1341-1381` (`TryRegionBodyContext`), `:1860-1876` (`SlotBodyContext`).
- Runtime: `Extensions/IfExtension.cs:15-18`, `Extensions/ForIndexExtension.cs:13-16`, `Extensions/ListExtension.cs:36-53` (element-type derivation), `Runtime/HeddleCompiler.cs:1482-1500, :1539-1543, :1545-1561`.

**Drift risk: high.** A mistyped body changes which member/overload/conversion the emitted C# binds → different rendered value. The generator's copy is pinned knowledge guarded only by "engine assembly" checks (`:548-550, :670-687`); a change to `ListExtension`'s element-type derivation silently keeps the old generator typing.

**Extraction: (A), strongly.** The sharable artifact is a rule table — `extensionName → (BodyModelSource, ChainedModelSource)` with `BodyModelSource ∈ {Parent, Data, ElementOfData, Declared, SlotOrData}` — a dependency-free linked enum/dictionary; the two resolvers stay per-side. Highest-value spec extraction in the file; the rule currently exists only as prose comments (`:546-547, :573-576, :604-606`).

---

## Finding 4 — `RenderType` derivation from `[EncodeOutput]` / `[NotEncode]`

**Rule.** `HasEncodeOutput ? (HasNotEncode ? Raw : Encode) : Raw`, over the concrete extension type with attribute inheritance, applied to the **inner** extension (before parameter-carrier wrap).

- Generator: `TemplateEmitter.cs:758-770` (`DerivedRenderTypeLiteral`), applied `:746, :788`; carrier-transparency note `:772-776`.
- Runtime: `HeddleCompiler.cs:1963-1965` (identical ternary), consumed via `Core/AbstractExtension.cs:69-72`; wrap ordering `HeddleCompiler.cs:1595-1614`; `Precompiled/PrecompiledRuntime.cs:77-100`.

**Drift risk: high** — same XSS-class blast radius as Finding 1, scoped to custom `[EncodeOutput]` extensions. **Extraction:** the decision is a two-bool truth table → linked `Data/RenderTypeRules.cs` with `static RenderType Derive(bool, bool)` — cheap genuine win. (`RenderType` itself is 6 lines and not linked; the generator emits it as a string literal.)

---

## Finding 5 — Definition prop-layout resolution (base chain, index stability, re-declaration)

**Rule.** Walk the definition's base chain outermost-first; each layer's `PropDeclarations` in order; new name appends a slot at `slots.Count`; a re-declared inherited name **keeps the base index** and re-types/re-defaults.

- Generator: `TemplateEmitter.cs:1426-1445` (`PropSlotInfo`/`PropLayoutInfo`), `:1447-1499` (`ResolvePropLayout`, cache key `name + "@" + Position` at `:1454`).
- Runtime: `Runtime/Expressions/PropLayout.cs:13-21, :181-252` (`Resolve`), cached at `HeddleCompiler.cs:1647-1658` with the identical key (`:1650`).

**Drift risk: high.** Slot indices are the wire format between the generated `object[] PropsN` prototype / `PrecompiledPropSetter(index, …)` and `PrecompiledRuntime.Prop(in scope, index)` (`PrecompiledRuntime.cs:129-135`). An ordering change doesn't fail to compile — it silently reads the **wrong prop value** at render. Existing asymmetry: runtime reports HED5010 and continues (`PropLayout.cs:203-217`); generator sets `Failed` and degrades (`TemplateEmitter.cs:1471-1475, :1054`) — compatible only because "failed ⇒ don't precompile".

**Extraction: (A) but very close to (B).** Everything except type resolution is identical bookkeeping over `DefinitionItem`/`PropDeclaration` — already-linked types. A shared `PropLayoutCore<TType>` parameterized by a resolver and an assignability predicate eliminates the index-ordering duplication outright. Recommended. (Same solution as Finding 6 and area 03's Finding 4.)

---

## Finding 6 — Extension `[Prop]` layout + the malformed-declaration fault table

**Rule.** Same outermost-first walk over attribute-sourced parameters, with a fixed *ordered* fault list: (1) null/empty name → HED5015; (2) reserved `out`/`this` → HED5007; (3) same-level duplicate → HED5007; (4) unusable type → HED5010; (5) re-declaration not assignable → HED5008; (6) default not convertible → HED5009.

- Generator: `TemplateEmitter.cs:812-815` (cache), `:817-934` (`ResolveExtensionPropLayout` — faults at `:842-848, :850-854, :856-860, :862-866, :868-877, :879-891/:900-912`), collapsed into one HED7017 (`:924-930`).
- Runtime: `PropLayout.cs:83-179` (`ResolveFromExtension` — identical ordered checks `:106-112, :115-121, :123-129, :134-140, :144-154`; defaults via `:261-285`), cached `HeddleCompiler.cs:1627-1640`. The unusable-type predicate lives in a **third** place: `ExtensionBinder`'s precomputed `TypeUnusable` flag (`TemplateEmitter.cs:862`) vs `PropLayout.cs:134`.

**Drift risk: high.** Generator collapses six runtime diagnostic IDs into one HED7017 free-text fault. Runtime *continues* past faults and still produces a layout; generator *breaks* and returns null — for a two-fault extension the sides don't agree how many things are wrong.

**Extraction: (A).** Same `PropLayoutCore<TType>` vehicle as Finding 5, plus a linked fault enum (`PropDeclarationFault { NullName, ReservedName, Duplicate, UnusableType, RedeclarationWidens, DefaultNotConvertible }`) so HED7017's message and the runtime's ID mapping derive from one list (`HeddleDiagnosticIds` is already linked).

---

## Finding 7 — Prop-default conversion: implicit-numeric table, null-default rule, boxed-value reproduction

**Rule.** (a) The C# §10.2.3 implicit numeric conversion set. (b) Convertibility of a default literal to the declared type (identity; box-to-object; widening; `T→T?` lift and widen-then-lift; `S?→W?`; reference assignability). (c) A widening **changes the boxed value's CLR type** (`Convert.ChangeType(value, targetUnderlying, InvariantCulture)`), which the generator must reproduce as an emitted cast. (d) `null` default legal only for reference/`Nullable<T>` targets.

- Generator: `TemplateEmitter.cs:1565-1605` (`IsImplicitNumericWidening` — full table over `SpecialType`), `:1607-1624` (`NumericKeyword`), `:1626-1642`, `:936-974` (`DefaultConvertible`), `:976-983` (`NullDefaultLegal`), `:1501-1563` (`TryFormatPropValue` — `"(" + keyword + ")(" + literal + ")"` at `:1554-1559`); literal rendering in `Emit/NativeExpressionWriter.cs:268-305`.
- Runtime: `Runtime/Expressions/NumericPromotion.cs:31-54` (same table over `Type`), `Runtime/Expressions/PropConversion.cs:24-53, :60-80, :87-100` (`Convert.ChangeType` at `:96`).

**Drift risk: high and subtle.** A missing generator row = safe over-refusal; an *extra* row = the generator emits a cast the runtime would reject, unreported. Internal inconsistencies already present: the generator's `DefaultConvertible` **omits the `S?→W?` row** the runtime has (`PropConversion.cs:44-46`), and its nullable-underlying probe uses `ConstructedFrom` in one place (`:1511-1514, :1826-1828`) and `OriginalDefinition` in another (`:952-955`).

**Extraction.** The **table is (B)** — pure data keyed by a neutral `PrimitiveKind` enum with `Type↔kind` / `SpecialType↔kind` adapters; share immediately. The predicates are (A) (reference assignability differs per side). `LiteralFormatter` has no runtime twin (runtime never re-emits literals) — see area 04 for its round-trip hazard.

---

## Finding 8 — `IsAssignableFrom` vs `ClassifyConversion`: the re-declaration assignability relation

**Rule.** Re-declared prop type must be assignable under **reflection's** `IsAssignableFrom`, which differs from Roslyn's conversion classification in two documented places (`int→int?`; `int?→IComparable`).

- Generator: `TemplateEmitter.cs:985-1027` (`RedeclarationAssignable`, corrections at `:1006-1011, :1019-1024`).
- Runtime: `PropLayout.cs:148, :223` → `Helpers/TypeExtension.cs:13-23` — one line of BCL behavior vs a 40-line hand-written emulation.

**Drift risk: medium-high.** Failure mode: generator accepts what the runtime rejects (build green, runtime HED5008) or vice versa. **Extraction: (A), not sharable as code** — the right artifact is a **shared conformance test corpus**: a table of `(source, target, expected)` triples driven from one data file by both a reflection-side and a symbol-side test.

---

## Finding 9 — Call-site prop binding: prototype, dynamic setters, argument fault set

**Rule.** Per call site: reject duplicate names; reject unknown names; constants convert into a frozen prototype slot; non-constants become per-invocation setters; every unbound slot needs a default ("missing required prop"); a widening dynamic argument gets a boxed-value converter.

- Generator: `TemplateEmitter.cs:1671-1763` (`TryBuildPropsPrototype` — duplicate `:1704`, unknown `:1705`, constant `:1707-1718`, setter `:1720-1726`, missing-required `:1733-1737`, defaults `:1739-1746`, emitted arrays `:1748-1760`), `:1765-1822` (`TryBuildDynamicSetter`, widening `:1785-1797`), prop-less guards `:697-704, :1684-1693`.
- Runtime: `HeddleCompiler.cs:1783-1888` (`BindProps` — HED5001/5002/5003/5004), `:1895-1905` (`BuildNumericConvert`), `Runtime/PropsBinder.cs` (whole file), `:1503-1514` + `:840-864` (HED5005 guards).

**Drift risk: medium-high** (prototype/setter indices are the wire format — see Finding 5). **Extraction: (A)** — fault set + ordering sharable as spec/enum (same vehicle as Finding 6); evaluation is not (expression trees vs static methods).

---

## Finding 10 — Model-parameter resolution: prop-first rule, dynamic path, root refs, empty parameter

**Rule.** (1) Empty path = `scope.ModelData`; (2) unless `::`-rooted, a first segment naming a slot in the active prop layout wins over the model (single segment = direct slot load; multi-hop casts the boxed slot and continues); (3) dynamic tier null-propagates per hop with a leading guard; (4) otherwise static resolution against the model type.

- Generator: `TemplateEmitter.cs:1925-2054` (`BuildParamExpr` — empty `:1936-1941`, prop-first `:1943-1975`, dynamic `:1977-1985`, typed `:1987-2004`), `:2150-2162` (`WriteDynamicPath`), `:2164-2177` (`MapHops`), `Emit/MemberPathWriter.cs`.
- Runtime: `HeddleCompiler.cs:947-968, :1040-1113` (`CompileModelAccessor`), `:1115-1161` (`TryCompilePropRead`), `Runtime/Parameters/DynamicParameter.cs:21-44`, `Runtime/Parameters/ModelParameter.cs:82-112`, `PropsSlotParameter.cs:130-156`, `EmptyParameter.cs:163-174`.

**Drift risk: medium-high** — this decides *which value* reaches every extension. Null-propagation shapes differ by construction (runtime yields `default(TProperty)`; emitted dynamic path yields `(object)null`; `PropsSlotParameter` yields the raw boxed slot on a null prefix). Comments say "differential-gated" (`:1943-1946, :1954-1956`) — correctness rests entirely on tests, not shared code.

**Extraction: (A)** for the emitters; **(B)** for the *precedence order* — a small classifier over `CallParameter` (linked type) both sides call before diverging.

---

## Finding 11 — `needsLocals` / `[ScopeChannel]` participant detection (probable live drift)

**Rule.** A body needs a fresh `ScopeLocals` frame iff it statically contains a `[ScopeChannel]` participant; nested bodies don't contribute; carriers must be unwrapped; **nested chain parameters must be recursed into**.

- Generator: `TemplateEmitter.cs:371-375` (leftmost-name probe), `:1404-1416` (`ScanHostsParticipant`), propagation `:564, :596, :618, :1137-1138, :2362-2364`.
- Runtime: `RuntimeDocument.cs:132-190` (carrier unwrap `:174`, **recursion into `ChainedParameter` `:178-187`**), consumed at `Core/AbstractExtension.cs:135, :39-44, :53-65`.

**Drift risk: medium-high — likely live drift.** The generator probes only `chain.Chain[0]`; a participant appearing as a nested chain parameter gets a frame on the dynamic tier but not precompiled → `@else` reads stale/parent branch state. Second asymmetry: the generator ORs the definition body's and caller content's flags and passes the same value to **both** carriers (`:1137-1140` → `PrecompiledRuntime.BindDefinition`, `PrecompiledRuntime.cs:63/:69`) while the runtime gives each carrier its own body's flag — and `AbstractExtension.cs:41-42/:57-60` deliberately *clears* the parent frame for a non-participating body, which the OR suppresses.

**Extraction: (B) — genuinely sharable.** The predicate walks `OutputChain`/`OutputItem`/`CallParameter` (all linked) and needs one injected `Func<string,bool> hasScopeChannel`. A linked `ParticipantScan.cs` fixes the leftmost-only gap by construction. **Highest-value (B) extraction after Finding 2.** (Corroborated independently by area 02, Finding 6.)

---

## Finding 12 — Region fill scope: candidate matching, materialization, self-call rebind

**Rule.** Match caller-content `RegionFillCandidate`s (filtered by `Origin == OriginIdentity`) against the callee's regions; public match materializes via the **shared** `DefinitionMaterializer.Materialize`; while compiling a region that *is* the fill, rebind its own name to `BaseDefinition` so a self-call terminates.

- Generator: `TemplateEmitter.cs:1272-1286` (`Rebound`), `:1288-1291`, `:1293-1339` (`TryBuildGeneratorFillScope`), `:1062-1075`, `:177-186` (unconsumed-candidate bail), `:1228-1270` (`FillsDigest`).
- Runtime: `HeddleCompiler.cs:1516-1537` (identical scope choice, `WithRebind` `:1531`), `:1677-1735` (`BuildRegionFillScope`), `:1737-1748`, `:825-836` (mirrored at `TemplateEmitter.cs:526-537`).

**Drift risk: medium** — the byte-relevant materialization half is already shared; the matching/precedence half is duplicated. Asymmetry: runtime `continue`s on a dangling candidate (`:1706`); generator hard-refuses (`:1326`) — intentional but re-verify on every change.

**Extraction: (B)** for the matching loop over linked types, returning a neutral fault enum with per-side reactions. (Same conclusion as area 02, Finding 7.)

---

## Finding 13 — Slot mode: slot-type inheritance and the `@out` value/error rules

**Rule.** Slot type = first non-empty `SlotTypeName` down the base chain; "slot mode" iff one exists. Inside a slot body every `@out` must pass a value, be bodiless, and be assignable to the slot type (no box-to-object); outside one, `@out` with a value is an error. "Has a value" = native expr | chain param | C# expr | prop args | non-empty first model segment (the canonical five-way `HasOutValue`).

- Generator: `TemplateEmitter.cs:1051, :1644-1650` (`DefinitionHasSlot`), `:1860-1876` (the same walk again), `:1144-1174` (`BuildOutCall` — approximate `hasValue` at `:1152-1155`).
- Runtime: `Extensions/OutExtension.cs:143-155` (`HasOutValue` — canonical), `:26-88` (`InitStart` incl. the assignability check at `:62-67` which the generator does **not** reproduce), `HeddleCompiler.cs:1750-1781` (`ResolveSlotType`), `:1500-1501`, `PrecompiledRuntime.cs:102-117`.

**Drift risk: medium.** The generator's `hasValue` approximates via `!cp.IsModelTypeParameter` — equivalent today; a sixth `CallParameter` carrier breaks the equivalence (template renders precompiled, errors dynamic).

**Extraction: (B) for both halves** — `HasOutValue` is a pure function of the linked `CallParameter`; the slot-name walk is a pure function of the linked `DefinitionItem`. Two small zero-risk extractions; the generator implements the walk twice itself.

---

## Finding 14 — Name-resolution precedence: definition → extension → registered function

**Rule.** Fill scope first, then parse-context definitions, then extension/function resolution (a definition may shadow a branch keyword). A bodiless call whose name is a known function (and not an extension) compiles as a function in an unnamed `EmptyExtension` carrier; "function-compatible shape" = no chain parameter, no C# expression.

- Generator: `TemplateEmitter.cs:526-543, :545-657` (precedence chain, standalone-function path `:624-643`, HED7006 `:645-657`), `:2098-2148` (same precedence again for chain items; shape test `:2140-2141`).
- Runtime: `HeddleCompiler.cs:825-836, :882-945` (`nameIsExtension`/`nameInRegistry`/shape at `:888-891`; extension-wins rule `:893`; function path `:916-936`; `UnknownFunction` `:938-944`), `:1032-1033`.

**Drift risk: medium.** The generator's assumption that default-function names never collide with built-in extension names (`:625-627`) is a runtime-registry invariant asserted only in a comment; if violated, generator resolves the function while runtime resolves the extension.

**Extraction: (B)** — a linked `ResolveCallTarget(name, hasBody, CallParameter, Func<string,bool> isExtension, Func<string,bool> isFunction) → CallTargetKind` pins the order for both (`DefaultFunctionTable` and `ParseContext` are already linked).

---

## Finding 15 — `ExpressionMode` gates

- Generator: `TemplateEmitter.cs:2015-2019` (native under `MemberPathsOnly`), `:2070-2076` (C# outside `FullCSharp`), `:2572-2580` (`Mode()` string mapping with `default: return "Native"`).
- Runtime: `HeddleCompiler.cs:843-849, :884-885, :971-977, :1009-1015`; `Data/ExpressionMode.cs:6-16`; `PrecompiledGauntlet.cs:64-66`.

**Drift risk: medium-low** (refusal gate; gauntlet re-checks) — but a *new* enum member silently maps to `"Native"` via the `default:`. **Extraction: (B)** — link the enums, parse with `Enum.TryParse`, delete the string table. (Same as area 05, F2.)

---

## Finding 16 — The embedded-C# identifier contract (`model` / `chained` / `root`)

- Generator: `TemplateEmitter.cs:2056-2062` (contract in prose), `:2086-2091` (regex exclusion of `\bchained\b`/`\broot\b`), `:2093-2094`, `:2414-2415/:2453-2454`.
- Runtime: `src/Heddle/LanguageTemplates/CSharpClassTemplate.tcs:9`, `CSharpPreparseTemplate.tcs:11`, driven by `Runtime/CSharpContext.cs`.

**Drift risk: medium-low but sharp** — renaming a `.tcs` parameter silently changes what user C# means on the dynamic tier only. The generator's guard is a regex over raw text (errs safe). **Extraction: (B), trivially** — three `const string` names in a linked file referenced by both.

---

## Finding 17 — Zero-output directive classification

- Generator: `TemplateEmitter.cs:208-215` (hardcoded `model`/`using`/`import`/`profile` list), `:217-240` (re-tests names), `:421` (`"profile"` again).
- Runtime: **no name list** — protocol-based: `returnTypeChainedPrevious == null` (`HeddleCompiler.cs:128-131`) via each extension's null `InitStart` return.

**Drift risk: medium-low.** A new zero-output built-in (or custom extension returning null `InitStart`) is invisible to the generator: its block text is kept as a literal piece precompiled while the dynamic tier removes it. **Extraction: (A)** — declarative marker (`[ZeroOutput]` attribute or a flag on `ExtensionNameAttribute`) the runtime asserts against and `ExtensionBinder` reads symbolically; at minimum, one linked `const string[]`. (Same conclusion as area 02, Finding 4.)

---

## Finding 18 — Manifest binding strings vs the gauntlet's format; pinned built-in type names

- Generator: `TemplateEmitter.cs:2221-2231` (`RecordExtensionBinding` ×2, default assembly literal `"Heddle"`), `:2582-2594` (`b.Type + ", " + b.Assembly` at `:2591`); pinned names at `:597-598, :619-620, :1888, :2200`.
- Runtime: `PrecompiledGauntlet.cs:207-212` (`AqnSansVersion`), `:93-97, :73-91`.

**Drift risk: medium-low (loud failure — gauntlet catches at first render, but as a permanent perf cliff).** Hardcoded `"Heddle"` breaks on assembly rename/ILMerge; pinned `Heddle.Extensions.*` names break on namespace moves. **Extraction: (B)** for the format (shared `AqnSansVersion` helper in the linkable `Precompiled` folder — see area 03 Finding 1); **(A)** for the built-in names (derive from `ExtensionBinder.Info` instead of literals).

---

## Finding 19 — `maxRecursionCount`: build-baked vs options-read (intentional, recorded)

Generator bakes `_config.MaxRecursionCount` (`TemplateEmitter.cs:1854`); runtime reads it from `TemplateOptions` on the dynamic tier (`PrecompiledRuntime.cs:44-46, :64/:70` vs `DefinitionBaseExtension.InitStart`). **Low risk, intentional (D23)** — but the value is not in the options fingerprint, so a host raising `MaxRecursionCount` at runtime gets divergent *error* behavior between tiers for deep recursion.

---

## Finding 20 — Intra-generator duplication (local cleanups, no cross-project constraints)

| What | Copies |
|---|---|
| Lone-surrogate scan (identical loop; bool vs index) | `TemplateEmitter.cs:449-470` vs `Emit/PieceWriter.cs:54-72`; `TemplateEmitter` calls the latter at `:192, :447` then re-scans with its own at `:194`. Collapse to one index-returning version. |
| `global::` prefix strip | `TemplateEmitter.cs:477-482` (`StripGlobal`), re-inlined at `:750-752` and `:792-794`, plus `ExtensionBinder.cs:212-213` and `FunctionExportResolver.cs:124-125` — four implementations of a 3-line function. |
| Version-less AQN construction | `TemplateEmitter.cs:2591` hand-builds what `ExtensionBinder.Info.AqnSansVersion` (`ExtensionBinder.cs:71,86`) and `FunctionExportResolver.ExportEntry.ContainerAqnSansVersion` (`:23,32`) already carry. |
| Base-chain walk over `DefinitionItem` | Five near-identical walks in one file: `:1418-1424, :1644-1650, :1866-1869, :1459-1462, :1655-1667`. |
| `RecordExtensionBinding` overloads | `:2221-2225` vs `:2227-2231` — one method with a default parameter. |
| Manifest emission | `BuildManifestEntry` `:2520-2540` vs `BuildMarkerManifestEntry` `:2546-2568` — ~10 of 14 lines verbatim. |
| `DedupeUnresolvable` recomputed per call | `:2305-2313`, invoked at `:164` and `:2562`. |
| Escape tables | `PieceWriter.Escape` (`:23-52`) vs `NativeExpressionWriter.LiteralFormatter.EscapeChar` (`:290-305`) — same table, string vs char form. |

---

## Recommended extraction order (this area)

**Do now — pure (B), zero constraint friction (all operate on already-linked types or plain strings/enums):**
1. Finding 11 — participant/`needsLocals` scan (closes a probable live bug).
2. Finding 2 — the document offset-walk segmentation.
3. Finding 13 — `HasOutValue` + the slot-type base-chain walk.
4. Findings 15/1/4 — link `OutputProfile`, `ExpressionMode`, `RenderType`; add `OutputProfileRules` + `RenderTypeRules.Derive(bool,bool)`.
5. Finding 12 — the fill-candidate matching loop with a neutral fault enum.
6. Finding 20 — the local cleanups.

**Do next — structural, still (B)-ish:**
7. Findings 5+6 — `PropLayoutCore<TType>` parameterized by resolver + assignability predicate.
8. Finding 7 — the implicit-numeric table as shared data over a neutral `PrimitiveKind`.

**Spec/table only, never shared code (A):**
9. Finding 3 — the body-model-typing table (currently prose comments; highest risk of the (A) group).
10. Findings 8, 9, 10, 14, 17 — written rule tables + a shared conformance corpus driven from one data file by both a reflection-side and a symbol-side test.
