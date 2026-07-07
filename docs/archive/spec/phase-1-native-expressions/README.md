# Phase 1 specification — native expressions

Status: **Specified — ready for implementation.**
Roadmap source: [phase 1 — native expression tier](../../roadmap/phase-1-native-expressions.md).
Prior phases: none — this is the first phase implemented
([D5, sequential order](../common/cross-cutting-decisions.md#d5--sequential-implementation-order));
two cross-cutting kickoff items land here:
[D1 diagnostic IDs](../common/cross-cutting-decisions.md#d1--stable-diagnostic-ids-hedxxxx) and
[D3 C# 14 span audit](../common/cross-cutting-decisions.md#d3--one-time-c-14-first-class-span-audit).

### Documents

| Document | Purpose |
| --- | --- |
| README.md (this file) | The spec proper: assumed state, decision records, implementation plan, API contract, diagnostics, testing, back-compat, performance. |
| [grammar.md](grammar.md) | Full lexer/parser rule diffs (CALL mode tokens, the 4th `call` alternative, left-recursive `expr`), coexistence analysis, editor-token mapping, regen procedure, and the parse corpus. |
| [operator-semantics.md](operator-semantics.md) | The normative operator table: precedence, numeric promotion, lifted nulls, enum bitwise, string concat, equality fallback, literal typing, constant folding, and the emission whitelist. |

## Scope and goal

Bare expressions inside call parentheses — `@if(Count > 0)`, `@(Price * Quantity)`,
`@(Name ?? "anon")`, `@(IsFeatured ? "★" : "")` — compile to `System.Linq.Expressions`
delegates and render correctly with `AllowCSharp = false` and zero Roslyn involvement.
The inner-`@` form (`@( @expr )`) remains the unchanged full-C#/Roslyn escape hatch.

This spec covers: the grammar change (the only one until phase 5), the `ExprNode` AST and
its builder, the `NativeExpressionCompiler`, the shared member-path resolver extraction,
the `FunctionRegistry` with its frozen default whitelist, the
`ExpressionMode`/`AllowCSharp` options model, the phase's diagnostics (including the
`HEDxxxx` plumbing itself), the published docs page, and the full test/benchmark plan.
Out of scope (deferred, with triggers, in [Deferred items](#deferred-items)): operators
over dynamic scopes, `?.`, verbatim/interpolated strings, partial constant folding.

## Assumed state

No prior phase exists; the seams below were **re-verified against the current source**
(July 2026). Roadmap line-number claims all check out — noted per seam.

| Seam | Verified state |
| --- | --- |
| [CallParameter](../../../src/Heddle/Language/CallParameter.cs) | Three mutually exclusive parameter shapes: `ModelParameter` (string segments) + `RootReference`, `ChainParameter`, `CSharpExpression`. `IsModelTypeParameter => ChainParameter == null && CSharpExpression == null`. Gains `NativeExpression` here. |
| [ParseContext.CreateItem](../../../src/Heddle/Language/ParseContext.cs) | Private, lines 399–494 exactly as the roadmap states; builds `OutputItem`+`CallParameter` from `HeddleParser.CallContext` and emits editor tokens through `AddToken` (no-op unless `ProvideLanguageFeatures`). The AST wiring lands here. |
| [HeddleCompiler.CompileItem](../../../src/Heddle/Runtime/HeddleCompiler.cs) | Lines 254–351 as stated. Branch order: memoization check → definition lookup → `IsModelTypeParameter` (member path / empty) → `CSharpExpression` (the `AllowCSharp` guard is at line 301, message "C# Code Not allowed here, see TemplateOptions.AllowCSharp Property") → nested-chain fallback → `CreateExtension`. The native branch inserts between the C# branch and the chain fallback. |
| [HeddleCompiler.CompileModelAccessor](../../../src/Heddle/Runtime/HeddleCompiler.cs) | Lines 353–425 as stated. Dynamic scopes short-circuit to `DynamicParameter`/`RootDynamicParameter`; the typed loop reflects each segment with `BindingFlags.Instance \| Static \| NonPublic \| Public`, rejects on `!CanRead`, `[Hidden]`, or getter neither assembly nor public, error `Property {0} not found in Type [{1}]`; a `[Dynamic]` property mid-path hands off to `DynamicParameter` wrapping a `ModelParameter` prefix. This loop is the extraction target. |
| [ModelParameter](../../../src/Heddle/Runtime/Parameters/ModelParameter.cs) | `GetPropertyChainAccessor` builds the null-safe chain: value-type input → direct `MakeMemberAccess`; reference input → `Condition(Equal(input, null), Default(prop.PropertyType), MakeMemberAccess)`. Null hop therefore yields `default(T)`. Shared with the native tier via extraction. |
| [CompiledParameter](../../../src/Heddle/Runtime/Parameters/CompiledParameter.cs) / [ConstantParameter](../../../src/Heddle/Runtime/Parameters/ConstantParameter.cs) | `CompiledParameter.ParameterImplementation : Func<object,object,object,object>` invoked as `(scope.ModelData, scope.ChainedData, scope.RootData)`; `ConstantParameter` wraps a fixed object. The native tier emits **only these two existing shapes** — zero changes to `TemplateItem`, `TemplateChain`, [Scope](../../../src/Heddle/Data/Scope.cs). |
| [TemplateOptions](../../../src/Heddle/Data/TemplateOptions.cs) | `AllowCSharp` is a plain bool, defaulted `false` in both ctors, copied in the copy-ctor (used for child compiles at [CompileContext](../../../src/Heddle/Runtime/CompileContext.cs) line 82). `Equals`/`GetHashCode` compare only `FileNamePostfix`/`TemplateName`/`RootPath` — `AllowCSharp` never participated in identity. [TemplateResolver](../../../src/Heddle/Runtime/TemplateResolver.cs) sets `AllowCSharp = true` for hosted views (lines 72, 84). |
| [TemplateFactory.Create](../../../src/Heddle/Runtime/TemplateFactory.cs) | Static registry keyed by ordinal case-sensitive name; the failure path adds `Cannot find extension <{0}>` and returns `null`. [EmptyExtension](../../../src/Heddle/Extensions/EmptyExtension.cs) is registered as `""` and renders `ModelData?.ToString()` — the carrier for standalone registered-function calls. [IfExtension](../../../src/Heddle/Extensions/IfExtension.cs) treats non-bool data as truthy and `bool` data as the condition — `@if(Count > 0)` needs no extension change once the parameter's `ExType` is `bool`. |
| [HeddleCompileError](../../../src/Heddle/Data/HeddleCompileError.cs) / [Warning](../../../src/Heddle/Data/HeddleCompileWarning.cs) | No diagnostic-ID field yet; `ToString()` renders `[{LinePosition}]{Error}\r\n{Exception}` when `LinePosition` is set, else `[{Position}]{Error}\r\n{Exception}`; a third overload `ToString(int line, int position, int length)` exists — D1's ID prefix must apply to all three renderings. `ToError` factories (message and exception overloads) live in [CompileError.cs](../../../src/Heddle/Data/CompileError.cs). D1 plumbing lands here. |
| [DocumentParser](../../../src/Heddle/Language/DocumentParser.cs) | SLL first without `BailErrorStrategy`; `ParseCanceledException` → LL retry + warning; errors also trigger an LL retry. Editor mode parses `LL_EXACT_AMBIG_DETECTION` directly. [HeddleSyntaxErrorListener](../../../src/Heddle/Language/HeddleSyntaxErrorListener.cs) overrides `SyntaxError` only — ambiguity reports are no-ops (relevant to the alt 3/alt 4 overlap, see [grammar.md](grammar.md#coexistence-analysis-alternative-selection-per-input)). |
| Grammar | [HeddleLexer.g4](../../../src/Heddle.Language/HeddleLexer.g4) `mode CALL` has eight rules (verified list in [grammar.md](grammar.md#verified-current-state)); [HeddleParser.g4](../../../src/Heddle.Language/HeddleParser.g4) `call` has the three alternatives; regen via [generate_cs.cmd](../../../src/Heddle.Language/generate_cs.cmd), ANTLR 4.13.1, `generated/` committed. |
| Namespaces | `Heddle.Native` already exists (interop/assembly helpers, e.g. `src/Heddle/Native/AssemblyHelper.cs`) — the new code therefore lives in `Heddle.Language.Expressions` and `Heddle.Runtime.Expressions`, **not** anything named "Native". |
| Tests / benchmarks | `InternalsVisibleTo("Heddle.Tests")` and `("Heddle.Performance")` are declared ([AssemblyInfo.cs](../../../src/Heddle/Properties/AssemblyInfo.cs)) — white-box assertions on `CSharpContext.Methods` are possible. Fixtures live in [src/Heddle.Tests/TestTemplate](../../../src/Heddle.Tests/TestTemplate) as `<name>.heddle` + `generated-<name>.html` pairs. |

### D3 kickoff — C# 14 first-class-span audit (result: no impact)

Per [cross-cutting D3](../common/cross-cutting-decisions.md#d3--one-time-c-14-first-class-span-audit),
the one-time audit was executed while writing this spec. Method: grep the engine sources
(`src/Heddle`, `src/Heddle.Language`, excluding `bin/`, `obj/`, `generated/`) for the
overload-resolution-sensitive patterns `.Contains(`, `.IndexOf(`, `.StartsWith(`,
`.EndsWith(`, `.LastIndexOf(`, `.SequenceEqual(`, `.Reverse(`, `.CopyTo(`, `.AsSpan(`,
`MemoryExtensions`, `string.Concat`; a *finding* is any call site where the C# 14
implicit `T[]`/`string` → span conversions
([What's new in C# 14](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-14))
could re-bind the invocation from an `Enumerable` extension to a `MemoryExtensions`
extension (spans are now valid extension receivers). Findings per hit class:

- All `.Contains`/`.StartsWith`/`.EndsWith`/`.IndexOf` hits bind to **instance** methods
  (`string` instance methods, `ICollection<T>.Contains`, `HashSet<T>.Contains` — e.g.
  `ReflectionHelper.ResolveSimpleType`'s `imports.Contains(...)` receives
  `ICollection<string>`). Instance methods always beat extension methods, so no re-bind
  is possible.
- Every `.Reverse()` receiver in `HeddleCompiler` is explicitly cast to
  `ICollection<T>` first — no array receiver exists, so `MemoryExtensions.Reverse`
  (which returns `void` and would break the `foreach` anyway) cannot bind.
- `.CopyTo` hits are array/`ICollection` instance methods (`LinearList`) or already
  explicit span calls (`ExStringBuilder`).
- `string.Concat(IEnumerable<string>)` in `HeddleMainListener` takes a LINQ sequence — no
  span conversion from `IEnumerable<T>` exists.

**Recorded result: no impact.** Additionally, `FunctionRegistry` built-ins bind by
explicit `MethodInfo` to non-span overloads (decision D13), which keeps the engine immune
by construction going forward. Later phases do not repeat this audit.

## Design decisions

Every roadmap lean and "pick at implementation" item is closed below. Records follow the
[conventions format](../common/spec-conventions.md#required-structure-of-a-phase-spec-entry-document); numbering is local
to this spec (cross-cutting decisions are always cited with links).

### D1 — Diagnostic-ID plumbing lands in this phase

**Decision.** `HeddleCompileError` gains `public string DiagnosticId { get; set; }`
(inherited by `HeddleCompileWarning`), `null` for diagnostics not yet assigned.
`ToString()` renders `[{position}]{DiagnosticId}: {Error}\r\n{Exception}` when the ID is
set and keeps today's exact format when it is `null`. A new
`public static class HeddleDiagnosticIds` (`src/Heddle/Data/HeddleDiagnosticIds.cs`)
holds one `public const string` per ID. `CompileError.ToError` gains overloads taking the
ID. Pre-existing diagnostics touched by this phase are assigned `HED0001`–`HED0003`
(see [Diagnostics](#diagnostics)); untouched pre-existing diagnostics stay ID-less until
their owning phase touches them.
**Rationale.** [Cross-cutting D1](../common/cross-cutting-decisions.md#d1--stable-diagnostic-ids-hedxxxx)
schedules the field and plumbing as a phase 1 work item; the `null`-preserving
`ToString()` keeps every existing test asserting on error text green.
**Alternatives rejected.** Assigning IDs to all ~40 pre-existing messages now (churn
across untouched code paths, contradicts D1's assign-as-touched rule); an enum instead of
`const string` (tooling and LSP `Diagnostic.code` want strings; enums can't be extended
per phase without central edits).

### D2 — C# 14 span audit executed; result recorded

**Decision.** The audit ran during spec writing with the method and per-hit-class
findings recorded [above](#d3-kickoff--c-14-first-class-span-audit-result-no-impact);
the implementation work item re-runs the greps at kickoff to confirm nothing drifted and
checks the recorded result off. No code change is expected.
**Rationale.** [Cross-cutting D3](../common/cross-cutting-decisions.md#d3--one-time-c-14-first-class-span-audit)
requires the result in this spec's assumed state; running it at spec time makes the spec
self-contained. **Alternatives rejected.** Deferring the whole audit to implementation
(spec would carry an unknown, violating the no-open-questions rule).
**Grounding.** [What's new in C# 14 — implicit span conversions](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-14).

### D3 — Grammar strategy, and the keyword-`WS*` correction

**Decision.** New tokens go to `mode CALL` only; the `call` rule gains a 4th alternative
ordered last; `expr` is a single left-recursive rule with one labeled alternative per
precedence level, `<assoc=right>` on `??` and `?:`, ternary gating the existing `DELIM`
token behind `OP_QUESTION`. Full rule text in [grammar.md](grammar.md). **Correction to
the roadmap:** placing `'true'|'false'|'null'` before `CALL_ID` is not sufficient —
`CALL_ID` is `ID_TOKEN WS*`, so with trailing whitespace it out-matches a bare keyword
under maximal munch. The keyword rules therefore carry the same trailing `WS*`
(`CALL_TRUE: 'true' WS* -> type(TRUE);`), making lengths equal so the rule-order
tie-break decides.
**Rationale.** First-alternative-wins preserves every existing parse (verified list in
[grammar.md — coexistence](grammar.md#coexistence-analysis-alternative-selection-per-input));
the left-recursion rewrite is done by the ANTLR tool itself and is target-independent,
so it works identically in the C# target.
**Alternatives rejected.** Stripping `WS*` from `CALL_ID` instead (changes existing token
spans and editor-token positions for every template); a stratified non-left-recursive
expression grammar (more rules, same result, harder to keep aligned with the precedence
table); semantic predicates to disambiguate alt 3/alt 4 (ordering already resolves it —
predicates are the documented last resort).
**Grounding.** [ANTLR left-recursion](https://github.com/antlr/antlr4/blob/master/doc/left-recursion.md),
[lexical FAQ (rule-order tie-break)](https://github.com/antlr/antlr4/blob/master/doc/faq/lexical.md),
[maximal munch](https://github.com/antlr/antlr4/blob/master/doc/wildcard.md).

### D4 — Literal set: C#-matching; regular strings only; char literals in

**Decision.** Integer (dec/hex/binary, separators, `u`/`l`/`ul` suffixes), real
(`f`/`d`/`m`, exponents), `'c'` char (full escape set), `"…"` regular strings (full
escape set, **no** `u8` suffix), `true`/`false`/`null`. Verbatim (`@"…"`), interpolated
(`$"…"`), and raw (`"""…"""`) strings are excluded from the native tier.
**Rationale.** `@` inside `CALL` mode *is* the C#-tier escape (`CSHARP_START`) and cannot
be reclaimed; `$` and `"""` stay token errors, so exclusion costs nothing that the `@` C#
tier doesn't already provide. Char literals were ratified in scope (roadmap, July 2026);
the token is one lexer rule reusing the `CHAR` fragment. String interpolation's use case
is covered by `+` and `format(...)`.
**Alternatives rejected.** Supporting `$"…"` in CALL mode (requires a hole-balancing mode
stack duplicating `INTERP_STR` for a feature the C# tier has); allowing `"…"u8`
(`ReadOnlySpan<byte>` values are useless to the object-typed pipeline and hostile to the
expression interpreter).

### D5 — AST shape: public, in `Heddle.Language.Expressions`, with two additions

**Decision.** The AST is public API (signatures in
[Public API contract](#public-api-contract)) in namespace `Heddle.Language.Expressions`
(folder `src/Heddle/Language/Expressions/`). Relative to the roadmap's node list, two
pinned refinements: (a) `PathNode` gains an optional `Target` node so postfix composition
(`Items[0].Name`, `upper(X).Length`) is representable while identifier-rooted paths stay
one node; (b) `MethodCallNode` exists so method-call syntax parses into a node the
compiler rejects with the targeted `HED1003` rather than a parse error. Every node
carries an absolute `BlockPosition` (same convention as `OutputItem.Position`).
**Rationale.** `CallParameter` is public, so the property's type must be public; phase 6
tooling consumes the AST. The `Target` refinement is forced by the grammar's postfix
alternatives — without it `Items[0].Name` (an explicit roadmap primary-tier capability:
"`.` member, `[]` indexer" at the same level) has no representation.
**Alternatives rejected.** Internal AST with a public opaque handle (breaks phase 6 for
zero gain); a separate `MemberAccessNode` per hop (allocates a node per segment and
loses the collapsed-path shape the shared resolver consumes).

### D6 — `ExpressionMode`, the `AllowCSharp` bridge, and options plumbing

**Decision.** `ExpressionMode { MemberPathsOnly = 0, Native = 1, FullCSharp = 2 }` in
`Heddle.Data`; `TemplateOptions.ExpressionMode` defaults to `Native`. `AllowCSharp`
becomes a bridge property with these exact semantics:
`get => ExpressionMode == ExpressionMode.FullCSharp`;
`set` — `true` sets `FullCSharp`; `false` sets `Native` only when the current mode is
`FullCSharp` (so it cannot silently upgrade `MemberPathsOnly`). `AllowCSharp` is kept
un-attributed in 1.x and gets `[Obsolete]` no earlier than the 2.0 window. Both
`ExpressionMode` and `Functions` are copied in the copy-ctor (child compiles — bodies,
partials, imports — inherit them via `CompileContext`'s private ctor). Neither
participates in `Equals`/`GetHashCode`.
**Rationale.** The guard at `HeddleCompiler.cs:301` reads `Options.AllowCSharp` and keeps
its exact behavior through the getter — `TemplateResolver`'s `AllowCSharp = true` hosted
views become `FullCSharp` (a superset: their templates compile as before, plus native
expressions, which is the phase's intended default-on behavior). Identity exclusion
matches the existing precedent: `AllowCSharp` never participated in `Equals`, and cache
identity is path-based (`FullPath`).
**Alternatives rejected.** `[Obsolete]` on `AllowCSharp` now (instantly warns every
existing consumer including this repo's own tests — churn without the 2.0 migration
note); three independent bools (invalid state combinations); adding the new options to
`Equals` (would change cache behavior for hosts comparing options, with no keying
scenario needing it — phase 2's completeness test will still cover the copy-ctor).

### D7 — `MemberPathsOnly` enforces at the compile step, not the lexer

**Decision.** The grammar is option-independent (a generated lexer cannot consult
`TemplateOptions`), so under `MemberPathsOnly` expression templates parse and are then
rejected in `CompileItem` with `HED1014` at the expression's position. Success
criterion 3 ("restores today's errors") is therefore met as: every expression fixture
fails compilation with a positioned error naming the option — not with byte-identical
pre-phase-1 parse-error text. This is a recorded correction of scope on the roadmap's
wording, with the criterion's intent (strict mode = these templates do not compile)
preserved.
**Rationale.** The alternative — shipping two lexers — is a maintenance and packaging
cliff for cosmetic error-text fidelity nobody depends on (the old errors were
token-recognition noise, not a contract).
**Alternatives rejected.** Post-parse token scan reproducing old messages (fabricates
fake lexer errors); making `MemberPathsOnly` also disable literal parameters but with a
different ID (one mode, one ID is simpler; literals are part of the native tier by
definition).

### D8 — Member-path knowledge single-sourced (the DRY extraction)

**Decision.** Two shared internal helpers own member-path semantics:
`MemberPathResolver.TryResolve` (new, `src/Heddle/Runtime/Expressions/MemberPathResolver.cs`)
— the reflection loop extracted verbatim from `CompileModelAccessor` (same
`BindingFlags`, same `CanRead`/`[Hidden]`/getter-visibility filter, same message, now
`HED0001`), returning `Resolved` / `DynamicHop(index)` / `Failed`; and
`ModelParameter.BuildNullSafePropertyChain` (refactor of the existing
`GetPropertyChainAccessor` body) — the null-safe expression builder over a typed input.
`CompileModelAccessor` and the native compiler both consume them; the member tier's
dynamic-hop handoff to `DynamicParameter` is preserved bit-for-bit.
**Rationale.** This is the coding standards' named example of sanctioned DRY
("member-path resolution semantics must exist exactly once… a phase 1 work item",
[coding standards](../common/coding-standards.md#dry-applied)); the member-tier
equivalence gate (identical output *and* identical error messages) proves the extraction
is semantics-preserving.
**Alternatives rejected.** Reimplementing resolution in the native compiler (the exact
divergence bug the standards call out); moving `ModelParameter`'s builder into the new
resolver class (splits the null-safety knowledge from its primary consumer; the
expression-building knowledge stays where it lives, exposed internal).

### D9 — Dynamic-scope operands are a compile error (v1)

**Decision.** A `PathNode` operand whose resolution starts on a dynamic scope, or crosses
a `[Dynamic]`-attributed property, is `HED1004` ("Native expressions require a typed
model; declare `@model(...)` / `:: <Type>` or use the `@` C# tier."). Plain member paths
(alternative 2) keep compiling to `DynamicParameter` unchanged.
**Rationale.** Roadmap-pinned v1 restriction; operator emission needs static operand
types, and `Binder`-based operator dispatch is a separate, deferred design.
**Alternatives rejected.** Falling back to runtime binder operators (large surface,
render-time exceptions, defeats the sandbox's static verifiability); silently treating
dynamic operands as `object` (wrong results for every operator).

### D10 — Method-call syntax is rejected before target resolution

**Decision.** `MethodCallNode` compilation emits `HED1003` immediately — the target path
is *not* resolved first. `@(System.IO.File.ReadAllText("x"))` therefore produces exactly
one targeted error pointing at the registry and the C# tier, and nothing about the
`System` property lookup.
**Rationale.** Sandbox fails closed with the most actionable message; resolving the
target first would leak confusing `HED0001` noise for exactly the inputs an attacker or a
confused user writes. **Alternatives rejected.** Resolving the target for a "better"
message (no better message exists; the remedy is identical regardless of target).

### D11 — Name resolution: definition → extension → registered function

**Decision.** Standalone calls keep today's resolution and add one fallback stage:
`CompileItem` checks definitions (existing), then extensions, and only when
`TemplateFactory.Exists(name)` is false consults `options.Functions`. On a registry hit
with a function-compatible parameter shape (empty, member path, or native expression),
the item compiles as: parameter = `NativeExpressionCompiler` result for
`CallNode(name, args)`, extension = `TemplateFactory.Create("")`
([EmptyExtension](../../../src/Heddle/Extensions/EmptyExtension.cs)), `ExType` = the
function's return type — so `@titlecase(Name)` renders and chains like any value call.
On a registry hit with a chain/C# parameter → `HED1017`; on a registry miss with a
function-compatible shape → `HED1001` (unified message); remaining shapes keep
`TemplateFactory.Create`'s legacy error (`HED0002`). Inside expressions,
`CallNode` names resolve against the registry **only**; a name that is instead an
extension or definition → `HED1002`. When an extension resolves for a standalone call
*and* the registry contains the same name → warning `HED1016` (extension wins).
`TemplateFactory` gains `internal static bool Exists(string name)`.
**Rationale.** Roadmap-pinned order and seam ("in the `TemplateFactory.Create` failure
path, consult the function registry"); `EmptyExtension` is the engine's existing
"render a value" carrier, so no new extension type is needed; keeping expression-context
resolution registry-only preserves the sandbox reasoning (an expression can never
instantiate an extension).
**Alternatives rejected.** Function-before-extension precedence (silently changes
existing templates when a host registers a colliding name — the warning + extension-wins
rule keeps behavior stable); a public `TemplateFactory.Exists` (no consumer yet — phase 6
can widen it); rewriting standalone hits inside `TemplateFactory` itself (the factory
has no access to options/registry and shouldn't — it is a global type registry).

### D12 — `FunctionRegistry` semantics

**Decision.** `public sealed class FunctionRegistry` in `Heddle.Runtime.Expressions`.
Names are ordinal, case-sensitive (matching extension lookup). `FunctionRegistry.Default`
is a frozen singleton containing exactly the D13 built-ins. `new FunctionRegistry()`
starts with the Default built-ins pre-registered (layering); hosts add or override from
there. `Register(string, Delegate)` and `Register(string, MethodInfo)` (static methods
only — instance behavior comes from a bound delegate): registering a signature whose
name + parameter types exactly match an existing entry **replaces** it (this is how a
host overrides a built-in); otherwise it is added as an overload. The registry
**freezes on first compile use** (internal `Freeze()`, called by the compiler);
`Register` after freeze throws `InvalidOperationException` — a host-programming error,
not a template diagnostic. Frozen registries are immutable and therefore safe for
concurrent compiles; pre-freeze mutation is not thread-safe (documented). Emission:
`MethodInfo` registrations → `Expression.Call(null, method, args)`; delegate
registrations → `Expression.Invoke(Expression.Constant(delegate), args)` (closures
supported). Overload binding: name → arity (with expanded-form matching when the last
parameter is a `params` array, ranked below normal form) → per-argument conversion rank
(exact = 0, implicit widening/reference/lifting = 1, boxing to `object` = 2); a unique
vector-dominant candidate wins; none → `HED1012` listing candidates; tie → `HED1013`.
**Rationale.** Replace-on-exact-signature makes built-in override possible without a
removal API (YAGNI); freeze-on-first-use gives lock-free render/compile reads with one
memory barrier; ranked binding follows C# betterness in miniature — enough for the
built-in set plus realistic host functions.
**Alternatives rejected.** Case-insensitive names (diverges from extension semantics and
invites shadowing surprises); throw-on-duplicate (blocks the built-in-override scenario
and re-registration in DI-composed hosts); empty non-Default registries by default
(every host would re-add the built-ins; a host wanting a bare registry can be served by
a later `FunctionRegistry.Empty` if a scenario ever appears — deferred).

### D13 — The default built-in set (frozen, invariant, exception-safe)

**Decision.** Exactly the roadmap's seventeen names, bound by explicit `MethodInfo` to
the engine's own `internal static class BuiltInFunctions`
(`src/Heddle/Runtime/Expressions/BuiltInFunctions.cs`) — never to BCL span overloads
(the interpreter-safety rule from the roadmap's .NET 10 review). One row per registered
name; the methods shown are the exact `MethodInfo`s `FunctionRegistry.Default` binds:

| Function | `BuiltInFunctions` method(s) | Pinned behavior (nulls, culture, errors) |
| --- | --- | --- |
| `upper` | `static string Upper(string value)` | `value?.ToUpperInvariant() ?? ""` — invariant culture; `null → ""`; never throws |
| `lower` | `static string Lower(string value)` | `value?.ToLowerInvariant() ?? ""` — invariant culture; `null → ""`; never throws |
| `trim` | `static string Trim(string value)` | `value?.Trim() ?? ""` — Unicode-whitespace trim, both ends; `null → ""`; never throws |
| `len` | `static int Len(string value)` | `value?.Length ?? 0` (UTF-16 code units, as `string.Length`); never throws |
| `contains` | `static bool Contains(string source, string value)` | ordinal (`string.Contains(string)` is ordinal by definition); `null` operands treated as `""`; empty `value → true`; never throws |
| `startswith` | `static bool StartsWith(string source, string value)` | ordinal — the body passes `StringComparison.Ordinal` explicitly, because the BCL's `StartsWith(string)` defaults to a culture-sensitive current-culture comparison that would make goldens machine-dependent; `null` operands treated as `""`; empty `value → true`; never throws |
| `endswith` | `static bool EndsWith(string source, string value)` | same rules as `startswith` (`EndsWith(string)` has the same culture-sensitive default) |
| `replace` | `static string Replace(string source, string oldValue, string newValue)` | ordinal search (`string.Replace(string, string)` semantics); `source` `null → ""`; `oldValue` `null`/`""` → `source` unchanged (never throws — the BCL method throws `ArgumentNullException`/`ArgumentException` there); `newValue` `null` treated as `""`, i.e. removal (matches the BCL) |
| `substr` | `static string Substr(string source, int start)`, `static string Substr(string source, int start, int length)` | `source` `null → ""`; `start` clamped to `[0, len]`, `length` clamped to `[0, len − start]`; negative `length → ""`; never throws (`Substring` would throw `ArgumentOutOfRangeException`) |
| `format` | `static string Format(object value, string format)` | `value` `null → ""`; `IFormattable` values → `ToString(format, CultureInfo.InvariantCulture)` (a `null` format selects the type's general format, per the `IFormattable` contract); non-`IFormattable` → `str(value)`; `FormatException → ""` |
| `format` (composite) | `static string Format(string format, params object[] args)` | `string.Format(CultureInfo.InvariantCulture, format, args)`; `format` `null → ""` (uniform null rule); a `null` arg renders empty for its hole (`string.Format` semantics); render-time `FormatException` (malformed or out-of-range holes reachable only via a non-literal format string) → `""`; a **literal** format string referencing `{n}` beyond the argument count → compile error `HED1015`, so it never reaches render |
| `str` | `static string Str(object value)` | `Convert.ToString(value, CultureInfo.InvariantCulture) ?? ""`; a throwing model `ToString()` propagates — model code sits on the host side of the trust boundary, same tier as property getters |
| `abs` | `static int Abs(int value)`, `static long Abs(long value)`, `static double Abs(double value)`, `static decimal Abs(decimal value)` | `Math.Abs`, except `abs(int.MinValue)`/`abs(long.MinValue)` return the value unchanged (`unchecked` two's-complement negation identity) instead of surfacing `Math.Abs`'s `OverflowException` — the never-throw rule; `double`/`decimal` inputs cannot overflow |
| `min` | `static int Min(int a, int b)`, `static long Min(long a, long b)`, `static double Min(double a, double b)`, `static decimal Min(decimal a, decimal b)` | `Math.Min`; mixed numeric args reach the right overload via the D12 widening rank; `NaN` wins for `double` (as `Math.Min`); never throws |
| `max` | `static int Max(int a, int b)`, `static long Max(long a, long b)`, `static double Max(double a, double b)`, `static decimal Max(decimal a, decimal b)` | `Math.Max`, same rules as `min` |
| `round` | `static double Round(double value)`, `static double Round(double value, int digits)`, `static decimal Round(decimal value)`, `static decimal Round(decimal value, int digits)` | `Math.Round`, banker's rounding (`MidpointRounding.ToEven`, the .NET default); `digits` clamped to `[0, 15]` (`double`) / `[0, 28]` (`decimal`) — `Math.Round` would throw `ArgumentOutOfRangeException`; never throws |
| `floor` | `static double Floor(double value)`, `static decimal Floor(decimal value)` | `Math.Floor`; never throws |
| `ceil` | `static double Ceil(double value)`, `static decimal Ceil(decimal value)` | `Math.Ceiling`; never throws |

Uniform null rule: string-returning built-ins never return `null` and treat `null`
string inputs as `""`. Built-ins never throw at render (defensive bodies, not
compiler-inserted try/catch); host-registered functions own their exception behavior —
the documented trust boundary.
**Rationale.** Composite `format` was ratified into v1 (roadmap, July 2026);
invariant culture keeps goldens machine-independent; ordinal comparisons match
`string.Contains`'s only defined semantics; defensive bodies keep the render path free of
hidden catch blocks.
**Alternatives rejected.** Culture-parameterized overloads ("recorded, not v1" — a
revisit trigger in [Deferred items](#deferred-items)); `len` over `ICollection`
(`.Count` member path already covers it — YAGNI); compiler-wrapped try/catch around all
function calls (hides host bugs and costs render time).

### D14 — Operator semantics are pinned by the normative table

**Decision.** [operator-semantics.md](operator-semantics.md) is the single normative
statement of operator behavior: C# precedence verbatim, table-driven binary numeric
promotion, lifted operands (`liftToNull: false` comparisons), same-enum bitwise via
underlying-type conversion, `bool` `&`/`^`/`|` included, `&&`/`||` non-nullable-bool
only, string `+` via `string.Concat`, user-defined operators honored through the
factories, equality falling back to static `object.Equals` for unrelated
reference/mixed static types, and the seven documented deviations from C#. The
`NativeExpressionCompiler` consults one `NumericPromotion` helper; the test table's
expected values are C#-computed.
**Rationale.** Roadmap operator table plus its grounding corrections (enum operands need
`Convert` to the underlying type; shifts require an `Int32` right operand;
`Expression.Condition` demands equal arm types) are folded into one artifact the docs
page republishes — tests, docs, and spec cannot drift.
**Alternatives rejected.** Reference-equality for unrelated reference types (C#'s rule —
roadmap explicitly chose `object.Equals` for template ergonomics); rejecting
user-defined operators (breaks `DateTime`/`TimeSpan` comparisons that "if you know C#,
it just works" promises; the factories bind them without any reflection-by-name from
template text).

### D15 — Common-type rule for `?:` and `??`

**Decision.** Arm/operand unification uses, in order: identity; `T`+`T?` → `T?`; binary
numeric promotion; `null` literal + reference/`Nullable<T>` → that type; reference
assignability → the base type; otherwise `HED1007` (ternary) — with `??` first validating
the left operand (`HED1006`) and then following `Expression.Coalesce`'s documented
result-type rules, inserting `Convert` nodes where promotion is needed.
**Rationale.** This is the roadmap's "identical, or one converts to the other" rule made
precise, aligned with the factory contracts verified in July 2026 (`Condition` throws on
arm mismatch; the `Type` overload only unifies reference-assignable arms; `Coalesce`
picks `T` for `T? ?? T`). **Alternatives rejected.** Full C# "better conversion target"
betterness (bidirectional user-conversion search — deviation 6 in the semantics doc
already excludes user-defined implicit conversions).

### D16 — Indexers: reflected, null-safe target, no bounds clamping

**Decision.** Arrays → `Expression.ArrayIndex`; everything else → reflected default
indexer under the member-tier visibility/`[Hidden]` filter; the indexed *target* is
null-safe like a member hop; index values are not clamped — out-of-range behaves as C#
(render-time exception). No match → `HED1010`.
**Rationale.** Null-safe target = member-tier consistency (a `.`-hop and a `[]`-hop on a
null object should not differ); clamping indexes would silently corrupt logic (`substr`
clamps because it is a *convenience function*; the indexer is *language surface*).
**Alternatives rejected.** Fully C#-faithful null behavior on targets
(NullReferenceException at render — contradicts the tier's founding null-safety rule);
clamping (silent wrong results).

### D17 — Constant folding to `ConstantParameter`

**Decision.** Trees containing only literals and operators are evaluated once at compile
time (build → `Compile()` → invoke) and stored as `ConstantParameter`; any tree with a
path, function call, indexer, or method node compiles to `CompiledParameter` whole.
Single literals (`@out(true)`) fold trivially.
**Rationale.** Matches the C# tier's existing constant handling
(`ParseAndGetResultType` → `ConstantParameter`); deterministic and sandbox-neutral (no
user code in a literal-only tree). Partial subtree folding is deferred (no scenario;
adds tree-rewriting complexity).
**Alternatives rejected.** Interpreting the tree manually at compile time (a second
evaluator to keep in sync with the emitted one — the compile-and-invoke path *is* the
emitted semantics).

### D18 — Delegate shape and type threading reuse the existing pipeline

**Decision.** The native compiler produces
`Expression.Lambda<Func<object, object, object, object>>` over parameters
`(object model, object chained, object root)` — the exact `CompiledParameter` shape —
with the result `Convert`-ed to `object`. Paths root at
`Convert(model, scopeType.Type)` (or `root`/`RootScopeType` under `::`); `chained` is
accepted but unused in v1 (no expression syntax addresses it). The expression's static
type becomes the item's `ExType`, threading into `CreateExtension`/`InitStart` exactly
like the other tiers — `@if(Count > 0)` hands `typeof(bool)` to `IfExtension` with zero
extension changes. `TemplateItem`, `TemplateChain`, and `Scope` are untouched.
**Rationale.** Roadmap-pinned; reusing the delegate shape means the render path cannot
regress (same `IRuntimeParameter.GetParameter` dispatch, same boxing profile — the
roadmap's .NET 10 review already established the result box escapes by design and is not
a phase concern, [cross-cutting D7](../common/cross-cutting-decisions.md#d7--net-10-stance-no-conditional-compilation-by-default)).
**Alternatives rejected.** A typed `Func<TModel, …>` per template (defeats the
object-typed `Scope` pipeline; phase 7's typed emission is the place for that).

### D19 — Docs deliverable: a dedicated `native-expressions.md` page

**Decision.** The published reference is a new top-level docs page
`docs/native-expressions.md` (not a section of
[language-reference.md](../../language-reference.md)), containing: the operator
precedence table and the semantics rules republished from
[operator-semantics.md](operator-semantics.md) (same rows — the tests pin both), the
literal set, the "`.` hops are null-safe, which is why there is no `?.`" equivalence,
the registered-function model with the built-ins table, `ExpressionMode`, and the
sandbox trust boundary. Cross-linked from `language-reference.md`,
[built-in-extensions.md](../../built-in-extensions.md) (`@if`/`@for` parameter docs) and
[csharp-api.md](../../csharp-api.md) (when to escalate to `@` expressions).
**Rationale.** Closes the roadmap's "page or section" choice, which it had deferred to
this spec: the material above is demonstrably page-sized (this spec's own semantics supplement is
~300 lines), and a stable page anchor is what the other pages and phase 6 hovers link to.
**Alternatives rejected.** A `language-reference.md` section (would double that page's
length and bury the operator table).

### D20 — Editor tokens: three additive values

**Decision.** `HeddleTokenType` gains `Operator`, `Literal`, `FunctionName`, appended
after `ParseError` so existing numeric values are stable; emission per the
classification table in [grammar.md](grammar.md#editor-token-classification), produced by
the AST builder through the existing `ParseContext.AddToken` gate.
**Rationale.** Roadmap-pinned additive values; appending is the binary-compatible choice
for a public enum. **Alternatives rejected.** Reusing `Id` for function names (kills
phase 6 semantic highlighting for the registry).

## Implementation plan

Work items in execution order; each runs the canonical loop (spec-first per the
[TDD verdict](#testing-plan)). "Check" is the completion gate for the item.

**WI1 — Diagnostic-ID plumbing (D1 kickoff).**
Files: `src/Heddle/Data/HeddleCompileError.cs` (add `DiagnosticId`, extend `ToString()`),
`src/Heddle/Data/HeddleDiagnosticIds.cs` (new — constants for every ID in
[Diagnostics](#diagnostics)), `src/Heddle/Data/CompileError.cs` (add
`ToError(this string, BlockPosition, string diagnosticId)` and the exception overload),
`src/Heddle/Language/HeddleSyntaxErrorListener.cs` (tag syntax errors `HED0003`).
Check: new `DiagnosticIdTests` green (ID-set and ID-null `ToString` formats); full
existing suite green unchanged.

**WI2 — Span-audit confirmation (D3 kickoff).**
Re-run the greps from the [audit record](#d3-kickoff--c-14-first-class-span-audit-result-no-impact)
against the implementation-time tree; confirm "no impact" still holds (any new hit is
inspected under the same finding rule and the record above is amended in the same PR).
Check: recorded result confirmed; no code change.

**WI3 — Options model.**
Files: `src/Heddle/Data/ExpressionMode.cs` (new enum),
`src/Heddle/Data/TemplateOptions.cs` (add `ExpressionMode` + `Functions` properties,
bridge `AllowCSharp` per D6, initialize in both ctors, copy both in the copy-ctor).
Check: `ExpressionModeTests` bridge-semantics rows green (get/set matrix incl. the
`MemberPathsOnly`-preserving setter); existing suite green (every
`AllowCSharp = true` site now yields `FullCSharp`).

**WI4 — Failing executable spec.**
Files: `src/Heddle.Tests/NativeExpressionParseTests.cs` (parse corpus from
[grammar.md](grammar.md#parse-corpus-executable-grammar-spec)),
`src/Heddle.Tests/NativeExpressionOperatorTests.cs` (the C#-computed
[operator table](operator-semantics.md)),
`src/Heddle.Tests/NativeExpressionSandboxTests.cs` (the
[negative matrix](#sandbox-invariant-and-negative-matrix) — written **before** the
features it constrains), `src/Heddle.Tests/FunctionRegistryTests.cs`,
`src/Heddle.Tests/ExpressionModeTests.cs`, fixtures `expr-flagship.heddle` /
`generated-expr-flagship.html` and `expr-functions.heddle` /
`generated-expr-functions.html` under `src/Heddle.Tests/TestTemplate/`.
Check: all new tests fail for the right reason (parse errors today); suite structure
compiles.

**WI5 — Grammar change + regen.**
Files: `src/Heddle.Language/HeddleLexer.g4`, `src/Heddle.Language/HeddleParser.g4`
(exact rule text in [grammar.md](grammar.md)), one dedicated regen commit of
`src/Heddle.Language/generated/` (and the `js/` regen),
`src/Heddle/Language/HeddleToken.cs` (three appended enum values).
Check: parse corpus green; existing suite green byte-identical;
`TemplateParseBenchmarks` before/after within gate (see WI11).

**WI6 — AST + builder + wiring.**
Files: `src/Heddle/Language/Expressions/ExprNode.cs` (hierarchy),
`src/Heddle/Language/Expressions/ExprOperator.cs`,
`src/Heddle/Language/Expressions/ExpressionAstBuilder.cs` (parse-tree → AST, literal
decoding, editor-token emission), `src/Heddle/Language/CallParameter.cs`
(`NativeExpression` property; `IsModelTypeParameter` gains `&& NativeExpression == null`),
`src/Heddle/Language/ParseContext.cs` (`CreateItem` populates `NativeExpression` from
`context.native_expression()` in both the named and unnamed branches).
Check: parse-corpus AST-shape assertions green; literal-typing rows green.

**WI7 — Member-path extraction (D8).**
Files: `src/Heddle/Runtime/Expressions/MemberPathResolver.cs` (new),
`src/Heddle/Runtime/HeddleCompiler.cs` (`CompileModelAccessor` rewired to the resolver),
`src/Heddle/Runtime/Parameters/ModelParameter.cs` (`BuildNullSafePropertyChain`
extracted; `GetPropertyChainAccessor` becomes a wrapper).
Check: **member-tier equivalence run** — every existing member-path fixture produces
identical output and identical error messages (now additionally carrying `HED0001`).

**WI8 — `NativeExpressionCompiler` (the L-sized item).**
Files: `src/Heddle/Runtime/Expressions/NativeExpressionCompiler.cs`,
`src/Heddle/Runtime/Expressions/NumericPromotion.cs`,
`src/Heddle/Runtime/HeddleCompiler.cs` (new `else if
(extensionItem.CallParameter.NativeExpression != null)` branch between the C# branch and
the chain fallback: `MemberPathsOnly` check → compile → parameter + `ExType` threading
per D18). Internal entry point:

```csharp
internal static class NativeExpressionCompiler
{
    /// <summary>Compiles a native expression to a runtime parameter. Returns null and
    /// records positioned compile errors on failure; never throws for template input.</summary>
    internal static IRuntimeParameter Compile(ExprNode expression, CompileScope compileScope,
        ParseContext parseContext, out ExType resultType);
}
```

Check: operator table fully green; sandbox negatives for
method-calls/dynamic/logical/coalesce/ternary/indexer green; folding rows produce
`ConstantParameter` (asserted via internals).

**WI9 — `FunctionRegistry` + built-ins + standalone unification (D11–D13).**
Files: `src/Heddle/Runtime/Expressions/FunctionRegistry.cs`,
`src/Heddle/Runtime/Expressions/BuiltInFunctions.cs`,
`src/Heddle/Runtime/TemplateFactory.cs` (`internal static bool Exists(string)`),
`src/Heddle/Runtime/HeddleCompiler.cs` (failure-path fallback + `HED1016` shadowing
warning + `HED1017`).
Check: `FunctionRegistryTests` (overload ranking, replace-on-exact-signature,
freeze-then-throw, composite `format` incl. `HED1015`, delegate-with-closure) green;
validation scenarios `@(max(A, B))`, `@upper(Name)` shadowing row green.

**WI10 — Mode enforcement.**
Files: `src/Heddle/Runtime/HeddleCompiler.cs` (`HED1014` in the native branch).
Check: `ExpressionModeTests` — every expression fixture under `MemberPathsOnly` fails
with positioned `HED1014`; the same fixtures compile under `Native`; C#-tier fixtures
still require `FullCSharp`.

**WI11 — Goldens, concurrency, benchmarks, combined gate.**
Files: flagship fixtures rendered against goldens; a parallel-compile/render test
(shared frozen registry across concurrent template compiles);
`src/Heddle.Performance/TemplateParseBenchmarks.cs` (new — `[MemoryDiagnoser]`, parses
the existing `TestSuite` template corpus via `DocumentParser.Parse`).
Check: the full [regression gate](../common/testing-standards.md#regression-gates) in one
combined run — build all TFMs, full suite, goldens byte-identical, parse benchmark
allocated-bytes not increased and mean within reported error, render benchmarks
unchanged (render path untouched), regen committed as its own reviewed commit. Success
criteria 1–6 of the roadmap asserted together, including the zero-Roslyn proof:
`compileScope.CSharpContext.Methods.Count == 0 && CompiledAssembly == null` after
compiling every flagship fixture (white-box via `InternalsVisibleTo`).

**WI12 — Published docs (D19).**
Files: `docs/native-expressions.md` (new page), cross-links added in
`docs/language-reference.md`, `docs/built-in-extensions.md`, `docs/csharp-api.md`;
VitePress sidebar entry in `docs/.vitepress/config.mts`.
Check: `cd docs && npm run docs:build` passes (uppercase-drive cwd on Windows); operator
table on the page textually matches [operator-semantics.md](operator-semantics.md).

## Public API contract

All additions are additive ([API design rules](../common/coding-standards.md#api-design-and-compatibility));
XML docs are required on every member below. Thread-safety notes inline.

```csharp
namespace Heddle.Data
{
    /// <summary>Selects which expression tier is available inside call parentheses.</summary>
    public enum ExpressionMode
    {
        /// <summary>Strict pre-phase-1 surface: member paths, nested chains, and empty parameters only.</summary>
        MemberPathsOnly = 0,
        /// <summary>Default. Adds the sandbox-safe native expression tier (operators, literals, registered functions).</summary>
        Native = 1,
        /// <summary>Implies Native; additionally enables the inner-@ Roslyn C# tier.</summary>
        FullCSharp = 2
    }

    public class TemplateOptions
    {
        /// <summary>Expression tier for this template and its child compiles. Default: Native.</summary>
        public ExpressionMode ExpressionMode { get; set; }

        /// <summary>Registered functions callable from native expressions.
        /// Null means FunctionRegistry.Default. The registry freezes on first compile use.</summary>
        public Heddle.Runtime.Expressions.FunctionRegistry Functions { get; set; }

        /// <summary>Bridge over ExpressionMode: true == FullCSharp. Setting false leaves
        /// MemberPathsOnly untouched and otherwise selects Native. Scheduled for
        /// [Obsolete] in the 2.0 window.</summary>
        public bool AllowCSharp { get; set; }   // body per D6; no longer an auto-property
    }

    public class HeddleCompileError
    {
        /// <summary>Stable diagnostic ID ("HED1003"); null for diagnostics not yet assigned one.
        /// Surfaced by ToString() and, in phase 6, as the LSP Diagnostic.code.</summary>
        public string DiagnosticId { get; set; }
    }

    /// <summary>Stable diagnostic-ID constants (see the cross-cutting registry).</summary>
    public static class HeddleDiagnosticIds
    {
        public const string PropertyNotFound = "HED0001";
        public const string ExtensionNotFound = "HED0002";
        public const string SyntaxError = "HED0003";
        public const string UnknownFunction = "HED1001";
        public const string ExtensionCalledAsFunction = "HED1002";
        public const string MethodCallNotAvailable = "HED1003";
        public const string TypedModelRequired = "HED1004";
        public const string LogicalOperatorRequiresBool = "HED1005";
        public const string CoalesceLeftNotNullable = "HED1006";
        public const string TernaryArmsNoCommonType = "HED1007";
        public const string BinaryOperatorNotDefined = "HED1008";
        public const string UnaryOperatorNotDefined = "HED1009";
        public const string IndexerNotFound = "HED1010";
        public const string TernaryConditionNotBool = "HED1011";
        public const string NoFunctionOverload = "HED1012";
        public const string AmbiguousFunctionCall = "HED1013";
        public const string NativeExpressionsDisabled = "HED1014";
        public const string FormatArgumentCountMismatch = "HED1015";
        public const string FunctionShadowedByExtension = "HED1016";
        public const string FunctionRequiresExpressionArguments = "HED1017";
        // DiagnosticIdTests asserts this list matches the Diagnostics table 1:1.
    }
}

namespace Heddle.Runtime.Expressions
{
    /// <summary>Named functions callable from native expressions. Instances are mutable
    /// until frozen (first compile use); frozen instances are immutable and safe for
    /// concurrent compiles and renders. Registration is the host's trust boundary:
    /// anything registered is callable from template text.</summary>
    public sealed class FunctionRegistry
    {
        /// <summary>The frozen default whitelist (upper, lower, trim, len, contains,
        /// startswith, endswith, replace, substr, format, str, abs, min, max, round,
        /// floor, ceil — invariant culture, exception-safe).</summary>
        public static FunctionRegistry Default { get; }

        /// <summary>Creates a registry pre-populated with the Default built-ins.</summary>
        public FunctionRegistry();

        /// <summary>Registers a delegate (closures allowed). Same name + identical
        /// parameter types replaces; otherwise adds an overload.
        /// Throws InvalidOperationException when frozen; ArgumentNullException on nulls.</summary>
        public void Register(string name, Delegate function);

        /// <summary>Registers a static method. Throws ArgumentException for instance
        /// methods, open generics, void returns, or ref/out/pointer parameters.</summary>
        public void Register(string name, System.Reflection.MethodInfo staticMethod);

        /// <summary>True when a function with this exact name is registered.</summary>
        public bool Contains(string name);

        /// <summary>True once the registry has been used by a compile.</summary>
        public bool IsFrozen { get; }
    }
}

namespace Heddle.Language
{
    public class CallParameter
    {
        /// <summary>The parsed native expression when the parameter used the expression
        /// tier; null for the member-path, chain, and C# shapes.</summary>
        public Heddle.Language.Expressions.ExprNode NativeExpression { get; set; }

        // IsModelTypeParameter now additionally requires NativeExpression == null.
    }

    public enum HeddleTokenType
    {
        // Existing members — verified against HeddleToken.cs (July 2026); values unchanged:
        Id = 0, RootReference, MemberSelector, Out, SubStart, SubClose,
        CSharpToken, CSharpStart, DefStartName, DefEndName, DefType, Delim,
        DefStart, DefClose, Comment, OutParamStart, OutParamEnd,
        LineTerminate, DefOutputOnEnd, ParseError,
        // Appended by this phase (D20):
        Operator,
        Literal,
        FunctionName
    }
}

namespace Heddle.Language.Expressions
{
    /// <summary>Base of the native-expression AST. Nodes are created by the parser-side
    /// builder; instances are immutable after construction and safe to share.</summary>
    public abstract class ExprNode
    {
        /// <summary>Absolute template position (same convention as OutputItem.Position).</summary>
        public BlockPosition Position { get; }
    }

    public enum ExprOperator
    {
        Add, Subtract, Multiply, Divide, Modulo, LeftShift, RightShift,
        LessThan, LessThanOrEqual, GreaterThan, GreaterThanOrEqual, Equal, NotEqual,
        And, ExclusiveOr, Or, AndAlso, OrElse, Coalesce,
        Not, Negate, UnaryPlus, OnesComplement
    }

    public sealed class BinaryNode : ExprNode
    { public ExprOperator Operator { get; } public ExprNode Left { get; } public ExprNode Right { get; } }

    public sealed class UnaryNode : ExprNode
    { public ExprOperator Operator { get; } public ExprNode Operand { get; } }

    public sealed class TernaryNode : ExprNode
    { public ExprNode Condition { get; } public ExprNode WhenTrue { get; } public ExprNode WhenFalse { get; } }

    /// <summary>A literal. Value is the decoded CLR value (int/long/uint/ulong/float/
    /// double/decimal/string/char/bool) or null for the null literal.</summary>
    public sealed class LiteralNode : ExprNode
    { public object Value { get; } }

    /// <summary>A property path. Target == null roots the path at the scope (RootRef
    /// selects the root scope); Target != null hops off that expression's static type.</summary>
    public sealed class PathNode : ExprNode
    { public bool RootRef { get; } public IReadOnlyList<string> Segments { get; } public ExprNode Target { get; } }

    public sealed class IndexNode : ExprNode
    { public ExprNode Target { get; } public IReadOnlyList<ExprNode> Arguments { get; } }

    /// <summary>A registered-function call: name(args).</summary>
    public sealed class CallNode : ExprNode
    { public string Name { get; } public IReadOnlyList<ExprNode> Arguments { get; } }

    /// <summary>Method-call syntax target.Name(args) — parsed for diagnostics, always
    /// rejected at compile time with HED1003.</summary>
    public sealed class MethodCallNode : ExprNode
    { public ExprNode Target { get; } public string Name { get; } public IReadOnlyList<ExprNode> Arguments { get; } }
}
```

Internal additions (not public API; listed for the implementer): `MemberPathResolver`,
`NativeExpressionCompiler`, `NumericPromotion`, `BuiltInFunctions`,
`ModelParameter.BuildNullSafePropertyChain`, `TemplateFactory.Exists`,
`FunctionRegistry.Freeze()`.

Thread-safety summary: the AST is immutable; `FunctionRegistry` is immutable once frozen
(and `Default` is born frozen); no extension gains per-render state; compiled parameters
are the existing stateless shapes. Nothing in this phase adds per-render mutable state.

## Diagnostics

IDs claimed from the `HED1xxx` block (and `HED0xxx` for the three pre-existing
diagnostics this phase touches), per
[cross-cutting D1](../common/cross-cutting-decisions.md#d1--stable-diagnostic-ids-hedxxxx).
All are errors unless marked warning. Position = the offending expression node / token
(absolute), except where noted. `<…>` are message parameters.

| ID | Message text | Trigger |
| --- | --- | --- |
| HED0001 | `Property <name> not found in Type [<type>]` | Member-path segment fails the shared resolver's filter (missing, non-readable, `[Hidden]`, or inaccessible getter) — both tiers, message unchanged from today. Position: the call item's position in the member tier (exactly as `CompileModelAccessor` reports today — preserved by the equivalence gate); the failing `PathNode`'s position inside expressions |
| HED0002 | `Cannot find extension <<name>>` | `TemplateFactory.Create` failure for parameter shapes that are not function-compatible (chain/C# arguments) — message and position (the call's `absoluteTextPosition`) unchanged |
| HED0003 | *(ANTLR-provided message)* | Parser syntax errors via `HeddleSyntaxErrorListener` — includes every construct the grammar rejects: assignment and compound assignment, `++`/`--`, lambdas, casts, `is`/`as`, `new`, `?.`, interpolated/raw/`u8`-suffixed strings, and malformed expressions (dangling operators/commas) — parse-corpus rows N01–N14. Position: the offending token as ANTLR reports it; the listener is parser-attached, so a CALL-mode token-recognition error surfaces through the parser error that follows it |
| HED1001 | `Cannot find extension or registered function '<name>'. Register it with TemplateOptions.Functions, or check the name.` | `CallNode` name misses the registry and is no extension/definition; also the standalone failure path when the parameter shape is function-compatible |
| HED1002 | `'<name>' is an extension, not a registered function — extensions cannot be called inside a native expression. Use a call chain, or register a function with TemplateOptions.Functions.` | `CallNode` name misses the registry but matches an extension or definition |
| HED1003 | `Method calls are not available in native expressions — register a function with TemplateOptions.Functions or use the @ C# tier.` | Any `MethodCallNode` (rejected before target resolution, D10) |
| HED1004 | `Native expressions require a typed model; declare @model(...) / ':: <Type>' or use the @ C# tier.` | Path operand on a dynamic scope or crossing a `[Dynamic]` property |
| HED1005 | `Operator '<op>' requires bool operands, but the operand type is <type>.` | `&&`/`\|\|` with any non-`bool` (incl. `bool?`) operand |
| HED1006 | `Operator '??' requires a reference-type or Nullable<T> left operand, but the operand type is <type>.` | `??` with a non-nullable value-type left operand |
| HED1007 | `The conditional operator arms have no common type (<t1> vs <t2>).` | `?:` arm unification fails (D15) |
| HED1008 | `Operator '<op>' is not defined for operand types <t1> and <t2>.` (literal-overflow form: `Literal is out of range of every numeric literal type.`) | Binary operator with no rule in the semantics table; numeric literal overflow |
| HED1009 | `Operator '<op>' is not defined for operand type <type>.` | Unary operator on an unsupported type (e.g. `-` on `ulong`, `!` on non-bool) |
| HED1010 | `Type <type> has no accessible indexer that takes (<argTypes>).` | `[]` on a non-array without a matching visible indexer |
| HED1011 | `The conditional operator requires a bool condition, but the condition type is <type>.` | `?:` with a non-`bool` condition |
| HED1012 | `No overload of function '<name>' takes (<argTypes>). Candidates: <candidates>.` | Registry hit, no overload binds |
| HED1013 | `The call to function '<name>' is ambiguous between: <candidates>.` | Two candidates tie under the conversion ranking |
| HED1014 | `Native expressions are disabled here — TemplateOptions.ExpressionMode is MemberPathsOnly. Set ExpressionMode.Native (default) or FullCSharp to enable them.` | Any `NativeExpression` parameter compiled under `MemberPathsOnly` |
| HED1015 | `The format string references argument {<n>} but only <count> argument(s) were supplied.` | Composite `format` with a literal format string whose highest placeholder index ≥ argument count. Position: the format-string literal node |
| HED1016 (warning) | `Registered function '<name>' is shadowed by the extension with the same name; standalone calls '@<name>(...)' resolve to the extension.` Fix: `Rename the function, or invoke it inside an expression: '@( <name>(...) )'.` | Standalone call resolves to an extension while the options registry contains the same name. Position: the standalone call item |
| HED1017 | `Registered function '<name>' takes native-expression arguments only — chained extension calls and C# expressions are not supported here. Use the @ C# tier.` | Standalone registry hit with a chain/C# parameter shape. Position: the standalone call item |

The pre-existing message `C# Code Not allowed here, see TemplateOptions.AllowCSharp
Property` (guard at `HeddleCompiler.cs:301`) is **not** modified and receives no ID in
this phase — the guard's behavior is preserved verbatim through the `AllowCSharp` bridge
getter; phase 2 (the next phase to touch output/options diagnostics) may claim it.

## Sandbox invariant and negative matrix

**Invariant.** The native compiler can only emit invocations of: (a) property/indexer
getters passing the member-tier filter (shared resolver — `[Hidden]`, accessibility),
(b) `MethodInfo`s/delegates the host explicitly registered (built-ins included),
(c) compiler-chosen intrinsics (`string.Concat`, static `object.Equals`, conversions),
and (d) user-defined operator methods declared by the operand's own static types, bound
by the `Expression` factories. There is **no reachable code path from template text to
`Type.GetMethod`-by-name** — `MethodCallNode` is rejected before resolution, `CallNode`
resolves against the registry only, and paths resolve properties only. Function
registration is the documented trust boundary; a sandbox violation is always a compile
error, never a degraded execution
([error-handling standards](../common/coding-standards.md#error-handling-and-diagnostics)).

Negative matrix (all rows are permanent security regression tests in
`NativeExpressionSandboxTests`; each asserts the exact diagnostic ID **and** position,
and — where the model carries booby-trapped throwing getters/functions — that nothing
executed):

| # | Template | Options/model | Expected |
| --- | --- | --- | --- |
| S01 | `@(Name.ToUpper())` | default | HED1003 |
| S02 | `@(System.IO.File.ReadAllText("x"))` | default | HED1003 (single error — target unresolved, D10) |
| S03 | `@("x".Substring(0))` | default | HED1003 |
| S04 | `@(GetType().Assembly)` | default | HED1001 (`GetType` unregistered) + no reflection occurs |
| S05 | `@(evil(1))` | `evil` unregistered | HED1001 |
| S06 | `@(if(Flag) + 1)` | `if` is an extension; the operator forces the expression alternative (bare `@(if(Name))` stays a valid nested chain) | HED1002 |
| S07 | `@(typeof(string))` | default | HED1001 (`typeof` is just an unregistered name) |
| S08 | `@(X = 1)` | default | HED0003 (parse) |
| S09 | `@(Items.Where(i => i))` | default | HED0003 (parse — lambda) |
| S10 | `@(new Foo())` | default | HED0003 (parse) |
| S11 | `@(Count && true)` | `Count : int` | HED1005 |
| S12 | `@(Value ?? 0)` | `Value : int` (non-nullable) | HED1006 |
| S13 | `@(Flag ? 1 : "x")` | `Flag : bool` | HED1007 |
| S14 | `@(Name ? 1 : 2)` | `Name : string` | HED1011 |
| S15 | `@("a" - 1)` | default | HED1008 |
| S16 | `@(-U)` | `U : ulong` | HED1009 |
| S17 | `@(Point["x"])` | `Point` has no string indexer | HED1010 |
| S18 | `@(min(Name, 2))` | `Name : string` | HED1012 with candidate list |
| S19 | `@(X > 1)` | `:: dynamic` model | HED1004 |
| S20 | `@(Secret)` | `Secret` is `[Hidden]` | HED0001 (same as member tier) |
| S21 | `@(len(Secret))` | `Secret` is `[Hidden]` | HED0001 — function args pass the same filter |
| S22 | `@(A + B)` | `ExpressionMode.MemberPathsOnly` | HED1014 naming the option |
| S23 | `@( @Model.X )` | `ExpressionMode.Native` (not FullCSharp) | existing "C# Code Not allowed here…" error unchanged |
| S24 | `@(format("{0}{1}", X))` | default | HED1015 |
| S25 | `@evil(a():b())` | `evil` registered | HED1017 |
| S26 | `@("""x""" + Name)` | default | HED0003 (parse — raw string) |

## Testing plan

Instantiates the [testing standards](../common/testing-standards.md); suite homes and
conventions apply as written there.

**TDD verdict (carried from the roadmap, unweakened).** Yes — this phase is the
roadmap's best TDD fit: the operator-semantics table is written first with C#-computed
expected values (the same table the docs page publishes); the sandbox-negative suite is
written before the compiler features it constrains so the sandbox fails closed from the
first commit; grammar work is TDD-adapted — the parse corpus is the executable spec each
ANTLR regen is validated against.

**Named test assets** (all in [src/Heddle.Tests](../../../src/Heddle.Tests) unless noted):

| Asset | Content |
| --- | --- |
| `NativeExpressionParseTests` | The [parse corpus](grammar.md#parse-corpus-executable-grammar-spec) (P01–P24, N01–N14) as `[Theory]` data; AST-shape assertions via the public `ExprNode` API; the alt 3/alt 4 coexistence rows; editor-token classification rows under `ProvideLanguageFeatures`. |
| `NativeExpressionOperatorTests` | The [normative table](operator-semantics.md) as `[Theory]` rows `(expression, model, expected)`; expected values produced by compiled C# expressions over the same inputs — a disagreement means re-deriving from the C# reference, which is a spec event, not a fix. Covers promotion pairs, lifted nulls, enum bitwise, string concat, `??`/`?:` typing, folding (asserts `ConstantParameter` via internals). |
| `NativeExpressionSandboxTests` | The [negative matrix](#sandbox-invariant-and-negative-matrix) S01–S26; every row asserts diagnostic ID + position and never-executed booby traps. |
| `FunctionRegistryTests` | Overload ranking (exact/widening/object), params-expanded composite `format`, replace-on-exact-signature, freeze-then-`InvalidOperationException`, delegate closures, `Default` immutability. |
| `ExpressionModeTests` | Bridge get/set matrix (D6); `MemberPathsOnly` restores failure for every expression fixture (`HED1014`); copy-ctor propagation of `ExpressionMode`/`Functions` to child compiles (imports/partials). |
| `DiagnosticIdTests` | `ToString()` formats with and without ID; `HeddleDiagnosticIds` constants match the table above. |
| Member-tier equivalence run | Re-execution of every existing member-path fixture after WI7 asserting byte-identical output and identical error text. |
| Concurrency test | Parallel compiles + renders sharing one frozen registry and one options object; opposite-condition templates prove isolation (per the standards' concurrency rule). |
| Fixtures/goldens | `expr-flagship.heddle` → `generated-expr-flagship.html` (the four flagship snippets + root-ref arithmetic + null-safe hop scenario); `expr-functions.heddle` → `generated-expr-functions.html` (built-ins incl. composite `format`; culture-sensitive outputs pinned by invariant-culture built-ins only). |
| `TemplateParseBenchmarks` ([src/Heddle.Performance](../../../src/Heddle.Performance)) | `[MemoryDiagnoser]` benchmark parsing the existing `TestSuite` corpus via `DocumentParser.Parse` — the ALL(\*) prediction-cost guard. |

**Zero-Roslyn proof (success criterion 1).** After compiling both flagship fixtures with
`ExpressionMode.Native`, assert `compileScope.CSharpContext.Methods.Count == 0` and
`CompiledAssembly == null` (white-box; `InternalsVisibleTo` verified present).

**Regression gate** (one combined run, per the standards): build all TFMs
(`netstandard2.0;net6.0;net8.0;net10.0`, tests add `net48` on Windows); full suite; all
pre-existing goldens byte-identical; the one dedicated regen commit diff-reviewed;
`TemplateParseBenchmarks` allocated bytes not increased and mean within BenchmarkDotNet's
reported error; render benchmarks re-run once to confirm the untouched render path
(expected: no change). Red gate → fix code; the operator table changes only by
re-derivation from compiled C# (spec event).

**Deferred phase 9 gallery item** (recorded per the standards): the
`samples/sandboxed-user-templates` demo — user-supplied template + registry trust
boundary end-to-end, golden-asserted in CI — plus the demo page's in-browser rendering as
the standing Roslyn-free integration proof. Owned by this phase, delivered when the
phase 9 harness exists.

## Back-compat and migration

Everything is additive; nothing lands in the
[2.0 window](../common/cross-cutting-decisions.md#d2--the-single-20-breaking-window)
from this phase.

| Surface | Behavior | Proof |
| --- | --- | --- |
| Member paths, nested chains, C# parameters | Byte-identical — alternatives 1–3 untouched and ordered first | Full suite + goldens; coexistence corpus rows P01–P03 |
| Templates whose parens previously failed to lex | Now compile as expressions (error → works) | Acceptable by definition; release note |
| `@out(true)`-style keyword parameters | Typed scopes: compile error → literal. **Verified refinement of the roadmap claim:** on *dynamic* scopes this was not a compile error but a render-time `RuntimeBinderException` (member `true` looked up via `DynamicParameter`); it likewise becomes the literal. Both directions are error → works | Corpus row P20; release note (keywords are not legal C# identifiers, so no property can collide) |
| Hosts equating `AllowCSharp = false` with "no logic" | Comparisons now compile by default | `ExpressionMode.MemberPathsOnly` escape hatch + release note |
| `AllowCSharp` consumers (incl. `TemplateResolver` hosted views) | `true` maps to `FullCSharp` — previous C#-tier behavior preserved, native tier additionally available (the phase's intended default) | `ExpressionModeTests` bridge rows; existing suite |
| Error objects | `ToString()` unchanged when `DiagnosticId == null`; touched messages keep their exact text and gain IDs | `DiagnosticIdTests`; member-tier equivalence run |
| ANTLR regen churn | One dedicated regen commit | Gate rule 3 |

Migration notes shipped with the phase: the release note rows above plus the
`native-expressions.md` page's `ExpressionMode` section. Nothing scheduled into 2.0
except the pre-existing plan to `[Obsolete]` `AllowCSharp` there (D6).

## Performance considerations

- **Render path: untouched by construction.** Native expressions produce the existing
  `CompiledParameter`/`ConstantParameter` shapes; `Scope`, `TemplateItem`, dispatch, and
  buffer behavior are unchanged. No new per-render allocations
  ([performance rules](../common/coding-standards.md#performance-rules-the-render-path-is-hot));
  the boxed result of value-typed expressions is the same boxing the member tier already
  produces, and per [cross-cutting D7](../common/cross-cutting-decisions.md#d7--net-10-stance-no-conditional-compilation-by-default)
  no design leans on JIT escape analysis to remove it. Render benchmarks run once in the
  gate to confirm.
- **Parse path: the real risk.** New CALL-mode tokens and the 4th alternative change
  ALL(\*) prediction for every template. Guard: `TemplateParseBenchmarks` before/after on
  the same machine; acceptance per gate rule 4 (allocations not up, mean within reported
  error). First remediation is alternative ordering; targeted predicates only as a last
  resort (roadmap risk note).
- **Compile path: moderate cost accepted.** Reflection, promotion lookups, and
  `Expression.Compile` are compile-once costs; constant folding removes delegate
  invocations for literal-only parameters. The compile-cost runners in
  [src/Heddle.Performance/Runners](../../../src/Heddle.Performance/Runners) exist if a
  question arises, but no budget is set — Roslyn leaving the common path is the dominant
  startup win this phase delivers.
- No `[MethodImpl(AggressiveInlining)]` additions — nothing here is a tiny hot
  transform with a benchmark justifying it.

## Standards compliance

The [balanced-mode](../common/coding-standards.md#solid-dry-and-yagni-in-balanced-mode)
calls actually made in this phase:

- **DRY, applied where knowledge must not diverge:** the member-path resolver extraction
  (D8) is the standards' own named phase 1 example — one representation of hop
  semantics, proven by the equivalence gate. Conversely, the operator table lives once
  in [operator-semantics.md](operator-semantics.md) and is *republished* (not
  re-derived) by docs and tests.
- **YAGNI, cutting scope:** no `?.` (redundant), no culture knobs on built-ins, no
  registry removal/enumeration API, no partial folding, no dynamic-operand support, no
  `FunctionRegistry.Empty` — each sits in [Deferred items](#deferred-items) with a
  trigger. No speculative options: `ExpressionMode` exists because three behaviors exist
  today (strict/native/C#), not "for flexibility".
- **Open/closed at the sanctioned seam:** `FunctionRegistry` is the roadmap-ratified
  extension point; `NativeExpressionCompiler` is a concrete internal static class — no
  plugin interface for a single-implementation internal (precedence rule 5).
- **SRP along pipeline stages:** parsing owns the AST (`Heddle.Language.Expressions`),
  compiling owns emission (`Heddle.Runtime.Expressions`) — mirroring the existing
  lex→parse→walk→compile split rather than inventing a new layering.
- **Security beats ergonomics:** D10 rejects method calls before resolution even though
  resolving first could occasionally produce a "nicer" message (precedence rule 2).

## Deferred items

| Item | Trigger to revisit |
| --- | --- |
| Operators over dynamic scopes (runtime-binder dispatch) | Recurring user demand for `:: dynamic` + expressions; requires its own sandbox analysis |
| `?.` operator | Only if member-tier null-safety is ever made opt-out; otherwise permanently redundant |
| Verbatim / interpolated / raw strings in the native tier | A concrete template corpus need that `+`/`format` cannot express; interpolation would need an `INTERP_STR`-style mode stack in CALL |
| Partial constant folding of literal subtrees | A profiled compile-time or render-time win on real templates |
| Culture-parameterized function overloads | Roadmap "recorded, not v1"; first host request for localized formatting via functions (phase 2 output work may supersede) |
| `len` over collections/enumerables | User confusion reports; `.Count` covers the known cases |
| Registered functions with chained-extension arguments (`@fn(a():b())`) | A scenario that cannot restructure as `@a():b():fn()` or an expression |
| `FunctionRegistry.Empty` (no built-ins) | A host needing a stricter-than-Default whitelist |
| `[Obsolete]` on `AllowCSharp` | The 2.0 window (D6) |
| IDs for untouched pre-existing diagnostics (e.g. the AllowCSharp guard message) | The phase that next touches each message ([cross-cutting D1](../common/cross-cutting-decisions.md#d1--stable-diagnostic-ids-hedxxxx)) |
| Interpreter mode (`Compile(preferInterpretation: true)`) | Only if an AOT-like host appears; built-ins are already interpreter-safe by the non-span binding rule |
| `samples/sandboxed-user-templates` gallery item | Phase 9 harness (recorded in the testing plan) |

## External references

Primary sources verified for this spec (July 2026), plus the roadmap's grounding set
which was re-checked where load-bearing:

- [ANTLR left-recursion](https://github.com/antlr/antlr4/blob/master/doc/left-recursion.md) — precedence by order, per-alternative `<assoc=right>`, tool-side target-independent rewrite (re-verified for the C# target question).
- [ALL(\*) tech report](https://www.antlr.org/papers/allstar-techreport.pdf) — first-alternative-wins.
- [ANTLR maximal munch](https://github.com/antlr/antlr4/blob/master/doc/wildcard.md) / [lexical FAQ](https://github.com/antlr/antlr4/blob/master/doc/faq/lexical.md) — the keyword-`WS*` correction's basis.
- [C# operators and expressions](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/) — precedence/associativity.
- [Built-in numeric conversions](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/numeric-conversions) — promotion table.
- [Expression class](https://learn.microsoft.com/en-us/dotnet/api/system.linq.expressions.expression) — factory coverage; integral-only bitwise/shift; `Int32` shift right operand.
- [Expression.LessThan](https://learn.microsoft.com/en-us/dotnet/api/system.linq.expressions.expression.lessthan) — `liftToNull` node-type rules (re-verified; quoted in the semantics doc).
- [Expression.Coalesce](https://learn.microsoft.com/en-us/dotnet/api/system.linq.expressions.expression.coalesce) — non-nullable-left `InvalidOperationException`; result-type rules (re-verified).
- [Expression.Condition](https://learn.microsoft.com/en-us/dotnet/api/system.linq.expressions.expression.condition) — equal arm types; `Type` overload limits.
- [Integral numeric types](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/integral-numeric-types) / [floating-point types](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/floating-point-numeric-types) — literal typing/suffixes.
- [String.StartsWith](https://learn.microsoft.com/en-us/dotnet/api/system.string.startswith) — the single-argument overload "performs a word (case-sensitive and culture-sensitive) comparison using the current culture", which is why the `startswith`/`endswith` built-ins pass `StringComparison.Ordinal` explicitly (verified July 2026; D13).
- [String.Replace](https://learn.microsoft.com/en-us/dotnet/api/system.string.replace) — ordinal search; `ArgumentNullException`/`ArgumentException` on null/empty `oldValue`; null `newValue` removes occurrences — the corners the defensive `replace` body pins (verified July 2026; D13).
- [What's new in C# 14](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-14) — implicit span conversions; spans as extension receivers (the D2 audit's finding rule; re-verified).
- [What's new in .NET 10 runtime](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/runtime) — escape-analysis/stack-allocation context for the no-conditional-compilation stance.
- [dotnet/roslyn#81329](https://github.com/dotnet/roslyn/issues/81329) — expression interpreter vs span-parameter methods (the non-span built-in binding rule).
- [Fluid](https://github.com/sebastienros/fluid) — allow-list sandbox precedent (roadmap grounding, carried).
