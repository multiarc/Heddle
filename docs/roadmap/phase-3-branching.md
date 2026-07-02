# Phase 3 — Branching via Declarative Local Context Flow

> Part of the [Heddle evolution roadmap](README.md). Status: **planned**.
> Depends on: nothing (phase 1 only improves conditions ergonomically). **Zero grammar change.**

## Goal

Optional `@elif`/`@else` as ordinary standalone `[ExtensionName]` extensions that coordinate
with a preceding `@if`/`@ifnot` through a **published local context** — no keywords, no chain
syntax, no imperative statement structure added to the language. Text between the branch
blocks renders normally, as if it were outside the set.

The underlying publish/read mechanism is a **general, public extension capability** — every
item can publish something for later sibling extensions to pick up. Branching is merely its
first consumer; the mechanism is the language feature.

## Surface (no new syntax at all)

```heddle
@if(IsFeatured){{ <b>Featured</b> }}
this text always renders, wherever the branches land
@elif(IsArchived){{ <i>Archived</i> }}
@else(){{ Regular }}
```

Each branch is its own ordinary output block with its own body — which is why no grammar,
lexer, or `TemplateChain` change is needed, and why only the winning branch's body ever
executes (short‑circuit is inherent, no laziness machinery).

## Design

### Precedent

Every mainstream engine makes else‑branches *structural* parts of a single `if` construct:
Jinja2/Django's `{% elif %}`/`{% else %}` keywords live inside `{% if %}…{% endif %}`, Liquid's
`{% elsif %}` likewise, Handlebars' `{{else}}` is a token valid only inside a block helper, and
Go templates' `{{else if}}` is documented sugar for a nested `if` closed by one `{{end}}` —
adjacency is grammar‑enforced, and text "between" branches is necessarily inside a branch body.
Sibling blocks coordinating through published state appears to be genuinely novel among
template engines; novel is not better, and the price is discoverability — users arriving from
keyword engines will expect adjacency to matter and will *not* expect interleaved text to
always render (see Risks). The closest design precedent is not a template engine but CSS
counters: `counter-increment` publishes state in document order for later siblings to read via
`counter()`, and nested elements get their own counter scope — the same
publish/read‑in‑document‑order, fresh‑frame‑per‑nesting shape as this phase (React's Context is
the other ambient‑state analogue, but it flows parent→child; counters, like this design, flow
across siblings).

### The local context channel

- [`Scope`](../../src/Heddle/Data/Scope.cs) (a struct with all‑`readonly` fields and members —
  see [External grounding](#external-grounding)) gains **one internal reference field** to a small mutable frame (working name `ScopeLocals`: effectively a tiny
  `Dictionary<string, object>` or inline slots). The field rides along unchanged through the
  existing pure transforms (`Model`, `Parent`, `Chain`) — siblings within one body execution
  therefore share the same frame instance automatically.
- **Lazily created on first publish** — templates that never publish pay nothing (no
  allocation, one null field copy per scope transform).
- **Frame lifetime = one body execution.** A fresh frame is introduced at every
  body‑execution entry point: the root `Generate` call
  ([HeddleTemplate.cs](../../src/Heddle/HeddleTemplate.cs)), and each descent into a
  subtemplate body (`AbstractExtension.RenderInnerResult`/`GetInnerResult`
  ([AbstractExtension.cs](../../src/Heddle/Core/AbstractExtension.cs)), plus the
  `@partial()` and definition‑invocation paths). Implementation starts with an explicit
  **inventory of all body‑execution entry points** — missing one leaks state across bodies.
- Consequences that fall out naturally:
  - each `@list`/`@for` **iteration** is a body execution → branch state resets per element;
  - **nested** bodies get their own frames → an if‑set inside an `@if` body cannot clobber
    the outer set;
  - the frame lives in the render‑scoped `Scope` lineage, never on extension instances
    (instances are shared across concurrent renders) → thread‑safe by construction.

### Public API (the general mechanism)

Exact naming decided at implementation; shape:

```csharp
scope.Publish(string key, object value);          // last-write-wins within the frame
bool scope.TryRead(string key, out object value);
```

Exposed to custom extensions and documented in
[custom-extensions.md](../custom-extensions.md) as the sanctioned way for sibling extensions
to coordinate declaratively (examples beyond branching: zebra striping, tab sets,
first‑match‑wins pickers).

### The branch protocol

`BranchState { bool Satisfied }` under a reserved key (e.g. `"heddle.branch"`).

| Extension | State absent | State `Satisfied == false` | State `Satisfied == true` |
| --- | --- | --- | --- |
| `@if(c)` / `@ifnot(c)` | evaluate `c`; render body if truthy; **publish** `Satisfied = truthy(c)` (starts a set) | same — a new `@if` always starts a new set (overwrites) | same — overwrites (new set) |
| `@elif(c)` (alias `elseif`) | behaves exactly like `@if` (standalone‑friendly) + lint warning | evaluate `c`; render body if truthy; publish `Satisfied = truthy(c)` | render nothing; leave state as is |
| `@else()` | render body (vacuously "no branch taken") + lint warning | render body; publish `Satisfied = true` | render nothing |

- "Satisfied" means *some branch in the current set has already fired* — an `@if` whose
  condition was true but whose body rendered empty still counts as satisfied (condition
  semantics, not output sniffing).
- Truthiness rules are today's `IfExtension` rules, extracted into a shared helper
  (`null` → false; `bool` → itself; non‑null non‑bool → true). `@ifnot` participates
  identically with its inverted predicate.
- Bodies compile against `parent` exactly like `IfExtension` today (the condition value never
  becomes the body's model).
- Publishing happens in both `ProcessData` and `RenderData` paths so branch sets behave the
  same when a branch block is used inside composition.

### Compile‑time lint

The compiler processes output chains in document order
([HeddleCompiler.Compile](../../src/Heddle/Runtime/HeddleCompiler.cs) iterates
`parseContext.OutputChains` per body's `ParseContext`), so it can track, per body level,
whether a branch‑set opener precedes each `@elif`/`@else` and emit a `HeddleCompileWarning`
("`@else` without a preceding `@if`/`@ifnot`/`@elif` in this scope") — a warning, not an
error, because the standalone semantics are defined (table above).

### Pinned‑during‑design decisions (before implementation)

1. **`@out()` projection**: does projected caller content execute in the caller's frame or
   the definition body's frame? Decide, pin with a fixture. (Lean: the frame follows the
   `Scope` it renders under — i.e. the definition body's frame — simplest and consistent.)
2. **`@partial()`**: fresh frame (separate document, separate root body). Pin with a fixture.
3. Whether `BranchState` is public or internal (lean: internal + documented reserved key
   semantics; custom extensions build their own protocols on their own keys).

### Explicitly not in v1 (recorded direction)

Auto‑publishing a minimal outcome record for **every** sibling item
(`{extension name, produced output}`) as a further generalization of "every item publishes"
— revisit once real custom‑extension usage of the channel exists.

## Back‑compat analysis

Effectively perfect:

- **No parsing changes** — `generated/` untouched; every existing template parses and
  renders byte‑identically.
- New extension names only (`elif`, `elseif`, `else` — verified free in the registry;
  a user assembly exporting those names would collide at startup → release note).
- One additive internal field on `Scope`; passed by `in` everywhere — verify no measurable
  perf change via the benchmark suite.
- `IfExtension`/`IfNotExtension` change is additive (publish + shared truthiness helper);
  standalone behavior identical.

## Risks & mitigations

- **Missing a body‑execution entry point** → state leaks across bodies. Mitigate: explicit
  entry‑point inventory + isolation fixtures (nested, iterated, partial, projected). (M)
- **Per‑iteration frame allocation in hot loops** — mitigated by lazy creation: allocation
  only happens when something publishes; a list body without branches allocates nothing. (S)
- **`@out()` frame semantics ambiguity** — resolved by a pinned fixture before coding. (S)
- **Two branch sets interleaved by design confusion** (users expecting `@else` to bind to a
  *specific* `@if` rather than "the set state") — mitigated by docs: one set per body level
  at a time; a new `@if` starts a new set. (S)
- **Keyword‑engine adjacency expectations** → every mainstream engine (Jinja2/Django, Liquid,
  Handlebars, Go templates) grammar‑enforces branch adjacency, so incoming users will assume
  text between branch blocks belongs to a branch — and may read "interleaved text always
  renders" as a bug rather than the feature it is. Mitigate: docs lead with the
  interleaved‑text example as the *first* example in `built-in-extensions.md`, plus an
  explicit "coming from Jinja2/Liquid/Handlebars/Go" call‑out box stating the two behaviors
  that differ (interleaved text renders; `@else` binds to set state, not a specific `@if`);
  the compile‑time lint already covers orphaned branches. (M)

## Success criteria

1. The flagship interleaved fixture renders exactly one branch **plus all interleaved text**
   for each condition state (goldens).
2. **Zero grammar changes** (`generated/` untouched) and the entire existing suite passes
   byte‑identically.
3. Per‑iteration isolation inside `@list` (alternating‑condition fixture).
4. Nested sets independent (if‑set inside an `@if` body).
5. Standalone `@elif` behaves as `@if`; standalone `@else` renders its body; the lint warning
   fires for both.
6. Parallel renders of one compiled template with opposite conditions show no cross‑talk.
7. A sample custom publisher/consumer extension pair works via the public API (shipped as a
   `custom-extensions.md` example).
8. Benchmarks show no regression on templates that never publish.

## Validation scenarios

| Scenario | Expected |
| --- | --- |
| Flagship template under `A` / `!A ∧ B` / neither | `Featured` / `Archived` / `Regular`; interleaved text always present |
| `@if(A){{1}}@else(){{2}} @if(B){{3}}@else(){{4}}` | two independent sets: `1/2` per `A`, `3/4` per `B` |
| Branch set inside `@list(Items)` with per‑element conditions | state resets each iteration (mixed outputs correct per element) |
| If‑set nested inside an `@if` body | inner set cannot affect outer set's `@else` |
| Branch set split across an `@out()` projection | pinned behavior per design decision 1 |
| `@else()` with no preceding `@if` | body renders + compile warning |
| `@elif(X)` with no preceding `@if` | acts as `@if(X)` + compile warning |
| `@ifnot(A){{x}}@else(){{y}}` | `else` responds to `ifnot`'s satisfaction |
| Concurrency: parallel renders, opposite conditions | no cross‑talk |
| Hot loop: `@list` over 10k items with if/else inside | no measurable regression vs `if`+`ifnot` pair baseline |

## Size estimate

| Work item | Size |
| --- | --- |
| `Scope` frame + body‑entry‑point inventory + lazy creation | M |
| `Elif`/`Else` extensions + `If`/`IfNot` publish + shared truthiness | S/M |
| Compile‑time lint | S |
| Tests (goldens, isolation, concurrency, perf) | M |
| Docs (`built-in-extensions.md` entries, `custom-extensions.md` channel section) | S |

## .NET 10 opportunities (net10.0 target)

What the `net10.0` build can — and honestly cannot — exploit for this phase behind
`#if NET10_0_OR_GREATER`. Verified July 2026 against primary sources (ship versions checked
per API). Headline: **nothing in this phase warrants a `#if` — the .NET 10 JIT will not
stack‑allocate the frame, and the frame shape that wins is the same on every target.** The
negative findings below are the engineering guidance; lazy allocation stays the load‑bearing
mitigation.

| Candidate | Ships in | Verdict for this phase |
| --- | --- | --- |
| JIT escape analysis stack‑allocating the frame | .NET 9 (objects/boxes), .NET 10 (small arrays, local struct fields, delegates) | **No** — defeated by the `in Scope` call surface; see analysis |
| `Dictionary.GetAlternateLookup<ReadOnlySpan<char>>` | .NET 9 | **No benefit** — keys are string literals; nothing to slice |
| `FrozenDictionary` | .NET 8 | **Wrong tool** — frame is mutable with one‑body‑execution lifetime |
| `CollectionsMarshal.GetValueRefOrAddDefault` | .NET 6 | **Marginal** — only if the frame stays a plain `Dictionary` |
| .NET 10 collection additions | .NET 10 | **Nothing relevant** — only `OrderedDictionary` index overloads |
| C# 14 `field` keyword / extension members | C# 14 (SDK/LangVersion, not TFM) | **Not version‑gated** — minor convenience at most |

### Escape analysis will not stack‑allocate the frame (verified negative)

.NET 10 meaningfully extended the JIT's escape analysis: stack allocation of small
fixed‑size arrays (value‑type *and* reference‑type elements), escape analysis through
**local struct fields**, and delegate stack allocation
([What's new in .NET 10 runtime — Stack allocation](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/runtime#stack-allocation)),
on top of .NET 9's object/box stack allocation
([What's new in .NET 9 runtime](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-9/runtime#object-stack-allocation-for-boxes)).
The struct‑field item sounds tailor‑made for "object referenced by a field of `Scope`" —
it is not, for three independent reasons, any one of which is fatal:

1. **The analysis is intraprocedural and the `Scope` crosses non‑inlined calls.** The
   official escape definition: objects escape "when assigned to non-local variables or
   passed to functions not inlined by the JIT"; the JIT design doc is blunter — "with
   intra-procedural analysis only, the compiler has to assume that arguments escape at all
   non-inlined call sites"
   ([object-stack-allocation.md](https://github.com/dotnet/runtime/blob/main/docs/design/coreclr/jit/object-stack-allocation.md)).
   However the frame is installed, its reference lives in a `Scope` that is then passed
   through the non‑inlined `ProcessData`/`RenderData`/`Execute` dispatch surface (`in Scope`
   everywhere — a byref), so the frame is treated as escaping at its first real use. The
   .NET 10 struct‑field improvement covers fields of *local, non‑escaping* structs — the
   docs' example is a struct local allocated and consumed inside one method — which is
   precisely what a `Scope` threading through virtual extension dispatch is not.
2. **`Dictionary<string,object>`'s real storage never qualifies anyway.** Array stack
   allocation requires small arrays with JIT‑known fixed lengths in the analyzed method (or
   its inlinees); a dictionary's buckets/entries arrays are allocated inside non‑inlined BCL
   code with prime‑derived, non‑constant sizes. Even a hypothetically non‑escaping frame
   would keep its backing arrays on the heap.
3. **Per‑iteration allocations sit in loops.** In‑loop stack allocation requires proving the
   allocation doesn't escape its iteration, which the design doc explicitly marks "beyond
   the scope of at least the initial implementation"; the .NET 10 loop‑adjacent wins
   (conditional escape analysis) target `foreach` enumerators via guarded devirtualization
   ([Array enumeration de‑abstraction](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/runtime#array-enumeration-de-abstraction)),
   not user collections.

**Consequence:** do not budget for the JIT deleting the per‑iteration allocation on any
target; the "hot loop" risk keeps lazy creation as its real mitigation. (.NET 10 still helps
this phase *ambiently* — delegates and enumerators in extension and benchmark code get
cheaper for free — but there is nothing to gate or act on.)

### Frame shape: inline reserved slot + overflow map (recommended; makes TFM questions moot)

The honest answer to "which .NET 10 dictionary API should the frame use" is *none*: with few
keys expected (usually zero or one — the branch key), the winning shape avoids a dictionary
on the hot path entirely.

- **Mechanism**: `ScopeLocals` is one small class holding a dedicated slot for the reserved
  branch key (the `BranchState`, or just its fields) plus a lazily created
  `Dictionary<string, object>` overflow for arbitrary public keys. `Publish`/`TryRead`
  check the reserved key first (reference equality against the interned literal, falling
  back to ordinal equals), touching the overflow map only for user keys.
- **Benefit**: first publish in a hot loop iteration costs **one** small allocation instead
  of three (`Dictionary` object + buckets array + entries array on first insert); branch
  reads are a null check plus a field load. Identical code and identical performance
  characteristics on `netstandard2.0` through `net10.0` — zero `#if` in the phase.
- **Conditionality**: none — this is the recommendation. If the general channel later grows
  hot multi‑key users, revisit with a few generic key/value slot pairs before the overflow
  map; measure first.
- If the frame nevertheless stays a plain `Dictionary<string, object>`:
  [`CollectionsMarshal.GetValueRefOrAddDefault`](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.collectionsmarshal.getvaluereforadddefault)
  (.NET 6+, i.e. every target except `netstandard2.0`) makes last‑write‑wins `Publish` a
  single hash probe, and a small explicit ctor capacity (all targets) avoids growth — useful,
  but not .NET 10‑specific, and moot under the inline‑slot shape.

### Modern BCL candidates assessed (not applicable)

- [`Dictionary<TKey,TValue>.GetAlternateLookup<TAlternateKey>`](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.dictionary-2.getalternatelookup)
  — shipped in **.NET 9** (so of Heddle's targets only `net10.0` has it), requires a comparer
  implementing `IAlternateEqualityComparer`. It pays only when lookups would otherwise
  materialize a string from a span slice. This phase's keys are compile‑time string literals
  already in hand — there is nothing to slice, so span lookup buys nothing. Worth remembering
  only if a future key scheme derives keys from template text spans at render time.
- [`FrozenDictionary`](https://learn.microsoft.com/en-us/dotnet/api/system.collections.frozen.frozendictionary)
  (**.NET 8**, `System.Collections.Frozen`) — optimized for build‑once/read‑many, long‑lived
  maps that amortize expensive construction. The frame is the opposite: mutable,
  write‑then‑few‑reads, lifetime of a single body execution. And the reserved‑key protocol
  needs no map at all under the inline‑slot shape.
- **.NET 10 collections additions** — the only change is index‑returning `TryAdd`/
  `TryGetValue` overloads on `OrderedDictionary<TKey,TValue>` (pairing with `GetAt`/`SetAt`;
  [What's new in .NET libraries for .NET 10 — Collections](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/libraries#collections)).
  No new small‑collection type, no relevant `CollectionsMarshal` additions. Explicit negative
  so nobody goes hunting.

### C# 14 conveniences (brief; not TFM‑gated)

[C# 14](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-14) ships with the
.NET 10 SDK and is selected by `LangVersion`, not by target framework; the `field` keyword
and extension members compile down without runtime support, so if adopted they apply to all
four targets at once — another "makes the version question moot" case (official support
couples C# 14 to .NET 10, so keep down‑level use to runtime‑independent features, which
these are). Utility here is minor: `Publish`/`TryRead` go directly on `Scope`, a type Heddle
owns, so extension members add nothing to this API; `field` can tidy accessor‑bodied
properties in the new extensions. Neither changes design or performance.

## Open questions

1. Reserved key naming and whether to expose `BranchState` publicly (lean: internal).
2. Promote `Scope` from `struct` with all‑`readonly` members to a declared `readonly struct`
   while adding the frame field, making the no‑defensive‑copy guarantee structural (see
   [External grounding](#external-grounding)). (Lean: yes — cheap while the type is open.)
3. Should `@else` republish `Satisfied = true` (second `@else` in a set renders nothing) —
   table above says yes; confirm against user expectations.
4. Frame for `@swap()`/`@param()` bodies — same rule as other bodies (fresh frame) or share?
   (Lean: same rule; consistency wins.)

## External grounding

Verified July 2026 against primary sources; these ground the [Precedent](#precedent) note, the
`Scope` mechanics, and the back‑compat perf claim.

- [Jinja2 — Template Designer Documentation](https://jinja.palletsprojects.com/en/stable/templates/)
  — `{% elif %}`/`{% else %}` are integral parts of the `{% if %}…{% endif %}` statement
  ("like in Python"); Django's template language has the same shape.
- [Liquid — control flow tags](https://shopify.github.io/liquid/tags/control-flow/) —
  `{% elsif %}`/`{% else %}` add "more conditions within an `if` or `unless` block"; not valid
  standalone.
- [Handlebars — built‑in helpers](https://handlebarsjs.com/guide/builtin-helpers.html) —
  `{{else}}` is a special token usable only inside a block helper
  (`{{#if}}…{{else}}…{{/if}}`, with else‑section chaining for `else if`).
- [Go `text/template`](https://pkg.go.dev/text/template) — `{{else if}}` is documented as
  exactly equivalent to an `if` nested in the `else`; the whole chain closes with a single
  `{{end}}`. Together these confirm the Precedent note: none of the four can even express
  "text between branches always renders" — in those grammars such text is necessarily inside
  a branch body.
- [Avoid memory allocations and data copies (Microsoft Learn)](https://learn.microsoft.com/en-us/dotnet/csharp/advanced-topics/performance/)
  — `in`/`ref readonly` pass a reference, so the added frame field costs nothing at call
  sites (verified: the entire `ProcessData`/`RenderData`/`Execute` surface takes `in Scope`);
  a struct copy only happens where the pure transforms construct a new `Scope`
  (`Parent`/`Chain`/`Model`), and one extra pointer‑sized field there is noise ("the cost of
  copying a value is negligible if the types are small").
- [The `in`‑modifier and the readonly structs in C# (Microsoft DevBlogs)](https://devblogs.microsoft.com/premier-developer/the-in-modifier-and-the-readonly-structs-in-c/)
  — the compiler emits a defensive copy on member access through an `in` parameter of a
  non‑readonly struct; readonly declarations eliminate it. Nuance found while grounding:
  [`Scope`](../../src/Heddle/Data/Scope.cs) is declared `public struct`, **not**
  `readonly struct` — today its all‑`readonly` fields plus `readonly`‑marked members already
  avoid defensive copies, and the new frame field must be `readonly` too (`readonly` pins the
  reference; the frame object it points to stays mutable, which is the point). Consider
  promoting the declaration to `readonly struct` while touching the type, so the
  no‑defensive‑copy guarantee is structural rather than per‑member.

Thread‑safety needs no external citation: the frame is per‑render‑invocation state — the
standard "encapsulate data into instances that are not shared across requests" pattern from
Microsoft's *Managed threading best practices* — so the "thread‑safe by construction"
reasoning above stands; synchronization is only required for state shared across concurrent
invocations, which the frame never is.
