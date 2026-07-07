# html-safe-output

**Shows:** the `OutputProfile.Html` auto-encoding sink, the `@raw` opt-out island, and encoder pluggability —
and doubles as the standing **2.0 migration rehearsal** (the default flips `Text` → `Html` in 2.0).
**Source of record:** [phase 2](../../docs/spec/phase-2-safe-output/README.md) (phase 9 D13 row 5).

## Run it

```bash
dotnet run --project samples/html-safe-output
```

One template carrying a hostile model string (`<script>alert("xss") & 'grüße' © 2026</script>`) is rendered
under both profile defaults, plus a third render through a custom encoder:

| Render | Profile | `@(Payload)` | `@raw(Payload)` |
| --- | --- | --- | --- |
| `profile-text.html` | `Text` (1.x default) | raw | raw |
| `profile-html.html` | `Html` (2.0 default) | HTML-encoded (`&lt;`, `&quot;`, `&#252;`, …) | raw (opt-out island) |
| `custom-encoder.html` | `Text` + `@tagged` | tagging stub (`[lt]`, `[quot]`, …) | — |

The `@tagged` encoder is a custom `[ExtensionName]` extension (`TaggingExtension.cs`) exported to the engine via
`[assembly: ExportExtensions]` — the 1.x seam for plugging a custom encoder. (A dedicated `TemplateOptions.Encoder`
is a 2.0-window feature; the extension mechanism is how you plug an encoder today.)

## Capture mode (what CI runs)

```bash
dotnet run --project samples/html-safe-output -c Release -- --capture out
bash samples/tools/compare-golden.sh samples/html-safe-output
```

## What the golden pins

All three rendered files. The `profile-text.html` / `profile-html.html` pair **is** the before/after of the 2.0
default flip — when the window opens, this sample's goldens are updated as a reviewed migration event.
