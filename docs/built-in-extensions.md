# Built‑in Extensions

Extensions are the verbs of Heddle: every `@name(...)` directive invokes an extension. This page
documents the extensions bundled with the `Heddle` assembly. Each entry lists the name
(`[ExtensionName]`), the expected input type (`[DataType]`, when declared), whether the output
is HTML‑encoded, and what the optional parameter / subtemplate mean.

All names and attributes below were read directly from
[src/Heddle/Extensions](../src/Heddle/Extensions). To write your own, see
[Writing Custom Extensions](custom-extensions.md).

## How to read these entries

- **Input** — the model value the extension expects (its `[DataType]`). If the actual model
  doesn't match, most extensions render empty rather than throw.
- **Body / parameter** — many extensions interpret their `{{ … }}` subtemplate as a *format
  string* or *item template* rather than literal output (e.g. `@date(d){{ yyyy-MM-dd }}`).
- **Body context** — for the conditionals and formatters (`if`, `ifnot`, `date`, `time`,
  `int`, `money`, `guid`, `string`) and `for`, the parameter is a value to test/format and the
  body renders in the **caller's** context (one step back), so `@if(flag){{ @(Title) }}` still
  sees the surrounding model. `list` and definitions instead render the body against the
  parameter. See [Language Reference → stepping back](language-reference.md#stepping-back-the-parent-context).
- **HTML‑encoded** — extensions marked `[EncodeOutput]` (and deriving from
  `AbstractHtmlExtension`) HTML‑encode their result by default. See
  [HTML encoding](#html-encoding) at the bottom.

---

## Conditionals

### `if`
[IfExtension.cs](../src/Heddle/Extensions/IfExtension.cs) · input: `bool` (or any object)

Renders its subtemplate when the condition is truthy. A `null` model renders nothing; a
`bool` model renders the body only when `true`; a non‑bool, non‑null model is treated as
present (renders the body).

```heddle
@if(IsFeatured){{ <span class="badge">Featured</span> }}
@if(@model.Comments.Count > 0){{ <h3>Comments</h3> }}
```

Because a non‑null, non‑bool value counts as present, **`@if(member)` is the idiomatic
"if present" check** for a nullable reference: `@if(Summary){{ <p>@(Summary)</p> }}` renders
only when `Summary` is set. The body runs in the caller's context (see
[stepping back](language-reference.md#stepping-back-the-parent-context)), so `@(Summary)` inside
it still refers to the surrounding model.

### `ifnot`
[IfNotExtension.cs](../src/Heddle/Extensions/IfNotExtension.cs) · input: `bool` (or `null`)

The inverse of `if`. Renders its body when the model is `null` or `false`. A non‑bool,
non‑null model renders nothing — so `@ifnot(member)` is the idiomatic "if absent" check.

```heddle
@ifnot(IsFeatured){{ <span class="muted">Regular post</span> }}
@ifnot(Summary){{ <p class="muted">No summary yet.</p> }}
```

> There is no `else`/`elif`. Combine `@if`/`@ifnot` (often nested) for the branches you need —
> see [Patterns → conditionals](patterns.md#presence-checks-and-and-conditions).

---

## Iteration

### `list`
[ListExtension.cs](../src/Heddle/Extensions/ListExtension.cs) · input: `IEnumerable`

Renders its subtemplate once per element; inside the body the current model is the element,
and an `int` index is available as the chained value. Works with both `dynamic` sequences and
strongly‑typed `IEnumerable<T>` (the element type is inferred for typed member access). When
the source implements `ICollection<T>`, the count is used to pre‑size the output buffer.

```heddle
@list(Articles){{ @article_card() }}                @* each element is an Article *@

@list(Tags){{ <span class="tag">@()</span> }}       @* @() is the current tag string *@

@list(@model.Articles.Where(a => a.IsFeatured)){{ @article_card() }}   @* filtered *@
```

### `for`
[ForIndexExtension.cs](../src/Heddle/Extensions/ForIndexExtension.cs) · input: `ForModel`

A counted loop. The model is a `ForModel { Start?, Last, Step? }`
([Heddle.Models](../src/Heddle/Models)); it iterates `i = Start (default 0)` while
`i < Last`, incrementing by `Step` (default 1). The loop index is exposed to the body as the
chained value (referenceable as `chained` in embedded C#).

```heddle
@for(@new ForModel() { Last = model.Articles.Count(), Step = 3 })
{{
  <div class="row">
    @list(@model.Articles.Skip(chained).Take(3)){{ @article_card() }}
  </div>
}}
```

(`chained` is the loop index; here it pages the articles three at a time.)

---

## Formatting

These all derive from `AbstractHtmlExtension` and are `[EncodeOutput]` (except `guid`). Their
optional body is the **format string**.

### `date`
[DateExtension.cs](../src/Heddle/Extensions/DateExtension.cs) · input: `DateTime` · HTML‑encoded

Formats a `DateTime` using the body as a .NET date format string (default `"d"`), with
`CultureInfo.InvariantCulture`.

```heddle
@date(PublishedOn){{ MMMM d, yyyy }}     @* e.g. June 28, 2026 *@
@date(PublishedOn)                       @* default short date *@
```

### `time`
[TimeExtension.cs](../src/Heddle/Extensions/TimeExtension.cs) · input: `DateTime` · HTML‑encoded

Like `date` but defaults to the `"t"` (short time) format.

```heddle
@time(PublishedOn){{ HH:mm }}
```

### `int`
[IntegerExtension.cs](../src/Heddle/Extensions/IntegerExtension.cs) · input: `int` / `long` · HTML‑encoded

Formats an integer; the body is an optional numeric format string. Other numeric types are
converted to `long` when possible (invariant culture); non‑convertible values render empty.

```heddle
@int(Year)                          @* 2026 *@
@int(@model.Comments.Count){{ 0:N0 }}   @* 1,200 *@
```

### `money`
[MoneyExtension.cs](../src/Heddle/Extensions/MoneyExtension.cs) · input: `decimal` · HTML‑encoded

Formats a `decimal` as currency (`"c"`). The body is an optional **culture name**; with no
body the current culture is used. Cultures are cached.

```heddle
@money(@9.99m){{ en-US }}    @* $9.99 — body is the culture name *@
@money(@1234.5m)             @* current culture *@
```

### `guid`
[GuidExtension.cs](../src/Heddle/Extensions/GuidExtension.cs) · input: `Guid` · *not* encoded

Formats a `Guid`; the body is an optional .NET GUID format specifier (`N`, `D`, `B`, `P`,
`X`).

```heddle
@guid(@System.Guid.NewGuid()){{N}}   @* 32 digits, no dashes *@
@guid(@System.Guid.NewGuid())        @* default format *@
```

### `string`
[StringExtension.cs](../src/Heddle/Extensions/StringExtension.cs) · input: `string` (or any) · HTML‑encoded

Converts the model to a string. The optional body is a **default/fallback** rendered when the
model is `null`. Non‑string models are converted with `Convert.ChangeType` (invariant
culture), falling back to `ToString()`.

```heddle
@string(Title)
@string(Author.Name){{ Anonymous }}    @* fallback when Author.Name is null *@
```

---

## Output and context

### Empty / unnamed
[EmptyExtension.cs](../src/Heddle/Extensions/EmptyExtension.cs) · name: `""`

The extension with the empty name backs the unnamed call form `@(...)`. If it has a body it
renders the body; otherwise it stringifies the current model (or empty when `null`). This is
the workhorse behind `@(Title)`, `@()`, etc.

> The unnamed `@(...)` form is **not** HTML‑encoded. For encoded text output use
> [`string`](#string) (or `html` below).

### `html`
[EmptyHtmlExtension.cs](../src/Heddle/Extensions/EmptyHtmlExtension.cs) · name: `html` · HTML‑encoded

Same behavior as the empty extension, but HTML‑encodes its output (`[EncodeOutput]`). Use it
when you want the "just stringify the value" behavior *with* encoding.

```heddle
@html(UserSuppliedText)
```

### `out`
[OutExtension.cs](../src/Heddle/Extensions/OutExtension.cs) · name: `out`

Emits the **chained** value — i.e. the data handed to the current definition/call. With no
body it renders the chained value directly; with a body it renders that body against the
chained data. This is how a definition surfaces the caller's inline content.

```heddle
<article_card>
{{ <article><h2>@(Title)</h2>@out()</article> }}    @* @out() drops in the caller's body *@
```

`@out()` is the mechanism behind layouts: a `layout` definition wraps the page chrome around a
central `@out()`, and each page supplies the content. See
[Language Reference → composition](language-reference.md#inheritance-and-override-childbase).

### `swap`
[SwapExtension.cs](../src/Heddle/Extensions/SwapExtension.cs) · name: `swap`

Swaps the model and chained values for the duration of its body — useful when an extension
chain produced a value you now want to treat as the model.

### `param`
[ParamExtension.cs](../src/Heddle/Extensions/ParamExtension.cs) · name: `param`

Passes the model through unchanged as the chain value (a no‑op renderer that yields the
current model as the chained input of the call to its left).

---

## Declarations (no output)

These configure compilation; they emit nothing.

### `model`
[ModelExtension.cs](../src/Heddle/Extensions/ModelExtension.cs) · name: `model`

Declares the document's model type from *within the template* (instead of in C# code). The
body is a type name resolved against the imported namespaces; `dynamic` is allowed.

```heddle
@model(){{Blog}}
@model(){{dynamic}}
```

### `using`
[UsingExtension.cs](../src/Heddle/Extensions/UsingExtension.cs) · name: `using`

Imports a C# namespace so that type names (in `@model()`, `:: Type`) and embedded C# can
resolve unqualified identifiers.

```heddle
@using(){{System.Linq}}
@using(){{MyBlog.Models}}
```

### `import`
[ImportExtension.cs](../src/Heddle/Extensions/ImportExtension.cs) · name: `import`

Compile‑time include: reads another template file (relative to `RootPath`) and parses its
**definitions** into the current parse context. This is the extension behind the
[`@<<{{ path }}`](language-reference.md#imports--) sugar. Emits no output of its own.

---

## Embedding whole templates

### `partial`
[PartialExtension.cs](../src/Heddle/Extensions/PartialExtension.cs) · name: `partial`

Compiles a **separate** template by name (its body is the template name) and renders that
template's output inline at run time, passing the current model and chained data. Unlike
`import`, a partial produces output.

```heddle
@partial(){{ sidebar }}      @* compiles & renders the "sidebar" template by name *@
```

The body (`sidebar`) names a separate template file the engine compiles and renders inline,
passing the current model. In ASP.NET Core MVC, partial resolution is specialized by
[`PartialMvcExtension`](../src/Heddle.Mvc/Extensions/PartialMvcExtension.cs) — see
[MVC Integration](mvc-integration.md).

---

## Quick reference

| Name | Source | Input type | Encoded | Body means |
| --- | --- | --- | --- | --- |
| `if` | IfExtension | `bool`/any | – | content shown when true |
| `ifnot` | IfNotExtension | `bool`/`null` | – | content shown when false/null |
| `list` | ListExtension | `IEnumerable` | – | per‑element template |
| `for` | ForIndexExtension | `ForModel` | – | per‑iteration template |
| `date` | DateExtension | `DateTime` | ✔ | date format string (def. `d`) |
| `time` | TimeExtension | `DateTime` | ✔ | time format string (def. `t`) |
| `int` | IntegerExtension | `int`/`long` | ✔ | numeric format string |
| `money` | MoneyExtension | `decimal` | ✔ | culture name |
| `guid` | GuidExtension | `Guid` | – | GUID format specifier |
| `string` | StringExtension | `string`/any | ✔ | fallback when null |
| *(empty)* | EmptyExtension | any | – | optional body, else stringify model |
| `html` | EmptyHtmlExtension | any | ✔ | optional body, else stringify model |
| `out` | OutExtension | chained | – | template over chained data |
| `swap` | SwapExtension | – | – | body with model/chained swapped |
| `param` | ParamExtension | any | – | pass model through chain |
| `model` | ModelExtension | – | – | model type name |
| `using` | UsingExtension | – | – | namespace to import |
| `import` | ImportExtension | – | – | file to include (definitions) |
| `partial` | PartialExtension | model | – | template name to render |

> The MVC package overrides `import` with
> [`ImportMvcExtension`](../src/Heddle.Mvc/Extensions/UseMvcExtension.cs) and adds an MVC
> partial. Whichever assembly you pass to `HeddleTemplate.Configure` determines which set is
> active; a later `[ExtensionName(...)]` registration replaces an earlier one of the same name.

---

## HTML encoding

Encoding is decided **per extension**, not globally:

- Extensions marked **`[EncodeOutput]`** (and deriving from `AbstractHtmlExtension`)
  HTML‑encode their output with `WebUtility.HtmlEncode`. These are `date`, `time`, `int`,
  `money`, `string`, and `html`.
- The unnamed `@(...)` output and non‑HTML extensions (`guid`, `out`, `list`, `if`, …) emit
  their text **as‑is**.
- The **`[NotEncode]`** attribute, applied to a *model property*, marks that property's value
  as pre‑trusted so it is not encoded even when flowing through an encoding extension. See
  [src/Heddle/Attributes/NotEncodeAttribute.cs](../src/Heddle/Attributes/NotEncodeAttribute.cs).

To emit user‑controlled text safely, prefer `@string(...)` or `@html(...)` over the bare
`@(...)` form.
