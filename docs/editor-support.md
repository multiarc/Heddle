# Editor support

Heddle ships a Language Server Protocol implementation and a VS Code extension that give `.heddle`
authors **typed** member completion, live diagnostics at template positions, hover types, semantic
coloring, and go‑to‑definition across imports. Because the compiler threads real type information
through every scope, completion is *correct*, not heuristic — and per‑use‑site type errors in
abstract definitions surface at the exact position that caused them.

Everything is a projection of the real engine pipeline: the server contains no compiler logic of its
own. The same [`Heddle.LanguageServices`](csharp-api.md) facade that powers the LSP is a public
package you can host yourself.

## Installing

### VS Code

Install the **Heddle Template Language** extension from its VSIX. CI builds a per‑target VSIX with
the language server bundled in, so grab it from the release/CI artifacts and install it with
*Extensions: Install from VSIX…* — no separate server install is needed. The extension keeps
working (TextMate coloring) even when the server is unavailable.

### The dotnet tool (any editor)

The server is a .NET tool, `heddle-lsp`. Install it version‑pinned:

```
dotnet tool install --global Heddle.LanguageServer --version <x.y.z>
```

or, recommended for teams, as a repo‑local tool so the pin lives in source control:

```
dotnet new tool-manifest
dotnet tool install Heddle.LanguageServer --version <x.y.z>
```

The tool targets .NET 10 and requires the .NET 10 runtime. Distribution is **explicit‑install only** —
the extension never downloads and executes the server from a feed at launch.

### Neovim (or any generic LSP client)

Point your client at the `heddle-lsp` executable over stdio. For Neovim `lspconfig`:

```lua
require('lspconfig.configs').heddle = {
  default_config = {
    cmd = { 'heddle-lsp' },
    filetypes = { 'heddle' },
    root_dir = require('lspconfig.util').root_pattern('.heddle-lsp.json', '.git'),
  },
}
require('lspconfig').heddle.setup {}
```

## Configuring

Two configuration channels. When a **`.heddle-lsp.json`** is present at the workspace root it
**replaces** client settings entirely — it does not merge field‑by‑field, so put every field you
need in the file (any field you omit falls back to the built‑in default, not to the client
setting). Because it is a single file, it is the editor‑agnostic carrier.

```json
{
  "assemblies": ["bin/Debug/net10.0/MyApp.dll"],
  "rootPath": "Views",
  "outputProfile": "html",
  "expressionMode": "native",
  "fileNamePostfix": ".heddle"
}
```

| Field | Meaning |
| --- | --- |
| `assemblies` | Model assemblies for typed completion/hover, **and** the input of the one‑shot export scan (see below). Relative to the workspace root. |
| `rootPath` | Template root for `@<<` import and `@partial` resolution (`TemplateOptions.RootPath`). |
| `outputProfile` | `text` or `html` — so diagnostics match your host's compile options. |
| `expressionMode` | `memberPathsOnly` / `native` / `fullCSharp`. |
| `fileNamePostfix` | Template file name postfix. |

The VS Code extension contributes mirror settings (`heddle.model.assemblies`,
`heddle.workspace.rootPath`, `heddle.compile.outputProfile`, `heddle.compile.expressionMode`,
`heddle.compile.fileNamePostfix`, plus `heddle.server.path` and `heddle.trace.server`) and forwards
them to the server.

**Types are stale until rebuild.** The editor loads your model assemblies as they are on disk;
rebuild your project to pick up type changes.

## Host registrations reach the editor via one scan

The server process never runs your host's startup code, so functions and extensions you register at
runtime are invisible to it *unless they are declared in the assembly*. At workspace load the editor
runs a **one‑shot scan** of the configured `assemblies`:

- Assembly‑level [`[ExportExtensions]`](custom-extensions.md#registering-your-extensions) — the
  exported extensions become offerable names and their calls stop drawing "unknown extension".
- Assembly‑level [`[ExportFunctions]`](custom-extensions.md#declaratively-exporting-functions) — the
  exported functions register into the workspace registry, so their calls resolve (no false
  "unknown function"), complete with real signatures, and participate in expression typing.

For runtime parity, call
[`FunctionRegistry.RegisterFrom(assembly)`](custom-extensions.md#declaratively-exporting-functions)
on the same assemblies in your host startup — the editor and the host then see one set.

**The scan is one‑shot per server process.** A new export, a changed extension body, or an
`assemblies` change after load requires a **server restart** (VS Code: *Heddle: Restart Language
Server*). Consider a dedicated export assembly so an extension/function export never pulls model
types into two load contexts.

**Delegate‑only registrations stay host‑only.** A purely runtime `Register(name, delegate)` closure
cannot be discovered by scanning metadata; its calls draw an editor‑only "unknown function" even
though your host resolves them. Export the function declaratively to share it.

## No‑server fallback

VS Code applies semantic tokens *on top of* the TextMate grammar, so when the server is down or not
installed the extension still colors `.heddle` files from the grammar — no configuration needed.

## Troubleshooting

| Symptom | Cause / fix |
| --- | --- |
| "language server not found" | Install the tool (`dotnet tool install --global Heddle.LanguageServer --version <x.y.z>`) or set `heddle.server.path`. |
| "the .NET 10 runtime was not found" | Install the .NET 10 runtime. |
| Types don't complete | No `assemblies` configured, or the project was not rebuilt. Check the paths in `.heddle-lsp.json`. |
| A definition/prop shows stale types | Rebuild — model types update on rebuild, not on source edit. |
| An extension or function is not offered | The export attribute is missing, the method is not an eligible public static, or the server needs a restart to rescan. Exported names are the **lowercase** method names; lookup is ordinal and case‑sensitive. |

See also: [syntax highlighting](syntax-highlighting.md) (grammar‑only setups),
[getting started](getting-started.md), and [the C# API](csharp-api.md) (hosting the facade
programmatically).
