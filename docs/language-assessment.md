# Heddle 2.0 — An Independent Language & Engine Assessment

*An outside-in review of the Heddle template language and engine (revision assessed: **2.0.0**,
`feature/lang_asmt` branch, July 2026), placed against the industry's template-engine landscape.
All statements about Heddle are grounded in the repository's grammar, source, and documentation;
all statements about other engines are grounded in public documentation, benchmarks, and
community commentary (linked throughout, collected in [Sources](#sources)).*

---

## Table of contents

1. [Executive summary](#executive-summary)
2. [What Heddle is](#what-heddle-is)
3. [The industry landscape — a taxonomy](#the-industry-landscape--a-taxonomy)
4. [Language semantics, feature by feature](#language-semantics-feature-by-feature)
5. [Engine implementation specifics](#engine-implementation-specifics)
6. [Head-to-head comparisons](#head-to-head-comparisons)
7. [Feature matrix](#feature-matrix)
8. [Security assessment](#security-assessment)
9. [Performance assessment](#performance-assessment)
10. [Tooling and ecosystem](#tooling-and-ecosystem)
11. [Weaknesses and risks](#weaknesses-and-risks)
12. [What is genuinely unique](#what-is-genuinely-unique)
13. [Verdict](#verdict)
14. [Sources](#sources)

---

## Executive summary

Heddle is a **compiled, statically-typed text template language for .NET** whose entire
control-flow vocabulary is a library rather than grammar. After tracing every documented
construct back to the ANTLR grammar and the engine source, and comparing the result against
roughly fifteen engines across five ecosystems, my overall finding is:

> **Heddle is one of the most carefully designed template languages in the industry — and one of
> the most demanding to learn.** Its three headline ideas (extension-only control flow with
> role-based branch sets, abstract late-bound definitions with per-call-site static typing, and a
> three-tier expression surface with a compile-time-enforced sandbox) are each individually rare;
> the combination exists nowhere else that I could find. Its engineering discipline —
> byte-identical precompilation differentials, positioned diagnostic codes, a real LSP — exceeds
> what most engines with 100× its adoption ship. Its liabilities are exactly the mirror image:
> sigil-dense syntax with a steep on-ramp, a single-implementation .NET-only ecosystem, no
> context-sensitive HTML escaping, and no render-resource budgets for hostile templates.

**Grades at a glance** (justified throughout, summarized in the [Verdict](#verdict)):

| Dimension | Grade |
| --- | --- |
| Language design coherence | **A** |
| Type system / static guarantees | **A** (unmatched in the category) |
| Composition & reuse model | **A−** |
| Security model | **B+** (compile-time sandbox excellent; no contextual escaping, no render budgets) |
| Performance architecture | **A−** (structurally sound; benchmarks self-reported) |
| Ergonomics / learnability | **C+** |
| Tooling | **A−** (for its size, extraordinary) |
| Ecosystem & portability | **D+** (single vendor, single runtime) |

---

## What Heddle is

Heddle compiles templates written in a small purpose-built language into reusable,
strongly-typed renderers for .NET (`netstandard2.0` / `net6.0` / `net8.0` / `net10.0`).
A template like:

```heddle
@model(){{dynamic}}
<p>Hi @(Name) — you have @int(Count) new comments.</p>
```

is lexed and parsed by an ANTLR 4.13.1 grammar ([HeddleLexer.g4](../src/Heddle.Language/HeddleLexer.g4),
[HeddleParser.g4](../src/Heddle.Language/HeddleParser.g4)), walked into a structured parse
context, and compiled by [HeddleCompiler](../src/Heddle/Runtime/HeddleCompiler.cs) into an
**execution-ready document**: an in-memory tree of extension instances wired to *compiled*
value accessors — member paths become expression-tree delegates, embedded C# becomes Roslyn
delegates. Rendering walks that tree; nothing is re-parsed, reflected, or interpreted per render.

The language's own vocabulary is deliberately tiny. Everything an author writes falls into five
categories — text, `@…` output blocks, `@% … %@` definition blocks, `@<<{{ … }}` imports, and
raw blocks — and *every verb* (`@list`, `@if`, `@date`, `@partial`, even `@else`) is an
extension class, not a keyword. The grammar knows how to parse a call; it does not know what
`if` means.

Two packages ship: `Heddle` (engine) and `Heddle.Language` (grammar + generated parsers), plus
`Heddle.Generator` (build-time precompilation), `Heddle.LanguageServer` (LSP), and a VS Code
extension. Ten golden-tested sample projects cover the supported integration modes, from
ASP.NET Core SSR streaming to a T4-successor code generator with no runtime engine dependency.

---

## The industry landscape — a taxonomy

To place Heddle, it helps to sort the industry into five families. Every engine I compare
against belongs to one:

**1. Full-language embedders.** The template hosts a real programming language: Razor (C#),
ERB (Ruby), EJS (JS), JSP, PHP itself. Maximum power, zero sandboxing — templates are code.
Razor is the .NET incumbent: excellent IDE support, compiled views, but hard to use outside
ASP.NET (a long-standing community pain point — see
[The Bleeding Edge of Razor](https://www.daveaglick.com/posts/the-bleeding-edge-of-razor) and the
fragmented RazorLight/RazorEngineCore ecosystem, none of which support full sections/layouts
fidelity).

**2. Sandboxed interpreted DSLs.** A deliberately limited language interpreted at render time:
Liquid (Shopify), DotLiquid/Fluid (.NET ports), Scriban, Jinja2, Twig, Django templates,
Velocity, Freemarker. Born from the *untrusted template* problem — Shopify built Liquid
explicitly so "users can edit them … you don't want your server running code that your users
wrote" ([Shopify/liquid](https://github.com/shopify/liquid)). Dynamic typing, runtime errors,
runtime sandboxes of varying (and periodically broken — see [Security](#security-assessment))
strength.

**3. Logic-less minimalists.** Mustache and strict Handlebars: no expressions at all, push all
logic into the view model. Praised for simplicity and polyglot reach; criticized for merely
*relocating* logic into bloated view-model preparation code — eBay's engineering blog made
[the canonical case against them](https://www.ebayinc.com/stories/blogs/tech/the-case-against-logic-less-templates/),
and even sympathizers concede ["the logic has to go somewhere"](https://javarants.com/mustache-is-logic-less-but-the-logic-has-to-go-somewhere-3e976b7a49c8).

**4. Compile-time typed generators.** Templates compiled to host-language code with full static
type checking: templ (Go), Askama (Rust, Jinja-syntax compiled to Rust), Maud (Rust macros),
gomponents, Java's JStachio. The fastest-growing family this decade — "all template syntax
errors, missing variables, or type mismatches are caught by the compiler before your
application even runs" ([Askama](https://github.com/askama-rs/askama)) — at the price of losing
runtime template loading.

**5. Component frameworks.** JSX/React, Vue SFCs, Svelte, Blazor. Not string template engines,
but they define modern expectations: typed props, scoped/typed slots (Vue scoped slots,
Svelte 5 [snippets](https://svelte.dev/docs/svelte/snippet), Blazor `RenderFragment<T>`),
composition-first design.

There is also a sixth, mostly historical pole worth naming because Heddle's parser heritage
points straight at it: **StringTemplate**, Terence Parr's engine (Parr also wrote ANTLR, which
Heddle's grammar uses). Parr's WWW2004 paper
[*Enforcing Strict Model-View Separation in Template Engines*](https://www.cs.usfca.edu/~parrt/papers/mvc.templates.pdf)
argued templates should be side-effect-free, order-independent, and unable to compute — the
philosophical *maximum separation* position.

**Where Heddle sits:** it is a genuine hybrid — family 4's static typing and compilation, family
2's small purpose-built DSL and (in `Native` mode) sandboxability, family 1's full-C# escape
hatch, and family 5's typed props/slots — while agreeing with StringTemplate that the *grammar*
should stay tiny and disagreeing that templates must not compute.

---

## Language semantics, feature by feature

This section walks the language surface and assesses each design decision against industry
practice. (The authoritative reference is [language-reference.md](language-reference.md).)

### 4.1 The `@` sigil and text model

Everything special starts with `@`; `@@` escapes it; all other text is literal. This is Razor's
single-sigil model, and it is the right call for HTML-heavy content — compare Jinja/Liquid's
dual `{{ }}`/`{% %}` and Go's `{{ }}`, which collide with JS template literals and require
escaping ceremonies. Heddle's raw blocks (`@{ … }@`, `@:`) and hidden-channel comments
(`@* … *@`, legal *mid-token* — a genuinely unusual lexer property) round this out well.

**Assessment: strong.** One caveat: Heddle reuses `{{ }}` as its *body* delimiter, so an author
coming from the Jinja/Liquid world will misread `{{ … }}` as interpolation for their first
hour. Minor, but real.

### 4.2 Output blocks, chains, and the unified `chained` channel

An output block is `@` + a chain of calls + an optional body. Chains compose **right-to-left**
(`@a():b():c()` = `a(b(c))`), with the leftmost call rendering, and the engine tracking the
*type* flowing through the chain at compile time.

The bold design economy here is that **one channel — `chained` — carries three things** most
engines model separately: the loop index (`@list`/`@for` expose it there), the piped value in a
chain, and the content projected into `@out()`. Where Liquid has `forloop.index`, filters via
`|`, and no content projection; where Jinja has `loop.index`, `|` filters, and
`{% block %}`/`caller()`; Heddle has one concept.

**Assessment: elegant, with a learnability tax.** Right-to-left chains read as "outermost
first" (like function application), the opposite of Unix/Liquid/Jinja pipes, which read
left-to-right as data flow. Every pipe user will invert their intuition. The unification is
intellectually satisfying and keeps the engine small; whether "the loop index and the pipe value
are the same thing" *helps* authors or puzzles them is genuinely debatable. I lean toward: it
helps extension authors, and template authors mostly never notice.

### 4.3 Relative context and `::` root

The current model is relative and *moves* as calls nest — the Mustache/Go `.` model, not
Razor's absolute `Model.X`. Descending calls (`@list`, definitions) rebind the body to their
parameter; **value calls** (`@if`, `@date`, `@money`, …) treat the parameter as a value and step
the body **one level back** to the caller's context. `::Member` jumps to the root from any
depth; there is deliberately no `..` parent path.

Industry context: Go's dot-context is the [single largest source of new-user template
errors](https://pkg.go.dev/text/template) in that ecosystem; Mustache climbs the whole context
stack implicitly (a well-known source of spooky action); Liquid keeps everything near-global.
Heddle's twist — the *kind of extension* determines whether context descends or steps back — is
more predictable than Mustache's stack search once learned, and the step-back rule solves the
real Go/Mustache annoyance where `{{if .Flag}}` would otherwise rebind the body to a boolean.

**Assessment: the right semantics, with one honest weakness.** At a call site, nothing visually
distinguishes a descending call from a value call — you must know the extension's category
(declared via its `[DataType]`/behavior, documented per extension). The compiler statically
checks the result, and the LSP shows you the scope type, so mistakes are caught — but *reading*
unfamiliar templates requires a lookup Go's uniform `with`/`range` never does. The one-level
step-back plus `::`-root with no `..` in between is a defensible simplification (Liquid's
`forloop.parentloop` shows where `..` chains end up), and "pass it down explicitly" is the
better habit anyway.

### 4.4 The three-tier expression surface

This is one of Heddle's two or three most important designs, and it deserves emphasis:

| Tier | Syntax | Compiled by | Trust level |
| --- | --- | --- | --- |
| Member paths | `@(A.B.C)` | expression trees | safe: getters only, null-safe hops |
| **Native expressions** | `@(Price * Quantity)`, `@(Name ?? "anon")`, `upper(x)` | expression trees, **zero Roslyn** | sandbox-safe: operators, literals, *registered* functions only |
| Full C# | `@( @model.Articles.Where(a => …) )` | Roslyn | trusted code, full BCL |

`TemplateOptions.ExpressionMode` (`MemberPathsOnly` / `Native` / `FullCSharp`) selects the tier.
The native tier reimplements C# operator semantics faithfully (precedence, numeric promotion,
lifted nullables) with a short, *documented* deviation list — null-safe `.` hops making `?.`
redundant, total `==` via `object.Equals`, no user-defined implicit conversions. The
`FunctionRegistry` is the explicit trust boundary: "anything registered is callable from
template text, and nothing else is" — and rejection happens at **compile time with positioned
errors**, not at render time.

Industry context: Scriban has a runtime sandbox ("by default, no .NET objects are exposed unless
explicitly allowed" — [scriban/scriban](https://github.com/scriban/scriban)); Jinja2 has
`SandboxedEnvironment` (runtime attribute interception); Liquid is safe by construction but has
*no* expression tier at all beyond filters. **No other engine I reviewed offers three explicit,
host-selectable trust tiers over one syntax**, and none makes the sandbox a *compile-time
rejection* property rather than a runtime interception property. Compile-time rejection is
categorically stronger: there is no gadget-chain to find at runtime because the disallowed call
was never compiled (see [Security](#security-assessment)).

**Assessment: best-in-class design.** The subtlety cost is real — an author must know which tier
a construct needs (`@card(A, x: @expr)` is a syntax error because named-argument values are
native-tier only) — but the diagnostics name the tier and the mode, which is exactly how to
manage that cost.

### 4.5 Definitions, abstract typing, and inheritance

Definitions (`@% <name> {{ body }} :: Type %@`) are Heddle's components. Three properties
combine into something no other template engine has:

1. **Abstract by default, bound late.** A definition without `:: Type` is compiled **against
   the concrete model at each call site** — per-use-site monomorphization with independent
   static type checking. The docs call this "closer to a C++ template than a C# generic," and
   the analogy is exact: no type parameters, no constraints, structural checking per
   specialization. Point the same `<panel>` at a `Blog` and an `Article`; each call site
   compiles and type-checks separately.

2. **Inheritance and document-order override.** `<child:base>` inherits; `<name:name>`
   *replaces from that point onward*. A child may narrow an abstract base's type (assignability
   enforced by the compiler). Props inherit, may be re-defaulted, and may be appended.

3. **Symmetric composition.** A layout exposes regions (`@sidebar()`, `@out()`); a page imports
   it (`@<<`) and overrides only what it needs. Unlike Razor sections — which bind "backwards,"
   pinning the rendered page as the layout's final consumer — nothing here is directional: any
   page can serve as a base for another, and it all flattens into one compiled document with
   zero runtime indirection.

Industry context, taken seriously rather than as a strawman:

- **Jinja2/Twig block inheritance** (`{% extends %}` + `{% block %}`) is the mature comparable
  for point 3 and has existed for nearly two decades. Heddle's genuine additions over it are
  static typing of the override chain, document-order layering (render, redefine, render again),
  and zero-cost flattening. The *shape* of the feature is not new; the guarantees are.
- **Mustache/Liquid partials** are dynamically typed and per-render; nothing checks that a
  partial's assumptions hold at its call site.
- **Askama/templ** type templates *nominally* — one declared context struct/parameter list per
  template, compiled once. Heddle's abstract definitions are *structural* — one source, N
  statically-checked specializations. This is the single feature I could not find anywhere
  else in the template-engine literature. (Its closest relatives are C++ templates and, at a
  stretch, TypeScript's structural generics — not anything in family 2, 3, or 4.)

**Assessment: the crown jewel.** Per-call-site structural typing is what makes "write the
section once, reuse it across model shapes, keep static checking" possible, and it is Heddle's
strongest claim to novelty. The cost is the classic C++-template cost, honestly inherited:
errors surface at the use site (the LSP mitigates), compile work multiplies per specialization,
and there is no way to state a definition's *requirements* up front (no `where`-style contract —
you learn what `<panel>` needs by reading its body or breaking a call site).

### 4.6 Props and parameterized slots

2.0's props (`<card(style: string = "plain", compact: bool = false)>`) and typed slots
(`<picker(out:: MenuOption)>` with `@out(expr)`) import the component-framework contract into a
string template engine:

- Props are typed, named-argument-passed, literal-defaulted, and exhaustively validated at
  compile time (missing/unknown/mistyped/duplicated → positioned `HED5xxx` errors, one
  documented conversion set, no string coercions).
- A typed slot hands a value *back into the caller's body*, which compiles against the slot
  type — this is exactly Blazor's `RenderFragment<T>`, Vue's scoped slots, and Svelte 5's
  snippet parameters.

Industry context: in the component-framework world this is table stakes; in the *string
template* world it essentially doesn't exist. Jinja macros take untyped args; Liquid
`{% render %}` passes untyped variables; Handlebars partials take a hash of whatever. Vue's own
docs community notes that [typing scoped slot props is
tricky](https://dev.to/aloisseckar/vuejs-tips-scoped-slot-props-and-how-to-type-them-lph) even
*with* TypeScript; Svelte moved from slots to snippets precisely because ["snippets type better,
scope cleaner"](https://fullstacksveltekit.com/blog/svelte-slots). Heddle got the typed version
right on the first public iteration, with better compile-time enforcement than Vue had for
years.

**Assessment: excellent, and strategically important** — it makes Heddle's "component" story
credible against Blazor/Razor components for server-rendered composition, not just against
string engines. Limitations worth noting: literal-only defaults, exactly one slot per
definition (Vue/Blazor/Svelte allow many named slots — Heddle's answer would be multiple
definitions or chained content, which is less direct), and props are per-definition, not
per-template-file.

### 4.7 Branch sets: control flow as a library, roles not names

`@if`/`@elif`/`@else` look like keywords but are ordinary extensions carrying a
`[BranchRole]` attribute (opener / continuation / terminal). The compiler recognizes a *set* by
adjacency at one body level, strips inter-branch text (warning `HED3001`), validates orphans
(`HED3003`), and provisions per-body-level set state — all driven by the attribute, so **a
custom extension with the same roles joins a set on equal footing with the built-ins**, even
interoperating in one set across families.

Industry context: Liquid also implements its standard tags in the same tag framework users
extend, so "control flow is library" is not unprecedented — but Liquid's `else`/`elsif`
handling is hard-coded inside the `if` block tag's parser, so you cannot write a *new*
continuation that composes with the built-in `if`. Jinja2 extensions can add tags but "writing
extensions is a non-trivial task" involving hand-built AST nodes that "are horrible to debug"
when malformed ([first-hand account](https://medium.com/horlu/my-experience-of-writing-a-jinja-extension-f8eed5fe3ef5),
[another](https://ron.sh/how-to-write-a-jinja2-extension/)). Heddle's flat, role-based,
attribute-driven branch protocol — with drift diagnostics (`HED3005`/`HED7016`) for
misconfigured custom participants — is the most principled version of extensible control flow I
found in any engine.

**Assessment: novel and well-executed.** The 1.x-era criticism ("no `else`, compose
`@if`/`@ifnot`") is resolved as of 2.0 — note that the README's "How Heddle compares" section
still repeats the stale "no `else`/`elif`" trade-off line, which now contradicts the language
reference. There is no `@switch`/`@case` or pattern matching; roles make one buildable in
user space, which is precisely the design's point.

### 4.8 Output profiles and encoding

2.0 flips the default to `OutputProfile.Html`: bare `@(value)` HTML-encodes, `@raw(value)` opts
out, encoding happens once at the emitting leaf (no double-encoding through containers), and
`@profile()` or host options select the profile. Text mode remains for codegen/JSON.

Industry context: this lands Heddle where Jinja (in Flask), Twig, Django, Razor, and React have
long been — escape by default — and the 2.0 flip mirrors the industry's one-way ratchet (Django
made this move in 2007; Handlebars and EJS still vary). It does **not** reach Go
`html/template`'s *contextual* autoescaping, which parses the surrounding HTML/JS/CSS/URL
context and picks the correct encoder per sink. See [Security](#security-assessment).

**Assessment: correct and well-engineered for the HTML-element context; one class below the
state of the art (Go) for attribute/script/URL contexts.**

### 4.9 Whitespace, imports, and the rest

- **Whitespace**: significant by default; `@\` trims a run; 2.0's `TrimDirectiveLines` swallows
  directive-only lines. This is a cleaner default than Jinja's `trim_blocks`/`lstrip_blocks`
  option matrix or Liquid/Go's per-tag `{%- -%}`/`{{-` dashes, at the cost of being subtler
  (line-granularity, eligibility rules). Good, honest design; the cache-identity participation
  of the option shows attention to detail.
- **Imports**: `@<<{{ path }}` merges definitions at parse time with collision-as-error
  (compose, don't shadow) — good. The coexistence of legacy `@import()` with *different,
  surprising* semantics is the language's most visible wart; the docs say "always use `@<<`,"
  and 2.0 would have been the window to deprecate `@import()` loudly.
- **Recursion** is first-class (definitions call themselves; `MaxRecursionCount` default 100) —
  cleaner than Liquid (no recursion without hacks) and on par with Jinja's recursive loops.
- **Model declaration** (`@model(){{Type}}`, `@using(){{ns}}`) and the `.tcs`-emitted Roslyn
  glue are conventional and fine.

---

## Engine implementation specifics

### 5.1 Pipeline

Lex (ANTLR, 8-mode stack) → parse (SLL with automatic LL fallback + a recorded warning — a
production-grade ANTLR pattern most ANTLR users never implement) → listener walk into a
structured `ParseContext` → compile into `RuntimeDocument` (extension instances + compiled
accessors) → render via struct `Scope`s and a size-adaptive `ScopeRenderer`.

Two properties distinguish this from the industry's two dominant architectures:

- Unlike **interpreters** (Liquid/Fluid/Scriban/Jinja), nothing walks an AST at render time —
  the document *is* the execution plan, and accessors are real compiled delegates.
- Unlike **transpilers** (Razor, Askama, templ), the template is *not* turned into host-language
  source; only expression leaves are. This preserves runtime template loading, hot reload, and
  `dynamic` models — the things transpilers give up ("this comes at the price of being able to
  dynamically change and reload the templates at runtime" —
  [Askama's own trade-off](https://github.com/askama-rs/askama)) — while keeping near-transpiler
  render costs.

This "compiled document" middle path is Heddle's engine-level signature. Scriban's author
[surveyed the design space in 2017](https://xoofx.github.io/blog/2017/11/13/implementing-a-text-templating-language-and-engine-for-dotnet/)
and chose a fast interpreter; Heddle demonstrates the third option is viable.

### 5.2 Rendering and streaming

- Struct `Scope` (model / chained / parent / root / callerData) with aggressively-inlined pure
  transforms; no per-scope heap allocation.
- `Generate(data)` → string; `Generate(data, TextWriter)`; `Generate(data, IBufferWriter<byte>)`
  writing **UTF-8 directly into the sink's spans** (`PipeWriter`-shaped, i.e. ASP.NET Core
  `Response.BodyWriter`). Write-through, caller owns the sink.
- **No async render methods, by design** — render synchronously, let the host flush
  asynchronously. This is a defensible, even elegant position (backpressure belongs to the
  host), and it sidesteps the async-state-machine-per-node cost that plagues async interpreters.
  The cost: no awaiting data *inside* a template (Scriban supports async functions; Razor is
  async throughout). Heddle's stance forces materialized models — which is arguably the better
  architecture anyway, and exactly what StringTemplate's separation doctrine prescribes.

### 5.3 Build-time precompilation

`Heddle.Generator` precompiles `.heddle` files into the application assembly: typed static entry
points (`Views_Home_Index.Generate(model)`), a discovery registry, and a **validation gauntlet**
(options fingerprint, extension bindings, function bindings, optional staleness hash) with an
explicit `Fallback`/`Strict` mismatch policy — silent divergence is treated as the failure mode
to design against, with `HED7101`/`HED7102` callbacks naming every fallback.

Two details are exceptional by industry standards:

1. **The differential guarantee**: the generated backend must produce **byte-identical** output
   to the runtime backend, proven by a differential harness across the fixture corpus on every
   build. Razor's compiled views, Askama, and templ have no runtime twin to diverge from; every
   engine that *does* have two tiers (e.g., precompiled + interpreted fallbacks in various
   engines) that I reviewed treats "close enough" as good enough. Byte-identity as a CI-enforced
   invariant is rigor I have not seen elsewhere in this category.
2. **The cache-not-cage principle**: precompilation is additive; runtime strings, `dynamic`,
   hot reload all keep working, and a registry miss is never an error.

This also underwrites the **T4-successor** use case (sample 8: build-time code generation with
no runtime Heddle dependency) — well-timed, since T4 is effectively abandoned ("T4 isn't well
supported anymore in recent .NET" — [community consensus](https://tim-maes.com/blog/2024/02/25/source-generators-vs-t4/),
[migration reports](https://blog.jermdavis.dev/posts/2023/migrating-t4-to-source-generators)) and
Roslyn source generators, its official successor, are a poor fit for *text* (non-C#) output.

### 5.4 Diagnostics

A numbered, positioned diagnostic system (`HED0003` syntax; `HED1xxx` expressions; `HED3xxx`
branches; `HED4xxx` semantics; `HED5xxx` props/slots; `HED7xxx` generator) with documented
messages, severities, and — for the sandbox — guarantees ("rejected at compile time with a
positioned error — never executed"). Most template engines report a message and a line if
you're lucky; stable diagnostic *codes* are a compiler-culture practice (Rust, Roslyn) that
almost no template engine has adopted. Jinja's own issue tracker and Go's
([#20773](https://github.com/golang/go/issues/20773), [#30635](https://github.com/golang/go/issues/30635))
show what the baseline looks like: years-open issues about insufficient error context.

---

## Head-to-head comparisons

### vs Razor (the .NET incumbent)

| | Heddle | Razor |
| --- | --- | --- |
| Logic model | 3 tiers, sandboxable Native tier | full C#, always |
| Typing | static, per-use-site structural for abstract defs | static, nominal (`@model`) |
| Composition | symmetric definition override, zero-cost | layouts/sections bind backwards; partials/view components with per-call machinery |
| Outside ASP.NET | first-class (console, WASM demo, codegen) | notoriously painful ([fragmented third-party wrappers](https://www.daveaglick.com/posts/the-bleeding-edge-of-razor)) |
| Untrusted templates | Native mode = real sandbox | never |
| Ecosystem/IDE | small; own LSP | enormous; best-in-industry tooling |

Razor remains unbeatable *inside* its home (ASP.NET MVC/Blazor apps with teams who live in
Visual Studio). Heddle's honest wins are: composition without Razor's layout directionality,
rendering outside ASP.NET without the RazorLight/RazorEngineCore compromise zoo, a sandboxed
tier Razor structurally cannot offer, and (self-reported) faster like-for-like rendering with
fewer allocations. Where Razor wins: language familiarity (C# in templates, no new syntax),
IntelliSense depth, sheer ubiquity, and async rendering.

### vs Scriban / Fluid / DotLiquid (the .NET sandboxed family)

These are what .NET teams actually deploy for user-editable templates today: Fluid is "faster
[than] all other well-known .NET Liquid parsers" ([fluid](https://github.com/sebastienros/fluid))
and recommended for "user-editable templates like email templates, CMS page layouts"
([Simple Talk](https://www.red-gate.com/simple-talk/development/dotnet-development/creating-templates-with-liquid-in-asp-net-core/));
Scriban offers a bigger language with an [extensible runtime sandbox](https://github.com/scriban/scriban);
DotLiquid is the venerable, slower port ([benchmarks](https://github.com/sebastienros/fluid)
put it ~5× behind Fluid/Scriban).

Heddle vs this family is a clean trade: Heddle brings static typing, compile-time errors,
compiled render speed, typed components, and compile-time sandboxing — the family brings
Liquid-syntax familiarity (millions of Shopify-trained authors), battle-tested interpreters,
polyglot template portability, async, and *maturity of the untrusted-input posture* (Shopify
Liquid ships render resource limits; Heddle has no render budget — see
[Weaknesses](#weaknesses-and-risks)). For first-party templates, Heddle's offer is strictly
stronger on guarantees. For hostile-author templates, Liquid-family engines remain the safer
*operational* choice today despite Heddle's stronger *language-level* sandbox, purely on budget
enforcement and track record.

### vs Jinja2 / Twig (the mainstream dynamic standard)

Jinja2 is arguably the world's most-liked template language. Heddle matches its headline
features (inheritance/blocks ≈ definitions/overrides; macros ≈ definitions with props; filters ≈
chains; autoescape ≈ Html profile; sandbox ≈ Native tier) and exceeds it on every static
guarantee — Jinja errors are runtime errors, its sandbox is a
[periodically-escaped runtime mechanism](https://hacktricks.wiki/en/pentesting-web/ssti-server-side-template-injection/index.html),
and its extension API is [infamously hard](https://ron.sh/how-to-write-a-jinja2-extension/).
Jinja wins on: ecosystem (every SSG, every ops tool), documentation surface, dynamic
flexibility, and the fact that a decade of Stack Overflow answers exists. A Python-shop
equivalent of Heddle does not exist; that's the point.

### vs Go text/template & html/template

The closest *semantic* relative (relative dot-context, root escape, library-registered
functions) and the sharpest contrast in two places: Go's context model is dynamically typed and
[famously error-prone for newcomers](https://pkg.go.dev/text/template) (Heddle statically
checks the same relative-context idea — a direct fix for Go's biggest template complaint), and
Go's `html/template` does **contextual** autoescaping that Heddle does not attempt. Notably, the
Go community's answer to its template pain was templ — leaving the family-2 design for family 4
— which is directionally the same bet Heddle makes.

### vs templ / Askama / Maud (the typed-compilation wave)

Heddle shares the thesis (types + compilation beat interpretation) and differs in mechanism:
templ/Askama monomorphize *at build time only* and are nominally typed; Heddle keeps a full
runtime compiler, adds build-time precompilation as a cache, and types abstract definitions
*structurally per call site*. Heddle is the only member of this wave that can also load a
template from a database at runtime, and the only one with a sandboxed tier at all. The wave's
engines win on: zero runtime engine (smaller deployment), host-language-native expressions (no
new expression language to learn), and — for templ — a hot community
([5-10× faster than html/template](https://ewen.quimerch.com/articles/14-templ-vs-gomponents/),
strong HTMX pull).

### vs StringTemplate (the purist pole)

Parr's doctrine — templates must not compute, for retargetability's sake
([paper](https://www.cs.usfca.edu/~parrt/papers/mvc.templates.pdf)) — is the position Heddle
politely rejects: Heddle *grades* computation (member → native → C#) instead of banning it.
Interestingly, Heddle satisfies a surprising amount of Parr's checklist anyway (no side effects
from member-tier templates, order-independent definitions, output-grammar flavor), while its
`FullCSharp` tier violates it completely — a violation shared with Razor/ERB/JSP, i.e., with the
mainstream. Given that the logic-less experiment largely failed in practice
([eBay's verdict](https://www.ebayinc.com/stories/blogs/tech/the-case-against-logic-less-templates/);
[the "cult" critique](https://www.boronine.com/2012/09/07/Cult-Of-Logic-less-Templates/)),
graded computation looks like the historically vindicated position.

### vs component frameworks (Blazor, Vue, Svelte)

Not direct competitors (they own interactivity), but the right benchmark for Heddle's
props/slots: Heddle's `out:: Type` slot ≈ `RenderFragment<T>` ≈ scoped slot ≈ snippet
parameter, with compile-time checking that Vue only approximates via tooling and Svelte only
recently achieved by [replacing slots with snippets](https://svelte.dev/docs/svelte/snippet).
For *server-only* HTML composition, Heddle offers most of the component authoring model at a
fraction of the runtime; it has no answer (and claims none) for client interactivity.

---

## Feature matrix

| | **Heddle 2.0** | Razor | Scriban | Fluid/Liquid | Handlebars | Jinja2/Twig | Go html/template | templ/Askama | StringTemplate |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Execution | compiled document | transpiled → C# | interpreted | interpreted | interpreted | interpreted (Twig: compiled→PHP) | interpreted | transpiled → host | interpreted |
| Static typing | ✅ incl. per-site structural | ✅ nominal | ❌ | ❌ | ❌ | ❌ | ❌ | ✅ nominal | ❌ |
| Runtime template loading | ✅ | ⚠️ painful | ✅ | ✅ | ✅ | ✅ | ✅ | ❌ | ✅ |
| Control flow | **library (roles)** | C# keywords | keywords | tags (extensible) | helpers | tags (ext. hard) | keywords | host language | ❌ (by doctrine) |
| Typed props/slots | ✅ | ✅ (components) | ❌ | ❌ | ❌ | ❌ (untyped macros) | ❌ | ✅ props / ⚠️ | ⚠️ untyped args |
| Sandbox for untrusted | ✅ compile-time (Native) | ❌ | ✅ runtime | ✅ by construction | ✅ logic-less | ⚠️ runtime, escaped | ⚠️ funcs only | ❌ n/a | ✅ by doctrine |
| Render resource budgets | ❌ (recursion cap only) | ❌ | ⚠️ limits API | ✅ (Shopify Liquid) | ❌ | ⚠️ | ❌ | ❌ | ❌ |
| Autoescaping | ✅ HTML-uniform (2.0) | ✅ HTML-uniform | ⚠️ opt-in | ✅ | ✅ | ✅ HTML-uniform | ✅ **contextual** | ✅ | ⚠️ renderers |
| Build-time precompile | ✅ + byte-identical differential | ✅ | ❌ | ❌ | ⚠️ precompiled JS | ⚠️ bytecode cache | ❌ | ✅ (only mode) | ❌ |
| Streaming bytes (UTF-8 sink) | ✅ | ✅ | ⚠️ | ⚠️ | ❌ | ⚠️ | ✅ | ✅ | ❌ |
| Async render | ❌ by design | ✅ | ✅ | ✅ | ❌ | ⚠️ | ❌ | ❌ | ❌ |
| LSP / typed completion | ✅ | ✅ (best) | ❌ | ❌ | ❌ | ⚠️ syntax only | ⚠️ | ✅ | ❌ |
| Stable diagnostic codes | ✅ HEDxxxx | ✅ (Roslyn) | ❌ | ❌ | ❌ | ❌ | ❌ | ⚠️ | ❌ |
| Polyglot / portability | ❌ .NET only | ❌ .NET only | ❌ | ✅ Liquid everywhere | ✅ everywhere | ✅ (Py/PHP/ports) | ❌ Go only | ❌ | ⚠️ Java/C#/Py |
| Community size | tiny | enormous | large | large | enormous | enormous | enormous | growing fast | small/legacy |

---

## Security assessment

The industry backdrop is grim: SSTI remains a top web vulnerability class, with
[PortSwigger's foundational research](https://portswigger.net/research/server-side-template-injection)
and [Check Point's 2024 survey](https://research.checkpoint.com/2024/server-side-template-injection-transforming-web-applications-from-assets-to-liabilities/)
documenting template engines as a recurring RCE vector, and sandbox escapes against Jinja2's
`SandboxedEnvironment`, Velocity's uberspector, and FreeMarker's resolver
[still circulating](https://hacktricks.wiki/en/pentesting-web/ssti-server-side-template-injection/index.html).

**Heddle's position, assessed:**

- **The Native-tier sandbox is architecturally superior to runtime sandboxes.** The compiler
  "can only ever emit invocations of" getters passing the visibility/`[Hidden]` filter,
  host-registered functions, and a short intrinsic list; method-call syntax, `new`, casts,
  lambdas, and unregistered names are compile-time errors, never executed. There is no runtime
  interceptor to confuse and no reflection gadget reachable from template text. This converts
  SSTI from "find a sandbox escape" into "find a compiler bug that emits an uncompiled call" — a
  much harder class. The frozen-registry rule (immutable after first compile) closes the
  registry-mutation race. This design deserves to be better known.
- **`FullCSharp` is correctly framed as all-or-nothing.** With Roslyn enabled, a template is
  code; the docs never pretend otherwise. This matches Razor's posture and is honest.
- **Remaining gaps, in order of severity:**
  1. **No render resource budgets.** A hostile-but-sandboxed template can still allocate
     unbounded output (`@for(range(0, hugeN))`, nested `@list` over large models). Shopify
     Liquid ships enforced render limits for exactly this reason; a `TemplateOptions` render
     budget (output bytes / node steps / wall time) is the missing piece before Heddle's
     sandbox claim covers *availability* as well as *confidentiality/integrity*.
  2. **Uniform, not contextual, HTML escaping.** `OutputProfile.Html` HTML-encodes leaves; it
     does not know whether a value lands in an attribute, a `<script>` block, or a URL. Go's
     `html/template` remains the only mainstream engine doing full contextual autoescaping;
     Heddle is at the Jinja/Twig/Razor level here. Injecting into an unquoted attribute or
     JS string context is still an author-responsibility zone.
  3. **Getter execution surface.** Member-tier access executes arbitrary property getters on
     exposed models. `[Hidden]` filtering exists, but the safe-by-default pattern (expose
     dedicated DTOs, never live domain objects) is convention, not enforcement — same as every
     engine, worth stating loudly in docs.

**Net: B+.** Ahead of everything in .NET for language-level sandbox rigor; behind Shopify
Liquid operationally (budgets) and Go structurally (contextual escaping).

---

## Performance assessment

Heddle's performance story is *structural* and I find it credible, with one reservation.

**Structurally**, the engine avoids every known template-engine tax: no per-render parse
(interpreters), no reflection at render (DotLiquid's classic sin), no per-component
activation/DI/section machinery (Razor's documented overhead), string building through a
high-water-mark buffer, struct scopes, `ICollection.Count` pre-sizing, and a UTF-8
`IBufferWriter<byte>` path that skips string materialization entirely. Precompilation removes
the one heavy phase (ANTLR + expression trees + optional Roslyn) from startup. These are the
same levers that put Fluid and Scriban [at the top of the interpreted
benchmarks](https://github.com/sebastienros/fluid) and templ/Askama
[5–10× ahead of runtime engines](https://ewen.quimerch.com/articles/14-templ-vs-gomponents/) —
Heddle pulls all of them at once.

**The reservation:** the "renders faster than Razor with fewer allocations" claim rests on the
repository's own BenchmarkDotNet suite (a realistic composed page, `[MemoryDiagnoser]`,
like-for-like Razor twin — a well-constructed benchmark), but no *third-party* numbers exist,
and Heddle does not appear in the community benchmark suites where Fluid/Scriban/Handlebars.NET
are measured. Publishing results against those baselines (not just Razor) would materially
strengthen the claim. Compile cost is honestly disclosed as non-trivial ("compile once, render
many") and benchmarked separately.

**Net: A− for architecture, pending independent verification of the headline numbers.**

---

## Tooling and ecosystem

**Tooling (A−, remarkable for the project's size):** an LSP (`heddle-lsp`, .NET tool) whose
completions are projections of the real compiler pipeline — "completion is *correct*, not
heuristic," including per-use-site errors inside abstract definitions at the offending call
position — plus a VS Code extension, TextMate grammar, Ace web mode with a JS build of the same
ANTLR grammar (drift-guarded in CI), a WASM demo, and ten golden-tested samples that double as
integration tests. Very few template engines at *any* scale have typed completion; in the
string-template family, essentially only Razor does, and Razor needed Microsoft to get there.

**Ecosystem (D+):** one implementation, one platform, one primary author
(github.com/multiarc/Heddle), no Stack Overflow corpus, no third-party extensions registry, no
production testimonials, and a bus factor of ~1. Every language assessment must weigh this
heavily: language quality does not adopt itself, and the .NET niche Heddle targets already has
an entrenched incumbent (Razor) on one side and two healthy sandboxed engines (Scriban, Fluid)
on the other. History (StringTemplate: better-principled than its era, permanently niche) shows
design excellence is not sufficient. Heddle's most defensible beachheads are the gaps *between*
the incumbents: typed templates outside ASP.NET, the T4-successor role, and
sandboxed-but-typed user templates.

---

## Weaknesses and risks

Ranked by how much they should worry an adopter:

1. **Ecosystem concentration risk** (above). Mitigation: the precompiled path leaves apps with
   generated code that keeps working even if the project stalls.
2. **Learnability.** The sigil inventory (`@`, `@@`, `@%…%@`, `{{}}`, `<>`, `->`, `:`, `::`,
   `@\`, `@<<`, `@{…}@`, `@:`) is large; `:` alone means chain, inheritance, and prop
   separator, while `::` means type annotation, root reference, and slot declaration —
   context-dependent overloading the docs must (and do) spend a cheat sheet on. Right-to-left
   chains invert pipe intuition. The descend-vs-step-back rule is per-extension knowledge.
   None of these are wrong individually; together they make the first week harder than
   Liquid's first hour. The counterweight: the LSP and positioned diagnostics catch essentially
   every consequence of confusion at compile time.
3. **No render budgets** for the untrusted-template scenario (see Security).
4. **Uniform escaping only** — attribute/JS/URL contexts remain author responsibility.
5. **Legacy `@import()`** with different, surprise-laden semantics still present alongside
   `@<<`; 2.0 was the natural removal window and it survived.
6. **Doc drift**: the README's comparison table still claims "no `else`/`elif`," contradicted
   by the 2.0 branch-set feature it ships — a small thing, but reviewers notice.
7. **Single slot per definition** and literal-only prop defaults cap the component model
   short of Vue/Blazor multi-slot ergonomics.
8. **No i18n story** (message catalogs, pluralization) — built-ins are deliberately
   invariant-culture; fine for the codegen half of the audience, a gap for the SSR half.
9. **No async render** — defensible, but disqualifying for teams whose data layer forces
   awaiting inside composition (they must restructure to materialize models first).

---

## What is genuinely unique

Having surveyed the field, I rank Heddle's claims to novelty as follows:

1. **Abstract, late-bound definitions with per-call-site structural static typing.**
   Compile-time monomorphization of template sections — "C++ templates for markup" — appears in
   no other template engine I could find, in any ecosystem. Nominal-typed engines (Razor,
   Askama, templ) compile once against one declared type; dynamic engines don't check at all.
   This single feature enables "one section, N model shapes, all statically verified," and it is
   real, implemented, and surfaced correctly in the LSP. **Unique.**
2. **Role-based, attribute-driven branch sets** — control flow where custom extensions join
   `@if`'s own else-chains on equal footing, with adjacency semantics, orphan diagnostics, and
   drift warnings. Liquid's tags-as-library comes closest but hard-codes its continuations.
   **Unique in mechanism; rare even in spirit.**
3. **Three-tier expression surface with a compile-time-enforced sandbox** where the function
   registry is the trust boundary and disallowed constructs are never compiled. Scriban/Jinja
   sandboxes are runtime interceptors; logic-less engines have no expressions to sandbox.
   **Unique as a graded, single-syntax design.**
4. **The byte-identical precompilation differential** — two backends, one CI-enforced equality
   invariant across a fixture corpus. An engineering practice more than a language feature, and
   one I have not seen in any competing engine. **Unique as far as I can determine.**
5. **The unified `chained` channel** (loop index = pipe value = projected content) —
   distinctive and defensible, though closer to "unusual design economy" than to a capability
   others lack.
6. Honorable mentions that are rare but not unique: symmetric zero-cost layout composition
   (Jinja inheritance minus directionality, plus types), typed slots in a string engine
   (component frameworks have them; string engines don't), comments valid mid-token,
   SLL→LL fallback parsing, stable diagnostic codes.

---

## Verdict

**As a language:** Heddle is a serious, coherent piece of language design — not a syntax skin
over an interpreter, but a considered position in the template-engine design space that
resolves the industry's central tension (power vs. safety) with *graded* rather than binary
answers, and that imports the last decade's component-model lessons (typed props, typed slots,
symmetric composition) into server-side text templating with stronger static guarantees than
the component frameworks themselves had until recently. Its abstract-definition typing is a
genuine contribution to the genre. It pays for all this with density: this is a language that
rewards its learning curve rather than one that flattens it.

**As an engine:** the compiled-document architecture legitimately occupies the unclaimed middle
between interpreters and transpilers — transpiler-class render mechanics with
interpreter-class runtime flexibility — and the engineering discipline around it
(differential-tested precompilation, gauntlet-guarded fallback, positioned diagnostics,
compiler-backed LSP) is, for a project of this size, the most impressive thing in the
repository.

**Should anyone use it?** For performance-sensitive, first-party .NET rendering by a team that
values types and component composition — yes, on the merits, with eyes open about the ecosystem
risk. For the T4-shaped codegen niche — it may be the best currently maintained option in .NET.
For untrusted user templates — the language-level sandbox is the strongest in .NET, but hold
until render budgets exist; use Fluid/Scriban meanwhile. For polyglot stacks, CMS theming, or
teams that need hiring liquidity in their template language — no; that was never the target.

**Final opinion:** the industry's template engines have historically forced a choice between
being safe (Liquid), being powerful (Razor), being fast (Fluid), being typed (Askama), or being
composable (Vue). Heddle is the most complete attempt I have reviewed to be all five in one
small language — approximately **90% successful on the design, with its remaining risk almost
entirely concentrated in adoption rather than architecture**.

---

## Sources

Industry documentation, benchmarks, and commentary referenced in this assessment:

- [Fluid — .NET Liquid engine (benchmarks vs Scriban/DotLiquid/Handlebars.NET)](https://github.com/sebastienros/fluid)
- [Scriban — fast, safe .NET scripting/templating engine](https://github.com/scriban/scriban)
- [xoofx — Implementing a Text Templating Language and Engine for .NET (Scriban design notes)](https://xoofx.github.io/blog/2017/11/13/implementing-a-text-templating-language-and-engine-for-dotnet/)
- [Khalid Abuhakmeh — Scriban for Text and Liquid Templating in .NET](https://khalidabuhakmeh.com/scriban-for-text-and-liquid-templating-in-dotnet)
- [Shopify Liquid — language site](https://shopify.github.io/liquid/) and [Shopify/liquid (design goals: safe, non-evaling, stateless)](https://github.com/shopify/liquid)
- [DotLiquid — safe templating for .NET](https://www.dotliquid.org/)
- [Simple Talk — Liquid templates in ASP.NET Core with Fluid (user-defined template rendering)](https://www.red-gate.com/simple-talk/development/dotnet-development/creating-templates-with-liquid-in-asp-net-core/)
- [Dave Glick — The Bleeding Edge of Razor (Razor outside ASP.NET)](https://www.daveaglick.com/posts/the-bleeding-edge-of-razor)
- [eBay Engineering — The Case Against Logic-less Templates](https://www.ebayinc.com/stories/blogs/tech/the-case-against-logic-less-templates/)
- [Alexei Boronine — Cult of Logic-less Templates](https://www.boronine.com/2012/09/07/Cult-Of-Logic-less-Templates/)
- [Sam Pullara — Mustache is logic-less but the logic has to go somewhere](https://javarants.com/mustache-is-logic-less-but-the-logic-has-to-go-somewhere-3e976b7a49c8)
- [Terence Parr — Enforcing Strict Model-View Separation in Template Engines (WWW2004)](https://www.cs.usfca.edu/~parrt/papers/mvc.templates.pdf) and [StringTemplate](https://www.stringtemplate.org/articles.html)
- [Go text/template documentation (dot-context errors)](https://pkg.go.dev/text/template) and [html/template](https://pkg.go.dev/html/template)
- [golang/go #27926 — proposal: hardened html/template](https://github.com/golang/go/issues/27926), [#20773 — parse failures lack context](https://github.com/golang/go/issues/20773), [#30635 — context representation in errors](https://github.com/golang/go/issues/30635)
- [PortSwigger Research — Server-Side Template Injection](https://portswigger.net/research/server-side-template-injection)
- [Check Point Research — SSTI: Transforming Web Applications from Assets to Liabilities (2024)](https://research.checkpoint.com/2024/server-side-template-injection-transforming-web-applications-from-assets-to-liabilities/)
- [HackTricks — SSTI (engine-by-engine payloads and sandbox escapes)](https://hacktricks.wiki/en/pentesting-web/ssti-server-side-template-injection/index.html)
- [Jinja — Extensions documentation](https://jinja.palletsprojects.com/en/stable/extensions/), [Ron — How to Write a Jinja2 Extension](https://ron.sh/how-to-write-a-jinja2-extension/), [Shang Liang — My experience writing a Jinja extension](https://medium.com/horlu/my-experience-of-writing-a-jinja-extension-f8eed5fe3ef5)
- [GeeksforGeeks — Comparing Jinja to Other Templating Engines](https://www.geeksforgeeks.org/python/comparing-jinja-to-other-templating-engines/)
- [Askama — type-safe Jinja-based templates compiled to Rust](https://github.com/askama-rs/askama), [Leapcell — Rust template engines: compile-time vs run-time vs macro trade-offs](https://leapcell.io/blog/rust-template-engines-compile-time-vs-run-time-vs-macro-tradeoffs), [INNOQ — Type-safe HTML templates in Java and Rust](https://www.innoq.com/en/blog/2024/07/typesafe-html-templates-java-rust/)
- [templ — typed HTML components for Go](https://templ.guide/), [Mike Sahari — Modern Templating for Go with Templ](https://blog.mikesahari.com/posts/html-templating/), [Ewen Quimerc'h — Templ vs Gomponents](https://ewen.quimerch.com/articles/14-templ-vs-gomponents/)
- [Svelte — {#snippet} docs](https://svelte.dev/docs/svelte/snippet), [Full Stack SvelteKit — Svelte slots and migrating to snippets](https://fullstacksveltekit.com/blog/svelte-slots)
- [Alois Seckar — Vue scoped slot props and how to type them](https://dev.to/aloisseckar/vuejs-tips-scoped-slot-props-and-how-to-type-them-lph)
- [Tim Maes — Source Generators: The End of T4 Templates?](https://tim-maes.com/blog/2024/02/25/source-generators-vs-t4/), [Jeremy Davis — Migrating a T4 Template to a Source Generator](https://blog.jermdavis.dev/posts/2023/migrating-t4-to-source-generators), [Microsoft Q&A — T4 in .NET Core and above](https://learn.microsoft.com/en-us/answers/questions/574341/t4-text-templating-in-net-core-and-above)

Repository sources: [README](../README.md), [language-reference.md](language-reference.md),
[native-expressions.md](native-expressions.md), [built-in-extensions.md](built-in-extensions.md),
[architecture.md](architecture.md), [precompilation.md](precompilation.md),
[csharp-api.md](csharp-api.md), [editor-support.md](editor-support.md),
[samples/README.md](../samples/README.md), [CHANGELOG.md](../CHANGELOG.md), and the grammar and
engine source under [src/](../src).
