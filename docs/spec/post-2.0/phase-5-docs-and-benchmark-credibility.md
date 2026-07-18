# Phase 5 — docs-and-benchmark-credibility (spec)

## Header

- **Status:** Specified — ready for implementation
- **Source plan:** [phase-5-docs-and-benchmark-credibility.md](../../plan/phase-5-docs-and-benchmark-credibility.md) (Plan DoR; P5-Q1..Q5 resolved in [open-questions.md](../../plan/open-questions.md))
- **Assumes merged:** nothing. This phase is editorial + additive-benchmark-harness only; it consumes no other phase's deliverable and can land in any order ([standing ruling R5](../../plan/common/standing-rulings.md#standing-rulings-user-set)).

| Document | Purpose |
|---|---|
| this document | Specifies both Phase 5 workstreams: (a) the single canonical version-status reconciliation across the six loci plus the dead-link retirement across the living spec docs; (b) the two new parity-checked benchmark workloads (trivial-substitution, large-loop) added to `src/Heddle.Performance`. |

This spec is self-contained given the [common specs](../common/spec-conventions.md); it restates the minimal context it needs. Source citations give `path` + member name; load-bearing line numbers carry their anchor and are marked *(verify at implementation — line numbers drift)*.

## Scope and goal

Close the two credibility gaps the plan names, touching **only** documentation prose and the benchmark harness under `src/Heddle.Performance` — **no engine, generator, runtime, public-API, grammar, or rendered-byte change** ([R5](../../plan/common/standing-rulings.md#standing-rulings-user-set)):

1. **One coherent version status.** Reconcile every doc that states or implies a version *toward* `2.0.0` as the current release line (the ratified P5-Q1 direction), via one canonical statement applied at six loci, worded so the docs are correct-and-consistent the moment the adjacent release action lands. The tag cut, package publish, and `CHANGELOG` `[Unreleased]`→`[2.0.0]` finalization are **flagged as that adjacent action — not performed here**.
2. **No dangling spec links.** Retire the four dead links in [docs/spec/README.md](../README.md), the one dead link in the living [cross-cutting-decisions.md](../common/cross-cutting-decisions.md) registry, and the two dead links discovered during verification in the living [breaking-windows.md](../common/breaking-windows.md) register, following the "specs retired — git history" precedent already in the index. The append-only [records.md](../records.md) receives only a **bounded correction** per maintainer standing rulings **SR-A/SR-B** (2026-07-15): its dead links to the four retired files are de-linked to plain text (the form its own header mandates) and its premature "2.0 window closed" phrasing is reconciled to window-open — every historical fact preserved (D4).
3. **Broader performance evidence.** Add a **trivial-substitution** workload and a **large-loop** workload to the benchmark suite, each parity-checked against the same four competitor engines as the existing composition page, published with committed BenchmarkDotNet reports and honest, workload-dependent framing.

**Out of scope** (plan non-goals): cutting the `v2.0.0` tag / publishing / finalizing the `CHANGELOG`; any engine/generator/runtime source; restructuring `ParityCheck`, the runner twins, or the reporting; adding competitor engines; altering any recorded *fact* in the append-only `records.md` (the SR-A/SR-B bounded correction only de-links dead URLs and corrects premature window-status phrasing — D4) or editing the dated `language-assessment.md`. This spec adds **no** `HED*` diagnostic and **no** public API.

## Assumed state

Re-verified against current source on branch `feature/lang_asmt`, 2026-07-13. Line anchors are *(verify at implementation)*.

### Version-status loci

| Seam | Verified state |
|---|---|
| `git tag -l` | Returns **`v1.0.0` and `v1.0.1` only** — no `v2.0.0` tag exists. |
| `CHANGELOG.md` release ledger | [CHANGELOG.md](../../../CHANGELOG.md) `## [Unreleased]` (≈L8-13): "The **2.0** release is being formed on this branch and has **not** shipped yet (the latest released tag is `v1.0.1`)… a single breaking window". Compare link (≈L113): `[Unreleased]: …/compare/v1.0.1...HEAD`. This is the release-ledger source whose finalization is the adjacent action (see [D2](#d2--changelog-tag-publish-are-the-flagged-adjacent-release-action-not-performed)). |
| Locus 1 | [docs/README.md](../../README.md) ≈L19: "Current version: **2.0.0**. The published version always follows the latest [release tag](https://github.com/multiarc/Heddle/releases) / [nuget.org](https://www.nuget.org/packages/Heddle)." — reads as *already released/published*; the only locus that materially over-claims. |
| Locus 2 | [building.md](../../building.md) ≈L38: "The current release line is **2.0.0**; the published version is set from the release tag (`vX.Y.Z`) at publish time, so the version in the source tree is just a placeholder." — **already the canonical form** (release-line + tag-driven publish + placeholder). |
| Locus 3 | [coming-from-liquid.md](../../coming-from-liquid.md) ≈L8: "verified to compile against **Heddle 2.0.0**". — a **compile-target** claim, not a release-status assertion. |
| Locus 4 | [coming-from-razor.md](../../coming-from-razor.md) ≈L8: "verified to compile against **Heddle 2.0.0**". — a **compile-target** claim. |
| Locus 5 | [built-in-extensions.md](../../built-in-extensions.md) ≈L660: "`WebUtility.HtmlEncode` behavior, **byte-identical to 2.0.0** (including its Latin-1 160–255 quirk)". — a **byte-baseline** reference to the 2.0.0 line. |
| Locus 6 | [csharp-api.md](../../csharp-api.md) ≈L193-195: `OutputProfile` "the default since 2.0"; `TrimDirectiveLines` "the default since 2.0"; `Encoder` "`WebUtility.HtmlEncode`, byte-identical to 2.0.0". — **defaults-introduced-in-2.0** references. |
| Not a locus (left as-is) | [spec/README.md](../README.md) ≈L31 (the **v2.0 nine-phase effort** row) already reads "Shipped in 2.0.0; specs retired" — consistent with the canonical statement. [records.md](../records.md) ≈L17/21/68 dated append-only 2.0 vocabulary — reconciled to *in-progress / window-open* by SR-B (D4), not policed by the version grep (whole `docs/spec/**` excluded). [language-assessment.md §2](../../language-assessment.md) is a dated research artifact (P5-Q5). |

### Dead-link loci (all four targets confirmed **absent from disk**)

Confirmed missing on disk: `docs/improvement-plan.md`, `docs/improvement-spec.md`, `docs/import-removal-plan.md`, `docs/import-removal-spec.md`.

| Living doc | Dead link(s) |
|---|---|
| [docs/spec/README.md](../README.md) ≈L28 ("Post-2.0 improvement effort" row) | `[Plan](../improvement-plan.md)`, `[Spec](../improvement-spec.md)` |
| [docs/spec/README.md](../README.md) ≈L30 ("Legacy `@import` removal" row) | `[Plan](../import-removal-plan.md)`, `[Spec](../import-removal-spec.md)` |
| [cross-cutting-decisions.md](../common/cross-cutting-decisions.md) ≈L159 (`HED4003` registry row, Owner cell) | `[import-removal-spec](../../import-removal-spec.md#7-diagnostics)` |
| [breaking-windows.md](../common/breaking-windows.md) ≈L43-44 (next-window candidate register) | `[Improvement-spec B2](../../improvement-spec.md#b2…)`, `[Improvement-spec B1](../../improvement-spec.md#b1…)` — **discovered during verification; not enumerated in the plan** (see [D3](#d3--dead-link-retirement-across-the-living-spec-docs-closes-p5-q3)). |
| [records.md](../records.md) ≈L42/47 **and ≈L104-107** (append-only) | `[improvement-spec B1/B2](../improvement-spec.md…)` at ≈L42/47, **plus** further dead `improvement-plan.md` / `improvement-spec.md` / `import-removal-spec.md` links in the amendments-ledger rows at ≈L104-107, **plus** a broken `#window-30…` anchor at ≈L104 — per **SR-A/SR-B** (D4) all are **de-linked to plain text / anchor-corrected in place**, preserving every recorded fact (this satisfies the file header's own "retired-doc citations are plain text for provenance" rule). |

### Benchmark harness

| Seam | Verified state |
|---|---|
| Parity gate | [`ParityCheck`](../../../src/Heddle.Performance/Runners/ParityCheck.cs) (`internal static`): `Twins()` yields four twins — Fluid/Scriban/DotLiquid/Handlebars (≈L16-22); `Assert()` normalizes both sides via `TwinContent.Normalize` and compares `Ordinal`, throwing on divergence (≈L25-35); `Report()` is the host-free `dotnet run … -- parity` harness (≈L38-58). |
| **Parity is byte-identical *after one documented normalization*, not raw** | [`TwinContent.Normalize`](../../../src/Heddle.Performance/Runners/TwinContent.cs) unifies `\r\n`/`\r`→`\n`, collapses every whitespace run **between two tags** (`>\s+<`→`><`), then trims (≈L87-95). **Correction to the plan/spike wording** ("byte-identical parity assertion"): the assertion is byte-identical *post-normalization*. Evidence: `TwinContent.Normalize`, and [Runners/README.md](../../../src/Heddle.Performance/Runners/README.md) "byte-for-byte after **one documented normalization**". The new workloads inherit this discipline (see [D6](#d6--parity-discipline-for-the-new-workloads)). |
| Parity-checked render benchmark | [`TextRenderBenchmarks`](../../../src/Heddle.Performance/TextRenderBenchmarks.cs) (`[MemoryDiagnoser]`): one `[Benchmark]` per engine, `RenderHeddle` is `[Benchmark(Baseline = true)]` (≈L57), and `[GlobalSetup]` calls `ParityCheck.Assert()` before any timing (≈L48). |
| Heddle-only render benchmark (precedent) | [`BranchRenderBenchmarks`](../../../src/Heddle.Performance/BranchRenderBenchmarks.cs) (`[MemoryDiagnoser]`): inline templates, typed models via `new CompileContext(typeof(ListModel))`, `@list(Items){{@(Name)}}` iterating a `List<Cell>` — the pattern the large-loop Heddle runner follows. |
| Twin runner shape | [`HeddleTest`](../../../src/Heddle.Performance/Runners/HeddleTest.cs) (oracle; file-based `TemplateOptions("home")` + `RootPath="TestTemplates"`), [`FluidTest`](../../../src/Heddle.Performance/Runners/FluidTest.cs)/[`ScribanTest`](../../../src/Heddle.Performance/Runners/ScribanTest.cs)/[`DotLiquidTest`](../../../src/Heddle.Performance/Runners/DotLiquidTest.cs)/[`HandlebarsTest`](../../../src/Heddle.Performance/Runners/HandlebarsTest.cs) each expose `string Render()`; Fluid+DotLiquid share one Liquid source ([`LiquidTemplates`](../../../src/Heddle.Performance/Runners/LiquidTemplates.cs)); engine-neutral content lives in [`TwinContent`](../../../src/Heddle.Performance/Runners/TwinContent.cs). |
| Engine ctors this phase binds to | `CompileContext(TemplateOptions options, ExType modelType = null)` ([CompileContext.cs](../../../src/Heddle/Runtime/CompileContext.cs) ≈L142); `ExType` has `implicit operator ExType(Type)` ([ExType.cs](../../../src/Heddle/Data/ExType.cs) ≈L41), so `new CompileContext(options, typeof(T))` is valid. `OutputProfile.Text = 0` (raw) / `Html = 1` ([OutputProfile.cs](../../../src/Heddle/Data/OutputProfile.cs)); `ExpressionMode.Native` is the default tier. `@list` is [`ListExtension`](../../../src/Heddle/Extensions/ListExtension.cs). |
| Fixtures + LF pinning | [TestTemplates/](../../../src/Heddle.Performance/TestTemplates) holds `home.heddle` + `layout.heddle`. All `*.heddle` files are already LF-pinned by the **global** rule [`.gitattributes`](../../../.gitattributes) L7 (`*.heddle text eol=lf`); new `.heddle` fixtures are covered without a new `.gitattributes` root. `docs/benchmarks/2026-07-11/` carries no `eol=lf` pin (its md/csv/html are generated artifacts, not byte-asserted goldens). |
| Committed run | [docs/benchmarks/2026-07-11/](../../benchmarks/2026-07-11) holds `TextRenderBenchmarks` + `TemplateParseBenchmarks` reports (md/csv/html) + `index.md`; the formatted render/compile tables live in the [README Performance section](../../../README.md#performance). |
| Program entry | [`Program.Main`](../../../src/Heddle.Performance/Program.cs): `-- parity` runs the host-free `ParityCheck.Report()` and exits `0/1`; otherwise `BenchmarkSwitcher.FromAssembly(...).Run(args)` (filterable) or defaults to `TextRenderBenchmarks`. |

**Plan corrections found.** Two, both recorded above and folded into decisions: (1) parity is byte-identical *after normalization*, not raw (D6); (2) `breaking-windows.md` L43-44 carries the same dead `improvement-spec.md` links and is in the same living-doc class as `cross-cutting-decisions.md`, so it is included in the dead-link fix (D3). Every other plan claim (the six loci, the four absent targets, the harness shape) was confirmed exactly.

## Design decisions

Every P5 open question (Q1–Q5) appears here as a closed decision.

### D1 — Canonical version-status statement and the six-locus disposition (closes P5-Q1)

- **Decision.** Adopt **one** canonical version-status statement (CVS) and apply it so every living doc asserting or implying a version is consistent with `2.0.0` as the current release line:

  > **CVS.** *The current release line is **2.0.0**; the published version is set from the release tag (`vX.Y.Z`) at publish time, so the version in the source tree is just a placeholder.*

  The CVS is [building.md](../../building.md) ≈L38 **verbatim** — building.md is **already** this form and is the single in-tree anchor, left unchanged. There is one canonical wording; the remaining loci are dispositioned to be *consistent with* it (not byte-identical — a locus may keep its own links/framing so long as it does not contradict the CVS):

  | Locus | Edit / rule |
  |---|---|
  | **1 — docs/README.md ≈L19** | **Edit.** Replace `Current version: **2.0.0**. The published version always follows the latest [release tag](https://github.com/multiarc/Heddle/releases) / [nuget.org](https://www.nuget.org/packages/Heddle).` with: `Current release line: **2.0.0**. The published version is set from the latest [release tag](https://github.com/multiarc/Heddle/releases) (`vX.Y.Z`) at publish time — see [nuget.org](https://www.nuget.org/packages/Heddle) — so the version in the source tree is just a placeholder.` (**consistent with** the CVS / building.md ≈L38 — not byte-identical, because it keeps docs/README.md's release-tag and nuget.org links; removes the "already released/published" over-claim). |
  | **2 — building.md ≈L38** | **No edit** — already the canonical form; it is the CVS anchor. |
  | **3 — coming-from-liquid.md ≈L8** | **No edit.** "verified to compile against **Heddle 2.0.0**" is a compile-target statement, consistent with 2.0.0 being the current line. |
  | **4 — coming-from-razor.md ≈L8** | **No edit** — same rule as locus 3. |
  | **5 — built-in-extensions.md ≈L660** | **No edit.** "byte-identical to 2.0.0" is a byte-baseline reference to the 2.0.0 line, consistent. |
  | **6 — csharp-api.md ≈L193-195** | **No edit.** "the default since 2.0" / "byte-identical to 2.0.0" are defaults-introduced-in-2.0 references, consistent. |

- **Rationale.** The ratified P5-Q1 flip is *toward* `2.0.0`, so the many docs that already say 2.0.0 become correct-and-consistent rather than being softened. Under the flip, only locus 1 materially over-claims (it reads as tagged/published *now*); the other five are compile-target or byte-baseline references that are already consistent. Anchoring the CVS on building.md's existing wording minimizes churn and gives one in-tree source of truth.
- **Alternatives rejected.** *Reconcile toward "unreleased"* — the superseded lean; would force edits to all six loci and contradicts the user's confirmation that 2.0 is imminent. *Rewrite all six to a new bespoke sentence* — needless churn; building.md L38 is already ideal, so the others need only to not contradict it. *Also edit the CHANGELOG "not shipped yet" line here* — that is a release act, deferred to D2.
- **Grounding.** [Assumed state → version-status loci](#version-status-loci); [open-questions P5-Q1](../../plan/open-questions.md); plan [Design direction](../../plan/phase-5-docs-and-benchmark-credibility.md).

### D2 — CHANGELOG, tag, publish are the flagged adjacent release action (not performed)

- **Decision.** This phase does **not** cut `v2.0.0`, publish the package, or edit the `CHANGELOG` body. It records — as a prominent note in the reconciliation work item — the exact adjacent action that finalizes the release: (a) cut the `v2.0.0` git tag; (b) publish `Heddle`/`Heddle.Generator` to nuget.org; (c) in [CHANGELOG.md](../../../CHANGELOG.md), move the `## [Unreleased]` section to a dated `## [2.0.0] — <date>` entry (dropping the "has **not** shipped yet" sentence) and update the compare footer from `[Unreleased]: …/compare/v1.0.1...HEAD` to `[2.0.0]: …/compare/v1.0.1...v2.0.0`. The docs' grep gate ([Testing plan](#testing-plan)) treats the CHANGELOG's `[Unreleased]` block as the expected pre-release state, closed by this adjacent action — not as a living-doc contradiction to fix here.
- **Rationale.** R5 forbids a release-tag change in this phase; SD-D scopes the docs to be *ready and internally consistent for* 2.0.0 so they are not published asserting a tag that does not yet exist. Naming the three-part action inline makes it unmissable when release lands.
- **Alternatives rejected.** *Finalize the CHANGELOG here* — a release act outside R5. *Leave the action unnamed* — risks the docs asserting a release line whose tag never gets cut (plan risk row 1).
- **Grounding.** [R5](../../plan/common/standing-rulings.md#standing-rulings-user-set); [CHANGELOG.md](../../../CHANGELOG.md) L8-13/L113; plan [Non-goals](../../plan/phase-5-docs-and-benchmark-credibility.md).

### D3 — Dead-link retirement across the living spec docs (closes P5-Q3)

- **Decision.** Repoint every dead link to a retired plan/spec as **plain text with a "(retired)" qualifier — never linked** (the [spec-conventions writing-style rule](../common/spec-conventions.md#writing-style)), matching the "specs retired (git history only)" treatment already on the **v2.0 nine-phase effort** row of [spec/README.md](../README.md) ≈L31. Concretely:
  - **spec/README.md ≈L28** ("Post-2.0 improvement effort"): drop the `[Plan](../improvement-plan.md)`/`[Spec](../improvement-spec.md)` links; render the plan/spec as plain text `improvement-plan.md` / `improvement-spec.md` (retired — git history), keeping the still-resolving `[Assessment](../../language-assessment.md)` and `[amendments ledger](records.md#cross-spec-amendments-ledger)` links. **Status:** the ≈L31 v2.0 row is the *link-retirement* precedent, but do **not** copy its "Shipped in 2.0.0" wording verbatim onto this row — this is a **separate, later** effort whose [2.0 window record](../records.md#the-20-breaking-window--as-shipped-record) documents that two of its items slipped 2.0.0 and re-land additively in 2.1 (records.md ≈L104; note the source has since gained B1's `[Obsolete]` on `AllowCSharp` and B2's `TemplateOptions.Encoder` — [TemplateOptions.cs](../../../src/Heddle/Data/TemplateOptions.cs) ≈L28/L89). Set the status to **"Specs retired — git history; shipped into the 2.0 window (two items re-land additively in 2.1)"** and add a resolving `[2.0 window record](../records.md#the-20-breaking-window--as-shipped-record)` link as its grounding. (There is **no** `[2.0 window record]` link on this row today — it lives on the ≈L31 v2.0 row; this adds one, it does not "preserve" an existing one.) This retires the dead links without over- or under-claiming shipment.
  - **spec/README.md ≈L30** ("Legacy `@import` removal"): drop the `[Plan](../import-removal-plan.md)`/`[Spec](../import-removal-spec.md)` links; render them as plain text `import-removal-plan.md` / `import-removal-spec.md` (retired — git history); status "Shipped in 2.0.0; specs retired" (its `HED4003` residue shipped — [CHANGELOG](../../../CHANGELOG.md) "Removed" section); keep the `[amendments ledger](records.md#cross-spec-amendments-ledger)` link.
  - **cross-cutting-decisions.md ≈L159** (`HED4003` Owner cell): replace `[import-removal-spec](../../import-removal-spec.md#7-diagnostics)` with plain text `@import removal (spec retired — git history)`; the Notes cell already links the resolving `[amendments ledger](../records.md#cross-spec-amendments-ledger)`, which becomes the live pointer for the removed spec's detail.
  - **breaking-windows.md ≈L43-44** (next-window candidate register, *discovered correction*): replace `[Improvement-spec B2](../../improvement-spec.md#b2…)` and `[Improvement-spec B1](../../improvement-spec.md#b1…)` with plain text `improvement-spec B2` / `improvement-spec B1` (retired — git history), keeping the resolving `[2.0 record](../records.md#the-20-breaking-window--as-shipped-record)` link in each owning-record cell.
- **Rationale.** All four target files are absent; the index already models the retirement treatment on L30, so the fix makes the living docs internally consistent with how they already describe retired specs. `breaking-windows.md` is the same living-common-spec class as `cross-cutting-decisions.md` (not append-only), so leaving its dead links would be the same defect the plan fixes elsewhere.
- **Alternatives rejected.** *Recreate the deleted files* — out of scope and reverses the `Clean-up` commit's intent. *Leave breaking-windows.md dead links (plan enumerated only two files)* — a known dead link in a living doc is a defect; the plan's success criterion "no markdown link to a nonexistent file" is honored by fixing all living occurrences. *Leave records.md's dead links untouched* — superseded by SR-A: they are de-linked to plain text in place (facts preserved) per D4, resolving the append-only-vs-no-dangling-links tension the maintainer ruled on.
- **Grounding.** [Assumed state → dead-link loci](#dead-link-loci-all-four-targets-confirmed-absent-from-disk); [spec-conventions](../common/spec-conventions.md#writing-style); [open-questions P5-Q3](../../plan/open-questions.md).

### D4 — records.md bounded-correction (SR-A/SR-B); assessment untouched (closes P5-Q4, P5-Q5)

- **Decision.** Make **no** edit to [language-assessment.md](../../language-assessment.md) §2 (P5-Q5) — the grep and link-check gates exclude it. records.md was originally left fully untouched under P5-Q4; that stance is **superseded in part** by maintainer standing rulings **SR-A** and **SR-B** (2026-07-15) for a **bounded correction** that preserves every historical fact:
  - **SR-A (de-link).** [records.md](../records.md)'s dead links to the four retired plan/spec files (`improvement-plan.md` / `improvement-spec.md` / `import-removal-plan.md` / `import-removal-spec.md`) at ≈L42/47 and in the amendments-ledger rows ≈L104-107 are converted to the plain-text "(retired — git history)" form the file's **own header already mandates** ("citations are kept as plain text for provenance"). No fact is altered — only dead URLs become plain text.
  - **SR-B (window status).** The premature "released 2.0.0 / byte-changing window **closed**" phrasing (the as-shipped intro ≈L17 and the ledger disposition at ≈L49-52/≈L104) is corrected so the record reads as **in-progress / 2.0 window open** — consistent with the record's own ledger row that already states there is "no `v2.0.0` tag … the open, not-yet-released 2.0 major breaking window". The default-swap deferral is re-grounded on its unmet precondition (a soaked `Encoder` pin), not window closure.
  - The **link-check gate now includes records.md** (all its links must resolve — the broken `#window-30…` anchor at ≈L104 is also corrected to `#next-window-candidate-register`). The **version-consistency grep** still excludes the whole `docs/spec/**` set (records.md included), so its dated 2.0 vocabulary is not policed as a product assertion.
- **Rationale.** Append-only forbids deleting or altering recorded *facts*; it does not forbid repairing a dead link into the plain-text citation the header already requires, nor correcting a *premature* statement to match reality (no tag cut). SR-A/SR-B are the maintainer's explicit resolution of the records.md append-only-vs-no-dangling-links and window-status tensions (recorded as [OQ2/OQ3](OPEN-QUESTIONS.md)). The assessment stays a dated point-in-time research artifact whose flagged §2 inconsistency is resolved in the *living* docs.
- **Alternatives rejected.** *Leave records.md fully untouched* — leaves 6+ dead links (violating [spec-conventions](../common/spec-conventions.md#writing-style)) and a doc claiming the 2.0 window closed, which blocks Phase 6's open-window premise. *Recreate the deleted files* — reverses the `Clean-up` commit (SR-A rejects). *Edit the assessment §2 note* — rewrites history, excluded by P5-Q5.
- **Grounding.** [records.md header](../records.md); [SR-A/SR-B → OQ2/OQ3](OPEN-QUESTIONS.md); [open-questions P5-Q4/Q5](../../plan/open-questions.md).

### D5 — Two new parity-checked benchmark workloads (closes P5-Q2)

- **Decision.** Add two workloads that bracket the existing composition page, each **parity-checked against all four competitor engines** (Fluid, Scriban, DotLiquid, Handlebars), reusing the precedented twin/parity/report shape without restructuring `ParityCheck`:
  - **Trivial-substitution** (`trivial-substitution.heddle`): one flat card whose output is dominated by ~10 scalar member substitutions with minimal literal glue and **no** composition (no layout, components, or loop) — the shape the assessment predicts narrows or inverts Heddle's advantage.
  - **Large-loop** (`large-loop.heddle`): a single `@list` over **5,000** rows, each emitting two scalar members — output dominated by one large iteration.

  Both Heddle runners render **raw** (`OutputProfile.Text`) under `ExpressionMode.Native`, matching the raw-rendering competitor twins (Scriban/DotLiquid do not encode; Fluid's default raw encoder; Handlebars triple-mustache), so the comparison measures templating, not encoding. Fixture stems (`trivial-substitution`, `large-loop`) are initiative-recognizable per the [testing standard](../common/testing-standards.md#fixtures-and-goldens). Full file/class inventory in [WI5–WI8](#implementation-plan).
- **Rationale.** P5-Q2 chose parity for both workloads (thoroughness over the cheaper Heddle-only). Bracketing the composition page (substitution below, loop above) is exactly the breadth the assessment asks for; `Text`/`Native` keeps the twins parity-clean and the numbers about templating cost.
- **Alternatives rejected.** *Heddle-only (the plan's cost-lean fallback)* — parity is the credibility move and the harness already accommodates it; recorded as the fallback if authoring the eight twins proves prohibitive (see [Deferred items](#deferred-items)). *`FullCSharp` expression mode* — would fold Roslyn cost into a micro-benchmark whose point is scalar substitution. *`OutputProfile.Html`* — would inject HTML-encoding cost and force encoding-parity gymnastics across engines. *Inline template strings (à la `BranchRenderBenchmarks`)* — the plan requires `.heddle` **fixtures** under `TestTemplates/`.
- **Grounding.** [assessment §5 obs 2 / §8 item 5](../../language-assessment.md); [BranchRenderBenchmarks](../../../src/Heddle.Performance/BranchRenderBenchmarks.cs); [open-questions P5-Q2](../../plan/open-questions.md).

### D6 — Parity discipline for the new workloads

- **Decision.** Reuse [`TwinContent.Normalize`](../../../src/Heddle.Performance/Runners/TwinContent.cs) unchanged as the parity normalizer for both new workloads, and author every template (Heddle fixture + twins) with **no whitespace between tags and no significant newlines** — so `Normalize` is a functional no-op and the outputs are genuinely raw-byte-identical, while still routing through the shared normalizer for consistency with the existing home workload. Add **new** parity members to `ParityCheck` (`TwinsSubstitution()`/`AssertSubstitution()`, `TwinsLoop()`/`AssertLoop()`, plus a shared private `AssertAgainst(oracle, twins)` helper); **leave the existing `Twins()`/`Assert()`/`Report()` untouched** (non-goal: do not restructure `ParityCheck`). Each new benchmark class calls its own `Assert*` in `[GlobalSetup]` before timing; `Program`'s `-- parity` path is extended additively to also print the two new reports and fail non-zero on any divergence.
- **Rationale.** The home page needs the whitespace-collapse because its twins compose via different constructs; authoring the new, simpler workloads whitespace-free makes parity exact without depending on the normalizer, yet reusing it keeps one discipline. Additive members satisfy P5-Q2's four-engine parity without touching the shipped home path.
- **Alternatives rejected.** *Assert raw equality with a separate comparator* — needless second mechanism; the shared normalizer with a no-op input is simpler and uniform. *Generalize `Assert()` to take a workload argument* — restructures the existing public shape (non-goal); a new sibling method is additive.
- **Grounding.** [`TwinContent.Normalize`](../../../src/Heddle.Performance/Runners/TwinContent.cs); [`ParityCheck`](../../../src/Heddle.Performance/Runners/ParityCheck.cs); [Runners/README.md](../../../src/Heddle.Performance/Runners/README.md).

### D7 — Publication of the new results (committed artifacts + framing)

- **Decision.** Publish the two new `[MemoryDiagnoser]` suites' raw BenchmarkDotNet artifacts in a **new dated sibling directory** `docs/benchmarks/2026-07-13/` (the implementer substitutes their actual run date; this is a facts-pin, not a design choice), **alongside** the preserved `docs/benchmarks/2026-07-11/`. The directory contains, for each of `SubstitutionRenderBenchmarks` and `LoopRenderBenchmarks`, the BenchmarkDotNet-emitted `-report-github.md`, `-report.csv`, `-report.html`, plus a new `index.md` that (a) states each workload's shape, (b) frames results as hardware/date-specific with the reproduce line, (c) explicitly presents trivial-substitution as the case where Heddle's gap **narrows or inverts** and makes **no** claim of universal superiority, and (d) cross-links the 2026-07-11 composition run. A short prose paragraph is added to the [README Performance section](../../../README.md#performance) naming the three-workload set and linking the new directory (prose only — the volatile numbers live in the committed reports/`index.md`, transcribed from the run exactly as the existing composition table was). The reports are generated deliverables (like goldens): the spec fixes their shape and generating command; the measured numbers are produced by running the suite.
- **Rationale.** Sibling directory keeps each dated run an honest snapshot of its commit/hardware; the framing is the credibility payload (publishing the unfavorable case). Keeping numbers out of README prose avoids committing volatile figures the spec cannot produce.
- **Alternatives rejected.** *Add the new reports into `docs/benchmarks/2026-07-11/`* — misdates them against a different run/commit. *Pre-fill README number tables in this spec* — the spec cannot run the machine; that is a generated artifact, not a design TBD. *Skip the framing prose* — the plan makes workload-dependent framing a success criterion.
- **Grounding.** [docs/benchmarks/2026-07-11/index.md](../../benchmarks/2026-07-11/index.md); [README Performance](../../../README.md#performance); plan [success criteria](../../plan/phase-5-docs-and-benchmark-credibility.md).

## Implementation plan

Ordered work items an implementer works straight down. WI1–WI3 are documentation-only; WI4–WI10 are the additive benchmark harness.

### WI1 — Reconcile the version-status loci (D1, D2)

- **Files.** [docs/README.md](../../README.md) (locus 1, one line). Loci 2–6 verified no-edit.
- **Change.** Apply the locus-1 replacement in [D1](#d1--canonical-version-status-statement-and-the-six-locus-disposition-closes-p5-q1) verbatim. Leave loci 2–6 unchanged (record the rule in the PR description). In the PR description (not a doc edit), state the D2 adjacent release action (tag cut + publish + `CHANGELOG` `[Unreleased]`→`[2.0.0]`).
- **Done when.** docs/README.md L19 matches the D1 new wording; the docs grep gate ([Testing plan](#testing-plan)) shows every living-doc version hit consistent with the CVS; loci 2–6 are byte-unchanged.

### WI2 — Retire the dead spec links (D3)

- **Files.** [docs/spec/README.md](../README.md) (rows ≈L28, ≈L30 — L29 is the clean Post-2.0 roadmap row, no dead links); [docs/spec/common/cross-cutting-decisions.md](../common/cross-cutting-decisions.md) (≈L159); [docs/spec/common/breaking-windows.md](../common/breaking-windows.md) (≈L43-44). (The append-only [records.md](../records.md) is handled separately in [WI3](#wi3--recordsmd-bounded-correction-sr-asr-b-assessment-untouched-d4) per SR-A/SR-B / D4.)
- **Change.** Apply the four repointings in [D3](#d3--dead-link-retirement-across-the-living-spec-docs-closes-p5-q3): dead links → plain text with "(retired — git history)"; keep every still-resolving link (assessment, amendments ledger, 2.0 window record). Per D3, the **@import-removal** row (≈L30) takes the "Shipped in 2.0.0; specs retired" status; the **improvement-effort** row (≈L28) takes the softened, grounded status ("specs retired — git history; shipped into the 2.0 window, two items re-land additively in 2.1") with an added resolving `[2.0 window record]` link — it does **not** copy the full-shipment wording.
- **Done when.** A link-check ([Testing plan](#testing-plan)) of spec/README.md, cross-cutting-decisions.md, breaking-windows.md, **and records.md** reports **zero** dangling targets; no link to `improvement-plan.md`/`improvement-spec.md`/`import-removal-plan.md`/`import-removal-spec.md` remains in any doc; records.md's retired-spec citations are plain text, its `#window-30…` anchor is corrected to `#next-window-candidate-register`, and no records.md row asserts the 2.0 window is closed (SR-A/SR-B, D4).

### WI3 — records.md bounded correction (SR-A/SR-B); assessment untouched (D4)

- **Files.** [records.md](../records.md). [language-assessment.md](../../language-assessment.md) — none.
- **Change.** In [records.md](../records.md) apply the **bounded correction** of [D4](#d4--recordsmd-bounded-correction-sr-asr-b-assessment-untouched-closes-p5-q4-p5-q5): (a) SR-A — de-link every dead `improvement-*.md` / `import-removal-*.md` citation (≈L42/47 and the ledger rows ≈L104-107) to plain text "(retired — git history)"; (b) SR-B — reconcile the premature "released 2.0.0 / window closed" phrasing (as-shipped intro ≈L17; ledger disposition ≈L49-52/≈L104) to *in-progress / window-open*, and correct the broken `#window-30…` anchor at ≈L104 to `#next-window-candidate-register`. Alter **no** recorded fact. [language-assessment.md](../../language-assessment.md) stays untouched and excluded from the grep.
- **Done when.** `git diff` on records.md shows only de-linking + window-status wording + the anchor fix (no fact removed/changed); the link-check finds zero dangling targets in records.md; `language-assessment.md` is byte-unchanged.

### WI4 — Engine-neutral content for the two workloads

- **Files (new).** `src/Heddle.Performance/Runners/SubstitutionContent.cs`, `src/Heddle.Performance/Runners/LoopContent.cs`.
- **Change.** `SubstitutionContent` (block-scoped namespace `Heddle.Performance.Runners`, `internal static`): a `SubstitutionModel` POCO with plain-ASCII scalar members and one shared instance, plus per-engine views:

  ```csharp
  public sealed class SubstitutionModel
  {
      public string Title { get; set; }
      public string Sku { get; set; }
      public int Price { get; set; }
      public string Brand { get; set; }
      public string Category { get; set; }
      public string Availability { get; set; }
      public string Url { get; set; }
      public string ImageUrl { get; set; }
      public string Summary { get; set; }
      public string Rating { get; set; }
  }
  // Single shared instance (plain ASCII — no char any engine encodes differently):
  //   Title="Heddle Handbook", Sku="HB-2001", Price=4200, Brand="Heddle Press",
  //   Category="Reference", Availability="In stock", Url="/catalog/handbook",
  //   ImageUrl="/img/handbook.png", Summary="A concise field guide to the engine.", Rating="4.8"
  public static SubstitutionModel Model();                    // Heddle typed model
  public static Dictionary<string, object> LiquidModel();     // keys: title,sku,price,brand,category,availability,url,image_url,summary,rating
  public static Dictionary<string, object> HandlebarsModel(); // same keys
  // Scriban ScriptObject built from LiquidModel() in the Scriban twin.
  ```

  `LoopContent` (same namespace, `internal static`): `LoopModel { public List<LoopRow> Items { get; set; } }`, `LoopRow { public string Name { get; set; } public int Value { get; set; } }`, `RowCount = 5000`, one shared row set (`Name = "row-" + i`, `Value = i`, plain ASCII), built **once** (static). Because the competitor engines resolve `item.name`/`item.value` on their **native member-accessible containers**, not on a plain CLR POCO (DotLiquid needs a `Hash`/`Drop`, and Fluid's default `MemberAccessStrategy` denies un-registered POCO members — see [D5 rationale](#d5--two-new-parity-checked-benchmark-workloads-closes-p5-q2) and [WI7](#wi7--large-loopheddle-fixture--competitor-twins)), each twin's rows are the same engine-native container the existing twins already use for their string maps — **not** `LoopRow`:
  - `Model()` → the Heddle-typed `LoopModel` (its `Items` is the `List<LoopRow>`), consumed by the typed `SubstitutionModel`/`LoopModel` compile context.
  - `DotLiquidModel()` → a `Hash` whose `items` is a `List<Hash>` (each row a `Hash { ["name"]=…, ["value"]=… }`), mirroring `DotLiquidTest.BuildModel`'s string-in-`Hash` precedent.
  - `LiquidModel()` → a `Dictionary<string, object>` whose `items` is a `List<Dictionary<string, object>>` (Fluid resolves dictionary members via `item.name`), mirroring `FluidTest`'s `Dictionary<string,object>` precedent.
  - `HandlebarsModel()` → a `Dictionary<string, object>` whose `items` is a `List<Dictionary<string, object>>` (Handlebars resolves `{{name}}` off dictionary rows).
  - The Scriban twin builds a `ScriptObject` per row from the neutral rows (mirroring `ScribanTest.ToScriptObject`).

  The shared row payload (the 5,000 `(name, value)` pairs) is built once and each per-engine container is materialized once in the runner ctor (not per benchmark op), so no op re-allocates the model.
- **Done when.** Both files compile in `src/Heddle.Performance`; the shared instances are constructed once (static) so no benchmark op re-allocates the model.

### WI5 — Heddle runners (oracles)

- **Files (new).** `src/Heddle.Performance/Runners/SubstitutionHeddleTest.cs`, `src/Heddle.Performance/Runners/LoopHeddleTest.cs`.
- **Change.** Each `public sealed` class mirrors [`HeddleTest`](../../../src/Heddle.Performance/Runners/HeddleTest.cs): a cached file-based `HeddleTemplate` and `string Render()` + `Task Run()`. Construction:

  ```csharp
  _target = new HeddleTemplate(new CompileContext(
      new TemplateOptions("trivial-substitution") {   // "large-loop" for the loop runner
          FileNamePostfix = ".heddle",
          RootPath = "TestTemplates",
          OutputProfile = OutputProfile.Text,
          ExpressionMode = ExpressionMode.Native,
          ProvideLanguageFeatures = false
      },
      typeof(SubstitutionModel)));                     // typeof(LoopModel) for the loop runner
  public string Render() => _target.Generate(SubstitutionContent.Model()); // LoopContent.Model()
  ```
- **Done when.** Both render non-empty output; `Render()` reused across iterations (template compiled once in the ctor).

### WI6 — `trivial-substitution.heddle` fixture + competitor twins

- **Files (new).** `src/Heddle.Performance/TestTemplates/trivial-substitution.heddle`; `src/Heddle.Performance/Runners/SubstitutionLiquidTemplates.cs` (shared Fluid+DotLiquid source); `src/Heddle.Performance/Runners/SubstitutionFluidTest.cs`, `SubstitutionDotLiquidTest.cs`, `SubstitutionScribanTest.cs`, `SubstitutionHandlebarsTest.cs`.
- **Change.** Fixture (single line, **no whitespace between tags**):

  ```heddle
  <article><h1>@(Title)</h1><p class="sku">@(Sku)</p><p class="price">@(Price)</p><p class="brand">@(Brand)</p><p class="cat">@(Category)</p><p class="avail">@(Availability)</p><a class="link" href="@(Url)"><img src="@(ImageUrl)"></a><p class="sum">@(Summary)</p><p class="rating">@(Rating)</p></article>
  ```

  Twin sources reproduce the identical byte sequence via idiomatic member substitution (all raw): Liquid `<article><h1>{{ title }}</h1>…<a class="link" href="{{ url }}"><img src="{{ image_url }}"></a>…</article>`; Scriban the same with `{{ }}`; Handlebars with triple-mustache `{{{title}}}`…`href="{{{url}}}"`…`{{{rating}}}`. Each twin class is `public sealed` with `string Render()` (parse once in ctor, render the WI4 model), mirroring the existing `FluidTest`/`ScribanTest`/`DotLiquidTest`/`HandlebarsTest` wiring (Fluid `TemplateContext.SetValue`, DotLiquid `Hash`, Scriban `ScriptObject` globals, Handlebars `Compile` + `Dictionary` model). No layout/include/loop is used (minimal composition).
- **Done when.** `SubstitutionHeddleTest.Render()` and all four twins produce identical bytes (whitespace-free ⇒ `TwinContent.Normalize` is a no-op); the fixture is picked up from `TestTemplates/` via `TemplateOptions("trivial-substitution")`.

### WI7 — `large-loop.heddle` fixture + competitor twins

- **Files (new).** `src/Heddle.Performance/TestTemplates/large-loop.heddle`; `src/Heddle.Performance/Runners/LoopLiquidTemplates.cs`; `src/Heddle.Performance/Runners/LoopFluidTest.cs`, `LoopDotLiquidTest.cs`, `LoopScribanTest.cs`, `LoopHandlebarsTest.cs`.
- **Change.** Fixture (single line):

  ```heddle
  @list(Items){{<tr><td>@(Name)</td><td>@(Value)</td></tr>}}
  ```

  Twin loop sources over the 5,000-row model (all raw): Liquid `{% for item in items %}<tr><td>{{ item.name }}</td><td>{{ item.value }}</td></tr>{% endfor %}`; Scriban `{{ for item in items }}<tr><td>{{ item.name }}</td><td>{{ item.value }}</td></tr>{{ end }}`; Handlebars `{{#each items}}<tr><td>{{{name}}}</td><td>{{value}}</td></tr>{{/each}}`. Each twin `public sealed` with `string Render()`, and each reads its rows from the **engine-native container** its precedent twin uses — `DotLiquidTest`-style rows are `Hash` (`LoopContent.DotLiquidModel()`), `FluidTest`/Handlebars rows are `Dictionary<string, object>` (`LoopContent.LiquidModel()`/`HandlebarsModel()`), and the Scriban twin materializes a `ScriptObject` per row (WI4). A plain `LoopRow` POCO is deliberately **not** passed to DotLiquid/Fluid: neither resolves `item.name` on an un-registered POCO (DotLiquid needs `Hash`/`Drop`; Fluid's default `MemberAccessStrategy` denies it), so using the native containers is what makes the four-engine parity gate actually pass. The Heddle oracle uses the typed `LoopModel` (`@list(Items)` over `List<LoopRow>`).
- **Done when.** All five engines render the identical 5,000-row concatenation (no inter-tag whitespace ⇒ normalizer no-op).

### WI8 — Extend `ParityCheck` and `Program` (additive)

- **Files (changed).** [src/Heddle.Performance/Runners/ParityCheck.cs](../../../src/Heddle.Performance/Runners/ParityCheck.cs), [src/Heddle.Performance/Program.cs](../../../src/Heddle.Performance/Program.cs).
- **Change.** In `ParityCheck`, **add** (leaving `Twins()`/`Assert()`/`Report()` untouched): `TwinsSubstitution()` and `TwinsLoop()` (yield the four workload twins as `(Name, Func<string>)` like `Twins()`); `AssertSubstitution()`/`AssertLoop()` delegating to a new `private static void AssertAgainst(string heddleRaw, IEnumerable<(string,Func<string>)> twins)` that reproduces the existing `Assert()` normalize-and-compare loop; and `ReportSubstitution()`/`ReportLoop()` (or a shared `ReportAgainst`) mirroring `Report()`. In `Program.Main`, extend the `parity` branch to also run the two new reports and exit non-zero if **any** of the three fails.
- **Done when.** `dotnet run -c Release --project src/Heddle.Performance -f net10.0 -- parity` prints PASS for all three workloads' twins and exits `0`; a deliberately drifted twin makes it exit `1`.

### WI9 — Benchmark classes

- **Files (new).** `src/Heddle.Performance/SubstitutionRenderBenchmarks.cs`, `src/Heddle.Performance/LoopRenderBenchmarks.cs`.
- **Change.** Each `public class` with `[MemoryDiagnoser]`, mirroring [`TextRenderBenchmarks`](../../../src/Heddle.Performance/TextRenderBenchmarks.cs) but host-free (no Razor/DI): fields for the Heddle oracle + four twins; `[GlobalSetup]` constructs them and calls `ParityCheck.AssertSubstitution()` / `AssertLoop()` **before** timing; one `[Benchmark]` per engine with `RenderHeddle` as `[Benchmark(Baseline = true)]` (`RenderFluid`/`RenderScriban`/`RenderDotLiquid`/`RenderHandlebars`). No `[GlobalCleanup]` needed (no host).
- **Done when.** `dotnet run -c Release --project src/Heddle.Performance -- --filter *SubstitutionRenderBenchmarks*` and `*LoopRenderBenchmarks*` each complete with the parity assertion passing in `[GlobalSetup]` and emit BenchmarkDotNet reports.

### WI10 — Publish artifacts + framing (D7)

- **Files (new).** `docs/benchmarks/2026-07-13/Heddle.Performance.SubstitutionRenderBenchmarks-report-github.md` (+`.csv`, `.html`); `docs/benchmarks/2026-07-13/Heddle.Performance.LoopRenderBenchmarks-report-github.md` (+`.csv`, `.html`); `docs/benchmarks/2026-07-13/index.md`. **Files (changed).** [README.md](../../../README.md) Performance section.
- **Change.** Copy the BenchmarkDotNet `BenchmarkDotNet.Artifacts/results/` output for the two suites into the dated directory. Author `index.md` on the model of [2026-07-11/index.md](../../benchmarks/2026-07-11/index.md): environment block, per-workload shape description, the workload-dependent framing (trivial-substitution = gap narrows/inverts; no universal-superiority claim), the reproduce line, and a cross-link to the 2026-07-11 composition run. Add one prose paragraph to the README Performance section after the reproduce block:

  > **Workload breadth.** The composition page above is one of three published workloads. A [trivial-substitution and a large-loop workload](docs/benchmarks/2026-07-13) bracket it — the former (scalar output, no composition) is the shape where Heddle's lead narrows or inverts, the latter (one large iteration) where it widens; both are parity-checked against the same four engines. Numbers are hardware- and date-specific; reproduce with the command above filtered to `*SubstitutionRenderBenchmarks*` / `*LoopRenderBenchmarks*`.
- **Done when.** The dated directory holds six committed report files + `index.md`; the README paragraph links it; the 2026-07-11 files are byte-unchanged.

### WI11 — Master-index reachability (coordination)

- **Files (changed).** [docs/spec/README.md](../README.md) Initiatives table (shared across the post-2.0 phase specs).
- **Change.** The shared "**Post-2.0 roadmap (phased)**" initiative row **already exists** in the [spec/README.md](../README.md) Initiatives table (≈L29) and already enumerates all eight phases including "P5 docs/benchmark credibility". So this WI is a **reachability guard**, not a create step: verify the Phase-5 entry (and its link into `post-2.0/README.md`, the set index) is present and correct; add it only if a future edit has dropped it. Do not add a duplicate row.
- **Done when.** This spec is reachable from [spec/README.md](../README.md) (via the existing roadmap row and the `post-2.0/` set index) per the [index-maintenance rule](../common/spec-conventions.md#index-maintenance).

## Public API / contract

n/a — no API change. This phase adds no public type or member to any shipped assembly. The new `Heddle.Performance` types (`SubstitutionModel`, `LoopModel`, the runners, the `ParityCheck` additions) live in the non-shipped benchmark project and are `internal`/benchmark-only.

## Diagnostics / error surface

n/a — no diagnostics. No `HED*` id is claimed, and no compile error/warning is added or changed. The only runtime failure surface is the benchmark harness's existing `ParityCheck` `InvalidOperationException` on twin drift (a developer-facing loud failure, not a template diagnostic), now also raised by the additive `AssertSubstitution`/`AssertLoop`.

## Testing plan

**TDD verdict.** *Test-first* for the parity of both new workloads: the twins plus `ParityCheck.AssertSubstitution()`/`AssertLoop()` are the executable gate and are authored and made to pass (via `-- parity` and the benchmark `[GlobalSetup]`) **before** any numbers are trusted or committed — a drifted twin fails loudly rather than benchmarking different work. *Test-with* for the benchmark measurement classes (BenchmarkDotNet scaffolding measures; it asserts no behavior). The documentation workstream (WI1–WI3) is verified by the link-check and grep gates below rather than by xUnit, since it changes no code path.

**Fixtures / assets added.** `trivial-substitution.heddle`, `large-loop.heddle` (LF-pinned automatically by the existing global `*.heddle text eol=lf` rule in [.gitattributes](../../../.gitattributes) L7 — **verify** the two new fixtures resolve to LF on a Windows checkout; **no new `.gitattributes` root is required**, and none is added for the generated benchmark reports, matching the unpinned 2026-07-11 run). No xUnit golden is added (the benchmark project is not an xUnit suite; parity is its equality assertion).

**Regression gate — the combined pre-merge run.**

1. **Docs link-check.** Every markdown link in [spec/README.md](../README.md), [cross-cutting-decisions.md](../common/cross-cutting-decisions.md), [breaking-windows.md](../common/breaking-windows.md), **and [records.md](../records.md)** resolves to an existing file/anchor — **zero** dangling targets (including the former `improvement-*.md` / `import-removal-*.md` links and the former `#window-30…` anchor). records.md is now **in scope** because SR-A/SR-B de-linked its retired-spec citations to plain text (D4); only its dated 2.0 *vocabulary* is exempt (from gate 2's grep, not the link-check).
2. **Docs consistency grep.** A `git grep` (git-tracked files only, so gitignored `docs/node_modules/**` and any un-tracked build output are excluded automatically — do **not** use a filesystem grep, which would walk them) across the **living** docs for `2.0.0` / "current version" / "release line" finds every hit consistent with the CVS and **no** contradiction. Scope: `docs/**/*.md` and root `README.md`/`CHANGELOG.md`, **excluding** `docs/language-assessment.md`, the **entire spec set `docs/spec/**`** (contributor task-material that *describes* the 2.0 roadmap/window — including this phase set, the shared `common/` docs, and the append-only `records.md` — not a product version-status assertion, exactly as `docs/plan/**` is excluded), `docs/plan/**`, and generated/vendored paths (`docs/.vitepress/**`, `docs/package-lock.json`). This asymmetry is deliberate and symmetric with the plan exclusion: the gate polices the **user-facing** docs (the six loci all live directly under `docs/`, none under `docs/spec/`), so excluding `docs/spec/**` removes the spec-material false hits (e.g. `docs/spec/post-2.0/common/cross-cutting.md`'s "no `v2.0.0` tag exists") without dropping any real version locus. The `CHANGELOG.md` `[Unreleased]` block is the expected pre-release state closed by the D2 adjacent action, not a contradiction.
3. **Benchmark build + parity.** `dotnet run -c Release --project src/Heddle.Performance -f net10.0 -- parity` exits `0` with PASS for all three workloads (home + substitution + loop); and `dotnet run -c Release --project src/Heddle.Performance -- --filter *SubstitutionRenderBenchmarks*` / `*LoopRenderBenchmarks*` complete with `ParityCheck.Assert*` passing in `[GlobalSetup]` for each registered twin and emit reports.
4. **Docs build.** `cd docs && npm run docs:build` succeeds (run from an **uppercase-drive** cwd on Windows, e.g. `E:\Work\Heddle\docs`; `preserveSymlinks` already configured). Note the new benchmark markdown lives under `docs/benchmarks/` and must not break the build.
5. **Engine suites untouched.** `dotnet test src/Heddle.Tests` and the generator/golden suites are **unaffected** (no engine source changed) — the existing golden corpus stays byte-identical because no rendered byte is touched (see [Back-compat](#back-compat-and-migration)).

**Verification loop.** Author twins + parity asserts failing → implement fixtures/runners until `-- parity` is green → run the two filtered suites, capture artifacts → author `index.md` + README prose → run gates 1–5 in one pass. A red parity gate is fixed by correcting the twin/fixture, never by loosening the normalizer.

## Back-compat and migration

- **Proof of no engine / rendered-byte change.** The complete diff touches only: `docs/**` (prose + new benchmark reports), root `README.md` (Performance prose), `.gitattributes` (no change — the global `*.heddle` rule already covers the fixtures), and `src/Heddle.Performance/**` (new fixtures/runners/benchmark classes + additive `ParityCheck`/`Program` members). **No file under `src/Heddle`, `src/Heddle.Language`, `src/Heddle.Language/generated`, `src/Heddle.Generator`, or any other shipped assembly is modified.** Therefore no template's rendered bytes, no public API, no diagnostic, and no grammar changes; the dynamic == precompiled differential and the golden corpus are untouched by construction. A reviewer confirms this by inspecting the diff's path set.
- **Existing benchmark contract preserved.** `Twins()`, `Assert()`, `Report()`, `TextRenderBenchmarks`, `BranchRenderBenchmarks`, and the 2026-07-11 artifacts are byte-unchanged; the new parity members and benchmark classes are purely additive. The suite is a developer tool, not a shipped contract, so nothing downstream depends on it.
- **Breaking window.** None. This phase schedules nothing into a breaking window ([breaking-windows.md](../common/breaking-windows.md)); it is editorial + additive. The `v2.0.0` tag cut / publish / `CHANGELOG` finalization is the **release** action flagged by [D2](#d2--changelog-tag-publish-are-the-flagged-adjacent-release-action-not-performed), performed adjacently, not here.
- **Migration note.** None required — no user-visible behavior or API changes.

## Performance considerations

- **The benchmark additions are the deliverable, not a hot-path change.** No engine hot path is touched: every new type lives in the `Heddle.Performance` project, which references but does not modify the engine. There is nothing to regress in the render/compile path because no render/compile-path source changes.
- **New guards added.** `SubstitutionRenderBenchmarks` and `LoopRenderBenchmarks` are `[MemoryDiagnoser]` render benchmarks with Heddle as `[Benchmark(Baseline = true)]`, extending the evidence base beyond the single composition page. They characterize two shapes the existing suite did not: scalar-substitution-dominated (where the assessment predicts Heddle's lead narrows/inverts) and iteration-dominated. The models are constructed once in `[GlobalSetup]`/statically so each op measures rendering, not model allocation.
- **Cost framing.** All published numbers retain the "hardware- and date-specific; reproduce yourself" framing and commit raw artifacts, exactly as the 2026-07-11 run does; no absolute claim is made.

## Standards compliance

- **YAGNI / simplicity (precedence rule 3).** The parity extension adds sibling `Assert*`/`Twins*` methods instead of generalizing `Assert()` into a parameterized engine — the smallest change that meets the criterion and honors the "don't restructure `ParityCheck`" non-goal. No speculative config knobs.
- **DRY (knowledge, not lines).** The one piece of knowledge that must not diverge — the parity normalization — is reused verbatim (`TwinContent.Normalize`); the new `AssertAgainst` helper consolidates the normalize-and-compare loop the two new asserts share. Per the repo's "tests optimize for diagnosis" rule, the per-engine twin classes are deliberately **not** collapsed into one generic twin — the duplication keeps each engine's idiomatic wiring readable and a drift diagnosable at a glance (Fluid+DotLiquid still share their Liquid source, matching the existing precedent).
- **Repository style.** New C# uses block-scoped namespaces (`Heddle.Performance.Runners`), matches the surrounding runner files, and carries XML-doc summaries on the public twin types (the existing runners' shape). Documentation edits follow the repo's dash/sentence-case/GFM-table style and use relative links only; retired documents are cited as plain text with a "(retired)" qualifier.
- **Additive-by-default (precedence rule 1).** The whole phase is additive/editorial; correctness and back-compat are preserved trivially because no engine source is in the diff.

## Deferred items

| Item | Trigger |
|---|---|
| Heddle-only fallback for the two workloads (drop the eight competitor twins, keep only the `[MemoryDiagnoser]` Heddle benchmarks, à la `BranchRenderBenchmarks`) | Authoring/maintaining the eight parity twins across four engines proves prohibitive or a new drift surface (plan P5-Q2 risk); the plan sanctions Heddle-only as the precedented fallback. |
| Cold parse/compile benchmarks for the new workloads (a `TemplateParseBenchmarks` sibling) | A characterization gap on the compile side for the new shapes is raised; the plan's success criteria require only the render suites. |
| Finalizing the `CHANGELOG` `[Unreleased]`→`[2.0.0]`, cutting `v2.0.0`, publishing | The imminent 2.0 release action lands (the flagged D2 adjacent action). |
| Adding `docs/benchmarks/**` to `.gitattributes` `eol=lf` | A benchmark report becomes a byte-asserted golden (it is not today); until then it matches the unpinned 2026-07-11 run. |
| Finalizing records.md as the *closed* 2.0 window record (flip "in-progress / window open" to a dated as-shipped close) | The `v2.0.0` tag is cut (the D2 adjacent release action); until then SR-B keeps the record reading window-**open**. |

## External references

- [BenchmarkDotNet](https://benchmarkdotnet.org/) — the benchmark tool and its reproducibility framing.
- [Keep a Changelog 1.1.0](https://keepachangelog.com/en/1.1.0/) and [Semantic Versioning 2.0.0](https://semver.org/spec/v2.0.0.html) — the `CHANGELOG` conventions the D2 adjacent action follows.
- [.NET library breaking-change rules](https://learn.microsoft.com/en-us/dotnet/standard/library-guidance/breaking-changes) — the additive-by-default basis for the no-engine-change proof.
- Repo primary sources cited throughout: [ParityCheck.cs](../../../src/Heddle.Performance/Runners/ParityCheck.cs), [TwinContent.cs](../../../src/Heddle.Performance/Runners/TwinContent.cs), [TextRenderBenchmarks.cs](../../../src/Heddle.Performance/TextRenderBenchmarks.cs), [BranchRenderBenchmarks.cs](../../../src/Heddle.Performance/BranchRenderBenchmarks.cs), [Runners/README.md](../../../src/Heddle.Performance/Runners/README.md), [CHANGELOG.md](../../../CHANGELOG.md), [docs/spec/README.md](../README.md), [cross-cutting-decisions.md](../common/cross-cutting-decisions.md), [breaking-windows.md](../common/breaking-windows.md).