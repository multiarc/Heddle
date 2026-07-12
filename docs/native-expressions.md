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
| shift | `<<` `>>` | left | integral left operand; `int` right operand |
| relational | `<` `<=` `>` `>=` | left | null operands compare `false` (lifted, non‑null result) |
| equality | `==` `!=` | left | `object.Equals` fallback for unrelated reference/mixed types |
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
(`null` in → `null` out). Relational and equality operators produce a plain `bool`: any `null`
operand compares `false`, and `null == null` is `true` — exactly as C#.

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

All are invariant‑culture and never throw at render (string‑returning ones map `null` input to
`""`):

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
| `abs`, `min`, `max`, `round`, `floor`, `ceil` | over `int`/`long`/`double`/`decimal` as applicable; clamped, non‑throwing |
| `range(start, last[, step])` | builds a `ForModel` for `@for` — iterates `start … last‑1` by `step` (default 1) |

<a id="range"></a>

**`range`.** The two overloads (`range(int, int)` and `range(int, int, int)`) return a
`ForModel { Start, Last, Step }`, so `@for(range(2, 10, 2))` iterates a start/step range with no
embedded C#. `last` is exclusive; `start >= last` renders empty (like `@for(0)`). The step must be
positive — unlike every other built‑in, `range` **validates** it: a zero or negative *literal*
step is a compile error (**HED4001**, positioned at the step argument), and a non‑positive step
known only at render throws `TemplateProcessingException` with the same message (a zero/negative
step would never terminate the loop). A host that registers its own `range` governs its own step
rules — the static check applies only to the built‑in. Calling `@range(1, 5)` standalone is legal
but renders `ForModel.ToString()` (useless); `range` is meant for `@for(...)`.

### Registering your own

```csharp
var functions = new FunctionRegistry();                 // starts with the built-ins
functions.Register("titlecase", (Func<string, string>)ToTitleCase);
functions.Register("slug", typeof(MyFns).GetMethod(nameof(MyFns.Slug))); // static method

var options = new TemplateOptions { Functions = functions };
```

- Names are ordinal and case‑sensitive.
- Registering the same name with identical parameter types **replaces**; otherwise it adds an
  overload. Overload resolution ranks exact match over widening over boxing to `object`.
- The registry **freezes on first compile use**; registering afterwards throws
  `InvalidOperationException`. Frozen registries are immutable and safe for concurrent compiles and
  renders.
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

1. `==`/`!=` on unrelated reference/mixed types compiles to a total, null‑safe `object.Equals`
   instead of a compile error.
2. `.` hops (and indexer targets) are null‑safe, yielding `default(T)`.
3. `-2147483648` types as `long` (first‑fit literal typing, without C#'s lexer special case); the
   value is identical.
4. Enum arithmetic (`enum + int`) is not supported.
5. The `enum & 0`‑literal special case is not carried over.
6. User‑defined *operators* are honored (e.g. `DateTime`/`TimeSpan`), but user‑defined *implicit
   conversions* are not consulted during promotion or arm unification.
7. `&&`/`||` reject `bool?` with a targeted error instead of C#'s wording.

## The sandbox

The compiler can only ever emit invocations of: property/indexer getters that pass the member‑tier
visibility and `[Hidden]` filter; the `MethodInfo`s/delegates the host registered (built‑ins
included); compiler‑chosen intrinsics (`string.Concat`, static `object.Equals`, conversions); and
user‑defined operator methods declared by the operand types themselves. Method‑call syntax
(`x.Foo()`), unregistered names, dynamic‑scope operands, assignment, lambdas, `new`, casts, and
`is`/`as` are all rejected at **compile time** with a positioned error — never executed. See the
[built‑in extension parameter docs](built-in-extensions.md) for how `@if`/`@for` consume these
expressions, and [csharp-api.md](csharp-api.md) for when to escalate to the `@` C# tier.

For host‑side guidance on exposing models safely — DTOs, `[Hidden]`, the registry freeze, render
budgets, and encoding contexts — see
[Exposing models to untrusted templates](patterns.md#exposing-models-to-untrusted-templates).
