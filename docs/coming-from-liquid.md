# Coming from Liquid

If you write Liquid — or **Jinja / Twig**, which share the same `{{ … }}` and `{% … %}` shapes —
Heddle looks close enough to be misleading. The delimiters that matter most mean different things
here. This page maps the habits that trip people up to their Heddle equivalents; for the full
picture read the [Language Reference](language-reference.md).

> Every Heddle snippet below is a complete template, verified to compile against **Heddle 2.0.0**
> under default options — **except the `@partial(){{ sidebar }}` snippet**, which additionally
> requires a configured `RootPath`/`FileNamePostfix` and an existing `sidebar` template on disk at
> compile time (see [that section](#include--render---vs-partial)).
> Where a snippet reads model members it declares a `dynamic` model with `@model(){{dynamic}}`;
> the Liquid snippets are illustrative and need not compile.

## `{{ }}` is a body, not interpolation

```text
<h1>{{ title }}</h1>       {# Liquid / Jinja: interpolates the value #}
```

```heddle
@model(){{dynamic}}
<h1>{{ Title }}</h1>       @* WRONG: {{ }} is a subtemplate body — this prints the braces *@
```

```heddle
@model(){{dynamic}}
<h1>@(Title)</h1>          @* RIGHT: @(...) emits a value *@
```

This is the number-one misread. In Liquid and Jinja `{{ x }}` interpolates a value; in Heddle
`{{ … }}` delimits a **subtemplate body** — the block a call or definition renders — so a bare
`{{ Title }}` in text emits the literal braces, not the value. To output a value, use the
`@(…)` [output block](language-reference.md#output-blocks).

## Filters → chains, composed right-to-left

```text
{{ title | emphasize }}   {# left-to-right: take title, then emphasize it #}
```

```heddle
@model(){{dynamic}}
@%
  <emphasize>{{ <em>@(Title)</em> }}
%@
@out():emphasize()          @* right-to-left: emphasize runs first; @out emits its result *@
```

A Liquid filter pipeline reads **left-to-right** — the value first, each filter after it. A Heddle
[chain](language-reference.md#chaining-with-) composes **right-to-left**: the rightmost call renders
first, and each `:` hands its output to the call on its *left* as that call's
[chained value](language-reference.md#the-second-channel-chained-data), which the built-in
[`@out()`](built-in-extensions.md#out) emits. So the transform sits on the right and the output step
(`@out()`) on the left — the same pipeline, read in reverse. The example renders `<em>Hello</em>`.
Note the shape difference: a Heddle transform reads the model directly (here `emphasize` reads
`Title`), and the built-in `@out()` is the consumer of the chained value — so to apply several
transforms you compose definitions rather than piping one scalar through a long chain.

## `include` / `render` → `@<<` vs `@partial()`

```text
{% include 'sidebar' %}     {# or {% render 'sidebar' %} #}
```

```heddle
@model(){{dynamic}}
@partial(){{ sidebar }}     @* compiled when the enclosing template compiles; rendered inline at run time *@
```

Liquid's `include` and `render` both splice in another template's output. Heddle splits the job:
[`@partial()`](built-in-extensions.md#partial) resolves and compiles its target *when the enclosing
template compiles*, then renders that template's output inline at run time (the closest match), while
[`@<<{{ file }}`](language-reference.md#imports---) is a *compile-time definition import* for sharing
layouts and reusable blocks. Reach for `@partial()` to embed rendered output, `@<<` to share
definitions. Because the target is read at compile time, this snippet needs more than the defaults to
compile: set `TemplateOptions.RootPath` and `FileNamePostfix`, and have a `sidebar` template on disk —
a missing partial is a *compile* error here, not a render-time one (unlike Liquid's `include`).

## `for` / `forloop.index` → `@list` + `@out()`

```text
{% for a in articles %}
  {{ forloop.index }}. {{ a.title }}
{% endfor %}
```

```heddle
@model(){{dynamic}}
@list(Articles){{ @out(). @(Title) }}
```

In Liquid the loop variable and `forloop.index` are separate names. Heddle's
[`@list`](built-in-extensions.md#list) makes each element the current model — so `@(Title)` is the
element — and exposes the index on the **chained** channel, which `@out()` emits. Note the index is
zero-based, where Liquid's `forloop.index` starts at 1.

## Tags → extensions

```text
{% if featured %}<b>Featured</b>{% endif %}
{% assign x = 1 %}
```

```heddle
@model(){{dynamic}}
@if(IsFeatured){{ <b>Featured</b> }}
```

Liquid's `{% … %}` tags — `if`, `for`, `assign`, and any custom tag — are a separate syntax. Heddle
has no tag syntax at all: every construct is an extension call, whether a built-in like `@if` /
`@list` or your own `@name()` C# class, so control flow and custom logic share one form. See
[Built-in Extensions](built-in-extensions.md) and [Writing Custom Extensions](custom-extensions.md).

## What to unlearn: whitespace-control dashes

```text
{%- assign x = 1 -%}
{{- title -}}          {# the dashes strip surrounding whitespace #}
```

```heddle
@model(){{dynamic}}
@using(){{System.Linq}}@\
<h1>@(Title)</h1>
```

Liquid trims whitespace with in-delimiter dashes (`{%- -%}`, `{{- -}}`). Heddle has no in-delimiter
form: use [`@\`](language-reference.md#whitespace-trimming-) to eat the following run of whitespace
(here suppressing the declaration line's trailing newline), or set
`TemplateOptions.TrimDirectiveLines` — on by default since 2.0 — so a whole-line directive swallows
its own line without any marker.
