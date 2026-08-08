---
paths:
  - "**/*.cs"
---

# Coding style

Authority order: (1) surrounding code in the file, (2) [coding-standards.md](../../docs/spec/common/coding-standards.md), (3) `.editorconfig` (editor guidance only, not a gate).

- Block-scoped namespaces (`namespace X { … }`) — not file-scoped, even in new files.
- Allman braces predominantly — match the file. Braceless single-statement guards for early returns are idiomatic; multi-line/nested bodies always braced.
- `var` when the type is apparent; explicit types otherwise and for built-ins.
- Naming: `_camelCase` private instance fields; `PascalCase` private static readonly; `I`-prefixed interfaces; extension classes `<Name>Extension` with `[ExtensionName("name")]`.
- Existing quirks (space before parameter list, space after cast) are deliberate — do not "fix" them.
- Comments follow C1–C8: default to none; write one only when the code cannot carry the meaning; never cite specs, plans, findings, or reviews in code comments (C4). XML doc comments on public API; regression tests carry a doc comment explaining the pinned scenario.
- 4-space indent, UTF-8, final newline, ~120-column wrap.
- Never mass-reformat or run whole-file IDE cleanups; diffs contain only the lines the change needs. The established style is the maintainer's deliberate choice — consistency beats modernization.
- `src/Heddle.Language/generated/` is generated (ANTLR) — never hand-edited; grammar change = `.g4` edit → regen → commit both.
