# Language Assessment & Evolution Direction

A comprehensive re‑assessment of the **Heddle** template language: what each construct is worth,
what it costs, how the language stands against its competitors inside and outside .NET, and —
the point of this document — which directions are open for evolving it and which one to take.
This replaces the earlier candid assessment; the user‑facing docs
([Language Reference](language-reference.md), [Architecture](architecture.md)) remain the
neutral reference. Everything here is grounded in the source as of v1.0.0.

---

## TL;DR

Heddle is a well‑engineered niche language whose core bet — *a tiny extension‑only grammar,
statically typed per‑use‑site compilation, and decoupled component composition* — is the bet
the rest of the industry is now making outside .NET (Templ in Go, Askama in Rust, HEEx in
Elixir, JSX everywhere). Inside .NET that quadrant is occupied only by Razor/Blazor, which are
welded to ASP.NET Core. **The open ground is framework‑independent, typed, component‑style
templating for .NET** — emails, documents, static sites, code generation, SSR outside MVC.

The core is worth keeping essentially as‑is. What limits the language is not the thesis but
four concrete debts, in order of strategic severity:

1. **Raw output by default** — `@(...)` does not HTML‑encode; encoding is opt‑in per extension.
2. **All expression power is welded to Roslyn** (`AllowCSharp`) — which simultaneously blocks a
   sandbox, blocks Native AOT, inflates first‑compile cost, and makes even a comparison
   require full C#.
3. **Missing everyday ergonomics** — no `else`/`elif`, no named arguments/props, a clunky
   counted loop.
4. **Tooling and ecosystem** — no LSP, thin debugging story, single‑author bus factor.

The recommended direction (§6) is **the typed component engine for .NET**, unlocked by one
keystone investment: a **small native expression language** that removes Roslyn from the
common path. That single move improves security, AOT, startup, and ergonomics at once, and it
is the prerequisite for every other direction worth pursuing.

---

## 1. Identity — what Heddle is

Mechanically ([Architecture](architecture.md)):

- A template **compiles to an execution‑ready document** (`RuntimeDocument` /
  `IProcessStrategy`) — a tree of extension instances wired to **compiled** value accessors.
  Member paths (`@(A.B.C)`) become expression‑tree delegates; embedded C# (`@( @… )`) becomes
  Roslyn‑compiled `(model, chained, root)` delegates. No reflection, no re‑parse at render.
- Rendering walks the document and streams through a size‑adaptive buffer. "Compile once,
  render many."

Linguistically, the defining choice is that **the language has almost no built‑in semantics**.
There are no `if`/`for`/`include` keywords — `@if`, `@list`, `@partial` are all *extensions*
(19 ship in the box). The only primitive is a `@`‑prefixed call (optional name, one
parenthesized parameter, optional `{{ }}` body), composed with `:`. The grammar is tiny; the
language grows by adding a class, not by changing the parser. That orthogonality is the spine
everything else hangs from — and, importantly for evolution, **the grammar is fully owned**
(hand‑rolled ANTLR lexer/parser, including the C# token grammar), so surface changes are
constrained only by design taste and back‑compat, not by a third‑party parser.

---

## 2. Feature‑by‑feature valuation

Each construct valued on both sides. Verdicts: **Keep** (asset, leave alone), **Polish**
(asset with a fixable rough edge), **Evolve** (right idea, needs a design investment),
**Rethink** (liability in its current form).

### 2.1 Core model

| Feature | Value | Cost | Verdict |
| --- | --- | --- | --- |
| Extension‑only core (`@name(param){{body}}`) | One uniform primitive; language grows without parser changes; users' directives are peers of built‑ins. | Everything *looks* like a call, so semantics (descend vs. step‑back) are invisible at the call site; discoverability depends on docs. | **Keep** |
| Chain `:` (right‑to‑left composition) | Function composition with typed data flow; wrapper/decorator idiom falls out for free. | Right‑to‑left surprises newcomers; `:` is visually overloaded with `::` and inheritance `:`. | **Keep** (document harder; consider a lint) |
| Chained channel (loop index / pipe value / `@out()` projection) | One mechanism where Razor needs three unrelated features (loop variables, ViewData‑ish side values, `RenderBody`). Compact and typed. | One name for three roles is elegant to the designer and opaque to the reader; `@swap()` is a symptom. | **Keep** |
| Relative context that narrows on descent | Templates read locally (`@(Title)` not `@(Model.Articles[i].Title)`); sections are relocatable, which is what makes composition work. | Mental model tax for Razor emigrants; "what is the model *here*?" is the #1 beginner question. | **Keep** |
| One‑level step‑back for value calls (`@if`, formatters) | Makes the 95% case correct: `@if(flag){{ @(Title) }}` sees the surrounding model. | It is an *invisible* rule — nothing at the call site says whether a call descends or steps back; custom extensions choose freely, so reading unfamiliar templates requires knowing each extension. | **Polish** (surface it in tooling/docs; consider a naming or signature convention for extension authors) |
| `::` root reference | Escape hatch from any depth without `AllowCSharp`; covers app/page context cleanly. | Only two rungs exist — *current* and *root*; anything in between must be threaded by hand (no `..` parent path, deliberately). | **Keep** (the restraint is right; `..` invites fragile coupling) |

### 2.2 Type system

| Feature | Value | Cost | Verdict |
| --- | --- | --- | --- |
| Abstract definitions, monomorphised per use site | The language's most distinctive idea: write a section once, each call site compiles it against the concrete model and is independently type‑checked. C++‑template semantics with zero annotation ceremony. Uncommon and genuinely useful. | Errors surface at the *call site* of a definition written elsewhere; without good diagnostics this is confusing. Per‑site compilation multiplies compile work. | **Keep** (invest in diagnostics that name both the definition and the offending use) |
| `:: Type` annotation | Pins a definition; enables member checking, faster accessors, and the inheritance narrowing rule. | Trailing position (after the body) hides the most important fact about a definition at the bottom. | **Polish** (allow the annotation in the header, e.g. `<card :: Article>`, keeping the trailing form) |
| `:: dynamic` | Honest escape hatch for genuinely late shapes (Expando, anonymous types across assemblies). | Silently forfeits every compile‑time guarantee; nothing in the template distinguishes a typo from a dynamic miss until render. | **Keep** (it's opt‑in and clearly spelled) |

### 2.3 Composition

| Feature | Value | Cost | Verdict |
| --- | --- | --- | --- |
| Definitions + inheritance/override `<child:base>`, layered in document order | The strongest practical differentiator. Layout and page are *symmetric* — no Razor‑style directionality; any template can base any other; overrides are declarative. Compiles flat, so **composition costs nothing at render time**. | Document‑order override is powerful and slightly spooky (later redefinition changes earlier‑written call sites' *future* renders); no way to "seal" a definition. | **Keep** (consider an opt‑in `sealed` marker later; not urgent) |
| `-> chain` default output | Lets a template of pure definitions render itself; the entry‑layout idiom is clean. | The "don't also call it" double‑render trap; `->` is one more sigil. | **Polish** (compile warning when a `->` definition is also called directly) |
| `@out()` content projection | Slots/`RenderBody` for free via the chained channel; multiple named regions are just multiple definitions — genuinely better than Razor sections. | Single content channel per call; parameterized slots (pass data *into* the projected body) have no story. | **Evolve** (named/parameterized slots are the missing piece for a component ecosystem) |
| Imports `@<<{{ path }}` vs `@partial()` | Clean compile‑time (definitions) vs run‑time (rendered output) split. | Two mechanisms to learn; import syntax `@<<{{ }}` is the strangest‑looking construct in the language. | **Keep** semantics, **Polish** surface (accept `@import(){{ path }}` as the documented spelling; `@<<` stays as the terse form) |
| Recursion (bounded by `MaxRecursionCount`) | Trees render naturally; the bound prevents runaways. | — | **Keep** |

### 2.4 Expressions

| Feature | Value | Cost | Verdict |
| --- | --- | --- | --- |
| Member paths → expression‑tree delegates | Fast, null‑safe, statically checked, no Roslyn involved, works with `AllowCSharp = false`. | Paths only — no comparison, arithmetic, indexing, or method call without escalating to full C#. | **Keep** as the floor |
| Embedded C# via Roslyn (full C# 14 expression grammar) | Maximum ceiling — LINQ, lambdas, interpolation; types bind to the real model. The lexer work here is substantial and correct. | The costs compound: (a) **security** — any C# is arbitrary code, so no sandbox is possible; (b) **AOT** — Roslyn emit does not run under Native AOT and expression‑tree `.Compile()` degrades to interpretation, so Heddle is effectively excluded from AOT deployments; (c) **startup** — Roslyn dominates first‑compile cost; (d) **ergonomics** — even `Count > 0` requires `AllowCSharp = true`. | **Keep the ceiling, Evolve the floor** — see §6 keystone |
| *(gap)* No native expression tier | — | Everything between a member path and full C# falls off a cliff. This single gap is what makes the security model all‑or‑nothing and the language Roslyn‑bound. | **Evolve** (the keystone investment, §6) |

### 2.5 Surface syntax

| Feature | Value | Cost | Verdict |
| --- | --- | --- | --- |
| `@` as the only special character, `@@` escape | Plain text stays plain; the rule fits in one sentence. | — | **Keep** |
| Sigil inventory (`@% %@`, `{{ }}`, `< >`, `->`, `::`, `:`, `@<<`, `@{ }@`, `@:`, `@\`) | Each is short and, once learned, unambiguous; the lexer modes make them all context‑correct. | The *set* is large and the punctuation overloaded: `:` is chain, base separator, and half of `::`; `{{ }}` is body, import path, and format string. Real templates read densely; this is the #1 first‑impression tax. | **Polish** (don't redesign — add readable aliases where cheap, spend the budget on syntax highlighting/LSP, which dissolves most of the density in practice) |
| Raw blocks `@{ }@` / `@:`, comments‑anywhere, `@\` trimming | All three are exactly right and cheap; comments working mid‑token is a nice ANTLR‑hidden‑channel dividend. | `@\` is needed in preambles because directives don't auto‑trim their own line. | **Polish** (trim the trailing newline of *whole‑line* directives by default under an opt‑in option; HTML output never notices, text/JSON output gets much cleaner) |
| Whitespace‑significant output | Predictable; what you type is what renders. | Preamble noise (mitigated by the above). | **Keep** |

### 2.6 Output & safety

| Feature | Value | Cost | Verdict |
| --- | --- | --- | --- |
| Per‑extension encoding (`[EncodeOutput]`), `@(...)` raw / `@html(...)`+`@string(...)` encoded | Flexible; correct for a *text* engine that also emits JSON, code, config. | **The default is unsafe for the dominant use case.** The shortest spelling — `@(Title)` — is the raw one; every modern HTML engine (Razor, Jinja2 autoescape, HEEx, JSX) encodes by default. This is an XSS footgun and the first thing a security review flags. | **Rethink** (2.0: per‑template output profile — `html` profile encodes on `@(...)` with `@raw(...)` opt‑out; `text` profile keeps today's behavior. Breaking, hence the 2.0 window.) |
| `AllowCSharp` all‑or‑nothing | Honest: off = safe, on = full trust. Nothing pretends to be a sandbox. | Off is *too* off (no expressions at all), so real templates turn it on, so effectively all real deployments run untrusted‑unsafe. | **Evolve** (falls out of the native expression tier: off no longer means expressionless) |

### 2.7 Runtime

| Feature | Value | Cost | Verdict |
| --- | --- | --- | --- |
| Execution‑ready document, streaming renderer, struct `Scope`, adaptive buffers | Measurably faster than Razor with fewer allocations on the component‑heavy benchmark ([Architecture → Performance](architecture.md#performance-characteristics)); the win is structural (no per‑component activation), so it grows with component density. This is a real, defensible asset. | First compile is expensive (ANTLR + expression trees + Roslyn). | **Keep** (the compile cost is addressed by the build‑time path, §5C) |
| `string Generate(object)` — synchronous, string‑building API | Simple; right for emails/documents/codegen. | No `TextWriter`/`IBufferWriter<byte>` sink, no UTF‑8 path, no async data resolution — all three matter for high‑throughput web SSR, which is otherwise Heddle's best benchmark story. | **Evolve** (additive API, no language change) |
| Native AOT / trimming compatibility | — | Roslyn runtime emit: unavailable. Expression trees: interpreter fallback. Reflection‑based member resolution: trim‑hostile. The .NET ecosystem is moving exactly this way. | **Evolve** (source‑generator compile path, §5C) |

### 2.8 Extensibility & tooling

| Feature | Value | Cost | Verdict |
| --- | --- | --- | --- |
| Extension authoring model (`InitStart` type negotiation, `ProcessData` + `RenderData`, `Scope` transforms) | Far more powerful than a Liquid filter: extensions participate in type flow and choose their context semantics. | The floor is high: a trivial helper needs a class, an attribute set, two render methods, and an understanding of `ExType`. Liquid's "a filter is a lambda" wins the first hour. | **Polish** (ship a `SimpleExtension` base or a `[TemplateFunction]` lambda registration for the value‑in/string‑out case; keep the full contract for real extensions) |
| Editor assets (Ace mode, TextMate grammar, `ProvideLanguageFeatures` token stream) | Better than most niche languages ship at 1.0; the token stream is the seed of an LSP. | No LSP, no completion/hover/go‑to‑definition; template errors don't map to editor squiggles anywhere. | **Evolve** (an LSP is the single biggest adoption lever after safety) |
| Debuggability | Compile errors are collected, not thrown, with positions. | A broken embedded‑C# expression or a per‑site type failure doesn't yet point at *template line + call site chain* as crisply as Razor's generated‑code mapping; render‑time inspection (step through a template) doesn't exist. | **Evolve** |
| Ecosystem / bus factor | Single coherent vision — the language is *designed*, not accreted. | Single author, .NET‑only, no corpus of answers, sophisticated internals (8‑mode lexer, Roslyn codegen) that few can maintain. Every strategic choice below must be weighed against maintenance capacity. | **Structural constraint** — see §6 sequencing |

---

## 3. Competitive landscape

### 3.1 The .NET field

| | **Heddle** | Razor (MVC/Pages) | Blazor SSR components | Scriban | Fluid (Liquid) | Handlebars.Net | T4 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Execution | Compiled document | Compiled (Roslyn, whole page) | Compiled | Interpreted (fast) | Interpreted (fast) | Compiled helpers | Build‑time codegen |
| Typing | **Static, per use site** | Static | Static | Dynamic | Dynamic | Dynamic | Static (it *is* C#) |
| Template logic | Paths now; full C# opt‑in | Full C# | Full C# | Rich sandboxed script | Liquid subset, sandboxed | Logic‑less + helpers | Full C# |
| Untrusted templates | No (or expressionless) | No | No | **Yes** | **Yes** | Mostly | No |
| Composition | **Symmetric inheritance + override, free at runtime** | Layouts/sections/partials (directional) | Components + parameters + `RenderFragment` | Includes/functions | Includes | Partials | None to speak of |
| Named parameters/props | **No** | Yes (tag helpers/partials via model) | **Yes (typed props)** | Yes (named args) | Filter args | Hash args | n/a |
| Framework coupling | **None** | ASP.NET Core | ASP.NET Core | None | None | None | VS/MSBuild |
| AOT/trimming | Poor today | Poor (runtime compile) / OK (build‑time) | OK | OK | **Good** | OK | **Good** (build‑time) |
| Ecosystem | Tiny | Enormous | Large | Healthy | Healthy | Healthy | Legacy |

Readings:

- **vs Razor/Blazor** — the only .NET engines in Heddle's quadrant (compiled + typed +
  components), and both are chained to ASP.NET Core hosting, DI, and tooling. Razor outside a
  web app is genuinely unpleasant (RazorLight et al. exist because of it). Heddle's decoupled
  composition and per‑component render cost beat Razor's; Razor's tooling, debugging, and
  hiring pool beat Heddle's by an order of magnitude. **Don't fight Razor inside MVC; own
  everything outside it.**
- **vs Scriban/Fluid** — they own untrusted/user templates and will keep them unless Heddle
  ships a sandboxable expression tier; even then they retain the polyglot‑Liquid familiarity.
  Their weakness is exactly Heddle's strength: no static typing, no compile‑time member
  checking, weaker composition.
- **vs Handlebars.Net/Mustache** — logic‑less philosophy, polyglot reach. Different religion;
  not a direct competitor for typed first‑party rendering.
- **vs T4 / source generators** — T4 is the incumbent for build‑time text codegen and it is
  unloved (no composition, VS‑bound, ugly). A build‑time Heddle (§5C) is a credible T4
  successor with a *vastly* better language.

### 3.2 Conceptual neighbors outside .NET

These matter more for *direction* than competition — they show where typed templating is
converging:

- **Templ (Go)** — templates compile to Go functions; components are typed functions with
  ordinary parameters; LSP ships with the tool. Validates: build‑time compilation + named,
  typed props + first‑class tooling.
- **Askama/Maud (Rust)** — templates type‑checked at build time against structs; zero runtime
  cost. Validates: the compile‑time direction, and that users accept strictness for safety.
- **HEEx (Elixir/Phoenix)** — compile‑time validated HTML, components with declared
  attributes and **named slots**, encode‑by‑default. Validates: slots + safety defaults.
- **JSX/TSX, Svelte** — components won; props are named and typed; composition is the whole
  model. Validates: Heddle's composition thesis, and highlights its missing named props.
- **Jinja2/Twig** — the interpreted world's high‑water mark: block inheritance (Heddle's is
  better), macros, autoescape‑by‑default (Heddle lacks), sandbox mode (Heddle lacks).

The industry consensus across ecosystems: **compiled, typed, component‑shaped, safe by
default, with real tooling**. Heddle already has the first three at a level most engines
don't; it is missing the last two.

### 3.3 Where the open ground is

Plotting typed↔dynamic against framework‑coupled↔independent: the *typed + independent*
quadrant in .NET is **empty except for Heddle**. Concrete underserved workloads:

- transactional email / notification rendering (today: Razor‑out‑of‑band hacks or Liquid,
  losing typing);
- document/report/export generation;
- code generation and scaffolding (T4's territory);
- static site generation in .NET;
- high‑throughput SSR fragments outside MVC (the benchmark's own story);
- AOT‑deployed services — *nobody* serves typed templates well there yet, including Heddle
  today.

---

## 4. Weaknesses ranked by strategic severity

1. **Raw‑by‑default output** — disqualifying for the HTML use case in any security‑conscious
   org; also the cheapest reputational loss ("failed our review in five minutes"). Fix in the
   2.0 breaking window via output profiles.
2. **Expression power welded to Roslyn** — the root cause behind four separate symptoms: no
   sandbox, no AOT, slow first compile, and `Count > 0` requiring full‑trust C#. One
   investment (§6 keystone) resolves all four.
3. **Everyday ergonomics** — no `else`/`elif` (the most‑felt gap in real templates), no named
   arguments/props (caps the component story that is supposed to be the differentiator), a
   `ForModel`‑shaped counted loop that requires C# to construct.
4. **Tooling** — no LSP/completion/diagnostics‑in‑editor; per‑use‑site type errors *need*
   good tooling more than ordinary engines do, because the error's cause and location are in
   different files by design.
5. **Ecosystem & bus factor** — single author, .NET‑only, no corpus. Not fixable by fiat;
   fixable only by picking a niche small enough to win and documenting relentlessly (already
   underway).
6. **Sigil density** — real but overweighted by first impressions; highlighting + an LSP
   defuse most of it. Spend here last.

---

## 5. Direction options

### A. The typed component engine for .NET *(double down on composition)*

Own the empty quadrant of §3.3: typed, fast, framework‑independent rendering for emails,
documents, SSR fragments, static sites. **Demands:** named/typed props, parameterized slots,
encode‑by‑default HTML profile, `else`, LSP. **Wins:** teams already typing their models who
hate stringly‑typed templates; the benchmark story sells itself. **Risk:** medium — it's an
extension of what exists; the competition (Razor‑out‑of‑band) is genuinely weak.

### B. Safe embeddable templating *(compete with Scriban/Fluid for user templates)*

A sandboxed tier: native expressions over whitelisted members, resource limits, no C#.
**Demands:** the native expression language *plus* member whitelisting, quotas, and a security
posture worth auditing. **Wins:** SaaS products embedding user templates. **Risk:** high —
security is a promise that must be kept forever, the incumbents are good and free, and
Heddle's typing advantage matters least exactly here (user templates bind late by nature).

### C. Build‑time compilation / AOT *(the T4 successor)*

A source generator (or MSBuild task) that compiles `.heddle` at build time into C# — real
stack traces, zero startup cost, Native‑AOT‑ and trimming‑clean, debuggable by stepping into
generated code. **Demands:** a second compiler backend (emit C# instead of building the
runtime document) — significant but well‑bounded work; the parse/walk front end is shared.
**Wins:** AOT services, codegen users, everyone who resents T4. **Risk:** medium — a second
backend is a maintenance commitment for a one‑maintainer project; mitigated by sharing the
front end and treating the runtime path as the reference semantics.

### D. Conservative polish *(ergonomics only, no strategic bet)*

`else`, loop sugar, docs, warnings; no new tiers or backends. **Wins:** current users.
**Risk:** lowest effort, highest opportunity cost — none of §4's top two debts move, and the
language stays confined to its current audience.

These are not mutually exclusive — they share a dependency structure, which is the basis of
the recommendation.

---

## 6. Recommendation

**Primary direction: A — the typed component engine for .NET — with C as its second phase and
B kept open but not promised.** A is where Heddle's genuine differentiators (per‑site typing,
free composition, render speed) buy the most, against the weakest competition. C compounds A
(AOT + debuggability + startup are all adoption objections A will hit). B is only credible
after the keystone exists, and should be re‑evaluated then rather than committed to now.

### The keystone: a native expression tier

One design investment unlocks every direction: a **small, statically typed expression
language** evaluated without Roslyn — comparisons, boolean logic, arithmetic, string
concatenation/interpolation, indexing, null‑coalescing, and calls to registered functions,
compiled to the same expression‑tree delegates member paths already use (and, in the §5C
backend, to plain C#). Consequences:

- `@if(@Comments.Count > 0)` works with `AllowCSharp = false` → the security model gains a
  real middle tier (path/native/full‑C#), making a future sandbox (B) *possible*;
- Roslyn leaves the common path → first‑compile cost drops, and the AOT story (C) stops being
  blocked by design;
- `AllowCSharp = true` remains the unchanged full‑power escape hatch — the ceiling is not
  lowered, the floor is raised.

The grammar is hand‑rolled and already lexes C# expressions; a native tier is a *subset*
carve‑out, not new lexer territory.

### Sequenced roadmap

**Phase 0 — the 2.0 breaking window (safety + ergonomics).** Small, high‑visibility, mostly
independent items; ship together because two are breaking:

1. **Output profiles**: `html` profile encodes `@(...)` by default with `@raw(...)` opt‑out;
   `text` profile preserves today's semantics. *(Breaking; the reason 2.0 exists.)*
2. **`else` / `elif`** — recognized after an `@if` body (the `CALL_RETURNED` lexer mode
   already dispatches on what follows a call; this is the natural place to hang it).
3. **Counted‑loop sugar** — `@for(@5)` / range forms so `ForModel` construction stops
   requiring C#.
4. Compile **warning** for calling a `-> ` definition directly (double render).
5. Optional line‑trim for whole‑line directives.

**Phase 1 — the keystone.** The native expression tier, plus **named arguments/props** for
definitions (`@card(Article, style: "wide")` with props declared in the definition header) and
**parameterized slots**. This is the release that makes "component engine" true, and it is
where grammar design time should be spent most carefully.

**Phase 2 — the build‑time backend (§5C).** Source generator emitting C#; Native AOT and
trimming supported; template debugging via generated code. Runtime path remains for dynamic
scenarios and stays the semantic reference.

**Phase 3 — tooling.** LSP built on the existing `ProvideLanguageFeatures` token stream:
completion for members (the type information already exists at compile time — few template
languages can offer *correct* completion; Heddle can), hover types, go‑to‑definition across
imports, inline per‑use‑site error mapping. Plus the `SimpleExtension`/lambda registration
floor for extension authors.

Each phase is independently shippable and independently valuable — important for a
one‑maintainer project: stopping after any phase leaves the language strictly better.

### Non‑goals (explicit)

- **Replacing Razor inside ASP.NET MVC** — unwinnable, unnecessary.
- **Polyglot ports** of the engine — the JS parser stays a *highlighting* artifact only.
- **A logic‑less mode** — contradicts the thesis; Mustache exists.
- **Widening the sigil redesign** — aliases and tooling, not a new surface; the installed
  base and docs are worth more than a prettier `@<<`.

---

## 7. Open design questions

1. **`else` surface**: block‑suffix (`@if(x){{ … }} @else {{ … }}`) vs chain‑integrated —
   must compose with `@ifnot` and chains without ambiguity in `CALL_RETURNED`.
2. **Props declaration syntax**: in the definition header (`<card(style: string, compact: bool)>`)
   vs annotation‑style; how props interact with the single model parameter and abstract typing.
3. **Native expression tier boundary**: does it allow *any* method calls (e.g. `.Count()`,
   `.ToUpper()`) via a registered‑function whitelist, or properties/indexers only? The answer
   determines whether B (sandbox) is ever credible — resolve before Phase 1 freezes.
4. **Encoding profile granularity**: per template, per compile options, or per file extension
   (`.heddle.html` vs `.heddle.txt`)?
5. **Async/streaming API**: `IBufferWriter<byte>`/UTF‑8 sink alongside `string Generate` —
   Phase 2 or opportunistic earlier?

---

## Verdict

The 1.0 assessment stands: high ceiling, limiting floor. What this re‑assessment adds is that
the ceiling is *strategically placed* — Heddle is already in the quadrant the industry is
moving toward, in an ecosystem where that quadrant is otherwise empty. The core language needs
almost no revision (§2 verdicts are overwhelmingly Keep/Polish); the work is to fix the unsafe
default, raise the expression floor, and let tooling dissolve the surface tax. Direction A,
keystone first.
