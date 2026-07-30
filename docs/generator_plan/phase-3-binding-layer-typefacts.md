# Phase 3 supplement — the ITypeFacts abstraction contract

Supplement to [Phase 3 — binding layer](phase-3-binding-layer.md). It pins the shape and the
obligations of the `ITypeFacts<TType>`/`IMemberFacts` abstraction
([03 F6](../research/generator-code-sharing/03-binding-layer.md)) and the assignability
conformance corpus, at plan altitude: the owning spec fixes final signatures; this document
fixes what the abstraction must and must not do, and why each member exists.

## Why an abstraction and not shared code

The assignability relation cannot be shared imperatively — it *is* the type graph, and each
side already has an engine for it: reflection's `Type.IsAssignableFrom`
(`src/Heddle/Helpers/TypeExtension.cs`, `IsType` at `:14-23`) and Roslyn's
`Compilation.ClassifyConversion`. What drifted is not the graph but the **corrections and
spellings around it**, currently transcribed three times on the generator side:

- `TemplateEmitter.RedeclarationAssignable` (`src/Heddle.Generator/Emit/TemplateEmitter.cs:993-1025`)
  — the relation plus two hand-written Roslyn-vs-CLR corrections;
- a second partial encoding inside `DefaultConvertible` (`TemplateEmitter.cs:965-971`);
- a third spelling of the `Nullable<T>` test in `SymbolTypeResolver.IsNonNullableValueType`
  (`src/Heddle.Generator/Binding/SymbolTypeResolver.cs:236-244`), which uses `ConstructedFrom`
  where the emitter uses `OriginalDefinition` — an internal inconsistency flagged in
  [07's live-drift table, row 13](../research/generator-code-sharing/07-recommendations.md).

The abstraction moves the *rules* (prop-layout sequencing, discovery precedence, conversion
legality) into shared files and confines the *type graph* to one adapter per side, with the
corrections stated once, next to the relation they correct.

## Hard constraints (restated from the plan — binding)

- The shared interface files live under `src/Heddle/` (exact folder a spec decision — the
  research suggests the `Runtime/Expressions`-adjacent shared area; `Precompiled/` also has
  precedent), compile under **netstandard2.0**, and are linked into `Heddle.Generator` via
  `<Compile Include>` — never copied.
- The shared files reference **neither `Microsoft.CodeAnalysis` nor any Roslyn concept**, and
  make no reflection-only assumptions. `TType` is fully opaque to shared code.
- The **reflection adapter lives in `Heddle`** (over `System.Type`), the **Roslyn adapter lives
  in `Heddle.Generator`** (over `ITypeSymbol`). Neither adapter type appears in a shared file.
- The generator keeps zero references to `Heddle.dll`; everything shared arrives as source.

## The contract surface

Member-by-member intent. Names are indicative; the spec finalizes signatures, `readonly
struct` vs interface representation, and null-handling.

### `ITypeFacts<TType>`

| Member | Contract | Consumers (this phase) |
|---|---|---|
| `bool IsAssignableFrom(TType target, TType source)` | The **CLR** relation `target.IsAssignableFrom(source)`, exactly. The Roslyn adapter must return the CLR answer even where Roslyn's conversion classification disagrees (see corrections below). | `PropLayoutCore` redeclaration rule; extension-precedence override rule (OQ3); prop-default reference arm |
| `bool TryGetNullableUnderlying(TType type, out TType underlying)` | True iff `type` is `System.Nullable<T>`; one spelling replacing the three divergent ones. The Roslyn adapter pins `OriginalDefinition` vs `ConstructedFrom` once (spec records which and why). | Conversion legality; null-default legality; the nullable corrections themselves |
| `bool IsValueType(TType type)` / `bool IsInterface(TType type)` | Direct classification. | Precedence tie-breaks; conversion legality |
| `PrimitiveKind GetPrimitiveKind(TType type)` | Maps into **Phase 4's** shared `PrimitiveKind` enum (this phase must not define it). Reflection side: `Type`→kind map; Roslyn side: 12-case `SpecialType` switch. | Prop-default widening (consumes Phase 4's `IsImplicitNumeric` table); keyword table typing |
| `bool IsUsableAsPropType(TType type)` | The unified unusable-type predicate: false for null/unresolved, open/unbound generics (`ContainsGenericParameters` semantics, not merely unbound-definition), pointers, and by-ref types — the runtime's rule at `PropLayout.cs:134`, which the generator's local variant (`ExtensionBinder.cs:304-307`) currently under-implements (no by-ref arm, narrower generic test). | `PropLayoutCore` validation step 4 |
| `string FormatAqn(TType type)` (or decomposition to `(ns, metadataNames, assembly)`) | Routes through the shared `AqnFormatter`; both adapters must produce byte-identical output to live `Type.FullName + ", " + assemblySimpleName`. | Manifest rows; OQ4 prop-layout fingerprint |

### `IMemberFacts` (this phase's slice)

Phase 4 owns the `MemberPathWalk`/`MemberVisibility` core and with it the full member-facts
surface (property lookup, getter accessibility as a shared `MemberAccess` enum, `[Hidden]`
detection). This phase's obligations on that surface, recorded here so the Phase 4 contract can
bind them:

- the accessibility answer must be expressible as a **decision table over `MemberAccess`**
  (public / assembly / famORassem / other), so the OQ1 `ProtectedOrInternal` row (resolved,
  user, 2026-07-25: `hidden` — follow runtime) is an explicit table entry, not emergent
  behavior;
- `[Hidden]` detection must compare the **fully-qualified metadata name**
  `Heddle.Attributes.HiddenAttribute` on both sides (the symbol side currently matches any
  attribute *named* `HiddenAttribute` — `SymbolTypeResolver.cs:229`);
- interface property lookup must implement one documented rule for base-interface members
  (the runtime's `Type.GetProperty`-on-interface behavior is the authority; the symbol side's
  `AllInterfaces` walk at `SymbolTypeResolver.cs:204-214` is wider and must be constrained by
  the table, not by accident).

## The Roslyn adapter's corrections

The verified places where Roslyn's `ClassifyConversion` and the CLR relation disagree, kept
**only** in the Roslyn adapter with a comment block naming each (today they live in
`RedeclarationAssignable`'s doc comment):

1. `int` → `int?`: **CLR-assignable but not classification-assignable.** Reflection's
   `typeof(int?).IsAssignableFrom(typeof(int))` answers true (the CLR's special `Nullable<T>`
   treatment), while Roslyn classifies the conversion `ImplicitNullable` — a naive
   classification-based adapter would answer false. The adapter corrects toward the CLR
   answer.
2. `int?` → `IComparable` (interface): **classification-convertible but not CLR-assignable.**
   Roslyn classifies a boxing conversion (the boxed underlying value does implement the
   interface), while `typeof(IComparable).IsAssignableFrom(typeof(int?))` is false —
   `Nullable<T>` itself implements no interfaces. The adapter corrects toward the CLR answer.

   Row 2 is one case of a wider rule and was first written as its only case. A boxing
   conversion *out of* a `Nullable<T>` classifies against the boxed `T`, which reaches `T`'s
   interfaces and, for an enum, `System.Enum`; the CLR relates `Nullable<T>` itself, whose own
   hierarchy is `ValueType` and `object`. Phrasing the correction as "except an interface" left
   `DayOfWeek?` → `System.Enum` accepted.

3. `uint[]` → `int[]` (and `byte[]` ↔ `sbyte[]`, `long[]` ↔ `ulong[]`, `DayOfWeek[]` → `int[]`):
   **CLR-assignable but no conversion at all to Roslyn.** The CLR compares array element types
   after reducing an enum to its underlying primitive and each signed/unsigned integer pair to
   one representative, and this propagates through the array's generic interfaces
   (`uint[]` → `IList<int>`) and through jagged arrays. `char` and `bool` reduce to nothing —
   `char[]` → `ushort[]` and `bool[]` → `byte[]` are false — and a value-type element never
   reaches a reference-type one, so `int[]` → `object[]` stays false.

   In every row the corpus records the expected value **generated from live reflection at
   corpus-build time**, so the prose direction above is non-load-bearing by design
   (*(verify at implementation)* — regenerate and diff the expectations before relying on
   them).

One anticipated disagreement class stayed unverified through implementation (variance with
value-type type arguments; `ValueTuple` conversions): those rows agree. Array covariance is
the one that did not, and it was found by sweeping the relation rather than by reasoning about
it — which is exactly why the corpus below exists.

## The assignability conformance corpus

One data file (format a spec decision — the repo precedent for table-driven semantic pins is
xUnit `[Theory]` data whose table is part of the spec, per the
[testing standards](../spec/common/testing-standards.md#fixtures-and-goldens)), rows of
`(source type spelling, target type spelling, expected bool)`, executed by **two drivers**:

- a reflection-side test in `src/Heddle.Tests` resolving the spellings via
  `ReflectionHelper`/`Type` and asserting `ITypeFacts` (reflection adapter) row-by-row;
- a symbol-side test compiling a probe compilation, resolving the same spellings to
  `ITypeSymbol`, and asserting the Roslyn adapter row-by-row.

The `DefaultFunctionLockstepTests` precedent
(`src/Heddle.Tests/DefaultFunctionLockstepTests.cs`) is the model: one source of truth, two
type systems proving they agree with it. Seed row families:

| Family | Representative rows |
|---|---|
| Identity / reference | `string`→`string` T; `string`→`object` T; `object`→`string` F |
| Boxing | `int`→`object` T; `int`→`IComparable` T; `int`→`System.Enum` F |
| Nullable lift (the correction rows) | `int`→`int?` **CLR answer pinned by generated expectation**; `int?`→`int` F; `int?`→`IComparable` and `DayOfWeek?`→`System.Enum` pinned by generated expectation, with `DayOfWeek?`→`ValueType`/`object` as the neighbours they must not swallow |
| Nullable-to-nullable | `int?`→`long?` F (assignability, distinct from conversion legality); `int?`→`int?` T |
| Numeric (assignability, not widening) | `int`→`long` F — pins that widening legality never leaks into the assignability answer |
| Interface / hierarchy | derived→base T; base→derived F; class→implemented-interface T; interface→base-interface T |
| Variance probes | `IEnumerable<string>`→`IEnumerable<object>` T; `IEnumerable<int>`→`IEnumerable<object>` F (value-type variance — the anticipated third-disagreement class) |
| `ValueTuple` probes | `(int,string)`→`(int,string)` T; `(int,string)`→`(long,string)` F |
| Generic definitions/nesting | constructed→same-constructed T; open-definition rows per the `IsUsableAsPropType` boundary |
| Array | `string[]`→`object[]` T (array covariance); `int[]`→`object[]` F |
| Array covariance over reduced elements (the third correction family) | `uint[]`↔`int[]` T; `byte[]`→`sbyte[]` T; `DayOfWeek[]`→`int[]` T; `uint[]`→`IList<int>` T; `int[][]`→`uint[][]` T; and the refusals `int[]`→`long[]`, `int[]`→`ValueType[]`, `DayOfWeek[]`→`Enum[]`, `char[]`→`ushort[]`, `bool[]`→`byte[]` |

Expected values for correction-family rows are generated from live reflection at corpus-build
time and committed — the corpus asserts "the Roslyn adapter equals the CLR", never "equals what
the plan author believed".

## Consumption map (who calls what)

| Shared rule-core (owner) | `ITypeFacts` members used |
|---|---|
| `PropLayoutCore<TType>` (this phase) | `IsUsableAsPropType`, `IsAssignableFrom` (redeclaration), `GetPrimitiveKind` + Phase 4 tables (defaults), `TryGetNullableUnderlying` (null-default legality) |
| Extension discovery/precedence (this phase) | `IsAssignableFrom` (override rule), `IsInterface` (tie-break) |
| Export discovery (this phase) | none — `ExportedMethodFacts` is a pure record; kept fact-shaped deliberately |
| Model type lookup (this phase) | `FormatAqn`/decomposition; `GetPrimitiveKind` (keyword table) |
| `MemberPathWalk`/`MemberVisibility` (Phase 4) | `IMemberFacts` surface; this phase adopts, per the plan's F7 item |
| Conversion/widening tables (Phase 4) | `GetPrimitiveKind`, `TryGetNullableUnderlying` |

## Non-goals

- No `ITypeFacts` member is added for a consumer that does not exist in this phase or Phase 4's
  declared scope (YAGNI per the
  [coding standards](../spec/common/coding-standards.md#solid-dry-and-yagni-in-balanced-mode) —
  the seam exists because two consumers already do).
- No caching/interning strategy is mandated at plan level; adapters may memoize, the contract
  is purity (same inputs → same answer) and thread safety for concurrent compiles.
- No public API: everything here is `internal` on both sides.
