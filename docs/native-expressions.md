# Native Expressions

Native expressions are the sandbox‑safe middle tier of Heddle's expression surface. They sit
between plain member paths (`@(A.B.C)`) and the full‑C# escape hatch (`@( @expr )`): bare
expressions written inside call parentheses — operators, literals, and registered function calls —
compile to `System.Linq.Expressions` delegates and render with **zero Roslyn involvement**.

```heddle
@if(Items.Count > 0){{ <h3>Comments</h3> }}
@(Price * Quantity)
@(Name ?? "anonymous")
@(IsFeatured ? "★" : "")
@(::Year - PublishedOn.Year < 1 ? "new" : "")
```

Anything the [member tier](language-reference.md#member-expressions-abc) already accepts stays a
member path; an operator, a literal, or a multi‑argument function call is what makes a parameter a
native expression. No new sigil is introduced.

## When to use which tier

| You need… | Use | Notes |
| --- | --- | --- |
| A property value | member path `@(A.B.C)` | null‑safe hops, no operators |
| Arithmetic, comparisons, string building, a whitelisted function | **native expression** | this page |
| Arbitrary C#, method calls, LINQ, `new` | the `@` C# tier `@( @expr )` | requires `ExpressionMode.FullCSharp`; see [csharp-api.md](csharp-api.md) |

The native tier is on by default (`ExpressionMode.Native`). Because every native expression that
newly compiles was previously a compile error, turning it on is backward‑compatible.

## Operators

C# precedence, verbatim. Highest to lowest:

| Level | Operators | Assoc. | Notes |
| --- | --- | --- | --- |
| primary | `()` grouping, `.` member (null‑safe), `[]` indexer, `name(args)` function | left | shared member‑path chain / array & indexer access / registry‑bound call |
| unary | `!` `-` `+` `~` | right | `Not`, `Negate`, `UnaryPlus`, `OnesComplement` |
| multiplicative | `*` `/` `%` | left | |
| additive | `+` `-` | left | `+` is string concatenation when an operand is a string |
| shift | `<<` `>>` | left | integral left operand; **any** integral right operand, converted to `int` (a [deviation](#deviations-from-c)) |
| relational | `<` `<=` `>` `>=` | left | a `null`‑*valued* `Nullable<T>` compares `false` (lifted, non‑null result); the `null` **literal** is `HED1008` |
| equality | `==` `!=` | left | `object.Equals` fallback when **both** operands are reference types or `Nullable<T>`; a reference/value mix is `HED1008` |
| bitwise AND | `&` | left | ints, same‑enum, and `bool` |
| bitwise XOR | `^` | left | |
| bitwise OR | `\|` | left | |
| logical AND | `&&` | left | non‑nullable `bool` only, short‑circuit |
| logical OR | `\|\|` | left | non‑nullable `bool` only, short‑circuit |
| null‑coalescing | `??` | **right** | left operand must be a reference type or `Nullable<T>` |
| conditional | `?:` | **right** | condition must be `bool`; arms need a common type |

### Numeric promotion

Binary numeric operands are promoted to a common type before the operation, following the C#
rules (first match wins): `decimal` → `double` → `float` → `ulong` → `long` → (`uint` with a
signed operand becomes `long`) → `uint` → `int`. Mixing `decimal` with `float`/`double`, or
`ulong` with a signed integral, has no common type and is a compile error.

### Lifted (nullable) operands

When either operand is `Nullable<T>`, arithmetic and bitwise operators produce a nullable result
(`null` in → `null` out). Relational and equality operators produce a plain `bool`: a `null` operand
compares `false`, and `null == null` is a constant `true`.

**Lifting is numeric-path only.** A mixed-nullability pair of a *non-numeric* type does not lift, so
where C# has a lifted operator this tier has none:

| Expression | C# | Here |
| --- | --- | --- |
| `@(FlagNullable == Flag)` (`bool?` vs `bool`) | lifted, compiles | `HED1008` |
| `@(FlagNullable & Flag)` (`bool?` vs `bool`) | lifted, compiles | `HED0005`, the compile-item catch-all |
| `@(N < 3)` (`int?` vs `int`) | lifted, compiles | lifted, compiles |

The bitwise row is a **known defect, not a deviation**: every comparable illegality in this tier is a
positioned `HED1008`, and that one shape reaches `Expression.And` unguarded. Both tiers agree on
refusing it, so it is not drift — it is the wrong diagnostic rather than a wrong answer, and it is
filed rather than papered over here. `NativeOperatorRules.ClassifyBitwise` carries the same verdict at
build time.

The `null` **literal** is separate from a `null`-valued `Nullable<T>`: `@(x < null)` and
`@(3 == null)` are `HED1008`, exactly as C# rejects them.

### Why there is no `?.`

Member hops are **already null‑safe**: a hop off a `null` reference yields `default(T)` of the
property's type — `null` for reference types and `Nullable<T>`, the zero value for other value
types. So `@if(A.B > 0)` with `A == null` evaluates `0 > 0` and renders nothing. Because `.` is
null‑safe, a separate `?.` operator would be redundant, and it is deliberately absent.

## Literals

The full C# literal set except verbatim/interpolated/raw strings:

| Literal | Type |
| --- | --- |
| `42`, `0x2A`, `0b101010`, `1_000` | first of `int` → `uint` → `long` → `ulong` that fits |
| `42L`, `42u`, `42ul` | `long` / `uint` / `ulong` per the suffix |
| `1.5`, `1e3` | `double` |
| `1.5f` / `1.5d` / `1.5m` | `float` / `double` / `decimal` |
| `"text"` | `string` (standard escapes: `\' \" \\ \0 \a \b \e \f \n \r \t \v \xH…H \uHHHH \UHHHHHHHH`) |
| `'c'` | `char` (same escape set) |
| `true` / `false` | `bool` |
| `null` | typed by the consuming operator |

String interpolation is intentionally excluded — use `+` or the `format` function instead.

## `this` — the current model

`this` is the current scope's model, typed as the current scope type. It fills the one gap a bare
member path can't: naming the model **itself** rather than something derived from it.

- As a **whole** expression, `this` is the model passthrough — it compiles to the empty parameter
  and works on `dynamic` scopes too, exactly like an empty member path. Its flagship use is passing
  the current model into a [parameterized slot](language-reference.md#parameterized-slots-out-type):
  `@list(Options){{ @out(this) }}`.
- As an **operand or path root**, `this` is a typed operand and follows the same rule as any path:
  `this.Name`, `len(this)`, and `this == null` need a typed model (a `dynamic` scope reports
  **HED1004**). `this.<name>` is also the explicit escape for a model member a
  [prop shadows](language-reference.md#props-nameprop-type--default).

`this` is a C# keyword, so it can never collide with a model member. It is expression‑tier, so it
reports **HED1014** under `MemberPathsOnly`.

## Native expressions as named‑argument values

The value of a [prop named argument](language-reference.md#props-nameprop-type--default) is a native
expression — `@card(Article, style: Featured ? "wide" : "plain", tag: upper(Kind))`. Every construct
on this page is allowed there (paths off the caller model, `::` root refs, operators, functions,
`this`, literals). A named‑argument value is **not** a C# `@`‑tier expression and **not** a call
chain: `@card(A, x: @ expr)` and `@card(A, x: a():b())` are syntax errors. Compute anything the
native tier can't express in the model or a registered function.

## Registered functions

Native expressions can call functions the host has registered, plus a frozen set of built‑ins.
Registration is the **trust boundary**: anything registered is callable from template text, and
nothing else is. There is no path from template text to arbitrary methods by name.

### The default built‑ins

All are invariant‑culture, and all but `range` never throw at render (string‑returning ones map
`null` input to `""`). `range` is the one sanctioned exception: a non‑positive step known only at
render throws, because the alternative is a loop that never terminates — see [`range`](#range).

| Function | Behavior |
| --- | --- |
| `upper(s)` / `lower(s)` | invariant upper/lower case |
| `trim(s)` | trims Unicode whitespace both ends |
| `len(s)` | UTF‑16 code‑unit length |
| `contains(s, v)` / `startswith(s, v)` / `endswith(s, v)` | ordinal, `bool` |
| `replace(s, old, new)` | ordinal replace; empty `old` returns `s` unchanged |
| `substr(s, start[, length])` | `start`/`length` clamped into range |
| `format(value, fmt)` | `IFormattable.ToString(fmt, InvariantCulture)` |
| `format(fmt, args…)` | composite `string.Format(InvariantCulture, …)` |
| `str(value)` | invariant `Convert.ToString` |
| `abs`, `min`, `max` | `int`, `long`, `double`, `decimal` (one overload per type); clamped, non‑throwing |
| `round`, `floor`, `ceil` | `int`, `long`, `double`, `decimal` (as `abs`/`min`/`max`); `round` also takes a digit count. The integral overloads are the identity |
| `range(start, last[, step])` | builds a `Heddle.Models.Range` for `@for` — iterates `start … last‑1` by `step` (default 1) |

<a id="range"></a>

**`range`.** The two overloads (`range(int, int)` and `range(int, int, int)`) return a
`Heddle.Models.Range { Start, Last, Step }` — an immutable value type — so `@for(range(2, 10, 2))`
iterates a start/step range with no
embedded C#. `last` is exclusive; `start >= last` renders empty (like `@for(0)`). The step must be
positive — unlike every other built‑in, `range` **validates** it: a zero or negative *literal*
step is a compile error (**HED4001**, positioned at the step argument), and a non‑positive step
known only at render throws `TemplateProcessingException` with the same message (a zero/negative
step would never terminate the loop). A host that registers its own `range` governs its own step
rules — the static check applies only to the built‑in. Calling `@(range(1, 5))` standalone is legal
and renders the readable call form `range(1, 5)`. Note that a standalone `@fn(...)` accepts only a
single positional expression, so a multi‑argument call must be wrapped as `@( fn(a, b) )` —
`@range(1, 5)` written directly is a parse error (**HED0003**). `range` is meant for `@for(...)`.

### Registering your own

```csharp
var functions = new FunctionRegistry();                 // starts with the built-ins
functions.Register("titlecase", (Func<string, string>)ToTitleCase);
functions.Register("slug", typeof(MyFns).GetMethod(nameof(MyFns.Slug))); // static method

var options = new TemplateOptions { Functions = functions };
```

- Names are ordinal and case‑sensitive.
- Registering the same name with identical parameter types **replaces**; otherwise it adds an
  overload. Overload resolution ranks each candidate on a flat scale — exact match over implicit
  widening over boxing to `object` — and refuses when two candidates tie, rather than applying C#'s
  better‑conversion‑target rule. Both tiers share one ranker, so a call that binds at build time
  binds identically at run time and a tie is `HED1013` on both.
  The consequence is narrower acceptance than C#, not a different winner: measured over the shipped
  built‑in table, **0 of 480** argument combinations would change which overload wins under C#'s rule,
  **100** would become bindable that are ties today, and **38** stay ambiguous either way (`double`
  and `decimal` are mutually non‑convertible, so neither is closer). Adopting C#'s rule would *widen*
  what compiles, which cannot be withdrawn later, so it is a **window‑gated** change rather than a fix
  to make casually.
  Note that betterness is not what makes an integral argument bind to `floor`/`ceil`/`round`: with only
  a `double` and a `decimal` overload the two are mutually non‑convertible, so C# reports the tie too —
  `Math.Floor(3)` is `CS0121`. What binds `floor(3)` is that those three carry an `int` and a `long`
  overload, exactly as `abs`/`min`/`max` do.
- The registry **freezes when a native expression is first compiled against it** — not when a
  template is merely compiled, so a template containing no native expression leaves it open.
  Registering after the freeze throws `InvalidOperationException`. Frozen registries are immutable
  and safe for concurrent compiles and renders, which is why the freeze exists.
  Registration order therefore matters: register everything before the first render, not lazily on
  demand.
- `null` `TemplateOptions.Functions` means `FunctionRegistry.Default` (the frozen built‑ins).

### Standalone vs. in‑expression calls

A standalone `@fn(x)` resolves in the order **definition → extension → registered function**. If a
registered function name collides with an extension, the extension wins and a warning is emitted —
invoke the function inside an expression (`@( fn(x) )`) to disambiguate. Inside an expression,
`fn(...)` resolves against the registry only; a name that is an extension there is a compile error.

## `ExpressionMode`

`TemplateOptions.ExpressionMode` selects the tier:

| Value | Meaning |
| --- | --- |
| `MemberPathsOnly` | strict pre‑1.0 surface: member paths, nested chains, empty parameters only |
| `Native` (default) | adds the native expression tier |
| `FullCSharp` | implies `Native`; additionally enables the inner‑`@` Roslyn C# tier |

`AllowCSharp` is a bridge over this enum: `AllowCSharp = true` selects `FullCSharp`; reading it
returns whether the mode is `FullCSharp`. It is retained for compatibility and marked
`[Obsolete]` since 2.x — reads and writes keep working; new code uses `ExpressionMode`.

## Deviations from C#

Native expressions match C# except for a small, deliberate set of ergonomic choices:

1. `==`/`!=` on **unrelated reference types** compiles to a total, null‑safe `object.Equals`
   instead of a compile error — `@(Maker == Where)` renders `False` rather than failing to compile.
   The fallback requires **both** operands to be a reference type or `Nullable<T>`; a
   reference/value mix such as `@(Name == Count)` is a positioned `HED1008` on **both** tiers, which
   is what `NativeExpressionCompiler`'s `IsReferenceish(left) && IsReferenceish(right)` guard decides
   and what `OperatorGuardDifferentialTests.MixedTypeEquality_CompilesTheConsumerProject_AndDegrades`
   pins. **Do not widen the guard to match a looser reading of this rule:** doing so turns a compile
   error into a silent `false`, which is a breaking change and window‑gated.
2. `.` hops (and indexer targets) are null‑safe, yielding `default(T)`.
3. `-2147483648` types as `long` (first‑fit literal typing, without C#'s lexer special case); the
   value is identical.
4. Enum arithmetic (`enum + int`) is not supported.
5. The `enum & 0`‑literal special case is not carried over.
6. User‑defined *operators* are honored for **arithmetic, relational, equality and `??`** (e.g.
   `DateTime`/`TimeSpan`), and **not at all** for `&`/`^`/`|`, `<<`/`>>`, or any unary operator —
   those arms refuse a non‑numeric, non‑`bool`, non‑enum operand before an operator method could be
   found. User‑defined *implicit conversions* are never consulted, in any arm, during promotion or
   arm unification.
7. `&&`/`||` reject `bool?` with a targeted error instead of C#'s wording.
8. `<<`/`>>` accept **any** integral right operand and convert it to `int`, so `@(I << L)` compiles
   here and is `CS0019` in C#. **Do not narrow this to match C#:** it would break templates that
   compile today, so it is window‑gated.
9. Mixed nullability does not lift outside the numeric paths — see
   [Lifted (nullable) operands](#lifted-nullable-operands) for the two shapes and which of them is a
   deviation and which is a defect.

## Diagnostics

Every diagnostic the native tier raises. All are **compile-time** and positioned at the offending
construct, so none of them can reach render. The build tier raises the same id for the same input —
that is the match requirement — and where it cannot prove the refusal it degrades the call to the
dynamic tier instead of guessing.

| ID | Severity | Raised when |
| --- | --- | --- |
| `HED1001` | error | A function name matches neither the registry nor an extension/definition. |
| `HED1002` | error | An in‑expression call names an extension or definition rather than a registered function. Call it standalone, or register a function. |
| `HED1003` | error | Method‑call syntax (`x.Foo(...)`) appears in an expression. Only registered functions are callable. |
| `HED1004` | error | An operand is a dynamic scope, or a path crosses a `[Dynamic]` property. Declare a typed `@model`, or use the `@` C# tier. |
| `HED1005` | error | `&&`/`\|\|` applied to a non‑`bool` operand — including `bool?`, which C# would accept. |
| `HED1006` | error | `??` applied to a left operand that is a non‑nullable value type. |
| `HED1007` | error | The `?:` arms have no common type. Also raised for a `??` pair with no common type. |
| `HED1008` | error | A binary operator has no rule for its operand types — a reference/value equality mix, a promotion with no common type, enum arithmetic, a `null` literal in a relational position — or a numeric literal overflows its type. |
| `HED1009` | error | A unary operator is not defined for its operand type (`-` on `ulong` or a non‑numeric, `~` on a non‑integral, `!` on a non‑`bool`). |
| `HED1010` | error | An indexer target has no accessible indexer matching the argument types. |
| `HED1011` | error | The `?:` condition is not `bool`. |
| `HED1012` | error | No overload of a registered function binds to the supplied argument types. |
| `HED1013` | error | A registered‑function call is ambiguous — two candidates tie under the flat rank. `min(1, 2u)` is the canonical case; see [Registering your own](#registering-your-own). |
| `HED1014` | error | An expression beyond a bare member path is used while `ExpressionMode` is `MemberPathsOnly`. |
| `HED1015` | error | A composite `format` literal references an argument index beyond the supplied count. |
| `HED1016` | warning | A standalone `@name(...)` resolved to an extension that shadows a registered function of the same name. Write `@( name(...) )` to reach the function. |
| `HED1017` | error | A standalone registry hit was given a chain or C#‑parameter shape rather than a single expression. |

A member‑path segment that fails resolution is **`HED0001`**, not a `HED1xxx`: the member tier is
shared with the C# tier and the dynamic path, so its diagnostic is shared too. It fires when a segment
is missing, non‑readable, `[Hidden]`, or has an inaccessible getter — see
[Exposing models to untrusted templates](patterns.md#exposing-models-to-untrusted-templates).

## The sandbox

The compiler can only ever emit invocations of: property/indexer getters that pass the member‑tier
visibility and `[Hidden]` filter; **array element access** (`Expression.ArrayIndex`, single- and
multi-dimensional), which invokes nothing but is an emitted access all the same; the
`MethodInfo`s/delegates the host registered (built‑ins included); compiler‑chosen intrinsics
(`string.Concat`, static `object.Equals`, conversions); and user‑defined operator methods declared by
the operand types themselves. Method‑call syntax
(`x.Foo()`), unregistered names, dynamic‑scope operands, assignment, lambdas, `new`, casts, and
`is`/`as` are all rejected at **compile time** with a positioned error — never executed. See the
[built‑in extension parameter docs](built-in-extensions.md) for how `@if`/`@for` consume these
expressions, and [csharp-api.md](csharp-api.md) for when to escalate to the `@` C# tier.

For host‑side guidance on exposing models safely — DTOs, `[Hidden]`, the registry freeze, render
budgets, and encoding contexts — see
[Exposing models to untrusted templates](patterns.md#exposing-models-to-untrusted-templates).

---

*Verified against source at `6639f6f` (2026-07-26).* Claims marked ✓ are gated by a test:
the diagnostics table ✓ (`DiagnosticIdTests.EveryShippedIdIsNamedInAPublishedDocument` — every id
here is a shipped constant and this page is its registry-designated home); deviation 1's guard ✓
(`OperatorGuardDifferentialTests.MixedTypeEquality_CompilesTheConsumerProject_AndDegrades`); the
operator legality table and the lifted-operand shapes ✓ (`NativeOperatorRulesTests`, which drives the
shared rule core both tiers use); the overload-rank measurement ✓
(`OverloadBetternessEvaluationTests`). Everything else on this page is dated-verified, not gated —
which matters: an unmarked claim is **evidence of intent, not an authority**, so a contradiction
between it and both tiers agreeing is investigated and recorded, never resolved by editing code to
match the sentence.
