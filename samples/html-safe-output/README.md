# html-safe-output

**Shows:** the `OutputProfile.Html` auto-encoding sink, the `@raw` opt-out island, and the two ways to plug a
custom output encoder — the per-extension `@tagged` seam (1.x) and `TemplateOptions.Encoder` (the modern seam).
**Source of record:** [HTML encoding](../../docs/built-in-extensions.md#html-encoding).

## Run it

```bash
dotnet run --project samples/html-safe-output
```

One template carrying a hostile model string (`<script>alert("xss") & 'grüße' © 2026</script>`) is rendered four ways:

| Render | Profile / seam | `@(Payload)` | `@raw(Payload)` |
| --- | --- | --- | --- |
| `profile-text.html` | `Text` (1.x compatibility) | raw | raw |
| `profile-html.html` | `Html` (default) — built-in `WebUtility` path | HTML-encoded (`&lt;`, `&quot;`, `&#252;`, …) | raw (opt-out island) |
| `custom-encoder.html` | `Text` + `@tagged` | tagging stub (`[lt]`, `[quot]`, …) | — |
| `options-encoder.html` | `Html` + `TemplateOptions.Encoder` | `HtmlEncoder.Create(UnicodeRanges.All)` (`&lt;`, `&#x27;`, `ü`/`©` kept literal) | raw (opt-out island) |
| `context-lint.html` | `Html` — `<a title="@(Payload)">` | HTML-encoded, **plus one `HED2004` warning** (bare `@(…)` in an attribute position; suggests `@attr`) | — |

Two encoder seams, contrasted:

- **`TemplateOptions.Encoder`** (`options-encoder.html`) is a `System.Text.Encodings.Web.TextEncoder` applied at
  *every* HTML-encode site — the `Html`-profile unnamed sink and every `[EncodeOutput]` extension — with no
  per-template wiring. `null` (the default) keeps the built-in `WebUtility.HtmlEncode` behavior; supply an encoder
  (e.g. `HtmlEncoder.Create(UnicodeRanges.All)`) to opt into the modern contract. `@raw` still opts out, and a
  different encoder instance is part of the template cache key.
- **`@tagged`** (`custom-encoder.html`) is the 1.x seam: a custom `[ExtensionName]` extension
  (`TaggingExtension.cs`) exported via `[assembly: ExportExtensions]`. It transforms one output at its call site
  rather than the whole render — useful when only a specific region needs a bespoke transform.

## Capture mode (what CI runs)

```bash
dotnet run --project samples/html-safe-output -c Release -- --capture out
bash samples/tools/compare-golden.sh samples/html-safe-output
```

## What the golden pins

All five rendered files. `profile-text.html` / `profile-html.html` pin the `Text` vs `Html` profile contrast;
`custom-encoder.html` / `options-encoder.html` pin the two custom-encoder seams against the same hostile input.
`context-lint.html` pins the [HED2004 HTML-context lint](../../docs/built-in-extensions.md#choosing-an-encoder-by-context):
the program asserts the compile surfaces exactly **one** `HED2004` (`MissingContextEncoder`) warning for the bare
`@(Payload)` in an attribute value, and the golden proves the warning changes **zero** rendered bytes — switch the
site to `@attr(Payload)` (or `@raw` for a trusted value) to silence it and fix the encoding.
