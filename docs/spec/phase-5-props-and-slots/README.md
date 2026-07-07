# Phase 5 specification — named props & parameterized slots

Status: **Specified — ready for implementation.**
Roadmap source: [phase 5 — named props & parameterized slots](../../roadmap/phase-5-props-and-slots.md).
Prior phases assumed merged
([D5, sequential order](../common/cross-cutting-decisions.md#d5--sequential-implementation-order)):
[phase 1 — native expressions](../phase-1-native-expressions/README.md) (the expression
grammar this phase extends — literals, the left-recursive `expr` rule, CALL-mode tokens,
the `ExprNode` AST, `NativeExpressionCompiler`, `MemberPathResolver`, `NumericPromotion`,
the `HEDxxxx` plumbing), [phase 2 — safe output](../phase-2-safe-output/README.md)
(`OutputProfile`, encode-at-the-leaf rule, and its D12 `[NotEncode]` revisit assigned to
this phase), [phase 3 — branching](../phase-3-branching/README.md) (`Scope` as a declared
`readonly struct` with the `ScopeLocals` frame, the body-execution funnel, and its D11
`@out()` frame semantics), and [phase 4 — ergonomics](../phase-4-ergonomics/README.md)
(`DefinitionItem.HasDefaultOutput`, `TrimDirectiveLines`). This phase claims IDs from the
`HED5xxx` block per
[cross-cutting D1](../common/cross-cutting-decisions.md#d1--stable-diagnostic-ids-hedxxxx)
and is, with phase 1, one of the only two grammar-changing phases (one `.g4` edit + one
`generate_cs.cmd` regen commit).

### Documents

| Document | Purpose |
| --- | --- |
| README.md (this file) | The spec proper: assumed state, decision records, implementation plan, public API contract, diagnostics, testing, back-compat, performance. |
| [grammar.md](grammar.md) | Full lexer/parser rule diffs (the new `DEF_PROPS` mode, the `THIS` token, the 5th `call` alternative, the `def_props` rules), coexistence/ambiguity analysis against every existing call and definition form, editor-token mapping, the regen procedure, and the parse corpus. |

## Scope and goal

Complete the component story. A definition declares **typed named parameters (props) with
defaults** in its header; call sites pass them **by name** alongside the positional model
parameter; a definition can declare a **slot parameter** and hand a value back into the
caller's projected `{{ … }}` content through `@out(expr)`:

```heddle
@%
  <card(style: string = "plain", compact: bool = false)>
  {{
    <article class="card @(style)">
      <h2>@(Title)</h2>
      @ifnot(compact){{ <p>@(Summary)</p> }}
      @out()
    </article>
  }} :: Article

  <picker(out:: MenuOption)>
  {{ <ul>@list(Options){{ <li>@out(this)</li> }}</ul> }} :: Menu
%@

@card(Article, style: "wide", compact: true)
@picker(Menu){{ <a href="/go">@(Label)</a> }}   @* body renders once per option, option as model *@
```

Missing, unknown, and mistyped props are **hard compile errors** with positions —
maintainer-ratified (July 2026, recorded in the
[roadmap index](../../roadmap/README.md)); this spec makes "mistyped"
precise. In scope: the grammar change (definition-side `DEF_PROPS` mode, call-side named
arguments, the `this` expression), the parse DTOs, the prop layout/binding compiler with
its diagnostics, the `Scope.PropsData` runtime carriage, body prop resolution merged into
member resolution, the slot parameter (`out:: Type` declaration + `@out(expr)`
projection), inheritance/override semantics, the `[NotEncode]` resolution owed to
phase 2, tests/benchmarks, and the published docs. Out of scope with triggers
([Deferred items](#deferred-items)): named/multiple slots, C#-tier prop values and prop
reads, named arguments on extensions or registered functions, expression-valued defaults.

## Assumed state

Every seam below was **re-verified against the current source** (July 2026; searches
excluded `bin/`, `obj/`, `node_modules/`, `generated/`). Corrections to roadmap claims
are marked ⚠ and closed in the decision records. Phase 1–4 artifacts are cited from their
specs; everything else is current-source fact.

| Seam | Verified state |
| --- | --- |
| [HeddleLexer.g4](../../../src/Heddle.Language/HeddleLexer.g4) `mode DEF` | Exactly ten rules: `DEF_COMMENT`, `DEF_STARTNAME` (`<`), `TYPE_ID` (`ID_TYPE → ID` — dotted names, generics, `[]`), `DEF_ENDNAME` (`>`), `SUB_START` (`{{` → `SUB_BLOCK`), `DEF_OUT` (`->` → `OUT_MODE`), `DEF_TYPE` (`::`), `DELIM` (`:`), `DEF_CLOSE` (`%@`), `DEF_WS` (hidden). **No rule matches `(`, `)`, `,`, `=`, or literal characters** — the roadmap's constraint 4 ("definition-header parens are currently a lex error") checks out: every character of the new declaration surface is a token-recognition error today. The `ID_TYPE` fragment carries trailing `SINGLE_LINE_WS*`, which drives the keyword-length tie-break in the new mode (same maximal-munch analysis as phase 1's `CALL_ID` correction, [grammar.md](grammar.md#lexer-changes)). |
| [HeddleParser.g4](../../../src/Heddle.Language/HeddleParser.g4) | `def: DEF_STARTNAME ID def_base? DEF_ENDNAME default_chain? subtemplate def_type?` — the props list slots between `ID` and `def_base?`. `default_chain: DEF_OUT chain` reuses the ordinary `call` rule with a name override, so the call-side named-argument alternative works inside `-> (…)` for free (D17). The phase 1 spec adds the 4th `call` alternative and the `expr` rule ([phase 1 grammar](../phase-1-native-expressions/grammar.md)); this phase adds the 5th alternative and `THIS`. |
| [DefinitionItem](../../../src/Heddle/Language/DefinitionItem.cs) / [DefinitionBlock](../../../src/Heddle/Language/DefinitionBlock.cs) | `DefinitionItem` carries `Name`, `ParameterTemplate`, `BaseDefinition`, `ModelType` (string, default `"object"`), `Position`, `Context`, `FullOverride`; the copy constructor deep-copies the base chain; `OverrideWith` demotes the current state to `BaseDefinition` and replaces name/template/type/context in place. Phase 4 adds `HasDefaultOutput` (copied in the copy ctor). Prop declarations and the slot type ride the same DTO (WI3) and must join both the copy ctor and `OverrideWith`. |
| [HeddleMainListener](../../../src/Heddle/Language/HeddleMainListener.cs) / [ParseContext.CreateDefinition](../../../src/Heddle/Language/ParseContext.cs) | `EnterDef` → `CreateDefinition(context, …)` reads `context.ID()`, `def_base()`, `def_type()`, `default_chain()` and builds the `DefinitionItem` (two near-identical branches for base/no-base). Props parsing lands here as one shared helper over `context.def_props()`. Definition isolation (`IsolateContext`) copies `DefinitionItem`s via the copy ctor — prop declarations survive isolation once the ctor copies them. |
| [HeddleCompiler.CompileItem](../../../src/Heddle/Runtime/HeddleCompiler.cs) | Definition lookup at the top (`parseContext.DefenitionExists` / `GetDefenition`, lines 272–275, anchor `CompileItem`); parameter classification follows (member path / C# / nested chain, plus phase 1's native branch). Named-argument validation and binder construction hook in after definition resolution. `CompileModelAccessor` (line 353) is the member-tier resolution loop phase 1 extracts into `MemberPathResolver` — the prop-first hook lands in that shared resolver (D9). |
| [HeddleCompiler.CreateExtension](../../../src/Heddle/Runtime/HeddleCompiler.cs) | Definition branch (lines 442–465, anchor `CreateExtension`): builds **two** `DefinitionBaseExtension` instances — the outer one compiles the **invocation-site body** (caller content) via `InitializeTemplate(extension, extensionItem.ParameterTemplate, dataType, …, parseContext)`, then the inner one (`def.DefinitionParameterTemplate`) compiles the **definition body** via `InitializeTemplate(…, definition.ParameterTemplate, dataType, …, definition.Context)`. The caller body compiles **first**; its threaded return type (`typeof(string)` from `AbstractExtension.InitStart`) becomes the definition body's chained type. `CheckTypes(dataType, definition.Position, …, acceptType)` is the per-site model check the hard-error policy mirrors. |
| [DefinitionBaseExtension](../../../src/Heddle/Core/DefinitionBaseExtension.cs) | `ProcessData`/`RenderData`: caller content is **pre-rendered once** to a string (`GetInnerResult(scope)`), then the definition body executes under `scope.Chain(chained)` — the string arrives as `ChainedData`. Recursion guarded by a `ThreadLocal<int>`; no cross-render mutable state. This is the seam the props frame installation and the slot carrier replace (D7, D11). |
| [OutExtension](../../../src/Heddle/Extensions/OutExtension.cs) | ⚠ **Roadmap verification item closed:** `ProcessData`/`RenderData` read `scope.ChainedData` and **ignore `ModelData` entirely** — `@out(X)` today is *accepted-and-ignored*: `X` is compiled (and can fail property resolution), evaluated at render, and discarded; the chained value is spliced regardless. This becomes a positioned error (D13). Verified oddity, preserved: `RenderData`'s `if (!InnerExist)` branch has no `else` — the bodiless path additionally calls `RenderInnerResult`, which renders the empty `_innerResult` (no observable output). The slot branch (D11) is added without "fixing" this shape. |
| [ParamExtension](../../../src/Heddle/Extensions/ParamExtension.cs) | `InitStart` returns `dataType` without compiling a body; `ProcessData` returns `ModelData`; `RenderData` is a no-op (phase 3's correction confirmed). Props do not touch it. |
| [Scope](../../../src/Heddle/Data/Scope.cs) | Six current fields plus phase 3's `internal readonly ScopeLocals Locals`; all transforms (`Parent` ×2, `Chain`, `Model` ×2, `RenderProxy`, phase 3's `WithLocals`) are pure copies. `Model(model)` sets `ParentModelData` to the pre-swap `ModelData` — so inside an extension the **caller's model is `ParentModelData`** and `scope.Parent()` reconstructs the caller's view; this is the props-argument evaluation basis (D8). `PropsData` joins as one more copied field. |
| [TemplateChain](../../../src/Heddle/Runtime/TemplateChain.cs) / [TemplateItem](../../../src/Heddle/Runtime/TemplateItem.cs) | Item dispatch is `Extension.…(scope.Model(Parameter.GetParameter(scope)))`; the first item of a body-level chain executes against the body-level scope (so `ChainedData` at a definition call site inside a definition body is that body's chained channel — the composition path D11's runtime guard covers). |
| [CompileContext](../../../src/Heddle/Runtime/CompileContext.cs) | Private copy ctor (line 73) is the child-compile inheritance route (`@model`'s `ScopeType`, phase 2's `OutputProfile` precedent). `ActivePropLayout` and `SlotParameterType` ride the same route (D12). `CompiledItems` memoizes per `OutputItem`. |
| [ExType](../../../src/Heddle/Data/ExType.cs) | `Type` + `IsDynamic`; equality by type + dynamic flag. Prop and slot types resolve to `ExType` via `ReflectionHelper.ResolveType` against `CSharpContext.Namespaces`, exactly like `DefinitionItem.ModelType`. |
| [CallParameter](../../../src/Heddle/Language/CallParameter.cs) | Three shapes today (`ModelParameter`+`RootReference`, `ChainParameter`, `CSharpExpression`); phase 1 adds `NativeExpression`. `PropArguments` joins as an orthogonal list — it does not participate in `IsModelTypeParameter` (the positional classification governs, D4). |
| [InitContext](../../../src/Heddle/Core/InitContext.cs) | Carries `ParameterTemplate`, `CompileScope`, `ParseContext` — **no seam exposes the call parameter to an extension at compile time** (phase 2's D2 finding, whose deferred "argument-access seam" trigger names this phase). D16 adds the internal `SourceItem` seam `OutExtension` needs. |
| Phase 1 artifacts used | The `expr` grammar and the complete public AST hierarchy — abstract `ExprNode` plus the eight sealed nodes `BinaryNode`, `UnaryNode`, `TernaryNode`, `LiteralNode`, `PathNode`, `IndexNode`, `CallNode`, `MethodCallNode` (this phase adds `ThisNode` as the ninth) — `ExpressionAstBuilder` (literal decoding reused by the definition-side walker), `NativeExpressionCompiler` + `NumericPromotion` (argument conversion ranking), `MemberPathResolver` + `ModelParameter.BuildNullSafePropertyChain` (prop-rooted member hops), `ConstantParameter` folding (constant prop arguments), `HED1014` (`MemberPathsOnly`), diagnostic-ID plumbing. |
| Phase 2 artifacts used | `OutputProfile` and the encode-at-the-emitting-leaf rule (prop values need zero new encoding logic — D14); the D12 `[NotEncode]` revisit obligation resolved here as D14; the deferred "marker-interface seam for paren-argument directives" resolved narrowly as D16. |
| Phase 3 artifacts used | `Scope` as declared `readonly struct`, `ScopeLocals` + compile-time frame provisioning, the body-execution funnel (`GetInnerResult`/`RenderInnerResult`) — slot projections enter through the existing funnel members of the outer extension, so frame isolation holds with zero new entry points (D11); phase 3 D11's `@out()` pins gain exactly one slot-mode refinement. |
| Phase 4 artifacts used | `DefinitionItem.HasDefaultOutput` (coexists with the new DTO fields); `TrimDirectiveLines` already whole-line-trims `@% … %@` spans — multi-line prop headers live inside those spans and add no new trimming surface; HED4002's self-call marker is unaffected by props on default chains. |
| [DocumentParser](../../../src/Heddle/Language/DocumentParser.cs) | SLL-first, LL fallback on `ParseCanceledException`/errors; editor mode parses `LL_EXACT_AMBIG_DETECTION`. The ambiguity analysis and the SLL-corpus gate in [grammar.md](grammar.md#coexistence-and-ambiguity-analysis) are stated against this behavior. |

## Design decisions

Numbering is local to this spec; cross-cutting decisions are cited by link. Every roadmap
open question, lean, "fixed input", and pin-during-design item for this phase is closed
below.

### D1 — Declaration surface: a props list in header parens, lexed by a new `DEF_PROPS` mode

**Decision.** Props are declared in parentheses immediately after the definition name,
before the optional `:base`:

```heddle
<card(style: string = "plain", compact: bool = false)>
<fancy(tone: string = "info"):card>
<picker(out:: MenuOption)>
<combo(style: string = "plain", out:: Option)>
```

Lexing: one new `mode DEF` rule (`(` → `pushMode(DEF_PROPS)`) and one new lexer mode
`DEF_PROPS` containing every token of the declaration surface (`)`, `:`, `::`, `=`, `,`,
`-`, the phase 1 literal tokens, `ID_TYPE`-based identifiers/type names, hidden
whitespace and comments). Full rule text in [grammar.md](grammar.md#lexer-changes);
parser rules `def_props`/`def_prop_item`/`def_prop`/`def_slot`/`def_prop_default`/
`def_literal` in [grammar.md](grammar.md#parser-changes). Empty parens (`<card()>`) are
legal and mean "no props", mirroring `@card()`.
**Rationale.** A dedicated mode makes the additive proof trivial: existing templates
cannot reach it (a `(` in `DEF` mode is a token error today — verified above), and ANTLR
guarantees "the lexer can only return tokens matched by … the current mode", so no
existing token stream can change ([lexer rules doc](https://github.com/antlr/antlr4/blob/master/doc/lexer-rules.md)).
It also keeps the ten existing `DEF` rules byte-identical instead of threading eight new
tokens through them.
**Alternatives rejected.** Adding the tokens to `mode DEF` directly (every new rule
interacts with `TYPE_ID` maximal munch across the *whole* header, not just inside
parens); declaring props in the body via a directive extension (`@props(){{…}}` — loses
the header's single-glance signature, and the phase's per-call-site binding needs the
declarations on the `DefinitionItem` before any body compiles); attribute-style
declarations on the C# model (props are template-level, model-orthogonal by roadmap
constraint 1).
**Grounding.** [ANTLR lexer rules & modes](https://github.com/antlr/antlr4/blob/master/doc/lexer-rules.md)
(mode isolation, `pushMode`/`popMode`/`type()` commands);
[Phoenix.Component `attr/3`](https://hexdocs.pm/phoenix_live_view/Phoenix.Component.html)
(declaration-adjacent-to-definition precedent).

### D2 — Prop types resolve like model types; defaults are literals only

**Decision.** A prop's type name is an `ID_TYPE` token (dotted names, generics, arrays)
resolved at compile time via `ReflectionHelper.ResolveType` against
`CSharpContext.Namespaces` — exactly the `DefinitionItem.ModelType` path; unresolvable →
`HED5010`. A prop's default is a **phase 1 literal** (int/real/string/char/bool/null,
with an optional leading `-` on numerics) decoded by the same routine the expression
builder uses (`ExpressionAstBuilder`'s literal decoding, exposed as an internal static
helper — the DRY move). The default must be convertible to the declared type under the
D10 conversion rule (identity; implicit numeric widening per phase 1's
[promotion table](../phase-1-native-expressions/operator-semantics.md); `null` for
reference/`Nullable<T>` types; boxing when the prop type is `object`); otherwise
`HED5009`. Converted defaults are boxed **once at compile time** into the frozen
prototype array (D7). A prop with no default is **required** (ratified).
**Rationale.** Literal-only defaults make defaults compile-time constants, which is what
funds the zero-allocation frozen-array path (D7) and keeps the evaluation-context
question from existing at all (a default like `::Site.Style` would have to pick caller
vs definition context — both surprising for a *declaration*). Peer engines agree the
default belongs to the declaration and is a plain value: HEEx `attr … default:`
(compile-time value), Vue `defineProps` `default`, Blazor property initializers.
**Alternatives rejected.** Expression defaults (context ambiguity above; revisit trigger
in [Deferred items](#deferred-items)); `Enum`-style string coercion ("false" → false —
silent stringly typing, contradicts the strict-errors identity); allowing suffix-free
real → decimal narrowing (C# forbids it; the operator-semantics literal table is the
single source of literal typing).
**Grounding.** [Phoenix.Component attr/3](https://hexdocs.pm/phoenix_live_view/Phoenix.Component.html),
[Vue 3 props](https://vuejs.org/guide/components/props.html).

### D3 — The slot parameter is declared in the header as `out:: Type`; `out` and `this` are reserved prop names

**Decision.** A definition that passes values into the caller's projected content
declares exactly one slot parameter inside the props parens using the existing
`::`-means-type token: `<picker(out:: MenuOption)>`. `out` here is the identifier of the
`@out` mechanism; a *prop* named `out` is rejected with `HED5015` (fix text points at
`out::`), and `this` is likewise reserved (`HED5015`) because a `this:` named argument is
unlexable at call sites (D5). `foo:: T` (any identifier other than `out` before `::`) is
`HED5016`; a second `out::` in one header is `HED5017`. The slot type resolves like a
prop type (`HED5010` on failure). The declaration may appear anywhere in the list;
documentation shows it last.
**Rationale.** The slot type must be **declared**, not inferred from the body's
`@out(expr)` expressions: (a) the caller body compiles against the slot type, and
declaration keeps the verified caller-body-first compile order in `CreateExtension`
intact (inference would require compiling the definition body first and re-threading the
chained type); (b) every precedent that types the passed value declares it — Blazor
`RenderFragment<TValue>`, Svelte 5 `Snippet<[T]>`, HEEx slot `attr`s; (c) phase 6 tooling
wants the type on the signature. Reserving `out` (instead of a subtle one-colon vs
two-colon distinction deciding silently between "prop named out" and "slot") converts the
likeliest confusion into a positioned error.
**Alternatives rejected.** Inference from `@out(expr)` static types (compile-order
surgery; ambiguous with multiple `@out`s; useless for tooling before a body exists); a
marker after the body like `:: Model` (`{{…}} :: Menu :: Option` — two `::` with
different subjects on one line); an arrow form `<picker(-> Option)>` (`->` already means
"default output" one token away in the same header); a `slot:` contextual keyword
(`slot` is a plausible real prop name; `out` is already the engine's projection word).
**Grounding.** [Blazor templated components](https://learn.microsoft.com/en-us/aspnet/core/blazor/components/templated-components)
(`RenderFragment<TValue>`), [Svelte 5 snippets](https://svelte.dev/docs/svelte/snippet)
(typed `Snippet<[Product]>` parameters — the post-`let:` design), [Phoenix.Component
slot/3](https://hexdocs.pm/phoenix_live_view/Phoenix.Component.html).

### D4 — Call-side named arguments: a 5th `call` alternative; values are native expressions only

**Decision.** The `call` rule gains a fifth alternative, ordered last:

```antlr
| extension_id? OUT_PARAMSTART (expr COMMA)? named_argument (COMMA named_argument)* OUT_PARAMEND
```

with `named_argument: ID DELIM expr;`. The optional leading `expr` is the positional
model argument; the AST builder classifies it: a pure `PathNode` (no target, optional
root ref) populates `ModelParameter`/`RootReference` exactly like alternative 2 — so a
member-path positional compiles bit-identically to today, including dynamic-scope
handling and definition monomorphisation; a `ThisNode` positional maps to the empty
model parameter (explicit current-model passthrough); anything else becomes
`NativeExpression`. Named-argument **values are phase 1 native expressions evaluated in
the caller's context** — literals, paths, `::` root refs, operators, registered
functions, `this`. ⚠ **Correction to roadmap constraint 2** ("the inner-`@` C# tier
remains available per argument"): it is not, and cannot be. Verified against `mode CS`:
the inner `@` pops `CALL` and pushes `CS`, whose **only exit is the call-closing `)`**
(`CS_CSHARP_END: PARA_CL -> type(OUT_PARAMEND), popMode`) — a C# argument value would
swallow every subsequent argument and the terminator, and re-teaching `CS` to stop at a
top-level `,` would break existing C# expressions containing top-level commas. Prop
values are therefore native-tier only; the C# escape remains per-call (compute the value
in the model, or via a registered function). `@card(A, x: @ expr )` is a positioned
syntax error, pinned in the corpus.
**Rationale.** Ordering the alternative last preserves every existing parse
(first-alternative-wins, the phase 1 mechanism); the roadmap's `:` collision analysis is
confirmed and formalized in [grammar.md](grammar.md#coexistence-and-ambiguity-analysis):
a chain `:` can only follow `)`, a ternary `:` only follows `?`, and a named-argument `:`
only follows a bare identifier in argument position — an identifier without parens
cannot start a chain, so the three are grammatically disjoint.
**Alternatives rejected.** C# values for the final argument only (a position-dependent
cliff: reordering arguments changes validity); `=` instead of `:` for named arguments
(no `=` token exists in `CALL` and `:` is the ecosystem convention the roadmap sketched);
multiple positionals (nothing to bind them to — the model is the only positional
channel).

### D5 — `this` joins the native expression tier

**Decision.** A new CALL-mode keyword token `THIS` (`'this' WS*`, the phase 1
keyword-`WS*` pattern, placed before `CALL_ID`) and a new primary `expr` alternative
`THIS # ThisExpr` → public AST node `ThisNode`. Semantics: the current scope's model,
statically typed as the current scope type. As a **whole** expression it compiles to the
existing `EmptyParameter` (model passthrough — works on dynamic scopes too, like the
empty member path); as an **operand or path root** (`this.Name`, `len(this)`,
`this == null`) it is a typed operand and follows phase 1's dynamic-operand rule
(`HED1004` on dynamic scopes). Grammar position: `ThisExpr` is a primary
(non-left-recursive seed) alternative between `LiteralExpr` and `PathRootExpr`
([full rule text](grammar.md#parser-changes)) — primaries take no part in operator
precedence, and the postfix alternatives (`MemberHopExpr`, `IndexExpr`,
`MethodCallExpr`) compose over it exactly as over any primary, so `this.Name` is
`PathNode{Target = ThisNode}[Name]` and member-path hop semantics (null-safe chain,
`HED0001` on a missing member) apply from the first hop on. `this` cannot collide with
model members: `this` is a C# keyword, so no CLR property can be named `this`. ⚠ This replaces the roadmap sketch's
slot-passing form `@out(@())`, which cannot lex — inside `CALL` mode, `@` is
`CSHARP_START` (the C#-tier escape), so `@out(@())` is today a C#-expression parameter,
not a nested empty call.
**Rationale.** Slot passing needs a way to hand over *the current model itself*
(`@list(Options){{ @out(this) }}` is the flagship), and phase 1 deliberately shipped no
such primary because bare paths covered every value *derived from* the model — the model
itself was only reachable as an empty parameter, which `@out()` already assigns a
different meaning. `this` is the C#-familiar spelling and composes with the whole
existing `expr` grammar for free (member hops, function arguments, comparisons).
**Alternatives rejected.** A bare `.` or `*` marker (unprecedented syntax for C#-shaped
users); overloading empty `@out()` by context (empty already means "splice the caller
content"; slot mode gives it a targeted error instead, D11); `model` as the keyword
(a common real property name — collision, whereas `this` is collision-free by
construction).

### D6 — Prop layout: per definition, inheritance-flattened, index-stable; re-declaration re-defaults

**Decision.** Each definition's props compile to a **layout**: an ordered slot table
`[base chain props in declaration order (outermost base first), then this definition's
new props in declaration order]`. A child inherits every base prop (roadmap
constraint 1). A child re-declaring an inherited prop name **keeps the base slot index**
and replaces the default (re-defaulting is the sanctioned use); the re-declared type
must be identical to or assignable to the inherited type under the same
`TypeExtension.IsType` discipline as model narrowing — otherwise `HED5008`
(roadmap success criterion 4's `style: int` case). Two declarations of one name in a
*single* header → `HED5007`. Full overrides (`<name:name>`) follow the same rule with
the demoted old definition as base — no special case. Layouts are resolved once per
compile (cached on `CompileContext` keyed by `DefinitionItem`) and shared by every call
site of that definition in the compile, including the two-site abstract scenario: an
untyped `<panel(style: string)>` monomorphises its *body* per call site while both sites
bind against the **same** layout — prop types are model-orthogonal.

**Worked examples** (the executable rows live in `PropsInheritanceTests` and the
`props-inherit` fixture; slot indices asserted via internals):

```heddle
@%
  <card(style: string = "plain", compact: bool = false)>{{ … }} :: Article
  <fancy(tone: string = "info", style: string = "wide"):card>{{ … }} :: Article
%@
```

| Layout | Slot 0 | Slot 1 | Slot 2 |
| --- | --- | --- | --- |
| `card` | `style: string = "plain"` | `compact: bool = false` | — |
| `fancy` | `style: string = "wide"` — re-declaration: **base index kept**, default replaced | `compact: bool = false` — inherited untouched | `tone: string = "info"` — new, appended after the base chain |

Note the child's *declaration order* (`tone` before `style`) does not reorder anything:
re-declared props stay at their inherited index, and only genuinely new names append.
`@fancy(Article)` binds `style = "wide"`, `compact = false`, `tone = "info"`;
`@fancy(Article, style: "narrow")` overrides slot 0 at the call site as usual. Index
stability is what keeps a child's layout a prefix-compatible extension of its base's:
every base slot keeps its index in every descendant, so compiled slot reads and frozen
prototypes never shift when a library adds a derived definition.

Re-declaration assignability is `TypeExtension.IsType` — accepted when
`inheritedType.IsAssignableFrom(redeclaredType)`, the same direction as model narrowing
(`WalkValidateDefinitionType`'s `baseType.IsType(currentType)`), i.e. the child may
promise something more specific:

- `<media(content: object)>` + `<textmedia(content: string = "hi"):media>` — accepted:
  `string` is assignable to `object`; the slot keeps index 0, the type narrows, the
  default is added.
- `<card(…)>` + `<fancy(style: int):card>` — `HED5008`: `int` is not assignable to
  `string` (roadmap success criterion 4's exact case).

Full override with props — same path, no special case:

```heddle
@%
  <card(style: string = "plain")>{{ … }} :: Article
  <card(style: string = "boxed", elevated: bool = false):card>{{ … }} :: Article
%@
```

The second declaration demotes the first to `BaseDefinition` (`OverrideWith`), keeps
`style` at slot 0 with the new default `"boxed"`, and appends `elevated` at slot 1;
call sites compiled against the override bind the two-slot layout. Only the overriding
definition's own body compiles and executes (verified: `CompileFromDefenition` compiles
`definition.ParameterTemplate` only; the base chain feeds model-type validation), so no
stale slot read against the old layout can exist.
**Rationale.** Index stability plus flattening makes the runtime carriage a plain array
with compile-time indices (constraint 5); the assignability rule is the roadmap's
ratified lean made concrete with the engine's existing helper so prop narrowing and
model narrowing cannot drift apart.
**Alternatives rejected.** Error on any re-declaration (kills the ratified re-default
scenario); silent re-typing (contract erosion across template libraries — the exact
thing the hard-error identity forbids); per-call-site layouts (nothing varies per site —
the layout is declaration data).

### D7 — Runtime carriage: `Scope.PropsData object[]`, frozen prototype, all-constant sharing

**Decision.** `Scope` gains one field, `internal readonly object[] PropsData` (`null`
for every prop-less execution — the overwhelmingly common case), copied unchanged by all
transforms and replaced only by a new internal `Scope WithProps(object[] props)`
transform. Per call site the compiler builds a `PropsBinder` (internal) holding:

- the **frozen prototype array** — one `object[layout.Count]` built at compile time with
  every default and every constant argument (literals and constant-folded expressions,
  phase 1 `ConstantParameter`) already converted and boxed;
- the **dynamic slot plan** — `(int index, IRuntimeParameter parameter)[]` for the
  non-constant arguments.

At render, `Bind(in Scope)`: when the plan is empty the **frozen array itself** is
returned — shared across invocations, renders, and threads, never written after
construction (the engine exposes no write path to `PropsData`; this immutability
invariant is normative). Otherwise `(object[]) frozen.Clone()` plus one
`parameter.GetParameter(callerView)` store per dynamic slot. The outer
`DefinitionBaseExtension` installs the result on the definition-body scope only
(`scope.Chain(carrier).WithProps(props)`) — caller content keeps the incoming
`PropsData`, which is exactly the lexical scoping D9 compiles against. Per
[cross-cutting D7](../common/cross-cutting-decisions.md#d7--net-10-stance-no-conditional-compilation-by-default)
no part of this design leans on JIT escape analysis: the roadmap's .NET 10 review proved
the per-invocation array escapes (stored into `Scope`, carried into separately compiled
delegates), so the all-constant sharing is the allocation lever — it works on every TFM.
The carriage stays behind `PropsBinder` + `WithProps` so phase 7's generated-C# backend
can swap the representation (typed parameters / spans) without touching layout or
binding, as the roadmap's .NET 10 item 2 directs.

**Clone-path allocation accounting** (normative budget; measured on x64 via
`GC.GetAllocatedBytesForCurrentThread`, .NET 8 — the same 16 B-object-overhead
convention as phase 3's D15 arithmetic):

- One `object[]` clone per dynamic invocation: **24 + 8·N bytes** for a layout of `N`
  slots (16 B object overhead + 8 B length word + 8 B per element reference). The
  two-slot `card` layout is 40 B; `(object[]) frozen.Clone()` allocates exactly what
  `new object[N]` does — no `Clone()` premium.
- Plus **one 24 B box per value-typed dynamic argument** (16 B object overhead + 8 B
  data slot — `int`, `bool`, `long`, and `double` all measure 24 B). Reference-typed
  dynamic arguments add nothing beyond their slot store; constant arguments add nothing
  at all (boxed once into the prototype at compile time).
- All-constant call sites: **0 bytes per invocation** — the frozen array itself is
  returned.

`PropsRenderBenchmarks` (WI8) pins both ends: `RenderAllConstantProps10K` at zero extra
allocation and `RenderDynamicProps10K` at exactly this arithmetic — any additional byte
is a defect, not a ratified trade.

**All-constant sharing proof.** The frozen array is safe to share across invocations,
renders, and threads because its write paths are enumerable and all pre-publication:
(1) it is written only inside the `PropsBinder` construction (defaults and converted
constant arguments), which runs on the compiling thread before the template is handed
to any renderer; (2) `Bind` returns it without writing; (3) the only readers —
`PropsSlotParameter.GetParameter` and the props-aware compiled delegates — perform
`PropsData[index]` loads; no engine code path contains a store into a `PropsData`
array; (4) `WithProps` replaces the *reference* on a `Scope` copy, never the array
contents; (5) no public member exposes `PropsData`, so user code cannot obtain the
array to mutate it. Publication to render threads rides the same happens-before edge as
every other compiled artifact (the host publishes the compiled template object;
`ConstantParameter` values and `_innerResult` already rely on exactly this edge).
`PropsConcurrencyTests` (WI8) is the executable form of this proof.
**Rationale.** This is roadmap constraint 5 ("index-based slots, zero lookup cost, no
dictionaries at render") satisfied with the smallest mechanism; the frozen-array lever is
the roadmap's mandated design item ("the RFC should mandate this regardless of carriage
choice"). Boxing of value-typed *dynamic* arguments is accepted — the roadmap records
that both candidate carriages box, and all-defaults/literal sites (the common component
case) pay zero.
**Alternatives rejected.** `[InlineArray]` fixed slots in `Scope` (roadmap .NET 10
item 3: doubles `Scope`'s copy cost against the phase 3 growth guard, needs an overflow
path and a second carriage behind `#if NET8_0_OR_GREATER` — revisit trigger recorded);
per-arity generic frames (roadmap item 4: evaluated, rejected — instantiation explosion);
`ReadOnlySpan<object>` carriage (ByRef-like fields are rejected by
`System.Linq.Expressions` — phase 7 territory); a dictionary frame (constraint 5 forbids
it); carrying props inside `ScopeLocals` (locals frames are per-body and deliberately
isolated at every body boundary — props must flow *into* nested bodies).

### D8 — Prop arguments evaluate against the caller view: `scope.Parent()`

**Decision.** `PropsBinder.Bind` evaluates dynamic argument parameters against
`scope.Parent()` — the reconstruction of the caller's scope available inside the
extension (verified: `TemplateItem` dispatch swaps the model and stores the pre-swap
model in `ParentModelData`; `Parent()` restores it while keeping `RootData`, `Renderer`,
`Locals`, and `PropsData`). Consequences, all normative: argument member paths resolve
against the **caller's model** (roadmap constraint 2 — compile-time: the arguments
compile in the call site's `CompileScope`, whose `ScopeType` *is* the caller's type);
`::` root refs work unchanged; and **prop forwarding is free** — inside a definition
body, `@card(style: style)` compiles the value against the enclosing layout
(D12 context) and evaluates it against the caller view's `PropsData`, which is the
enclosing frame.
**Rationale.** Evaluating inside the extension (rather than pre-evaluating in
`TemplateItem`) needs zero dispatch changes and zero new `Scope` channels; the caller
view is exact for everything the native tier can express (paths off the model, root
refs, functions, literals — the tier has no chained-value syntax, phase 1 D18).
**Alternatives rejected.** Evaluating in `TemplateItem` before the model swap (requires
handing an extra value set through the `IExtension` dispatch surface — a signature
break or a mutable side channel); compiling arguments against the *definition's* scope
type (wrong by ratified constraint 2 and useless for the caller's data).

### D9 — Body prop reads merge into member resolution; props win; `this.<name>` is the escape

**Decision.** Roadmap lean (a) adopted. Inside a definition body (and every plain nested
body it contains), resolution of the **first segment** of a member path — in both the
member tier (`MemberPathResolver`, shared with `CompileModelAccessor`) and the native
tier (`PathNode` roots) — consults the active prop layout **before** the model:

- Hit → the read compiles to a props-slot access: bare single-segment reads become the
  new `PropsSlotParameter(index)` (a direct `scope.PropsData[index]` read, no delegate);
  multi-hop reads (`style.Length`) root the phase 1 null-safe property chain at
  `Convert(PropsData[index], propType)`; native-tier operands read the slot inside the
  compiled expression (props-aware delegate shape, [Public API contract](#public-api-contract)).
  The read's static type is the **declared prop type** — so `@if(compact)` threads
  `bool` with zero extension changes, `@list(items)` iterates a collection-typed prop,
  and prop reads stay **statically typed even in `:: dynamic` definitions** (the layout
  is model-orthogonal; only non-prop names fall through to dynamic dispatch).
- Hit **and** the current scope type also exposes a readable, visible property of the
  same name → warning `HED5011` at the read site; the prop still wins (static, no
  runtime ambiguity). The escape hatch for the shadowed member is explicit:
  `this.<name>` in a native expression addresses the model unconditionally (D5).
- `::`-rooted paths never consult props (the root reference is an explicit scope
  escape), and standalone *calls* (`@style()`) never resolve to props — props are value
  roots, not callables; extension/definition/function resolution (phase 1 D11) is
  untouched.

**Rationale.** Precedent-backed by the roadmap's own survey (Blazor, HEEx, Vue, Templ
all read declared props as bare names — declaration is what makes resolution static);
the shadowing warning is the Heddle-specific addition because Heddle uniquely shares the
namespace with a model. Hooking the *shared* resolver keeps hop semantics single-sourced
(the phase 1 DRY extraction pays off here — one prop check, both tiers).
**Alternatives rejected.** An explicit `@prop(style)` accessor (roadmap lean (b) —
ceremony at every read, and the shadowing case it "solves" is rarer than the reads it
taxes); model-before-props precedence (a model refactor could silently capture a prop
read — the warning-plus-props-win order makes the failure loud instead); erroring on
shadowing (template libraries could not add props without auditing every consumer's
model).

### D10 — The hard-error policy, made precise

**Decision.** Binding runs at every call site of a props-declaring definition (named
arguments present or not) and enforces, in this order, with **errors — never
warnings** (maintainer-ratified July 2026):

1. **Target check.** Named arguments on a call that does not resolve to a definition →
   `HED5005`. Named arguments on a definition whose layout is empty → `HED5006`.
2. **Duplicate argument.** The same name twice in one call → `HED5004` (second
   occurrence's position).
3. **Unknown prop.** Argument name not in the layout → `HED5001`, message lists the
   declared names (the roadmap's `stlye` typo scenario).
4. **Type check.** The argument expression's static `ExType` must convert to the
   declared prop type by exactly one of: identity; implicit numeric widening (phase 1's
   `NumericPromotion` table — the compiled delegate inserts the `Convert` node);
   `T` → `T?` lifting; reference assignability (`TypeExtension.IsType`); boxing when the
   prop type is `object`; the `null` literal for reference/`Nullable<T>` props. Nothing
   else — no narrowing, no user-defined conversions, no string parsing. Failure →
   `HED5003`. A dynamic-typed argument (possible only via a dynamic-scope path, which
   phase 1's `HED1004` already rejects inside operators) cannot reach a successful bind.
5. **Missing required prop.** After arguments and defaults, any unbound slot →
   `HED5002` at the call's position — including all-positional calls (`@card(Article)`
   with a required prop) and default-output chains (D17).

**Rule 4, operationally.** The six conversions, each defined with its mechanism and an
accept/reject pair (every row is executable — the binding matrix and
`PropsBindingTests` carry them):

| Conversion | Operational definition | Accepts | Rejects |
| --- | --- | --- | --- |
| Identity | The argument's static `ExType` equals the declared prop type (`ExType` equality: CLR type + dynamic flag). No conversion node is emitted. | `style: "wide"` → `style: string` | `compact: "yes"` → `compact: bool` — `HED5003` (B05; no later rule catches it either) |
| Implicit numeric widening | The (source → target) pair appears in the C# implicit-numeric-conversions table pinned below (the same specification table phase 1's `NumericPromotion` is derived from). Dynamic arguments get an `Expression.Convert` node in the compiled delegate; constant arguments and defaults are converted **and boxed at compile time** into the prototype (D2/D7). | `width: B` (`B : byte`) → `width: int` (B10's accept half); `<n(x: double = 1)>` — the `int` literal widens into the frozen default (B12) | `width: Count` (`Count : long`) → `width: int` — narrowing, `HED5003` (B10); `price: 1.5` → `price: decimal` — `double → decimal` is not implicit in C# (use the `m` suffix; D2's literal-typing rule) |
| `T` → `T?` lifting | The prop type is `Nullable<W>`: a non-nullable value-typed argument converts when its type is `W` (identity-lift) or widens to `W` (widen-then-lift — `Convert` to `W`, then to `Nullable<W>`). A `Nullable<S>` argument (reachable via phase 1 lifted operators) converts when `S` is `W` or widens to `W`. A nullable argument never converts to a **non**-nullable prop type. | `max: 5` → `max: int?`; `max: A + B` (both `int?`, static type `int?`) → `max: long?` | `max: 1.5` → `max: int?` (`double` does not widen to `int`); `n: MaybeCount` (`int?`) → `n: int` — `HED5003` |
| Reference assignability | Applies only when the argument's static type is a **reference type**; accepted when `propType.Type.IsAssignableFrom(argType.Type)` (`TypeExtension.IsType` — base classes, interfaces, array covariance). Value-typed arguments never ride this rule — they are covered exhaustively by identity/widening/lifting/box-to-`object`. (This restriction is load-bearing: `IsAssignableFrom` alone would also admit value-type → interface, which the rule set deliberately excludes — it is why box-to-`object` exists as a separate rule at all.) | `body: ArticleBody` → `body: IContent` where `ArticleBody : IContent` | `item: TheMenu` (`Menu`) → `item: Article` (unrelated types); `tag: 42` → `tag: IComparable` (value type to interface — type the prop `object` or compute in the model) |
| Boxing to `object` | The prop type is exactly `System.Object`: any value-typed argument is accepted (constants boxed once into the prototype; dynamics boxed at bind — the 24 B in D7's accounting). | `data: 42` → `data: object`; `data: true` → `data: object` | `data: 42` → `data: ValueType` or any interface prop — only the exact `object` target boxes |
| `null` literal | The literal `null` is accepted for any reference-typed and any `Nullable<T>` prop; rejected for non-nullable value types. (Same rule admits `= null` defaults, D2/B11.) | `tag: null` → `tag: string`; `max: null` → `max: int?` | `compact: null` → `compact: bool` — `HED5003` |

Excluded — with the concrete shape each exclusion rejects: **no narrowing** (`long` →
`int`, `double` → `float`, B10); **no user-defined conversions** (a model type declaring
`public static implicit operator string(Money m)` still does not bind `style: Price` —
the reflection-side conversion search is phase 1's documented deviation 6, kept); **no
string parsing** (`"42"` → `int` prop rejected); **no implicit `ToString()`** (`42` →
`string` prop rejected). D11's slot-value check (`HED5014`) reuses rows 1–4 of this
table verbatim (its recorded subset — no box-to-`object`).

The pinned widening table (C# implicit numeric conversions,
[Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/numeric-conversions);
`decimal` targets are implicit **from integral types only** — never from `float`/`double`):

| From | To |
| --- | --- |
| `sbyte` | `short`, `int`, `long`, `float`, `double`, `decimal` |
| `byte` | `short`, `ushort`, `int`, `uint`, `long`, `ulong`, `float`, `double`, `decimal` |
| `short` | `int`, `long`, `float`, `double`, `decimal` |
| `ushort` | `int`, `uint`, `long`, `ulong`, `float`, `double`, `decimal` |
| `int` | `long`, `float`, `double`, `decimal` |
| `uint` | `long`, `ulong`, `float`, `double`, `decimal` |
| `long` | `float`, `double`, `decimal` |
| `ulong` | `float`, `double`, `decimal` |
| `float` | `double` |
| `char` | `ushort`, `int`, `uint`, `long`, `ulong`, `float`, `double`, `decimal` |

"Mistyped" is therefore defined as *failing rule 4*; the rules are the same ones D2
applies to defaults, so a declaration can never promise a default its own call sites
would reject.
**Rationale.** Heddle is deliberately at the strict end of the precedent spectrum —
Blazor's `[EditorRequired]` and HEEx's `attr` validations only warn; Templ's plain Go
parameters hard-fail — and Heddle's own per-site model checking (`CheckTypes`) already
errors, so props behave like the model parameter they sit beside. The conversion set is
phase 1's function-argument ranking minus the "unique best candidate" machinery (one
declared type, not an overload set).
**Alternatives rejected.** Warn-and-default for missing required props (the
Blazor/HEEx posture — explicitly rejected by the ratified decision); implicit
`ToString()` for string props (silent data mangling); treating `HED5006` as `HED5001`
(the fixes differ: add a declaration vs fix a typo).

### D11 — Slot projection: `@out(expr)` renders the caller body with the value as its model

**Decision.** Roadmap lean ratified — the slot value channel is the **model**. Semantics
and mechanics:

- **Compile, caller side.** When the invoked definition declares `out:: T`, the
  invocation-site body compiles with **model type `T`** (instead of the invocation's
  data type) — `@picker(Menu){{ @(Label) }}` binds `Label` against `MenuOption`. The
  invocation's positional model is still checked against the definition's model type
  exactly as today (`CheckTypes` unchanged).
- **Compile, definition side.** Inside a slot-declaring definition body, every `@out`
  must pass a value (`@out()` → `HED5013`) and must be bodiless (`@out(expr){{…}}` →
  `HED5018`); the expression's static type must be assignable to `T` per D10 rule 4
  minus boxing (identity, widening, lifting, reference assignability) — else `HED5014`
  (a dynamic-typed value is `HED5014`'s "no static type" variant). Conversely, `@out`
  with a value where no slot is declared — including outside definition bodies entirely —
  is `HED5012` (see D13). `@out`'s `InitStart` performs these checks via the D16 seam
  and threads `typeof(string)` as its return type (the projection renders text).
- **Render.** The outer `DefinitionBaseExtension` in slot mode does **not** pre-render
  the caller content; it passes an internal `SlotContent` carrier (the outer extension +
  a copy of the invocation scope) down the existing chained channel, and installs props
  as in D7: `scope.Chain(slotContent).WithProps(props)`. A slot-mode `@out(expr)` reads
  the carrier from `ChainedData` and renders the caller body through the outer
  extension's existing funnel members under
  `invocationScope.Model(value)` — model = the slot value, `ParentModelData` = the
  invocation model, `PropsData` = the *invocation-site* frame (caller content reads the
  enclosing props, not the definition's), renderer and root unchanged. Each `@out(expr)`
  execution is one caller-body render — `@list(Options){{ <li>@out(this)</li> }}`
  renders the body once per option (the roadmap's picker scenario). An empty caller body
  renders nothing.
- **Runtime guard.** A slot-mode `@out(expr)` whose `ChainedData` is not the carrier
  (reachable only through chain composition, e.g. `@a():out(X)` inside the body) throws
  `TemplateProcessingException` — *"'@out' expected the definition's projected content
  on the chained channel; it cannot take a value after a chained call."* — the phase 3
  composition-path precedent.
- **Phase 3 reconciliation.** Phase 3 D11 pinned "a bodiless `@out()` splices the
  already-rendered string and executes nothing"; that remains true for every non-slot
  definition. The slot-mode refinement: `@out(expr)` *executes the caller-content body*,
  but it does so through the **same funnel entry** phase 3 inventoried for that body
  (the outer extension's `GetInnerResult`/`RenderInnerResult`, entry E4) — frame
  provisioning, per-projection fresh frames, and strict body isolation therefore hold
  with zero new entry points. Composition across nesting also falls out of the chain
  threading: a non-slot definition invoked as the first item of a chain inside a
  slot-mode body pre-renders *its* caller content under a scope whose `ChainedData` is
  the outer carrier, so an `@out(this)` written in that nested caller content projects
  the **outer** definition's slot — lexically correct, verified against
  `TemplateChain`/`TemplateItem` dispatch and pinned by the `slot-compose` fixture.

**Rationale.** Every precedent makes the passed value the projected body's primary
binding — Blazor's implicit `context` on `RenderFragment<T>`, Vue's slot props, HEEx's
`:let`, Svelte 5 snippets' declared parameters — and Heddle's implicit model *is* that
binding, so the body "zooms into the value" with no new syntax on the caller side at
all. The chained channel is reused because it is precisely the caller→definition-body
conduit that exists today; only its payload changes in slot mode.
**Alternatives rejected.** Chained-value channel for the slot value (`@out()` then
reading the value off `ChainedData` in the body — collides with the carrier itself and
breaks "the body zooms in"); lazily rendering non-slot caller content too (uniform
laziness — observable perf change for every existing definition, double-render of
side-effect-free content on multiple `@out()` splices; rejected for byte-compat);
compiling the caller body per `@out` site (one body, one compile — the value type is
fixed by the declaration).
**Grounding.** [Blazor templated components](https://learn.microsoft.com/en-us/aspnet/core/blazor/components/templated-components),
[Vue 3 scoped slots](https://vuejs.org/guide/components/slots), [Phoenix.Component
`:let`](https://hexdocs.pm/phoenix_live_view/Phoenix.Component.html),
[Svelte 5 snippets/`{@render}`](https://svelte.dev/docs/svelte/snippet).

### D12 — Compile-context threading: `ActivePropLayout` and `SlotParameterType`

**Decision.** `CompileContext` gains two internal properties, `ActivePropLayout`
(the resolved layout, or `null`) and `SlotParameterType` (`ExType`, or `null`). Rules:

- The child-context copy constructor **copies both** — plain nested bodies (`@list`,
  `@if`, bodied calls) inside a definition body keep the definition's layout and slot
  type, which is what lets `@out(this)` sit inside `@list` and `@(style)` inside `@if`.
- `CreateExtension`'s definition branch **always overwrites both around the
  definition-body compile** (save → set to the *invoked* definition's own layout/slot
  type, `null`s included → `InitializeTemplate(DefinitionParameterTemplate, …)` →
  restore). Setting `null` for prop-less/slot-less definitions is load-bearing: a
  prop-less definition compiled from inside a props definition must **not** see the
  enclosing layout (its body is lexically foreign text).
- The **caller-content compile inherits the current values untouched** — caller content
  is lexically part of the enclosing scope, so its prop reads and `@out(expr)` bind to
  the *enclosing* definition, matching the render-time flow (D7 keeps `PropsData`, D11
  keeps the enclosing carrier) exactly.

**Rationale.** This is the `@model`/`OutputProfile` state pattern (verified copy route,
snapshot-at-creation makes save/restore safe) applied twice; the
compile-time rules are constructed to be the precise static mirror of the runtime data
flow, so "compiles" and "reads the right frame" cannot diverge.
**Alternatives rejected.** Threading through `InitStart` parameters (public interface
change on `IExtension` for internal state); resolution keyed off `ParseContext`
ancestry (definition contexts are isolated/copied — the compile context is the only
spine that actually follows compile order).

### D13 — `@out(X)` stops being accepted-and-ignored: it is now `HED5012`

**Decision.** The roadmap's pre-M1 verification item is closed with source evidence
(assumed state): today `OutExtension` ignores `ModelData`, so `@out(X)` compiles `X`
(which can itself fail member resolution), evaluates it at render, discards it, and
splices the chained value. From this phase on, `@out` with any non-empty parameter
outside a slot-declaring definition body is compile error `HED5012`. This also
supersedes the *runtime* half of phase 1's corpus row P20: `@out(true)` still parses to
`LiteralNode(true)` (the grammar row stands) but now fails compilation with `HED5012`
instead of rendering as an ignored value.
**Rationale.** The roadmap authorizes documenting this as a behavior change; an error
is strictly safer than the alternative of silently *changing* what `@out(X)` does (from
"ignore X" to "project X" — the worst possible migration for the rare template that has
one). The construct never produced output derived from `X`, so affected templates were
already buggy.
**Alternatives rejected.** Keeping ignore semantics in non-slot definitions (permanently
forecloses ever making `@out(X)` meaningful there, and hides typos); a warning
(the value is discarded — that is an authoring error, and this phase's identity is hard
errors).

### D14 — `[NotEncode]` is not revived; phase 2's D12 trigger is closed

**Decision.** The phase 2 D12 revisit assigned to this phase concludes: **typed props do
not revive `[NotEncode]`**, and nothing replaces it. Prop values are ordinary values —
when one is emitted through the unnamed carrier under `OutputProfile.Html` it encodes at
the emitting leaf exactly like any other value (phase 2 D10's uniform rule); trusted
markup is expressed at the *output site* via the existing `@raw(...)`, never at the
declaration site. No `raw`/`trusted` modifier is added to prop declarations. The
attribute type itself remains shipped-and-inert exactly as phase 2 left it (removing a
public type is a break, and the ratified
[2.0 window](../common/cross-cutting-decisions.md#d2--the-single-20-breaking-window)
list does not include it); any future deletion needs its own ratification. This is the
final disposition — no later phase inherits a revisit obligation.
**Rationale.** A declaration-site trust marker is the auditability anti-pattern the
phase 2 grounding warned about: it acts at a distance from the sink, and it would ship
in the same era that introduces safe-by-default output. Security precedence
([standards rule 2](../common/coding-standards.md#precedence-when-principles-conflict))
beats the marginal ergonomics of not writing `@raw(style)`.
**Alternatives rejected.** A per-prop `raw` flag (`style: string raw` — widens the
trusted surface, unauditable at call sites); deleting the attribute now (public API
removal outside a ratified window).

### D15 — `ExpressionMode` interaction: named arguments require `Native`; prop reads do not

**Decision.** Named-argument values are expression-tier constructs: under
`ExpressionMode.MemberPathsOnly` a call carrying named arguments fails with the existing
`HED1014` at the first named argument's position (no new ID — same mode, same message,
same remedy). Prop **reads** in definition bodies are member-tier (bare paths) and work
under every mode, so a prop-declaring library compiled under `MemberPathsOnly` still
renders via all-defaults calls. The `this` expression, being expression-tier, is also
`HED1014` under `MemberPathsOnly`.
**Rationale.** One mode, one ID, one rule (phase 1 D7's simplification carried
forward); prop reads must stay member-tier because resolution — not operators — is all
they need.
**Alternatives rejected.** A dedicated `HED5xxx` for named-args-under-strict-mode
(duplicates `HED1014`'s meaning and fix).

### D16 — The argument-access seam: internal `InitContext.SourceItem`

**Decision.** `InitContext` gains `internal OutputItem SourceItem`, populated by
`InitializeTemplate`'s call sites (which all have the `OutputItem` in hand).
`OutExtension.InitStart` uses it (with `CompileContext.SlotParameterType`) to implement
the D11 checks — the first extension ever able to see its call parameter at compile
time. The seam stays **internal**: phase 2's deferred "marker-interface seam for
paren-argument directives" is resolved as *narrowly as the consumer requires*. Making it
public (for custom extensions, or to give `@profile(html)` a paren form) is re-deferred
with the trigger "a concrete out-of-assembly extension that cannot express its contract
through the body form" ([Deferred items](#deferred-items)); `@profile`'s body form
remains canonical per phase 2 D2.
**Rationale.** Precedence rule 5 — a seam goes public when a second (external) consumer
exists, not because one might; the internal property is a one-line, fully reversible
mechanism.
**Alternatives rejected.** Public `InitContext.CallParameter` now (public API with XML
docs, compat surface, and doc pages for zero known external consumers); compiler-side
name-keyed special-casing of `"out"` (moves an extension's contract into the compiler —
the anti-pattern phase 2 D2 rejected for `@profile`).

### D17 — Default output chains: named arguments work; required props are enforced there too

**Decision.** `-> (…)` compiles through the ordinary `call` rule (verified —
`CreateOutputChain(defOutChain?.chain(), definitionName)` with a name override), so
`<card(style: string = "plain")> -> (Article, style: "wide") {{…}}` parses and binds
with no additional machinery; the D10 rules apply at the default chain's position. In
particular a definition with a *required* prop and a `->` default output that does not
supply it is `HED5002` at the default chain — there is no "defaults only" exemption.
**Rationale.** One binding path for every invocation form (explicit call, chained call,
default chain) — the alternative is a second, weaker checker for the least-visible form.
**Alternatives rejected.** Forbidding named arguments in default chains (gratuitous —
the grammar provides them for free and the semantics are identical).

### D18 — Editor tokens: no new enum values

**Decision.** `HeddleTokenType` is unchanged. Classification (emitted through the
existing `ParseContext.AddToken` gate, active only under `ProvideLanguageFeatures`):
named-argument names and prop/slot names/types → `Id`; the named-argument and
prop-declaration `:` → `Delim`; the slot `::` → `DefType`; `=`, `,`, and the default's
`-` → `Operator`; default literals → `Literal`; `this` → `Literal` (the same class as
the phase 1 keyword literals `true`/`false`/`null`); the props parens →
`OutParamStart`/`OutParamEnd`. Full table in
[grammar.md](grammar.md#editor-token-classification).
**Rationale.** Every new terminal maps naturally onto an existing class; a public enum
addition is a compat commitment that buys phase 6 nothing it cannot derive (prop-name
semantics come from the AST/DTOs, not the flat token class).
**Alternatives rejected.** A `PropName`/`Keyword` value (speculative until phase 6
demonstrates a rendering distinction it cannot compute; appending later is additive
anyway).

### D19 — Multiple/named slots stay deferred, without grammar lock-in

**Decision.** Exactly one slot parameter per definition in this phase (multiple
`out::` → `HED5017`). The deferral does not lock the surface: the declaration grammar
extends additively to named slots as `out <name>:: Type` (a second `ID` between `out`
and `::` — currently a parse error), and the projection side extends as
`@out(<name>: expr)` (named arguments on `@out` — currently `HED5005`-adjacent space,
since `out` is an extension). Both future forms live in today's error space, satisfying
the roadmap's "the deferral must not grammar-lock the single-slot form".
**Rationale.** The single default slot with a value covers the ratified scenarios;
every surveyed engine did eventually grow named typed slots (Blazor multiple
`RenderFragment<T>` parameters, HEEx named slots, Vue named scoped slots, Svelte
multiple snippet props), so the extension path is designed now and built when demanded.
**Alternatives rejected.** Shipping named slots speculatively (YAGNI — doubles the
binder, the carrier, and the diagnostics surface with no ratified scenario); reserving
extra tokens for them (nothing needs reserving — the space is already invalid).

### D20 — Docs deliverable

**Decision.** [language-reference.md](../../language-reference.md): the *Definitions*
section gains two subsections — *Props* (declaration, defaults, required-ness, call-site
named arguments, shadowing and `this.<name>`, inheritance re-defaulting) and
*Parameterized slots* (`out:: Type`, `@out(expr)`, the model-channel rule) — and the
grammar sketch line becomes
`def = < name [( props )] [: base] > [-> chain] {{ body }} [:: Type]`.
`docs/native-expressions.md` (created by phase 1's D19) documents `this` (primary, typing,
dynamic-scope rule) and named-argument value positions.
[built-in-extensions.md](../../built-in-extensions.md): the `out` entry documents the
two modes and the value-without-slot error. [patterns.md](../../patterns.md) gains a
card/picker component recipe (the phase 9 sample's narrative twin).
[custom-extensions.md](../../custom-extensions.md) is untouched (props are
definition-side; extensions gain no named-argument surface — recorded in
[Deferred items](#deferred-items)).
**Rationale.** Matches the docs rule in the
[coding standards](../common/coding-standards.md#api-design-and-compatibility); the
material extends existing pages rather than warranting a new one (phase 1's page-sized
operator corpus is the counter-precedent).
**Alternatives rejected.** A dedicated `components.md` page (the definitions section
already owns the component story; splitting it would fork the narrative).

## Implementation plan

Work items in execution order; each runs the
[canonical loop](../common/testing-standards.md#the-canonical-loop). "Check" is the
completion gate. The fixtures-as-spec discipline from the roadmap's M0 is WI1 — no
implementation lands before the corpus and negative tables exist and fail.

**WI1 — Failing executable spec (fixtures-as-spec).**
Files: `src/Heddle.Tests/PropsParseTests.cs` (the
[parse corpus](grammar.md#parse-corpus-executable-grammar-spec) PP01–PP24 / NP01–NP15 as
`[Theory]` data), `src/Heddle.Tests/PropsBindingTests.cs` (the D10 positive matrix and
negative table B01–B14 from the [testing plan](#testing-plan)),
`src/Heddle.Tests/PropsInheritanceTests.cs`, `src/Heddle.Tests/SlotProjectionTests.cs`,
`src/Heddle.Tests/PropsShadowingTests.cs`, fixtures `props-card.heddle`,
`props-defaults.heddle`, `props-abstract-panel.heddle`, `props-inherit.heddle`,
`slot-picker.heddle`, `slot-compose.heddle` under `src/Heddle.Tests/TestTemplate/`
(goldens captured in WI8). Check: everything compiles; every new test fails for the
right reason (parse errors on the new syntax; missing members).

**WI2 — Grammar change + regen.**
Files: `src/Heddle.Language/HeddleLexer.g4` (`THIS`/`ASSIGN` vocabulary, `CALL_THIS`,
the `DEF` `(` rule, `mode DEF_PROPS`), `src/Heddle.Language/HeddleParser.g4` (5th `call`
alternative, `named_argument`, `THIS` primary, `def_props` rule family) — exact text in
[grammar.md](grammar.md); one dedicated regen commit of `src/Heddle.Language/generated/`
plus the `generate_js.cmd` output, per the
[regen procedure](grammar.md#regeneration-procedure). Check: parse corpus green;
existing suite green byte-identical; `TemplateParseBenchmarks` (phase 1) within gate;
the editor-mode `LL_EXACT_AMBIG_DETECTION` sweep over the full fixture corpus reports no
new ambiguity ([grammar.md](grammar.md#coexistence-and-ambiguity-analysis)).

**WI3 — Parse DTOs and walker wiring.**
Files: `src/Heddle/Language/PropDeclaration.cs`, `src/Heddle/Language/NamedArgument.cs`
(new public DTOs), `src/Heddle/Language/Expressions/ThisNode.cs` (new AST node),
`src/Heddle/Language/DefinitionItem.cs` (`PropDeclarations`, `SlotTypeName`; copy ctor
and `OverrideWith` updated), `src/Heddle/Language/CallParameter.cs` (`PropArguments`),
`src/Heddle/Language/ParseContext.cs` (`CreateDefinition` parses `def_props()` via one
shared helper — both branches; `CreateItem` populates `PropArguments` and classifies the
alt-5 positional per D4; editor tokens per D18),
`src/Heddle/Language/Expressions/ExpressionAstBuilder.cs` (`ThisNode`; literal decoding
exposed as the internal static helper D2 reuses; header parse-time checks `HED5015`/
`HED5016`/`HED5017` and duplicate-in-header `HED5007`). Check: corpus AST/DTO-shape
assertions green; header negatives report positioned IDs.

**WI4 — `Scope.PropsData` + runtime parameters.**
Files: `src/Heddle/Data/Scope.cs` (field, ctor parameter, copy in all transforms,
internal `WithProps`), `src/Heddle/Runtime/Parameters/PropsSlotParameter.cs` (new
internal: direct slot read, optional null-safe hop chain via
`ModelParameter.BuildNullSafePropertyChain`),
`src/Heddle/Runtime/Expressions/NativeExpressionCompiler.cs` (props-aware delegate
shape: a fourth `object[]` parameter bound to `scope.PropsData`, emitted only when the
tree contains a prop root; `ThisNode` compilation per D5),
`src/Heddle/Runtime/Parameters/CompiledParameter.cs` sibling
`src/Heddle/Runtime/Parameters/PropsCompiledParameter.cs` (new internal —
`Func<object, object, object, object[], object>` invoked with
`(ModelData, ChainedData, RootData, PropsData)`). Check: white-box unit tests over the
new parameters (via `InternalsVisibleTo`); existing suite green (field is inert while
`null`).

**WI5 — Layout resolution + call-site binding + diagnostics (the L-sized item).**
Files: `src/Heddle/Runtime/Expressions/PropLayout.cs` (internal `PropLayout`/`PropSlot`
+ the resolver: inheritance flattening, re-declaration checks `HED5008`, type
resolution `HED5010`, default conversion `HED5009`; cached on `CompileContext`),
`src/Heddle/Runtime/PropsBinder.cs` (internal: frozen prototype, dynamic slot plan,
`Bind(in Scope)`), `src/Heddle/Runtime/HeddleCompiler.cs` (`CompileItem`: named-argument
target checks `HED5005`/`HED5006`, `HED1014` under `MemberPathsOnly`; `CreateExtension`
definition branch: layout resolution, argument compilation in the caller's scope,
D10 checks `HED5001`–`HED5004`/`HED5002`, binder attachment to the outer extension,
D12 save/set/restore), `src/Heddle/Runtime/CompileContext.cs` (`ActivePropLayout`,
`SlotParameterType`, copy-ctor lines), `src/Heddle/Core/DefinitionBaseExtension.cs`
(`PropsBinder` field; props installation in `ProcessData`/`RenderData`),
`src/Heddle/Data/HeddleDiagnosticIds.cs` (all IDs of this phase). Check:
`PropsBindingTests` fully green — the roadmap validation table rows (all-defaults,
root-ref value, typo, mistype, missing required) with exact IDs and positions;
`PropsInheritanceTests` green.

**WI6 — Body prop resolution.**
Files: `src/Heddle/Runtime/Expressions/MemberPathResolver.cs` (prop-first hook on the
first segment, layout from `CompileContext.ActivePropLayout`, shadowing detection),
`src/Heddle/Runtime/HeddleCompiler.cs` (`CompileModelAccessor` consumes the hook →
`PropsSlotParameter`), `src/Heddle/Runtime/Expressions/NativeExpressionCompiler.cs`
(`PathNode` root hook; `HED5011` emission shared with the member tier). Check:
`PropsShadowingTests` green (prop wins + positioned warning + `this.<name>` escape);
prop reads type-thread into `@if`/`@list` fixtures; dynamic-model definition reads props
statically.

**WI7 — Slots.**
Files: `src/Heddle/Core/InitContext.cs` (internal `SourceItem`),
`src/Heddle/Runtime/HeddleCompiler.cs` (`InitializeTemplate` call sites populate
`SourceItem`; caller-body compile under the declared slot type),
`src/Heddle/Core/SlotContent.cs` (new internal carrier),
`src/Heddle/Core/DefinitionBaseExtension.cs` (slot mode: carrier instead of pre-render),
`src/Heddle/Extensions/OutExtension.cs` (slot mode: D11 checks `HED5012`/`HED5013`/
`HED5014`/`HED5018` in `InitStart`; carrier render path + runtime guard). Check:
`SlotProjectionTests` green — picker renders per-option with option as model; caller
body typed against `MenuOption`; composition fixture (`slot-compose`) green; runtime
guard throws on the chained-composition path; `@out` byte-identical in every non-slot
fixture.

**WI8 — Goldens, concurrency, benchmarks, combined gate.**
Files: goldens `generated-props-card.html`, `generated-props-defaults.html`,
`generated-props-abstract-panel.html`, `generated-props-inherit.html`,
`generated-slot-picker.html`, `generated-slot-compose.html`;
`src/Heddle.Tests/PropsConcurrencyTests.cs` (parallel renders of one compiled template —
shared frozen array, opposite dynamic prop values — proving isolation);
`src/Heddle.Performance/PropsRenderBenchmarks.cs` (new, `[MemoryDiagnoser]`:
`RenderDefinitionNoProps` allocation parity vs the pre-phase baseline,
`RenderAllConstantProps10K` zero-extra-allocation proof, `RenderDynamicProps10K` the
documented per-invocation cost, `RenderParameterizedSlot10K`). Check: the full
[regression gate](../common/testing-standards.md#regression-gates) in one combined run —
build all TFMs; full suite byte-identical; the one regen commit diff-reviewed;
`TextRenderBenchmarks` allocations not increased and mean within reported error (the
`Scope` growth guard); zero-Roslyn proof for all props/slot fixtures under
`AllowCSharp = false` (roadmap success criterion 1); roadmap criteria 1–6 asserted
together.

**WI9 — Published docs (D20).**
Files: `docs/language-reference.md`, `docs/native-expressions.md`,
`docs/built-in-extensions.md`, `docs/patterns.md`. Check:
`cd docs && npm run docs:build` passes (uppercase-drive cwd on Windows); doc examples
compile as tests where the pages promise behavior (the picker and card snippets run in
`SlotProjectionTests`/`PropsBindingTests`).

## Public API contract

All additions are additive
([API design rules](../common/coding-standards.md#api-design-and-compatibility)); XML
docs required on every member. Thread-safety notes inline. No `TemplateOptions` change —
props add no options, so identity, the copy ctor, and phase 2's completeness test are
untouched.

```csharp
namespace Heddle.Language
{
    public class CallParameter
    {
        /// <summary>Named prop arguments of this call ("name: expr"), in source order;
        /// null when the call passes none. Orthogonal to the positional parameter
        /// shapes — IsModelTypeParameter is unaffected.</summary>
        public IReadOnlyList<NamedArgument> PropArguments { get; set; }
    }

    /// <summary>One named argument at a call site. Immutable after parse.</summary>
    public sealed class NamedArgument
    {
        /// <summary>The prop name (ordinal, case-sensitive).</summary>
        public string Name { get; }
        /// <summary>The value expression (native tier), evaluated in the caller's context.</summary>
        public Heddle.Language.Expressions.ExprNode Value { get; }
        /// <summary>Absolute template position of the argument (OutputItem.Position convention).</summary>
        public BlockPosition Position { get; }
    }

    public class DefinitionItem
    {
        /// <summary>Declared props, inheritance not flattened (base props live on
        /// BaseDefinition). Empty for a header without a prop list.</summary>
        public IReadOnlyList<PropDeclaration> PropDeclarations { get; }

        /// <summary>The declared slot parameter type name ("out:: Type"), or null when
        /// the definition does not parameterize its slot.</summary>
        public string SlotTypeName { get; }
    }

    /// <summary>One prop declaration from a definition header. Immutable after parse.</summary>
    public sealed class PropDeclaration
    {
        public string Name { get; }
        /// <summary>The declared type name, resolved at compile time like DefinitionItem.ModelType.</summary>
        public string TypeName { get; }
        /// <summary>True when the declaration carries "= literal"; false means required.</summary>
        public bool HasDefault { get; }
        /// <summary>The decoded default literal (pre-conversion CLR value, null for the
        /// null literal); meaningful only when HasDefault.</summary>
        public object DefaultValue { get; }
        /// <summary>Declaration position (DefinitionItem.Position convention — offset-relative
        /// parse coordinates).</summary>
        public BlockPosition Position { get; }
    }
}

namespace Heddle.Language.Expressions
{
    /// <summary>The 'this' keyword: the current scope's model, statically typed as the
    /// current scope type. Immutable, like every ExprNode.</summary>
    public sealed class ThisNode : ExprNode { }
}

namespace Heddle.Data
{
    public static class HeddleDiagnosticIds
    {
        public const string UnknownProp                = "HED5001";
        public const string MissingRequiredProp        = "HED5002";
        public const string PropTypeMismatch           = "HED5003";
        public const string DuplicatePropArgument      = "HED5004";
        public const string NamedArgumentsNotSupported = "HED5005";
        public const string DefinitionHasNoProps       = "HED5006";
        public const string DuplicatePropDeclaration   = "HED5007";
        public const string PropRedeclarationMismatch  = "HED5008";
        public const string PropDefaultNotConvertible  = "HED5009";
        public const string UnresolvedPropType         = "HED5010";
        public const string PropShadowsModelMember     = "HED5011";
        public const string SlotValueWithoutSlot       = "HED5012";
        public const string SlotValueRequired          = "HED5013";
        public const string SlotValueTypeMismatch      = "HED5014";
        public const string ReservedPropName           = "HED5015";
        public const string InvalidSlotDeclaration     = "HED5016";
        public const string MultipleSlotDeclarations   = "HED5017";
        public const string SlotValueWithBody          = "HED5018";
    }
}
```

### Internal API shapes (not public API; normative for the implementer)

| Member | Shape | Notes |
| --- | --- | --- |
| `Scope.PropsData` | `internal readonly object[] PropsData;` + trailing optional ctor parameter `object[] props = null` | The D7 carriage. `null` for every prop-less execution; copied unchanged by **all** transforms (`Parent()` ×2, `Chain`, `Model` ×2, `RenderProxy`, phase 3's `WithLocals`); `Scope.Null` keeps it `null`. Never exposed publicly. |
| `Scope.WithProps` | `internal Scope WithProps(object[] props)` | Returns a copy with only `PropsData` replaced (all other fields carried over). Called **only** by `DefinitionBaseExtension`'s D7/D11 installation (`scope.Chain(carrier).WithProps(props)`). `[MethodImpl(AggressiveInlining)]`, mirroring the sibling transforms and phase 3's `WithLocals`. |
| `PropSlot` | `internal sealed class PropSlot` in `src/Heddle/Runtime/Expressions/PropLayout.cs`; members `internal string Name; internal ExType Type; internal bool HasDefault; internal object DefaultBoxed; internal int Index; internal BlockPosition Position;` | One resolved layout slot. `DefaultBoxed` is the D2-converted value boxed once at layout resolution (`null` both for the `null`-literal default and for required props — `HasDefault` disambiguates). `Position` is the declaration position (`HED5008`/`HED5009`/`HED5010` anchor). |
| `PropLayout` | `internal sealed class PropLayout`; members `internal IReadOnlyList<PropSlot> Slots; internal int Count; internal bool TryGet(string name, out PropSlot slot);` static `internal static PropLayout Resolve(DefinitionItem definition, CompileScope compileScope)` | The D6 flattened, index-stable table. `TryGet` is backed by a `Dictionary<string, PropSlot>` (`StringComparer.Ordinal`) built once at resolution and used at **compile time only** — render never sees a name (constraint 5). `Resolve` walks the base chain outermost-first, applies re-declaration rules (`HED5008`), resolves types (`HED5010`), converts defaults (`HED5009`). |
| `CompileContext.ResolvedPropLayouts` | `internal Dictionary<DefinitionItem, PropLayout> ResolvedPropLayouts { get; }` | The once-per-definition-per-compile cache (D6). Shared through the private copy ctor exactly like `CompiledItems` (same instance, not a copy — one compile, one cache). |
| `PropsBinder` | `internal sealed class PropsBinder` in `src/Heddle/Runtime/PropsBinder.cs`; ctor `internal PropsBinder(object[] frozenPrototype, (int Index, IRuntimeParameter Parameter)[] dynamicPlan)`; fields `private readonly object[] _frozen; private readonly (int Index, IRuntimeParameter Parameter)[] _plan;`; members `internal object[] Bind(in Scope scope)`, `internal bool IsAllConstant => _plan.Length == 0;` | `Bind`: plan empty → return `_frozen` (shared, never written — D7's proof); else `var props = (object[]) _frozen.Clone();` then `props[e.Index] = e.Parameter.GetParameter(callerView)` per plan entry with `callerView = scope.Parent()` (D8), return `props`. Write-once at compile; unsynchronized by design. |
| `PropsSlotParameter` | `internal sealed class PropsSlotParameter : IRuntimeParameter` in `src/Heddle/Runtime/Parameters/PropsSlotParameter.cs`; ctor `internal PropsSlotParameter(int index, Func<object, object> hops = null)`; `GetParameter(in Scope scope)` → `_hops == null ? scope.PropsData[_index] : _hops(scope.PropsData[_index])`; empty `Dispose()` | The bare and multi-hop prop read (D9). `_hops` is the phase 1 null-safe property chain (`ModelParameter.BuildNullSafePropertyChain`) rooted at `Convert(slot, propType)`; `null` for single-segment reads — a direct array load, no delegate. `[MethodImpl(AggressiveInlining)]` on `GetParameter`, like `CompiledParameter`. |
| `PropsCompiledParameter` | `internal sealed class PropsCompiledParameter : IRuntimeParameter` in `src/Heddle/Runtime/Parameters/PropsCompiledParameter.cs`; `public Func<object, object, object, object[], object> ParameterImplementation { get; set; }`; `GetParameter(in Scope scope)` → `ParameterImplementation?.Invoke(scope.ModelData, scope.ChainedData, scope.RootData, scope.PropsData)` | The props-aware sibling of `CompiledParameter` (whose 3-arg shape stays untouched). Emitted **only** when the expression tree contains a prop root (WI4) — prop-free expressions keep today's delegate shape bit-identically. |
| `SlotContent` | `internal sealed class SlotContent` in `src/Heddle/Core/SlotContent.cs`; members `internal readonly DefinitionBaseExtension Outer; internal readonly Scope InvocationScope;` ctor `internal SlotContent(DefinitionBaseExtension outer, in Scope invocationScope)` | The D11 carrier. `Outer` is the extension whose funnel members (`GetInnerResult`/`RenderInnerResult` — phase 3 entry E4) render the caller body; `InvocationScope` is the invocation-site scope captured at `ProcessData`/`RenderData` entry (its `PropsData` is the invocation-site frame; `@out(expr)` renders under `InvocationScope.Model(value)`). Created once per slot-mode invocation, confined to that render lineage, never cached. |
| `DefinitionBaseExtension.PropsBinder` / `.SlotMode` | `internal PropsBinder PropsBinder; internal bool SlotMode;` | Written once during compile (`CreateExtension` definition branch), read at render — the `_innerResult` write-once-at-`InitStart` pattern. `PropsBinder == null` ⇒ the prop-less fast path (no new render code executes). |
| `OutExtension` slot-mode flag | `private bool _slotMode;` set in `InitStart` from `CompileContext.SlotParameterType != null` | Selects the D11 carrier path in `ProcessData`/`RenderData`; the non-slot paths keep their current text byte-for-byte (assumed-state oddity preserved). |
| `CompileContext.ActivePropLayout` / `.SlotParameterType` | `internal PropLayout ActivePropLayout { get; set; }` / `internal ExType SlotParameterType { get; set; }` | The D12 threading state. Copied by the private copy ctor (line 73 route, the `ScopeType`/`OutputProfile` precedent); save/set/restore around every definition-body compile in `CreateExtension`. |
| `InitContext.SourceItem` | `internal OutputItem SourceItem;` | The D16 seam; populated by `InitializeTemplate`'s call sites (all have the `OutputItem` in hand). `InitContext` is a struct — the field is additive and invisible publicly. |
| `ExpressionAstBuilder.DecodeLiteral` | `internal static object DecodeLiteral(IToken literalToken, bool negate = false)` | The D2 DRY extraction of phase 1's literal decoding: returns the boxed CLR value per the [literal-typing table](../phase-1-native-expressions/operator-semantics.md); `negate` is used only for `def_literal`'s `OP_MINUS INT_LIT/REAL_LIT` form. Serves CALL-mode expression building and the `def_props` walker from one routine. |

Thread-safety summary: all parse DTOs and AST nodes are immutable after parse; the
frozen prototype array is shared across renders and threads and is **never written after
compile** (no engine write path to `PropsData` exists — normative invariant); dynamic
binds allocate a fresh array per invocation inside one render lineage; `SlotContent` is
created per invocation and confined to it; the only compile-written instance state
(`DefinitionBaseExtension.PropsBinder`/`SlotMode`, `OutExtension`'s flag) follows the
existing `_innerResult` write-once-at-`InitStart` pattern. The parallel-render test
(WI8) is the standards-mandated proof.

## Diagnostics

Claimed from the `HED5xxx` block
([registry](../common/cross-cutting-decisions.md#d1--stable-diagnostic-ids-hedxxxx));
prior claims verified against the merged specs: `HED0001`–`HED0004`, `HED1001`–`HED1017`,
`HED2001`–`HED2003`, `HED3001`–`HED3004`, `HED4001`–`HED4002` are taken; this phase
claims `HED5001`–`HED5018` and touches **no** pre-existing diagnostic message (no new
`HED0xxx`). `<…>` are message parameters. Positions: *arg* = the named argument's
absolute position (`NamedArgument.Position`); *call* = the call item
(`OutputItem.Position`); *decl* = the declaration
(`PropDeclaration.Position`/definition-header context); *item* = the `@out` item.

| ID | Severity | Message text | Trigger / position |
| --- | --- | --- | --- |
| HED5001 | Error | `Unknown prop '<name>' on definition '<def>'. Declared props: <list>.` | Named argument not in the layout (D10 rule 3). Position: arg. |
| HED5002 | Error | `Missing required prop '<name>' (<type>) on the call to definition '<def>'. Pass '<name>: …' or declare a default.` | A slot with no default is unbound after binding (D10 rule 5) — explicit calls, chained calls, and `->` default chains (D17) alike. One error per missing prop. Position: call (for `->`, the default chain). |
| HED5003 | Error | `Prop '<name>' of definition '<def>' expects <propType>, but the argument type is <argType>.` | Argument fails the D10 rule 4 conversion set. Position: arg. |
| HED5004 | Error | `Prop '<name>' is passed more than once.` | Same name twice in one call. Position: the second arg. |
| HED5005 | Error | `'<name>' is not a definition — named arguments can only be passed to definitions that declare props.` | Named arguments on an extension, registered-function, or unknown-name call. Position: the first arg. |
| HED5006 | Error | `Definition '<def>' declares no props.` `Fix`: `Add a prop list to the definition header: '<<def>(name: type)>'.` | Named arguments on a definition with an empty layout. Position: the first arg. |
| HED5007 | Error | `Prop '<name>' is declared more than once on definition '<def>'.` | Duplicate name within one header's prop list. Position: the second decl. |
| HED5008 | Error | `Prop '<name>' is re-declared with type <newType>, which is not assignable to the inherited type <baseType> (from '<base>').` | Child re-declaration fails the `IsType` assignability rule (D6). Position: decl. |
| HED5009 | Error | `The default value for prop '<name>' (<literalType>) is not convertible to <propType>.` | Default literal fails the D10 rule 4 set (D2). Position: decl. |
| HED5010 | Error | `Cannot resolve type '<typeName>' for prop '<name>' of definition '<def>'.` (slot form: `… for the slot parameter of definition '<def>'.`) | `ReflectionHelper.ResolveType` returns null/throws for a prop or slot type. Position: decl. |
| HED5011 | Warning | `Prop '<name>' hides the model member '<modelType>.<name>' — '<name>' reads the prop.` `Fix`: `Rename the prop, or read the member explicitly with 'this.<name>' in an expression.` | Prop-layout hit whose current scope type also exposes a readable, visible property of the same name (D9). Once per read site per body compile. Position: the read site. |
| HED5012 | Error | `'@out' with a value requires the enclosing definition to declare a slot parameter: '<name(out:: Type)>'.` (outside any definition body: `'@out' with a value is only valid inside a definition body.`) | `@out` with a non-empty parameter while `SlotParameterType` is null (D11, D13) — including the formerly accepted-and-ignored `@out(X)`. Position: item. |
| HED5013 | Error | `Definition '<def>' declares a slot parameter (out:: <Type>) — every '@out' in its body must pass a value: '@out(expr)'.` | Empty-parameter `@out` in a slot-declaring definition body. Position: item. |
| HED5014 | Error | `The slot value type <argType> is not assignable to the declared slot parameter type <slotType>.` (dynamic form: `The slot value must have a static type, but it is 'dynamic' here.`) | Slot value fails identity/widening/lifting/reference-assignability, or is dynamic-typed (D11). Position: item. |
| HED5015 | Error | `'<name>' is reserved and cannot be used as a prop name.` `Fix` (for `out`): `Declare the slot parameter with 'out:: Type'.` | Prop declared with name `out` or `this` (D3). Position: decl. |
| HED5016 | Error | `Expected 'out' before '::' — the slot parameter is declared as 'out:: Type'.` | `def_slot` whose identifier is not `out` (ordinal). Position: decl. |
| HED5017 | Error | `Definition '<def>' declares more than one slot parameter — only one 'out::' declaration is allowed.` | Second `def_slot` in one header (D19). Position: the second decl. |
| HED5018 | Error | `'@out' cannot take both a slot value and a body.` | Slot-mode `@out(expr)` carrying a `{{ … }}` body (D11). Position: item. |

Reused, not claimed: `HED1014` for named arguments (and `this`) under
`ExpressionMode.MemberPathsOnly` (D15); `HED1004` for dynamic path operands inside
argument expressions; `HED0003` for every construct the grammar rejects (see the
[negative corpus](grammar.md#parse-corpus-executable-grammar-spec)).

Runtime error (no ID — render-time, composition-only path):
`TemplateProcessingException` — `'@out' expected the definition's projected content on
the chained channel; it cannot take a value after a chained call.` (D11's guard).

## Testing plan

Instantiates the [testing standards](../common/testing-standards.md); suite homes,
fixture conventions (`props-`/`slot-` stems, sibling goldens, line-ending
normalization), and gates apply as written there.

**TDD verdict (carried from the roadmap, unweakened).** Yes — institutionalized by the
milestone structure: WI1 is the roadmap's M0 **fixtures-as-spec** gate (the corpus and
negative tables exist and fail before any grammar or binder code), the ANTLR ambiguity
verification runs at WI2 exit against the *existing* corpus before semantics land, and
diagnostics are error-first — every row of the negative tables is a failing test before
the binder learns the rule. A fixture change after WI1 reopens the spec (this document),
never a local edit.

**Binding matrix** (executable spec in `PropsBindingTests`; the definition is the D1
`card` unless stated):

| # | Call / setup | Expected |
| --- | --- | --- |
| B01 | `@card(Article, style: "wide", compact: true)` | props honored; body sees `Article` as model (roadmap scenario 1) |
| B02 | `@card(Article)` | `style = "plain"`, `compact = false`; binder returns the **frozen array instance** (asserted via internals) |
| B03 | `@card(Article, style: ::Site.DefaultCardStyle)` | value from root context |
| B04 | `@card(Article, stlye: "x")` | HED5001 at the argument, message lists `style, compact` |
| B05 | `@card(Article, compact: "yes")` | HED5003 (`string` → `bool`) |
| B06 | `<hero(title: string)>` + `@hero(Article)` | HED5002 at the call |
| B07 | `@card(Article, style: "a", style: "b")` | HED5004 at the second `style` |
| B08 | `@list(Items, sep: ", ")` | HED5005 (extension target) |
| B09 | `<plain>` + `@plain(X, a: 1)` | HED5006 |
| B10 | `<pad(width: int)>` + `@pad(X, width: Count)` where `Count : long` | HED5003 (no narrowing) — and `width: B` where `B : byte` binds (widening) |
| B11 | `<opt(tag: string = null)>` + `@opt(X)` | binds; `null` default legal for a reference type |
| B12 | `<n(x: double = 1)>` | int literal widens into the frozen default |
| B13 | named args under `MemberPathsOnly` | HED1014 at the first argument (D15) |
| B14 | `<card(style: string)> -> (Article)` with `style` required | HED5002 at the default chain (D17) |

**Diagnostic coverage map.** Every ID of this phase has at least one positioned
negative test (the standards' rule: positioned diagnostics, never bare "compilation
failed"; sandbox-negative rows additionally prove the construct never executes):

| ID | Triggering construct(s) in the suite | Owning suite |
| --- | --- | --- |
| HED5001 | B04 — `@card(Article, stlye: "x")`, message lists `style, compact` | `PropsBindingTests` |
| HED5002 | B06 — all-positional call with a required prop; B14 — `->` default chain (D17) | `PropsBindingTests` |
| HED5003 | B05 — `compact: "yes"` (`string` → `bool`); B10 — `width: Count` (`long` → `int`, no narrowing) | `PropsBindingTests` |
| HED5004 | B07 — `style:` passed twice, position at the second occurrence | `PropsBindingTests` |
| HED5005 | B08 — `@list(Items, sep: ", ")` (extension target); a registered-function target row | `PropsBindingTests` |
| HED5006 | B09 — `<plain>` + `@plain(X, a: 1)` (definition, empty layout) | `PropsBindingTests` |
| HED5007 | `<card(a: int, a: int)>` — duplicate name in one header (grammar.md corpus-boundary list) | `PropsInheritanceTests` |
| HED5008 | `<fancy(style: int):card>` — re-type not assignable to inherited `string` (D6) | `PropsInheritanceTests` |
| HED5009 | `<pad(width: int = "x")>` — parse-legal literal, D2-inconvertible default | `PropsInheritanceTests` |
| HED5010 | `<x(a: NoSuch.Type)>` and `<x(out:: NoSuch.Type)>` — both message forms (prop and slot) | `PropsInheritanceTests` |
| HED5011 | Prop named like a model member — warning position, `Fix` text, prop-wins output | `PropsShadowingTests` |
| HED5012 | Legacy `@out(X)`/`@out(true)` in a slot-less definition body; `@out(X)` outside any definition body — both message forms | `SlotProjectionTests` |
| HED5013 | `@out()` inside `<picker(out:: MenuOption)>`'s body | `SlotProjectionTests` |
| HED5014 | `@out(Count)` (`int` not assignable to `MenuOption`); the dynamic-value form in a `:: dynamic` definition | `SlotProjectionTests` |
| HED5015 | `<card(out: Option)>` (with the `out::` `Fix` text) and `<card(this: int)>` | `PropsInheritanceTests` |
| HED5016 | `<card(foo:: Option)>` | `PropsInheritanceTests` |
| HED5017 | `<x(out:: A, out:: B)>` — position at the second declaration | `PropsInheritanceTests` |
| HED5018 | `@out(expr){{ … }}` inside a slot-declaring body | `SlotProjectionTests` |

**Named test assets** (all in [src/Heddle.Tests](../../../src/Heddle.Tests) unless
noted):

| Asset | Content |
| --- | --- |
| `PropsParseTests` | The [parse corpus](grammar.md#parse-corpus-executable-grammar-spec) (PP01–PP24, NP01–NP15) as `[Theory]` data: DTO shapes (`PropDeclarations`, `SlotTypeName`, `PropArguments`), positional classification rows (path → member shape, `this` → empty, expr → native), editor-token rows under `ProvideLanguageFeatures`. |
| `PropsBindingTests` | B01–B14 plus the frozen-array identity assertions (all-constant call sites share one instance across renders; dynamic sites allocate) and the zero-Roslyn proof for every props fixture (`CSharpContext.Methods.Count == 0`, `CompiledAssembly == null`, `AllowCSharp = false`). |
| `PropsInheritanceTests` | The D6 matrix: `<fancy(tone: string = "info"):card>` inherits + adds (roadmap criterion 4); re-default keeps slot index (internals); `style: int` re-type → HED5008; full override `<card:card>` with props; duplicate-in-header HED5007; HED5009/HED5010 declaration negatives; HED5015/HED5016/HED5017 header negatives. |
| `PropsShadowingTests` | Prop wins + HED5011 position + `Fix` text; `this.<name>` reads the member; `::`-rooted path skips props; `:: dynamic` definition reads props statically (no binder involvement — asserted by rendering against `ExpandoObject` lacking the prop name). |
| `SlotProjectionTests` | Picker renders once per option, option as model (roadmap scenario); caller body compiled against `MenuOption` (member typo in the body → positioned HED0001); `@out(this)` under `@list`; HED5012 (both message forms, incl. legacy `@out(X)`/`@out(true)`), HED5013, HED5014 (both forms), HED5018; the composition fixture (`slot-compose` — nested non-slot definition's caller content projecting the outer slot); the runtime `TemplateProcessingException` guard; encoding row — under `OutputProfile.Html` a projected body's leaf output encodes exactly once (phase 2 container rule). |
| `PropsConcurrencyTests` | Parallel renders of one compiled template: shared frozen array + opposite dynamic prop values per thread → outputs strictly per-thread (the standards' concurrency rule; also the frozen-array immutability proof). |
| Fixtures/goldens | The six sibling pairs under `src/Heddle.Tests/TestTemplate/` — content shape and expected output per fixture in the table below. |
| `PropsRenderBenchmarks` ([src/Heddle.Performance](../../../src/Heddle.Performance)) | `[MemoryDiagnoser]`: `RenderDefinitionNoProps` (allocation parity with the pre-phase baseline — criterion 5), `RenderAllConstantProps10K` (zero extra allocations vs no-props), `RenderDynamicProps10K` (the documented cost — D7's byte arithmetic), `RenderParameterizedSlot10K`. |

**Fixtures and goldens** (all under
[src/Heddle.Tests/TestTemplate](../../../src/Heddle.Tests/TestTemplate); goldens
captured in WI8 and byte-frozen from then on):

| Fixture → golden | Content shape | Expected output (what the golden asserts) |
| --- | --- | --- |
| `props-card.heddle` → `generated-props-card.html` | The D1 `card` definition (`style: string = "plain"`, `compact: bool = false`, `:: Article`, body with `@out()` splice) called three ways: `@card(Article, style: "wide", compact: true)` with caller content, `@card(Article)` all-defaults, `@card(Article, style: ::Site.DefaultCardStyle)` root-ref value. Compiled with `AllowCSharp = false`. | Three `<article>` blocks: `class="card wide"` with the summary suppressed (compact) and the caller content spliced; `class="card plain"` with summary; `class="card <root value>"`. Roadmap success criterion 1 and validation rows 1–3 in one golden. |
| `props-defaults.heddle` → `generated-props-defaults.html` | The defaults matrix: `<n(x: double = 1)>` (widened `int` default, B12), `<opt(tag: string = null)>` (B11's `null` default), `<flag(active: bool = true, label: string)>` called passing only `label:`, `<pad(width: int = -4)>` (signed default, PP20), `<card()>` empty prop list (PP22). | Every unpassed slot renders its compile-time-converted default; the `null`-default prop renders empty; the signed and widened defaults render their converted values. Pins D2 end-to-end (declaration → frozen prototype → body read). |
| `props-abstract-panel.heddle` → `generated-props-abstract-panel.html` | One untyped `<panel(style: string)>` (no `:: Type`) whose body reads both `style` and a model member; invoked from a `Blog`-typed scope and an `Article`-typed scope with different `style:` arguments. | Both renders correct with per-site member typing; the test additionally asserts via internals that both call sites resolved the **same** `PropLayout` instance while the body monomorphised per site (roadmap success criterion 3, the D6 two-site scenario). |
| `props-inherit.heddle` → `generated-props-inherit.html` | The D6 worked pair — base `card` plus `<fancy(tone: string = "info", style: string = "wide"):card>` — called as `@fancy(Article)` and `@fancy(Article, style: "narrow", tone: "loud")`; plus the D6 full-override `<card:card>` re-default layer. | Inherited default (`compact = false`), re-declared default (`style = "wide"`), per-call overrides, and the override-layer rendering — criterion 4's positive half (the `style: int` negative lives in `PropsInheritanceTests`, not the golden). |
| `slot-picker.heddle` → `generated-slot-picker.html` | The D1 `picker`: `<picker(out:: MenuOption)>{{ <ul>@list(Options){{ <li>@out(this)</li> }}</ul> }} :: Menu`, invoked as `@picker(Menu){{ <a href="/go?id=@(Id)">@(Label)</a> }}`. | One `<li><a …>` per option with `Id`/`Label` bound from `MenuOption` — the roadmap's picker scenario: caller body renders once per option, option as model, inside the definition's `<ul>` wrapper. |
| `slot-compose.heddle` → `generated-slot-compose.html` | D11's composition pin: a slot-declaring outer definition whose body invokes a **non-slot** definition, passing caller content that itself contains `@out(this)`. (The chained-composition line that trips the runtime guard is a separate non-golden negative test.) | The nested caller content projects the **outer** definition's slot value as its model — lexical correctness of D11's last bullet, verified against `TemplateChain`/`TemplateItem` dispatch. |

**Regression gate** (one combined run, per the standards): build all TFMs
(`netstandard2.0;net6.0;net8.0;net10.0`, tests add `net48` on Windows); full suite; all
pre-existing goldens byte-identical (criterion 6); the **one dedicated regen commit**
diff-reviewed (the phases-1-and-5 rule); `TemplateParseBenchmarks` and
`TextRenderBenchmarks` allocations not increased, means within reported error (the ALL(\*)
prediction guard and the `Scope`-growth guard); `PropsRenderBenchmarks` recorded as the
phase baseline. Roadmap success criteria: 1 → `props-card` golden + zero-Roslyn proof;
2 → B04/B06; 3 → `props-abstract-panel`; 4 → `PropsInheritanceTests`; 5 →
`RenderDefinitionNoProps` + `TextRenderBenchmarks` parity; 6 → the untouched-suite gate.

**Deferred phase 9 gallery item** (recorded per the standards):
`samples/component-props-slots` — the card/layout/picker component library on typed
props and parameterized slots, golden-asserted in CI; doubles as the component-story
showcase the assessment said Heddle lacked. Owned by this phase, delivered when the
phase 9 harness exists.

## Back-compat and migration

Everything lands in previously invalid syntax space except one deliberate,
roadmap-sanctioned behavior change (`@out(X)`, D13). Nothing from this phase enters the
[2.0 window](../common/cross-cutting-decisions.md#d2--the-single-20-breaking-window).

| Surface | Behavior | Proof / note |
| --- | --- | --- |
| Prop-less definitions and calls | Byte-identical: the grammar additions are last-ordered/new-mode ([grammar.md coexistence](grammar.md#coexistence-and-ambiguity-analysis)); at render, a null `PropsBinder` executes no new code and `PropsData` stays null | Full suite + goldens; `RenderDefinitionNoProps` allocation parity |
| Templates using `(`, `=`, `,`, literals in definition headers | Were token errors; now parse (error → works) | Corpus PP15–PP23; release note |
| `@out(X)` / `@out(true)` (accepted-and-ignored today; P20's runtime half) | Now positioned HED5012 | D13; release note names the migration (delete the argument, or declare `out:: T`) |
| `this`, named arguments in call parens | Were parse/lex errors; now expression surface (error → works) | Corpus PP01–PP14 |
| `@out(a():b())` and every existing chain/`:`/`::` form | Unchanged — alternative ordering and mode isolation | PP11 + the coexistence table |
| `Scope` layout (+1 reference field) | Behavior-neutral; copy cost measured | `TextRenderBenchmarks` gate (the phase 3 growth-guard class) |
| `DefinitionItem`/`CallParameter` consumers | Additive properties only; ctors/`OverrideWith` keep copying everything they copied | `PropsInheritanceTests` full-override rows |
| Hosts exporting an extension named with any new reserved word | No new registry names are claimed by this phase (no new extensions) | — |
| ANTLR regen churn | One dedicated regen commit | Gate rule 3 |

Migration notes shipped with the phase: the `@out(X)` release note above and the
language-reference props/slots sections (WI9).

## Performance considerations

- **Prop-less render path: zero change by construction.** No binder object exists on
  prop-less definitions; `PropsData` rides as a null reference copied by transforms the
  struct already copies. The measurable cost is `Scope` growing one field —
  the same guard class as phase 3's growth risk, gated by `TextRenderBenchmarks`
  allocation/mean parity ([performance rules](../common/coding-standards.md#performance-rules-the-render-path-is-hot)).
- **Props path, budgeted:** all-constant call sites (all defaults or literal
  arguments) — **zero per-invocation allocation** (the shared frozen array; identity
  asserted in tests). Dynamic call sites — one `object[]` clone per invocation
  (24 + 8·N bytes on x64 for an N-slot layout) plus one 24 B box per value-typed
  dynamic argument (D7's measured accounting); per
  [cross-cutting D7](../common/cross-cutting-decisions.md#d7--net-10-stance-no-conditional-compilation-by-default)
  the design assumes this array **escapes** (it does — stored into `Scope`, read by
  separately compiled delegates) and buys no JIT rescue. `PropsRenderBenchmarks` pins
  both numbers.
- **Slot path:** projection renders the caller body per `@out(expr)` execution — the
  cost is the body render itself (the feature's semantics), plus one `SlotContent` per
  definition invocation. Non-slot definitions keep the single pre-render.
- **Compile path:** layout resolution is once per definition per compile (cached);
  argument compilation reuses the phase 1 machinery. Moderate compile cost is accepted
  per the standards' compile-once-render-many rule.
- No `[MethodImpl(AggressiveInlining)]` additions — `PropsSlotParameter.GetParameter`
  is a candidate only if `PropsRenderBenchmarks` produces a justifying delta (the
  standards require the benchmark first).

## Standards compliance

The [balanced-mode](../common/coding-standards.md#solid-dry-and-yagni-in-balanced-mode)
calls actually made in this phase:

- **DRY where knowledge must not diverge:** literal decoding exists once
  (`ExpressionAstBuilder`'s helper serves both CALL-mode expressions and DEF_PROPS
  defaults); the conversion rule exists once (D10's set is used verbatim for defaults,
  arguments, and slot values); assignability reuses `TypeExtension.IsType` so prop
  narrowing and model narrowing cannot drift; the prop-read hook lives in the *shared*
  `MemberPathResolver` so both tiers resolve identically.
- **YAGNI cutting scope:** no named/multiple slots, no C#-tier prop values or reads, no
  named arguments on extensions/functions, no expression defaults, no public
  argument-access seam, no `[InlineArray]` second carriage — each in
  [Deferred items](#deferred-items) with a trigger.
- **Open/closed at existing seams:** props flow through the definition machinery,
  the options-free compile context, and the existing extension registry — no new plugin
  seam; `PropsBinder` stays internal behind the carriage abstraction the roadmap's
  .NET 10 review mandated for phase 7's benefit.
- **Security beats ergonomics:** named-argument values are native-tier only (D4 keeps
  the sandbox surface closed — no new Roslyn path); `[NotEncode]` stays dead (D14);
  every policy violation is a hard error, never degraded execution.
- **SRP along pipeline stages:** declarations parse into DTOs (`Heddle.Language`),
  layout/binding compile in the runtime compiler (`Heddle.Runtime`), and the render path
  gains only dumb carriers (`PropsData`, `SlotContent`) — no stage learns another's job.

## Deferred items

| Item | Trigger to revisit |
| --- | --- |
| Named / multiple parameterized slots (`out <name>:: T`, `@out(<name>: expr)`) | User demand for a second value-carrying region; the forward-compatible surface is designed in D19 |
| C#-tier values for individual named arguments | A scenario a registered function or model member cannot express; would require a `CS`-mode redesign (D4's evidence) |
| C#-tier prop *reads* (`@( @style )`) | Roslyn-side scope injection design; native reads and model plumbing cover known cases |
| Expression-valued prop defaults | A concrete corpus need literals cannot express; must first pin the evaluation context (D2) |
| Named arguments on extensions / registered functions (`@list(Items, sep: ", ")`) | A ratified extension-options design; today `HED5005` keeps the space reserved |
| Public argument-access seam (`InitContext.CallParameter`) | An out-of-assembly extension that cannot use the body form (D16) |
| `[InlineArray]` props carriage on `Scope` | `PropsRenderBenchmarks` showing the per-invocation array measurably hurting real templates (roadmap .NET 10 item 3) |
| Span-based props carriage | Phase 7's generated-C# backend (roadmap .NET 10 item 2; the `PropsBinder` seam is the swap point) |
| Lint: slot declared but no `@out` in the body / caller body supplied to a slot-less definition that never splices it | Phase 6 diagnostics/LSP |
| `[NotEncode]` type deletion | A future ratified breaking window (D14 — the 2.0 list does not include it) |
| `samples/component-props-slots` gallery item | Phase 9 harness (testing plan) |

## External references

Primary sources verified for this spec (July 2026); the roadmap's grounding set was
re-checked where load-bearing.

- [ASP.NET Core Blazor templated components](https://learn.microsoft.com/en-us/aspnet/core/blazor/components/templated-components) — `RenderFragment<TValue>` parameters have an implicit parameter named `context`, renamable via the `Context` attribute (re-verified; D11's model-channel precedent).
- [ASP.NET Core Blazor components](https://learn.microsoft.com/en-us/aspnet/core/blazor/components/) and [`EditorRequiredAttribute`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.components.editorrequiredattribute) — `[Parameter]` properties, defaults via initializers; `[EditorRequired]` produces the RZ2012 build **warning**, not a hard error (D10's strictness contrast).
- [ASP.NET Core Razor component generic type support](https://learn.microsoft.com/en-us/aspnet/core/blazor/components/generic-type-support) — `@typeparam`; the declared-generic contrast to Heddle's per-site monomorphisation (D6).
- [Phoenix.Component — `attr/3`, `slot/3`, `:let`](https://hexdocs.pm/phoenix_live_view/Phoenix.Component.html) — declared attrs with `required:`/`default:`; missing required, unknown attr, and literal mismatches are compile-time **warnings**; `:let` binds the slot-passed value (D1, D3, D10, D11).
- [Vue 3 — Props](https://vuejs.org/guide/components/props.html) and [Vue 3 — Slots (scoped slots)](https://vuejs.org/guide/components/slots) — declaration-based props; slot props received via `v-slot`, destructurable, per named slot (D9, D11, D19).
- [Svelte 5 — snippets](https://svelte.dev/docs/svelte/snippet) and the [v5 migration guide](https://svelte.dev/docs/svelte/v5-migration-guide) — snippets replace slots/`let:`; the child calls the snippet with arguments and the type is declared (`Snippet<[T]>`) — the newest precedent for *declared* slot parameter types (D3).
- [templ — basic syntax](https://templ.guide/syntax-and-usage/basic-syntax/) — props as typed Go parameters with hard compile errors (D10's strict end; carried from the roadmap).
- [ANTLR lexer rules & commands](https://github.com/antlr/antlr4/blob/master/doc/lexer-rules.md) — modes group rules by context; only current-mode rules can match; `pushMode`/`popMode`/`type()`/`channel()` commands (D1's isolation argument).
- [ANTLR maximal munch](https://github.com/antlr/antlr4/blob/master/doc/wildcard.md) and [rule-order tie-break](https://github.com/antlr/antlr4/blob/master/doc/faq/lexical.md) — the keyword-`WS*` length-tie technique reused in `DEF_PROPS` and for `THIS` (carried from phase 1, re-applied in [grammar.md](grammar.md)).
- [ALL(\*) tech report](https://www.antlr.org/papers/allstar-techreport.pdf) — first-alternative-wins; the 5th-alternative ordering argument.
- [What's new in .NET 10 runtime — stack allocation](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/runtime#stack-allocation) — re-verified for D7: escape analysis cannot rescue the props array (it escapes into `Scope` and separately compiled delegates), per the roadmap's verified negative and [cross-cutting D7](../common/cross-cutting-decisions.md#d7--net-10-stance-no-conditional-compilation-by-default).
- [`InlineArrayAttribute`](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.compilerservices.inlinearrayattribute) — the rejected-with-trigger alternate carriage (D7).
- [C# built-in numeric conversions](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/numeric-conversions) — the implicit-widening table D10 pins verbatim (also phase 1's `NumericPromotion` source); re-checked for this spec: `decimal` is an implicit target from integral types only, never from `float`/`double`.
