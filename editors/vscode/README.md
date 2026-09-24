# Heddle Template Language

Language support for [Heddle](https://multiarc.github.io/Heddle/) `.heddle` templates: typed member
completion, live diagnostics at template positions, hover types, semantic coloring, and
go-to-definition across imports.

Everything is a projection of the real Heddle compiler pipeline, so completion and diagnostics are
correct rather than heuristic. The extension bundles the `heddle-lsp` language server for its
platform; no separate server install is needed. TextMate coloring keeps working even when the
server is unavailable.

## Requirements

- The bundled server runs on the .NET 10 runtime. Install it from
  [dot.net](https://dotnet.microsoft.com/download/dotnet/10.0) if it is not already present.

## Configuration

A `.heddle-lsp.json` at the workspace root replaces the settings below entirely, so it is the
editor-agnostic carrier for teams. Without it, these settings apply:

| Setting | Purpose |
|---|---|
| `heddle.model.assemblies` | Model, extension and function assembly paths for typed completion. |
| `heddle.workspace.rootPath` | Template root for `@<<` import and `@partial` resolution. |
| `heddle.compile.outputProfile` | `html` (default) or `text`. |
| `heddle.compile.expressionMode` | `native` (default), `memberPathsOnly` or `fullCSharp`. |
| `heddle.compile.fileNamePostfix` | Template file name postfix. |
| `heddle.compile.trimDirectiveLines` | Whether whole-line directives swallow their line. |
| `heddle.compile.maxRecursionCount` | Compile-time recursion bound. |
| `heddle.server.path` | Explicit path to a `heddle-lsp` server, overriding the bundled one. |
| `heddle.trace.server` | LSP trace level: `off`, `messages` or `verbose`. |

Command: **Heddle: Restart Language Server**.

## Documentation

- [Editor support](https://multiarc.github.io/Heddle/editor-support) — installing, configuring, and the
  configuration keys the language server accepts.
- [Language reference](https://multiarc.github.io/Heddle/language-reference)
- [Source and issues](https://github.com/multiarc/Heddle)
