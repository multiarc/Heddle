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

## Running the suites

Test projects are xUnit v3 on Microsoft Testing Platform: each one is a stand-alone executable that
hosts its own runner. Three consequences, all of which bite silently if ignored:

- **`dotnet test` needs `--project` or `--solution`.** The directory form (`dotnet test src/Foo`) is
  rejected outright, so it fails loudly rather than testing nothing.
- **Filters are MTP syntax** — `--filter-method`, `--filter-class`, `--filter-namespace`,
  `--filter-trait`, after a `--` separator. A VSTest-style `--filter FullyQualifiedName~X` is not
  understood. A filter that matches nothing exits **8**, so a stale filter can no longer pass by
  running nothing, which is how several checks in this repo came to be documented as pinned while
  nothing executed them.
- **Every CI leg passes a floor.** `.github/scripts/dotnet-test-guarded.sh <minimum> <project>` wraps
  `--minimum-expected-tests`, which exits **9** when fewer tests ran than the suite really has. The
  floor is the suite's true size, not 1 — a suite that loses half its rows still ran "some" tests.
  When a suite legitimately grows or shrinks, update the floor in the same commit.

The runner also randomises test order per run. Order-dependent tests therefore fail intermittently
rather than never, which is a feature: it found one on the first run after the migration.

## A reported issue becomes a test, whichever way it turns out

Every finding is measured before it is believed, and the measurement decides the test's shape. A
report is a claim, not a result: findings in this repository have been overturned as often as they
have been confirmed, and both outcomes are worth the same test.

Write the test first, run it, and then read the outcome:

- **Confirmed red** — the defect reproduces, and the fix is not part of this change. Check the test
  in **skipped**, with the reason and its owner in the skip string:
  `[Fact(Skip = "known defect — <owner>: <defect>; un-skip with that fix")]`. Rehearse it red first
  and check *why* it went red: a test that fails for the wrong reason pins nothing. The skip list is
  the pending-work list, and un-skipping is the fixer's acceptance evidence.
- **Overturned, green, and the behaviour is what it should be** — **keep the test open.** There is
  nothing to fix, so there is nothing to skip. The test stays as a normal running test: it is now
  the pin that stops the behaviour regressing, and the record that the claim was checked rather
  than dismissed. Say in its doc comment what was claimed and why it does not hold — that is what
  stops the same report arriving again next cycle.
- **Green, but you are not sure the current behaviour is the intended one** — do not guess, and do
  not quietly pick one. A test asserting the wrong contract is worse than no test, because it makes
  the wrong behaviour permanent. **Ask the maintainer** what the expected outcome is, then write the
  test to that answer.

A green test proves nothing on its own. Whichever way it lands, pair it with a near-neighbour that
must behave the *other* way, so the row cannot be satisfied by a blanket refusal or a blanket
acceptance — and confirm the assertion is answered by the arm it names, not by a different one.

## Suite homes

| Home | Framework | Role |
| --- | --- | --- |
| [src/Heddle.Tests](../../../src/Heddle.Tests) | xUnit v3 (MTP), `dotnet test --project` | Unit, integration, golden-file, negative/security, and concurrency tests. Multi-targets `net8.0;net10.0` (+ `net48` on Windows) — new tests must pass on **all** TFMs. |
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
2. `dotnet test --project src/Heddle.Tests/Heddle.Tests.csproj` — full suite, all TFMs, zero
   failures; golden comparisons byte-identical for templates the change does not intentionally
   touch. Run the suites serially: they share process-global engine state.
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

**Both configurations, not just Debug.** The engine carries `#if DEBUG` arms that change *runtime*
behaviour rather than only diagnostics — the model-type guard is unconditional under `DEBUG` and
opt-in under `Release` — so a Debug-only run is structurally unable to see a Release-only failure.
One survived in the generator integration suite for exactly that reason: every suite in that series
was verified in Debug. Each suite therefore carries **both legs**, all TFMs — where one is missing the
table says so rather than leaving the reader to infer coverage that is not there:

| Suite | Debug | Release |
| --- | --- | --- |
| `src/Heddle.Tests` | [`dotnet.yml`](../../../.github/workflows/dotnet.yml), its own named leg with a test-count floor, Linux **and** Windows | [`lsp.yml`](../../../.github/workflows/lsp.yml), Windows only |
| `src/Heddle.LanguageServices.Tests` | same | [`lsp.yml`](../../../.github/workflows/lsp.yml), Windows only |
| `src/Heddle.Generator.Tests` | same | [`dotnet.yml`](../../../.github/workflows/dotnet.yml), Linux **and** Windows |
| `src/Heddle.Generator.IntegrationTests` | same | [`dotnet.yml`](../../../.github/workflows/dotnet.yml), Linux **and** Windows |
| `src/Heddle.Tool.Tests` | same | **not covered** — a known gap, recorded rather than implied away |

A test whose expectation genuinely differs by configuration writes **both** arms behind `#if DEBUG` /
`#if !DEBUG`, so each configuration's behaviour is pinned and neither is left to whichever build the
author happened to run. Making the expectation explicit — asking for the option the assertion needs —
is the fix; deleting the assertion is not.

**Rendered numbers are culture-dependent, and a golden is not.** The default value-to-text render is
the value's own `ToString()`, so decimal separators, group separators, digit substitution and the
negative sign all come from `CultureInfo.CurrentCulture` (ar-SA prefixes U+061C to a negative sign;
de-DE and tr-TR use a decimal comma). Both tiers reach text through that same call, so they do not
diverge — but a committed golden or an inline expected-text literal is written in one culture and
compares byte-for-byte. A test asserting rendered numeric text either **pins the culture it renders
under** (the right answer wherever the expectation is a committed golden this suite may not
regenerate) or **builds its expectation under the ambient culture** from typed literals (the right
answer wherever the culture is not the subject and pinning it would discard coverage — notably a
cross-tier differential, where agreeing under the host's own culture is the thing only that test can
observe). Changing the render to make such a test pass is a golden/engine change and goes through
review, never through the test.

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

## Documentation currency

Prose is an artifact with a maintainer, and a wrong sentence in a normative document is a latent bug
rather than an untidiness — one has already been demonstrated to instruct a reader into breaking
working code.

- **A change that alters an observable behaviour names the documents that describe it, in the same
  landing, and either updates them or records why not.** For four surfaces the named list is derived
  rather than remembered: diagnostic ids (`DiagnosticIdTests` — every shipped id named in a published
  page, and in the page the registry's owner column links), option names and defaults
  (`WorkspaceOptionParityTests` — the documented settings table and the editor's contributed defaults),
  public members (`PublicApiDocMentionTests` — documented `Type.Member` mentions against the API
  goldens), and versions (`VersionConsistencyTests`). Links and line citations are covered separately
  (`DocumentationLinkTests`). For everything else the list is a review obligation, and the rule's value
  is that it exists to be pointed at.
- **What cannot be gated is dated.** A document whose claims have been verified against source carries
  a footer naming the commit and the date, and marks the individual claims some gate covers. That
  marker is what makes documentation authority conditional: a marked claim outranks the
  implementations, an unmarked one is evidence of intent, so a contradiction between it and both tiers
  agreeing is investigated and recorded rather than obeyed.
- **Recorded so the gap is not later mistaken for an oversight, the following are *not* gateable:**
  prose accuracy about behaviour no test observes; the accuracy of a rationale or a design argument;
  overstatement ("all", "every", "never", "exactly one") except where the quantity is machine-
  countable; whether a document's *omissions* matter; and the ordering and emphasis choices that make
  prose useful or misleading without any individual sentence being false. Pretending otherwise
  produces either a gate that checks something trivial and calls it coverage, or a gate people
  disable.
