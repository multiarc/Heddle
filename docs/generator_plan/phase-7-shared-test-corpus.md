# Phase 7 — shared test corpus

## Header

- **Status:** **stage 0 implemented (2026-07-26); migration stages 1–5 stopped before starting, with
  cause.** WI1–WI7 are landed and green. WI8/WI9 (stages 1–5) are **not** started: surveying stage 1's
  own inputs found its premise does not hold — see
  [Implementation record](#implementation-record-2026-07-26). The corpus is unchanged at 62 templates
  and the precompiled figure unchanged at 40, which is the byte-neutrality D9 demanded of stage 0.
- **Goal (one line):** Every tier's tests consume the *same* template texts and the *same* goldens
  from one shared, intent-declaring corpus — so a feature's shape exists exactly once in the repo,
  both tiers compile the same bytes, and the gauntlet-crossing posture holds by construction rather
  than by author discipline.
- **Depends on:** phase 0 (the posture and the corpus sweep this phase feeds); phases 1–6 landed
  (their extraction gates are what the corpus must stay byte-neutral against). No phase depends on
  this one — it is a follow-on that closes phase 0's D4 residue.
- **Changes an externally-visible contract:** no. Test assets, test-project MSBuild wiring, and one
  additive testing-standards rule (D8, via the amendments mechanism). No engine source, no public
  API, no rendered bytes, no diagnostic ID (`HED7018`–`HED7024` are claimed; this phase claims
  none, and needs none — every gate it adds is a test assertion, not a build diagnostic).
- **Numbering note.** "Phase 7" continues *this* plan's 0–6 sequence. It is unrelated to the
  precompilation spec's phase 7, which existing test doc comments cite (e.g.
  `CorpusDifferentialTests`' *"Phase 7 WI9 (D20)"* refers to [precompilation.md](../precompilation.md)).
  Work items here must not reuse that citation form without qualifying it.

## Goal

The seven-phase program eliminated rules that the source generator and the runtime engine each
maintained as hand-kept duplicate copies. **Hand-kept duplicate test *inputs* are the same defect
one level up**, and the tree still has them.

The mechanism is identical. A shared rule with two copies drifts because nothing forces the copies
to agree; a template shape written as an inline string in `Heddle.Generator.IntegrationTests/RegionTests.cs`
*and again* as an inline string in `Heddle.Tests/RegionTests.cs` drifts for exactly the same reason.
The two tiers are then verified against two texts, and the whole point of a differential suite —
that build tier and run tier are handed identical input — silently stops being true. Nothing
detects it. Surveying for this phase found the duplication is real and literal: **18 distinct
template literals appear character-for-character in both `Heddle.Tests` and the generator suites**
(the region/props/`@out` families; see [External grounding](#external-grounding)). Those 18 are the
lucky ones — they still agree. There is no gate that keeps them agreeing, and one edit on one side
is all it takes.

The program's own thesis, applied to its own evidence: **a template shape should exist exactly
once, and both tiers should compile the same bytes.** That is not an aesthetic preference. Four
concrete consequences follow, each of which the tree needs:

1. **It closes phase 0's D4 residue.** D4's coverage rationale claimed *"the corpus is the union of
   the feature templates"*; the post-implementation audit found that false — the feature suites
   build templates inline, so their shapes never enter `TestTemplate` and never join the
   gauntlet-crossing sweep. Roughly 130 feature tests therefore never cross the gauntlet. The
   standing rule that new feature areas contribute their templates to the corpus
   ([testing-standards §Precompiled-tier posture](../spec/common/testing-standards.md#precompiled-tier-posture),
   added by phase 0's WI8) has never been backfilled, and cannot be enforced while contributing a
   template means hand-adding a `<None Update>` row to one project's csproj and reaching it from
   another project by path traversal.
2. **It deletes a whole failure class, rather than asserting against it.** The generator
   integration suite locates the corpus by rewriting its own assembly path to `Heddle.Tests`,
   climbing `../../..` out of `bin/<cfg>/<tfm>`, and looking for `TestTemplate`. That helper is
   triplicated, as is `LoadCorpus` beside it. Until commit `45154fd`, a miss made five tests
   silently `return`. The assert that replaced the silent return is a fine stopgap, but **the
   fragility is the mechanism, not the assert**: when the corpus is copied into every consumer's
   own output directory, `CorpusDir()` collapses to `Path.Combine(AppContext.BaseDirectory, …)`
   — the shape `Heddle.LanguageServices.Tests` already uses — and the assert becomes unnecessary
   rather than load-bearing. The traversal also silently hid a coverage hole: the integration
   suites build `net8.0;net10.0`, so the path swap can only ever find `Heddle.Tests`' `net8.0` and
   `net10.0` corpus copies — the `net6.0` and Windows `net48` copies are gated by nothing.
3. **"A tier stopped exercising this" becomes visible.** A shape that exists once, with a declared
   intent row, has an owner and a classification. A shape that exists twice as string literals has
   neither: deleting one copy reddens nothing, and the surviving copy looks complete.
4. **Both tiers compile the same bytes.** This is the program's single-artifact principle, and the
   argument for it does not weaken when the artifact is a test input.

## Non-goals / scope boundary

- **No engine or generator source change.** Test assets, test-project MSBuild wiring, and shared
  test-side accessor code only. Nothing this phase does may change a rendered byte; the existing
  golden, snapshot and differential suites are the proof (they are unchanged by construction —
  see D9).
- **The phase-0 D4 posture stands.** Feature suites keep direct-invoke isolation; this phase does
  *not* convert them to resolver-path renders. It makes their *shapes* reachable by the corpus
  sweep, which is what D4 always intended.
- **No new gauntlet checks, manifest rows, or diagnostics.**
- **Not a wholesale migration.** Trivial one-line probes and parse-position negative probes stay
  inline (D4). Roughly 190 native-expression / lint / parse literals are explicitly out of scope.
- **Other corpora are out of scope**, now by ruling rather than by default (OQ7.1, OQ7.2).
  `src/Heddle.Performance/TestTemplates` (9), `src/Heddle.Performance/ThirdParty` (1),
  `benchmarks/dotnet/templates/**` (18), `samples/**/templates` (9) and
  `src/Heddle.LanguageServices.Tests/Corpus` (3) are separate asset sets with their own contracts
  (the parity contract, the golden-corpus spec, per-sample golden jobs).
- **`src/Heddle.Performance` is not touched at all** (OQ7.2 ruling). A benchmark effort is
  mid-flight in that project; the shared props file serves the four **test** projects only, and the
  benchmark project keeps its own path-traversal helper. That is **accepted residue**, recorded so
  nobody later reads it as an oversight and "fixes" it: the failure class D2 eliminates is gone from
  the test suites and survives in the benchmark project until the benchmark work settles.
- **`src/Heddle.LanguageServices.Tests.Corpus` is not a precedent and is not touched.** Despite the
  name it holds **no** `.heddle` files: it is a 74-line satellite *export assembly* (`Corpus.cs`,
  `[assembly: ExportExtensions]` / `[ExportFunctions]` plus two POCOs) that exists so the language
  service tests can load a real external assembly **by path** into a collectible `AssemblyLoadContext`.
  It is a compiled artifact whose purpose is to be a DLL. Nothing about it argues for or against a
  content-only corpus project — do not cite it in that debate.
- **No CI/pipeline changes** beyond the suite itself.

## Design direction

### D1 — Physical home: **the corpus does not move** (revised 2026-07-26)

**Decision (revised — supersedes the struck-through original below).** The corpus **stays where it
is**, in `src/Heddle.Tests/TestTemplate/`. Nothing relocates.

The original decision moved it to a neutral `src/TestCorpus/` on the reasoning that a shared input
should not live inside one consumer. That reasoning was rejected (user, 2026-07-26): *"Build artifacts
and build related copies does not conflict logically with the fact that input is stored elsewhere —
it's just a build copy, not a logical issue."* Copying an input into a consumer's output directory so a
test can read it is a **build copy**. It is not a second home for the input, and it creates no
ownership question to solve by relocation. Test inputs are the git-tracked artifacts; they live in
tracked folders, and which project directory holds them is a filing detail, not a correctness property.

**What survives, and it is the whole point of the phase:** sharing is still one MSBuild props file that
every consuming test project imports with a single line, globbing the corpus as `Content` with `Link`
and `CopyToOutputDirectory` so each consumer gets its own copy in its own output directory. The props
file simply points at the existing directory instead of a new one. The generator suites become
consumers of the same files the engine suites already use — which is what makes the ~130 inline-string
feature templates able to cross the precompiled gauntlet, and that gap, not the folder layout, is what
phase 7 exists to close.

**Cost of the reversal, stated:** the relocation was executed once and reverted (`ac1d0b4`); the full
move is preserved at branch `wip/phase7-corpus-move` if any part of it is wanted. The revert cost
nothing but the move itself — no test changed meaning, because a rename of an input the build copies
anyway is invisible to every reader.

<details>
<summary>Superseded original (do not implement)</summary>

**Decision.** The corpus moves to a neutral, project-independent home — `src/TestCorpus/` — with
`templates/` holding the `.heddle` files and their sibling goldens. Sharing is one MSBuild props
file, `src/TestCorpus/TestCorpus.props`, that every consuming test project imports with a single
line. The props file globs the corpus as `Content` with `Link` and `CopyToOutputDirectory`, so each
consumer gets its **own** copy in its **own** output directory:

```xml
<Content Include="$(MSBuildThisFileDirectory)templates\**\*"
         Link="TestTemplate\%(RecursiveDir)%(Filename)%(Extension)"
         CopyToOutputDirectory="PreserveNewest" />
```

The **link folder name stays `TestTemplate`**, so every existing read path
(`File.ReadAllText("TestTemplate/…")`, `options.RootPath = "TestTemplate"`) is unchanged and the
physical move is invisible to test code. No new `.csproj`.

**Rationale.** A csproj buys nothing here: there is no code to compile and no assembly anyone needs
to load, and it would drag in a TFM matrix, a `ProjectReference` edge in the build graph, and a
solution row — cost with no consumer. (Contrast `Heddle.LanguageServices.Tests.Corpus`, which is a
project *because* a real DLL must exist on disk to be loaded by path.) The props file, meanwhile,
gives exactly the two properties that matter: **one physical copy under source control**, and
**real files at a real path in every consumer's output**. It also replaces `Heddle.Tests.csproj`'s
**112 hand-listed `<None Update="TestTemplate\…">` rows** with one glob — the hand-listing is
itself an instance of the defect this phase exists to remove, since a corpus file added without its
row is a file that silently never reaches any output directory.

The neutral home matters independently of the mechanism: the corpus is not `Heddle.Tests`'
property. Today it lives inside one consumer's project directory and the other consumers reach in,
which is precisely what produced the path traversal.

**Alternatives rejected.**
- *Embedded resources.* Rejected. The engine contract under test is file-based: `TemplateOptions.RootPath`
  + the `.heddle` extension rule + `PrecompiledGauntlet.HashFile`, which opens a `FileStream` and
  decodes with BOM detection. `DifferentialHarness.StageCorpus` materialising to a temp directory
  makes resources *compatible* with the integration suite's file-backed sub-mode, but it does
  nothing for `Heddle.Tests`' own resolver tests, which point `RootPath` at the output directory.
  Resources would also put a build-time encoding pipeline between the checked-in bytes and the
  bytes under test — the exact hazard D7 is written to prevent.
- *A content-only `Heddle.TestCorpus.csproj`.* Rejected for the cost/benefit above. Reversible: if
  a future consumer needs the corpus as an assembly resource or a NuGet asset, the props file's
  glob is trivially re-homed inside a csproj.
- *Keeping the corpus in `Heddle.Tests` and adding links outward.* Rejected — it leaves ownership
  where it caused the problem, and every consumer's link path still hard-codes a sibling project's
  directory layout.
- *`git` submodule / separate repo.* Rejected as absurd for in-tree test assets.

</details>

### D2 — The traversal is deleted, not hardened; corpus access becomes one shared accessor

**Decision.** With D1 in place, `CorpusDir()` in all three integration-suite call sites becomes
`Path.Combine(AppContext.BaseDirectory, "TestTemplate")`, and the triplicated `CorpusDir` /
`HeddleTestsDll` / `LoadCorpus` / diagnostic-fixture-set code collapses into one shared accessor,
`src/TestCorpus/TestCorpusIndex.cs`, linked into every consumer by the same props file (the
precedent is `Heddle.Tests/LineIndexVectors.cs`, `DiagnosticCorpusVectors.cs` and
`PropDefaultConversionVectors.cs`, already `<Compile Include … Link="Shared\…">`-shared into
`Heddle.Generator.Tests`). `TestCorpusIndex` exposes: the enumerated, ordinal-sorted, `/`-relativized
entry list; the raw text of an entry; and the intent table of D3.

Consequences to land in the same change: the five `if (dir == null) return;` sites and their
replacement asserts are removed as dead; the `net6.0`/`net48` copies stop being ungated (the
integration suite still builds `net8.0;net10.0`, but each of `Heddle.Tests`' TFM outputs now
receives the corpus from the same glob rather than from a hand-maintained list); and the stale
floor `Assert.True(templates.Count >= 40, "Expected the full TestTemplate corpus (~45 files).")`
in `CorpusDifferentialTests` is deleted — it is a live instance of the anti-pattern D5 governs
(a floor of 40 and a comment saying "~45" against an actual **62**).

**Rationale.** A helper that reconstructs a sibling project's `bin` layout from its own assembly
path encodes assumptions about configuration name, TFM directory, and project nesting — four
independent things that a build change can move. Copying the files instead means the only
assumption left is "content copies to output", which MSBuild guarantees and every other test in
the repo already relies on.

**Alternatives rejected.** Hardening the traversal (a `Directory.GetParent` walk searching upward
for a marker file): rejected — it keeps the failure class and adds a search. Passing the path in
via an MSBuild-generated constant: rejected — it re-creates a build-layout coupling in a different
spelling and still leaves the corpus absent from the consumer's output.

### D3 — Intent is declared per entry, in one compile-checked table, with two orthogonal axes

**Decision — the load-bearing one.** Every corpus entry declares its intent in a single shared
table, `src/TestCorpus/CorpusIntent.cs` (linked like `TestCorpusIndex`), with **one row per
`.heddle` file** and **two orthogonal fields plus a mandatory justification**:

| Field | Values | Meaning |
| --- | --- | --- |
| `Tier` | `Precompiles` · `DegradesToMarker` · `FallsBackSafely` · `FrontEndError` | How the **build tier** must classify this entry. Exactly today's four `CorpusDifferentialTests` buckets, promoted from prose comments and a `HashSet` to a declared field. |
| `Render` | `Standalone` · `WithModel(key)` · `ResolveOnly` | How the **sweep** may exercise it. `Standalone` = model-less and byte-compared against the dynamic reference. `WithModel` = crosses the gauntlet in the sweep; byte parity is owned by the named family differential suite. `ResolveOnly` = not standalone-renderable (a bare `@else` continuation, or an import library only meaningful when imported). |
| `Why` | non-empty string | One line saying why this classification is correct. Enforced non-empty by a test. |
| `Bom` | bool, default false | This file intentionally carries a UTF-8 BOM (see D7). |

Two completeness gates make the table impossible to skip: **every `.heddle` file has exactly one
row**, and **every row names a file that exists**. A template added without a row fails the suite
with the filename in the message. This is what turns phase 0's standing rule from a request into a
mechanism — contributing a template *requires* declaring what it is for.

`Render` deserves its own axis because today's sweep passes a **blanket `render: false`**: because
*a handful* of entries are import fragments that no tier can render standalone, **every** entry
loses its byte assertion. Per-entry `Render` lets the sweep render the `Standalone` set and skip
only the entries that genuinely cannot be rendered — a strict coverage gain that falls out of the
taxonomy rather than needing separate work.

**Rationale.** The intent decision must be made *once, up front*, because the alternative is a
per-template judgement call at migration time, made ~90 times by whoever happens to be migrating,
with no reviewable record. And it has to be made before any migration starts for a sharper reason:
many inline templates are **negative probes that must fail to compile** or **deliberate-degrade
shapes that must not precompile**. Dropping those into a sweep whose premise is "every precompiled
entry crosses the gauntlet" either breaks the sweep or, worse, quietly widens what counts as
precompiled. `BranchRoleUniversalityTests` alone contributes 32 (`Heddle.Tests`) + 13
(integration) literals that are overwhelmingly deliberate-degrade.

A C# table is chosen over the alternatives because it is compile-checked, because the repo's
established pattern for shared test data is exactly this (`DiagnosticCorpusVectors`,
`LineIndexVectors`), and because the classification it replaces is *already* a C# `HashSet` in
`CorpusDifferentialTests` — this is a promotion of an existing artifact, not a new concept.

**Alternatives rejected.**
- *Filename conventions* (`must-not-compile-*.heddle`, a `degrade-` prefix). Rejected: encodes at
  most one axis, is unenforceable, silently mis-classifies on rename, and cannot carry `Why`.
- *An in-template header comment* (`@* intent: degrades *@`). Rejected: it changes the bytes of the
  artifact under test, which is disqualifying for a corpus whose whole value is byte fidelity —
  and several entries' expected output depends on their exact leading whitespace.
- *A TSV/JSON sidecar manifest.* Rejected: needs a parser and a schema, loses compile-time
  checking, and buys nothing the C# table lacks. (Reconsider only if a non-.NET consumer ever needs
  the table — the named trigger.)
- *One flat enum over the cross-product* (`PrecompilesStandalone`, `PrecompilesWithModel`, …).
  Rejected: the two axes are genuinely independent, and the cross-product enum is where the
  "a tier stopped exercising this" signal gets lost.

### D4 — Selective migration, with the migrate/keep-inline test written down before stage 1

**Decision.** Migration is selective, and the criteria are fixed here rather than at migration
time. **Migrate** an inline template when *any* of:

- (a) the same or near-identical text already exists in another tier's suite (the 18 verified
  literal duplicates — these go first, they are the defect itself);
- (b) it exercises a **language or emitter shape** (a region/slot arrangement, a definition layering,
  a branch protocol, a props layout, an `@out` body form) rather than an assertion mechanic;
- (c) it is multi-line, or carries more than a single trivial directive.

**Keep inline** when *any* of:

- (d) it is a one-line probe whose readability depends on sitting next to its assertion — a template
  next to its `Assert` is genuinely better test code, and this phase is not an argument against
  that;
- (e) it is a **parse/diagnostic-position probe** whose assertion pins a line and column *of that
  literal*. Splitting the text from the coordinates that describe it makes both harder to trust.
  (Corpus entries *can* carry forwarded front-end errors — the five `import-origin*` /
  `ergo-import-broken` fixtures do, and `Tier = FrontEndError` covers them — but those assert the
  diagnostic's *identity*, not offsets into a hand-counted string.)
- (f) the text is constructed, parameterized, or `[Theory]`-generated.

Applying the criteria to the survey: the ~193 literals in `NativeExpressionParseTests` (45),
`NativeExpressionOperatorTests` (42), `HtmlContextLintTests` (33), `NativeExpressionSandboxTests`
(27), `PropsParseTests` (25) and `TrimDirectiveLinesTests` (17) fall under (e)/(f) and stay inline.
The migration target is the feature-shape families: region, props, branch, branch-role, for,
profile-flip, extension-parameters, scope-participant, slot/default-output, C#-verbatim,
definition.

**Rationale.** Wholesale migration would move ~1,115 literals, produce an unreviewable diff, and
make several suites *worse* to read for no coverage gain — (e) and (f) templates gain nothing from
gauntlet membership because their subject is the front end, which both tiers already share
outright after phase 6. Selectivity is only defensible if the criteria are written down; otherwise
it is just taste, applied inconsistently.

**Alternatives rejected.** Wholesale ("consistency"): rejected above. Purely opportunistic
("migrate when you happen to touch a suite"): rejected — that is the discipline-based posture whose
failure this phase is fixing.

### D5 — The exact-count gate becomes set equality against the declared table

**Decision.** `EveryPrecompiledCorpusEntryCrossesTheGauntlet` today pins
`precompiledKeys.Count == 40`. That literal is replaced by **set equality against D3's table**:

```
observed precompiled set  ==  { e : table[e].Tier == Precompiles }
```

reported as a symmetric difference naming the drifting files. Alongside it, **one literal remains**:
the table's own row count, asserted once. So a migration stage changes the pinned number by adding
rows, and each added row carries a filename, a `Tier`, a `Render` and a `Why`.

**Rationale.** The count was already the right *instinct* — phase 0 replaced a `>= 25` floor
(against an actual 40, letting fifteen templates stop precompiling silently) with an exact count for
exactly this reason. But an exact count still has the rubber-stamp failure mode: a stage that
changes classification can be made green by editing one digit, and the commit looks the same either
way. Set equality cannot be rubber-stamped, because making it green requires naming the specific
file whose classification changed and writing down why — which is precisely the review artifact the
count was trying to force. It is also a strictly better failure message: "`props-inherit.heddle`
stopped precompiling" instead of "expected 40, got 39".

The remaining literal (row count) is deliberate: it makes "this stage added 14 entries" a one-line
diff a reviewer can check against the stage's stated scope, so a stage cannot smuggle in extra
templates.

**Alternatives rejected.** Keeping the bare count (rubber-stampable, poor message). Deriving
everything with no literal at all (a stage could add rows unnoticed). A floor of any kind
(the anti-pattern, twice demonstrated in this tree).

### D6 — Suite-time budget: the sweep stays under 6 s per TFM, measured per stage

**Decision.** Today's measured cost (phase 0's WI4 record, `net10.0`, Debug, second of two
consecutive runs): `CorpusResolverSweepTests` ≈ **1.7 s** for 3 tests over 40 precompiling entries,
beside ≈ 1.0 s (`CorpusDifferentialTests`) + ≈ 3.1 s (`CorpusRenderParityTests`). The structure is
one generator run + one registration amortized across the sweep, plus roughly one `TryResolve` per
entry — call it ~1.0 s fixed and ~15–20 ms marginal per entry.

Projection at the migrated size: the corpus grows 62 → **~130–150 files**, the precompiling set
40 → **~90–110**. Marginal cost ≈ 70 × 17 ms ≈ **1.2 s**, plus the renders D3 newly enables for the
`Standalone` set (today skipped wholesale by the blanket `render: false`) — bought deliberately,
and charged to stage 0 where it is measurable in isolation.

**Budget held to:** `CorpusResolverSweepTests` ≤ **6 s per TFM**, and the three corpus suites
together ≤ **15 s per TFM**. Each stage records a before/after measurement in its done-when. A stage
that breaches the budget lands a fix in the same change (sharding the sweep across `[Theory]` cases
so it parallelizes, or batching more targets per generator run) rather than relaxing the number.

**Measurement discipline.** Wall times from this workstation are not trustworthy in isolation —
phase 0's own record says so ("noisy workstation — treat as order-of-magnitude"). Stage measurements
follow the same procedure phase 0 used (per-TFM, second of two consecutive runs, reported as
order-of-magnitude), and the budget is a tripwire for *structural* cost growth, not a benchmark.

**Rationale.** The failure mode worth guarding is not "the suite got 2 s slower" but "the sweep
became quadratic and nobody noticed until it was three minutes". Amortizing the generator run is
what keeps it linear, and the budget's job is to make a regression in that structure visible.

### D7 — Goldens: they move because they are already siblings; snapshots do not; encoding gets a gate

**Decision.** Three separate rulings, because "the goldens" is three different things:

1. **Sibling output goldens move with their templates — by construction.** The 48 `.html` and 2
   `.txt` goldens already live *in the same directory* as the 62 templates
   (`generated-<name>.html` × 40, `generated.html`, `generated_mono.html`, plus the two public-API
   surface goldens). D1's glob is `templates/**/*`, so the sibling-pair convention that
   testing-standards already pins survives untouched. There is nothing to decide beyond stating it.
2. **`Heddle.Generator.Tests/Snapshots/*.verified.txt` (8 files) stay where they are.** Verify's
   file discovery is convention-bound to the test class's own source directory; a single consumer
   means there is no duplication to remove; and moving them would put Verify's `.received.txt`
   regeneration output into a directory three projects copy to their outputs. Zero benefit, real
   risk. (Verified: no `.received.txt` is checked in today, and all 8 `.verified.txt` are BOM-free.)
3. **Written artifacts leave the corpus.** Six of the 48 `.html` files —
   `test-<name>.html` × 5 and `test.html` — are **actual** output that tests *write* via
   `File.WriteAllText` and that are checked in beside the inputs. A shared corpus directory whose
   contents three projects copy to their outputs must not also be a directory tests write into: the
   invariant "the corpus is input" is what makes byte-neutrality checkable. These move to a
   sibling path excluded from the glob (or, preferably, the writes are dropped — they appear to be
   debugging aids, not assertions). **This is a finding, not a preference**; it is called out as
   WI4.

**Encoding is pinned by a gate, not by hope.** The pre-program baseline had inconsistent BOMs
across the eight generator snapshots (two had one, six did not) and the program normalised all to
no-BOM. Whatever this phase does must not silently re-introduce per-file encoding drift, so:

- Every root that moves is added to `.gitattributes`' `text eol=lf` list **in the same change**
  (already the standing rule; `src/Heddle.Tests/TestTemplate/**` is pinned today, and the new root
  replaces it in that list).
- A new **encoding gate test** over the corpus directory asserts: no `.html`/`.txt` golden carries a
  BOM; every `.heddle` carries a BOM **iff** its intent row says `Bom = true`. Verified ground truth
  for the initial table: exactly **8 of 62** corpus templates carry a UTF-8 BOM (`template`,
  `partial`, `raw`, `empty-override`, `wierd-whitespace`, `recursion`, `optimized-document`,
  `dynamic-recursion`). Those BOMs are *deliberate coverage* — phase 5's F1 fix is about hashing
  BOM'd templates correctly — and today they are indistinguishable from accidents. The `Bom` flag
  makes them declared.
- **A related finding, worth fixing in the same phase:** `DifferentialHarness.StageCorpus` writes
  every staged entry as `new UTF8Encoding(false)` — no BOM, always. So the file-backed sub-mode,
  whose entire reason for existing is exercising `PrecompiledGauntlet.HashFile`'s
  decode-then-hash BOM path, **never stages a BOM'd file**, even though 8 corpus files have one.
  The declared `Bom` flag lets `StageCorpus` reproduce each entry's real encoding, which is what
  makes the file-backed pass actually cover phase 5 F1's shape (see WI5). The harness's own doc
  comment argues staging encoding is immaterial *because both sides hash decoded text* — true, and
  exactly why hashing decoded text is the fix; but a test that only ever stages the shape that
  works is not evidence that the other shape works.

**Alternatives rejected.** Moving the Verify snapshots into the shared corpus (no duplication to
remove; Verify regeneration hazard). Normalising all corpus templates to no-BOM (destroys the phase-5
regression coverage the BOMs provide). Leaving encoding to `.gitattributes` alone (it governs line
endings, not byte-order marks — the two are independent, and the BOM drift the program fixed would
have passed every `eol=lf` check).

### D8 — The principle becomes a testing-standards rule, via the amendments ledger

**Decision.** Land an additive *Test-input single-sourcing* section in
[testing-standards.md](../spec/common/testing-standards.md) through the amendments mechanism
([spec-conventions §Amendments](../spec/common/spec-conventions.md#amendments-during-implementation)),
with a ledger entry in [records.md](../spec/records.md#cross-spec-amendments-ledger) carrying the
evidence. Phase 0's WI8 / **E8** is the model in both form and placement. The rule states the
principle — *a template shape that two tiers verify exists once, in the shared corpus, with a
declared intent* — plus the intent-declaration requirement, the keep-inline carve-out, and the
set-equality gate.

**Rationale.** A plan's motivation expires when the plan is marked implemented. The reason phase
0's corpus-contribution rule is still unbackfilled is that it was recorded as prose in a standard
with no mechanism behind it. This phase supplies the mechanism (D1–D3, D5); the amendment is what
makes the *principle* survive as a standing rule that future specs inherit rather than
re-litigate. It is additive — no earlier decision is overturned; E8's third bullet is *sharpened*
by gaining an enforceable mechanism.

### D9 — Byte-neutrality is the acceptance gate for every stage, exactly as extraction was

**Decision.** Every stage carries the same done-when the program's extraction WIs carried: the
golden corpus, the differential suites, the Verify snapshots and the resolver sweep are **unchanged
before and after**, with zero fallback events. Where a stage moves a template's text, the move is
proven byte-identical (a `git mv`-shaped diff plus a hash comparison of the extracted literal
against the new file); where a stage merges two tiers' near-identical copies, **any difference
between them is resolved explicitly and recorded** — a diff between the two copies is either a
drift finding (report it) or an intentional variation (then they are two entries, not one).

**Rationale.** This is the program's own convention, and it is the only way a ~90-template
migration stays reviewable. It also converts the migration into a **drift audit**: every merge of
two copies is an opportunity to discover that they had already diverged. The 18 known literal
duplicates currently agree; whether the *near*-duplicates do is unknown, and the merge is what
finds out.

## Dependencies & ordering

- **Depends on phase 0** for the posture, `FallbackGuard`, `RenderViaResolver`/`SweepViaResolver`,
  and the exact-count gate this phase generalizes. Depends on phases 1–6 being landed only in the
  sense that their byte-neutrality gates are the invariant D9 measures against.
- **Nothing depends on this phase**, so it can land at any time — but it should land *after* the
  in-flight phase-2 fixture audit, whose territory includes `src/Heddle.Tests/TestTemplate/`. Two
  efforts restructuring the same directory concurrently is a merge conflict by construction.
- **Internal ordering is strictly sequential** by stage (WI1 → WI9). Stage 0 (WI1–WI6) is
  infrastructure and must be complete and green before any template text moves, because the intent
  table and the set-equality gate are what make the later stages reviewable.

## Back-compat / impact

- **Engine surface:** none. No source file under `src/Heddle/**` is touched.
- **Rendered bytes:** none, by D9's gate.
- **Public API:** none.
- **Build:** each consuming test csproj gains one `<Import>` line; `Heddle.Tests.csproj` loses ~112
  hand-listed `<None Update>` rows. `.gitattributes` gains the new root and loses the old one.
- **TFM legs this box cannot verify.** `net6.0` cannot run here (SDK absent — the leg aborts with
  `MSB4181`) and `net48` is `Condition="'$(OS)' == 'Windows_NT'"`. The `Content`/`Link`/glob
  mechanism is TFM-independent MSBuild, and copy-to-output is the same machinery
  `Heddle.Tests.csproj` uses today, so no *design* element depends on those legs. But the claim
  "the corpus reaches every TFM's output directory" is verified here only for `net8.0`/`net10.0`
  and must be re-confirmed on a Windows checkout before the phase is called done.
- **Not window-relevant** ([breaking-windows](../spec/common/breaking-windows.md)): test-asset and
  test-wiring work with no observable behavior change.

## Risks & mitigations

- **The corpus becomes a dumping ground.** Mitigation: D4's written migrate/keep-inline criteria,
  D3's mandatory non-empty `Why`, and D5's row-count literal that makes each stage's additions a
  one-line reviewable diff.
- **Readability regression** — a template one file away from its assertion. Mitigation: (d)/(e)/(f)
  keep the cases where proximity genuinely wins; a migrated test names its corpus key in its test
  name or XML doc comment so the shape is one grep away.
- **A shared file edited for one tier breaks the other.** This is not a risk — it is the deliverable.
  Mitigation is only that the failure be legible: D9's per-stage byte-neutrality gate plus the
  intent row make a cross-tier break point at the template, not at plumbing.
- **Merge conflict with the in-flight phase-2 fixture audit.** Mitigation: sequencing (above); this
  phase is authored now and executed after that audit lands.
- **Intent-table rot** (rows outliving their templates, or vice versa). Mitigation: the two
  bidirectional completeness gates in D3 — neither direction can drift silently.
- **Stage-0 render enablement inflates suite time before any migration value lands.** Mitigation:
  D6 charges it to stage 0 explicitly and measures it there in isolation, so the cost is attributed
  correctly rather than blamed on a later stage.
- **A migrated deliberate-degrade shape quietly starts precompiling** (the sweep's premise
  breaking). Mitigation: `Tier = DegradesToMarker` / `FallsBackSafely` is asserted positively, not
  by exclusion — D5's set equality is symmetric, so a template *joining* the precompiled set reddens
  the gate just as loudly as one leaving it.

## Success criteria

1. **One physical copy.** No `.heddle` file or output golden used by more than one test project
   exists at more than one path in the tree, and no test project's csproj hand-lists corpus files.
2. **The traversal is gone.** No test file contains an assembly-path rewrite or a `../../..` climb
   to reach template assets; no corpus test contains a `dir == null` early-return or its
   replacement assert; grep for `HeddleTestsDll` returns nothing.
3. **Intent is total and bidirectional.** Every corpus `.heddle` has exactly one intent row with a
   non-empty `Why`; every row names an existing file; both directions are asserted, and the
   assertion names offending files.
4. **The gate is set equality, not a count.** The observed precompiled set equals the table-derived
   set, reported as a symmetric difference. Exactly one literal number remains (the table's row
   count). The stale `>= 40` / "~45 files" floor is deleted.
5. **The 18 known cross-tier literal duplicates are single-sourced**, and any divergence discovered
   while merging them is recorded (as a drift finding or as a deliberate two-entry split).
6. **Coverage strictly increased, provably.** The count of precompiling corpus entries crossing the
   gauntlet rises from 40 to the migrated figure; the `Standalone` set is byte-compared in the
   sweep (which the blanket `render: false` prevented today); and the file-backed pass stages each
   entry at its declared encoding, so at least one BOM'd template crosses
   `PrecompiledGauntlet.HashFile` under staleness checking.
7. **Encoding cannot drift.** The encoding gate passes; `.gitattributes` covers the new root;
   BOM-bearing entries are exactly those declared `Bom = true` (8 at the start).
8. **Byte-neutral throughout.** Every stage leaves goldens, snapshots, differential suites and the
   sweep unchanged, with zero unexpected fallback events, on every TFM leg that runs.
9. **Within budget.** `CorpusResolverSweepTests` ≤ 6 s per TFM and the three corpus suites ≤ 15 s
   per TFM, measured per stage under D6's procedure.
10. **The standing rule exists.** The testing-standards amendment and its ledger entry are landed,
    and phase 0's D4 correction block points at this phase as the residue's owner.

### Outcomes (2026-07-26)

| # | Verdict | Evidence |
| --- | --- | --- |
| 1 | **met** | No corpus file exists at two paths; no csproj hand-lists corpus files (112 rows → one glob). |
| 2 | **met** | `grep -r HeddleTestsDll` empty; no assembly-path rewrite or `../../..` climb for assets or for the models DLL; all five `dir == null` returns and their replacement asserts deleted. |
| 3 | **met** | Both directions asserted in both tiers; rehearsed — adding `zz-probe.heddle` fails naming `zz-probe.heddle`. |
| 4 | **met** | Four count gates replaced by symmetric-difference set equality (one more than the plan found — see the record). Exactly one literal remains. Rehearsed by mutating the *table* rather than the code: still red, still names the file. |
| 5 | **not met — stopped with cause** | The 18 do not agree as whole templates. See *Why stages 1–5 stopped*; cost recorded as **Q8.42**. |
| 6 | **partially met** | Byte-compared standalone renders **10 → 32** (F2); the file-backed pass stages each entry at its declared encoding and a real BOM'd corpus template now crosses `HashFile` (F3, rehearsed). The precompiled figure stays **40**, because no migration ran. |
| 7 | **met** | Encoding gate green; `.gitattributes` covers the new `TestOutput/` root and now states that `eol=lf` does not govern BOMs; BOM-bearing entries are exactly the 8 declared. Rehearsed both directions. |
| 8 | **met** | Every leg green on every TFM that runs here; goldens, Verify snapshots, differential suites and the sweep unchanged; zero fallback events. |
| 9 | **met** | The three corpus suites total ≈ 4 s per TFM (baseline ≈ 5.8 s), against a 15 s budget — *lower* than before despite 22 more byte-compared renders, because the sweep's three generator runs now share one corpus load. |
| 10 | **met** | The amendment and ledger E9 were authored ahead of execution and needed no change; the README row and phase-0 pointer are updated. |

**Not verifiable on this box:** the `net6.0` and Windows `net48` legs. The `Content`/`Link`/glob
mechanism is TFM-independent MSBuild, so no design element depends on them, but the claim "the corpus
reaches every TFM's output directory" is confirmed here only for `net8.0`/`net10.0` and still needs a
Windows checkout before the phase is called done.

## Implementation record (2026-07-26)

Executed against `1b0ee64`. Baseline and post-stage-0 suite counts, per TFM leg that runs on this box
(`net6.0` has no runtime installed; `net48` is Windows-only):

| Leg | Before | After | Delta |
| --- | --- | --- | --- |
| `Heddle.Tests` net8.0 / net10.0 | 1810 / 1810 | 1817 / 1817 | +7 (the D3/D7 gates) |
| `Heddle.Generator.IntegrationTests` net8.0 / net10.0 | 406 / 406 | 407 / 407 | +1 (the declared-BOM-in-sweep gate) |
| `Heddle.Generator.Tests` net8.0 / net10.0 | 448 / 448 | 448 / 448 | 0 (not a corpus consumer — WI1 scopes two projects) |

All legs green, zero fallback events, goldens and Verify snapshots untouched (D9).

### What stage 0 changed

WI1 — `src/TestCorpus/TestCorpus.props`, imported by `Heddle.Tests.csproj` and
`Heddle.Generator.IntegrationTests.csproj`. `src/TestCorpus/` holds **wiring only**; the templates stay
in `src/Heddle.Tests/TestTemplate/` (D1 as revised), and the directory belongs to no project precisely
so no csproj's default glob reaches the shared accessor sources and all consumers import on identical
terms. The 112 hand-listed `<None Update>` rows are one glob. **Audited at the swap: the hand-list and
the directory agreed exactly, 112 to 112** — it had not drifted yet, but nothing was preventing it.

WI2 — `CorpusDir` / `HeddleTestsDll` / `LoadCorpus` / the fixture-name `HashSet`, each triplicated
across the three corpus suites, are one `TestCorpusIndex` + the intent table. `grep -r HeddleTestsDll`
is empty. Heddle.Tests.dll is now copied into the integration suite's own output
(`OutputItemType="Content"`), so the assembly lookup is `AppContext.BaseDirectory` too — the Q8.40
ruling permits the read, and this makes it fail loudly rather than return `null`. All five
`dir == null` early-returns and the asserts that replaced them are gone.

WI3 — `src/TestCorpus/CorpusIntent.cs`, 62 rows, both completeness gates, non-empty `Why` enforced.

WI4 — the six checked-in written artifacts moved to `src/Heddle.Tests/TestOutput/` (preserved, per the
Q7.3 ruling), and **all 25 write sites across 14 files** were repointed to
`TestCorpusIndex.WrittenArtifactPath`, which writes to the writer's own output. The plan expected six
files; the six were only the ones that had been committed. The engine tier's output corpus directory
held **148 files against a tracked 106** — 36 accumulated test writes — which is what "the corpus is
input" was protecting and what the new `TheCorpusDirectoryHoldsNoTestWrittenArtifact` gate now holds.

WI5 — per-entry `Render` on `ResolverTarget`; `StageCorpus` honours declared `Bom`.

WI6 — set equality everywhere, symmetric-difference messages. **Four** count gates were replaced, not
one: `precompiledKeys.Count == 40` (sweep), `templates.Count == 62` (differential),
`Assert.Equal(62, files.Count)` in `ParticipantScanLockstepTests` — which the plan's survey missed —
and the `>= 40` / "~45 files" floor. Exactly one literal survives, `CorpusIntent.DeclaredRowCount`.

WI7 — the testing-standards amendment and ledger E9 were already authored ahead of execution and
needed no change; this record, the README row and the phase-0 pointer are the remainder.

### Findings

**F1 — `DegradesToMarker` has zero members, and the bucket was never asserted.** Every one of the 17
non-precompiling, non-error corpus entries is **`Absent`** from the manifest (a whole-template
degrade), not a `HED7014` marker. `CorpusDifferentialTests` computed a `markers` `SortedSet` and then
asserted nothing about it — a dead computation, which is why nobody noticed the bucket was empty. The
tier is kept (stage 4 was expected to populate it) and is now asserted **positively**: an empty set is
still a pinned set, so the first template that starts emitting a marker reddens something.

**F2 — the sweep was byte-comparing 10 entries where 32 were available.** Measured, not assumed: of
the 40 precompiling entries, **32 render standalone and byte-identically on both backends**. The
blanket `render: false` meant that because 8 entries genuinely cannot render standalone, all 40 lost
their byte assertion, and only the 10 hand-listed in `CorpusRenderParityTests` were compared. The
per-entry `Render` axis raises that to 32 with no new fixtures. Only **one** entry is genuinely
`ResolveOnly` (`branch-import-else.heddle`, a bare `@else` continuation); the other 7 are `WithModel`
(their `:: PropArticle` / `:: ErgoForData` short names cannot bind in a model-less standalone render).

**F3 — the BOM staging hole was real, and the rehearsal proves the new coverage.** With phase 5's
`HashFile` locally reverted to raw-byte hashing, the file-backed sweep now fails
`StaleContent` on `optimized-document.heddle` — a real corpus template. It did not before, because
`StageCorpus` wrote every entry BOM-free. **Correction to the plan's claim that "neither happens
today":** a purpose-built `QuarantinedDriftFixtures.BomTemplate_StaysOnThePrecompiledTier_UnderFileBackedStaleness`
already covered the shape with a synthetic `drift-bom.heddle`, and it failed in the same rehearsal. So
the gain is not first-ever coverage; it is that the **real corpus's 8 BOM'd templates** now cross
`HashFile` instead of only a synthetic one.

### Why stages 1–5 stopped before starting

**Stage 1's premise does not survive contact with its own inputs.** The plan's External grounding
records "18 distinct template literals appear character-for-character in both `src/Heddle.Tests` and
the generator suites … Those 18 are the lucky ones — they still agree." Re-running that survey finds
31 shared literals over 14 characters, of which ~21 are template-ish. They agree **as substrings**.
The whole templates the two tiers actually compile do **not** agree, and cannot be single-sourced
without work this plan never scoped:

- **Region family (13 of the ~21) — blocked on a fixture-model unification.** The two tiers' flagship
  `Feed` prelude has already diverged three ways: the runtime writes `:: RegionFeedModel` and
  `:: RegionArticle` (bare short names) where the generator writes
  `:: Heddle.Generator.IntegrationTests.Fixtures.RegionFeed` (full AQN) — **different type names, not
  just different spellings** — and the generator prepends a `@model(){{…}}@\` preamble plus a trailing
  newline the runtime has neither of. The fixture models themselves differ in shape too:
  `src/Heddle.Tests/RegionModels.cs` carries a `RegionSpecialArticle : RegionArticle` for the narrowing
  tests that `Fixtures/Models.cs` does not have, and `RegionFeed` is `sealed` where `RegionFeedModel`
  is not. **This is the drift D9 predicted the merge would find.** Single-sourcing the family requires
  first unifying those model types across two projects and then deciding whether the shared template
  spells its model as a bare short name or an AQN — which changes what the generator tier exercises
  (phase 3 F8's short-name binding path vs the AQN path), so it is a decision, not a rename.
- **`@out`, chained-definition and body-model families (the rest) — excluded by D4's own criteria.**
  On the generator side every one of these shared literals is an `[InlineData]` row, which D4 criterion
  **(f)** (*"the text is constructed, parameterized, or `[Theory]`-generated"*) says stays inline. The
  two tiers also feed them different inputs and assert different outputs: for `[@out(){{BODY}}]` the
  runtime supplies a *chained value* and expects `[CH]` while the generator supplies a model and
  expects `[]`. They are not two copies of one test; they are two different tests that share a
  substring.
- **The `DocumentShapingCharacterizationTests` pairs are not templates.** Nine of the 31 are expected-
  *output* vectors (`C:if=Opener|B:if=Opener|…`, `if@0+2|elif@2+2|else@6+2`) and one is a diagnostic
  message. Duplicated shared *vectors* are a real defect, but they belong to the already-solved
  `LineIndexVectors` / `DiagnosticCorpusVectors` linked-file pattern, not to a `.heddle` corpus.

So the honest accounting of stage 1's headline set is: **zero are migratable as authored.** Thirteen
need a cross-project fixture-model unification first; the remainder are either excluded by this plan's
own keep-inline criteria or are not templates at all. Proceeding anyway would have produced exactly the
failure this phase exists to remove — a half-migrated corpus with two homes and nothing forcing them to
agree. The cost is recorded as **Q8.42**; stages 2–5 inherit the same blocker, since they are the same
families at greater volume.

## Validation scenarios

- **Layout-change canary.** Rename a consuming project's output layout (or build a different
  configuration) → every corpus test still finds the corpus, because it reads its own
  `AppContext.BaseDirectory`. Pre-phase, this is the scenario that silently no-op'd five tests.
- **Unclassified template.** Add `probe.heddle` to the corpus with no intent row → the completeness
  gate fails naming `probe.heddle`. Add a row naming a non-existent file → the reverse gate fails.
- **Classification drift, both directions.** Locally make a `Precompiles` entry degrade (or make a
  `FallsBackSafely` entry precompile) → the symmetric-difference gate reddens and names the file.
  Mutating the table's `Tier` instead of the code produces an equally red, equally specific
  failure — the pin is not satisfiable by editing a digit.
- **Cross-tier single-source proof.** Take one migrated shape, edit the shared corpus file, and
  confirm **both** the `Heddle.Tests` test and the `Heddle.Generator.IntegrationTests` test change
  behavior in the same run. Pre-phase, editing one inline copy changed exactly one tier and
  reddened nothing else — that is the defect, demonstrated.
- **BOM under file-backed staleness.** With `Bom = true` honored by `StageCorpus`, a BOM'd corpus
  entry crosses the gauntlet under `EnableFileChangeCheck` → green (phase 5 F1's fix, exercised on
  the path it was written for). Revert phase 5's `HashText` to raw-byte hashing locally → it fails
  `StaleContent`. Today, neither happens, because no BOM ever reaches the staged tree.
- **Deliberate-degrade migration.** Migrate a `BranchRoleUniversalityTests` bodied-custom-branch
  shape with `Tier = DegradesToMarker` → the sweep's precompiled set is unchanged, the differential
  classification asserts the marker, and the entry is never rendered standalone.
- **Encoding gate.** Add a BOM to a corpus template without declaring it → the gate fails naming
  the file and the two disagreeing facts.

## Open questions

Recorded rather than defaulted, because each needs a maintainer ruling and none blocks stage 0:

**All four are resolved (user, 2026-07-26).** The register in
[open-questions.md](open-questions.md#post-implementation-questions-opened-2026-07-26) is
authoritative; the rulings are restated here because two of them changed this plan.

- **OQ7.1 — Does `src/Heddle.LanguageServices.Tests/Corpus` (3 `.heddle`) join?** Those templates
  serve editor-tier completion/hover/diagnostics, whose "renders correctly" axis is absent.
  **Ruling: keep them separate.** No `EditorOnly` tier; D3's three-value `Tier` axis stands
  unchanged. Revisit only if the editor tier needs a shape the corpus already has.
- **OQ7.2 — Do the benchmark and sample corpora converge, and should D1's props file serve
  `src/Heddle.Performance`?** **Ruling: leave `Heddle.Performance` alone entirely — change nothing
  within it.** A new benchmark effort is mid-flight there and must not be disturbed. The shared props
  file serves the four **test** projects only.
  **Accepted residue, recorded deliberately:** `TemplateParseBenchmarks.cs` keeps its own
  path-traversal helper walking up from the assembly location. So the failure class D2 eliminates is
  gone from the test suites but survives in the benchmark project — accepted, not overlooked, and
  revisited once the benchmark work settles. Do not "helpfully" fix it.
- **OQ7.3 — Delete or relocate the six checked-in written artifacts** (`test-<name>.html` × 5,
  `test.html`)? **Ruling: relocate, do not delete.** WI4 moves them outside the shared corpus glob
  and repoints the writing tests at the new location, so no file inside the glob is written by a
  test — but the artifacts themselves are preserved.
- **OQ7.4 — How far does the migration go past stage 3?** **Ruling: all stages, including 5, land
  inside this phase.** The D4 coverage residue is closed completely rather than left as a tail.
  WI9's stage 5 is promoted from conditional to committed; the open-ended scope is bounded by
  per-stage acceptance (byte-neutral gate plus suite-time measurement per stage), not by stopping
  early.

## External grounding

Verified against the tree at `45154fd` while planning (all counts re-checked, not taken from prior
docs):

- **Corpus:** `src/Heddle.Tests/TestTemplate/` — **62** `.heddle` files, flat (no subdirectories);
  **112** non-`.cs` files total = 62 `.heddle` + 48 `.html` + 2 `.txt`; **8** of the 62 carry a
  UTF-8 BOM. `Heddle.Tests.csproj` hand-lists all 112 as `<None Update … CopyToOutputDirectory=Always>`.
- **Precompiled classification:** `CorpusResolverSweepTests.EveryPrecompiledCorpusEntryCrossesTheGauntlet`
  pins `precompiledKeys.Count == 40`; `CorpusDifferentialTests.ExpectedPrecompiled` contains
  **exactly 40** filenames (re-counted programmatically — the two pins **agree**; an earlier survey
  pass reporting 39 was wrong). `ExpectedDiagnosticFixtures` holds 5. 62 − 5 = 57 classified;
  40 precompile, 17 do not.
- **Stale floor still in the tree:** `CorpusDifferentialTests` line ~157 —
  `Assert.True(templates.Count >= 40, "Expected the full TestTemplate corpus (~45 files).")`,
  against an actual 62.
- **Traversal:** `CorpusDir()` triplicated at `CorpusDifferentialTests.cs:105`,
  `CorpusResolverSweepTests.cs:55`, `CorpusRenderParityTests.cs:19`; `HeddleTestsDll()` rewrites
  `typeof(DifferentialHarness).Assembly.Location`, replacing `Heddle.Generator.IntegrationTests`
  with `Heddle.Tests`, then climbs `"..", "..", ".."` from `bin/<cfg>/<tfm>`. `LoadCorpus` and the
  diagnostic-fixture name set are likewise triplicated. `Heddle.Generator.IntegrationTests.csproj`
  has **no** corpus wiring of any kind.
- **Cross-tier literal duplication:** 18 distinct template literals (length > 14) appear
  character-for-character in both `src/Heddle.Tests` and the generator suites — concentrated in
  `RegionTests.cs` ↔ `Heddle.Generator.IntegrationTests/RegionTests.cs` (e.g.
  `@feed(theme: "dark"){{@%<heading:heading>{{<h2 class="@(theme)">@(title)</h2>}}%@}}`,
  `@panel(){{@%<head:head>{{[h:@foot()]}}<foot:foot>{{[f-filled]}}%@}}`) and
  `OutStaticBodyInertTests.cs` ↔ `OutStaticBodyFallbackTests.cs` (`[@out(){{BODY}}]`). A diagnostic
  *message* string is duplicated too (`"'@finish' is a branch terminal with no matching opener in
  this scope."`).
- **Inline-template inventory** (string literals containing Heddle directive syntax):
  `src/Heddle.Tests` ≈ **780** across 70 files (top: `OutputProfileEncodingTests` 48,
  `NativeExpressionParseTests` 45, `NativeExpressionOperatorTests` 42, `HtmlContextLintTests` 33,
  `BranchRoleUniversalityTests` 32, `RegionTests` 30, `BranchProtocolTests` 28);
  `src/Heddle.Generator.IntegrationTests` ≈ **300** across 45 files (`RegionTests` 36,
  `PropsTests` 21, `BranchTests` 14, `BranchRoleUniversalityTests` 13, `ForTests` 11,
  `ScopeParticipantDifferentialTests` 11, `ProfileFlipTests` 11,
  `ExtensionParametersDifferentialTests` 11); `src/Heddle.Generator.Tests` **35** across 9 files.
  Grand total ≈ **1,115**. (Heuristic lexing, so ±; the per-family ordering is the load-bearing
  part, and it matches the brief's ~32/~21/~12 for region/props/branch.)
- **Goldens:** `src/Heddle.Generator.Tests/Snapshots/` — exactly **8** `*.verified.txt`
  (`GeneratorSnapshotTests.Example1…Example7b`), no `.received.txt` checked in, none BOM'd.
  `TestTemplate/`'s 48 `.html` = `generated-<name>.html` × 40 + `generated.html` +
  `generated_mono.html` + `test-<name>.html` × 5 + `test.html`; the last six are **written** by
  tests via `File.WriteAllText`. The 2 `.txt` are `public-api-heddle.txt` /
  `public-api-heddle-language.txt`.
- **Harness seams:** `DifferentialHarness.StageCorpus` (`DifferentialHarness.cs:581`) writes each
  entry with `new UTF8Encoding(false)` into `Path.GetTempPath()/heddle-file-backed-<guid>`, sets
  `RootPath`/`FileNamePostfix`/`EnableFileChangeCheck`, deletes in `finally`;
  `PrecompiledGauntlet.HashFile` (`PrecompiledGauntlet.cs:203`) opens a `FileStream` and reads via
  `StreamReader(new UTF8Encoding(false), detectEncodingFromByteOrderMarks: true)` then
  `ContentHash.HashText`. `SweepViaResolver` passes a blanket `render: false` from
  `CorpusResolverSweepTests.cs:178`.
- **TFMs:** `Heddle` `netstandard2.0;net6.0;net8.0;net10.0`; `Heddle.Tests`
  `net6.0;net8.0;net10.0` (+`net48` on Windows); `Heddle.Generator.Tests` and
  `Heddle.Generator.IntegrationTests` `net8.0;net10.0`; `Heddle.LanguageServices.Tests`,
  `…Tests.Corpus`, `Heddle.Tool.Tests` `net10.0`.
- **Existing shared-test-code precedent:** `Heddle.Generator.Tests.csproj` links
  `..\Heddle.Tests\LineIndexVectors.cs`, `DiagnosticCorpusVectors.cs`,
  `PropDefaultConversionVectors.cs` as `Shared\…`. Shared *vectors* were solved this way; shared
  *templates* were not.
- **`.gitattributes`** already pins `*.heddle text eol=lf` plus `src/Heddle.Tests/TestTemplate/**`,
  `src/Heddle.Generator.Tests/Snapshots/**`, `src/Heddle.Generator.IntegrationTests/Fixtures/**`
  and `src/Heddle.LanguageServices.Tests/Corpus/**` as `text eol=lf`, with the standing rule that a
  new golden/fixture root is added in the same change.
- **Program context:** phase 0's [D4 correction block](phase-0-test-fallback-guardrails.md#d4--coverage-posture-every-corpus-entry-crosses-the-gauntlet-feature-suites-keep-isolation)
  and corrected criterion 2; the [README's post-implementation finding 7](README.md#post-implementation-review-findings-2026-07-26);
  the standing rule in [testing-standards §Precompiled-tier posture](../spec/common/testing-standards.md#precompiled-tier-posture)
  and its [ledger entry E8](../spec/records.md#cross-spec-amendments-ledger).

---

## Work items

**Stage 0 — infrastructure (WI1–WI6). No template text moves; byte-neutral; the count stays 40.**

- **WI1 — The shared home and the props file (D1).** Create `src/TestCorpus/` with
  `templates/` and `TestCorpus.props`; `git mv` the 62 `.heddle` + 44 read-only goldens in; add one
  `<Import>` to `Heddle.Tests.csproj` and `Heddle.Generator.IntegrationTests.csproj`; delete
  `Heddle.Tests.csproj`'s ~112 `<None Update>` rows; update `.gitattributes`. **Done when** the
  corpus appears under `TestTemplate/` in both projects' output directories on every TFM leg that
  runs, `git` records the moves as renames (no content change), and the full suite is green with
  no test-code edit beyond WI2's.
- **WI2 — Delete the traversal; one shared accessor (D2).** `CorpusDir()` → `AppContext.BaseDirectory`;
  fold the triplicated `CorpusDir`/`HeddleTestsDll`/`LoadCorpus`/fixture-set code into
  `src/TestCorpus/TestCorpusIndex.cs`, linked by the props file; delete the `dir == null`
  early-returns and their replacement asserts; delete the stale `>= 40` / "~45 files" floor.
  **Done when** criterion 2 holds, `grep -r HeddleTestsDll` is empty, and the layout-change canary
  (validation scenarios) passes.
- **WI3 — The intent table and its completeness gates (D3).** `src/TestCorpus/CorpusIntent.cs`:
  62 rows, each with `Tier`, `Render`, non-empty `Why`, `Bom`; the two bidirectional gates. The
  initial `Tier` values are transcribed from `ExpectedPrecompiled` (40), `ExpectedDiagnosticFixtures`
  (5) and the differential suite's marker/fallback classification (17), with each block comment in
  `CorpusDifferentialTests` becoming the relevant rows' `Why`. **Done when** criterion 3 holds and
  both unclassified-template scenarios fail with the filename in the message.
- **WI4 — Written artifacts leave the corpus (D7.3, OQ7.3 — ruled: *relocate*).** Move
  `test-<name>.html` × 5 and `test.html` out of the shared corpus tree, exclude the new path from
  the glob, and repoint the writing tests at it. They are **preserved, not deleted** — the ruling is
  relocation. **Done when** no file inside the shared corpus glob is written by any test, the
  artifacts still exist at their new location, and the suite is green.
- **WI5 — Per-entry render and per-entry staging encoding (D3 `Render`, D7 `Bom`).** Replace
  `SweepViaResolver`'s blanket `render: false` with the per-entry `Render` axis; teach
  `StageCorpus` to honor `Bom`. **Done when** the `Standalone` set is byte-compared in the sweep,
  at least one BOM'd entry crosses `HashFile` under `EnableFileChangeCheck`, the phase-5 revert
  rehearsal (validation scenarios) has been executed once, and D6's stage-0 measurement is recorded.
- **WI6 — Set-equality gate (D5).** Replace the `== 40` literal with symmetric-difference set
  equality against the table; keep exactly one literal (row count). **Done when** criterion 4 holds
  and the both-directions classification-drift scenario reddens with the filename named.
- **WI7 — Amendment + cross-references (D8).** The *Test-input single-sourcing* section in
  testing-standards, the ledger entry in `records.md`, the README phase row, and the pointer in
  phase 0's D4 correction block. **Done when** criterion 10 holds. *(Authored ahead of execution —
  see the Status line.)*

**Stages 1+ — migration. Each stage is one reviewable, byte-neutral landing under D9.**

- **WI8 — Stage 1: the 18 known cross-tier literal duplicates.** Region/props/`@out` families;
  both tiers' tests read the same corpus file. Every merge is diffed first and any divergence
  recorded. **Done when** criterion 5 holds, the cross-tier single-source proof (validation
  scenarios) passes on at least one migrated shape, and the stage's measurement is inside D6.
- **WI9 — Stages 2–5: the feature-shape families,** in this order, one landing each:
  **(2)** region remainder (integration 36 / runtime ~30 literals);
  **(3)** props + branch + branch-protocol;
  **(4)** branch-role universality — the stage that proves the taxonomy carries non-renderable
  shapes (mostly `DegradesToMarker`);
  **(5)** for / profile-flip / extension-parameters / scope-participant / slot-and-default-output /
  C#-verbatim / definition, bounded by D4's criteria. **Done when** each stage independently meets
  criteria 6, 8 and 9, and the final precompiled figure is recorded here beside the starting 40.
  *(Scope of stage 5 is subject to OQ7.4.)*
