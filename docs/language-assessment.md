# Heddle — Language & Engine Assessment

*An independent research report on the Heddle template language and engine, assessed against the
wider template-engine industry.*

- **Assessed revision:** branch `feature/lang_asmt`, July 2026 (the 2.0 line; docs and code as of
  commit `702a26d0`).
- **Method:** full read of the published documentation set ([language reference](language-reference.md),
  [architecture](architecture.md), [built-in extensions](built-in-extensions.md),
  [native expressions](native-expressions.md), [C# API](csharp-api.md), [patterns](patterns.md),
  [precompilation](precompilation.md), migration guides, spec/records), inspection of the grammar
  and engine source under [src/](../src), and web research into comparable languages and engines
  (Razor, Scriban, Fluid, DotLiquid, Handlebars, Shopify Liquid, Jinja2/Twig, Go
  `text/template`/`html/template`, Mustache, JSX/TSX, Vue, Askama, Templ, T4). External sources are
  linked inline and collected in [Sources](#sources).
- **Independence note:** this report was written without consulting the previous
  `language-assessment.md` (deleted in the `Clean-up` commit); overlaps with it are convergent, not
  inherited.

---

## 1. Executive summary

Heddle is a **compiled, statically typed template language for .NET** whose entire control-flow
vocabulary — `if`, `else`, `list`, `for`, layouts, partials — is *library*, not grammar. Templates
compile to an execution-ready in-memory document (extension objects wired to pre-compiled value
accessors), render without reflection or re-parsing, and can optionally be pre-compiled into the
application assembly by a Roslyn source generator with a byte-identical-output guarantee.

**The headline judgment:** Heddle is a genuinely original design executed with unusual engineering
rigor, occupying a niche no incumbent fills — *typed, component-composable, compile-once
render-many templating for first-party .NET rendering* — while paying a real, honest ergonomic tax
for its small orthogonal core, and carrying the adoption risks of any single-author language.

Five findings drive that judgment:

1. **The extension-only core is unique in the industry.** No mainstream engine — not Razor, not
   Liquid, not Jinja2, not Go templates — lets a user-defined construct join an `if`/`elif`/`else`
   chain on equal footing with the built-ins. Heddle's `[BranchRole]` mechanism does exactly that,
   because the built-ins themselves are ordinary extensions.
2. **Abstract definitions are C++-template-style monomorphization applied to templates** — a
   reusable section with no declared type is re-compiled and *statically type-checked against the
   concrete model at every call site*. Among template engines surveyed, nothing else does this:
   typed engines (Razor, [Askama](https://github.com/askama-rs/askama),
   [Templ](https://blog.mikesahari.com/posts/html-templating/), TSX) require declared types or
   generics; dynamic engines don't check at all.
3. **The engine is fast where it claims to be, and the claim is unusually honest.** The benchmark
   suite holds four competing engines to *byte-identical output* via a parity assertion before any
   timing — a methodological standard vendor benchmarks (including
   [Scriban's own](https://cscoder.cn/docs/3th/scriban/benchmarks.html)) rarely meet. On the
   documented 2026-07-11 run Heddle rendered 2.0× faster than the next engine (Fluid) with the
   least allocation, at the cost of a ~70× more expensive one-time compile.
4. **The ergonomic tax is real and partly self-inflicted.** Relative context with a per-extension
   "descend vs. step back" rule, right-to-left chains in an industry standardized on left-to-right
   pipes, `{{ … }}` meaning the *opposite* of what two decades of Liquid/Jinja/Mustache trained
   authors to expect, and a heavily overloaded colon family all raise the learning curve. The docs
   mitigate this with unusual candor, but the tax does not disappear.
5. **The dominant risk is ecosystem, not engineering.** One author, .NET-only, latest released tag
   `v1.0.1` with 2.0 still forming on this branch, and incumbent alternatives that are "good
   enough" for most teams. Technical quality is not the bottleneck to adoption; distribution is.

**Best fit:** performance-sensitive, first-party .NET server-side rendering and build-time code
generation by teams that value typed templates and component-style composition.
**Poor fit:** untrusted user-supplied templates as the primary use case, polyglot stacks, teams
wanting a large ecosystem.

---

## 2. What Heddle is

Heddle ships as six packages ([README](../README.md)): the core engine (`Heddle`), the ANTLR
grammar package (`Heddle.Language`), a build-time source generator (`Heddle.Generator`), editor
language services and an LSP server (`Heddle.LanguageServices`, `Heddle.LanguageServer`), and a
CLI (`Heddle.Tool`) positioned as a T4 successor. The engine targets
`netstandard2.0;net6.0;net8.0;net10.0`.

Footprint, measured on this revision:

| Metric | Value |
| --- | ---: |
| Hand-written C# (engine + generator + language services + LSP, excl. generated/ANTLR output) | ~24,700 LOC |
| Grammar | 363-line lexer + 108-line parser + 276-line C# token grammar (ANTLR 4.13) |
| Built-in extensions | 24 |
| Test methods (xUnit, all test projects) | ~770 |
| Runnable golden-asserted samples doubling as CI integration tests | 10 |
| Released version | v1.0.1 (2.0 unreleased, forming on this branch) |

A status caveat: the repo is internally inconsistent about 2.0's release state — the
[CHANGELOG](../CHANGELOG.md) says 2.0 "has **not** shipped yet (the latest released tag is
`v1.0.1`)" while [spec records](spec/records.md) speak of "the released 2.0.0 source (2026-07)"
and the migration guides say snippets are "verified to compile against **Heddle 2.0.0**". This
report assesses the 2.0 feature set as present in the branch.

---

## 3. The language

### 3.1 One sigil, one form: control flow as a library

Heddle's grammar knows five things: text, `@`-prefixed output chains, `@% … %@` definition blocks,
`@<<` imports, and raw regions. Everything else — conditionals, loops, formatters, partials,
layout regions — is an extension resolved by name. The
[parser grammar](../src/Heddle.Language/HeddleParser.g4) is 108 lines; there is no `if` token
anywhere in it.

This is the design decision everything else follows from, and it is Heddle's most distinctive
trait. Every comparable engine hard-codes control flow in the grammar or a privileged tag layer:

- **Razor** — `@if`, `@foreach`, `@switch` are C# keywords transpiled by the Razor compiler; you
  cannot add a keyword.
- **Liquid / Jinja2 / Twig** — `{% if %}`, `{% for %}` are tags; custom tags exist but are a
  separate registration surface, and a custom tag cannot join the built-in `if`/`elsif`/`else`
  chain.
- **Go templates** — `{{if}}`, `{{range}}` are actions baked into the parser; extension happens
  only through data functions (`FuncMap`), never new control structures.
- **Handlebars / Mustache** — block helpers get closest (control flow *is* helper-shaped), but
  helpers are dynamically dispatched, untyped, and the `{{else}}` inverse-block plumbing is
  special-cased in the runtime rather than an open protocol.

Heddle goes further than all of these in two ways. First, the branching protocol is *open*: the
built-in `@if`/`@elif`/`@else` declare their role in a branch set via a public `[BranchRole]`
attribute (opener / continuation / terminal) and coordinate through a published
`Scope.Publish`/`TryRead` local-context channel — so a custom extension with the same attributes
[joins a set on equal footing](custom-extensions.md#building-your-own-branch-set), can satisfy an
`@else`, and even interoperates with the built-in family in one set. Second, type information flows
through the extension protocol at compile time (`InitStart` receives and returns `ExType`), so
library-provided control flow is still statically typed — the thing that usually forces control
flow into the grammar in typed systems.

**Assessment:** this is the closest a template engine has come to the Lisp property that the
language's own constructs are not privileged. The practical payoff is real (project-specific
verbs like region collectors and asset helpers read identically to built-ins; the engine grows by
adding a class, not changing the grammar), and the risk usually attached to open-ended
extensibility — incoherent dialects per project — is mitigated by the strong typing and the
positioned-diagnostics discipline. A genuine innovation.

### 3.2 Relative context, `::` root, and the step-back rule

Member paths resolve against a *current model* that narrows as calls descend — the Go
`text/template` and Mustache model, not Razor's absolute `Model.X`
([reference](language-reference.md#context-and-data-flow)). `::Member` jumps to the root from any
depth. So far, conventional.

The controversial part is the **step-back rule**: iteration and component calls *descend* into
their parameter, but conditionals and formatters (`@if`, `@date`, `@money`, …) treat the parameter
as a value and render their body one level back, in the *caller's* context. Without it,
`@if(IsFeatured){{ @(Title) }}` would absurdly resolve `Title` against a boolean. With it, the
body does what the author almost always wants — but *which* behavior applies is a per-extension
fact that the docs must maintain as an authoritative
[classification table](built-in-extensions.md#body-context-at-a-glance).

Industry comparison: Go's `{{with}}`/`{{range}}` *always* rebind dot and `{{if}}` *never* does —
the same pragmatic split, but keyed to three fixed keywords rather than an open extension set, and
community experience with Go templates suggests even that simpler rule is a recurring source of
confusion (Hugo's docs spend [substantial effort](https://gohugo.io/templates/introduction/) on
"context" for exactly this reason). Heddle's version is more principled (the classification is
part of each extension's declared contract, and the context stays *statically typed* through every
transition — something neither Go nor any Liquid dialect offers) but also more demanding: the rule
surface scales with the extension vocabulary.

**Assessment:** the right default semantics with an above-average cognitive price. The one-level
limit (no `..` parent path; only `::` root or explicit passing) is a good call — engines that
allow arbitrary upward navigation (JSP EL, some Velocity idioms) produce templates that are
impossible to refactor. Compile-time typing of the context is the redeeming differentiator: a
misresolved path is a positioned compile error, not a silently empty render as in Liquid or a
runtime panic as in Go.

### 3.3 The chained channel: one mechanism where others have three

Alongside the model, a single **chained** value flows through renders: it is the loop index inside
`@list`/`@for`, the piped value inside a `:` chain, and the caller's projected content inside
`@out()` ([reference](language-reference.md#the-second-channel-chained-data)). Other engines cover
these with three unrelated features (e.g. Liquid: `forloop.index` magic variable + `|` filters +
`{% include %}` parameters; Razor: `foreach` locals + method calls + `RenderBody`).

The unification is elegant and the compiler tracks the chained *type* end-to-end. The cost is the
chain's direction: `@a():b():c()` composes **right-to-left** (`c` runs first), which is function
composition — but the entire industry pipes left-to-right (Liquid/Jinja filters, Unix pipes, F#
`|>`, Elixir, JS method chains). The [Liquid migration guide](coming-from-liquid.md) frankly
labels this "the same pipeline, read in reverse". Right-to-left is *consistent* with the language
(the leftmost call renders, mirroring nesting), but it is a permanent friction point for every
author arriving from anywhere else, and the docs' own guidance ("compose definitions rather than
piping one scalar through a long chain") concedes that long chains don't read well.

### 3.4 Definitions, inheritance, override — symmetric composition

Definitions (`@% <name> {{ … }} %@`) are Heddle's components: invoked like extensions, they accept
a model, an inline body (surfaced by `@out()`), typed named props, and one typed slot. Two design
choices stand out against the industry:

**Symmetric layouts.** In Razor, the *page* declares sections and sets `Layout`; the layout
consumes them via `@RenderSection`/`@RenderBody`, which pins the rendered entry point to the final
page and makes nested layouts notoriously awkward (a sub-layout must
[re-declare and forward every section](http://bryblog.com/razor-nested-layouts-and-sections), and
an undeclared section [throws at runtime](https://www.learnrazorpages.com/razor-pages/files/layout)).
Jinja2's `{% extends %}` has the same directionality. Heddle inverts this: a layout is an ordinary
definition exposing regions as overridable sibling definitions; a page imports it (`@<<`) and
*calls* it. Nothing about the layout knows the page; any page can serve as a base for another; and
the whole composition compiles into one execution-ready document with no render-time indirection.
This is a cleaner composition algebra than any layout system surveyed.

**Document-order layered overrides.** Re-declaring `<name:name>` replaces a definition *from that
point onward*, so a template can render a region, redefine it, and render again — a cascade-like
mechanism with no equivalent in Razor/Liquid/Jinja (Jinja's `{% block %}` override is
whole-template, not positional). Powerful, though order-dependence is a subtle-bug surface the
compiler only partially guards (the `HED4002` double-render warning is a good example of guarding
the adjacent mistake).

**The multi-slot gap.** A definition has exactly one content slot; multiple named regions are done
with sibling-definition overrides ([patterns](patterns.md#components-with-multiple-content-regions)).
The idiom works and stays typed, but it is more ceremony than Vue's named slots, Blazor's multiple
`RenderFragment` parameters, or Web Components' named `<slot>` elements. This is the most visible
expressiveness gap versus the component-framework state of the art.

### 3.5 Abstract definitions: monomorphized, structurally-typed templates

A definition without `:: Type` is *abstract*: its body is compiled against whatever concrete model
reaches each call site, and each use is independently type-checked
([reference](language-reference.md#abstract-definitions-and-late-type-binding)). The docs' own
analogy is exact: this is the **C++ template** model — compile-time duck typing with per-use
monomorphization — not C# generics
([background](https://caiorss.github.io/C-Cpp-Notes/CPP-template-metaprogramming.html)).

Placed against the industry, this is Heddle's second genuine invention. The clear 2024–2026 trend
is toward compile-time-typed templates — [Askama](https://github.com/askama-rs/askama) (Rust,
Jinja-like, "generates type-safe Rust code at compile time"),
[Templ](https://blog.mikesahari.com/posts/html-templating/) (Go, "type errors caught at
compile-time, not runtime"), TSX components
([typed contracts end to end](https://blog.openreplay.com/tsx-rise-typed-frontend/)) — but all of
these bind a template to *one declared* model type (or explicit generics). Heddle is alone in
offering *structural* reuse: one untyped `panel` section serves `Blog` and `Article` alike, each
call site statically verified, no type parameters declared. Dynamic engines (Liquid, Handlebars,
Mustache) get the same reuse with zero checking; declared-type engines get the checking with less
reuse; Heddle gets both.

The known failure mode of C++-style instantiation — errors surfacing at the use site in
potentially confusing form — is bounded here by scale (template bodies are small, and diagnostics
carry template positions), and the inheritance narrowing rule (`<child:base>` may only narrow to
assignable types) gives the mechanism a disciplined story that raw C++ templates lacked until
concepts.

### 3.6 Typed props and the typed slot

2.0 adds named props with types and literal defaults
(`<card(style: string = "plain", compact: bool = false)>`), passed by name and validated with
positioned compile errors (missing/unknown/mistyped/duplicated → `HED5001–HED5009`), plus one
typed slot (`out:: Type`) whose caller body compiles against the slot type and renders once per
`@out(expr)` ([reference](language-reference.md#props-nameprop-type--default)).

This is the Vue *scoped slot* pattern — child owns the data, caller owns the presentation — but
statically compiled. Notably, in Vue itself scoped-slot prop typing is a recognized weak spot
("TypeScript support is limited … typed so broadly that it is effectively of any type",
[community guidance](https://dev.to/aloisseckar/vuejs-tips-scoped-slot-props-and-how-to-type-them-lph));
TSX gets full typing but only by abandoning a template DSL for the host language. For a standalone
template language to deliver compile-checked component contracts (props *and* slot content) puts
Heddle ahead of every template DSL surveyed and at parity with TSX on this axis. The restrictions
(literal-only defaults, one slot, no shorthand positional props) are conservative but coherent.

### 3.7 Three expression tiers

`ExpressionMode` gates what call parentheses accept: `MemberPathsOnly` → `Native` (default) →
`FullCSharp` ([native expressions](native-expressions.md)). The native tier is the interesting
one: C# operator precedence verbatim, the full literal set, null-safe member hops, registered
functions as the *only* callable surface, compiled to `System.Linq.Expressions` with zero Roslyn —
and a documented, deliberate list of seven deviations from C# semantics.

This tiering lands precisely on the "less-logic" compromise the industry converged on after the
logic-less wars — Mustache-style purity produces
[bloated view models](https://tech.ebayinc.com/engineering/the-case-against-logic-less-templates/),
full host-language access produces unauditable templates; structural logic plus a curated function
whitelist is the middle
([the "less-logic" position](https://dev.to/cocoroutine/truth-about-template-engines-3a7)). Liquid
and Fluid embody the same philosophy dynamically; Heddle's version is statically compiled and the
whitelist (`FunctionRegistry`, frozen on first compile) is a crisper trust boundary than Liquid's
filter registration. The escape hatch to full C# — real Roslyn compilation of C# 14 expressions
inside the template, with `model`/`root`/`chained` in scope — is more powerful than anything
comparable outside Razor itself.

One deliberate absence deserves praise: there is no `?.` because member hops are already
null-safe, and no string interpolation because `+`/`format` cover it — the design consistently
prefers *removing* a decision to adding a feature.

### 3.8 Output profiles and encoding — safe by default, but manual by context

2.0 flips the default to `OutputProfile.Html`: bare `@(value)` HTML-encodes, `@raw` opts out.
This matches the industry's settled norm (Razor, Go `html/template`, Handlebars, Twig, Jinja2
under Flask all encode by default) and closes 1.x's XSS-by-default gap. The pluggable
`TemplateOptions.Encoder` with a byte-identical legacy default is a careful compatibility story.

The gap versus the state of the art is **context awareness**. Heddle provides per-context
encoders — `@attr`, `@js`, `@url` — that the *author must remember to use* in attribute, script,
and URL positions. Go's `html/template` set the gold standard here in 2011:
[contextual auto-escaping](https://blog.vghaisas.com/go-autoescaping/) parses the surrounding
HTML/CSS/JS/URI context and picks the escaper automatically, an approach Google advocated as the
systemic XSS fix ([Google security blog](https://security.googleblog.com/2009/03/reducing-xss-by-way-of-automatic.html)).
Heddle's manual approach matches OWASP's per-context guidance and is honest about the element-text
limit of the default encoder, but a template author who writes `<a title="@(Tooltip)">` gets
element-text encoding in an attribute context with no diagnostic. Given that Heddle already parses
its templates with a 13-mode context-sensitive lexer, context-aware encoding (or at least a
lint-tier diagnostic for value output in attribute/script positions) is the single most valuable
security feature it could add.

### 3.9 Syntax ergonomics — the tax, itemized

The language is learnable and internally consistent, but four decisions compound into a real
onboarding tax:

1. **`{{ … }}` is a body, not interpolation.** Two decades of Mustache/Handlebars/Liquid/Jinja
   trained authors that `{{x}}` prints x; in Heddle it prints *braces*. The
   [Liquid migration guide](coming-from-liquid.md) itself calls this "the number-one misread".
2. **No `@@` escape.** A literal `@` needs a raw region (`@{…}@` or `@:`); Razor's `@@` doubling
   is the expected affordance and its absence (a deliberate compile error) surprises.
3. **Colon overloading.** `:` is the chain delimiter, *and* the inheritance separator, *and* the
   prop-type separator; `::` is the root reference, *and* the type annotation, *and* the slot
   declarator. All uses are grammatically unambiguous, but the reader must carry the mode in their
   head — a symptom the docs acknowledge by needing a
   [symbol cheat sheet](language-reference.md#symbol-cheat-sheet).
4. **Whitespace significance** with two mitigation mechanisms (`@\` and `TrimDirectiveLines`) —
   better than Liquid's `{%- -%}` dash zoo after the 2.0 default flip, but still a topic every
   author meets.

None of these is fatal; all are documented with unusual honesty (the two "coming from" guides are
exactly the right artifact). But a language whose pitch includes ergonomic superiority over Razor
should expect the first week with it to be *harder* than Razor, not easier — the payoff arrives
with composition scale.

### 3.10 Grammar and parsing

The lexer is a 13-mode ANTLR mode-stack machine that embeds a **complete C# 14 expression token
grammar** (all string forms including raw/interpolated-raw, digit separators, verbatim
identifiers) inside template parentheses — a technically impressive and rarely attempted feat; the
usual industry approach (Razor) is a hand-written incremental parser co-developed with the C#
compiler team. Comments-on-the-hidden-channel work in every mode (comments mid-construct), and the
parser runs SLL-first with LL fallback for speed. The same grammar generates the C# engine parser
and the JavaScript parser behind the Ace web editor — one source of truth for engine and tooling,
which most template DSLs (whose editor grammars are approximate TextMate regexes) never achieve.

---

## 4. The engine

### 4.1 Compilation model

Heddle occupies a deliberate middle point between the industry's two poles:

| Model | Engines | Cost profile |
| --- | --- | --- |
| Interpret an AST per render | Liquid, DotLiquid, Fluid, Scriban, Jinja2, Go templates | Cheap compile, per-render dispatch |
| Transpile whole template to host code | Razor, Askama, Templ, JSX | Expensive build step, fastest render |
| **Execution-ready object document + compiled accessors** | **Heddle** (dynamic backend) | Mid compile (~265 μs), near-transpiled render |

The template's *structure* becomes an object graph of pre-resolved, pre-typed extension instances;
only the *value accessors* are code-generated (expression-tree delegates for member paths, Roslyn
delegates for embedded C#) ([architecture](architecture.md#4-5-compilation)). Reflection happens
once at compile; statically-typed renders never reflect. This gets most of the transpilation
payoff without a build step — and then the source generator (below) adds the build step as an
option rather than a requirement.

### 4.2 Render path

The render engine reads like a checklist of modern .NET performance practice: a readonly-struct
`Scope` with aggressively-inlined transforms (no per-scope heap allocation), a single streaming
renderer with a per-instance high-water-mark buffer, `ICollection<T>.Count` pre-sizing,
`ISpanFormattable`/`IUtf8SpanFormattable` value formatting without intermediate strings, and three
sinks — `string`, `TextWriter`, and `IBufferWriter<byte>` with a surrogate-safe UTF-8 transcode —
the last matching ASP.NET Core's `Response.BodyWriter` shape for zero-materialization SSR.

The **no-async-render** decision is defensible and well-argued (render synchronously, let the host
flush asynchronously via `PipeWriter`); it is also a genuine limitation for templates that would
await data mid-render — a scenario Razor supports and Heddle explicitly scopes out (data belongs
on the model). Scriban, notably, does offer `RenderAsync`. For the target workload (model fully
materialized before render) the sync-only choice is the faster architecture.

### 4.3 Build-time precompilation — the strongest engineering story in the repo

`Heddle.Generator` compiles `.heddle` files to C# `IProcessStrategy` classes at build time with:

- a **byte-identical output guarantee** versus the dynamic engine, proven by a differential
  harness across the whole fixture corpus in CI;
- a **validation gauntlet** at runtime lookup (options fingerprint, extension bindings, function
  bindings, staleness) with an explicit `Fallback`-with-diagnostic vs `Strict`-throw policy —
  designed around the failure mode of silent fallbacks re-adding compile cost;
- typed entry points (`Heddle.Generated.Home.Generate(model)`) with optional pre-encoded
  UTF-8 static pieces (`"…"u8`);
- a trim/feature switch (`Heddle.CSharpTierEnabled=false`) that lets the linker drop the entire
  Roslyn dependency graph — how the in-browser WASM demo ships Roslyn-free;
- extension and function binding **by reference, never inlined**, so security patches in
  referenced packages reach precompiled templates.

The framing — "a cache seeded at build time, never a cage" — is exactly right, and the dual-backend
byte-parity discipline exceeds what comparable dual-mode systems document (Razor's runtime vs
compiled views have never carried a byte-parity guarantee; Handlebars precompilation is
JS-ecosystem folklore). Combined with the `heddle` CLI, this is a credible successor story for T4,
which the ecosystem widely regards as end-of-life
([community consensus](https://tim-maes.com/source-generators-vs-t4.html),
[migration reports](https://blog.jermdavis.dev/posts/2023/migrating-t4-to-source-generators)) — and
Heddle's data-driven CLI covers the *template-shaped* codegen T4 did better than raw source
generators do (source generators are C#-shaped, not template-shaped).

### 4.4 Untrusted templates — a candid second-tier sandbox

The sandbox story has four legs (Native tier + frozen `FunctionRegistry` + DTO models/`[Hidden]` +
`RenderBudget`), and its documentation is remarkable for including a real **evasion analysis**:
zero-output loops and value-context accumulation escape the output/ops counters entirely, so
`MaxRenderTime` is documented as *the only universal backstop* ([C# API](csharp-api.md#render-budgets)).
Most engines' security docs assert; Heddle's proves and qualifies.

Against the industry: Shopify Liquid remains the reference design for hostile-author templating
(the language is *incapable* of expressing computation over anything but handed-in data), and the
OWASP/SSTI literature's first rule — never let user input become template source in a
full-power engine — is why Jinja2/Twig/Freemarker SSTI is a
[celebrated RCE class](https://portswigger.net/web-security/ssti) and why sandboxes there are
["bypassable but raise the bar"](https://hacktricks.wiki/en/pentesting-web/ssti-server-side-template-injection/index.html).
Heddle's Native tier is architecturally sounder than a retrofitted sandbox (the compiler *cannot
emit* calls outside the whitelist — rejection is at compile time, not interception at runtime),
but the engine is one `ExpressionMode.FullCSharp` misconfiguration away from arbitrary C#, and the
README correctly labels the model "all-or-nothing … not a sandbox" in its comparison table. The
honest positioning — untrusted templates are a *supported secondary* scenario, not the design
center — is the right one.

### 4.5 Tooling

For a niche language, the tooling surface is exceptional: a hand-rolled LSP 3.17 server whose
completions/hover/diagnostics are *projections of the real compiler pipeline* (typed member
completion inside `@(...)`, per-use-site errors in abstract definitions at the exact causing
position, go-to-definition across `@<<` imports), a VS Code extension, a TextMate grammar, an Ace
mode generated from the same ANTLR grammar, an in-browser WASM playground with a typed language
worker, and the CLI. Compiler-backed (rather than heuristic) editor intelligence is something
Liquid/Scriban/Handlebars authors simply do not have, and even Razor achieves it only inside
Visual Studio's dedicated tooling. For a single-author project this breadth is almost alarming —
it is a maintenance surface bet — but as shipped it is a differentiator.

### 4.6 Engineering-culture signals

Worth recording because they bear on trustworthiness of the whole artifact:

- **~770 test methods** across engine/generator/LSP/CLI, plus ten golden-asserted sample apps run
  as CI integration tests, plus a differential precompiled==dynamic gate.
- A **spec discipline** unusual outside standards bodies: breaking-change "windows" with as-shipped
  reconciliation records (including two items that *slipped* the window, documented with
  disposition), a claimed-diagnostic-ID registry, an append-only amendments ledger
  ([records](spec/records.md)).
- **Stable diagnostic IDs** (`HEDxxxx`) shared by runtime compiler and build-time generator.
- **Documented sharp edges**, in the docs rather than an issue tracker: the file watcher's filter
  bug (recompile-on-change currently inoperative), the not-fully-synchronized disposal counter,
  the DEBUG-only model-type check in `Generate`, `TryCompilation` not running Roslyn finalization.

The willingness to document defects and benchmark caveats in the primary docs is the single
strongest credibility signal in the repository.

---

## 5. Performance

The repository benchmarks Heddle against Fluid, Scriban, DotLiquid, and Handlebars.Net under a
**byte-identical-output parity assertion**, plus Razor rendering a larger non-parity page
(flagged as indicative only). Documented run of 2026-07-11 (Ryzen 9 9950X, .NET 10,
BenchmarkDotNet 0.15.8; ~55.5 KB component-heavy composition workload;
[artifacts committed](benchmarks/2026-07-11)):

| Engine | Render | Ratio | Allocated | Cold compile |
| --- | ---: | ---: | ---: | ---: |
| **Heddle** | **32.50 μs** | 1.00 | **227.86 KB** | 264.99 μs |
| Fluid 2.31.0 | 64.88 μs | 2.02 | 231.98 KB | 3.65 μs |
| Handlebars.Net 2.1.6 | 69.76 μs | 2.17 | 227.59 KB | 8,287 μs |
| DotLiquid 2.3.197 | 178.21 μs | 5.55 | 404.69 KB | 7.21 μs |
| Scriban 7.2.5 | 376.71 μs | 11.73 | 1,154.34 KB | 4.68 μs |
| Razor (larger page, not parity-checked) | 65.55 μs | 2.04 | 263.51 KB | — |

Three observations an independent reader should weigh:

1. **The methodology is better than the industry norm.** Parity-checked output before timing
   removes the classic vendor-benchmark trick of comparing unequal work; committing raw
   BenchmarkDotNet artifacts with environment headers makes the run reproducible and auditable.
   Very few engine-published benchmarks meet this bar.
2. **Results are workload-dependent, and this is one workload.** Scriban's own suite shows Scriban
   [1.2–14× faster than other engines](https://cscoder.cn/docs/3th/scriban/benchmarks.html) on its
   simple product-list workload; here it is 11.7× *slower*. Both can be true: this workload is
   composition-heavy (layout import, definition overrides, a dozen component calls), which is
   precisely the shape Heddle's pre-wired document excels at and interpreters pay dispatch on. The
   fair statement is: *on component-composition SSR workloads, Heddle's architecture wins clearly;
   on trivial string-substitution workloads the gap will narrow or invert, and no such counter-
   benchmark is published here.*
3. **The compile-cost trade is honest and acceptable for the target.** 265 μs vs single-digit μs
   for the Liquid engines is ~70× — irrelevant under compile-once-render-many caching, dominant
   for compile-per-render misuse. (Handlebars.Net at 8.3 ms shows incumbents can be far worse;
   slow compilation is a [known Handlebars.Net pain point](https://github.com/Handlebars-Net/Handlebars.Net/issues/407).)
   Precompilation removes even this cost at runtime.

The structural explanations offered (no per-call activation/DI/section buffers versus Razor;
streaming high-water-mark buffer; struct scopes) are consistent with the measured allocation
numbers and with community experience that Razor's per-partial overhead
[mounts up on component-heavy pages](https://hudosvibe.net/post/death-by-thousands-partial-views).

---

## 6. Competitive landscape

| | Execution | Typing | Untrusted-safe | Composition model | Context-aware encoding | Ecosystem |
| --- | --- | --- | --- | --- | --- | --- |
| **Heddle** | Compiled document (+ optional build-time codegen) | **Static, per use site (structural)** | Secondary (Native tier + budgets) | Definitions, inheritance, override, typed props/slot, symmetric layouts | Manual (`@attr`/`@js`/`@url`) | .NET, tiny |
| Razor / Blazor | Transpiled to C# | Static (declared `@model`/generics) | No | Layouts/sections (directional), partials, tag helpers / components | Element-text auto only | .NET, huge |
| Scriban | Interpreted | Dynamic | Partial (scripting language w/ limits) | Includes, functions | Manual | .NET, large |
| Fluid | Interpreted | Dynamic | **Yes** (Liquid semantics, allowlisted members) | Partials/includes | Manual | .NET, large (Orchard) |
| DotLiquid / Shopify Liquid | Interpreted | Dynamic | **Yes** (design center) | Partials, layout tags | Manual | Polyglot, huge |
| Handlebars(.Net) | Compiled delegates | Dynamic | Partial (logic-less + helpers) | Partials, block helpers | Manual | Polyglot, huge |
| Jinja2 / Twig | Interpreted (bytecode) | Dynamic | Sandbox retrofit (SSTI-prone) | `extends`/blocks (directional), macros | Autoescape (element-text) | Python/PHP, huge |
| Go `html/template` | Interpreted | Dynamic (runtime reflect) | Author-trusted only | Nested templates, blocks | **Automatic contextual** | Go, huge |
| Mustache | Interpreted | None | **Yes** (logic-less) | Partials | No | Polyglot, huge |
| JSX/TSX (SSR) | Transpiled to JS | **Static (declared)** | No | Components, typed props/children, slots | Element-text auto | JS, dominant |
| Askama (Rust) / Templ (Go) | **Build-time codegen** | **Static (declared)** | No | Includes/components | Element-text auto | Small but growing |

Reading the table: Heddle's column is the only one combining *compiled execution, static typing,
and library-defined control flow* — and the only statically typed row whose typing is structural
rather than declared. Its two weakest cells (untrusted-safety as design center; contextual
encoding) are exactly where Fluid/Liquid and Go `html/template` respectively remain the right
tools.

---

## 7. What is genuinely unique — an opinionated ranking

1. **Open, role-based branch sets.** User extensions joining `if`/`elif`/`else` semantics via
   `[BranchRole]` + a published scope channel, with adjacency stripping and orphan diagnostics
   applying uniformly. No surveyed engine has an equivalent; this is the cleanest demonstration
   that "control flow as library" is real and not a slogan.
2. **Abstract definitions (per-call-site monomorphization).** Statically checked structural reuse
   of template sections across model shapes — unmatched in template DSLs, and differentiated even
   against the compile-time-typed newcomers (Askama, Templ, TSX), which all require declared types.
3. **The single chained channel.** Loop index, pipe value, and projected slot content as one typed
   mechanism instead of three features.
4. **Symmetric, zero-cost layout composition.** Layouts as ordinary callees; pages as reusable
   bases; overrides layered in document order — a strictly better composition algebra than
   Razor sections or Jinja `extends`, and the benchmark shows it costing nothing at render.
5. **Dual-backend byte-parity discipline.** A source-generated backend proven byte-identical to
   the dynamic engine by differential CI, guarded at runtime by a fingerprint gauntlet with
   explicit fallback policy.
6. **Parity-asserted competitive benchmarking** with committed raw artifacts — a methodological
   standard the industry (including much larger projects) rarely meets.
7. **One grammar, every surface.** The same ANTLR source feeding the engine, the LSP, the web
   editor, and the WASM demo — with a full C# 14 expression grammar embedded in template context.

## 8. What holds it back

1. **The cognitive tax** (§3.9, §3.2, §3.3): per-extension descend/step-back knowledge,
   right-to-left chains, the `{{ }}` false friend, colon overloading. Individually defensible,
   collectively the reason first impressions will lag the design's actual quality.
2. **Manual context encoding.** Fifteen years after Go shipped contextual autoescaping, a new
   HTML-first engine that already owns a context-sensitive lexer should not leave
   attribute/script/URL encoding to author discipline. This is the highest-value gap to close.
3. **Ecosystem and continuity risk.** One author; latest release v1.0.1 with the entire 2.0 story
   unreleased; internal docs disagreeing on 2.0's status; ANTLR runtime + (optional) Roslyn
   dependency weight; no third-party user base evident. The tooling breadth (LSP, generator, CLI,
   WASM demo) multiplies the maintenance surface a single maintainer must carry.
4. **Known engine sharp edges**, documented but unresolved: the inoperative file-change watcher,
   best-effort disposal under concurrent renders, release-mode skipping model-type validation,
   `TryCompilation` not covering Roslyn finalization.
5. **Single-workload performance evidence.** The claim is credible and honestly framed, but one
   composition-shaped page is not a performance characterization; a trivial-substitution and a
   large-loop workload would make the story robust against the obvious rebuttal.
6. **"Good enough" incumbents.** Razor for .NET apps (with the entire Microsoft tooling
   ecosystem), Scriban/Fluid for text and untrusted templates, source generators for codegen. To
   displace any of them, Heddle must win on its *combination* — which requires the buyer to value
   typed composition and render cost simultaneously. That buyer exists (design systems /
   component-heavy SSR, email/report rendering at scale, T4 refugees) but is a niche.

---

## 9. Scorecard and verdict

| Dimension | Score | Rationale |
| --- | :-: | --- |
| Language design coherence | 9/10 | Small orthogonal core; every feature composes; deliberate omissions. |
| Language ergonomics / learnability | 6/10 | Dense sigils, contrarian conventions; excellent migration docs partially compensate. |
| Type system | 9/10 | Structural per-site checking, typed props/slots, typed chains — beyond any template DSL. |
| Runtime performance | 9/10 | Best-in-class on the measured (relevant) workload; honest trade-offs. |
| Security model | 7/10 | Sound compile-time sandbox + candid budgets; manual context encoding; FullCSharp foot-gun. |
| Engine engineering quality | 9/10 | Parity discipline, tests, diagnostics registry, documented defects. |
| Tooling | 8/10 | Compiler-backed LSP, generator, CLI, WASM demo — exceptional for the size; VSIX-only distribution. |
| Documentation | 9/10 | Complete, grammar-grounded, self-critical; minor version-status inconsistencies. |
| Ecosystem / adoption readiness | 3/10 | Single author, pre-2.0, .NET-only, no visible community. |

**Overall opinion.** Heddle is an *auteur language*: a single, strongly held idea — that a template
language needs no keywords, only a typed extension protocol — pursued to its logical end with a
level of engineering discipline (byte-parity dual backends, parity-checked benchmarks, spec
ledgers, self-documented defects) that most well-funded projects do not reach. The idea pays off:
the composition model is objectively better than Razor's, the typing story is ahead of every
template DSL in any ecosystem, and the render-path performance validates the architecture. The
price is a language that reads like nothing else — which is both its moat and its barrier. As
technology, this is an 8.5/10 piece of work. As a product, its fate rests on factors outside the
code: releasing 2.0, closing the contextual-encoding gap, publishing broader benchmarks, and
finding the component-heavy .NET SSR / T4-successor audiences for whom the combination — not any
single feature — is the purchase. Nothing else in the industry currently offers that combination.

---

## Sources

Repository documents: [README](../README.md) · [CHANGELOG](../CHANGELOG.md) ·
[language-reference.md](language-reference.md) · [architecture.md](architecture.md) ·
[built-in-extensions.md](built-in-extensions.md) · [native-expressions.md](native-expressions.md) ·
[csharp-api.md](csharp-api.md) · [custom-extensions.md](custom-extensions.md) ·
[patterns.md](patterns.md) · [precompilation.md](precompilation.md) ·
[coming-from-razor.md](coming-from-razor.md) · [coming-from-liquid.md](coming-from-liquid.md) ·
[editor-support.md](editor-support.md) · [spec/records.md](spec/records.md) ·
[samples/README.md](../samples/README.md) · [benchmarks/2026-07-11](benchmarks/2026-07-11) ·
grammar and engine source under [src/](../src).

External references:

- [Scriban repository](https://github.com/scriban/scriban) and
  [Scriban benchmarks](https://cscoder.cn/docs/3th/scriban/benchmarks.html)
- [Fluid repository](https://github.com/sebastienros/fluid) ·
  [DotLiquid vs Scriban comparison](https://dotnet.libhunt.com/compare-dotliquid-vs-scriban)
- [Implementing a Text Templating Language and Engine for .NET (xoofx)](https://xoofx.github.io/blog/2017/11/13/implementing-a-text-templating-language-and-engine-for-dotnet/)
- [Scriban for Text and Liquid Templating in .NET (Khalid Abuhakmeh)](https://khalidabuhakmeh.com/scriban-for-text-and-liquid-templating-in-dotnet)
- [HTML templating engine discussion (.NET community)](https://www.answeroverflow.com/m/1376686241600376832) ·
  [awesome-template-engine list](https://github.com/sshailabh/awesome-template-engine)
- [Go text/template package docs](https://pkg.go.dev/text/template) ·
  [Hugo templating introduction](https://gohugo.io/templates/introduction/)
- [Context-aware Autoescaping in Go](https://blog.vghaisas.com/go-autoescaping/) ·
  [Go html/template docs](https://pkg.go.dev/html/template) ·
  [Go HTML Template Autoescaping](https://garbagecollected.org/2024/07/26/go-html-template-autoescaping/) ·
  [Reducing XSS by way of Automatic Context-Aware Escaping (Google Security Blog)](https://security.googleblog.com/2009/03/reducing-xss-by-way-of-automatic.html)
- [PortSwigger: Server-side template injection](https://portswigger.net/web-security/server-side-template-injection) ·
  [OWASP WSTG: Testing for SSTI](https://owasp.org/www-project-web-security-testing-guide/v42/4-Web_Application_Security_Testing/07-Input_Validation_Testing/18-Testing_for_Server-side_Template_Injection) ·
  [HackTricks SSTI overview](https://hacktricks.wiki/en/pentesting-web/ssti-server-side-template-injection/index.html)
- [Razor nested layouts and sections (bryblog)](http://bryblog.com/razor-nested-layouts-and-sections) ·
  [The Razor _Layout.cshtml file (Learn Razor Pages)](https://www.learnrazorpages.com/razor-pages/files/layout) ·
  [ASP.NET MVC 3: Layouts and Sections with Razor (ScottGu)](https://weblogs.asp.net/scottgu/asp-net-mvc-3-layouts-and-sections-with-razor/)
- [Death by thousands partial views](https://hudosvibe.net/post/death-by-thousands-partial-views) ·
  [Partial views in ASP.NET Core (Microsoft Learn)](https://learn.microsoft.com/en-us/aspnet/core/mvc/views/partial?view=aspnetcore-8.0) ·
  [Don't replace your View Components with Razor Components (Andrew Lock)](https://andrewlock.net/dont-replace-your-view-components-with-razor-components/)
- [Using Razor Templates Beyond Web Pages and ASP.NET (Trailhead)](https://trailheadtechnology.com/using-razor-templates-beyond-web-pages-and-asp-net/) ·
  [Email-Templating with Blazor (Fusonic)](https://www.fusonic.net/en/blog/email-templating-blazor)
- [Source Generators: The end of T4 templates? (Tim Maes)](https://tim-maes.com/source-generators-vs-t4.html) ·
  [Migrating a T4 Template to a Source Generator (Jeremy Davis)](https://blog.jermdavis.dev/posts/2023/migrating-t4-to-source-generators) ·
  [CodegenCS (T4 alternative)](https://github.com/Drizin/CodegenCS)
- [Handlebars.Net compilation performance issue #407](https://github.com/Handlebars-Net/Handlebars.Net/issues/407) ·
  [Handlebars.Net repository](https://github.com/Handlebars-Net/Handlebars.Net)
- [The Case Against Logic-less Templates (eBay Engineering)](https://tech.ebayinc.com/engineering/the-case-against-logic-less-templates/) ·
  [Cult of Logic-less Templates (Boronine)](https://www.boronine.com/2012/09/07/Cult-Of-Logic-less-Templates/) ·
  [Truth About Template Engines](https://dev.to/cocoroutine/truth-about-template-engines-3a7) ·
  [mustache(5) manual](https://mustache.github.io/mustache.5.html)
- [C++ template metaprogramming notes (duck typing / monomorphization)](https://caiorss.github.io/C-Cpp-Notes/CPP-template-metaprogramming.html) ·
  [Duck Typing vs. Type Erasure (nullprogram)](https://nullprogram.com/blog/2014/04/01/)
- [TSX and the Rise of Typed Frontend Components (OpenReplay)](https://blog.openreplay.com/tsx-rise-typed-frontend/) ·
  [Using JSX on the server as a template engine (Evert Pot)](https://evertpot.com/jsx-template/) ·
  [React Server Components](https://react.dev/reference/rsc/server-components)
- [Vue slots guide](https://vuejs.org/guide/components/slots.html) ·
  [Typing Vue scoped slot props](https://dev.to/aloisseckar/vuejs-tips-scoped-slot-props-and-how-to-type-them-lph) ·
  [Slots vs Props in UI components](https://dev.to/fyodorio/slots-vs-props-in-ui-components-3b55)
- [Askama — type-safe compiled Jinja-like templates for Rust](https://github.com/askama-rs/askama) ·
  [Rust template engines: compile-time vs run-time trade-offs (Leapcell)](https://leapcell.io/blog/rust-template-engines-compile-time-vs-run-time-vs-macro-tradeoffs) ·
  [Modern Templating for Go with Templ](https://blog.mikesahari.com/posts/html-templating/)
