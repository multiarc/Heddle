# Language Assessment (candid)

A frank engineering evaluation of the template language (currently **TTL**, the Templater
engine) — strengths, weaknesses, and how it stacks up against the engines people usually weigh
it against. This is opinion grounded in the source, not marketing; the user‑facing docs
([Language Reference](language-reference.md), [Architecture](architecture.md)) are the neutral
reference.

> **Confidence/caveats.** This is a read‑only assessment from the grammar, compiler, extensions,
> and fixtures. Performance claims come from the repo's own benchmark
> ([src/Templates.Performance](../src/Templates.Performance)); debuggability and source‑mapping
> were reasoned about, not measured. Treat those two as informed inference.
>
> **Naming note.** The project is slated to be renamed to **Heddle** (language, packages,
> extension). This file uses the current names (TTL/Templater) and will be swept by that rename.

---

## TL;DR

A well‑engineered, intellectually honest **niche** language with a coherent, distinctive thesis:
*a tiny orthogonal core where control flow is library, plus full compiled & statically‑typed
expressions, plus an inheritance/late‑binding composition model.* It out‑expresses the
logic‑less engines, matches Razor on power while offering a cleaner composition story, and is
measurably faster. Its costs are real and mostly **not** about the engine's quality: high sigil
density, a few missing ergonomic basics, an all‑or‑nothing security model, and a small
ecosystem / bus‑factor. Best for performance‑sensitive, first‑party .NET rendering by a team
that values typed templates and component composition; poor for untrusted templates, polyglot
stacks, or teams wanting batteries‑included tooling.

---

## What it actually is (mechanics)

- **Compiles to an execution‑ready document**, not to interpreted templates and not to a single
  whole‑template assembly. `TtlCompiler` builds a `RuntimeDocument`/`IProcessStrategy` — a tree
  of extension instances wired to **compiled** value accessors.
- **Member paths** (`@(A.B.C)`) compile to **expression‑tree delegates**
  (`System.Linq.Expressions`, `.Compile()`, null‑safe) — reflection only at compile time, never
  at render.
- **Embedded C#** (`@( @… )`) compiles via **Roslyn** into `(model, chained, root)` delegates,
  which is also where expressions bind to the model's types. Roslyn touches *only* the C#
  expressions, not the template structure.
- Rendering walks the document and invokes those delegates — no re‑parse, no reflection per
  render. "Compile once, render many."

## The thesis

The defining choice: **the language has almost no built‑in semantics.** There are no
`if`/`for`/`include` keywords — `@if`, `@list`, `@date`, `@partial` are all *extensions*. The
only primitive is a `@`‑prefixed call (optional name, parenthesized parameter, optional `{{ }}`
body), composed with `:`. The grammar is tiny; the language grows by adding a class, not by
changing the parser. That orthogonality is the spine everything else hangs from.

---

## Distinctive strengths (the combination, more than any single trait)

### 1. Extension‑only core
Control flow is a library, so the language is small and uniformly extensible. The `:` chain
composes calls **right‑to‑left** (the leftmost call renders; each receives the right neighbour's
output as its *chained* value, splice‑able via `@out()`). One mechanism — the **chained
channel** — unifies three things other engines keep separate: the **loop index**, the
**pipeline value**, and **content/slot projection** (`@out()`). Razor needs three unrelated
features for that.

### 2. Abstract, late‑bound definitions (C++ templates, not generics)
A definition with no `:: Type` is *abstract*: it's compiled against the concrete model present
at **each call site** (or pinned when an inheritor narrows it). This is the **C++ template**
model — monomorphised and statically type‑checked *per use*, with no type parameters or
constraints — not C# generics (compiled once behind constraints, abstracted away, not
late‑bound), and not `:: dynamic` (runtime binder). Write a section once; every call site is its
own specialization, each checked. Uncommon and genuinely useful.

### 3. Composition without coupling
Definition inheritance/override are *declarative extension points*, not forward dependencies.
Unlike Razor sections — which bind "backwards," pinning the rendered page as the layout's final
consumer — a TTL page can be split into independent templates recombined by a layout, any page
can serve as a base for another, and (because it all compiles to one flat document)
**composition costs nothing at render time**. This is the strongest practical differentiator.

### 4. Context model: relative, typed
The current model **narrows as you descend** (Go `text/template` `.`/`$`, Mustache family — the
*shape* is not novel), with `::`/`@root` as the escape hatch and a deliberate one‑level
step‑back in value‑extension bodies (so `@if(flag){{ @(Title) }}` still sees the surrounding
model). What *is* uncommon is pairing this relative model with **static typing threaded through
the narrowing** at compile time.

### 5. Compiled and fast
Member access and embedded C# are checked at compile time, and rendering walks a pre‑compiled
document with no per‑call activation/section/DI overhead. On the repo's component‑heavy
home‑page benchmark it renders **markedly faster than ASP.NET Core Razor (about double the
throughput) with fewer allocations** — see [Architecture → Performance](architecture.md#performance-characteristics)
and [src/Templates.Performance](../src/Templates.Performance). The win is structural (no
per‑component partial/view‑component machinery), so it's largest on component‑dense pages and
narrower on text‑heavy ones.

---

## Honest weaknesses

- **Sigil density / readability.** `@% %@`, `{{ }}`, `<name:base>`, `-> ()`, `:: Type`,
  `::Member`, `@<<{{ }}`, `@{ }@`, `@:`, `@\`, `@@` — and the punctuation is overloaded (`:` is
  chain delimiter *and* base separator *and* half of `::`; `{{ }}` is body *and* import‑path
  wrapper *and* definition body). Real templates read densely. (Whitespace trimming `@\` is less
  of a tax than it first appears — HTML tolerates the whitespace, so it's mostly needed in the
  declaration preamble.)
- **Missing ergonomic basics.** No `else`/`elif` (compose `@if`/`@ifnot`, often nested); no
  **named extension arguments** (a call takes one model value or a C# expression). *Correction
  to an earlier draft:* "no named slots" was wrong — named regions are simply multiple
  definitions a layout calls; that's a real strength, not a gap.
- **Security is all‑or‑nothing.** `AllowCSharp` is the whole story: off = safe but you lose
  typed expressions; on = arbitrary C# execution. No sandbox like Scriban/Liquid, so it's
  unsuitable for **untrusted, user‑supplied** templates. Fine for trusted first‑party templates
  (the intended use).
- **Supportability / ecosystem.** Sophisticated implementation (8‑mode ANTLR lexer, Roslyn
  codegen, expression‑tree accessors, custom string builders, bespoke VS extension + Ace mode)
  for what looks like a single‑author project — real **bus‑factor** and a thin ecosystem.
  .NET‑only; no Stack Overflow corpus; the MVC `import`/`partial` resolvers are currently
  commented‑out stubs; docs were near‑absent before this pass (now addressed). The novelty cuts
  both ways: elegant but unfamiliar, so the pool who can maintain/extend it is small.
- **Debuggability (inferred).** Embedded C# compiles via Roslyn and member access via
  expression trees; a broken expression likely won't map back to a template line as cleanly as
  an interpreted engine. Worth verifying before trusting in anger.
- **Extension‑authoring floor.** Registering a Liquid/Scriban filter is a lambda; a TTL
  extension is a class with `InitStart` type negotiation (`ExType`), *both* `ProcessData` and
  `RenderData`, and `Scope` transforms. More powerful, but the simple case isn't simple.

---

## Versus competitors

| | **TTL** | Razor | Liquid / Scriban | Handlebars / Mustache | Go `text/template` |
| --- | --- | --- | --- | --- | --- |
| Execution | Compiled to an exec‑ready document | Compiled | Interpreted | Interpreted | Interpreted |
| Typing | **Static, per use site** | Static | Dynamic | Dynamic | Dynamic |
| Logic in templates | Full C# (opt‑in) | Full C# | Sandboxed filters | Logic‑less + helpers | Limited + funcs |
| Control flow | **Extensions (a library)** | Keywords | Tags | Block helpers | Keywords |
| Context model | Relative: current / `::`root | Absolute (`Model.X`) | Mostly global | Relative stack | Relative (`.` / `$`) |
| Composition | Definition inheritance + override | Layouts / sections / partials | Partials / includes | Partials | Templates / blocks |
| Untrusted templates | No (or no‑C# mode) | No | **Yes (sandboxed)** | **Yes (logic‑less)** | Partial |
| Reach / ecosystem | .NET only, small | .NET, excellent | Large | Large, polyglot | Large |

Notes:
- **vs Razor** — comparable expressive power (both compiled, full C#), but TTL's composition is
  decoupled where Razor's sections are not, and TTL is faster on component‑heavy pages. Razor
  wins decisively on tooling, debugging, and ecosystem.
- **vs Liquid/Scriban** — those are sandboxed and safe for untrusted templates; TTL is not. TTL
  is compiled/typed where they are dynamic/interpreted.
- **vs Handlebars/Mustache** — TTL's extensions ≈ helpers, but typed and compiled; those are
  dynamic, logic‑less, and polyglot.
- **vs Go templates** — closest on the context model (`.`/`$` ≈ current/`::`), but Go is dynamic
  and interpreted; TTL adds static typing and an inheritance/late‑binding composition model.
- **vs T4** — same "compiled + full C#" idea, far cleaner composition.

---

## Verdict

- **Best fit:** performance‑sensitive, first‑party .NET HTML/text generation by a team that
  values typed templates and component‑style composition and will invest in the bespoke
  toolchain.
- **Poor fit:** untrusted user‑supplied templates, polyglot stacks, or teams wanting
  batteries‑included tooling and a large hiring pool.

The ceiling is high and now measurable; what limits adoption is the **floor** (ergonomics) and
the **ecosystem**, not the engine.

## If I were evolving it (highest‑leverage, ergonomics over features)

1. `if/else`/`elif` (a syntax or extension) — the most felt gap.
2. Named extension arguments.
3. Reduce sigil overload / a friendlier surface.
4. A real "first 10 minutes" doc + an LSP/editor story beyond the bespoke VS extension.
5. Decide and document the security posture (keep all‑or‑nothing, or add a restricted mode).
