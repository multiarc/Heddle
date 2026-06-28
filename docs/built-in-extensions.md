# Built‑in Extensions

Extensions are the verbs of TTL: every `@name(...)` directive invokes an extension. This page
documents the extensions bundled with the `Templates` assembly. Each entry lists the name
(`[ExtensionName]`), the expected input type (`[DataType]`, when declared), whether the output
is HTML‑encoded, and what the optional parameter / subtemplate mean.

All names and attributes below were read directly from
[src/Templates/Extensions](../src/Templates/Extensions). To write your own, see
[Writing Custom Extensions](custom-extensions.md).

## How to read these entries

- **Input** — the model value the extension expects (its `[DataType]`). If the actual model
  doesn't match, most extensions render empty rather than throw.
- **Body / parameter** — many extensions interpret their `{{ … }}` subtemplate as a *format
  string* or *item template* rather than literal output (e.g. `@date(d){{ yyyy-MM-dd }}`).
- **HTML‑encoded** — extensions marked `[EncodeOutput]` (and deriving from
  `AbstractHtmlExtension`) HTML‑encode their result by default. See
  [HTML encoding](#html-encoding) at the bottom.

---

## Conditionals

### `if`
[IfExtension.cs](../src/Templates/Extensions/IfExtension.cs) · input: `bool` (or any object)

Renders its subtemplate when the condition is truthy. A `null` model renders nothing; a
`bool` model renders the body only when `true`; a non‑bool, non‑null model is treated as
present (renders the body).

```ttl
@if(IsShow){{ <span style="color: aqua">Shown</span> }}
@if(@model.SubCategories.Count>0){{ <ul> … </ul> }}
```

### `ifnot`
[IfNotExtension.cs](../src/Templates/Extensions/IfNotExtension.cs) · input: `bool` (or `null`)

The inverse of `if`. Renders its body when the model is `null` or `false`. A non‑bool,
non‑null model renders nothing.

```ttl
@ifnot(IsShow){{ <span style="color: red">Hidden</span> }}
```

---

## Iteration

### `list`
[ListExtension.cs](../src/Templates/Extensions/ListExtension.cs) · input: `IEnumerable`

Renders its subtemplate once per element; inside the body the current model is the element,
and an `int` index is available as the chained value. Works with both `dynamic` sequences and
strongly‑typed `IEnumerable<T>` (the element type is inferred for typed member access). When
the source implements `ICollection<T>`, the count is used to pre‑size the output buffer.

```ttl
@list(@model.Products.Where(p => p.Quantity < 95))
{{
  @partial(){{partial}}
}}

@list(SubCategories){{ <li>@(Name)</li> }}
```

### `for`
[ForIndexExtension.cs](../src/Templates/Extensions/ForIndexExtension.cs) · input: `ForModel`

A counted loop. The model is a `ForModel { Start?, Last, Step? }`
([Templates.Models](../src/Templates/Models)); it iterates `i = Start (default 0)` while
`i < Last`, incrementing by `Step` (default 1). The loop index is exposed to the body as the
chained value (referenceable as `chained` in embedded C#).

```ttl
@for(@new ForModel() { Last = model.Products.Count(), Step = 3 })
{{
  <div>
    @list(@model.Products.Skip(chained).Take(3)){{ … }}
  </div>
}}
```

---

## Formatting

These all derive from `AbstractHtmlExtension` and are `[EncodeOutput]` (except `guid`). Their
optional body is the **format string**.

### `date`
[DateExtension.cs](../src/Templates/Extensions/DateExtension.cs) · input: `DateTime` · HTML‑encoded

Formats a `DateTime` using the body as a .NET date format string (default `"d"`), with
`CultureInfo.InvariantCulture`.

```ttl
@date(BirthDate){{ yyyy-MM-dd }}
@date(Date)
```

### `time`
[TimeExtension.cs](../src/Templates/Extensions/TimeExtension.cs) · input: `DateTime` · HTML‑encoded

Like `date` but defaults to the `"t"` (short time) format.

```ttl
@time(Date){{ HH:mm }}
```

### `int`
[IntegerExtension.cs](../src/Templates/Extensions/IntegerExtension.cs) · input: `int` / `long` · HTML‑encoded

Formats an integer; the body is an optional numeric format string. Other numeric types are
converted to `long` when possible (invariant culture); non‑convertible values render empty.

```ttl
@int(Count)
@int(Count){{ 0:N0 }}
```

### `money`
[MoneyExtension.cs](../src/Templates/Extensions/MoneyExtension.cs) · input: `decimal` · HTML‑encoded

Formats a `decimal` as currency (`"c"`). The body is an optional **culture name**; with no
body the current culture is used. Cultures are cached.

```ttl
@money(Cost){{@(Locale)}}    @* e.g. body resolves to "en-US" *@
@money(Price)
```

### `guid`
[GuidExtension.cs](../src/Templates/Extensions/GuidExtension.cs) · input: `Guid` · *not* encoded

Formats a `Guid`; the body is an optional .NET GUID format specifier (`N`, `D`, `B`, `P`,
`X`).

```ttl
@guid(Id){{X}}
@guid(Id)
```

### `string`
[StringExtension.cs](../src/Templates/Extensions/StringExtension.cs) · input: `string` (or any) · HTML‑encoded

Converts the model to a string. The optional body is a **default/fallback** rendered when the
model is `null`. Non‑string models are converted with `Convert.ChangeType` (invariant
culture), falling back to `ToString()`.

```ttl
@string(Text)
@string(MiddleName){{ (none) }}
```

---

## Output and context

### Empty / unnamed
[EmptyExtension.cs](../src/Templates/Extensions/EmptyExtension.cs) · name: `""`

The extension with the empty name backs the unnamed call form `@(...)`. If it has a body it
renders the body; otherwise it stringifies the current model (or empty when `null`). This is
the workhorse behind `@(Name)`, `@(@5)`, etc.

> The unnamed `@(...)` form is **not** HTML‑encoded. For encoded text output use
> [`string`](#string) (or `html` below).

### `html`
[EmptyHtmlExtension.cs](../src/Templates/Extensions/EmptyHtmlExtension.cs) · name: `html` · HTML‑encoded

Same behavior as the empty extension, but HTML‑encodes its output (`[EncodeOutput]`). Use it
when you want the "just stringify the value" behavior *with* encoding.

```ttl
@html(UserSuppliedText)
```

### `out`
[OutExtension.cs](../src/Templates/Extensions/OutExtension.cs) · name: `out`

Emits the **chained** value — i.e. the data handed to the current definition/call. With no
body it renders the chained value directly; with a body it renders that body against the
chained data. This is how a definition surfaces the caller's inline content.

```ttl
<text>
{{ <input name="@(Name)" value="@(Value)" @out() /> }}    @* @out() drops in the caller's body *@
```

In [vc-test.thtml](../src/Templates.Tests/TestTemplate/vc-test.thtml), `@out()` threads the
caller's content through nested layout definitions (`left`, `center`, `right`, `layout`).

### `swap`
[SwapExtension.cs](../src/Templates/Extensions/SwapExtension.cs) · name: `swap`

Swaps the model and chained values for the duration of its body — useful when an extension
chain produced a value you now want to treat as the model.

### `param`
[ParamExtension.cs](../src/Templates/Extensions/ParamExtension.cs) · name: `param`

Passes the model through unchanged as the chain value (a no‑op renderer that yields the
current model to the next call in a chain).

---

## Declarations (no output)

These configure compilation; they emit nothing.

### `model`
[ModelExtension.cs](../src/Templates/Extensions/ModelExtension.cs) · name: `model`

Declares the document's model type from *within the template* (instead of in C# code). The
body is a type name resolved against the imported namespaces; `dynamic` is allowed.

```ttl
@model(){{TestDataStructure}}
@model(){{dynamic}}
```

### `using`
[UsingExtension.cs](../src/Templates/Extensions/UsingExtension.cs) · name: `using`

Imports a C# namespace so that type names (in `@model()`, `:: Type`) and embedded C# can
resolve unqualified identifiers.

```ttl
@using(){{System.Linq}}
@using(){{Templates.Models}}
```

### `import`
[ImportExtension.cs](../src/Templates/Extensions/ImportExtension.cs) · name: `import`

Compile‑time include: reads another template file (relative to `RootPath`) and parses its
**definitions** into the current parse context. This is the extension behind the
[`@<<{{ path }}`](language-reference.md#imports--) sugar. Emits no output of its own.

---

## Embedding whole templates

### `partial`
[PartialExtension.cs](../src/Templates/Extensions/PartialExtension.cs) · name: `partial`

Compiles a **separate** template by name (its body is the template name) and renders that
template's output inline at run time, passing the current model and chained data. Unlike
`import`, a partial produces output.

```ttl
@partial(){{partial}}        @* compiles & renders the "partial" template *@
```

The fixture [partial.thtml](../src/Templates.Tests/TestTemplate/partial.thtml) is the body
used by `@partial(){{partial}}` calls in
[template.thtml](../src/Templates.Tests/TestTemplate/template.thtml). In ASP.NET Core MVC,
partial resolution is specialized by
[`PartialMvcExtension`](../src/Templates.Mvc/Extensions/PartialMvcExtension.cs) — see
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
> [`ImportMvcExtension`](../src/Templates.Mvc/Extensions/UseMvcExtension.cs) and adds an MVC
> partial. Whichever assembly you pass to `TtlTemplate.Configure` determines which set is
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
  [src/Templates/Attributes/NotEncodeAttribute.cs](../src/Templates/Attributes/NotEncodeAttribute.cs).

To emit user‑controlled text safely, prefer `@string(...)` or `@html(...)` over the bare
`@(...)` form.
