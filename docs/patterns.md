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
class PageContext { Article Article;  Site Site;  string ArticleJson; }
class Site        { string Name;  string Host;  string BuildNumber;  CultureInfo Culture; }
// Article { Title, Author, PublishedOn, IsFeatured, IsArchived, string Summary, IList<string> Tags, Comments }
```

## Recipes

- [The inline "with" block](#the-inline-with-block)
- [Presence checks and AND conditions](#presence-checks-and-and-conditions)
- [First, last, and the loop index](#first-last-and-the-loop-index)
- [Conditional attributes](#conditional-attributes)
- [Local helper definitions](#local-helper-definitions)
- [Root for app and page context](#root-for-app-and-page-context)
- [An entry layout that renders itself](#an-entry-layout-that-renders-itself)
- [An abstract section inside a typed page](#an-abstract-section-inside-a-typed-page)
- [Inject JSON and inline scripts](#inject-json-and-inline-scripts)
- [Whitespace: when to bother with `@\`](#whitespace-when-to-bother-with-)
- [Custom extensions in practice](#custom-extensions-in-practice)

---

## The inline "with" block

**Problem:** zoom into a sub‑object for a few lines without declaring a definition.

```ttl
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

**Problem:** render markup only when a nullable field is set; combine conditions (there is no
`else`/`elif`).

```ttl
@if(Summary){{ <p class="summary">@(Summary)</p> }}
@ifnot(Summary){{ <p class="muted">No summary yet.</p> }}

@if(IsFeatured)
{{
  @ifnot(IsArchived){{ <span class="badge">Featured</span> }}
}}
```

**Why:** `@if` treats any non‑null, non‑bool value as "present", so `@if(member)` is an
"if set" check and `@ifnot(member)` an "if absent" check. Nest `@if`/`@ifnot` for AND. The
bodies render in the caller's context, so `@(Summary)` inside still refers to the article. See
[`if`/`ifnot`](built-in-extensions.md#conditionals) and
[stepping back](language-reference.md#stepping-back-the-parent-context).

---

## First, last, and the loop index

**Problem:** mark the first item active; render the last item differently from the rest.

```ttl
@list(Tags)
{{
  <a class="tag@if(@chained == 0){{ active}}">@()</a>
}}
```

```ttl
@* all but the last tag, then the last on its own *@
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

```ttl
<a@if(@!string.IsNullOrEmpty(model.Url)){{ href="@(Url)"}}>@(Title)</a>
```

**Why:** an `@if` placed mid‑tag emits its body (the ` href="…"` fragment) only when the test
passes; because the body runs in the caller's context, `@(Url)` resolves on the current model.
The same shape toggles a class: `class="card@if(IsFeatured){{ card-featured}}"`.

---

## Local helper definitions

**Problem:** build repeated strings (ids, slugs) from a value, in one place.

```ttl
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

```ttl
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

---

## Root for app and page context

**Problem:** reach site/app data and the active culture from deep inside any section.

```ttl
<link rel="canonical" href="https://@(::Site.Host)/articles/@(Title)">
<script src="/app/main.js?v=@(@root.Site.BuildNumber)"></script>

@date(PublishedOn){{ @(::Site.Culture.DateTimeFormat.ShortDatePattern) }}
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

```ttl
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
Because `layout` renders on its own, you do **not** call `@layout()`.

---

## An abstract section inside a typed page

**Problem:** write a reusable section once and use it against whatever page renders it.

```ttl
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

## Inject JSON and inline scripts

**Problem:** emit a pre‑serialized JSON payload or JSON‑LD into a `<script>` verbatim.

```ttl
<script>
  var article = @(ArticleJson);          @* emitted raw — NOT HTML-encoded *@
</script>

<script type="application/ld+json">@(ArticleJson)</script>
```

**Why:** the unnamed `@(...)` does not HTML‑encode, so a serialized JSON string is written
as‑is. Only do this with values **you** produced — never with untrusted text. See
[HTML encoding](built-in-extensions.md#html-encoding).

---

## Whitespace: when to bother with `@\`

**Problem:** decide where whitespace trimming actually matters.

```ttl
@using(){{System.Linq}}@\        @* trim: keeps the preamble from emitting blank lines *@
@model(){{PageContext}}@\
<article>@(Title)</article>      @* HTML: no @\ needed — the browser ignores the whitespace *@
```

**Why:** TTL is whitespace‑significant, but HTML collapses insignificant whitespace, so most
markup doesn't need `@\`. Reserve it for the declaration preamble and for whitespace‑sensitive
output (plain text, `<pre>`, JSON). See
[Whitespace trimming](language-reference.md#whitespace-trimming-).

---

## Custom extensions in practice

**Problem:** handle cross‑cutting concerns (escaping, asset URLs, head/script regions) cleanly.

```ttl
<img src="@asset_url(CoverImage).webp" alt="@quote(Title)">

@head(){{ <meta name="description" content="@quote(Summary)"> }}
@script(){{ <script src="/app/main.js?v=@(::Site.BuildNumber)"></script> }}
```

**Why:** these are project‑specific [custom extensions](custom-extensions.md): value
transforms (`asset_url`, `quote`) and region collectors (`head`, `script`) that capture their
body and emit it elsewhere (the `<head>`, end‑of‑body scripts). See
[common categories in practice](custom-extensions.md#common-categories-in-practice).
