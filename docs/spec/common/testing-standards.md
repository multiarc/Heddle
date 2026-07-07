# Testing standards (shared across all phases)

Normative for the *Testing plan* section of every phase spec (structure defined in
[spec-conventions.md](spec-conventions.md)). This instantiates the canonical
implementation–verification loop the roadmap ratified for all phases and pins the shared
mechanics: suite homes, fixture/golden conventions, and regression gates.

## The canonical loop

Every work item in every phase runs the same loop (from the
[roadmap index](../../roadmap/README.md), unchanged):

1. **Spec first** — the executable spec (fixtures, tables, negative/security tests, per
   the phase's TDD verdict) is written **failing** before implementation.
2. **Implement** until the new spec is green.
3. **Gate** — the phase's regression requirements run: the existing suite plus goldens
   byte-identical, benchmarks wherever a hot path is touched.
4. **Fix forward** — a red gate means fixing code. Goldens, perf baselines, and spec
   tables change only through maintainer review/re-ratification — never to silence a
   failing test.
5. **Repeat** per work item; phase exit = all success criteria green in **one combined
   run**, plus the phase's gallery item scheduled into
   [phase 9](../../roadmap/phase-9-demo-and-integration.md).

Each phase spec carries the roadmap's **TDD verdict** for that phase (which parts are
test-first versus test-with) and must not weaken it.

## Suite homes

| Home | Framework | Role |
| --- | --- | --- |
| [src/Heddle.Tests](../../../src/Heddle.Tests) | xUnit, `dotnet test` | Unit, integration, golden-file, negative/security, and concurrency tests. Multi-targets `net6.0;net8.0;net10.0` (+ `net48` on Windows) — new tests must pass on **all** TFMs. |
| [src/Heddle.Performance](../../../src/Heddle.Performance) | BenchmarkDotNet (`[MemoryDiagnoser]`) | Render/compile benchmarks incl. the Razor head-to-head. Hot-path phases add or extend benchmarks here. |
| `samples/` gallery (created by phase 9) | Per-sample CI jobs with golden assertions | Each phase's demo/integration item. Phase 9 owns the harness; each phase owns its sample and records it in its spec as a deferred deliverable until the harness exists. |

## Fixtures and goldens

- Template fixtures are `.heddle` files under
  [src/Heddle.Tests/TestTemplate](../../../src/Heddle.Tests/TestTemplate); expected output
  goldens are sibling `.html`/text files (existing naming: `<name>.heddle`,
  `generated-<name>.html` / `test-<name>.html`). New phases follow the sibling-pair
  pattern and prefix fixture names with a phase-recognizable stem
  (e.g. `expr-precedence.heddle`, `branching-interleaved.heddle`).
- Tests normalize line endings when reading fixtures (`.Replace("\r\n", "\n")` — the
  established pattern) so goldens are byte-identical across OSes.
- **Golden change policy:** a golden file changes only when the phase spec says output
  changes, and the diff is reviewed as part of the PR — a golden regenerated to make a
  red test green without a spec-backed reason is a defect.
- Table-driven semantic pins (e.g. phase 1's operator-semantics table comparing against
  C#-compiled expected values) live as xUnit `[Theory]` data next to the feature's tests,
  and the table itself is part of the spec — changing a row is a spec change.
- Negative/security tests assert **positioned diagnostics** (`HED*` ID + position), never
  just "compilation failed".

## Regression gates

The gate every phase runs before merge, in one combined invocation:

1. `dotnet build -c Release` — whole solution, all TFMs.
2. `dotnet test src/Heddle.Tests` — full suite, all TFMs, zero failures; golden
   comparisons byte-identical for templates the phase does not intentionally change.
3. **Grammar-stability check** for the seven no-grammar phases (2, 3, 4, 6, 7, 8, 9):
   `src/Heddle.Language/generated/` has no diff. Phases 1 and 5 instead commit exactly one
   regen alongside the `.g4` change and diff-review it.
4. **Benchmarks when a hot path is touched**: run the affected
   [Heddle.Performance](../../../src/Heddle.Performance) benchmarks before/after on the
   same machine. Acceptance: allocated bytes must not increase, and mean time must not
   regress beyond BenchmarkDotNet's reported error for that benchmark. An intentional
   trade-off needs maintainer ratification recorded in the phase spec.
5. Docs build when docs pages changed: `cd docs && npm run docs:build` (run from an
   uppercase-drive cwd on Windows; `preserveSymlinks` is already configured).

## Test authoring conventions

- Descriptive PascalCase test names reading as sentences
  (`NamedAndUnnamedCSharpCallsClassifyParenTokensEqually`); regression tests carry an XML
  doc comment explaining the pinned scenario and why it matters.
- Assert with context: `Assert.True(result.Success, result.ToString())` so a failure
  prints the compile errors.
- Concurrency: any phase adding state that could be shared across renders ships a
  parallel-render test proving isolation (phase 3's opposite-conditions test is the
  model).
- Sandbox-negative tests are non-optional wherever an execution capability is scoped
  (phases 1, 5, 7): every rejected construct in the spec's diagnostics table has a test
  proving it produces a positioned compile error and **never executes**.
- Tests are exempt from DRY pressure (see
  [coding standards](coding-standards.md#dry-applied)): repeat setup where it makes a
  failing test diagnosable at a glance.
