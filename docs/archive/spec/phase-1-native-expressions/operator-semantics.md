# Phase 1 operator semantics — the normative table

Supplementary document of the [phase 1 spec](README.md). This is the **normative
definition** of what every native-expression operator computes: precedence,
associativity, operand-type rules, numeric promotion, lifted-null behavior, literal
typing, and the exact `System.Linq.Expressions` emission. Three artifacts must state the
same rules — this document, the table-driven `[Theory]` data in
`NativeExpressionOperatorTests` (whose expected values are C#-computed), and the
published `native-expressions.md` docs page. Changing a row here is a spec change per the
[testing standards](../common/testing-standards.md#fixtures-and-goldens); the test table
is never hand-edited to pass.

## Precedence and associativity

C# precedence verbatim ([C# operators and expressions](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/)),
realized by alternative order in the `expr` rule ([grammar.md](grammar.md#parser-changes)):

| Level (high → low) | Operators | Associativity | Emission (summary) |
| --- | --- | --- | --- |
| primary | `()` grouping, `.` member hop (null-safe), `[]` indexer, `name(args)` function call | left | shared member-path chain / `ArrayIndex`·`Property` / registry-bound `Call`/`Invoke` |
| unary | `!` `-` `+` `~` | right | `Not`, `Negate`, `UnaryPlus`, `OnesComplement` |
| multiplicative | `*` `/` `%` | left | `Multiply`, `Divide`, `Modulo` |
| additive | `+` `-` | left | `Add` / `string.Concat`, `Subtract` |
| shift | `<<` `>>` | left | `LeftShift`, `RightShift` |
| relational | `<` `<=` `>` `>=` | left | `LessThan` … `GreaterThanOrEqual` (`liftToNull: false`) |
| equality | `==` `!=` | left | `Equal`, `NotEqual` (`liftToNull: false`; `object.Equals` fallback) |
| bitwise AND | `&` | left | `And` |
| bitwise XOR | `^` | left | `ExclusiveOr` |
| bitwise OR | `\|` | left | `Or` |
| logical AND | `&&` | left | `AndAlso` (bool only, short-circuit) |
| logical OR | `\|\|` | left | `OrElse` (bool only, short-circuit) |
| null-coalescing | `??` | **right** | `Coalesce` |
| conditional | `?:` | **right** | `Condition` |

`?.` does not exist: `.` hops already carry member-tier null-safety (below). This
equivalence is documented on the published docs page.

## Path and indexer semantics (primary tier)

- **Member hops are null-safe with member-tier semantics**, produced by the shared
  builder extracted from
  [ModelParameter.GetPropertyChainAccessor](../../../src/Heddle/Runtime/Parameters/ModelParameter.cs):
  a hop off a `null` reference yields `default(T)` of the hop's property type — `null`
  for reference types and `Nullable<T>`, the zero-value for non-nullable value types.
  Consequently `@if(A.B > 0)` with `A == null` and `B : int` evaluates `0 > 0 == false`
  and renders nothing (roadmap validation scenario, verified against the current
  `Expression.Condition`/`Expression.Default` emission in `ModelParameter`).
- **Property resolution is identical to the member tier** — same
  `BindingFlags.Instance | Static | NonPublic | Public` lookup, same `CanRead` +
  `[Hidden]` + getter `IsAssembly || IsPublic` filter, same
  `Property {name} not found in Type [{type}]` message (now `HED0001`), single-sourced in
  the extracted resolver.
- A `[Dynamic]`-attributed property or a dynamic scope anywhere in an expression operand
  is a compile error `HED1004` (v1 restriction; plain member paths via `call`
  alternative 2 keep using `DynamicParameter` unchanged).
- **Indexers:** single- and multi-dimensional arrays use `Expression.ArrayIndex`
  (indexes converted to `int`; `long` indexes accepted via C# rules); any other type
  reflects its default indexer (`DefaultMemberAttribute`, falling back to `Item`)
  matching the argument types under the same visibility/`[Hidden]` filter, emitted with
  `Expression.Property(target, indexer, args)`. The **target** is null-safe like a
  member hop (`null` target → `default` of the indexer/element type); the **index value
  is not range-checked** — an out-of-range index throws at render exactly as in C#. No
  match → `HED1010`.
- **String indexing** `Name[0]` works through `string`'s indexer (returns `char`).

## Numeric promotion (`NumericPromotion` helper, table-driven)

Binary numeric promotion follows the C# specification
([built-in numeric conversions](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/numeric-conversions)):
both operands are converted (via `Expression.Convert`) to the first matching row, then
the factory is called with equal operand types.

| Rule (first match wins) | Promoted type |
| --- | --- |
| either operand is `decimal` (other must not be `float`/`double` → else `HED1008`) | `decimal` |
| either operand is `double` | `double` |
| either operand is `float` | `float` |
| either operand is `ulong` (other must not be signed `sbyte`/`short`/`int`/`long` → else `HED1008`) | `ulong` |
| either operand is `long` | `long` |
| one operand is `uint`, other is `sbyte`/`short`/`int` | `long` |
| either operand is `uint` | `uint` |
| otherwise (`sbyte`, `byte`, `short`, `ushort`, `char`, `int` mixes) | `int` |

Unary promotion: `sbyte`/`byte`/`short`/`ushort`/`char` → `int`; `-` on `uint` → `long`
(C# rule); `-` on `ulong` → `HED1009`. `~` applies to integral types and enums (enums via
underlying-type conversion, converted back to the enum type). `!` applies to
non-nullable `bool` only.

**Documented deviation:** the literal `-2147483648` types as `Negate((long)2147483648)`
= `long` here (C# special-cases it to `int`). The value and rendered text are identical;
the static type differs only in that extreme corner. Recorded in the deviations table
below.

## Lifted (nullable) operands

Verified against the factory documentation
([Expression.LessThan](https://learn.microsoft.com/en-us/dotnet/api/system.linq.expressions.expression.lessthan) —
"If `left`.Type and `right`.Type are both nullable, the node is lifted. The type of the
node is nullable Boolean if `liftToNull` is `true` or Boolean if `liftToNull` is
`false`."):

- When either operand's static type is `Nullable<T>`, both operands are converted to
  `Nullable<P>` of the promoted type `P` and the factory produces a lifted node.
- **Arithmetic/bitwise/shift lifted result:** `Nullable<P>`; `null` operand → `null`
  result (C# semantics, produced automatically by the lifted node).
- **Relational (`<` `<=` `>` `>=`):** emitted with `liftToNull: false` → node type
  `bool`; any `null` operand compares `false`. Matches C#.
- **Equality (`==` `!=`):** emitted with `liftToNull: false` → `null == null` is `true`,
  `null == value` is `false`. Matches C#.
- `&&`/`||` do **not** lift: `bool?` operands are `HED1005` (C# has no short-circuit
  lifted forms either; the message suggests `== true`).

## Per-operator operand rules

### Additive `+` (string concatenation)

| Operand static types | Result | Emission |
| --- | --- | --- |
| both numeric (incl. lifted) | promoted type | `Expression.Add` |
| either is `string` | `string` | both `string`: `Expression.Add(l, r, string.Concat(string, string))`; otherwise the non-string operand is `Expression.Convert`-ed to `object` and `string.Concat(object, object)` is used — `null` renders as empty, value types are boxed and `ToString()`-ed, exactly as C# emits |
| `char + char` | `int` (promotion) | `Expression.Add` over `int` |
| user-defined `op_Addition` on either type (e.g. `DateTime + TimeSpan`) | operator's return type | factory binds the user-defined operator automatically |
| anything else | — | `HED1008` |

### Multiplicative / `-` / shift

Numeric-promoted only, plus user-defined operators — the CLS operator methods the
factories bind for this group are `op_Multiply`, `op_Division`, `op_Modulus`,
`op_Subtraction`, `op_LeftShift`, and `op_RightShift`. Shift
operators follow C#: left operand must be (or promote to) `int`/`uint`/`long`/`ulong`,
right operand must be (or convert to) `int`; the predefined expression-tree shifts are
integral-only, so enum operands are `HED1008` for shifts. Division (`/`) and remainder
(`%`) by zero throw `DivideByZeroException` at render for integral and `decimal`
operands; `float`/`double` produce ±infinity/`NaN` — C# semantics, not clamped.

### Relational `<` `<=` `>` `>=`

Numeric-promoted operands, `char` via promotion, user-defined operators (`DateTime`,
`TimeSpan`, …) via the factories. `string` relational comparison is **not** defined
(same as C#) → `HED1008`.

### Equality `==` `!=`

Resolution order (first match):

1. Both operands numeric (incl. lifted) → promote, compare by value.
2. Both operands the **same enum type** → compare via underlying-type conversion.
3. `bool`/`bool`, `char`/`char` (and lifted forms) → value comparison.
4. Either static type declares a matching user-defined `op_Equality` and the factory can
   bind it (covers `string == string` value equality, `DateTime`, user types) →
   `Expression.Equal`/`NotEqual` binds it automatically.
5. `null` literal against a reference-type or `Nullable<T>` operand → typed-null
   comparison (`HasValue` test for nullables).
6. Both operands reference types with one assignable to the other → reference equality
   (`ceq`), as C#.
7. **Fallback for remaining reference/mixed combinations:**
   `object.Equals(Convert(l, object), Convert(r, object))`, negated for `!=`. This is a
   deliberate, documented deviation — C# rejects unrelated static types at compile time;
   the template tier prefers a total, null-safe value comparison (roadmap-ratified
   "object.Equals fallback for mixed reference types").
8. Value-type operand with no rule above (e.g. `int == string`) → `HED1008`.

### Bitwise `&` `^` `|`

| Operand static types | Result | Emission |
| --- | --- | --- |
| both integral (incl. lifted) | promoted type | `And`/`ExclusiveOr`/`Or` |
| both the **same enum type** (incl. lifted) | that enum type | operands `Convert`-ed to the underlying type, op applied, result `Convert`-ed back (the C# compiler's own emission shape — the predefined expression-tree forms are integral-only) |
| both `bool` | `bool` | non-short-circuit `And`/`ExclusiveOr`/`Or` (C# defines these) |
| enum vs its underlying integral type, or two different enums | — | `HED1008` (C# also rejects, except the literal-`0` corner which is not carried over) |

### Logical `&&` `||`

Operands must be non-nullable `bool` (after evaluating any user-defined `op_True`
scenarios is *out of scope* — no `&&`/`||` over user types in v1). Anything else →
`HED1005`. Short-circuit via `AndAlso`/`OrElse`.

### Null-coalescing `??`

- Left operand's static type must be a reference type or `Nullable<T>`; a non-nullable
  value type is `HED1006` — mirroring the factory contract
  ([Expression.Coalesce](https://learn.microsoft.com/en-us/dotnet/api/system.linq.expressions.expression.coalesce)
  throws `InvalidOperationException` exactly then; the compiler checks first and never
  surfaces the exception).
- Result type follows the factory's documented rules: `T? ?? T` → `T`; otherwise if the
  right type is implicitly convertible to the left → left type; otherwise if the
  unwrapped left is implicitly convertible to the right → right type. When the common-type
  rule (below) requires a numeric promotion first (`int? ?? long`), `Convert` nodes are
  inserted before calling the factory.
- `null` literal on the right (`Name ?? null`) types as the left's type.

### Conditional `?:`

- Condition must be non-nullable `bool` → else `HED1011`.
- Arm unification uses the **common-type rule**: (1) identical types → done; (2) `T` and
  `T?` → `T?`; (3) both numeric → binary numeric promotion; (4) `null` literal and a
  reference type / `Nullable<T>` → that type; (5) one arm's type reference-assignable
  from the other → the base type. No rule → `HED1007`. `Expression.Condition` is called
  with both arms `Convert`-ed to the unified type — the factory requires
  `ifTrue.Type == ifFalse.Type`
  ([Expression.Condition](https://learn.microsoft.com/en-us/dotnet/api/system.linq.expressions.expression.condition)),
  and its `Type`-taking overload only unifies reference-assignable arms, so numeric arm
  unification must insert the `Convert` nodes itself.

## Literal typing

Per the C# literal pages
([integral](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/integral-numeric-types),
[floating-point](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/floating-point-numeric-types)):

| Literal | Static type | Notes |
| --- | --- | --- |
| `42`, `0x2A`, `0b101010`, `1_000` | first of `int` → `uint` → `long` → `ulong` that fits | C# first-fit rule |
| `42L` / `42l` | first of `long` → `ulong` that fits | lowercase `l` accepted (C# warns; the AST builder does not) |
| `42u` | first of `uint` → `ulong` | |
| `42ul` (any case/order) | `ulong` | |
| `1.5`, `1e3` | `double` | suffix-less reals default to `double` |
| `1.5f` / `1.5d` / `1.5m` | `float` / `double` / `decimal` | any case |
| `"text"` | `string` | escapes `\' \" \\ \0 \a \b \e \f \n \r \t \v \xH…H \uHHHH \UHHHHHHHH` processed at AST build |
| `'c'` | `char` | same escape set |
| `true` / `false` | `bool` | |
| `null` | contextual null | typed by the consuming operator (equality, `??`, ternary arm); an uncontextualizable use (`null + null`) is `HED1008` |

Numeric literals out of range for every candidate type (e.g. `1e999`,
`99999999999999999999999999999`) are a positioned `HED1008`-family compile error emitted
by the AST builder (`Literal is out of range` wording pinned in the diagnostics table of
the entry document under `HED1008`'s literal-overflow row — same ID, literal-specific
message form).

## Constant folding

A tree containing only literals and operators (no `PathNode`, `CallNode`, `IndexNode`,
`MethodCallNode`) is evaluated **once at compile time** — the compiler builds the LINQ
expression, compiles, invokes, and stores the result in the existing
[ConstantParameter](../../../src/Heddle/Runtime/Parameters/ConstantParameter.cs). This is
deterministic and safe (no user code can run: only literals and intrinsics are present).
A render-visible consequence: `@(2 + 2 * 2)` costs a field read per render, not a
delegate call. Trees with any non-literal operand are compiled to
[CompiledParameter](../../../src/Heddle/Runtime/Parameters/CompiledParameter.cs) as a
whole — no partial subtree folding in v1 (deferred; see the entry document).

## Deviations from C# (complete list, documented on the docs page)

| # | Deviation | Rationale |
| --- | --- | --- |
| 1 | `==`/`!=` on unrelated reference/mixed static types compiles to `object.Equals` instead of being a compile error | Roadmap-ratified; templates compare loosely-typed values; total and null-safe |
| 2 | `.` hops (and indexer targets) are null-safe, yielding `default(T)` | Member-tier equivalence — the founding rule of the phase |
| 3 | `-2147483648` types as `long`, not `int` | First-fit literal typing without the C# lexer special case; value identical |
| 4 | enum `+`/`-` arithmetic (`enum + int`) is not supported (`HED1008`) | Rarely meaningful in templates; C#-parity here costs table complexity with no roadmap scenario |
| 5 | the `enum & 0`-literal special case is not carried over | Same as 4 |
| 6 | no user-defined implicit conversions are consulted during promotion/unification (user-defined *operators* are) | Reflection-side conversion search is the complexity/soundness cliff of C# semantics; explicit `str(...)`/`format(...)` cover the template need |
| 7 | `&&`/`||` reject `bool?` with `HED1005` instead of C#'s "no lifted short-circuit" compile error wording | Same outcome (error), friendlier message |

## Expression-tree emission summary (intrinsics whitelist)

The only invocables the compiler may emit, completing the sandbox invariant of the entry
document:

| Construct | Emitted call target |
| --- | --- |
| member hop | property getter passing the member-tier filter (shared resolver) |
| indexer | `ArrayIndex` (no method) or the reflected indexer getter passing the same filter |
| function call | host-registered `MethodInfo`/`Delegate` from the frozen registry (incl. the built-ins' own static methods) |
| string `+` | `string.Concat(string, string)` / `string.Concat(object, object)` |
| equality fallback | `object.Equals(object, object)` (static) |
| user-defined operators | `op_*` methods **declared by the operand's own static type**, bound by the `Expression` factories — host-authored model code, same trust tier as its property getters |
| everything else | operator nodes with `Method == null` (JIT intrinsics), `Convert` nodes, `Constant` nodes |

There is no code path from template text to `Type.GetMethod` by name, no `nameof`-style
member discovery, and no binding to methods of types other than (a) the operand types
themselves (operators/indexers/getters, filtered) and (b) registry entries.

## External grounding for this document

- [C# operators and expressions](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/) — precedence/associativity.
- [Built-in numeric conversions](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/numeric-conversions) — the promotion table.
- [Expression class](https://learn.microsoft.com/en-us/dotnet/api/system.linq.expressions.expression) — factory coverage; predefined bitwise/shift forms are integral-only (enum → underlying `Convert`), shifts require `Int32` right operand.
- [Expression.LessThan](https://learn.microsoft.com/en-us/dotnet/api/system.linq.expressions.expression.lessthan) — `liftToNull` semantics quoted above (re-verified July 2026).
- [Expression.Coalesce](https://learn.microsoft.com/en-us/dotnet/api/system.linq.expressions.expression.coalesce) — result-type rules and the non-nullable-left `InvalidOperationException` (re-verified July 2026).
- [Expression.Condition](https://learn.microsoft.com/en-us/dotnet/api/system.linq.expressions.expression.condition) — arm-type equality requirement.
- [Integral](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/integral-numeric-types) / [floating-point](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/floating-point-numeric-types) numeric types — literal typing and suffixes.
