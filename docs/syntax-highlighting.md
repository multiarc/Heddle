# Syntax Highlighting

Heddle ships a portable **TextMate grammar** so `.heddle` files highlight consistently across
editors and documentation tools. The grammar and its scope choices are derived from the
canonical Ace highlighter
([heddle_highlight_rules.js](../src/Heddle.Language/js/src/mode/heddle_highlight_rules.js)),
which is the reference implementation of how Heddle tokens are classified.

## What lives where

| Asset | Path | Used by |
| --- | --- | --- |
| TextMate grammar | [coloring-scheme/heddle.tmLanguage.json](https://github.com/multiarc/Heddle/blob/main/docs/coloring-scheme/heddle.tmLanguage.json) | Any TextMate/Shiki/Monaco/VS Code consumer |
| Ace highlighter (reference) | [src/Heddle.Language/js/src/mode/heddle_highlight_rules.js](../src/Heddle.Language/js/src/mode/heddle_highlight_rules.js) | The Ace‑based web editor |

The grammar is **HTML‑hosted**: it embeds `text.html.basic` for markup and `source.cs` for
inline C# (`@( @ … )`), then overlays the Heddle directives on top. Because it uses **standard
TextMate scope names**, any color theme renders it without per‑theme configuration.

## Token → scope mapping

The grammar mirrors the **v1 directive** token families the Ace highlighter assigns (the segment
after the `heddle-*.` state prefix in the reference). Standard TextMate scopes are chosen so themes
color each family sensibly. The v2 expression‑level tokens — numeric/string/language literals inside
calls, `variable.parameter` for props and named arguments, prop‑default constants, and
native‑expression operators — are classified only by the Ace mode; the shipped TextMate grammar does
not yet cover them, so they render unclassified under it.

| Construct | Example | Ace family | TextMate scope |
| --- | --- | --- | --- |
| Output / extension call | `@`, `@list`, `@if` | `support.function` / `identifier` | `support.function.directive.heddle`, `entity.name.function.extension.heddle` |
| Parameter parens | `(` `)` | `keyword.paren` | `keyword.operator.paren.*.heddle` |
| Member access | `.` | `punctuation.operator` | `punctuation.accessor.heddle` |
| Member name | `Title` in `@(Title)` | `variable.language` | `variable.language.member.heddle` |
| Root reference | `::` in `@(::User)` | `punctuation.operator` | `keyword.operator.root.heddle` |
| Chain delimiter | `:` between calls | `punctuation.operator` | `punctuation.separator.chain.heddle` |
| Inline C# | `@( @model.X )` | `cs-` (CSharp rules) | `keyword.control.csharp.heddle` + `source.cs` |
| Body block | `{{` `}}` | `keyword.operator.paren` | `keyword.operator.paren.*.heddle` |
| Definition block | `@%` `%@` | `keyword.operator.paren` | `keyword.operator.paren.*.heddle` |
| Definition name | `<card:base>` | `identifier` | `entity.name.type.definition.heddle` |
| Inheritance separator | `:` in `<card:base>` | `keyword.operator` | `keyword.operator.inheritance.heddle` |
| Inherited base name | `base` in `<card:base>` | `identifier` | `entity.name.type.definition.heddle` |
| Default‑out | `->` | `keyword.operator` | `keyword.operator.default-out.heddle` |
| Accept type | `:: Article` | `keyword.operator` + `storage.type` | `keyword.operator.type.heddle`, `storage.type.heddle` |
| Import | `@<<{{ … }}` | `keyword.operator` + `constant.other` | `keyword.operator.import.heddle`, `constant.other.import-path.heddle` |
| Raw block | `@{` `}@` | `keyword.paren` | `keyword.operator.paren.*.heddle` |
| Line directive | `@:` | `keyword` | `keyword.control.line.heddle` |
| Comment | `@* … *@` | `comment.block` | `comment.block.heddle` |
| Whitespace trim | `@\` | `comment.block` | `comment.block.whitespace-trim.heddle` |

> **Scope of a TextMate grammar.** TextMate grammars are regex/stack based and cannot fully
> reproduce the Ace highlighter's mode stack (the 13‑mode lexer that disambiguates, e.g.,
> `:` as chain delimiter vs. inheritance separator in every position). The grammar covers the
> distinctive constructs faithfully for reading; the Ace mode remains the reference for
> interactive editing.

A few known drifts between the two assets:

- **`@\` whitespace‑trim.** The TextMate grammar matches the real single‑backslash `@\` token, but
  the Ace reference's whitespace‑trim rules currently require *two* backslashes (`@\\`) and so never
  match the actual token — the table row above describes TextMate behavior, not the Ace mode.
- **`@{ … }@` raw block.** By the language it is emitted **verbatim** (not parsed). The Ace mode
  honors this (plain HTML embed, no Heddle rules), but the TextMate grammar still parses Heddle
  directives inside it, so an `@(Title)` written in a raw block is mis‑colored as a live directive.
- **`@@` is the literal‑`@` escape.** The TextMate grammar's rule that colors `@@` as a literal‑`@`
  escape is **correct**: `@@` in text (and in subtemplate bodies) emits a single literal `@`. The
  one exception is `@@` immediately followed by `*`, which is directive‑`@` + comment‑start
  (`@*…*@`), not an escape — see the [language reference](language-reference.md#text-and-the--escape).

## Using the grammar

- **VS Code / Monaco / Shiki:** register `coloring-scheme/heddle.tmLanguage.json` with `scopeName`
  `text.html.heddle` and associate the `.heddle` file extension.
- **Documentation sites:** Shiki‑based tools (e.g. VitePress, Docusaurus) can load the grammar
  as a custom language with `html` and `c-sharp` as embedded languages, so ` ```heddle ` fenced
  blocks highlight accurately.

GitHub does not know the `heddle` language, so ` ```heddle ` blocks render as neutral text
there and highlight correctly anywhere the grammar above is loaded.
