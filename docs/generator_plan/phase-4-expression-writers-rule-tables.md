# Phase 4 — expression writers: rule tables (supplement)

Supplement to [phase-4-expression-writers.md](phase-4-expression-writers.md). These tables are
the normative content of the shared artifacts that phase's plan commissions — the spec of record
the implementation transcribes and the tests derive from. Changing a row here is a plan/spec
change, per the table-driven-semantics rule in
[testing-standards.md](../spec/common/testing-standards.md). Sources: the runtime tables at
`src/Heddle/Runtime/Expressions/NumericPromotion.cs:32-54` and the generator twin at
`src/Heddle.Generator/Emit/TemplateEmitter.cs:1567-1605` (diffed and agreeing, per
[04 F7](../research/generator-code-sharing/04-expression-writers.md)); the deviations list and
literal/escape definitions in [docs/native-expressions.md](../native-expressions.md); the
runtime operator dispatch at `src/Heddle/Runtime/Expressions/NativeExpressionCompiler.cs:724-994`.

## NumericKind and the conversion tables

### The enum

```csharp
internal enum NumericKind
{
    None = 0,
    SByte, Byte, Int16, UInt16, Int32, UInt32, Int64, UInt64,
    Char, Single, Double, Decimal
}
```

`None` means "not a numeric primitive" — the table functions return false/`None` for it rather
than throwing, so callers can feed unclassified operands safely.

### CLR type → kind (`NumericTable.FromClrType`, shared — BCL only)

| CLR type | Kind |
| --- | --- |
| `System.SByte` | `SByte` |
| `System.Byte` | `Byte` |
| `System.Int16` | `Int16` |
| `System.UInt16` | `UInt16` |
| `System.Int32` | `Int32` |
| `System.UInt32` | `UInt32` |
| `System.Int64` | `Int64` |
| `System.UInt64` | `UInt64` |
| `System.Char` | `Char` |
| `System.Single` | `Single` |
| `System.Double` | `Double` |
| `System.Decimal` | `Decimal` |
| anything else | `None` |

### Roslyn `SpecialType` → kind (generator-side adapter, not in the shared file)

`System_SByte → SByte`, `System_Byte → Byte`, `System_Int16 → Int16`, `System_UInt16 → UInt16`,
`System_Int32 → Int32`, `System_UInt32 → UInt32`, `System_Int64 → Int64`,
`System_UInt64 → UInt64`, `System_Char → Char`, `System_Single → Single`,
`System_Double → Double`, `System_Decimal → Decimal`, else `None`. `Nullable<T>` is unwrapped by
the caller before mapping (the nullability travels separately on `OperandKind.IsNullable`).

### Implicit numeric conversions (C# §10.2.3, excludes identity)

Re-keyed from `NumericPromotion.ImplicitNumeric` (`NumericPromotion.cs:32-44`); one row per
source kind, targets comma-separated. This is the exact set both existing copies encode today.

| From | Implicitly converts to |
| --- | --- |
| `SByte` | `Int16, Int32, Int64, Single, Double, Decimal` |
| `Byte` | `Int16, UInt16, Int32, UInt32, Int64, UInt64, Single, Double, Decimal` |
| `Int16` | `Int32, Int64, Single, Double, Decimal` |
| `UInt16` | `Int32, UInt32, Int64, UInt64, Single, Double, Decimal` |
| `Int32` | `Int64, Single, Double, Decimal` |
| `UInt32` | `Int64, UInt64, Single, Double, Decimal` |
| `Int64` | `Single, Double, Decimal` |
| `UInt64` | `Single, Double, Decimal` |
| `Char` | `UInt16, Int32, UInt32, Int64, UInt64, Single, Double, Decimal` |
| `Single` | `Double` |
| `Double` | — |
| `Decimal` | — |

### Binary promotion (`NumericTable.TryPromote`)

First matching rule wins (transcribes `NumericPromotion.TryPromote`,
`NumericPromotion.cs:60-115`; also documented at
[docs/native-expressions.md](../native-expressions.md) — numeric promotion):

1. Either operand `None` → **no promotion** (not numeric).
2. Either operand `Decimal`: if the other is `Single`/`Double` → **illegal** (HED1008 class);
   else `Decimal`.
3. Either operand `Double` → `Double`.
4. Either operand `Single` → `Single`.
5. Either operand `UInt64`: if the other is signed integral (`SByte, Int16, Int32, Int64`) →
   **illegal**; else `UInt64`.
6. Either operand `Int64` → `Int64`.
7. One operand `UInt32` and the other signed integral → `Int64`.
8. Either operand `UInt32` → `UInt32`.
9. Otherwise → `Int32`.

Signed integral set: `SByte, Int16, Int32, Int64`. Integral set: those plus
`Byte, UInt16, UInt32, UInt64, Char`.

### Unary promotion (`NumericTable.UnaryPromote`)

`SByte, Byte, Int16, UInt16, Char → Int32`; every other kind maps to itself. (Transcribes
`NumericPromotion.UnaryPromote`, `NumericPromotion.cs:118-124`.)

## Operand kind descriptor (for the classifier and the ranker)

```csharp
internal enum OperandCategory
{
    Unknown = 0,   // no static type available — always degrades
    Numeric,       // Kind carries the NumericKind (Char included)
    Bool,
    String,
    Enum,          // any enum type
    NullLiteral,   // the typed-by-consumer null literal
    Reference,     // known non-string reference type
    Other          // known non-primitive value type (user structs, DateTime, …)
}

internal readonly struct OperandKind
{
    public OperandCategory Category { get; }
    public NumericKind Kind { get; }      // meaningful only for Numeric
    public bool IsNullable { get; }       // Nullable<T> wrapper present
}
```

## Operator classification table

`NativeOperatorRules.Classify(op, left, right) → Supported | RequiresRuntimeSemantics | NotDefined`.

Reading: **Supported** — verbatim C# emission is provably byte-equivalent to the runtime result;
the generator may emit. **RequiresRuntimeSemantics** — the expression is legal in the native tier
but its semantics deviate from what emitted C# would do (the runtime implements it by hand); the
generator degrades. **NotDefined** — the native tier rejects it with a positioned error; the
generator degrades (the dynamic tier raises the runtime's own error, so verdicts match).
Either operand `Unknown` → `RequiresRuntimeSemantics` unconditionally (degrade-on-doubt). The
"Dev n" column cites the deviation number in
[docs/native-expressions.md](../native-expressions.md) (deviations 1–7) or the runtime dispatch
lines encoded.

| Operator(s) | Operand condition | Verdict | Dev / source |
| --- | --- | --- | --- |
| `+ - * / %` | both `Numeric`, promotion legal (nullable allowed — lifted matches C#) | `Supported` | `NativeExpressionCompiler.cs:724-755`; lifted rules per spec |
| `+ - * / %` | both `Numeric`, promotion **illegal** (rule 2/5 above) | `NotDefined` | HED1008 class |
| `+` | one operand `String`, other `String`/`Numeric`/`Bool`/`Char`/`NullLiteral` | `Supported` | string concat = `string.Concat`, both sides |
| `+` | one operand `String`, other `Enum`/`Reference`/`Other` | `RequiresRuntimeSemantics` | formatting of the non-string side is runtime-owned |
| `+ - * / %` | any operand `Enum` | `NotDefined` | Dev 4 (enum arithmetic unsupported) |
| `+ - * / %` | any operand `Reference`/`Other` (user-defined operators / implicit conversions may exist) | `RequiresRuntimeSemantics` | Dev 6; runtime honors user *operators* but not user *conversions* — emitted C# would honor both |
| `<< >>` | left `Numeric` integral non-nullable, right kind ∈ {`Int32`, or implicitly-`Int32` (`SByte, Byte, Int16, UInt16, Char`)} non-nullable | `Supported` | `NativeExpressionCompiler.cs:778-798` |
| `<< >>` | any other combination (incl. nullable shift) | `RequiresRuntimeSemantics` | lifted-shift parity unproven; degrade |
| `< <= > >=` | both `Numeric`, promotion legal (nullable allowed — null compares false, both sides) | `Supported` | `:800-836`; spec lifted-relational rule |
| `< <= > >=` | any other combination | `RequiresRuntimeSemantics` / `NotDefined` per operand legality | Dev 6; runtime errors on non-comparable |
| `== !=` | both `Numeric` with legal promotion; or both `Bool`; or both `String`; or `NullLiteral` vs (`String`/`Reference`/nullable operand) | `Supported` | value/reference equality identical in C# |
| `== !=` | same-`Enum` both sides | `RequiresRuntimeSemantics` (conservative — promote to `Supported` only with a differential corpus proof) | `:838-888` |
| `== !=` | mixed/unrelated categories (incl. `Enum` vs anything, `Other`, cross-category) | `RequiresRuntimeSemantics` | **Dev 1** — total `object.Equals`; emitted `==` is CS0019 or reference-equality divergence |
| `& \| ^` | both `Numeric` integral with legal promotion (nullable allowed), or both `Bool` | `Supported` | `:890-927` |
| `& \| ^` | any operand `Enum` (incl. the `enum & 0` literal form) | `NotDefined` | Dev 5 (and same-enum bitwise stays runtime-owned until corpus-proven → `RequiresRuntimeSemantics` for same-enum) |
| `&& \|\|` | both `Bool` non-nullable | `Supported` | `:929-951` |
| `&& \|\|` | any `bool?` operand | `NotDefined` | Dev 7 (targeted error) |
| `&& \|\|` | any non-bool operand | `NotDefined` | spec: non-nullable `bool` only |
| `??` | left `IsNullable` numeric with right same-kind (or legally-promoting) numeric; or left `String`/`Reference` with right same category; or right `NullLiteral` | `Supported` | `:953-994` |
| `??` | left non-nullable value kind | `NotDefined` | spec: left must be reference or `Nullable<T>` |
| `??` | any other combination | `RequiresRuntimeSemantics` | coalesce unification is runtime-owned |
| `?:` (ternary) | condition `Bool` non-nullable; arms of identical category+kind+nullability | `Supported` | `:1000-1087` trivial-unification case |
| `?:` | arms differing but both `Numeric` with legal promotion | `RequiresRuntimeSemantics` (conservative; promotable later) | arm unification is runtime-owned; Dev 6 reaches it |
| `?:` | condition not non-nullable `Bool` | `NotDefined` | spec: condition must be `bool` |
| unary `!` | operand `Bool` non-nullable (nullable → `RequiresRuntimeSemantics`) | `Supported` | `NativeExpressionCompiler.cs:626` |
| unary `- +` | operand `Numeric`, and not (`-` on `UInt64`) | `Supported` | `:632-664`; unary promotion table |
| unary `-` | operand `UInt64` | `NotDefined` | no negation of `ulong` |
| unary `~` | operand `Numeric` integral | `Supported` | `:641` |
| unary any | operand `Enum`/`Reference`/`Other`/`Unknown` | `RequiresRuntimeSemantics` | degrade-on-doubt |

Completeness rule: the table must contain a verdict for **every** `ExprOperator` member × the
category cross-product; a structural test enumerates the enum and fails on any missing operator
(the F5 four-touch-point drift guard). Rows marked "conservative" may be promoted to `Supported`
only with a differential corpus entry proving byte-equivalence — a spec change per the golden
policy.

## Interim emit-guard whitelist

The WI2 generator-local guard (deleted by WI5) is the table above restricted as follows —
because the interim `Estimate` is coarser than the real facts adapter:

- Only `Supported` rows are emittable; **all** `Enum`, `Other`, `Reference` (except the
  string-`+` and null-comparison rows), and `Unknown` estimates degrade.
- Built-in calls contribute a kind only when every `DefaultFunctionTable` row for the name shares
  one `ReturnTypeName` (`upper/lower/trim/replace/substr/format/str → String`, `len → Int32`,
  `contains/startswith/endswith → Bool`); `abs/min/max/round/floor/ceil/range` and every export
  call estimate as `Unknown`.
- Ternary and unary guarding uses the same rows as the full table's `Supported` set.

The interim guard's acceptance bar: every expression it still emits is one the full table also
marks `Supported`; every F2 differential corpus entry degrades. Expressions it over-degrades are
a coverage cost only (output-identical via the dynamic tier), quantified by the benchmark gate.

## Member visibility (`MemberFacts` / `MemberVisibility`)

```csharp
internal enum MemberAccess
{
    Public, Internal, ProtectedOrInternal, Protected, ProtectedAndInternal, Private
}

internal readonly struct MemberFacts
{
    public bool CanRead { get; }
    public MemberAccess Access { get; }
    public bool HasHidden { get; }   // full-name match: Heddle.Attributes.HiddenAttribute
    public bool IsStatic { get; }
}
```

`MemberVisibility.IsAccessible(in MemberFacts f)` under the OQ1 ruling (resolved user,
2026-07-25: runtime behavior is normative):

| Fact | Verdict |
| --- | --- |
| `!CanRead` | inaccessible |
| `HasHidden` | inaccessible |
| `IsStatic` | inaccessible (error-shape fix: positioned not-found on both sides) |
| `Access ∈ {Public, Internal}` | accessible |
| `Access ∈ {ProtectedOrInternal, Protected, ProtectedAndInternal, Private}` | inaccessible (runtime-normative; widening `ProtectedOrInternal` is a breaking-window candidate per OQ1) |

Walk order (`MemberPathWalk<TType>`): the receiver type, then its base chain most-derived-first,
taking the **first** accessible property with the requested ordinal-case-sensitive name
(deterministic `new`-shadowing resolution); for an interface root, the interface itself then its
full base-interface closure. A `dynamic` receiver splits the walk into a dynamic hop at that
segment; a miss is property-not-found at that segment (HED0001 class). Whether the reflection
adapter can *supply* facts for inherited non-public and base-interface members is adapter
capability; the OQ1 ruling fixes the policy so both adapters return the same accept/reject for
the conformance corpus regardless.

## Member hop form

`MemberHopRule.Form(bool receiverIsValueType, bool propertyIsNonNullableValueType)`:

| Receiver is value type | Property is non-nullable value type | `HopForm` | Generator text (`MemberPathWriter.cs:38-49`) | Runtime shape (`ModelParameter.cs:39-50`) |
| --- | --- | --- | --- | --- |
| yes | — | `Direct` | `recv.Prop` | `MakeMemberAccess` |
| no | no | `NullConditional` | `recv?.Prop` | `Condition(recv == null, Default(T), MakeMemberAccess)` |
| no | yes | `NullDefaultConditional` | `(recv == null ? default(T) : recv.Prop)` | `Condition(recv == null, Default(T), MakeMemberAccess)` |

The runtime maps the two null-guarded forms to one `Expression` shape; the generator needs the
split because C# `?.` on a non-nullable value property would produce `Nullable<T>` instead of the
runtime's boxed `default(T)`. The rule fixes both mappings to one input pair.

## Operator lexemes (`OperatorLexeme.For`)

One row per supported operator — 19 binary, 4 unary; `null` for anything else. Derived
"supported set" = the table's key set. Replaces `BinarySymbol`
(`NativeExpressionWriter.cs:240-265`), the runtime error-text `Symbol` (which today lacks
`AndAlso`/`OrElse`/`Coalesce`), and the inline unary lexemes.

| `ExprOperator` | Lexeme | | `ExprOperator` | Lexeme |
| --- | --- | --- | --- | --- |
| `Add` | `+` | | `Equal` | `==` |
| `Subtract` | `-` | | `NotEqual` | `!=` |
| `Multiply` | `*` | | `And` | `&` |
| `Divide` | `/` | | `ExclusiveOr` | `^` |
| `Modulo` | `%` | | `Or` | `\|` |
| `LeftShift` | `<<` | | `AndAlso` | `&&` |
| `RightShift` | `>>` | | `OrElse` | `\|\|` |
| `LessThan` | `<` | | `Coalesce` | `??` |
| `LessThanOrEqual` | `<=` | | `Not` (unary) | `!` |
| `GreaterThan` | `>` | | `Negate` (unary) | `-` |
| `GreaterThanOrEqual` | `>=` | | `UnaryPlus` (unary) | `+` |
| | | | `OnesComplement` (unary) | `~` |

## C# escape set (`CSharpEscape`)

The single table for `char` and `string` literal emission — the union of the two generator
tables, matched against the decoder's escape set
([docs/native-expressions.md](../native-expressions.md), literals: `\' \" \\ \0 \a \b \e \f \n
\r \t \v \xH…H \uHHHH \UHHHHHHHH`). Emission always uses the shortest canonical form below;
decoding accepts the full documented set (the decoder is unchanged by this phase).

| Character | Emitted as | Context |
| --- | --- | --- |
| `"` | `\"` | string literals |
| `'` | `\'` | char literals |
| `\` | `\\` | both |
| NUL | `\0` | both |
| BEL | `\a` | both |
| BS | `\b` | both |
| FF | `\f` | both |
| LF | `\n` | both |
| CR | `\r` | both |
| TAB | `\t` | both |
| VT | `\v` | both |
| other C0 controls (`< 0x20`) | `\uXXXX` | both |
| **lone surrogate** (`D800–DFFF` unpaired) | `\uXXXX` | both — closes [04 F9](../research/generator-code-sharing/04-expression-writers.md)'s raw-emission hole |
| everything else | verbatim | both |

`PieceWriter.Escape` and `LiteralFormatter` both delegate here; the u8-twin lone-surrogate
*eligibility* check (`PieceWriter.HasLoneSurrogate`) is unchanged — a lone surrogate is still
ineligible for a `"…"u8` twin (it has no UTF-8 encoding), but its `string`/`char` literal is now
emitted escaped instead of raw. String output is byte-identical to today's `PieceWriter.Escape`
for every input that contains no lone surrogate and no character only `EscapeChar` handled —
i.e. all existing goldens.
