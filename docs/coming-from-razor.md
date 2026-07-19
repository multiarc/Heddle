# Coming from Razor

If you write ASP.NET Core Razor, most of Heddle will feel familiar — `@` starts a directive,
markup is just text — but a handful of core ideas are deliberately different. This page maps the
Razor habits that trip people up to their Heddle equivalents. For the full picture read the
[Language Reference](language-reference.md).

> Every Heddle snippet below is a complete template, verified to compile against **Heddle 2.0.0**
> under the default `ExpressionMode` — with one exception: the embedded‑C# line in
> [Where C# runs](#where-c-runs-three-tiers-not-everywhere) (`@(@model.Title.ToUpper())`) requires
> `ExpressionMode.FullCSharp`, as noted inline. Where a snippet reads model members it declares a
> `dynamic` model with `@model(){{dynamic}}`; the Razor snippets are illustrative and need not compile.

## Context is relative, not `Model.X`

```text
<h1>@Model.Title</h1>
@foreach (var a in Model.Articles)
{
    <h2>@a.Title</h2>
    <a href="/">@Model.Title</a>   @* still absolute: Model.Title *@
}
```

```heddle
@model(){{dynamic}}
<h1>@(Title)</h1>
@list(Articles){{
  <h2>@(Title)</h2>
  <a href="/">@(::Title)</a>
}}
```

In Razor every path is absolute — `Model.X` reads the page model from anywhere. In Heddle the
context is *relative* and narrows as you descend, so inside `@list(Articles)` a bare `@(Title)` is
the **article's** title, not the blog's. To reach back to the original model, prefix the path with
`::` — the [root reference](language-reference.md#root-reference-member) is the escape hatch.

## Layouts and sections → definitions + `@<<`

```text
@* _Layout.cshtml — the layout reaches into the page for its body *@
<html><body>
  <main>@RenderBody()</main>
  <footer>&copy; @Model.Year</footer>
</body></html>

@* Index.cshtml — the page declares what the layout will consume *@
@{ Layout = "_Layout"; }
<h1>Latest</h1>
```

```heddle
@* layout.heddle — a plain definition that knows nothing about any page *@
@%
  <layout>
  {{
    <html><body>
      <main>@out()</main>
      <footer>&copy; @(Year)</footer>
    </body></html>
  }}
%@
```

```heddle
@* home.heddle — pulls the layout in and calls it *@
@model(){{dynamic}}
@<<{{layout.heddle}}
@layout(){{ <h1>Latest</h1> }}
```

Razor's direction runs page → layout: the page sets `Layout` and the layout pulls the page body in
with `@RenderBody`. Heddle **flips** it — `layout` is an ordinary definition, and the page imports
it with `@<<` and *calls* it, dropping its own content in through `@out()`. Because the relationship
is symmetric, any template that exposes regions can serve as a base for another; see
[composition without coupling](language-reference.md#inheritance-and-override-childbase).

## `@:` is a raw line, not code-block text

```text
@{
    var name = "Ada";
    @: Hello, @name          @* @: escapes a line of literal text inside a code block *@
}
```

```heddle
@: Hello, @(Name) and @if() print verbatim - no directive runs.
```

In Razor `@:` marks a line of literal text *inside a `@{ … }` C# code block*. Heddle has no C# code
blocks, so `@:` means something else entirely: a [raw line](language-reference.md#raw-blocks----and-)
— everything to the end of the line is emitted verbatim, which is why `@(Name)` and `@if()` above are
printed exactly as written rather than evaluated.

## Where C# runs: three tiers, not everywhere

```text
@{ var featured = Model.Articles.Where(a => a.IsFeatured).ToList(); }
@foreach (var a in featured) { <li>@a.Title.ToUpper()</li> }
```

```heddle
@model(){{dynamic}}
<li>@(Title)</li>                    @* member path — every tier *@
<li>@(upper("draft"))</li>           @* registered function — Native tier and up *@
<li>@(@model.Title.ToUpper())</li>   @* embedded C# — FullCSharp only *@
```

Razor runs arbitrary C# anywhere on the page. Heddle gates it behind three `ExpressionMode` tiers —
`MemberPathsOnly`, the default sandbox-safe `Native` (operators, literals, and registered functions),
and `FullCSharp` (the inner-`@` Roslyn tier) — so an untrusted template can be compiled without ever
enabling C#. Only the last line above needs `FullCSharp`; see [Native Expressions](native-expressions.md).

## Tag helpers and view components → extensions

```text
<email-button to="@Model.Email">Contact</email-button>   @* a tag helper *@
@await Component.InvokeAsync("Cart")                      @* a view component *@
```

```heddle
@model(){{dynamic}}
@%
  <email_button>{{ <a href="mailto:@()">Contact</a> }}
%@
@email_button(Email)
```

Heddle has no tag helpers or view components. Reusable markup becomes a **definition**
(`<email_button>` above, invoked as `@email_button(Email)`), and a helper that needs real logic
becomes a **registered extension** — a C# class, the language's single extension point. See
[Writing Custom Extensions](custom-extensions.md).
