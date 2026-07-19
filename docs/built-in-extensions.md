# Built‑in Extensions

Extensions are the verbs of Heddle: every `@name(...)` directive invokes an extension. This page
documents the extensions bundled with the `Heddle` assembly. Each entry lists the name
(`[ExtensionName]`), the expected input type (`[DataType]`, when declared), whether the output
is HTML‑encoded, and what the optional parameter / subtemplate mean.

All names and attributes below were read directly from
[src/Heddle/Extensions](../src/Heddle/Extensions). To write your own, see
[Writing Custom Extensions](custom-extensions.md).

> **Inner‑`@` examples need `ExpressionMode.FullCSharp`.** Snippets on this page that embed C#
> inside a directive (`@int(@model.Comments.Count)`, `@money(@9.99m)`, `@list(@model.…Where…)`,
> `@guid(@System.Guid.NewGuid())`, `@for(@new Heddle.Models.Range…)`) require the inner‑`@` C# tier. That is
> the resolver's default for file templates (`TemplateResolver` compiles Views/Partials with
> `ExpressionMode.FullCSharp`), so the file‑authoring path just works; but `new TemplateOptions()`
> defaults to `ExpressionMode.Native`, under which inner‑`@` is a compile error. Set
> `ExpressionMode.FullCSharp` on the options if you compile these directly.

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

### Body context at a glance

This is the **authoritative enumeration** of what each built‑in does to its `{{ … }}` body — the
`@list`/definitions *descend* into the parameter while the value calls *step back* to the caller
([Language Reference → stepping back](language-reference.md#stepping-back-the-parent-context)). The
**Body context** column is read from each extension's `InitStart` in
[`src/Heddle/Extensions`](../src/Heddle/Extensions); the **`@out()` sees** column is the value on the
[chained channel](language-reference.md#the-second-channel-chained-data) inside that body.

| Extension | Body context | `@out()` inside the body sees |
| --- | --- | --- |
| `if` | steps back to caller | caller's chained (passed through) |
| `ifnot` | steps back to caller | caller's chained (passed through) |
| `elif` / `elseif` | steps back to caller | caller's chained (passed through) |
| `else` | steps back to caller | caller's chained (passed through) |
| `list` | descends into parameter (the element) | element index (`int`) |
| `for` | steps back to caller | loop index (`int`) |
| `date` | steps back to caller | caller's chained (passed through) |
| `time` | steps back to caller | caller's chained (passed through) |
| `int` | steps back to caller | caller's chained (passed through) |
| `money` | steps back to caller | caller's chained (passed through) |
| `guid` | steps back to caller | caller's chained (passed through) |
| `string` | steps back to caller | caller's chained (passed through) |
| `attr` | steps back to caller | caller's chained (passed through) |
| `js` | steps back to caller | caller's chained (passed through) |
| `url` | steps back to caller | caller's chained (passed through) |
| *(empty)* `@(v){{…}}` | descends into parameter | caller's chained (passed through) |
| `raw` | descends into parameter | caller's chained (passed through) |
| `html` | descends into parameter | caller's chained (passed through) |
| `out` | rescopes to the chained value | parent model |
| `swap` | rescopes to the chained value | the pre‑swap model |
| `param` | no body | — |
| `model` | directive (compile‑time body) | — |
| `using` | directive (compile‑time body) | — |
| `profile` | directive (compile‑time body) | — |
| `partial` | directive (compile‑time body)¹ | — |

- **steps back to caller** — the parameter is a value to test/format; the body renders against the
  caller's model (`scope.Parent()`), one level back.
- **descends into parameter** — the body's model *is* the parameter (a rescoping container).
- **rescopes to the chained value** — `@out`/`@swap` rebind the body's model to the chained data
  (and thread a different value onto the chained channel, shown in the last column).
- **no body** — the `{{ … }}` body is not rendered.
- **directive (compile‑time body)** — the body is consumed at compile time (a type/namespace/profile
  literal, or ¹the template *name* for `@partial`), not rendered as a runtime subtemplate.

---

## Conditionals

### `if`
[IfExtension.cs](../src/Heddle/Extensions/IfExtension.cs) · input: `bool` (or any object)

Renders its subtemplate when the condition is truthy. A `null` model renders nothing; a
`bool` model renders the body only when `true`; a non‑bool, non‑null model is treated as
present (renders the body).

```heddle
@if(IsFeatured){{ <span class="badge">Featured</span> }}
@if(Comments.Count > 0){{ <h3>Comments</h3> }}
```

> The parameter of `@if`/`@for` is any [native expression](native-expressions.md), so
> `@if(Count > 0)` and `@if(Price * Quantity > 100)` work without the `@` C# tier.

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

### `elif` (alias `elseif`)
[ElifExtension.cs](../src/Heddle/Extensions/ElifExtension.cs) · input: `bool` (or any object)

Continues a branch set opened by a preceding `@if`/`@ifnot`. It renders its body only when no
earlier branch of the set fired **and** its own condition is truthy. Once a branch has fired,
every later `@elif`/`@else` in the set renders nothing (its condition is still evaluated, only
its body is skipped). `elseif` is an exact alias.

```heddle
@if(IsFeatured){{ <b>Featured</b> }}
@elif(IsArchived){{ <i>Archived</i> }}
```

With no preceding `@if`/`@ifnot` at the same level, `@elif` behaves exactly like `@if` (it
starts a new set) and the compiler emits a `HED3002` warning ("`'@elif' is a branch continuation
with no preceding opener in this scope — it starts a new set.`").

### `else`
[ElseExtension.cs](../src/Heddle/Extensions/ElseExtension.cs) · no condition

The terminal branch of a set: it renders its body when no earlier branch fired, then closes the
set. `@else` takes no parameter — a supplied one is evaluated and ignored (`HED3004`, "`A branch
terminal takes no condition — its parameter is ignored.`"). An `@else` with no open set at the same
level (an orphan, or a second `@else` after the set was already closed) is a **compile error**
`HED3003` ("`'@else' is a branch terminal with no matching opener in this scope.`").

```heddle
@if(IsFeatured){{ <b>Featured</b> }}
@elif(IsArchived){{ <i>Archived</i> }}
@else(){{ Regular }}
```

#### Branch sets

`@if`/`@ifnot`, `@elif`/`@elseif` and `@else` are ordinary standalone extensions; there is no
new syntax and no `{{endif}}`. A **branch set** is recognized at compile time by document order
at one body level: an opener (`@if`/`@ifnot`) followed by any run of `@elif` blocks and at most
one `@else`. Only the winning branch's body renders.

- **`@else` binds to the set state, not to a specific `@if`.** A set is "satisfied" once any
  branch's condition was true (an `@if` whose body renders empty still satisfies it). `@else`
  fires only when the set is unsatisfied.
- **Text between the blocks of one set is never rendered.** It is stripped at compile time;
  a stripped gap that holds anything beyond whitespace draws a `HED3001` warning. Text *before*
  the opener and *after* the last branch renders normally. Comments (`@* … *@`) between blocks
  live on the hidden channel and never affect the set.
- **A new `@if` starts a new set**, and a **non‑branch block between branches ends the set for
  the purposes of text stripping** but leaves the set state intact — so `@if(A){{…}} @date(D){{…}} @else(){{…}}`
  keeps `@else` bound to the open set while the text around `@date` still renders.
- **Imports.** Blocks spliced in by `@<<{{ … }}` are siblings of the importing body: they join
  its branch sets and its compile‑time scan.
- **Isolation.** Each `@list`/`@for` iteration, each nested body, and each `@partial` gets its
  own set state — an inner set can never satisfy or clear an outer one.

These four are the engine's own **role‑carrying** extensions: each declares its position in a set
with `[BranchRole]` (opener / continuation / terminal). Nothing about the classification is
name‑specific, so a custom extension with the same roles gets the same set semantics and can even
share a set with the built‑ins — see
[Writing Custom Extensions → Building your own branch set](custom-extensions.md#building-your-own-branch-set).
Custom extensions can also read or drive a set's state through the public `BranchState` channel — see
[Writing Custom Extensions → the local context channel](custom-extensions.md#the-local-context-channel).

---

## Iteration

### `list`
[ListExtension.cs](../src/Heddle/Extensions/ListExtension.cs) · input: `IEnumerable`

Renders its subtemplate once per element; inside the body the current model is the element,
and an `int` index is available as the chained value. Works with both `dynamic` sequences and
strongly‑typed `IEnumerable<T>` (the element type is inferred for typed member access). When
the source implements `ICollection<T>`, the count is used to pre‑size the output buffer.

```heddle
@using(){{System.Linq}}                             @* .Where(...) needs System.Linq *@

@list(Articles){{ @article_card() }}                @* each element is an Article *@

@list(Tags){{ <span class="tag">@()</span> }}       @* @() is the current tag string *@

@list(@model.Articles.Where(a => a.IsFeatured)){{ @article_card() }}   @* filtered *@
```

### `for`
[ForIndexExtension.cs](../src/Heddle/Extensions/ForIndexExtension.cs) · input: `Heddle.Models.Range` or `int`

A counted loop. The model is a `Heddle.Models.Range { Start, Last, Step }` — an immutable value type
([Heddle.Models](../src/Heddle/Models)); it iterates `i = Start` while
`i < Last`, incrementing by `Step` (1 via the two‑argument constructor). The loop index is exposed to the body as the
chained value (referenceable as `chained` in embedded C#, or output directly with `@out()`).

**Counted loops without C#.** `@for` also accepts a plain `int`, iterating `0…n−1`, so the
common cases need no embedded C# at all:

```heddle
@for(3){{<li>@out()</li>}}          @* renders <li>0</li><li>1</li><li>2</li> *@
@for(Count){{ ... }}                @* Count is an int member of the model *@
```

A negative or zero count renders empty (the loop condition is false immediately), matching a
data‑driven `Count = 0`.

**`range(start, last[, step])`.** The default [function registry](native-expressions.md)
includes `range`, which builds a `Heddle.Models.Range` — so `@for(range(...))` gives start/step
control with no new syntax:

```heddle
@for(range(2, 8, 3)){{[@out()]}}    @* [2][5] — start 2, step 3, last-exclusive *@
```

`step` must be positive: a zero or negative literal step is a compile error (**HED4001**), and a
non‑positive step known only at render throws. See [native expressions](native-expressions.md#range).

The C# tier still works for computed models:

```heddle
@using(){{System.Linq}}                 @* .Count()/.Skip()/.Take() need System.Linq *@
@for(@new Heddle.Models.Range(0, model.Articles.Count(), 3))
{{
  <div class="row">
    @list(@model.Articles.Skip(chained).Take(3)){{ @article_card() }}
  </div>
}}
```

(`chained` is the loop index; here it pages the articles three at a time.)

---

## Formatting

These all derive from `AbstractHtmlExtension` and are `[EncodeOutput]` — except `guid`, a plain
unencoded `AbstractExtension`. Their optional body is the **format string**.

> Format‑string and culture bodies are captured **verbatim** — leading/trailing spaces inside the
> `{{ … }}` are **not** trimmed and render literally (an empty‑but‑padded body can even throw, as with
> `@money`). Keep them tight: `{{MMMM d, yyyy}}`, `{{N0}}`, `{{en-US}}`.

### `date`
[DateExtension.cs](../src/Heddle/Extensions/DateExtension.cs) · input: `DateTime` · HTML‑encoded

Formats a `DateTime` using the body as a .NET date format string (default `"d"`), with
`CultureInfo.InvariantCulture`.

```heddle
@date(PublishedOn){{MMMM d, yyyy}}       @* e.g. June 28, 2026 *@
@date(PublishedOn)                       @* default short date *@
```

### `time`
[TimeExtension.cs](../src/Heddle/Extensions/TimeExtension.cs) · input: `DateTime` · HTML‑encoded

Like `date` but defaults to the `"t"` (short time) format.

```heddle
@time(PublishedOn){{HH:mm}}
```

### `int`
[IntegerExtension.cs](../src/Heddle/Extensions/IntegerExtension.cs) · input: `int` / `long` · HTML‑encoded

Formats an integer; the body is an optional numeric format string. On the typed tier the
parameter must be statically `int`/`long` — any other type is a compile‑time `HED0004`. Only on the
**dynamic tier** (a `dynamic`/`object` model) are other numeric types converted to `long` when
possible (invariant culture), with non‑convertible values rendering empty.

```heddle
@int(Year)                          @* 2026 *@
@int(@model.Comments.Count){{N0}}       @* 1,200 *@
```

### `money`
[MoneyExtension.cs](../src/Heddle/Extensions/MoneyExtension.cs) · input: `decimal` · HTML‑encoded

Formats a `decimal` as currency (`"c"`). The body is an optional **culture name**; with no
body the current culture is used. Cultures are cached.

```heddle
@money(@9.99m){{en-US}}      @* $9.99 — body is the culture name *@
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
[StringExtension.cs](../src/Heddle/Extensions/StringExtension.cs) · input: `string` (non‑string on the dynamic tier) · HTML‑encoded

Converts the model to a string. The optional body is a **default/fallback** rendered when the
model is `null`. On the typed tier the parameter must be a `string` — a statically non‑string
parameter is a compile‑time `HED0004`. Only on the **dynamic tier** (a `dynamic`/`object` model)
are non‑string values converted with `Convert.ChangeType` (invariant culture), falling back to
`ToString()`.

```heddle
@string(Title)
@string(Author.Name){{Anonymous}}      @* fallback when Author.Name is null *@
```

---

## Encoding contexts

`@string` (and the Html profile) encode a value for **element text** — the space between tags.
Other output positions need different escaping, and using element‑text encoding there is either
insufficient or actively wrong. These three extensions cover the remaining contexts.

All three are **value calls** like the formatters: the parameter is the value, and the optional
body is a **default** rendered (in the [caller's context](language-reference.md#stepping-back-the-parent-context))
when the value is `null`. They take a **string**, exactly like [`@string`](#string): on the typed
tier a statically non‑string parameter is a compile‑time type error (`HED0004`). Only on the
**dynamic tier** (a `dynamic`/`object` model) is a non‑string value accepted at run time and
stringified with `Convert.ToString(value, CultureInfo.InvariantCulture)` before escaping.

### `attr`
[AttrExtension.cs](../src/Heddle/Extensions/AttrExtension.cs) · input: `string` (non‑string on the dynamic tier) · encoding leaf

Escapes a value for an **HTML attribute**: `&`→`&amp;`, `<`→`&lt;`, `>`→`&gt;`, `"`→`&quot;`,
`'`→`&#39;`. It escapes the attribute‑significant characters including **both** quote styles, so the
result is safe in single‑ and double‑quoted attributes alike. Marked `[EncodeOutput]`, so under the
Html profile it is an encoding leaf — its output is never re‑encoded.

> The default HTML encoder already escapes `'`→`&#39;` (and the Latin‑1 160–255 range, which
> `@attr` leaves alone), so `@attr` is **not** a strict superset of it — it is the encoder tuned for
> the attribute context. If the value is `null` and you supply a default body, that body renders in
> the parent context and is **not** attribute‑escaped (it mirrors `@string`'s author‑controlled
> default) — route any untrusted value through `@attr` explicitly rather than relying on the body.

```heddle
<a title="@attr(Tooltip)">…</a>
<input value='@attr(UserName)'>
```

### `js`
[JsExtension.cs](../src/Heddle/Extensions/JsExtension.cs) · input: `string` (non‑string on the dynamic tier) · raw leaf

Escapes the **contents of a JavaScript string literal** — you write the surrounding quotes.
Escapes `\`→`\\`, `"`→`\"`, `'`→`\'`, `` ` ``→`` \` ``, U+000A→`\n`, U+000D→`\r`, U+2028→`\u2028`, U+2029→`\u2029`, `<`→`\u003C` (so a `</script>` can't close the block), `&`→`\u0026`, and every
other C0 control (U+0000–U+001F) as `\u00XX` (uppercase hex). `>` and `/` are intentionally left
alone. It is a **raw leaf**: like `@raw`, it is never HTML‑entity‑encoded under the Html profile
(entity encoding would corrupt a `<script>`/handler value).

```heddle
<script>const name = "@js(UserName)";</script>
```

### `url`
[UrlExtension.cs](../src/Heddle/Extensions/UrlExtension.cs) · input: `string` (non‑string on the dynamic tier) · encoding leaf

Percent‑encodes a single **URL component** — a query value or one path segment, *not* a whole URL —
with `Uri.EscapeDataString` (RFC 3986): the unreserved set `A‑Z a‑z 0‑9 - . _ ~` is kept, everything
else is UTF‑8 percent‑encoded. Marked `[EncodeOutput]` for uniformity (its output has no HTML‑special
characters by construction, so it is never re‑encoded).

```heddle
<a href="/search?q=@url(Query)">…</a>
```

### Choosing an encoder by context

The Html profile / `@string` covers the **element‑text context only**. Every other context needs the
matching extension below — the default HTML encoding is not sufficient (or is wrong) there.

| Output context | Encode with |
| --- | --- |
| Element text (between tags) | Html profile / `@string` |
| Attribute value | `@attr` |
| Inside a JS string literal | `@js` |
| URL component (query value / path segment) | `@url` |
| Trusted, pre‑sanitized markup | `@raw` |

Under an explicitly declared **Html profile** the compiler warns (**`HED2004`**) when a bare
`@(value)` sits in an attribute / `<script>` / URL position — a best‑effort, **single‑hop**,
model‑free heuristic over the literal text to the left of the block (sibling `@(…)` source spans are
skipped). It is a lint only: it never changes rendered bytes, never fires off the Html profile, and
its accepted limits are documented:

- **Single‑hop false negative** — a definition body's `@(X)` whose output lands in an attribute at
  a *call site* is never flagged (the lint sees only literals adjacent in the block's own body).
- **Fires inside an HTML comment** (`<!-- @(X) -->`) and after a stray literal `<` — the scan
  cannot distinguish a comment from an unclosed tag without an HTML model.
- **Over‑fires to the script context** on a `<script>`‑shaped literal inside an attribute value, a
  raw‑text element (`<textarea>`/`<title>`), or an HTML comment / CDATA section — a single
  unmatched `<script>` mention upstream routes subsequent element‑text outputs to `@js` until a
  `</script` literal appears.
- **Attribute‑name / tag‑name injection** (`<div @(Attrs)>`, `<@(Tag)>`) gets the generic in‑tag
  `@attr` suggestion — no shipped encoder is strictly correct for a name position, but the
  "inside a tag, not element text" signal is the point.
- **Mixed‑quote poisoning** (`<a title='it"s' href="/s?q=@(X)">`) under‑classifies a URL position
  to the generic attribute suggestion (never to a wrong, corrupting encoder).

Every flagged site is silenced per‑site by switching to the matching encoder (`@attr`/`@js`/`@url`)
— which also fixes the encoding — or to `@raw(...)` for a trusted value. No pragma or option knob.

---

## Output and context

### Empty / unnamed
[EmptyExtension.cs](../src/Heddle/Extensions/EmptyExtension.cs) · names: `""`, `raw`

The extension with the empty name backs the unnamed call form `@(...)`. If it has a body it
renders the body; otherwise it stringifies the current model (or empty when `null`). This is
the workhorse behind `@(Title)`, `@()`, etc.

Its output depends on the effective [output profile](#output-profiles):

- Under **`OutputProfile.Html`** (the default) a *bodiless* unnamed `@(value)` is HTML‑encoded
  by default (the compiler resolves it to `html` below). A *bodied* `@(value){{…}}` is unchanged
  — it is a raw rescoping container, so its literal body markup is never encoded; only value
  leaves inside it encode. Use [`raw`](#raw) to opt a trusted value out.
- Under **`OutputProfile.Text`** (the 1.x‑compatibility setting) the unnamed `@(...)` form is
  **not** HTML‑encoded — raw text/JSON/code output.

### `raw`
[EmptyExtension.cs](../src/Heddle/Extensions/EmptyExtension.cs) · name: `raw`

A second name on the empty extension: `@raw(value)` always emits the value verbatim, under
**both** profiles. Under `Html` it is the trusted‑value opt‑out (the analogue of Razor's
`Html.Raw`, Handlebars' `{{{ }}}`, Jinja's `|safe`); under `Text` it is a harmless alias.

```heddle
@raw(TrustedHtmlFragment)
```

> Never pass untrusted input through `@raw(...)` under `Html` — that reopens the XSS hole the
> profile closes.

### `html`
[EmptyHtmlExtension.cs](../src/Heddle/Extensions/EmptyHtmlExtension.cs) · name: `html` · HTML‑encoded

Same behavior as the empty extension, but always HTML‑encodes its output (`[EncodeOutput]`),
under both profiles. Use it when you want the "just stringify the value" behavior *with*
encoding, independent of the profile.

```heddle
@html(UserSuppliedText)
```

> Under `Html`, wrapping a value in `@html(...)` inside the unnamed sink (`@(html(X))`) encodes
> twice and raises the [`HED2003`](#html-encoding) warning — drop the `html()` or use `@raw`.

### `out`
[OutExtension.cs](../src/Heddle/Extensions/OutExtension.cs) · name: `out`

Emits the **chained** value — i.e. the data handed to the current definition/call. With no
body it renders the chained value directly; with a body it renders that body against the
chained data. This is how a definition surfaces the caller's inline content.

> A body counts only when it contains dynamic (`@`) content. A body of pure literal text has no
> effect on `@out()` — it is treated as absent, and `@out()` emits just the chained value — the
> same rule the value emitters `@()`/`@raw`/`@html` follow for their bodies (a static-only body is
> inert; it is **not** a null-default the way `@string`/`@attr` bodies are). Only a body with an
> `@`-construct is a real transform of the chained value, e.g. `@out(){{<@(this)>}}`.

```heddle
@%
  <article_card>
  {{ <article><h2>@(Title)</h2>@out()</article> }}    @* @out() drops in the caller's body *@
%@
```

`@out()` is the mechanism behind layouts: a `layout` definition wraps the page chrome around a
central `@out()`, and each page supplies the content. See
[Language Reference → composition](language-reference.md#inheritance-and-override-childbase).

**Two modes.** `@out` has a second, *slot* mode. When the enclosing definition declares a
[slot parameter](language-reference.md#parameterized-slots-out-type) (`out:: Type`), `@out(expr)`
renders the caller's body **once per execution** with `expr` (typed as the slot type) as its model
— the picker pattern, `@list(Options){{ @out(this) }}`. In slot mode every `@out` must pass a value
(a bare `@out()` is **HED5013**) and is bodiless (**HED5018**).

**`@out` with a value needs a slot.** Outside a slot‑declaring body, `@out` takes **no** argument —
`@out(X)` (formerly accepted and silently ignored) is now the compile error **HED5012**. The fix is
to drop the argument (`@out()`) or to declare `out:: Type` and mean it.

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

### `import` — removed
[ImportExtension.cs](../src/Heddle/Extensions/Archived/ImportExtension.cs) · name: `import`

`@import()` has been **removed**. The name is kept registered only as a tombstone so that any
call site fails with a single positioned [`HED4003`](language-reference.md#imports---) error
naming its replacements, instead of a generic "unknown extension" error. Use
[`@<<{{ path }}`](language-reference.md#imports---) to share definitions and layouts across
files, or [`@partial()`](#partial) to embed another template's rendered output inline.

### `profile`
[ProfileExtension.cs](../src/Heddle/Extensions/ProfileExtension.cs) · name: `profile`

Compile‑time directive that sets the effective [output profile](#output-profiles) for output
compiled **after** it in document order (bodies, partials, and imports created afterwards
inherit it). The value is read from the body and must be `text` or `html`
(case‑insensitive); any other value is the [`HED2001`](#html-encoding) error. Emits no output.

```heddle
@profile(){{ html }}     @* everything below now HTML‑encodes the unnamed @(...) form *@
```

Placing `@profile()` after some unnamed output has already been compiled still applies the
flip but raises the [`HED2002`](#html-encoding) warning — keep it at the top of the template.

---

## Embedding whole templates

### `partial`
[PartialExtension.cs](../src/Heddle/Extensions/PartialExtension.cs) · name: `partial`

Compiles a **separate** template by name (its body is the template name) and renders that
template's output inline at run time, passing the current model and chained data.

```heddle
@partial(){{ sidebar }}      @* compiles & renders the "sidebar" template by name *@
```

The body (`sidebar`) names a separate template file the engine compiles and renders inline,
passing the current model.

---

## Quick reference

| Name | Source | Input type | Encoded | Body means |
| --- | --- | --- | --- | --- |
| `if` | IfExtension | `bool`/any | – | content shown when true |
| `ifnot` | IfNotExtension | `bool`/`null` | – | content shown when false/null |
| `elif` / `elseif` | ElifExtension | `bool`/any | – | next branch of a set (else‑if) |
| `else` | ElseExtension | – | – | terminal branch of a set |
| `list` | ListExtension | `IEnumerable` | – | per‑element template |
| `for` | ForIndexExtension | `Heddle.Models.Range` / `int` | – | per‑iteration template |
| `date` | DateExtension | `DateTime` | ✔ | date format string (def. `d`) |
| `time` | TimeExtension | `DateTime` | ✔ | time format string (def. `t`) |
| `int` | IntegerExtension | `int`/`long` | ✔ | numeric format string |
| `money` | MoneyExtension | `decimal` | ✔ | culture name |
| `guid` | GuidExtension | `Guid` | – | GUID format specifier |
| `string` | StringExtension | `string`³ | ✔ | fallback when null |
| `attr` | AttrExtension | `string`³ | ✔ | fallback when null (HTML‑attribute escape) |
| `js` | JsExtension | `string`³ | –² | fallback when null (JS‑string escape) |
| `url` | UrlExtension | `string`³ | ✔ | fallback when null (URL‑component escape) |
| *(empty)* | EmptyExtension | any | profile¹ | optional body, else stringify model |
| `raw` | EmptyExtension | any | – | trusted‑value opt‑out (never encodes) |
| `html` | EmptyHtmlExtension | any | ✔ | optional body, else stringify model |
| `out` | OutExtension | chained | – | template over chained data |
| `swap` | SwapExtension | – | – | body with model/chained swapped |
| `param` | ParamExtension | any | – | pass model through chain |
| `model` | ModelExtension | – | – | model type name |
| `using` | UsingExtension | – | – | namespace to import |
| `profile` | ProfileExtension | – | – | output profile (`text`/`html`) |
| `partial` | PartialExtension | model | – | template name to render |

¹ The bodiless unnamed `@(...)` form encodes under `OutputProfile.Html` and is raw under
`OutputProfile.Text` — see [Output profiles](#output-profiles).

² `attr`/`url` are `[EncodeOutput]` **encoding leaves** — the Html profile never re‑encodes them.
`js` is a **raw leaf** (like `@raw`): it escapes for a JS string literal but is never HTML‑entity
encoded. See [Encoding contexts](#encoding-contexts).

³ Takes a `string`. On the **typed tier** a statically non‑string parameter is a compile‑time
`HED0004`; a non‑string value is accepted (and stringified) only on the **dynamic tier**
(`dynamic`/`object` model). See [Encoding contexts](#encoding-contexts).

---

## Output profiles

The **output profile** decides whether the bodiless unnamed `@(...)` form HTML‑encodes by
default. It is selected programmatically — there is no file‑extension inference:

- **`OutputProfile.Html`** (the default) — a bodiless `@(value)` HTML‑encodes by default
  (XSS‑safe output); `@raw(value)` opts a trusted value out; a bodied `@(value){{…}}` stays a
  raw rescoping container (only value leaves inside it encode).
- **`OutputProfile.Text`** (the 1.x‑compatibility setting) — `@(value)` emits raw text. This is
  the profile for text, JSON, and code generation and keeps 1.x templates byte‑identical.

Set it two ways (both feed the same effective profile; the directive wins for output after it):

- Host: [`TemplateOptions.OutputProfile`](csharp-api.md#templateoptions) (and the
  `TemplateResolver` default‑profile constructor).
- Template: the [`@profile()`](#profile) directive.

Encoding always happens at the **emitting leaf** and exactly once: a value flowing through a
container (`list`, `if`, `out`, …) or a nested producer (`@(upper(X))`) is encoded a single
time by the leaf that writes it. Wrapping a value in an already‑encoding extension under the
unnamed sink (`@(html(X))`) encodes twice and raises the `HED2003` warning.

> **Since 2.0:** the default profile is `Html` — a bodiless `@(value)` HTML‑encodes via
> `WebUtility.HtmlEncode`. `Text` was the 1.x default; hosts that need the 1.x behavior set
> `OutputProfile.Text` explicitly (or mark trusted spots with `@raw`).

## HTML encoding

Encoding is decided **per extension** plus the profile:

- Extensions marked **`[EncodeOutput]`** (and deriving from `AbstractHtmlExtension`)
  HTML‑encode their output with `WebUtility.HtmlEncode`. These are `date`, `time`, `int`,
  `money`, `string`, and `html` — under **both** profiles.
- The unnamed `@(...)` output encodes only under `OutputProfile.Html` (see
  [Output profiles](#output-profiles)); `@raw(...)` and non‑HTML extensions (`guid`, `out`,
  `list`, `if`, …) emit their text **as‑is** under both profiles.

Profile diagnostics:

- **`HED2001`** (error) — `@profile()` names a value other than `text`/`html` (or is empty).
- **`HED2002`** (warning) — `@profile()` appears after output has already been compiled in the
  same scope; the flip still applies, but earlier output kept the previous profile.
- **`HED2003`** (warning) — an `[EncodeOutput]` producer feeds the auto‑encoding unnamed sink
  under `Html` (`@(html(X))`), encoding twice. Remove the inner call or use `@raw(...)`.
- **`HED2004`** (warning) — a bare bodiless `@(value)` sits in an HTML **attribute value**, a
  **`<script>` block**, or a **URL component** under the Html profile without the matching context
  encoder; the message suggests `@attr`/`@js`/`@url` (or `@raw` for a trusted value). A heuristic,
  byte‑neutral lint — see [Encoding contexts](#choosing-an-encoder-by-context) for its documented
  limits. Never fires off the Html profile.

To emit user‑controlled text safely, run under `OutputProfile.Html` (or prefer `@string(...)`
/ `@html(...)`) rather than the bare `@(...)` form under `Text`.

### The encoder is pluggable

Which encoder runs at those encoding sites is set by
[`TemplateOptions.Encoder`](csharp-api.md#templateoptions), a
`System.Text.Encodings.Web.TextEncoder`:

- **`null` (the default)** selects the legacy built‑in path — the current
  `WebUtility.HtmlEncode` behavior, **byte‑identical to 2.0.0** (including its Latin‑1 160–255
  quirk). Leaving it unset changes nothing.
- **Any `TextEncoder`** customizes encoding; the recommended modern opt‑in is
  `HtmlEncoder.Create(UnicodeRanges.All)`, which uses the span/UTF‑8 paths.

The encoder applies to **encoding sites only** — the `Html`‑profile unnamed sink and
`[EncodeOutput]` extensions. It never touches `@raw`, raw blocks, `OutputProfile.Text` output, or
literal text. It participates in `TemplateOptions` `Equals`/`GetHashCode` **by reference**, so it is
part of the template‑cache identity: a different encoder *instance* is a different cache key.

This encoder — the Html profile — covers the **element‑text context only**. For attribute,
JavaScript, or URL output use [`@attr`/`@js`/`@url`](#encoding-contexts) instead.
