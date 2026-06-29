# Language Reference (Heddle)

This is the complete reference for the Heddle template language. Every construct below is
grounded in the ANTLR grammar — [HeddleLexer.g4](../src/Heddle.Language/HeddleLexer.g4) (tokens)
and [HeddleParser.g4](../src/Heddle.Language/HeddleParser.g4) (rules) — and illustrated with
clear, self‑contained examples built around one small model (see
[below](#the-model-used-in-these-examples)). The examples are written to teach; for real,
runnable templates that exercise edge cases, see the test fixtures in
[src/Heddle.Tests/TestTemplate](../src/Heddle.Tests/TestTemplate).

> Delimiters at a glance: `@% … %@` wraps definition blocks and `{{ … }}` wraps bodies /
> subtemplates.

## Table of contents

1. [Mental model](#mental-model)
2. [Symbol cheat sheet](#symbol-cheat-sheet)
3. [Text and the `@` escape](#text-and-the--escape)
4. [Output blocks](#output-blocks)
5. [Member expressions `@(A.B.C)`](#member-expressions-abc)
6. [Root reference `@(::Member)`](#root-reference-member)
7. [Context and data flow](#context-and-data-flow)
8. [Embedded C# expressions](#embedded-c-expressions)
9. [Definitions `@% … %@`](#definitions---)
10. [Default output `-> chain`](#default-output---chain)
11. [Type annotation `:: Type`](#type-annotation--type)
12. [Inheritance and override `<child:base>`](#inheritance-and-override-childbase)
13. [Subtemplates `{{ … }}`](#subtemplates--)
14. [Chaining with `:`](#chaining-with-)
15. [Recursion](#recursion)
16. [Imports `@<<{{ … }}`](#imports--)
17. [Comments `@* … *@`](#comments---)
18. [Raw blocks `@{ … }@` and `@:`](#raw-blocks----and-)
19. [Whitespace trimming `@\`](#whitespace-trimming-)
20. [How the lexer reads a template (modes)](#how-the-lexer-reads-a-template-modes)
21. [Behavioral nuances summary](#behavioral-nuances-summary)

---

## Mental model

A Heddle document ([`heddle`](../src/Heddle.Language/HeddleParser.g4) rule) is a sequence of:

- **text** — literal output, copied verbatim;
- **output blocks** — `@…` directives that call *extensions* and emit their results;
- **definitions** — `@% … %@` blocks that declare reusable named templates;
- **imports** — `@<<{{ path }}` that pull definitions in from another file;
- **raw blocks** — verbatim regions that are not parsed.

```
document = ( text | output | definition | import | raw )*
```

Everything special starts with `@`. Plain text never needs escaping unless it contains `@`.

> **Notation.** The "shape" boxes below use a simplified syntax, not the real grammar:
> `[ x ]` = optional, `( x )*` = zero or more, `a | b` = either, and literal symbols
> (`@`, `{{`, `:`, `::`, …) are written as you'd type them. For the exact rules see the
> authoritative [HeddleLexer.g4](../src/Heddle.Language/HeddleLexer.g4) /
> [HeddleParser.g4](../src/Heddle.Language/HeddleParser.g4).

### The model used in these examples

Every example in this reference renders the same little **blog** model. Keep it in mind as you
read:

```csharp
class Blog    { string Title;  int Year;  ICollection<Article> Articles; }
class Article { string Title;  Author Author;  DateTime PublishedOn;
                bool IsFeatured;  IList<string> Tags;  ICollection<Comment> Comments; }
class Author  { string Name;   string Email; }
class Comment { string Author; string Text;  ICollection<Comment> Replies; }
```

So the root model is a `Blog`; inside `@list(Articles){{ … }}` the current model is an
`Article`; and so on.

### Two ideas to know up front

If you are coming from Razor, two of Heddle's design choices will surprise you. Both are
deliberate, and both are what make templates compose:

- **Context is relative, not absolute.** A member path `@(Title)` is resolved against the
  *current* context, and that context **changes as you nest calls** (a call either descends
  into its parameter or, for conditionals and formatters, keeps the caller's context). This is
  the Go `text/template` (`.` / `$`) and Mustache model, not Razor's always‑absolute `Model.X`.
  The escape hatch back to the original model is
  [`::`](#root-reference-member); the full picture is in
  [Context and data flow](#context-and-data-flow).

- **Heddle are abstract by default; types bind late.** A definition written **without**
  `:: Type` has no fixed model type — it is a *polymorphic section*. Its real type is bound
  when it's actually used (compiled against whatever concrete model reaches that call site) or
  when an inheriting definition narrows it with `:: Type`. This is closer to a **C++ template**
  than a C# generic: there are no type parameters or constraints — each use re‑substitutes the
  concrete model and is type‑checked on its own. Write the section once; every call site is its
  own specialization. See [Type annotation](#type-annotation--type) and
  [Inheritance](#inheritance-and-override-childbase).

---

## Symbol cheat sheet

| Syntax | Name | Meaning |
| --- | --- | --- |
| `@(expr)` | Output expression | Emit a member value or C# expression. |
| `@name(param)` | Extension call | Invoke extension `name` (e.g. `@list`, `@if`). |
| `a:b:c` | Chain | Compose calls right‑to‑left; the **leftmost** renders, each call feeds the one to its left. |
| `{{ … }}` | Block / subtemplate | Body of a definition or the inline template for a call. |
| `@% … %@` | Definition block | Declare one or more named templates. |
| `<name>` | Definition name | Names a template inside `@% … %@`. |
| `<child:base>` | Inheritance | Define `child` based on `base`. |
| `-> chain` | Default output | Marks a definition to render automatically; `chain` selects its data. |
| `:: Type` | Type annotation | Strongly types a definition's model. |
| `::Member` | Root reference | Read `Member` from the root model, not the current one. |
| `@<<{{ path }}` | Import | Include definitions from another template file. |
| `@* … *@` | Comment | Ignored; never emitted. |
| `@{ … }@` | Raw block | Emitted verbatim; not parsed. |
| `@: …` | Raw line | Rest of the line emitted verbatim. |
| `@\` | Whitespace trim | Eat the following run of whitespace. |

---

## Text and the `@` escape

Any character that is not part of a directive is literal text and is emitted unchanged.
Unicode is fully supported — `<h1>Café — Привет!</h1>` renders as written.

The only special character is **`@`**. To emit a literal `@`, double it: **`@@`**:

```heddle
<p>Reach us at support@@example.com</p>      @* outputs: support@example.com *@
```

To emit a block of literal text that contains many `@` or `{{ }}` characters, use a
[raw block](#raw-blocks----and-) instead of escaping each one.

---

## Output blocks

An **output block** is `@` followed by a [chain](#chaining-with-) of one or more calls, with
an optional [subtemplate](#subtemplates--):

```
output =  @ chain [ {{ body }} ]
chain  =  call ( : call )*
call   =  [name] ( parameter )
parameter =  member-path  |  @ C#-expression  |  chain  |  (empty)
```

A **call** has three parts: an optional extension name, a parenthesized parameter, and an
optional `{{ … }}` body. The parameter is one of:

- **a member expression** — `@(Title)`, `@list(Articles)`;
- **a C# expression** — introduced with an inner `@`, e.g. `@(@2026)`, `@list(@model.Articles.Where(a => a.IsFeatured))`;
- **another chain** — nested calls;
- **empty** — `@()`, `@out()`, `@list(){{ … }}` — meaning "use the current value as‑is".

When the extension name is omitted, the call uses the **empty extension**
(`@(...)` is `@` + an unnamed call). The empty/`html` extensions simply stringify the
current value — so `@(Title)` prints the title and `@()` prints the current value itself
(handy inside a list of strings). See
[Built‑in Extensions](built-in-extensions.md#empty--unnamed).

Examples:

```heddle
<h1>@(Title)</h1>                       @* a member value (Blog.Title) *@
<p>Est. @(@model.Year - 1) — present</p>  @* a C# expression *@
<p>@string(Title)</p>                   @* the "string" extension: stringify + HTML-encode *@
@list(Articles){{ <li>@(Title)</li> }}  @* a call with a body, once per article *@
```

### `@(expr){{ … }}` — the inline "with" block

Give the **unnamed** call both a parameter *and* a body, and it renders the body with the
current model set to `expr` — an inline way to "zoom in" on a value without declaring a
definition:

```heddle
@(Author)
{{
  <span class="byline">@(Name) &lt;@(Email)&gt;</span>   @* model here is the Author *@
}}
```

Inside the block, `@(Name)`/`@(Email)` resolve against `Author`. It is the lightweight, inline
counterpart to [passing a value into a definition](#passing-the-current-value-into-a-call), and
it pairs well with a C# expression — e.g. render markup for just the last item of a list:

```heddle
@(@model.Articles.LastOrDefault())
{{
  <li class="last">@(Title)</li>
}}
```

(Mechanically this is the [empty extension](built-in-extensions.md#empty--unnamed) with a body:
with a body it renders the body against its model; with no body it just stringifies the value.)

---

## Member expressions `@(A.B.C)`

```
member-path = [::] name ( . name )*
```

A member expression is a dotted path of identifiers resolved against the current model:

```heddle
@(Title)                @* current model's Title *@
@(Author.Name)          @* nested property access *@
@(Author.Email)         @* …any depth *@
```

Member access works on statically‑typed models (resolved by reflection) and on `dynamic`
models (resolved with the C# runtime binder). The "current model" is whatever value the
enclosing extension established — for example, inside `@list(Articles){{ … }}` the current
model of the body is *one `Article`*, so `@(Author.Name)` there is that article's author.

---

## Root reference `@(::Member)`

Prefix a member path with `::` to read from the **root** model — the value originally passed
to `Generate` — regardless of how deeply nested the current scope is. This is what lets you
reach page‑level data (the site title, the current year, a culture) from inside a loop.

```heddle
@list(Articles)
{{
  <article>
    <h2>@(Title)</h2>                        @* the Article's title (current model) *@
    <a href="/">← back to @(::Title)</a>     @* the Blog's title (root model) *@
  </article>
}}
```

Inside the loop the current model is an `Article`, so `@(Title)` is the article's title and
`@(::Title)` reaches past it to the root `Blog`. The equivalent embedded‑C# form is
`@(@root.Title)`; the `::` form is pure template syntax and does **not** require
`AllowCSharp`.

In real templates this is how you reach page‑ and app‑level context from anywhere — site host,
build number for cache‑busting, the active culture — without threading it through every call.
For instance, a date can be formatted with a pattern that itself comes from the root culture:
`@date(PublishedOn){{ @(::Site.Culture.DateTimeFormat.ShortDatePattern) }}`. See
[Patterns → root for app/page context](patterns.md#root-for-app-and-page-context).

---

## Context and data flow

This section ties the previous two together. At every point while rendering, there is a
**current context** — and the whole reason [`::` root](#root-reference-member) exists is that
this context *moves* as you descend through calls. Understanding the flow is the key to
threading data through a template.

What keeps this tidy rather than ad‑hoc is that the model moves *predictably*: a call either
**descends** into its parameter (iteration and component calls) or **keeps the caller's
context** and treats its parameter as a value (conditionals and formatters — see
[Stepping back](#stepping-back-the-parent-context)). Alongside the model, a single **chained**
channel carries the loop index, the value piped through a chain, and the content projected by
`@out()` — three things most engines handle with three separate features. And because the flow
is tracked at compile time, the context stays **statically typed** throughout.

Three data channels are in scope at any point:

| Channel | Read in templates as | What it is |
| --- | --- | --- |
| **model** (current) | `@(X)`, or `@model` in C# | The data the current block renders against. **Changes on every descent.** |
| **chained** | `@out()`, or `chained` in C# | A side value passed along, independent of the model (e.g. a loop index). |
| **root** | `@(::X)`, or `@root` in C# | The original model passed to `Generate`. **Never changes.** |

### The current model narrows as you descend

Iteration and component calls re‑base the context for their body: the parameter is evaluated
against the *current* model and becomes the *new* current model inside the body. (Conditionals
and formatters are the exception — see [Stepping back](#stepping-back-the-parent-context).)
Nesting these calls walks you down the object graph:

```heddle
@* current model = Blog *@
@list(Articles)
{{
  @* current model = one Article *@
  <h2>@(Title)</h2>                 @* Article.Title *@
  <ul>
    @list(Tags)
    {{
      @* current model = one Tag (a string) *@
      <li>@()</li>                  @* the tag itself *@
    }}
  </ul>
}}
```

`@(Title)` inside the first body is the *article's* title, not the blog's — the context shifted
when you entered `@list(Articles)`. A dotted path (`@(Author.Name)`) still walks within the
current model; it doesn't change which model is current.

### Passing the current value into a call

This is the everyday way data flows down: you **take a value from the current context and pass
it in** as a call's parameter.

```heddle
@list(Articles)
{{
  @author_badge(Author)      @* hand this article's Author to a definition *@
  @article_card()            @* empty parameter → forward the whole current Article *@
}}
```

- `@author_badge(Author)` passes the current article's `Author` down; inside `author_badge`
  (e.g. `:: Author`) the current model *is* that `Author`, so `@(Name)` there is the author's
  name.
- `@article_card()` with an **empty** parameter forwards the current model unchanged — the
  whole `Article`.

So you steer context explicitly: name a member to narrow into it, or pass nothing to forward
what you already have.

### Stepping back: the parent context

A call's parameter is always evaluated in the current context, but **what the body sees depends
on the kind of call**:

- **Descending calls** — `@list` and definitions — rebind the body to the parameter (the
  previous two subsections).
- **Value calls** — the conditionals and formatters (`@if`, `@ifnot`, `@date`, `@time`,
  `@int`, `@money`, `@guid`, `@string`) and `@for` — treat their parameter as a *value to test
  or format*, not a new model, so their body **steps one level back to the caller's context**:

```heddle
@list(Articles)
{{
  @* current model = one Article *@
  @if(IsFeatured)
  {{
    @* still the Article here — not the bool *@
    <h2>@(Title)</h2>            @* Article.Title *@
  }}
}}
```

Without this step‑back, `@(Title)` inside the `@if` would try to resolve against the boolean
`IsFeatured`. Because `@if` keeps the caller's context, the body still sees the surrounding
`Article`. The same holds for `@date(PublishedOn){{ … }}`, `@money(Price){{ … }}`, and the
rest: the parameter is the value being formatted, and the body renders against the model
*around* the call. `@for` works the same way — its body runs against the caller's model, with
the iteration index on the [chained](#the-second-channel-chained-data) channel.

This step‑back is exactly **one level** (it is `scope.Parent()` under the hood — see
[Writing Custom Extensions](custom-extensions.md)). There is no operator to climb further; for
anything higher, use the [root](#reaching-back-out-root) or pass values down explicitly.

### Reaching back out: `root`

Because the current model keeps narrowing, a value you need may no longer be in reach — deep
inside the article loop, the blog's own title is "above" you. There is **no parent *path*** you
can write in a member expression (no `..`), and the automatic
[step‑back](#stepping-back-the-parent-context) only climbs one level.
[`::`](#root-reference-member) is the escape hatch that jumps straight back to the original
model from any depth:

```heddle
@list(Articles){{ <a href="/">← @(::Title)</a> }}   @* Blog.Title, from inside the loop *@
```

For anything between current and root, [pass it down explicitly](#passing-the-current-value-into-a-call)
— or, when you control the call site, seed it through the **chained** channel.

### The second channel: chained data

A **chained** value flows alongside the model, independently of it:

- A [chain](#chaining-with-) `a():b()` runs `b` first and feeds its output to `a` (the
  leftmost call renders) as `a`'s chained input.
- [`@list`](built-in-extensions.md#list) and [`@for`](built-in-extensions.md#for) expose the
  current **index** as the chained value.
- [`@out()`](built-in-extensions.md#out) emits the chained value, and
  [`@swap()`](built-in-extensions.md#swap) exchanges the model and chained values for its body.

In [embedded C#](#embedded-c-expressions) the three channels are simply the identifiers
`model`, `root`, and `chained`:

```heddle
@for(@new ForModel(){ Last = model.Articles.Count(), Step = 3 })
{{
  @* model = the Blog (the for body keeps the outer model); chained = the loop index *@
  @list(@model.Articles.Skip(chained).Take(3)){{ @article_card() }}
}}
```

(There is also `callerData`, a fourth value you may pass to `Generate` and read from custom
extensions — see [`Scope`](csharp-api.md#scope-the-data-view-during-rendering).)

---

## Embedded C# expressions

Inside a call's parentheses, an inner **`@`** switches the lexer into C# mode (`CS` mode,
[HeddleLexer.g4](../src/Heddle.Language/HeddleLexer.g4)), so the rest of the parameter is parsed
as a real C# expression and compiled by Roslyn. This requires
`TemplateOptions.AllowCSharp = true`.

Supported forms include literals, member access, method calls, LINQ, lambdas, and object
initializers. Nested parentheses are handled (the lexer balances `(`/`)` within the
expression):

```heddle
@(@model.Title.ToUpper())                            @* method call *@
@(@model.PublishedOn.Year)                           @* member of a member *@
@list(@model.Articles.Where(a => a.IsFeatured))      @* LINQ + lambda *@
@list(@model.Articles.OrderByDescending(a => a.PublishedOn).Take(5))
@if(@model.Comments.Count > 0){{ <h3>Comments</h3> }}   @* a boolean expression *@
```

Two well‑known identifiers are available in embedded C#:

- **`model`** — the current model (e.g. `@model.Articles`);
- **`root`** — the root model (e.g. `@root.Title`), equivalent to the `::` reference.

There is also a `chained` value available to extensions such as `@for` (the loop index);
see [`Scope`](csharp-api.md#scope-the-data-view-during-rendering).

> **Parser nuance.** A C# expression is just a run of C# tokens up to the matching `)`, so
> nested parentheses inside it (as in `@Foo(1)`) are part of the expression. The named call
> form `@x(@Foo(1))` and the unnamed form `@(@Foo(1))` therefore classify those tokens
> identically.

---

## Definitions `@% … %@`

A **definition block** declares one or more reusable named templates. Once declared, a
definition is invoked like any extension: `@name()`.

```
definitions = @% def+ %@
def         = < name [: base] >  [ -> chain ]  {{ body }}  [ :: Type ]
```

Anatomy of one definition — an `article_card` component:

```heddle
@%
  <article_card>             @* name, wrapped in < > *@
  {{                         @* body (subtemplate) *@
    <article>
      <h2>@(Title)</h2>
      <p class="byline">by @(Author.Name)</p>
      @out()                 @* the caller's content drops in here *@
    </article>
  }} :: Article              @* optional type annotation *@
%@
```

A single `@% … %@` block may declare many definitions back‑to‑back (e.g. `article_card`,
`comment`, `layout`, …). Definition blocks may appear at the top level **or nested inside a
subtemplate**, so a call's body can introduce local definitions before using them.

Invoking a definition — pass it a model, and optionally a `{{ … }}` body that it surfaces via
`@out()`:

```heddle
@list(Articles)
{{
  @article_card()           @* model = the current Article *@
  {{
    <a href="/read">Read more →</a>   @* this is what @out() renders *@
  }}
}}
```

Here each `Article` in the list is passed to `article_card`, and the `<a>` link is the body
that the card exposes through `@out()`.

---

## Default output `-> chain`

Most definitions are inert: they render only when you call them by name (`@name()`). Adding a
`->` turns a definition into an **output** — it renders automatically, in place, as part of
the document, using the `chain` after `->` to select its data. (This is how a template made of
nothing but definitions still produces output.)

```
< name >  -> chain  {{ body }}
```

```heddle
@%
  <article_list> -> (Articles)        @* the -> makes this render automatically *@
  {{
    <ul>
      @list(){{ <li>@(Title)</li> }}
    </ul>
  }} :: Blog
%@
```

`-> (Articles)` does two things: it selects the body's data (take `Articles` off the root
`Blog`, so `@list()` with an empty parameter iterates them), and it marks the definition as an
output that emits **here**, at its declaration. The empty form `-> ()` means "render using the
current value as‑is".

> **Don't also call it.** Because a `->` definition already renders at its declaration, writing
> `@article_list()` as well would render the list a *second* time. Use `->` for the regions a
> template should emit on its own; leave the `->` off for reusable definitions you invoke by
> name (like `article_card` above).

---

## Type annotation `:: Type`

A trailing `:: Type` strongly types a definition's model. The type name is resolved against
the namespaces brought in by [`@using()`](built-in-extensions.md#using) (and the model
type’s own assembly).

```heddle
<article_card>
{{ … }} :: Article                       @* a concrete type *@

<feed>
{{ … }} :: ICollection<Article>          @* generic types are allowed *@

<widget>
{{ … }} :: dynamic                       @* opt into dynamic dispatch *@
```

`dynamic` makes member access late‑bound (resolved at render time via the C# runtime
binder), which is how `@(Title)` works against an `ExpandoObject` or anonymous model. Concrete
types (e.g. `Article`, `ICollection<Article>`) enable compile‑time member checking and faster
access.

Generic and array type names are recognized by the lexer's type rule
(`ID_TYPE`, [HeddleLexer.g4](../src/Heddle.Language/HeddleLexer.g4)), which accepts
`Namespace.Type<T1, T2>[]` forms.

### Abstract definitions and late type binding

The annotation is **optional**, and leaving it off is a feature, not a shortcut. A definition
with **no `:: Type`** is *abstract* — it has no fixed model type. Its type is bound **late**:

- **When it's used.** Each call compiles the definition's body against whatever concrete model
  reaches that call site — the current model (`@panel()`), the type of a member parameter
  (`@badge(Author)`), or the chained type. The *same* source is specialized per use site.
- **When it's narrowed by inheritance.** An inheriting definition can pin the type with
  `:: Type` (see [Inheritance](#inheritance-and-override-childbase)).

```heddle
@%
  <panel>                                  @* no :: Type → abstract / polymorphic *@
  {{ <section class="panel"><h3>@(Title)</h3>@out()</section> }}
%@

@panel()                       @* current model = Blog    → @(Title) binds to Blog.Title *@
@list(Articles){{ @panel() }}  @* current model = Article → @(Title) binds to Article.Title *@
```

The same `panel` is reused against two different model types, and **each use is independently
type‑checked** — point it at a model with no `Title` and *that* call site fails to compile.
This is the **C++ template** model, not C# generics. There are no type parameters and no
`where`‑style constraints to satisfy up front: the body is simply re‑substituted with the
concrete model at each call site and checked there (compile‑time monomorphisation, or
"duck typing"). A C# generic, by contrast, is compiled **once** behind its constraints and
*abstracted away* — it is not late‑bound, and its body may only touch members the constraints
guarantee.

> This is **not** the same as `:: dynamic`. An abstract definition is still **statically**
> typed — just typed *per use* at compile time. `:: dynamic` defers member resolution to the
> runtime binder. Reach for abstract definitions to share structure (layouts, cards, wrappers)
> across many model shapes; reach for `dynamic` when the shape genuinely isn't known until
> render time.

In practice this is common: a reusable section is left untyped and simply invoked from inside a
typed page, where it binds to that page's model. See
[Patterns → abstract section in a typed page](patterns.md#an-abstract-section-inside-a-typed-page).

---

## Inheritance and override `<child:base>`

A definition can inherit from another by name using `:`:

```
< child : base >
```

```heddle
<featured_card:article_card>     @* featured_card inherits article_card *@
{{
  <div class="featured">
    @article_card()              @* call the base, then decorate it *@
  </div>
}}
```

`@featured_card()` now renders the base `article_card` wrapped in a `<div class="featured">`.

**Full override.** Re‑declaring an existing name as `<name:name>` replaces that definition
from that point onward:

```heddle
<article_card:article_card>
{{ <article class="compact">@(Title)</article> }}
```

After this declaration, later `@article_card()` calls render the compact version. Overrides
are layered in **document order**, so you can render a page, redefine a region, and render
again with the new look — without touching the call sites.

**Type compatibility rule (narrowing).** Inheritance is where an
[abstract definition](#abstract-definitions-and-late-type-binding) gets pinned down. A child's
type must be **assignable to** its base's type; otherwise compilation fails with *"The new
definition type &lt;X&gt; isn't assignable to base &lt;Y&gt;"*
([HeddleCompiler.WalkValidateDefinitionType](../src/Heddle/Runtime/HeddleCompiler.cs)). Two
consequences:

- If the base is **abstract** (no `:: Type`, i.e. `object`), a child may narrow it to **any**
  concrete type — this is the normal way to turn a shared, typeless section into a typed one.
- If the base **is** typed, children may only narrow to assignable (more‑derived) types; a type
  fixed anywhere in the chain can't be widened or swapped for an incompatible one.

**Composition without coupling (the standout feature).** Definitions are *declarative
extension points*, not forward dependencies. A layout exposes named regions and any page
supplies or overrides them. Put the layout in its own file:

```heddle
@* layout.heddle — a reusable shell *@
@%
  <sidebar>{{ <aside>Recent posts…</aside> }}
  <layout>
  {{
    <html>
      <head><title>@(Title)</title></head>
      <body>
        @sidebar()
        <main>@out()</main>
        <footer>© @(Year) @(Title)</footer>
      </body>
    </html>
  }} :: Blog
%@
```

Then a page imports it and overrides only what it needs:

```heddle
@* home.heddle *@
@<<{{ layout.heddle }}            @* pull in the layout + sidebar definitions *@
@%
  <sidebar:sidebar>           @* this page wants a different sidebar *@
  {{ <aside>Welcome, subscriber!</aside> }}
%@
@layout()
{{
  <h1>Latest articles</h1>
  @list(Articles){{ @article_card() }}
}}
```

This is the direct equivalent of Razor's `@RenderSection`/`@RenderBody`, but **without Razor's
directionality**: in Razor a page *declares* sections that the layout consumes, so the
rendered entry point must be the final page and a page can't easily become a base for another.
In Heddle the relationship is symmetric — `layout` knows nothing about `home`, any template that
exposes regions can serve as a base for anything, and because it all compiles into a single
execution‑ready document, **this composition costs nothing at render time**. (The
[performance benchmark](../src/Heddle.Performance) uses exactly this `home` + `layout`
shape.) See [Architecture → Performance](architecture.md#performance-characteristics).

---

## Subtemplates `{{ … }}`

A `{{ … }}` block is a full nested template. It appears as a definition body and as the
inline body attached to a call.

```
{{ ...any template content... }}
```

Because the body is itself a full template, subtemplates may contain text, output blocks,
nested definitions, imports, and raw blocks — to any depth. A call hands its subtemplate to
its extension; `@list(Articles){{ … }}`, for instance, renders its body once per element with
the element as the current model:

```heddle
@list(Articles)
{{
  <li>
    <a href="/post">@(Title)</a> — @(Author.Name)
  </li>
}}
```

---

## Chaining with `:`

Calls in a chain are evaluated **right to left**, and the **leftmost** call renders the final
output. Each call receives the output of the call to its *right* as its **chained** value,
which it can splice into its own result with `@out()`.

```
call : call : call ...
```

So a chain nests like function composition — `@a():b():c()` makes `a` the outer wrapper around
`b` around `c`: `c` runs first, its output is handed to `b`, `b`'s output is handed to `a`, and
`a` renders. For example:

```heddle
@%
  <heading>  {{ <h2>@out()</h2> }}        @* wraps its chained content in <h2> *@
  <emphasis> {{ <em>@(Title)</em> }}
%@

@heading():emphasis()
```

`emphasis` runs first and produces `<em>…Title…</em>`; that becomes `heading`'s chained value,
which `heading`'s `@out()` drops inside its `<h2>`. Result: `<h2><em>…Title…</em></h2>`.

The engine tracks the data **type** flowing through the chain, so each call sees the correct
chained‑input type and the final output type is known at compile time. Among the built‑ins,
`@out()` is the usual consumer of a chained value (it emits whatever the call to its right
produced), and `@swap()` exchanges the model and chained values for the duration of its body.

---

## Recursion

Definitions may call themselves, which is the natural way to render trees — like a threaded
comment list, where each `Comment` has `Replies` that are themselves `Comment`s:

```heddle
@%
  <comment>
  {{
    <li>
      <strong>@(Author)</strong>: @(Text)
      @if(@model.Replies.Count > 0)
      {{
        <ul>
          @list(Replies){{ @comment() }}    @* recurse into each reply *@
        </ul>
      }}
    </li>
  }} :: Comment
%@
```

Rendering `@list(Comments){{ @comment() }}` then walks the whole tree to any depth. Recursion
is bounded by `TemplateOptions.MaxRecursionCount` (default **100**); see the
[C# API Reference](csharp-api.md#templateoptions).

---

## Imports `@<<{{ … }}`

The `@<<` directive imports the **definitions** of another template file into the current
parse, so you can share a library of definitions across templates.

```
@<< {{ path/to/file }}
```

```heddle
@<<{{ layout.heddle }}
```

The path between `{{ }}` is resolved relative to `TemplateOptions.RootPath`. Imports are
handled by the [`import` extension](built-in-extensions.md#import) machinery
([ImportExtension.cs](../src/Heddle/Extensions/ImportExtension.cs)), which re‑parses the
referenced file into the current parse context.

> **`@<<` vs `@partial()`.** `@<<` / `@import` pull in *definitions* at compile time (no
> output of their own). [`@partial()`](built-in-extensions.md#partial) compiles a separate
> template by name and renders its output inline at run time. Choose `@<<`/`@import` to share
> reusable definitions; choose `@partial()` to embed another rendered template.

---

## Comments `@* … *@`

Comments are removed during lexing (routed to a hidden channel) and never appear in output.

```
@* ...comment text... *@
```

Comments may appear almost anywhere — including **inside other tokens**:

```heddle
<h2>@(Title)</h2>@* the article headline *@
@if(@* featured only *@ @model.IsFeatured){{ <span class="badge">★</span> }}
<a href="/po@* even mid-word *@sts">Posts</a>
```

Comments are recognized in every lexer mode (top level, subtemplate, definition, import, call,
output), so they are safe to drop in anywhere.

---

## Raw blocks `@{ … }@` and `@:`

Raw regions are emitted **verbatim** and are not parsed for directives.

```
@{ ...literal text... }@        block form
@: ...literal to end of line    line form
```

- **Block** `@{ … }@` — everything between the delimiters is literal. Handy for emitting code
  samples or text full of characters that would otherwise be parsed:

  ```heddle
  <pre>@{ Use @(Title) and {{ … }} in a template — none of this is evaluated. }@</pre>
  ```

- **Line** `@: …` — everything to the end of the line is literal:

  ```heddle
  @: Raw line: @(Title) and @if() are printed verbatim, exactly as typed.
  ```

Raw blocks are recognized at the top level, in subtemplates, in output mode, and after a
call returns, so they compose with the rest of the syntax.

---

## Whitespace trimming `@\`

`@\` followed by any run of whitespace is consumed and produces no output. Use it to keep
templates readable while controlling the emitted whitespace — particularly at the end of a
line, to suppress the trailing newline.

```
@\ followed by whitespace  →  consumed (emits nothing)
```

Declaration lines that should produce no output of their own are a common place for it — end
them with `@\` so they don't leave a blank line behind:

```heddle
@using(){{System.Linq}}@\
@model(){{Blog}}@\
<h1>@(Title)</h1>
```

Without the `@\`, each `@using`/`@model` line would emit its trailing newline. Whitespace that
is *not* trimmed is preserved exactly — Heddle is whitespace‑significant, so the spaces and
newlines you write around directives appear in the output as written.

> In practice, HTML templates rarely need `@\` — browsers collapse insignificant whitespace, so
> the stray newlines around directives don't matter. Reach for `@\` mainly in the declaration
> preamble and when emitting whitespace‑sensitive text (plain text, `<pre>`, JSON, etc.).

---

## How the lexer reads a template (modes)

Heddle is context‑sensitive: the same characters mean different things depending on where they
appear. The ANTLR lexer implements this with a stack of **modes**
([HeddleLexer.g4](../src/Heddle.Language/HeddleLexer.g4)). Understanding the modes explains why,
for example, `}}` ends a subtemplate but is plain text elsewhere.

| Mode | Entered when | Role | Notable exits |
| --- | --- | --- | --- |
| *(default)* | start of document | Top‑level text, `@…` directives, definitions, imports, raw blocks. | `@%`→DEF, `@<<`→IMPORT, `@`→OUT |
| `SUB_BLOCK` | after `{{` | Inside a subtemplate body; like default but `}}` closes it. | `}}`→pop |
| `DEF` | after `@%` | Inside a definition block: names `< >`, `:` base, `::` type, `->` default, `%@` close. | `{{`→SUB_BLOCK, `->`→OUT, `%@`→pop |
| `IMPORT_MODE` | after `@<<` | Reads the import path between `{{ }}`. | `}}`→pop |
| `OUT_MODE` | after `@` | Reads an extension name, then `(` opens the parameter. | `(`→CALL, raw/sub/def/import→transition |
| `CALL` | after `(` | Inside a parameter: member ids, `.`, `::` root ref, `:` delim, nested `(`. | `)`→pop, inner `@`→CS |
| `CALL_RETURNED` | after `)` | Decides what follows a call: `:` (chain), `@` (next out), `{{` (subtemplate), raw, etc. | many |
| `CS` | inner `@` inside a parameter | Embedded C# expression; balances nested `(` `)`, ends at the matching `)`. | matching `)`→pop |

You normally never think about modes — but they are the reason comments work everywhere,
why C# expressions can contain arbitrary parentheses, and why whitespace handling differs
slightly between a definition header and a body. For the full picture see
[Architecture → Lexing](architecture.md#1-lexing).

---

## Behavioral nuances summary

- **`@@` emits a literal `@`.** Everything else that isn't a directive is literal text.
- **Whitespace is significant.** Use `@\` to trim; otherwise spaces and newlines are emitted
  as written.
- **Comments can appear mid‑token** and are always stripped.
- **Embedded C# requires `AllowCSharp = true`**; `model`, `root`, and `chained` are the
  available identifiers.
- **`::Member` reads the root model**; plain `Member` reads the current model.
- **Definitions are invoked like extensions** (`@name()`), can be nested, typed, inherited,
  and fully overridden in document order.
- **HTML encoding is per‑extension** — see [Built‑in Extensions](built-in-extensions.md) and
  the `[EncodeOutput]` / `[NotEncode]` attributes. The unnamed `@(...)` output does **not**
  HTML‑encode; use `@string(...)` (or another `[EncodeOutput]` extension) when you need
  encoding.
- **Recursion is capped** by `TemplateOptions.MaxRecursionCount` (default 100).
- **Type mismatches and unknown members fail at compile time** (or, in `DEBUG`, when
  `Generate` is given a model of the wrong type) — see
  [error handling](csharp-api.md#errors-and-diagnostics).

Continue to the **[Built‑in Extensions](built-in-extensions.md)** reference for the helpers
you can call from these constructs.
