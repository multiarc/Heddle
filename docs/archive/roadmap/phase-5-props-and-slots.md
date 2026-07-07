# Phase 5 — Named Props & Parameterized Slots

> Part of the [Heddle evolution roadmap](README.md). Status: **planned, design‑first**.
> Depends on: phase 1 (literals/expressions as argument values). Grammar change: **yes** —
> the largest language‑design item on the roadmap; this doc fixes scope and constraints and
> mandates an RFC milestone before implementation. Grammar sketches below are illustrative,
> not final.

## Goal

Complete the component story. A definition can declare **typed named parameters (props) with
defaults**, call sites pass them by name alongside the model, and definitions can pass values
**into** the caller's projected content (parameterized slots). This is the capability gap
every component‑shaped engine (JSX/TSX, Blazor, HEEx, Templ) has and Heddle lacks — today a
call carries exactly one value, so multi‑input components need ad‑hoc model classes or
anonymous C# objects (which require `FullCSharp`).

## Surface (illustrative sketches)

Call site — named arguments after the positional model parameter, values are phase‑1
expressions:

```heddle
@card(Article, style: "wide", compact: true)
@card(Article, style: ::Site.DefaultCardStyle)
```

Definition — props declared in the header with types and optional defaults:

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
%@
```

Parameterized slot — the definition hands a value to the caller's projected body:

```heddle
@%
  <picker>{{ <ul>@list(Options){{ <li>@out(@())</li> }}</ul> }} :: Menu
%@
@picker(){{ <a href="/go">@(Label)</a> }}   @* body sees the option @out passed it *@
```

## Design constraints (fixed inputs to the RFC)

1. **Must compose with abstract (late‑bound) definitions and inheritance narrowing** — props
   are orthogonal to the model type: an untyped `<panel(style: string)>` still monomorphises
   per call site; a child definition inherits the base's props, may add new ones, and prop
   signatures follow the same assignability discipline as model narrowing.
2. **Named‑argument values are phase‑1 native expressions** (literals, paths, operators,
   registered functions) evaluated in the **caller's** context; the inner‑`@` C# tier remains
   available per argument under `FullCSharp`.
3. **`:` collision analysis** (why the sketches are viable): chain `DELIM :` appears only
   between calls (after `)`); a named‑arg `:` follows a bare identifier in argument position
   (an identifier without parens cannot start a chain); the ternary `:` is only reachable
   after `?`. All three are grammatically separable — the RFC must verify with ANTLR
   prediction diagnostics on the fixture corpus.
4. **Definition‑header parens are currently a lex error in `DEF` mode** → the declaration
   syntax occupies previously invalid space (back‑compat‑safe). New DEF‑mode tokens needed:
   `(`, `)`, `,`, `:`, `=`, literals, and type names (the `ID_TYPE` rule already exists for
   `:: Type`).
5. **Static binding, no dictionaries at render.** Props compile to index‑based slots: each
   call site binds its named arguments (plus defaults for omitted ones) to a fixed layout at
   compile time; the body's prop reads compile to slot accesses. Runtime carriage: a new
   `Scope` field (e.g. `PropsData object[]`) or equivalent — decided in the RFC; the
   requirement is zero lookup cost and no boxing regressions in hot paths.
6. **Missing required prop / unknown prop name / type mismatch are call‑site compile
   errors** with positions, consistent with per‑use‑site model checking.

## Key decisions for the RFC (M0)

| Decision | Options | Lean |
| --- | --- | --- |
| How bodies read props | (a) merged into member resolution — `@(style)` checks props first, then model, with a compile‑time shadowing warning; (b) explicit `@prop(style)` accessor | (a) — most ergonomic; resolution is static so there is no runtime ambiguity. Precedent‑backed: Blazor, HEEx, Vue, and Templ all read props as bare declared names — declaration is exactly what makes resolution static. None of them shares that namespace with a separate model, so the shadowing warning is the right Heddle‑specific addition |
| Slot value channel | `@out(expr)` renders the projected body with the value as **model** vs as **chained** | model (matches "the body zooms into the value"); chained stays the index/pipe channel. Every precedent makes the passed value the body's primary binding — Blazor's implicit `context` on `RenderFragment<T>`, HEEx's `:let={var}`, Vue's `v-slot` slot props — and Heddle's implicit model is the exact equivalent of that binding |
| Multiple named slots with props | already possible via multiple definitions — do parameterized slots need names too? | defer; single default slot with a value covers the common case — but Blazor (several `RenderFragment<T>` parameters), HEEx (named slots with attrs), and Vue (named scoped slots) all ended up offering named typed slots, so the deferral must not grammar‑lock the single‑slot form |
| Prop inheritance conflicts | child re‑declares a base prop: error vs narrow vs re‑default | re‑default allowed, type must stay assignable; error otherwise |
| Optional vs required | prop without default = required | **ratified (July 2026): hard compile errors** — required‑ness is declared statically in every precedent (Blazor `[EditorRequired]`, HEEx `required: true`, Vue `required`/non‑optional TS props). Heddle is deliberately at the strict end: Blazor and HEEx only **warn** on a missing required prop, while Heddle's hard compile error matches Templ (props are plain Go parameters) and Heddle's own per‑site model checking. No longer an RFC question — M0 takes it as fixed input |

### Ecosystem precedents

The leans above are checked against how the component‑shaped engines actually do this
(sources in [External grounding](#external-grounding)):

| Engine | Prop declaration | Typed slot mechanism | What Heddle takes |
| --- | --- | --- | --- |
| **Blazor** | `[Parameter]` public properties, defaults via initializers; `[EditorRequired]` = design/build‑time warning only, not enforced at runtime | `RenderFragment<T>` parameter — the body reads the passed value via an implicit variable named `context`, renamable with the `Context` attribute | the closest .NET shape for "slot receives a value"; Heddle's implicit model plays the role of `context`, no rename attribute needed |
| **Phoenix HEEx** | `attr :name, :type` with `required:`, `default:`, `values:` — unknown attr, missing required attr, and literal type mismatch are **compile‑time warnings** | `slot/3`, optionally with its own declared attrs; the caller binds the value the component passes back with `:let={var}` (`render_slot(@inner_block, value)`) | the strongest precedent for declared, compiler‑checked props on a template; Heddle hardens HEEx's warnings into positioned errors |
| **Templ (Go)** | components are functions — props are ordinary typed Go parameters, missing/mistyped arguments are hard Go compile errors | a `templ.Component` (or function producing one) passed as a parameter | the compiled, strictly‑typed discipline: props as real typed parameters with hard errors |
| **Vue 3** | `defineProps` — explicit declaration required; type/`default`/`required` validated at runtime, or compile‑checked under TS type‑based declaration | scoped slots: the child binds values onto `<slot>`, the caller receives them via `v-slot="slotProps"` (destructurable, per named slot) | the scoped‑slot naming precedent ("slot props"); defaults live on the declaration, not the call site |
| **React/JSX** | props are function parameters (TS‑checked) | render props / children‑as‑function: a prop that is a function the component calls with data | confirms "callee hands a value into the caller's body" as the universal component contract |

## Milestones

- **M0 — RFC** (gate for everything else): final grammar for both sides, ANTLR ambiguity
  verification against the existing fixture corpus, the five decisions above, and the
  fixture set written *first* (fixtures‑as‑spec).
- **M1 — Grammar + parse**: lexer/parser changes
  ([HeddleLexer.g4](../../src/Heddle.Language/HeddleLexer.g4) CALL + DEF modes,
  [HeddleParser.g4](../../src/Heddle.Language/HeddleParser.g4) `call` args + `def` header),
  regen, `CallParameter`/`DefinitionItem` carrying named args / prop declarations.
- **M2 — Compile + bind**: prop layout allocation, default‑value compilation, call‑site
  binding + diagnostics, body prop resolution (decision a/b), `Scope` carriage.
- **M3 — Slots**: `@out(expr)` parameter support in
  [OutExtension](../../src/Heddle/Extensions/OutExtension.cs) + projection semantics.
- **M4 — Docs + tests**: language‑reference section, patterns recipes, golden suite.

## Back‑compat analysis

- All new syntax lives in positions that are lex/parse **errors** today (named args inside
  parens, parens in definition headers) — no existing template changes meaning.
- `OutExtension` with a parameter: `@out(expr)` currently — verify current behavior (the
  empty parameter is the documented form); if `@out(X)` is currently accepted-and-ignored,
  that is a behavior change to document.
- Definitions without props are untouched; the props machinery must add zero overhead to
  prop‑less calls (benchmark‑guarded).

## Risks & mitigations

- **Grammar complexity creep** — the single biggest risk; contained by M0's
  fixtures‑as‑spec + ambiguity verification before any implementation. (L)
- **Interaction with abstract definitions** (prop layout per monomorphised specialization):
  layout is per definition, types checked per site — RFC must include a worked example with
  one abstract definition used at two sites. (M)
- **Scope growth** (another field): measure; `Scope` is a readonly struct passed by `in` —
  same guard as phase 3. (S)

## Success criteria

1. A card/layout fixture using two typed props with defaults plus one parameterized slot
   renders correctly with `AllowCSharp = false`.
2. Prop typo at a call site → compile error naming the definition, the unknown prop, and the
   call‑site position; missing required prop likewise.
3. Props work on an **abstract** definition used against two model types (independent
   type‑checking per site, shared prop layout).
4. Inherited definition adds a prop and re‑defaults a base prop; incompatible re‑type →
   compile error.
5. Prop‑less templates show zero perf/allocation regression (benchmark suite).
6. Entire existing suite passes byte‑identically.

## Validation scenarios

| Scenario | Expected |
| --- | --- |
| `@card(Article, style: "wide", compact: true)` with the sketch definition | props honored; body sees `Article` as model |
| `@card(Article)` (all defaults) | `style = "plain"`, `compact = false` |
| `@card(Article, style: ::Site.DefaultCardStyle)` | prop value from root context |
| **Negative** `@card(Article, stlye: "x")` | compile error "unknown prop 'stlye' on card" at the call site |
| **Negative** `@card(Article, compact: "yes")` | type‑mismatch compile error |
| **Negative** required prop omitted | positioned compile error |
| Prop named like a model member | shadowing warning; resolution per RFC decision |
| Abstract `<panel(style: string)>` called from Blog and Article scopes | both sites compile, each type‑checked |
| `<fancy:card>` adding `tone: string = "info"` | inherited props + new prop work; re‑typing `style: int` → error |
| Parameterized slot (picker sketch) | caller body renders once per option with the option as model |
| Chain still works: `@out(a():b())` | unchanged (nested chain, not a named arg) |

## Testing

### Impact analysis

The largest test surface of any phase, because the touch points are the engine's most
intricate subsystems: the grammar (CALL **and** DEF modes — prediction changes affect the
parse of every template), the definition machinery (monomorphisation, inheritance
narrowing, override layering), `Scope` carriage (another hot‑path field — same guard
class as phase 3), and `OutExtension` projection semantics. A regression here can be
subtle: a prediction change that re‑parses an existing template differently would corrupt
output without erroring.

### Regression requirements

- Full suite + goldens byte‑identical (success criterion 6).
- Prop‑less calls: zero perf/allocation overhead, benchmark‑guarded (criterion 5).
- The `@out(X)` current‑behavior verification from Back‑compat runs **before M1** — if
  it is accepted‑and‑ignored today, that pin becomes the documented behavior‑change test.
- **ANTLR ambiguity verification against the existing corpus is an M0 gate** — regression
  testing starts before a line of implementation exists.

### TDD verdict

**Yes — institutionalized by the milestone structure.** M0 mandates **fixtures‑as‑spec**:
the RFC ships with the fixture set written first, which is TDD at the design level — the
grammar is not final until the fixtures parse on paper. M1/M2 then implement against that
frozen fixture set. Diagnostics are error‑first: every negative scenario (unknown prop,
type mismatch, missing required — hard errors, ratified) is a failing test before the
binder learns the rule, consistent with the strict‑errors decision being the phase's
identity.

### The verification loop

Milestone‑gated — the loop nests inside the M0→M4 sequence, and regression gates run at
**every milestone exit**, not once at the end:

1. **M0**: fixtures‑as‑spec written + ANTLR ambiguity verification green on the existing
   corpus. No implementation before this gate.
2. **M1**: grammar + regen; loop = regen → fixture corpus parse assertions → adjust
   grammar → regen, until the M0 fixtures parse and the existing corpus is untouched.
3. **M2**: negative diagnostics first (each a failing test), then binding/layout until
   the positive fixtures render; benchmark gate for prop‑less calls at exit.
4. **M3**: slot fixtures (written in M0) drive `@out(expr)`; projection semantics pinned.
5. **M4**: full gates — suite, goldens, benchmarks, docs examples compiled as tests.
   Red at any milestone → fix within that milestone; a fixture change reopens **M0**
   (spec re‑ratification), it is never a local edit.

### The final suite (including deferred phase 9 items)

- The fixtures‑as‑spec corpus (positive + the full negative table), abstract‑definition
  two‑site fixtures, the inheritance/narrowing matrix, shadowing‑warning tests, the
  prop‑less benchmark guard, and goldens.
- **Deferred to [phase 9](phase-9-demo-and-integration.md):** the
  `samples/component-props-slots` demo — a small component library (card/layout/picker)
  built on typed props and parameterized slots, golden‑asserted in CI; doubles as the
  component‑story showcase the assessment said Heddle lacked.

## Size estimate

| Work item | Size |
| --- | --- |
| M0 RFC + fixtures‑as‑spec | M |
| M1 grammar + regen + parse DTOs | M/L |
| M2 compile/bind + diagnostics | **L** |
| M3 slots | M |
| M4 docs + tests | M |

## .NET 10 opportunities (net10.0 target)

Inputs to the M0 RFC's carriage decision (constraint 5). The engine multi‑targets
`netstandard2.0;net6.0;net8.0;net10.0`; everything here is about what the **net10.0 build**
can exploit behind `#if NET10_0_OR_GREATER` (one item is `NET8_0_OR_GREATER`). Versions were
verified against the official release notes because they matter: .NET 9's escape analysis
shipped for **boxed value types only**; array stack allocation is genuinely new in .NET 10.

1. **JIT stack allocation of small `object[]` arrays — real in .NET 10, but it will not
   rescue the sketched carriage.** *Mechanism*: .NET 10's escape analysis stack‑allocates
   small, fixed‑size arrays — value‑type element arrays and, new in 10, **reference‑type
   element arrays** like `object[]` — when the array provably doesn't outlive its allocating
   method ([What's new in .NET 10 runtime — stack allocation](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/runtime#stack-allocation);
   .NET 9 covered [boxes only](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-9/runtime#object-stack-allocation-for-boxes)).
   .NET 10 also stops treating a store into a **local struct field** as an escape (the
   struct itself must not escape) and added delegate‑object escape analysis
   ([Performance Improvements in .NET 10](https://devblogs.microsoft.com/dotnet/performance-improvements-in-net-10/),
   [de‑abstraction tracking issue](https://github.com/dotnet/runtime/issues/108913)).
   *Benefit if it applied*: the per‑invocation props array would cost zero heap with no
   engine changes. *Honest conditionality — it mostly won't apply*: escape analysis is
   intraprocedural plus inlining, and Heddle's props array is stored into `Scope`
   ([Scope.cs](../../src/Heddle/Data/Scope.cs)) and then carried into the definition body —
   a separately compiled `Expression.Compile` delegate or an
   [IScopeRenderer](../../src/Heddle/Data/ScopeRenderer.cs) call. Arguments that flow into
   non‑inlined callees escape by definition, and runtime‑compiled dynamic methods are not
   inlined into their callers. The .NET 10 struct‑field improvement removes exactly one
   blocker (the store into a `Scope` local), which helps only when the *entire* call —
   binding, scope construction, body — happens to inline: not a shape to design for.
   *Design lever that needs no JIT*: call sites whose props are all compile‑time constants
   (all defaults, or literal arguments) should share one frozen array built at
   template‑compile time — zero per‑invocation allocation on every TFM. The RFC should
   mandate this regardless of carriage choice.
2. **Span‑aware escape analysis (.NET 10) points at a span carriage — blocked by the
   runtime backend, viable in phase 7.** *Mechanism*: .NET 10 escape analysis reasons
   through struct fields including `Span<T>`/`ReadOnlySpan<T>`, so an array immediately
   wrapped in a span can be stack‑allocated and used as if `stackalloc`‑seeded
   (dotnet/runtime [#113977](https://github.com/dotnet/runtime/pull/113977) and
   [#116124](https://github.com/dotnet/runtime/pull/116124), per the
   [.NET 10 perf post](https://devblogs.microsoft.com/dotnet/performance-improvements-in-net-10/)).
   A `ReadOnlySpan<object>` props carriage would be the JIT‑friendly — and, seeded from a
   caller‑owned buffer, *deterministic* — shape. *Conditionality*: a span field makes
   `Scope` a `ref struct`, and `System.Linq.Expressions` rejects ByRef‑like types outright —
   incompatible with the current runtime backend. Viable only in the
   [phase 7](phase-7-build-time-compilation.md) generated‑C# backend, where a definition
   body can take `scoped ReadOnlySpan<object>` (or better — see item 5). *Action for M0*:
   keep the carriage behind a narrow internal abstraction so phase 7 can swap `object[]`
   for a span without touching slot layout or binding.
3. **`[InlineArray]` fixed‑slot carriage — .NET 8 / C# 12, not .NET 10‑specific;
   deterministic but costly to `Scope`.** *Mechanism*: a single‑field struct annotated
   [`[InlineArray(N)]`](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.compilerservices.inlinearrayattribute)
   (runtime support since .NET 8, language support since C# 12) embedded in `Scope` gives N
   object slots with no heap allocation and no reliance on escape analysis. *Trade‑offs*:
   (a) `Scope` grows by N×8 bytes copied on every `Chain`/`Model`/`Parent` transition —
   an 8‑slot buffer more than doubles today's 6‑field struct, colliding head‑on with the
   phase‑3 "Scope growth" guard; (b) fixed max arity — prop counts above N need an
   `object[]` overflow path anyway; (c) netstandard2.0/net6.0 builds keep the `object[]`
   carriage regardless, so this is a **second** carriage behind `#if NET8_0_OR_GREATER`,
   doubling the test surface; (d) expression‑tree bodies cannot index inline arrays or
   spans — slot reads must go through small inlineable C# helpers (fine, but the carriage
   can't be manipulated directly in trees); (e) **boxing of value‑type props remains under
   both carriages** — the element type is `object` either way. *Verdict*: candidate only if
   M2 benchmarks show the per‑invocation array actually hurting; take the frozen‑array
   lever from item 1 first.
4. **Per‑arity generic frames — evaluated, rejected.** `PropsFrame<T1,…,Tn>` structs would
   eliminate per‑prop boxing (one boxed frame per invocation instead of an array plus a box
   per value‑type prop, with typed reads via `Expression.Unbox` + field access). But it
   adds a per‑call‑site frame type, a generic‑instantiation explosion across arities × prop
   types, and a second dimension to binding and default compilation — heavy machinery to
   shave allocations that the frozen‑array path already avoids for the common constant
   case. Overengineered for M2. Phase 7 gets the same result for free: generated C# can
   pass props as plain typed parameters (the Templ shape), where no frame type and no
   boxing exist at all.
5. **Declaration side — C# 12/13/14 matter only if phase 7 generates C#.** For the
   [phase 7](phase-7-build-time-compilation.md) backend: C# 13
   [`params` collections](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-13#params-collections)
   let generated definitions accept `params ReadOnlySpan<object>` with the compiler
   synthesizing a stack‑backed inline array at each call site —
   [no heap allocation](https://devblogs.microsoft.com/dotnet/csharp13-calling-methods-is-easier-and-faster/);
   C# 12 collection expressions targeting spans lower the same way; C# 14 (ships with
   .NET 10) adds
   [first‑class span conversions](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-14#implicit-span-conversions)
   that remove friction between `object[]` and span APIs at the runtime/generated‑code
   boundary. None of this touches the phase‑5 runtime backend on any TFM — expression trees
   never see these language features. Cross‑reference recorded here so the phase‑7 design
   doesn't re‑derive it.
6. **Reflection/binding — negative finding.** The official
   [.NET 10 runtime](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/runtime)
   and [libraries](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/libraries)
   what's‑new pages list **no** reflection or member‑binding additions relevant to prop
   layout resolution (the libraries page has no reflection section at all). Prop layout is
   resolved once per template compile — a cold path already served by existing reflection;
   nothing here is worth a `NET10_0_OR_GREATER` gate. Likewise .NET 10's delegate
   stack‑allocation targets short‑lived non‑escaping closures; Heddle's compiled render
   delegates are cached and long‑lived, so there is nothing to gain there either.

## External grounding

- [ASP.NET Core Blazor components](https://learn.microsoft.com/en-us/aspnet/core/blazor/components/) —
  `[Parameter]` properties, defaults via initializers; `[EditorRequired]` "is enforced at
  design‑time and when the app is built … isn't enforced at runtime".
- [ASP.NET Core Blazor templated components](https://learn.microsoft.com/en-us/aspnet/core/blazor/components/templated-components) —
  `RenderFragment<TValue>` parameters "have an implicit parameter named `context`",
  renamable via the `Context` attribute.
- [`EditorRequiredAttribute` API](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.components.editorrequiredattribute) —
  the required‑parameter marker's exact semantics.
- [Phoenix.Component — `attr/3` and `slot/3`](https://hexdocs.pm/phoenix_live_view/Phoenix.Component.html) —
  declared attrs with type/`required`/`default`/`values`; unknown attrs, missing required
  attrs, and literal type mismatches produce compile‑time warnings; `:let` binds the value a
  slot passes back.
- [templ — basic syntax](https://templ.guide/syntax-and-usage/basic-syntax/) — components
  compile to Go functions; props are typed Go parameters checked by the Go compiler.
- [Vue 3 — Props](https://vuejs.org/guide/components/props.html) — `defineProps` with
  types, `default`, `required`; runtime‑validated, compile‑checked under TS type‑based
  declaration.
- [Vue 3 — Slots](https://vuejs.org/guide/components/slots.html) — scoped slots: attributes
  bound on `<slot>` arrive as "slot props" via `v-slot`.
- [React — passing data with a render prop](https://react.dev/reference/react/cloneElement#passing-data-with-a-render-prop) —
  "a prop that specifies how to render something": the component calls the caller's
  function with data.
