# Testing standards (shared across all specs)

Normative for the *Testing plan* section of every spec (structure defined in
[spec-conventions.md](spec-conventions.md#required-structure-of-a-spec)). This
instantiates the canonical implementation–verification loop ratified for all spec work
and pins the shared mechanics: suite homes, fixture/golden conventions, and regression
gates.

## The canonical loop

Every work item in every spec runs the same loop:

1. **Spec first** — the executable spec (fixtures, tables, negative/security tests, per
   the spec's TDD verdict) is written **failing** before implementation.
2. **Implement** until the new spec is green.
3. **Gate** — the spec's regression requirements run: the existing suite plus goldens
   byte-identical, benchmarks wherever a hot path is touched.
4. **Fix forward** — a red gate means fixing code. Goldens, perf baselines, and spec
   tables change only through maintainer review/re-ratification — never to silence a
   failing test.
5. **Repeat** per work item; spec exit = all success criteria green in **one combined
   run**, plus a [samples-gallery](../../../samples/README.md) item when the change is
   user-visible.

Each spec carries its **TDD verdict** (which parts are test-first versus test-with) and
must not weaken it.

## Suite homes

| Home | Framework | Role |
| --- | --- | --- |
| [src/Heddle.Tests](../../../src/Heddle.Tests) | xUnit, `dotnet test` | Unit, integration, golden-file, negative/security, and concurrency tests. Multi-targets `net6.0;net8.0;net10.0` (+ `net48` on Windows) — new tests must pass on **all** TFMs. |
| [src/Heddle.Performance](../../../src/Heddle.Performance) | BenchmarkDotNet (`[MemoryDiagnoser]`) | Render/compile benchmarks incl. the Razor head-to-head. Hot-path changes add or extend benchmarks here. |
| [`samples/` gallery](../../../samples/README.md) | Per-sample CI jobs with golden assertions | The demo/integration item of each user-visible change. The harness (comparer, workflow, conventions) exists; each spec owns its sample per the gallery conventions. |

## Fixtures and goldens

- Template fixtures are `.heddle` files under
  [src/Heddle.Tests/TestTemplate](../../../src/Heddle.Tests/TestTemplate); expected output
  goldens are sibling `.html`/text files (existing naming: `<name>.heddle`,
  `generated-<name>.html` / `test-<name>.html`). New work follows the sibling-pair
  pattern and prefixes fixture names with an initiative-recognizable stem
  (e.g. `expr-precedence.heddle`, `branching-interleaved.heddle`).
- Tests normalize line endings when reading fixtures (`.Replace("\r\n", "\n")` — the
  established pattern) so goldens are byte-identical across OSes.
- **Line-ending enforcement is git-level, not just test-level (cross-OS).** CI runs on both
  `ubuntu-latest` and `windows-latest`, and developer machines commonly set
  `core.autocrlf=true`. Every checked-in golden / fixture / snapshot file **MUST** be pinned to
  LF via [`.gitattributes`](../../../.gitattributes) (`text eol=lf`) so a Windows checkout cannot
  smudge it to CRLF and diverge from the LF-normalized engine output (rendered output is LF
  because `.heddle` templates are LF-pinned). This is the *primary* guarantee: a verbatim
  `Assert.Equal` golden test (no `\r\n` normalization) is then correct on both platforms.
  Comparer-side `\r\n`→`\n` normalization (the in-tree fixture reads and the sample gallery's
  [`compare-golden.sh`](../../../samples/tools/compare-golden.sh)) stays as defense-in-depth, but
  a new golden/fixture directory is added to the `.gitattributes` `eol=lf` list **in the same
  change that introduces it** — never rely on the comparer alone. A golden that shows a spurious
  Windows-only `\r\n` diff is a missing `.gitattributes` pin, not a golden to regenerate.
- **Golden change policy:** a golden file changes only when the spec says output
  changes, and the diff is reviewed as part of the PR — a golden regenerated to make a
  red test green without a spec-backed reason is a defect.
- Table-driven semantic pins (e.g. the native-expression operator-semantics table comparing against
  C#-compiled expected values) live as xUnit `[Theory]` data next to the feature's tests,
  and the table itself is part of the spec — changing a row is a spec change.
- Negative/security tests assert **positioned diagnostics** (`HED*` ID + position), never
  just "compilation failed".

## Regression gates

The gate every spec runs before merge, in one combined invocation:

1. `dotnet build -c Release` — whole solution, all TFMs.
2. `dotnet test src/Heddle.Tests` — full suite, all TFMs, zero failures; golden
   comparisons byte-identical for templates the change does not intentionally touch.
3. **Grammar-stability check** for specs that declare no grammar change (the default):
   `src/Heddle.Language/generated/` has no diff. A spec licensed to change grammar instead commits exactly one regen
   alongside the `.g4` change and diff-reviews it.
4. **Benchmarks when a hot path is touched**: run the affected
   [Heddle.Performance](../../../src/Heddle.Performance) benchmarks before/after on the
   same machine. Acceptance: allocated bytes must not increase, and mean time must not
   regress beyond BenchmarkDotNet's reported error for that benchmark. An intentional
   trade-off needs maintainer ratification recorded in the owning spec.
5. Docs build when docs pages changed: `cd docs && npm run docs:build` (run from an
   uppercase-drive cwd on Windows; `preserveSymlinks` is already configured).

## Test authoring conventions

- Descriptive PascalCase test names reading as sentences
  (`NamedAndUnnamedCSharpCallsClassifyParenTokensEqually`); regression tests carry an XML
  doc comment explaining the pinned scenario and why it matters.
- Assert with context: `Assert.True(result.Success, result.ToString())` so a failure
  prints the compile errors.
- Concurrency: any spec adding state that could be shared across renders ships a
  parallel-render test proving isolation (the branch-set opposite-conditions test —
  [BranchConcurrencyTests](../../../src/Heddle.Tests/BranchConcurrencyTests.cs) — is the
  model).
- Sandbox-negative tests are non-optional wherever an execution capability is scoped
  (expressions, props/named arguments, precompiled function binding): every rejected construct in the spec's diagnostics table has a test
  proving it produces a positioned compile error and **never executes**.
- Tests are exempt from DRY pressure (see
  [coding standards](coding-standards.md#dry-applied)): repeat setup where it makes a
  failing test diagnosable at a glance.
