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

## Precompiled-tier posture

> Added by [ledger entry E8](../records.md#cross-spec-amendments-ledger) (generator ↔ engine
> code-sharing program, phase 0). Normative for every spec whose work touches the source
> generator, the precompiled registry, or the resolver.

The precompiled tier and the dynamic engine render byte-identical output *by design*, so a
precompiled→dynamic fallback is invisible in output. A test that does not pin the tier is
therefore not evidence about the tier it claims to test — two shipped drifts (the content-hash
input mismatch and the nested/generic AQN mismatch) reached release precisely that way. The rule:

- **End-to-end precompiled tests pin the precompiled tier.** A test that renders real generator
  output through registration → resolver → gauntlet runs under
  `TemplateOptions.PrecompiledMismatchPolicy = Strict` **and** a fallback sentinel hooked onto
  `PrecompiledTemplates.OnFallback`; any fallback raised during the test fails it. In the
  generator integration suite the two guards are packaged as `FallbackGuard` /
  `DifferentialHarness.RenderViaResolver`.
- **Fallback is tested only where fallback is the subject, and the expectation is declared.**
  A build-time degrade is declared with `DifferentialHarness.ExpectDegrade(gen, key)`; a
  run-time fallback with `FallbackGuard.Expect(key, reason)`. Nothing else may fall back —
  the complete set of tests that expect a fallback must stay enumerable by grepping those two
  APIs.
- **New feature areas contribute their templates to the gauntlet-crossing corpus** rather than
  re-plumbing their own suites. Feature suites keep direct-invoke isolation (a red test points
  at the emitter, not at five layers of plumbing); the corpus sweep carries the tier posture
  for every template that precompiles, with at least one pass file-backed so the staleness /
  content-hash path is exercised.
- **A guarded fixture that fails because of a known, owned defect is quarantined, never
  weakened.** Its `Skip` string names the owning work item and the defect, and the owning work
  item un-skips it as acceptance evidence. An unexplained or orphaned skip is a review failure.

## Test-input single-sourcing

> Added by [ledger entry E9](../records.md#cross-spec-amendments-ledger) (generator ↔ engine
> code-sharing program, phase 7). Normative for every spec that adds tests exercising the same
> language construct on more than one tier (build tier, run tier, editor).

**A duplicate test input is a duplicate rule one level up.** The code-sharing program removed rules
the generator and the runtime each maintained as two hand-kept copies, because nothing forced the
copies to agree. A template shape written as an inline string in a generator test *and again* as an
inline string in a runtime test drifts for exactly the same reason, and the consequence is worse:
the two tiers are then verified against two texts, so the premise of a differential test — that
both tiers were handed identical input — silently stops holding, and no assertion anywhere notices.
The rule:

- **A template shape that more than one tier verifies exists exactly once**, as a file in the
  shared test corpus, and every consuming suite reads that file. Both tiers compile the same bytes.
  Copying a template literal from one suite into another is the defect this rule names, not a
  shortcut.
- **Every corpus entry declares its intent, and the declaration is total.** An entry says how the
  build tier must classify it (precompiles / degrades to a marker / falls back safely / front-end
  error) and how it may be exercised (standalone and byte-compared / with a named model / resolve-only,
  for shapes no tier can render standalone), with a one-line justification. Intent is *declared*,
  never inferred from a filename: negative probes that must fail to compile and deliberate-degrade
  shapes that must not precompile are ordinary corpus members, and a sweep that assumed otherwise
  would either break or quietly widen what counts as precompiled. Completeness is asserted in both
  directions — an entry with no declaration and a declaration with no entry are each a red test.
- **Corpus membership is gated by set equality against the declaration, never by a count or a
  floor.** A count can be made green by editing one digit; set equality can only be made green by
  naming the file whose classification changed and writing down why. Floors are worse still — this
  repo has shipped a `>= 25` floor against an actual 40, which let fifteen templates stop
  precompiling silently.
- **Proximity still wins where it genuinely wins.** A one-line probe, a diagnostic-position probe
  whose assertion pins offsets into that literal, and a constructed or `[Theory]`-generated template
  stay inline: a template next to its assertion is better test code, and those cases gain no
  cross-tier coverage. The rule targets shapes two tiers verify, not every string literal.
- **Shared test *inputs* are reached from the consumer's own output directory**, never by walking up
  out of `bin/<cfg>/<tfm>` into a sibling project. Path traversal to another project's inputs
  encodes the configuration name, the TFM directory and the project nesting as assumptions, and its
  failure mode is a test that finds nothing and passes.

  **A build copy is not a second home.** Copying an input into a consumer's output directory so a test
  can read it at run time says nothing about where the input is *stored*, and creates no ownership
  problem to solve by relocating it. Inputs live in tracked folders; which project directory holds them
  is a filing detail. This rule constrains how a test *reaches* an input, never where the repository
  keeps it.

  **The rule is about inputs, and inputs are what git stores** — templates, goldens, fixtures. A test
  that reads another project's *output* — a compiled assembly, a generated file — is doing something
  else and is not covered here: build artifacts legitimately live outside the consumer's own output
  directory, because the thing that produced them decides where they go. Such a read carries its own
  obligation instead: it must be **conflict-free and it must fail loudly on a miss**. Order the build
  with a `ProjectReference` (`ReferenceOutputAssembly="false"` where only ordering is wanted), and
  assert the artifact was found rather than returning early — the silent-pass failure mode above is
  the one thing both cases share, and it is the part that actually bites.
- **What moves is byte-identical, and its encoding is pinned by a gate.** A move is proven a rename;
  a merge of two tiers' near-identical copies resolves and *records* every difference (a divergence
  found while merging is a drift finding, not a formatting nit). Line endings are pinned in
  [`.gitattributes`](../../../.gitattributes) per the rule above; byte-order marks are independent
  of that pin and get their own assertion, because a BOM-bearing fixture is either deliberate
  coverage or an accident and the two must be distinguishable.
