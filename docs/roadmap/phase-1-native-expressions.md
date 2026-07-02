# Phase 1 — Native Expression Tier (sandbox‑safe, Roslyn‑free)

> Part of the [Heddle evolution roadmap](README.md). Status: **planned**.
> Depends on: nothing. Enables: sandboxing, phase 7 (build‑time/AOT backend), cheaper compiles.

## Goal

Bare expressions inside call parentheses — `@if(Count > 0)`, `@(Price * Quantity)`,
`@(Name ?? "anon")` — compile to `System.Linq.Expressions` delegates and render correctly with
`AllowCSharp = false` and **zero Roslyn involvement**. The inner‑`@` form (`@( @expr )`)
remains the unchanged full‑C#/Roslyn escape hatch.

This is the keystone phase: it raises the expression floor (today everything between a member
path and full C# falls off a cliff), makes a credible sandbox *possible* (expressions no
longer imply arbitrary code), removes Roslyn from the common compile path (startup cost), and
is the hard prerequisite for the build‑time/AOT backend (phase 7).

## Surface

No new sigil. Native expressions extend today's sigil‑less member paths — anything the member
tier already accepts stays a member path; operators, literals, and function calls make it a
native expression:

```heddle
@if(Items.Count > 0){{ <h3>Comments</h3> }}
@(Price * Quantity)
@(Name ?? "anonymous")
@(IsFeatured ? "★" : "")
@list(Articles){{ @if(::Year - PublishedOn.Year < 1){{ <span>new</span> }} }}
```

### Operator set — complete C#/C style

Exactly the operators `System.Linq.Expressions` supports natively, with **C# precedence
verbatim** — if you know C#, it just works, no surprising gaps:

| Level (high → low) | Operators | Expression‑tree mapping |
| --- | --- | --- |
| primary | `()` grouping, `.` member (null‑safe like the member tier), `[]` indexer, `name(args)` function | MemberAccess / ArrayIndex + indexers / Call (whitelist only) |
| unary | `!` `-` `+` `~` | Not, Negate, UnaryPlus, OnesComplement |
| multiplicative | `*` `/` `%` | Multiply, Divide, Modulo |
| additive | `+` `-` (`+` = string concat when an operand is string) | Add / `string.Concat`, Subtract |
| shift | `<<` `>>` | LeftShift, RightShift |
| relational | `<` `<=` `>` `>=` | LessThan … GreaterThanOrEqual (lifted‑null, as C#) |
| equality | `==` `!=` | Equal, NotEqual (`object.Equals` fallback for mixed reference types) |
| bitwise | `&`, then `^`, then `\|` | And, ExclusiveOr, Or (ints and enums) |
| logical | `&&`, then `\|\|` (bool‑only, short‑circuit) | AndAlso, OrElse |
| null‑coalescing | `??` (right‑assoc) | Coalesce |
| conditional | `?:` (right‑assoc) | Condition |

Notes:

- **Ternary is in.** The `?` token gates the `:`, so `:` stays unambiguous versus the chain
  delimiter — within the expression rule `:` is only consumed after a `?`, and the
  nested‑chain alternative stays ordered first so every currently valid `a():b()` parameter
  parses exactly as today.
- **`?.` is deliberately absent** — `.` hops are already null‑safe (member‑tier semantics);
  document this equivalence.
- **Excluded for simplicity and safety:** assignment / compound assignment, `++`/`--`,
  lambdas, casts, `is`/`as`, `new`, arbitrary method calls, string interpolation (use `+` or
  the `format` function).

### Literals — C#‑matching

Integer (incl. `0x` hex, `L` suffix), real with `f`/`d`/`m` suffixes, `"string"` with
standard escapes, `'c'` char, `true` / `false` / `null`.

## Design

### Grammar

- **Lexer** ([HeddleLexer.g4](../../src/Heddle.Language/HeddleLexer.g4)) — add the operator
  and literal tokens above to `mode CALL` **only** (keyword rules `true|false|null` placed
  before `CALL_ID`; maximal munch orders `??` before `?`, `<<`/`<=` before `<`, `==` before
  `=`, etc.). Every new token was previously a token‑recognition **error** in CALL mode, so no
  currently valid template changes meaning. `CS`/`CS_NESTED`/`CALL_RETURNED`/`OUT_MODE` modes
  untouched. One deliberate edge: `@out(true)` previously lexed `true` as an ID and failed
  compile; it becomes a literal (error → works).
- **Parser** ([HeddleParser.g4](../../src/Heddle.Language/HeddleParser.g4)) — a 4th `call`
  alternative, ordered **last** so ALL(*) first‑alternative‑wins preserves all existing
  parses:

  ```antlr
  call:
        extension_id? OUT_PARAMSTART CSHARP_START csharp_expression OUT_PARAMEND   // 1: C# (unchanged)
      | extension_id? OUT_PARAMSTART WS* member_expression? OUT_PARAMEND           // 2: member path / empty (unchanged)
      | extension_id? OUT_PARAMSTART chain OUT_PARAMEND                            // 3: nested chain (unchanged)
      | extension_id? OUT_PARAMSTART native_expression OUT_PARAMEND                // 4: NEW
      ;
  ```

  `native_expression` is a left‑recursive `expr` rule with one labeled alternative per
  precedence level from the table (ternary reuses the `DELIM` token, reachable only after
  `?`). One correction from grounding: ANTLR's left‑recursive alternatives are
  **left‑associative by default**, so the `??` and `?:` alternatives must carry the
  `<assoc=right>` option to deliver the right‑associativity the operator table promises —
  the ANTLR reference shows exactly this shape (`<assoc=right> e '?' e ':' e`).
  Member‑method syntax `x.Foo()` is **parsed but always compile‑rejected** with a
  targeted error pointing at registered functions / the `@` C# tier — better diagnostics than
  a parse error.
- Regenerate via [generate_cs.cmd](../../src/Heddle.Language/generate_cs.cmd) (ANTLR 4.13.1);
  commit `generated/` in a dedicated regen commit.

### AST and compile pipeline

- New `ExprNode` AST hierarchy (`BinaryNode`, `UnaryNode`, `TernaryNode`, `LiteralNode`,
  `PathNode {Segments, RootRef}`, `IndexNode`, `CallNode {Name, Args}` — each carrying a
  `BlockPosition`) under `src/Heddle/Language/Expressions/`. Carried on
  [CallParameter](../../src/Heddle/Language/CallParameter.cs) as a new `NativeExpression`
  field; extend the `IsModelTypeParameter` predicate. Built from the parse tree in
  `ParseContext.CreateItem` (~lines 399–494), which also emits editor tokens (new additive
  `HeddleTokenType.Operator/Literal/FunctionName` values).
- New `NativeExpressionCompiler` (`src/Heddle/Runtime/Expressions/`): builds a LINQ
  expression over `(object model, object chained, object root)` and returns the **existing**
  [CompiledParameter](../../src/Heddle/Runtime/Parameters/CompiledParameter.cs) shape
  (`Func<object,object,object,object>`), or the existing `ConstantParameter` when the tree
  folds to a literal. Zero changes to `TemplateItem`, `TemplateChain`, or `Scope`.
- New branch in `HeddleCompiler.CompileItem`
  ([HeddleCompiler.cs](../../src/Heddle/Runtime/HeddleCompiler.cs) lines 254–351) between the
  C# branch and the nested‑chain fallback; the produced result `ExType` threads into
  `CreateExtension`/`InitStart` exactly like the other tiers (so `@if(Count > 0)` hands
  `typeof(bool)` to `IfExtension` with no extension changes).
- Extract the property‑resolution loop from `CompileModelAccessor` (lines 353–425) into a
  shared internal helper so paths inside expressions get **identical** semantics to the
  member tier: null‑safe hops via `Expression.Condition`, `[Hidden]` filtering, accessibility
  rules, same error messages.

### Type binding rules (reflection + `ExType`, no Roslyn)

- Paths: shared resolver; result type = final property's `ExType`.
- Indexers: arrays → `Expression.ArrayIndex`; otherwise reflect the indexer matching the
  argument types (same visibility/`[Hidden]` rules).
- Numeric promotion per C# rules in one table‑driven `NumericPromotion` helper;
  lifted‑to‑null comparison semantics for nullables; `&&`/`||` require bool operands
  (compile error otherwise); `??` errors when the left side's static type is a non‑nullable
  value type; ternary arms must have a common type (C# conversion rules, simplified:
  identical, or one converts to the other).
- **Dynamic scope restriction (v1):** a `PathNode` operand on a `dynamic` scope is a compile
  error — *"Native expressions require a typed model; declare `@model(...)` or use the `@`
  C# tier."* Plain member paths on dynamic models are unaffected (they parse via alternative
  2 and keep using `DynamicParameter`). Operators over the runtime binder is a possible
  follow‑up, not v1.

### `name(args)` disambiguation and the function registry

- The nested‑chain alternative stays first, so **all currently valid input parses as
  today** — standalone `fn(Name)` remains a nested extension call. Function calls reach the
  expression grammar only where a chain cannot match: multiple args (`max(A, B)`), literal
  args (`fn("x")`), or embedded in operators (`upper(Name) == X`).
- For the one overlap — standalone `fn(Path)` where `fn` is not a definition or extension —
  unify at the compile seam: in the `TemplateFactory.Create` failure path, consult the
  function registry before emitting "Cannot find extension". Net resolution order:
  **definition → extension → registered function**, with a compile *warning* when a
  registered function name shadows an extension name.
- `FunctionRegistry` (new, `src/Heddle/Runtime/Expressions/`):
  `Register(string name, Delegate)` / `Register(string name, MethodInfo staticMethod)`;
  layered over a frozen `Default` set of safe built‑ins (`upper, lower, trim, len, contains,
  startswith, endswith, replace, substr` (clamped), `format, str, abs, min, max, round,
  floor, ceil` — invariant culture, exception‑safe). Exposed as `TemplateOptions.Functions`
  (null → `Default`); propagated via the options copy‑ctor; freezes on first compile use.
  Overload binding: name → arity → conversion rank (exact > widening > `object`); ambiguity
  or no match is a positioned compile error listing candidates.

### Sandbox invariant

The compiler can only ever emit calls to: (a) property getters already reachable by the
member tier (same `[Hidden]`/accessibility filter), (b) `MethodInfo`s the **host** explicitly
registered, (c) intrinsics the compiler itself chooses (`string.Concat`, `object.Equals`,
numeric conversions). There is no `Type.GetMethod` path reachable from template input.
Function registration is the documented trust boundary.

### Options model and back‑compat

```csharp
public enum ExpressionMode {
    MemberPathsOnly = 0,   // strict pre‑phase‑1 behavior (opt‑in escape hatch)
    Native          = 1,   // DEFAULT: sandbox‑safe expression tier
    FullCSharp      = 2    // implies Native; enables the inner‑@ Roslyn tier
}
```

- `TemplateOptions.ExpressionMode` defaults to `Native`. Safe default: every template that
  newly compiles was previously a compile error; every previously valid template parses via
  the unchanged alternatives 1–3.
- `AllowCSharp` becomes a bridge property over `ExpressionMode` (kept, later `[Obsolete]`);
  the guard at `HeddleCompiler.cs:301` keeps its exact behavior.
- Add `ExpressionMode`/`Functions` to the `TemplateOptions` copy‑ctor so child compiles
  (bodies, partials, imports) inherit them.

## Back‑compat analysis

| Surface | Risk | Prevention |
| --- | --- | --- |
| Member paths / nested chains / C# params | None — alternatives 1–3 untouched, ordered first | Golden files must pass byte‑identical |
| Templates whose parens previously failed to lex | Now may compile as expressions (error → works) | Acceptable by definition; release note |
| `@out(true)`‑style IDs named like keywords | Member → literal (was an error anyway) | Release note; not legal C# identifiers |
| Hosts equating `AllowCSharp=false` with "no logic" | Comparisons now compile | `MemberPathsOnly` escape hatch + release note |
| ANTLR regen churn under `generated/` | Noisy diffs | Single dedicated regen commit |

## Risks & mitigations

- **ALL(*) prediction cost/ambiguity** with left‑recursive `expr` beside `chain`: measure
  parse time of the existing fixture corpus; resolve warnings by alternative ordering first,
  targeted predicates only as a last resort. (M)
- **Operator semantics drift from C#** (promotion, lifted nulls, enum bitwise): one
  table‑driven test comparing against C#‑compiled expected values. (M)
- **Dynamic‑scope confusion**: precise compile error with the fix in the message. (S)
- **Registry misuse** (host registers a dangerous delegate): by‑design host responsibility;
  documented trust boundary.

## Success criteria

1. The flagship snippets compile and render with `AllowCSharp = false` and **zero Roslyn
   invocation** (assert `CSharpContext` produces no in‑memory assembly).
2. Full existing test suite + golden files pass unchanged under the `Native` default.
3. `ExpressionMode.MemberPathsOnly` restores today's errors for every expression fixture.
4. Every sandbox‑negative template below produces a positioned `HeddleCompileError` — never
   an exception, never code execution.
5. Registered functions work standalone (`@titlecase(Name)`) and inside expressions
   (`@(titlecase(Name) + "!")`).
6. Operator semantics test table matches C#‑computed results for all covered operator/type
   combinations.

## Validation scenarios

| Template | Model / options | Expected |
| --- | --- | --- |
| `@if(Items.Count > 0){{has items}}` | `Items = [1]`, AllowCSharp=false | `has items` |
| `@(Price * Quantity)` | `Price=2.5m, Quantity=4` | `10.0` (decimal promotion) |
| `@(Name ?? "anon")` | `Name=null` | `anon` |
| `@(IsFeatured ? "★" : "")` | `IsFeatured=true` | `★` (ternary) |
| `@(Flags & 4)`, `@(1 << Bits)` | ints/enums | C#‑identical results |
| `@(A + B * C == D && !E)` | ints/bools | equals the C#‑computed result (precedence) |
| `@(::Total - Amount)` | root + scoped values | correct root‑ref arithmetic |
| `@if(A.B > 0){{x}}` | `A=null` | empty (null‑safe hop → comparison false) |
| `@(max(A, B))` | `A=3, B=7` | `7` (multi‑arg → registry) |
| `@out(a():b())` | — | still parses as a nested chain (coexistence) |
| `@upper(Name)` with both an extension and a function named `upper` | — | extension wins + shadowing warning |
| **Negative** `@(Name.ToUpper())` | — | compile error "Method calls are not allowed…" |
| **Negative** `@(System.IO.File.ReadAllText("x"))` | — | rejected (no such property path; method call rejected) |
| **Negative** `@(evil(1))` unregistered | — | compile error "Cannot find extension or registered function" |
| **Negative** `@(X = 1)` | — | parse error (no assignment) |
| **Negative** `@(Count && true)` | `Count:int` | compile error (`&&` requires bool) |
| **Negative** dynamic model + `@(X > 1)` | `:: dynamic` | compile error "requires a typed model" |
| **Negative** `MemberPathsOnly` + `@(A + B)` | — | compile error naming the option |

## Size estimate

| Work item | Size |
| --- | --- |
| Lexer/parser rules + regen | M |
| AST + builder + `CallParameter`/`CreateItem` wiring | M |
| `NativeExpressionCompiler` (operators, promotion, indexers, ternary, folding) | **L** |
| `FunctionRegistry` + built‑ins + standalone‑call unification | M |
| `ExpressionMode` options + copy‑ctor | S |
| Tests (goldens, promotion table, negatives, sandbox suite) | M |

## .NET 10 opportunities (net10.0 target)

The engine multi‑targets `netstandard2.0;net6.0;net8.0;net10.0`, so everything below is either
automatic JIT behavior in the net10.0 build or explicitly noted as needing no guard at all.
Framing constraint for judging claims: this phase's hot path is a *cached*
`Func<object,object,object,object>` produced by `Expression.Compile` — a `DynamicMethod` the
JIT never inlines into callers — invoked through the polymorphic
`IRuntimeParameter.GetParameter` call site in `TemplateItem`. Verified against the official
.NET 10 notes (July 2026):

- **Delegate escape analysis (JIT, new in .NET 10)** — mechanism: escape analysis now covers
  delegates, so a `Func` object that provably never leaves its frame is stack‑allocated (the
  closure object still heap‑allocates; that part is slated for a future release). Microsoft's
  benchmark: a capture‑and‑invoke loop drops 19.5 ns / 88 B → 6.7 ns / 24 B. Benefit for this
  phase lands in the **compile pipeline**: `NativeExpressionCompiler`, overload ranking, and
  `NumericPromotion` lookups can use short‑lived helper lambdas without per‑compile heap churn.
  It does **not** cheapen invoking the cached `CompiledParameter` delegate — that delegate
  escapes by design (stored on the parameter). Conditionality: none — pure JIT behavior,
  automatic for net10.0 consumers; no `#if NET10_0_OR_GREATER` needed —
  [What's new in .NET 10 runtime — escape analysis](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/runtime#escape-analysis),
  [Performance improvements in .NET 10](https://devblogs.microsoft.com/dotnet/performance-improvements-in-net-10/).
- **Small‑array stack allocation (JIT, new in .NET 10)** — mechanism: the JIT now
  stack‑allocates small fixed‑size arrays of value types *and* reference types that don't
  escape, and inliner heuristics were tuned to favor callees returning small fixed‑size arrays.
  Benefit: compile‑time scratch arrays (`Type[]` buffers in overload ranking, operand‑type
  pairs in the promotion table) become allocation‑free when self‑contained; caveat — an array
  passed to a non‑inlined callee escapes, so this only covers leaf helpers. Conditionality:
  none — automatic on net10.0 —
  [What's new in .NET 10 runtime — stack allocation](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/runtime#stack-allocation).
- **Late devirtualization + inlining (JIT, new in .NET 10)** — mechanism: the inliner now
  inlines methods that become devirtualizable due to earlier inlining, propagates precise
  return types for late devirtualization, and relaxes size limits under profile data. Benefit:
  the polymorphic `IRuntimeParameter.GetParameter` dispatch (Constant / Compiled / Dynamic /
  Chained implementations) devirtualizes and inlines more reliably under tiered PGO, leaving
  the delegate call into the compiled method as the only opaque step per parameter — a modest
  render‑path win. Conditionality: none — automatic on net10.0 —
  [What's new in .NET 10 runtime — inlining improvements](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/runtime#inlining-improvements).
- **C# 14 for the new code (implementation convenience only)** — the `field` keyword fits
  `TemplateOptions.Functions` (null → `Default` plus freeze‑on‑first‑use logic in the accessor,
  no hand‑written backing field), and null‑conditional assignment tidies copy‑ctor/registry
  plumbing. Honesty note: [Heddle.csproj](../../src/Heddle/Heddle.csproj) already sets
  `LangVersion=latest`, and these features are runtime‑independent — they compile for **all**
  TFMs under the .NET 10 SDK, so no `#if NET10_0_OR_GREATER` applies (this is an SDK feature,
  not a net10.0‑target feature). One 10‑era caution: C# 14's first‑class span conversions can
  silently retarget overload resolution in engine source (e.g. `Contains` binding to
  `MemoryExtensions.Contains`); registry built‑ins bind by explicit `MethodInfo`, which is
  immune — keep it that way —
  [What's new in C# 14](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-14).
- **Interpreter‑safe built‑in binding (forward‑looking)** — mechanism: the
  `System.Linq.Expressions` *interpreter* (`Compile(preferInterpretation: true)`, and the only
  path under NativeAOT — relevant to phase 7) throws on expressions calling methods with
  `Span<T>`/ref‑struct parameters, and .NET 10‑era overload resolution selects span overloads
  more often, surfacing the bug. Benefit: binding `FunctionRegistry.Default` built‑ins to
  non‑span overloads explicitly (e.g. `string.Contains(string)`, never a `MemoryExtensions`
  extension) costs nothing now and keeps the function set portable to the interpreter later.
  Conditionality: applies to all TFMs; it's a compatibility rule, not a guard —
  [dotnet/roslyn#81329](https://github.com/dotnet/roslyn/issues/81329).

Investigated and rejected — not .NET 10‑specific or not applicable:

- **Generic math (`INumber<T>`) for `NumericPromotion`** — shipped in .NET 7, so not
  10‑specific; and a poor fit regardless: the promotion helper works over *reflected* operand
  `Type`s at template‑compile time to insert `Expression.Convert` nodes, while generic‑math
  operators are C# compile‑time constraints and the built‑in numerics implement only
  homogeneous (`TSelf, TSelf`) shapes — mixed‑type promotion (`int + long`) is exactly what
  they don't do. The table‑driven design stands —
  [Generic math](https://learn.microsoft.com/en-us/dotnet/standard/generics/math).
- **`System.Linq.Expressions` itself** — no .NET 10 compile‑ or interpret‑performance work;
  neither the release notes nor the performance post touch it (the library is in maintenance
  mode). No free wins there.
- **Escape analysis vs. the `(object,object,object) → object` boxing** — the realistic answer
  to the marketing question: the box for a value‑typed result is created *inside* the compiled
  `DynamicMethod`, which the JIT never inlines into the render loop, so it always escapes —
  .NET 10's stack‑allocation advances don't remove it. If it ever matters, the fix is
  engine‑side (e.g. emitting cached `true`/`false` boxes as `Expression.Constant` of type
  `object`), not a runtime‑version bump.

## Open questions

1. Should `format(...)` support composite formatting (`format("{0:d}", X)`) in v1, or only
   single‑value `format(X, "d")`? (Lean: single‑value; composite later.)
2. Char literal `'c'` — worth the token, or defer? (Lean: include; trivial.)
3. Whether to expose the operator‑semantics test table in docs as the authoritative
   "what native expressions do" reference. (Lean: yes.)

## External grounding

Load‑bearing claims above, verified against primary sources (July 2026):

- **ANTLR left‑recursive expression rules** — direct left recursion is rewritten into a
  precedence‑climbing form where the alternative listed first gets the highest precedence,
  and right associativity is declared per alternative via `<assoc=right>` (the reference
  shows the exact ternary shape `<assoc=right> e '?' e ':' e`) —
  [antlr4 doc/left-recursion.md](https://github.com/antlr/antlr4/blob/master/doc/left-recursion.md).
- **ALL(\*) first‑alternative‑wins** — "ALL(\*) uses the order of the productions in a rule
  to resolve ambiguities in favor of the production with the lowest number"; this is what
  makes ordering the new `call` alternative last a sound back‑compat strategy —
  [ALL(\*) tech report, Parr/Harwell/Fisher](https://www.antlr.org/papers/allstar-techreport.pdf).
- **Lexer maximal munch** — "the primary goal is to match the lexer rule that recognizes
  the most input characters", so `??`/`<<`/`==` beat their one‑char prefixes regardless of
  rule order —
  [antlr4 doc/wildcard.md](https://github.com/antlr/antlr4/blob/master/doc/wildcard.md).
- **Lexer rule‑order tie‑break** — "To resolve ambiguities, ANTLR gives precedence to the
  lexical rules specified first"; the reason `true|false|null` must precede `CALL_ID` —
  [antlr4 doc/faq/lexical.md](https://github.com/antlr/antlr4/blob/master/doc/faq/lexical.md).
- **C# operator precedence** — the table above matches the official order exactly
  (… equality → `&` → `^` → `|` → `&&` → `||` → `??` → `?:`), and `??` and `?:` are both
  right‑associative —
  [C# operators and expressions](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/).
- **Factory coverage** — every mapped factory exists on `Expression` (`LeftShift`,
  `RightShift`, `OnesComplement`, `AndAlso`/`OrElse`, `Condition`, `Coalesce`, comparisons
  with `liftToNull` overloads). Two gotchas: the predefined bitwise/shift forms are
  integral‑only, so enum operands need a `Convert` to their underlying type (as the C#
  compiler emits), and predefined shifts require an `Int32` right operand, same as C# —
  [Expression class](https://learn.microsoft.com/en-us/dotnet/api/system.linq.expressions.expression).
- **Ternary** — `Expression.Condition` throws unless `ifTrue.Type == ifFalse.Type`; the
  overload taking a `Type` only unifies *reference‑assignable* arms, so numeric arm
  unification must insert `Convert` nodes (the "common type" rule above) —
  [Expression.Condition](https://learn.microsoft.com/en-us/dotnet/api/system.linq.expressions.expression.condition).
- **`??`** — `Expression.Coalesce` throws `InvalidOperationException` when the left
  operand's type is neither a reference type nor a nullable value type — exactly the
  compile error the binding rules specify —
  [Expression.Coalesce](https://learn.microsoft.com/en-us/dotnet/api/system.linq.expressions.expression.coalesce).
- **Lifted‑null comparisons** — the comparison factories take `liftToNull`; passing `false`
  yields a `Boolean` node where null operands compare `false`, which is the C# semantics
  the relational row promises —
  [Expression.LessThan](https://learn.microsoft.com/en-us/dotnet/api/system.linq.expressions.expression.lessthan).
- **Integer literals** — `0x`/`0X` hex prefix and `L` suffix confirmed (lowercase `l` is
  legal but draws a compiler warning; the lexer should accept both) —
  [Integral numeric types](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/integral-numeric-types).
- **Real, char, and string literals** — `f`/`F` = `float`, `d`/`D` = `double`,
  `m`/`M` = `decimal`, suffix‑less reals default to `double`; `'c'` char literals and the
  standard escape set (`\n \t \" \\ \uHHHH \xH…`) are as the C# pages document —
  [Floating‑point numeric types](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/floating-point-numeric-types).
- **Precedent** — the registry‑as‑trust‑boundary model matches how other .NET template
  engines scope safety: Fluid is "secure by default" because model members and filters are
  allow‑listed ("Secure templates by allow‑listing all available properties in the
  template. User templates can't break your application"), and Scriban's sandbox likewise
  has the host decide which objects and functions templates may call —
  [Fluid](https://github.com/sebastienros/fluid).
