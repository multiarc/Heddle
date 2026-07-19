# Patterns & Recipes

Short, practical idioms distilled from real production templates. Each recipe is a *problem →
snippet → why*. For the formal rules behind them, follow the links into the
[Language Reference](language-reference.md) and [Built‑in Extensions](built-in-extensions.md).

## The model these recipes use

Most recipes render an `Article` (the blog model from the
[Language Reference](language-reference.md#the-model-used-in-these-examples)), extended with a
nullable `Summary` and an `IsArchived` flag. The app‑context recipes assume the root passed to
`Generate` is a small page context:

```csharp
class PageContext { public Article Article { get; set; }  public Site Site { get; set; }  public string ArticleJson { get; set; } }
class Site        { public string Name { get; set; }  public string Host { get; set; }  public string BuildNumber { get; set; }  public CultureInfo Culture { get; set; } }
// Article { Title, Author, PublishedOn, IsFeatured, IsArchived, string Summary, string Url, IList<string> Tags, Comments }
```

> **Inner‑`@` recipes need `ExpressionMode.FullCSharp`.** Several recipes below embed C# inside a
> directive (`@if(@chained == 0)`, `@if(@!string.IsNullOrEmpty(model.Url))`, `@slug(@chained)`,
> `@(@root.Site.BuildNumber)`, `@list(@model.Tags.Take(...))`). That is the resolver's default for
> file templates (`TemplateResolver` compiles Views/Partials with `ExpressionMode.FullCSharp`), so
> the file‑authoring path works as shown; but `new TemplateOptions()` defaults to
> `ExpressionMode.Native`, under which inner‑`@` is a compile error — set `FullCSharp` if you compile
> these directly.

## Recipes

- [The inline "with" block](#the-inline-with-block)
- [Presence checks and AND conditions](#presence-checks-and-and-conditions)
- [First, last, and the loop index](#first-last-and-the-loop-index)
- [Conditional attributes](#conditional-attributes)
- [Local helper definitions](#local-helper-definitions)
- [Root for app and page context](#root-for-app-and-page-context)
- [An entry layout that renders itself](#an-entry-layout-that-renders-itself)
- [An abstract section inside a typed page](#an-abstract-section-inside-a-typed-page)
- [A component library: typed props and a parameterized slot](#a-component-library-typed-props-and-a-parameterized-slot)
- [Inject JSON and inline scripts](#inject-json-and-inline-scripts)
- [Whitespace: when to bother with `@\`](#whitespace-when-to-bother-with-)
- [Custom extensions in practice](#custom-extensions-in-practice)
- [Components with multiple content regions](#components-with-multiple-content-regions)
- [Culture and localization](#culture-and-localization)
- [Exposing models to untrusted templates](#exposing-models-to-untrusted-templates)

---

## The inline "with" block

**Problem:** zoom into a sub‑object for a few lines without declaring a definition.

```heddle
@(Author)
{{
  <span class="byline">@(Name) &lt;@(Email)&gt;</span>
}}
```

**Why:** giving the unnamed call a parameter *and* a body sets the current model to the
parameter for the body — `@(Name)`/`@(Email)` resolve against `Author`. It's the inline
counterpart to passing a value into a definition. See
[Output blocks → the with‑block](language-reference.md#output-blocks).

---

## Presence checks and AND conditions

**Problem:** render markup only when a nullable field is set; pick between mutually exclusive
branches; combine conditions.

```heddle
@if(Summary){{ <p class="summary">@(Summary)</p> }}
@ifnot(Summary){{ <p class="muted">No summary yet.</p> }}

@* mutually exclusive branches — a real branch set *@
@if(IsFeatured){{ <span class="badge">Featured</span> }}
@elif(IsArchived){{ <span class="muted">Archived</span> }}
@else(){{ <span>Regular</span> }}

@* AND — native && in one condition *@
@if(IsFeatured && !IsArchived){{ <span class="badge">Featured</span> }}
```

**Why:** `@if` treats any non‑null, non‑bool value as "present", so `@if(member)` is an
"if set" check and `@ifnot(member)` an "if absent" check. For mutually exclusive alternatives use
[`@elif`/`@else`](built-in-extensions.md#conditionals); for AND, native `&&`/`||` combine conditions
directly (`@if(A && B)`), and nesting `@if`/`@ifnot` remains an alternative. The bodies render in the
caller's context, so `@(Summary)` inside still refers to the article. See
[`if`/`ifnot`](built-in-extensions.md#conditionals) and
[stepping back](language-reference.md#stepping-back-the-parent-context).

---

## First, last, and the loop index

**Problem:** mark the first item active; render the last item differently from the rest.

```heddle
@list(Tags)
{{
  <a class="tag@if(@chained == 0){{ active}}">@()</a>
}}
```

```heddle
@* all but the last tag, then the last on its own *@
@using(){{System.Linq}}                    @* .Take()/.LastOrDefault() need System.Linq *@
@list(@model.Tags.Take(model.Tags.Count - 1))
{{
  <li>@()</li>
}}
@(@model.Tags.LastOrDefault())
{{
  <li class="current">@()</li>
}}
```

**Why:** inside `@list`/`@for` the loop index is the [chained](language-reference.md#the-second-channel-chained-data)
value, usable in C# as `@chained` (so `@chained == 0` is the first item). For the "last"
element, LINQ splits the sequence and the [with‑block](#the-inline-with-block) renders the tail
item with its own markup.

---

## Conditional attributes

**Problem:** add an attribute only when a value is present.

```heddle
<a@if(@!string.IsNullOrEmpty(model.Url)){{ href="@(Url)"}}>@(Title)</a>
```

**Why:** an `@if` placed mid‑tag emits its body (the ` href="…"` fragment) only when the test
passes; because the body runs in the caller's context, `@(Url)` resolves on the current model.
The same shape toggles a class: `class="card@if(IsFeatured){{ card-featured}}"`.

---

## Local helper definitions

**Problem:** build repeated strings (ids, slugs) from a value, in one place.

```heddle
@%
  <slug>{{post-@int()}}        @* an int in → "post-N" out *@
%@

@list(Articles)
{{
  <section id="@slug(@chained)">@(Title)</section>   @* post-0, post-1, … *@
}}
```

Definitions can also be declared **inside** another definition's body, scoping the helper to
where it's used:

```heddle
@%
  <gallery>
  {{
    @%
      <thumb_id>{{thumb-@int()}}
    %@
    @list(Images){{ <img id="@thumb_id(@chained)" src="@(Url)"> }}
  }}
%@
```

**Why:** a definition is a reusable function — pass it a value, get a result. `@int()` formats
the current (integer) model. See [Definitions](language-reference.md#definitions---).

**Sharing a helper library across templates.** When several templates need the same helpers,
put them in a definitions‑only file and pull it in with `@<<` — the composition import merges the
definitions into each template that includes it:

```heddle
@* helpers.heddle — a shared library *@
@%
  <slug>{{post-@int()}}
  <thumb_id>{{thumb-@int()}}
%@
```

```heddle
@* any template *@
@<<{{helpers.heddle}}            @* slug/thumb_id are now callable below *@
<section id="@slug(@chained)">…</section>
```

`@<<` is the right tool here: it actually merges the definitions. A name already
defined before the import is an error, so libraries compose rather than silently override. See
[Imports](language-reference.md#imports---).

---

## Root for app and page context

**Problem:** reach site/app data and the active culture from deep inside any section.

```heddle
<link rel="canonical" href="https://@(::Site.Host)/articles/@(Title)">
<script src="/app/main.js?v=@(@root.Site.BuildNumber)"></script>

@date(PublishedOn){{@(::Site.Culture.DateTimeFormat.ShortDatePattern)}}
```

**Why:** `::` (template syntax) and `@root` (C#) reach the original model no matter how deep the
current context has narrowed — ideal for host, cache‑busting build numbers, and culture. Note
the last line: a [formatter's body runs in the caller/root context](language-reference.md#stepping-back-the-parent-context),
so the `@date` **format string itself** can be pulled from `::Site.Culture`. See
[Root reference](language-reference.md#root-reference-member).

---

## An entry layout that renders itself

**Problem:** one top‑level layout that renders automatically, works on a sub‑model, and still
reaches the whole root.

```heddle
@%
  <layout> -> (Article)
  {{
    <title>@(Title) — @(::Site.Name)</title>
    <link rel="canonical" href="https://@(::Site.Host)/articles/@(Title)">
    <main>@article_body()</main>
    <script src="/app/main.js?v=@(::Site.BuildNumber)"></script>
  }} :: PageContext
%@
```

**Why:** `-> (Article)` makes `layout` an [auto‑rendering output](language-reference.md#default-output---chain)
whose working model is `root.Article` (so `@(Title)` is the article's), while `::Site…` still
reaches the root `PageContext`. `@article_body()` is one of the page's section definitions.
Because `layout` renders on its own, you do **not** call `@layout()` — doing so renders it twice,
which the compiler flags as **HED4002** (see [Default output](language-reference.md#default-output---chain)).

---

## An abstract section inside a typed page

**Problem:** write a reusable section once and use it against whatever page renders it.

```heddle
@%
  <tag_list>                       @* no :: Type → abstract *@
  {{
    <ul class="tags">@list(Tags){{ <li>@()</li> }}</ul>
  }}

  <article_body>
  {{
    <h1>@(Title)</h1>
    @tag_list()                    @* binds to Article here *@
  }} :: Article
%@
```

**Why:** `tag_list` declares no type, so it's compiled against the model at each call site —
here `Article`, because `article_body` (typed `:: Article`) invokes it. Each use is still
statically type‑checked. See
[abstract definitions and late type binding](language-reference.md#abstract-definitions-and-late-type-binding).

---

## A component library: typed props and a parameterized slot

**Problem:** build small, reusable components that take **typed options** and let the caller
supply **typed content** — a card that varies by style, and a picker that renders the caller's
markup once per option.

```heddle
@%
  @* A card component: two typed props with defaults, plus the caller's body via @out(). *@
  <card(style: string = "plain", compact: bool = false)>
  {{
    <article class="card @(style)">
      <h2>@(Title)</h2>
      @ifnot(compact){{ <p>@(Summary)</p> }}
      @out()
    </article>
  }} :: Article

  @* A picker component: a typed slot hands each option back to the caller's body. *@
  <picker(out:: MenuOption)>
  {{ <ul>@list(Options){{ <li>@out(this)</li> }}</ul> }} :: Menu
%@

@card(Article, style: "wide", compact: true){{ <a href="/read">Read more →</a> }}
@card(Article)                                  @* both props take their defaults *@

@picker(Menu){{ <a href="/go?id=@(Id)">@(Label)</a> }}
```

**Why:**

- **Props** (`style`, `compact`) are declared with types and literal defaults, passed by name,
  and read in the body as bare names (`@(style)`, `@if(compact)`). Missing/unknown/mistyped
  props are compile errors — a component's contract is enforced at every call site.
- **The slot** (`out:: MenuOption`) types the value the picker hands back; the caller's
  `{{ <a …>@(Label)</a> }}` is compiled against `MenuOption` and rendered once per option with
  `@out(this)`.

See [Props](language-reference.md#props-nameprop-type--default) and
[Parameterized slots](language-reference.md#parameterized-slots-out-type).

---

## Inject JSON and inline scripts

**Problem:** emit a pre‑serialized JSON payload or JSON‑LD into a `<script>` verbatim.

```heddle
<script>
  var article = @raw(ArticleJson);       @* emitted verbatim under either profile *@
</script>

<script type="application/ld+json">@raw(ArticleJson)</script>
```

**Why:** a serialized JSON string must be written as‑is. Under `OutputProfile.Text` the bare
`@(ArticleJson)` already emits raw, but under an `OutputProfile.Html` host the bare form
HTML‑encodes — so use `@raw(...)` (or run this template under `Text`) to guarantee verbatim
output regardless of the host's profile. Only do this with values **you** produced — never
with untrusted text. See [Output profiles](built-in-extensions.md#output-profiles).

---

## Whitespace: when to bother with `@\`

**Problem:** decide where whitespace trimming actually matters.

**`TrimDirectiveLines` handles the boilerplate for you — and it is on by default in 2.0.** When a
whole line is nothing but a no‑output directive, the option swallows the line, so you write no `@\`
at all:

```heddle
@using(){{System.Linq}}          @* whole line swallowed — no @\ needed *@
@model(){{PageContext}}
@* preamble comment — its line goes too *@
<article>@(Title)</article>
```

If a host explicitly set `TrimDirectiveLines = false` (for 1.x byte‑identical output), or you need
**mid‑line** control the option doesn't cover, `@\` still works exactly as before:

```heddle
@using(){{System.Linq}}@\        @* @\: trims the following whitespace including the newline *@
@model(){{PageContext}}@\
<article>@(Title)</article>      @* HTML: no @\ needed — the browser ignores the whitespace *@
```

**Why:** Heddle is whitespace‑significant, but HTML collapses insignificant whitespace, so most
markup doesn't need `@\`. Reserve `@\` for mid‑line control; use `TrimDirectiveLines` for the
whole‑line preamble noise. See
[Whitespace trimming](language-reference.md#whitespace-trimming-).

---

## Custom extensions in practice

**Problem:** handle cross‑cutting concerns (escaping, asset URLs, head/script regions) cleanly.

```heddle
<img src="@asset_url(CoverImage).webp" alt="@quote(Title)">

@head(){{ <meta name="description" content="@quote(Summary)"> }}
@script(){{ <script src="/app/main.js?v=@(::Site.BuildNumber)"></script> }}
```

**Why:** these are project‑specific [custom extensions](custom-extensions.md): value
transforms (`asset_url`, `quote`) and region collectors (`head`, `script`) that capture their
body and emit it elsewhere (the `<head>`, end‑of‑body scripts). See
[common categories in practice](custom-extensions.md#common-categories-in-practice).

---

## Components with multiple content regions

**Problem:** build a component that exposes *several* content regions — a header, a footer, and a
main body — the way other frameworks do with multiple named slots.

A Heddle definition has exactly **one** typed slot (`@out()` / `out:: Type`). Declaring a second
`out::` is a compile error (**HED5017** — see
[Parameterized slots](language-reference.md#parameterized-slots-out-type)). For extra regions you
have two tools:

1. **Named content regions `<:name>`** — declare overridable regions *inside* the component as
   public inner definitions (`<:heading>`, `<:item :: Article>`), and fill them per call with the
   ordinary `<name:name>` override in the call body. Call-scoped, visibility-gated
   (private regions cannot be overridden from a call site — **HED5019**), and the override body
   runs in the region's own context. See
   [Named content regions](language-reference.md#named-content-regions-name).
2. **Sibling definitions the caller overrides** (the pre-existing idiom below): reserve the single
   `@out()` slot for the main region, and expose each additional region as its own document-scope
   definition with default content that a call site replaces via a `<name:name>`
   [override](language-reference.md#inheritance-and-override-childbase) — document-scoped
   (the override applies from that point onward, to every later call).

The sibling recipe:

```heddle
@%
  @* Two content regions, each with a default the caller may replace… *@
  <shell_header>{{ <header><h1>@(Site.Name)</h1></header> }}
  <shell_footer>{{ <footer>@(Site.Name)</footer> }}

  @* …and the shell that lays them out around the single main slot. *@
  <page_shell>
  {{
    <body>
      @shell_header()
      <main>@out()</main>          @* the one typed slot: the caller's body *@
      @shell_footer()
    </body>
  }} :: PageContext
%@
```

A call site fills the main region with the `@page_shell()` body and overrides only the regions it
cares about by re‑declaring them as `<shell_header:shell_header>`. The compact one‑line form below is
a style choice — a multi‑line indented override body renders identically:

```heddle
@%
  @* this page wants a hero header — compact one-line override form (multi-line works too) *@
  <shell_header:shell_header>{{ <header class="hero"><h1>@(Article.Title)</h1></header> }}
%@
@page_shell(){{ <article>@(Article.Summary)</article> }}
```

This renders the shell with the page's hero header, the caller's body in `<main>`, and the shell's
own default footer:

```html
<body>
  <header class="hero"><h1>…Article.Title…</h1></header>
  <main><article>…Article.Summary…</article></main>
  <footer>…Site.Name…</footer>
</body>
```

**Why:** the override is layered in document order, so the shell renders the page's header and its
own default footer around the caller's `@out()` body — three independently supplied regions from a
one‑slot definition. Region overrides are the multi‑slot idiom here, **not** a workaround: they
compose without coupling (the shell knows nothing about the page), stay statically type‑checked,
and cost nothing at render time. See
[composition without coupling](language-reference.md#inheritance-and-override-childbase).

---

## Culture and localization

**Problem:** render dates, money, and messages for a specific culture when the built‑ins are
invariant‑culture on purpose.

**Culture lives on the root model.** Put the active `CultureInfo` on the page context and reach it
with `::` from anywhere; a formatter's body runs in the caller/root context, so you can feed a
culture‑derived **format pattern** straight into `@date`/`@time`:

```heddle
@date(PublishedOn){{yyyy-MM-dd}}                                        @* fixed ISO pattern *@
@date(PublishedOn){{@(::Site.Culture.DateTimeFormat.ShortDatePattern)}}  @* the culture's date pattern *@
@time(PublishedOn){{@(::Site.Culture.DateTimeFormat.ShortTimePattern)}}
```

**Per‑call locale on the formatting built‑ins.** The optional body of these extensions is their
locale/format hint — note that the three read it differently (verified against source):

- **`@money(value){{ locale }}`** — the body is a **culture name** (e.g. `en-US`). It formats the
  value with the currency pattern (`"c"`) of `new CultureInfo(locale)`; an empty body falls back
  to the thread's current culture. This is the one built‑in whose body selects a *culture*.

  ```heddle
  @money(Price){{en-US}}                     @* $1,234.56 — explicit locale *@
  @money(Price){{@(::Site.Culture.Name)}}    @* locale taken from the root model *@
  @money(Price)                              @* no body → the thread's current culture *@
  ```

- **`@date(value){{ pattern }}`** and **`@time(value){{ pattern }}`** — the body is a .NET
  **format pattern** (defaults `d` / `t`), always applied under `CultureInfo.InvariantCulture`.
  They take a *pattern*, never a locale — which is why the culture escape above passes the
  culture's `ShortDatePattern`/`ShortTimePattern` rather than its name.

**Anything beyond formatting — register a host function.** Message lookup and pluralization aren't
built‑ins; expose them through the [`FunctionRegistry`](native-expressions.md#registered-functions)
so they become callable from template text. Because the registry freezes on first compile, register
a `t` whose closure resolves against a culture‑aware store **at render time** (via
`CultureInfo.CurrentUICulture`) rather than re‑registering per request:

```csharp
var strings = new ResourceManager("MyApp.Messages", typeof(Program).Assembly);

var functions = new FunctionRegistry();
functions.Register("t", (Func<string, string>)(key =>
    strings.GetString(key, CultureInfo.CurrentUICulture) ?? key));

var options = new TemplateOptions { Functions = functions };
```

```heddle
<p>@(t("greeting")), @(Name)!</p>       @* "Bonjour" under fr-FR, "Hello" under en-US *@
```

Set `CultureInfo.CurrentUICulture` per request; a `t(key, count)` overload layers pluralization on
the same seam.

**Why:** the built‑ins are invariant‑culture **by design** — deterministic, golden‑testable output
that never shifts with the machine's regional settings. The root‑culture pattern (a `CultureInfo`
on the model feeding format patterns, plus registered culture‑aware functions) is the sanctioned
escape when you *do* need localized output. See
[Root reference](language-reference.md#root-reference-member).

---

## Exposing models to untrusted templates

**Problem:** let end users author templates without handing them your application internals.

Heddle's sandbox (`ExpressionMode.Native` + a curated `FunctionRegistry`) blocks arbitrary C#, but
the model you pass and the profile you pick are still yours to get right. The four legs below close
the confidentiality, integrity, and availability gaps.

**Getters execute — expose dedicated DTOs, never live objects.** A member access runs the property
getter, so `@(User.AccountBalance)` triggers whatever that getter does. Passing a live domain or EF
entity hands templates its lazy‑loading, side‑effecting, and navigation‑property surface. Project to
a flat DTO of plain auto‑properties that carries only what the template may read.

**`[Hidden]` trims a surface — it is not the boundary.** `[Heddle.Attributes.Hidden]` marks a
**property** (its only valid target) so member‑path resolution treats it as *not found* — the same
compile error as a misspelled member, applied uniformly to the member tier and the native‑expression
tier. It is non‑inherited and does not affect fields or methods (neither is reachable from template
text anyway). Use it to prune a DTO, but treat the DTO's *shape* as the real boundary, not `[Hidden]`.

**The `FunctionRegistry` freeze is the whole trust boundary.** Anything registered is callable from
template text and nothing else is — there is no path from a template to an arbitrary method by name.
The registry **freezes on first compile use** (`IsFrozen`); registering after that throws
`InvalidOperationException`. So the whitelist you build *before* the first compile is the complete,
immutable set of host code a template can invoke. Register narrowly and vet every function for side
effects. See [Registering your own](native-expressions.md#registering-your-own).

**`ExpressionMode.Native` is required.** `FullCSharp` opens the inner‑`@` Roslyn tier — arbitrary
C#, method calls, `new`, LINQ — and must never be used for untrusted input. `Native` compiles only
operators, literals, member paths, and registered functions; everything else is a positioned
compile error that never executes. See [`ExpressionMode`](native-expressions.md#expressionmode) and
[The sandbox](native-expressions.md#the-sandbox).

**Render budgets are the availability leg.** The three legs above stop a template from *reading* or
*calling* what it shouldn't, but a well‑formed template can still burn CPU or memory
(`@for(1000000000){{x}}` compiles cleanly). Set a
[`RenderBudget`](csharp-api.md#render-budgets) on `TemplateOptions` — `MaxOutputChars`,
`MaxRenderOps`, and `MaxRenderTime` cap output size, render operations, and wall‑clock time at the
render seam and throw `TemplateRenderBudgetException` on breach. Because enforcement counts render
*operations*, not loop iterations, `MaxRenderTime` is the mandatory backstop for zero‑output loops;
set it for any untrusted render. On a streaming sink the exception fires after partial output has
been written, so treat it as "abort the response."

**Encoding contexts.** Under `OutputProfile.Html` bare `@(value)` is HTML‑encoded for element text;
non‑element contexts need the matching leaf extension — `@attr` for attribute values, `@js` for JS
string literals, `@url` for URL components, and `@raw` only for markup you produced. See
[Encoding contexts](built-in-extensions.md#encoding-contexts).

**Checklist — copy into your host setup:**

- [ ] **Mode** — compile untrusted templates with `ExpressionMode.Native` (never `FullCSharp`).
- [ ] **Registry** — expose only vetted, side‑effect‑free functions on a `FunctionRegistry`; it
      freezes on first compile, so the whitelist is fixed for the process.
- [ ] **DTOs** — pass purpose‑built DTOs, never live domain/EF entities; `[Hidden]` any property
      that must not appear.
- [ ] **Budgets** — set a [`RenderBudget`](csharp-api.md#render-budgets) (output chars, render ops,
      **and** wall‑clock time) so a hostile template can't exhaust resources; `MaxRenderTime` is
      mandatory — it's the only backstop for a zero‑output loop.
- [ ] **Profile** — render under `OutputProfile.Html` so bare `@(value)` output is encoded by
      default.
- [ ] **Encoding** — use `@attr`/`@js`/`@url` for non‑element contexts; reserve `@raw` for values
      you produced.
