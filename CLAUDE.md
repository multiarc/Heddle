# Heddle

.NET templating engine: runtime compiler + render engine (`src/Heddle`), ANTLR grammar + generated parser (`src/Heddle.Language`), build-time source generator (`src/Heddle.Generator`), LSP (`src/Heddle.LanguageServer`), CLI (`src/Heddle.Tool`).

Normative sources (read before non-trivial work): [docs/spec/README.md](docs/spec/README.md) → `docs/spec/common/` (coding-standards, testing-standards, cross-cutting-decisions D1–D9 + diagnostic registry, breaking-windows, spec-conventions). Rules below are the operational summary; the specs win on conflict.

## Commands

- Build: `dotnet build -c Release` (whole solution, all TFMs)
- Test: `dotnet test src/Heddle.Tests` (all TFMs, zero failures)
- Benchmarks: BenchmarkDotNet in `benchmarks/dotnet` (not in the solution; gate first: `dotnet run -c Release --project benchmarks/dotnet -- gate`)
- Docs site: `cd docs && npm run docs:build`
- Merge gate (one combined run): build → test → no diff in `src/Heddle.Language/generated/` → benchmarks if a hot path was touched → docs build if docs changed
- Grammar: edit `.g4` → `src/Heddle.Language/generate_cs.cmd` (ANTLR 4.13.1) → commit both. Never hand-edit `src/Heddle.Language/generated/`.

## Hard rules

- Libraries target `netstandard2.0;net6.0;net8.0;net10.0` — new engine code must work on `netstandard2.0`. C# 14 is fine on all TFMs; no `#if NET10_0_OR_GREATER` by default (D7).
- Template mistakes become collected, positioned `HED####` diagnostics — never thrown exceptions. Host-programming errors throw. Sandbox violations are compile errors, never execution.
- The render path is hot: no new per-render allocations. Perf claims are proven by benchmarks, not asserted.
- Additive by default; breaking changes only inside a ratified breaking window (D2).
- Never mass-reformat, never `dotnet format`; match the file you are in; keep diffs minimal.
- Goldens, perf baselines, and spec tables never change to silence a red test — fix forward.

Detailed rules live in `.claude/rules/` — auto-loaded, most path-scoped to the files they govern. Don't restate them here.

<!-- DRAFT: on ratification rename to CLAUDE.md; rules.draft/ → .claude/rules/ (rule-file links to docs/spec then need one more ../). -->
