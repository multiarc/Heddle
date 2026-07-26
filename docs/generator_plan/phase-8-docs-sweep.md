# Phase 8 — post-implementation documentation sweep

## Header

- **Status:** **proposed — not started.**
- **Goal (one line):** Bring the prose documentation back into agreement with the code the program
  actually shipped — starting with the documents that **outrank** the code, where a wrong sentence
  is a latent bug rather than a nuisance — and leave behind gates so the agreement is mechanical
  rather than remembered.
- **Depends on:** phases 0–6 landed (their behaviour changes are the sweep's input) and the six
  post-implementation audits (their findings are its bill of materials). Two *ordering* dependencies,
  not gating ones: the queued **Q8.2** work item (the 2.1 version bump + the schema floor rise) must land
  before this plan's CHANGELOG/migration deliverables can state a shipped fact. **The phase-7 dependency
  is gone** — D9 was rejected by Q8.10, and with it this phase's only hard external dependency. Nothing
  depends on this phase.
- **Changes an externally-visible contract:** **the published documentation is itself an
  externally-visible contract, and this phase changes it** — that is the point. No engine source, no
  generator source, no rendered byte, no public API and no diagnostic ID changes (`HED7025` is
  claimed by Q8.1's work item, not by this one; this phase only documents it once it exists). Three
  normative-document touches go through the amendments mechanism
  ([spec-conventions §Amendments](../spec/common/spec-conventions.md#amendments-during-implementation))
  with ledger entries in [records.md](../spec/records.md#cross-spec-amendments-ledger): the
  authority-convention narrowing (D3), the diagnostics-documentation rule (D4), and the
  documentation-currency rule (D11).

## Goal

The program's authority convention states that *"expression semantics defer first to
[docs/native-expressions.md](../native-expressions.md)"* — the document **outranks both
implementations**. That document currently says, as deviation 1, that `==`/`!=` on unrelated
reference/**mixed** types compiles to a total, null-safe `object.Equals`. It does not. The runtime
guards the fallback with `IsReferenceish(left) && IsReferenceish(right)`
([NativeExpressionCompiler.cs:800](../../src/Heddle/Runtime/Expressions/NativeExpressionCompiler.cs)),
so `string == int` raises `HED1008` on **both** tiers. Phase 4 found it; phase 4's audit re-confirmed
it unfixed; Q8.7 ruled that the fix be widened into a documentation sweep.

The reason the widening is right is visible in that one sentence. It is not a typo — it is a
**live trap**. A future agent doing exactly what the program taught it to do (find drift between
tiers, align to the normative source) would read that sentence, conclude both tiers are wrong,
delete the `IsReferenceish` guard, and ship `string == int` silently comparing false where it used
to be a positioned compile error. The documentation would have caused the bug, and the code review
would have approved it, because the plan of record said so.

That is a distinct severity class, and it is the class the sweep exists to close first. It also
generalises: the program changed a great deal of observable behaviour and updated prose only where a
phase happened to walk past it. Seven diagnostics were claimed (`HED7018`–`HED7024`) with an eighth
allocated; the generator's blanket `catch` became a per-template build **error**, so a project that
silently degraded now fails its build; the editor's default output profile moved `Text`→`Html`; the
runtime's short-name type-resolution tie became an ambiguity error; float/double literal text moved
from `"R"` to `G17`/`G9`; `HED7006`'s trigger narrowed; `Name` item metadata was removed and
`Precompile` wired; `[ZeroOutput]` became public API; `PrecompiledSchema` moved 2→5 across three
phases; and a **binary break** shipped in `PrecompiledExtensionBinding`.

Surveying for this phase (see [External grounding](#external-grounding)) found the debt is real,
bounded, and unevenly distributed in a way that changes what the sweep should do:

1. **Deviation 1 is not the only false normative claim — it is one of four.** A claim-by-claim audit
   of `native-expressions.md` against source found **four outright false statements** and **seven
   more that are imprecise enough to mislead an implementer**, against roughly forty claims that
   check out. The other three false ones are the same *shape* as the anchor case — each would be
   "fixed" by widening acceptance, and each such widening is a breaking change the tree has
   deliberately not made:
   - **Deviation 6** says user-defined *operators* are honored. They are honored for arithmetic,
     relational, equality and `??` — and **not at all** for `&`/`^`/`|`, `<<`/`>>`, or any unary
     operator, where the compiler fails before reaching the factory.
   - **The shift row** says the right operand must be `int`. Any integral right operand is accepted
     and converted; `@(I << L)` compiles here and is `CS0019` in C#. The doc **understates** what the
     engine accepts, so "fixing" the code to match would break templates that compile today.
   - **The lifted-operands section** says relational *and equality* operators lift and compare
     `false` on `null`, "exactly as C#". Lifting exists only on the numeric paths: a `bool`/`bool?`
     equality is `HED1008`, and a `bool`/`bool?` **bitwise** pair reaches `Expression.And` unguarded
     and surfaces as an id-less *"Error while compiling"* — a documented-as-working shape that in
     fact has no diagnostic at all.

   The generator's shared rule tables already **document each of these divergences correctly**, in
   code comments, beside the verdict that encodes them. So the tree's most accurate description of
   native-expression semantics is currently `Language/Expressions/NativeOperatorRules.cs`, and the
   document the authority convention points at is the least accurate. That inversion is the finding.
2. **The `HED7xxx` block is in excellent shape and the rest of the diagnostic surface is not.**
   All 24 `HED70xx` IDs are triple-covered (descriptor ⇄ registry ⇄ `precompilation.md` table),
   in both directions, by a real gate. Meanwhile **20 of the 82 shipped diagnostic IDs are named in
   no user-facing document at all**, and the worst block is the one a template author is most likely
   to hit: the registry designates `native-expressions.md` as the normative home of
   `HED1001`–`HED1017`, and that document names **2 of the 17**. `HED1008` — the error the anchor
   defect is *about* — is documented nowhere. Every gate is green, because no gate reaches
   user-facing prose.
3. **Class M is 2 wrong, 30 stale, 4 overstated across the published set — and two of the four
   overstatements are self-contradicted by their own document.** The two factually wrong
   user-facing statements attributable to this program are `csharp-api.md`'s description of
   `TemplateOptions.FullPath` (wrong on both clauses after phase 6's fix, and contradicting
   `getting-started.md`) and `custom-extensions.md`'s *"You get this for free … there is nothing extra
   to implement"* about directive-line trimming, which the **same file** now contradicts 450 lines
   later by documenting `[ZeroOutput]` as exactly the thing you must implement. Separately,
   `precompilation.md` contains two absolutes that its own tables disprove
   (*"an unset property never causes a gauntlet mismatch"*; *"every class above still degrades
   silently"*). Self-contradiction inside one document is the cheapest possible evidence that nothing
   checks these claims.
4. **The stale-citation problem is not where it was expected.** User-facing docs (`docs/*.md`),
   `docs/spec/**` and `docs/plan/**` carry **zero** file:line citations — they are structurally
   immune. All **830** line citations in the tree sit in `docs/generator_plan/**` (471) and
   `docs/research/**` (359), of which **86 cite a line past the end of the file**. Those are
   precisely the documents this phase may not rewrite: the phase plans are owned by their phases
   (and by two concurrently-working agents), and the research documents are dated evidence.
5. **The version surface is wider than the nine csproj files** and partly redundant. Nine
   `<Version>` elements exist with **no central property**; CI overrides all of them from the git
   tag, so they are documentation of the release line rather than the release mechanism. Thirteen
   files must change for 2.0.0→2.1.0 and five more are coupled — including
   `editors/vscode/src/extension.ts`'s `PINNED_VERSION`, which pins a NuGet tool version outside
   any consistency check at all.

### Findings that are code defects, not doc defects — escalated, not fixed here

Three surfaced while surveying. Per D2 they are recorded and escalated, and this phase touches none
of them:

- **A `bool`/`bool?` bitwise operand pair has no diagnostic.** `NativeExpressionCompiler`'s bool arm
  passes the mismatched pair straight to `Expression.And`, whose `InvalidOperationException` escapes
  to `HeddleCompiler`'s generic handler and surfaces as an id-less *"Error while compiling &lt;ext&gt;"*.
  Every comparable illegality in the tier is a positioned `HED1008`. The generator's shared table
  already pins this as the agreed verdict, so it is a *matched* defect, not drift — which is exactly
  why it needs a ruling rather than an edit.
- **`floor(3)` / `ceil(3)` / `round`-on-`int` are compile errors** (`HED1013`, ambiguous), because the
  built-ins are `double`/`decimal`-only and `int→double` and `int→decimal` tie under the flat
  Pareto rank. `native-expressions.md` says these functions work *"over `int`/`long`/`double`/`decimal`
  as applicable"*. This is the shipped `min(1, 2u)` counter-example wearing a friendlier face, and
  the fix is the **already-filed next-window candidate** (C# betterness in the runtime binder). The
  doc gets a correction; the behaviour waits for the window.
- **A shipped sample still uses the removed `Name` item metadata.**
  `samples/codegen-t4-successor/CodegenT4Successor.csproj` carries
  `<HeddleTemplate Include="templates\report.heddle" Name="BuildReport" />`. `Heddle.Generator.props`
  no longer reads it, so the sample silently loses its intended key. It is a live, golden-checked
  user-facing artifact rather than prose — filed as **Q8.12**.

## Non-goals / scope boundary

- **No code change of any kind.** Not a behaviour fix, not a diagnostic, not a rename, not a test
  of behaviour. The only source files this phase adds are **documentation gates** (test code that
  reads markdown). Where the survey finds the *code* wrong and the doc right, the finding is
  escalated as an open question in the register — see D2. This phase never "fixes" code to match
  prose, which is the exact failure mode it exists to prevent.
- **`src/Heddle.Performance` is not touched at all** (Q7.2 ruling, inherited verbatim). A benchmark
  effort is mid-flight there. Note the consequence: two broken doc links point at the deleted
  `src/Heddle.Performance/GoldenCorpus` path from `docs/benchmarks/2026-07-25/index.md`. They are
  **reported to the benchmark effort, not fixed here**, because that report and its evidence tree
  belong to it.
- **`docs/generator_plan/phase-*.md` are not edited by this phase.** Their corrections belong to
  their owning phases (the [spec-conventions](../spec/common/spec-conventions.md) rule: a later
  effort records the correction with evidence and *does not edit* the earlier document), and two
  agents are concurrently working in that directory. This phase edits exactly two files there:
  [README.md](README.md) (the phase table row) and
  [open-questions.md](open-questions.md) (its own new questions). The 86 beyond-EOF citations in
  those plans are handed over as an inventory, not as edits.
- **`docs/research/**` is frozen.** Eight documents, dated, cited by every phase as the evidence
  base. Their line numbers were correct when written; rewriting them destroys the audit trail and
  gains nothing. The two present-tense claims that are now false (`DocumentsCache.cs` "is entirely
  dead code" for a deleted file; `ReflectionHelper.CSharpTypes` for a deleted symbol) are recorded
  in this plan's inventory as known-historical, not corrected in place.
- **`docs/benchmarks/**` and `docs/spec/cross-stack-benchmarks/**` are out of scope** — 46 documents
  owned by the benchmark effort, governed by their own amendment ledger entries (E1–E7).
- **No new documentation pages, no restructure, no rewrite for style.** The sweep corrects claims. A
  missing-page argument (e.g. "there is no user-facing diagnostic index") is *recorded* as a finding
  with a recommendation; creating that page is a separate effort with its own plan.
- **No re-litigation of ruled behaviour.** Where a behaviour change was ruled correct (Q3.5's
  ambiguity error, Q2.2's narrowed catch, Q6.2's profile flip), the sweep documents it. It does not
  reopen it.
- **Not a translation, localisation, or docs-site-infrastructure change.** `docs/.vitepress/**` is
  not touched at all — D9's include mechanism was rejected (Q8.10), which was the only reason to.

## Design direction

### D1 — Three severity classes, ordered by *what happens if the document is trusted*

**Decision.** Doc debt is triaged into three classes, and the ordering is by consequence-of-trust,
not by effort or by visibility:

| Class | Definition | Consequence if trusted | Disposition |
| --- | --- | --- | --- |
| **N — normative and wrong** | A document the program's authority convention or the claimed-ID registry designates as the **owner** of a rule, stating that rule incorrectly. | A correct implementation gets "fixed" into a defect. The doc *causes* the bug. | Fixed first, individually, each with the source evidence quoted in the landing. |
| **M — user-facing and misleading** | A behaviour changed; the prose still describes the old behaviour, or omits the new one. | A user builds a wrong mental model, mis-configures, or is surprised by a build failure. No code is broken by the doc. | Fixed second, in behaviour-change batches so each landing is one reviewable subject. |
| **S — stale but inert** | A citation, line number, count, or symbol reference that no longer resolves. | A reader loses time. Nothing is misled about behaviour. | **In scope only as a gate, not as a hand-fix** — see below. |

**Class S is deliberately not hand-swept.** The survey is what makes that defensible rather than
lazy: all 830 line citations live in `docs/generator_plan/**` and `docs/research/**`, both excluded
from this phase's edit surface for independent reasons (ownership; frozen evidence). The
user-facing documentation this phase *does* own carries **zero** of them. So a hand-sweep of class S
would consist entirely of edits this phase is not allowed to make. What is in scope is the
**mechanism**: a citation gate (D8) that lands green over the surface this phase owns, so the debt
cannot start there, plus an advisory report over the excluded directories that their owners can act
on.

**Rationale.** Severity is normally proxied by visibility ("front-page docs first"). That proxy is
wrong here, and the anchor case shows why: `native-expressions.md`'s deviation list is not
front-page material, and it is the single most dangerous sentence in the tree — because the
convention points at it *for the purpose of overruling code*. Ordering by consequence-of-trust also
gives a natural stop rule: class N is small and finite (a document is either designated normative or
not), class M is enumerable from the behaviour-change list, and class S is a gate.

**Alternatives rejected.**
- *Order by document popularity / page views.* Rejected — inverts the anchor case.
- *One flat sweep, doc by doc.* Rejected — it makes the landing that fixes a latent bug
  indistinguishable in review from the landing that fixes a typo, which is precisely the reviewability
  the Q8.7 ruling asked for.
- *Fold class S into class M.* Rejected — it triples the apparent size of the effort with the part
  that cannot be executed, and it invites the sweep to edit documents it does not own.

### D2 — The sweep corrects documents, never code — and escalates the reverse case

**Decision.** Every class-N and class-M finding resolves in exactly one of three ways, decided
before the edit:

1. **The code is right and the doc is wrong** → correct the doc, quoting the deciding source line in
   the landing's commit message and in the doc's own verification footer (D11). This is the expected
   case and the anchor case.
2. **The doc is right and the code is wrong** → **do not touch either.** File it in
   [open-questions.md](open-questions.md) as a `Q8.<n>` with a stated default, exactly as the audits
   did. A behaviour change needs a ruling, and a docs sweep is not the vehicle for one.
3. **Both are defensible and they disagree** (the doc describes an intent the code never had) →
   record the disagreement in the doc as an explicit *known limitation* with a register entry, and
   file the reconciliation as a next-window candidate if it would widen behaviour.

**Rationale.** This is the load-bearing safety property of the whole phase. The sweep is being run
*because* documentation outranks code, which means a careless sweep is the most efficient possible
way to introduce behaviour bugs: change one sentence and every future aligner is now aiming at the
wrong target. Making outcome 2 an escalation rather than a fix keeps the sweep's blast radius inside
the documentation, and keeps behaviour changes going through the ruling mechanism that the six
audits established.

It also has a concrete precedent in this program: the audits found phase-plan claims that were
factually false (a coverage rationale, a "exactly one numeric-kind table" claim, a done-when
promising proof that was never measured) and **corrected the documents in place while leaving the
behaviour alone**, filing the behaviour questions as Q8.1–Q8.5. This phase inherits that posture.

**Alternatives rejected.** *Fix small code defects opportunistically when the doc is clearly right.*
Rejected — "clearly right" is exactly the judgement the anchor case proves unreliable, and the
program already has a mechanism (the register) that costs one paragraph.

### D3 — Narrow the authority convention: a normative doc outranks code only where the claim is *verified or gated*

**Decision.** The authority convention as written —
*"expression semantics defer first to `docs/native-expressions.md`"* — is amended, via the
amendments ledger, to a two-part rule:

> A designated normative document outranks the implementations **for any claim that carries a
> verification marker or is covered by a gate**. An unmarked, ungated claim in a normative document
> is **evidence of intent, not an authority**: a contradiction between it and both implementations
> agreeing is resolved by *investigating and then recording the outcome* — never by changing code to
> match the sentence.

Mechanically: each normative document's rule-bearing sections gain a footer of the form
*"Verified against source at `<commit>` (`<date>`); claims marked ✓ are gated by `<test>`."* (D11
supplies the mechanism.) The convention lands as a new cross-cutting decision in
[cross-cutting-decisions.md](../spec/common/cross-cutting-decisions.md), not in this program's
README, because it outlives the program.

**Rationale.** The convention was written to solve a real problem — when two tiers disagree, some
document has to break the tie, and for expression semantics the spec genuinely is the better
arbiter than either implementation. But as stated it grants unconditional authority to prose that
nothing checks, and the anchor case is the proof that unconditional authority over unchecked prose
is a defect-generating mechanism. Narrowing it costs nothing where the convention was doing useful
work (the tie-break case: *two tiers disagree*, which is what it was written for) and removes its
teeth exactly where it was dangerous (the case where both tiers agree and the doc is simply stale).

Note the asymmetry that makes this safe: outcome 2 in D2 already covers "the doc is right and the
code is wrong". This narrowing does not make stale prose unfixable — it makes it non-executable.

**Alternatives rejected.**
- *Leave the convention alone and just fix the sentence.* Rejected: that is the original Q8.7
  question, which the user widened. It fixes one instance of a mechanism that will re-offend — the
  document has six other deviations and forty-odd other normative claims, and nothing checks any of
  them.
- *Remove the convention entirely (code is always normative).* Rejected — it discards the tie-break
  the convention exists for, and for expression semantics the specification really is the better
  authority. It would also invert the program's own phase-2 and phase-4 findings, several of which
  correctly named the *runtime* as the defect against a documented rule.
- *Require every normative claim to be gated (no verification-marker tier).* Rejected as
  unreachable: "arms need a common type" or "clamped, non-throwing" are gateable, but "the trust
  boundary is registration" is a design statement no test can express. A two-tier rule (gated ✓ /
  dated-verified) covers the surface honestly; a one-tier rule would either lie or block.

### D4 — Gate the diagnostics documentation across **all** blocks, using the registry's own owner column

**Decision.** The registry table in
[cross-cutting-decisions.md](../spec/common/cross-cutting-decisions.md#claimed-diagnostic-ids-registry)
already names, per ID range, the **owning document** (`native-expressions.md`,
`built-in-extensions.md`, `language-reference.md`, `precompilation.md`, or an explicit
*"this registry row is the live normative home"*). That column is currently decorative. It becomes
the gate's input:

> For every claimed ID whose registry row names an owning **published** document, that document must
> name the ID.

Three concrete changes, each independently landable:

1. **Extend the existing gates to the third leg.** The generator-side gate
   (`PipelineDiagnosticsTests.EveryGeneratorDiagnosticIdIsClaimedInTheRegistryAndListedInTheDocsTable`)
   already does code ⇄ registry ⇄ `precompilation.md` for `HED70xx`. The runtime-side gate
   (`DiagnosticIdTests.ConstantsAndTheClaimedIdRegistryAgree`) does code ⇄ registry only. The new
   leg is **registry ⇄ owning document**, driven off the owner column, covering every block.
2. **Fix two defects in the gates themselves,** found while surveying and reported here rather than
   silently: (a) the generator gate's `ClaimedIds` helper is **unanchored** — it regexes
   `` `HED\d{4}` `` over the whole registry file, so a mere *cross-reference mention* anywhere in
   that document satisfies "claimed"; the runtime gate's copy is correctly section-anchored and
   row-anchored. (b) `DiagnosticIdTests`' own doc comment claims it is scoped to `HED0xxx`–`HED5xxx`;
   the code has no block filter and in fact gates all 82 constants. Neither is a behaviour defect,
   both are gate-credibility defects, and (a) is a real hole.
3. **Close the `HED71xx` and undocumented-ID gaps as documentation, not as exemptions.** The
   `70\d{2}` predicate on both sides means `HED7101`–`HED7103` are gated against no document; the 20
   IDs named in no published document are green everywhere. Each of the 20 gets either a doc row or
   an **explicitly named exemption** in the gate's exclusion set with a stated reason — the pattern
   `DiagnosticIdTests.RegistryOnly` already establishes ("named here rather than silently absent").

**Rationale.** This is the phase's answer to "how does a claim get *gated* rather than fixed", and
the diagnostics surface is where the answer is cheapest and strongest: the registry is already
normative, already machine-readable, already parsed by two tests, and already carries the owner
mapping. The gate that exists proves the pattern works — `HED7018`–`HED7024` landed across three
phases with zero documentation drift, while the ungated `HED1xxx` block drifted to 2-of-17 without
anyone noticing. That contrast is the argument.

**Alternatives rejected.**
- *A single central user-facing diagnostic index page, gated as the one document.* Attractive, and
  probably right eventually — but it is a new page (out of scope per Non-goals) and it would move
  the normative home of five blocks in one step. Recorded as a recommendation; the gate is designed
  so that adopting an index later is a change to the owner column, not to the gate.
- *Gate message text as well as presence.* Rejected for now: catalog `MessageFormat` is
  consumed-rows-only by Q6.3's ruling, so a text gate would either force the catalog's end-state or
  encode a partial one. Presence is the property whose absence actually hurt.
- *Require every ID to be documented, no exemptions.* Rejected — `HED0001`/`HED0002` are internal
  resolver/legacy-shape errors and `HED9001` is deliberately not public surface; forcing prose for
  them would produce documentation nobody needs and a gate people learn to route around.

### D5 — Gate option names and defaults against the shared table the LSP already uses

**Decision.** Phase 6's WI9 landed an LSP options-parity **completeness gate** (6 wired options, 11
documented exclusions) driven off the shared names/defaults table. The published option
documentation joins it as a third consumer:

- `editor-support.md`'s option table must list exactly the wired set, with the table's own defaults.
- Its prose exclusion list ("Options with no analysis meaning — `Encoder`, `RenderBudget`,
  `ValidateModelType`, `PrecompiledMismatchPolicy`, `EnableFileChangeCheck`, `Data`") must equal the
  gate's exclusion set. It currently names **6** of the gate's **11** while asserting *"every compile
  option that affects analysis has a key here"* — a checkable claim, stated over an incomplete list.
- The VS Code extension's `contributes.configuration` defaults must equal the same table's defaults,
  in the same gate. The mirror default is where the `Text`→`Html` flip could most easily have been
  missed; it was not missed, and the gate is what keeps the next one from being.

**Rationale.** Options are the highest-traffic factual surface in the docs (a name or a default is
either right or it costs the reader a debugging session) and the most mechanically checkable — a
name is a string and a default is a value. The table already exists and is already the source of
truth for two consumers; adding a third is a parser, not a design.

**Alternatives rejected.** *Review-only, because option docs rarely change.* Rejected on evidence:
this program alone changed one default, removed one MSBuild item metadata (`Name`), wired another
(`Precompile`), and fixed two `TemplateOptions` path properties. *Generating the doc table from the
table.* Rejected — a generated table loses the per-row prose ("so diagnostics match your host's
compile options") that is the reason a reader consults it; the gate checks the facts and leaves the
prose to the author.

### D6 — Gate public-API prose against the public-API golden that already exists

**Decision.** `TestTemplate/public-api-heddle.txt` and `public-api-heddle-language.txt` are already
checked-in approved public-surface goldens. A new gate extracts every fully-qualified type or member
name that the published docs assert *exists* (`[ZeroOutput]`, `TemplateOptions.Encoder`,
`PrecompiledMismatchPolicy`, `BindDefinition`, `Heddle.Models.Range`, …) and asserts each appears in
the golden. Direction is one-way by design: **doc-mentioned ⊆ public surface**. The reverse (every
public member is documented) is not asserted — that is a coverage goal, not a correctness gate, and
asserting it would red the suite over intentional plumbing.

**Rationale.** This catches the two failure shapes that actually occurred: a doc naming a member
that was removed (`Name` item metadata is the MSBuild analogue) and a doc silently not knowing about
an added one (`[ZeroOutput]`). One-way containment is the version that can land green today and
stays meaningful.

**Alternatives rejected.** *Bidirectional.* Rejected above. *Roslyn-based symbol resolution of every
backticked identifier in the docs.* Rejected — the false-positive rate on prose backticks (template
syntax, MSBuild properties, JSON keys, third-party names) makes it unlandable; the golden is a flat
list and the extraction can be restricted to `Heddle.`-qualified and attribute-bracket forms.

### D7 — Gate version consistency; the *bump itself* belongs to Q8.2

**Decision, two parts.**

**(a) Ownership.** The `2.0.0`→`2.1.0` bump — nine csproj `<Version>` elements, two `package.json`
files, `editors/vscode/src/extension.ts`'s `PINNED_VERSION`, the `lsp.yml` tool-install pin, the two
generator tests and eight Verify snapshots asserting `engineVersion: "2.0.0"` — belongs to **Q8.2's
work item, not to this phase.** The reasoning is atomicity: 2.1 *is* the declaration of the binary
break, and it must ship in the same landing as `MinSupportedSchemaVersion = 4` and the
old-schema manifest fixture. A version bump without the gate advertises a break that the metadata
does not enforce; the gate without the bump enforces a break the version does not declare. Splitting
them across two efforts creates a window in which the repo is in exactly the state finding P1
describes.

**(b) What this phase owns:** the *prose* deliverables that the bump requires and the *gate* that
keeps them honest — the CHANGELOG `## [2.1.0]` entry and compare link (the migration note is a
policy-rule-4 deliverable), the
[breaking-windows](../spec/common/breaking-windows.md) disposition, the as-shipped record entry in
[records.md](../spec/records.md), the schema-support-window paragraph in `precompilation.md` (which
today states no schema number anywhere, so `MinSupportedSchemaVersion = 4` has no documented home),
and the six docs prose version claims. Plus a **version-consistency gate**: every shipping
`<Version>`, both `package.json` versions, `PINNED_VERSION`, and the CHANGELOG's newest heading
agree. That gate lands *after* Q8.2's bump, so it lands green.

**Rationale.** The user's question — whose scope is the bump — has a mechanical answer: whichever
effort can make it atomic with the gate it declares. That is Q8.2. Everything textual about it is
this phase's, because Q8.2's work item is a code-and-packaging change and folding a CHANGELOG
migration note into it is how the last window's documentation ended up scattered.

Recorded for whoever executes Q8.2: **four of the nine `<Version>` elements are on non-shipping
projects and all nine are overridden by CI from the git tag**, so the honest fix is a single
`<VersionPrefix>` in `Directory.Build.props` (which today deliberately excludes Version) and the
deletion of the four pointless ones. That is a build change, so it is Q8.2's call — filed as
**Q8.11**.

**Alternatives rejected.** *This phase does the whole sweep because "it is mostly editing strings".*
Rejected — it would ship the version declaration separately from the mechanism it declares.
*Neither effort owns it; a third release-engineering task does.* Rejected as ceremony: the break has
exactly one owner already.

### D8 — Citations: convert to symbol anchors, gate the surface this phase owns, report the rest

**Decision.** Three rulings, because "stale citations" is three different populations:

1. **The documents this phase owns** (published `docs/*.md`, `docs/spec/common/**`,
   `docs/spec/records.md`) carry **zero** line citations today. A **citation gate** lands over
   exactly that surface: any `file:line` citation must resolve (file exists, line in range), and any
   `path` citation must resolve to an existing file. It lands green and its job is to keep that
   surface at zero debt. `docs/spec/README.md`'s index links are covered by the same gate.
2. **`docs/generator_plan/**` and `docs/research/**`** get an **advisory report** — the same checker,
   run in report mode, emitting the 86 beyond-EOF citations and the dangling-symbol list as a
   generated inventory file. Not a failing test, because this phase cannot fix them and a red gate
   over another owner's documents is a gate that gets disabled. The inventory is the handover
   artifact.
3. **The convention is restated, not invented:** [spec-conventions](../spec/common/spec-conventions.md)
   already says *"cite source locations as `path` + member name rather than bare line numbers where
   practical (line numbers drift); when a line number is load-bearing, state the anchor symbol next
   to it."* The survey shows why: 86 citations are provably stale, and spot-checking the 744
   "in-range" ones found silent wrong anchors that no line-count check can catch (`AqnSansVersion`
   cited at `:207-212`, actually at `:213`, with 207-212 now a doc comment). The rule is already
   right; it has no gate. Part 1 gives it one where it can be given one.

**Rationale.** The valuable half of citation hygiene is prevention, and the surface where prevention
is free is the surface that is already clean. Retro-fixing 830 citations in documents whose whole
purpose is to be a dated record would be a large, unreviewable, low-value diff — and 359 of them are
in evidence documents where the *old* line numbers are the historically accurate ones.

**Alternatives rejected.** *Sweep all 830.* Rejected above, and it is not this phase's edit surface.
*Strip line numbers from plan docs wholesale.* Rejected — destroys precision in the cases where the
anchor is still right, and it is an edit to documents owned by others. *No gate at all, just the
convention.* Rejected — that is the current state, and the current state produced 86.

### D9 — ~~Prose examples become executable by single-sourcing from phase 7's corpus~~ — **rejected**

**Decision (revised, 2026-07-26 — supersedes everything struck through below).** **D9 is rejected as
designed. Doc examples stay hand-written prose and this phase does not block on phase 7.**

Documentation has a different job from a test fixture: it *explains*, and byte-identity with a corpus
entry is not a property worth buying. A doc example is chosen for what it teaches — minimal, elided,
built up in stages — and a corpus entry is chosen for what it exercises. Forcing one artifact to
serve both makes each worse at its own job, and the coupling would be paid on every future doc edit.

Concretely, none of this happens: no `<!--@include: -->` from corpus templates into published pages,
no corpus intent rows added for the sake of doc examples, no expected-output goldens included into
prose, and **no dependency on phase 7 stage 0** — this phase's stage 5 loses its only hard external
dependency and can run whenever the earlier stages are done.

What replaces it is not a mechanism: doc examples are kept honest by **review**, under D11's
documentation-currency rule (a rule-bearing section carries the commit it was verified against), and
by [D10](../spec/common/cross-cutting-decisions.md#d10--documentation-authority-is-mapped-and-it-is-conditional) —
an unmarked, ungated prose claim is evidence of intent, not an authority, so a stale example cannot
order a code change. That is the honest posture: the examples are prose, and prose is reviewed.

The one thing the struck-through text got right, kept: `ScopeChannelDocExampleTests` **is** an
anti-pattern — a hand transcription of a doc example, two copies of one input with nothing forcing
them to agree. The answer is to **delete the false coupling**, not to formalise it into an include
mechanism. That deletion stays in scope.

<details>
<summary>Superseded design (kept for the record — do not implement)</summary>

**Decision.** Yes, some prose examples become tested fixtures — but the mechanism is
single-sourcing, not a second copy. Specifically:

- **The precedent to *not* follow** is `Heddle.Tests/ScopeChannelDocExampleTests.cs`, whose comment
  reads *"--- Verbatim from docs/custom-extensions.md ---"*. It is a hand transcription of a doc
  example into a test file. It works, it is better than nothing, and it is **structurally the same
  defect phase 7 exists to delete**: two copies of one input, nothing forcing them to agree. Its
  existence is evidence for this decision, not against it.
- **The mechanism:** a doc example that must be executable lives **once**, as a shared-corpus entry
  under phase 7's `src/TestCorpus/templates/`, with an intent row (`Tier`, `Render`, `Why`). The
  published document **includes** it — VitePress's `<!--@include: -->` directive already reads
  from disk at build time in this repo (`docs/benchmarks/*/summary-tables.md` is included exactly
  this way), so the doc and the test read the same bytes by construction. The document's *expected
  output* is the corpus entry's sibling golden, included the same way.
- **Interaction with phase 7, stated so the two do not duplicate:** phase 7 owns the corpus, the
  props file, the intent table and the gates. This phase is a **consumer**. It adds no corpus
  mechanism, no new tier, and no new gate over the corpus — it adds intent rows and doc includes.
  It therefore *depends on* phase 7 stage 0 (WI1–WI6) and must not start D9's work item before it.
  If phase 7 slips, this phase ships without D9 (see **Q8.10**) rather than hand-transcribing.
- **Scope is narrow and criteria are written down.** Executable-by-inclusion applies to a doc
  snippet only when **(i)** it is a complete template (not a fragment), **(ii)** the document states
  an expected output, and **(iii)** the shape is a language/emitter behaviour rather than an API
  usage illustration. C# host-code snippets stay prose — they are compiled by nothing and gating
  them means a compile harness, which is a different effort.

**Rationale.** "Make the examples tested" is the right instinct and the wrong implementation if done
by copying, because the failure it prevents (an example that stopped working) is replaced by a
failure it creates (an example that no longer matches the doc). The include mechanism is already in
the repo, already used, and makes the doc a consumer of a gated artifact — which is the same
single-artifact principle the whole program is about, applied one more level out.

**Alternatives rejected.**
- *Extract fenced blocks from markdown at test time and run them.* Rejected: it makes the markdown
  the source of truth for a test input, so a prose edit becomes a silent test-input change, and it
  needs a fence-annotation convention plus a parser. The corpus already solves ownership better.
- *Hand-transcribe, like `ScopeChannelDocExampleTests`.* Rejected above — it is the defect.
- *Do nothing; examples are illustrative.* Rejected: the program shipped a byte-changing literal
  formatter change (`"R"`→`G17`/`G9`) and a profile default flip, both of which can invalidate a
  documented output.

</details>

**Note on the struck-through rationale's strongest point**, since rejecting D9 does not make it
false: the program *did* ship a byte-changing literal formatter change and a profile default flip,
either of which can invalidate a documented output, and review is a weaker guard against that than a
gate would be. The ruling accepts that cost knowingly — the alternative was coupling every doc
example to a corpus entry, and D11's currency rule is what carries the residual risk. If a shipped
doc example is later found stale, that is a docs defect to fix, not evidence that this should be
reopened.

### D10 — Staging: N before M before mechanism, with the gates landing before the batch they guard

**Decision.** Six stages, each one reviewable landing (or a small group), in this order:

| Stage | Contents | Why here |
| --- | --- | --- |
| **0** | Class **N** corrections, one landing per document, source evidence quoted. The anchor case (`native-expressions.md` deviation 1 + the equality row's matching note) is landing 1. | The latent bugs. Every day this waits is a day a well-behaved agent can be misled. Independent of every gate. |
| **1** | D3's authority-convention amendment + D11's verification footers on the normative documents corrected in stage 0. | The convention narrowing must land *with* the corrections, not after: stage 0 is the proof that unconditional authority is unsafe, and the footer is what makes stage 0's verification durable. |
| **2** | The gates: D4 (diagnostics, all blocks), D5 (options), D6 (public API), D8 part 1 (citations) + part 2 (advisory report). | Gates land **before** the class-M batches they guard, so each batch is landed *green against its gate* rather than hand-checked. This is the phase-0 ordering lesson applied to prose. |
| **3** | Class **M**, in behaviour-change batches: (a) the build-error/fallback batch — the narrowed catch, `HED7020`, `HED7006`'s narrowing, `[ZeroOutput]`, the schema window; (b) the diagnostics batch — the 20 undocumented IDs and the `HED71xx` rows, landed green against D4; (c) the options/MSBuild batch — `Name` removal, `Precompile`, `FullPath`/`RenderPath`, the LSP table, landed green against D5. | One subject per landing. Each batch has a gate that already exists by stage 2. |
| **4** | The 2.1 prose deliverables + version gate (D7b). **Blocked on Q8.2's landing.** | Cannot state a shipped fact before it ships. |
| **5** | The documentation-currency rule (D11) as a standing testing/spec-convention rule, the deletion of `ScopeChannelDocExampleTests`' false coupling, and the README/records bookkeeping. **No longer blocked** — D9's executable examples were rejected (Q8.10). | Last for least urgency; the external dependency it used to carry is gone. |

**Rationale.** The ordering falls out of D1 (consequence-of-trust) with one addition worth stating:
the gates go *before* the bulk prose work, not after. The temptation is the reverse — fix everything,
then add a gate to hold it — and this program has already demonstrated the failure mode: phase 0's
D8 recorded a corpus-contribution rule as prose with no mechanism, and it was never backfilled
(that is phase 7's entire existence). A gate landed after the sweep guards the *next* drift; a gate
landed before it also verifies the sweep itself.

**Alternatives rejected.** *One landing per document (a doc-at-a-time sweep).* Rejected — mixes
severities, and a document like `precompilation.md` appears in three different batches for three
different reasons. *Gates last.* Rejected above. *Class M before class N because it is bigger.*
Rejected — inverts the whole triage.

### D11 — What cannot be gated is dated: a per-document verification marker, and a standing rule

**Decision.** A large share of documentation truth is not machine-checkable — *"registration is the
trust boundary"*, *"falling back is the right answer for stale data and a defect everywhere else"*,
*"the two roots cannot see each other"*, every rationale, every ordering claim, and most of the
prose that makes a document worth reading. For that residue the deliverable is **provenance, not
enforcement**:

- Each document whose claims this phase verifies gains a footer: *"Claims verified against source at
  `<commit>` (`<date>`). Gated claims are marked ✓."* The marker is what D3's narrowed authority
  convention keys on.
- An additive rule lands in [testing-standards.md](../spec/common/testing-standards.md) (via the
  amendments ledger, as E8/E9 did) with the shape: **a change that alters an observable behaviour
  names the documents that describe it in the same landing, and either updates them or records why
  not.** The named-documents list is derivable from the gates for diagnostics, options, API and
  version; for everything else it is a review checklist item, and the rule's value is that it
  exists to be pointed at in review.
- **Explicitly *not* gateable, recorded so nobody later mistakes the gap for an oversight:** prose
  accuracy about behaviour that no test observes; the accuracy of rationales and design arguments;
  overstatement ("all", "every", "never", "exactly one") except where the quantity is machine-
  countable; the correctness of the 744 in-range line citations; whether a document's *omissions*
  matter; and the ordering/emphasis choices that make prose useful or misleading without any
  individual sentence being false.

**Rationale.** The program's lesson is that unenforced claims drift — but the corollary people skip
is that *most claims cannot be enforced*, and pretending otherwise produces either a gate that
checks something trivial and calls it coverage (finding 5's original `"R"` round-trip suite is the
in-tree example) or a gate people disable. Dating a verification is honest: it says what was
checked, when, and against what, and it converts "is this true?" into "is this older than the last
behaviour change?" — a question a reviewer can answer.

**Alternatives rejected.** *Natural-language assertions checked by an LLM in CI.* Rejected —
non-deterministic gate, no stable failure, and this repo's gates are all deterministic.
*Nothing; rely on review.* Rejected — that is the state that produced the anchor defect, twice
(phase 4 found it, phase 4's audit found it again, and it is still there).

## Dependencies & ordering

- **Depends on phases 0–6 landed and the six audits** — their behaviour changes and findings are the
  input. Not gating in a build sense; the plan cannot be *executed* against a different code state.
- **Stage 4 is blocked on Q8.2's work item** (the 2.1 bump + `MinSupportedSchemaVersion = 4` +
  old-schema manifest fixture). Stages 0–3 and 5 are unblocked.
- **Stage 5 has no external dependency.** D9's single-sourcing was rejected (Q8.10), so the former
  block on phase 7 stage 0 is void: stage 5 runs whenever stages 0–4 are done, in either order
  relative to phase 7.
- **Stage 3(b) is soft-blocked on Q8.1's work item** for one row only: `HED7025` gets its
  `precompilation.md` and registry rows *from Q8.1's landing*, per that ruling. This phase documents
  it only if it exists; the D4 gate is designed so an allocated-but-unimplemented ID is visible
  (today it is invisible to all four gates, which is how `HED7025` sits claimed in prose with zero
  surfaces).
- **Concurrency constraint:** two agents are working in `docs/generator_plan/**`. This phase touches
  exactly `README.md` (one table row) and `open-questions.md` (its own entries) there. Everything
  else in that directory is inventory, not edit surface.
- **Internal ordering** is the stage order in D10. Within stage 0 the landings are independent and
  can go in any order; landing 1 is the anchor case by priority, not by dependency. Within stage 2
  the four gates are independent.

## Back-compat / impact

- **Engine and generator surface:** none. No file under `src/Heddle/**` or `src/Heddle.Generator/**`
  is modified. New files are test-side gates.
- **Rendered bytes:** none. **Public API:** none. **Diagnostic IDs:** none claimed.
- **Published documentation:** changed, deliberately and visibly. Several corrections change what a
  reader is told the engine does — most consequentially that a template with an ambiguous
  reference/value equality is a compile error rather than a silent `object.Equals`, and that a
  template the generator cannot emit now **fails the build** instead of degrading quietly. Both were
  already true in code; only the description changes.
- **Docs site:** `cd docs && npm run docs:build` must pass on every landing (verified green at
  baseline, `c415943`). With D9 rejected there is no mechanism risk left here — every change is
  prose in a page the site already builds.
- **Suite:** four new gate tests plus one advisory report generator. All are markdown/file readers;
  cost is milliseconds. They must work on every TFM leg — `DiagnosticIdTests.ReadSpec`'s
  `[CallerFilePath]` trick is the established pattern for reaching a doc from a test without probing
  the output directory, and the new gates follow it.
- **Not window-relevant** ([breaking-windows](../spec/common/breaking-windows.md)) — with one
  exception that is *recorded here and owned by Q8.2*: the 2.1 binary break itself. This phase writes
  its documentation; it does not ship it.

## Risks & mitigations

- **The sweep introduces a behaviour bug by "correcting" a document in the wrong direction.** This is
  the phase's own worst-case, and it is the same mechanism as the anchor defect. Mitigations: D2's
  three-outcome rule with escalation as the *default* for any doubt; every class-N landing quotes the
  deciding source line; D3's narrowing means a corrected sentence is authoritative only once marked
  verified.
- **A gate that is green because it checks the wrong thing.** Already demonstrated twice in this
  tree (finding 5's `"R"` suite; the unanchored `ClaimedIds` regex). Mitigation: every gate in stage
  2 lands with a **mutation demonstration** in its done-when — delete a documented ID row, mis-state
  a default, remove an API mention, break a citation — and the gate must redden with the offending
  name in the message. A gate with no demonstrated red is not done.
- **Concurrent edits to `docs/generator_plan/**`.** Mitigation: the two-file edit surface stated in
  Non-goals and repeated in the work items; the 86 stale citations are handed over as an inventory.
- ~~**Phase 7 slips and D9 has nowhere to single-source into.**~~ Void — D9 is rejected (Q8.10) and
  this phase no longer consumes the corpus.
- **Q8.2 lands the bump with its own CHANGELOG text**, duplicating stage 4. Mitigation: this plan's
  D7 states the split explicitly, and stage 4's work item is written as *"write the entry Q8.2's
  landing references"* — the sequencing is one direction only.
- ~~**VitePress `@include:` cannot reach outside `docs/`.**~~ Void — no includes are added (Q8.10).
  The question was never answered and does not need to be.
- **The "documented exemption" escape hatch in D4 gets used as the default.** Mitigation: the
  exemption set requires a stated reason per entry (the `RegistryOnly` precedent) and its size is
  asserted, so growing it is a visible one-line diff — the same device phase 7's D5 uses for its row
  count.
- **Scope creep into a documentation rewrite.** Mitigation: the Non-goals list, and D1's rule that a
  missing-page finding is *recorded*, not built.

## Success criteria

1. **No published document designated normative by the authority convention or by the claimed-ID
   registry contains a statement contradicted by the implementation**, over the claim set enumerated
   in this plan's inventory. Each such statement's correction cites the deciding source location.
2. **The anchor defect is closed**: `native-expressions.md`'s deviation 1 and the equality row of the
   operator table describe the `IsReferenceish`-guarded behaviour, and a test named in stage 0's
   landing pins that `string == int` is `HED1008` on **both** tiers — so the doc's claim is gated,
   not merely rewritten. *(Whether such a test already exists is an inventory question; if it does,
   the landing cites it rather than adding one.)*
3. **The authority convention is narrowed and recorded** as a cross-cutting decision with a ledger
   entry, and every document it designates carries a verification footer naming a commit and a date.
4. **Every claimed diagnostic ID whose registry row names a published owning document is named in
   that document**, or appears in a gate exclusion set with a stated reason. The gate asserts both
   directions and covers **all** blocks — `HED0xxx`–`HED5xxx`, `HED70xx` **and** `HED71xx`. The
   20-ID gap is zero-or-explicitly-exempted.
5. **The two gate defects are fixed**: the generator-side `ClaimedIds` helper is section- and
   row-anchored like the runtime-side one, and `DiagnosticIdTests`' doc comment matches its actual
   (unfiltered) scope.
6. **Option names and defaults are gated** across the shared table, `editor-support.md`, and the VS
   Code extension's `contributes.configuration`; the doc's exclusion list equals the gate's, so the
   *"every compile option that affects analysis has a key here"* claim is true and checked.
7. **Doc-mentioned public API ⊆ the public-API golden**, gated, with the gate's extraction rules
   written down.
8. **The citation gate is green over the surface this phase owns** and the advisory report over
   `docs/generator_plan/**` + `docs/research/**` is generated and handed over, listing the 86
   beyond-EOF citations and the dangling-symbol set.
9. **Every behaviour change in the program's inventory is described correctly in every published
   document that describes it** — the build-error posture, the editor profile default, the ambiguity
   error, the literal-format change, `HED7006`'s narrowing, the `Name`/`Precompile` metadata change,
   the `FullPath`/`RenderPath` fixes, `[ZeroOutput]`, the `BindDefinition` overload, and the schema
   window — each verified against source and marked.
10. **The 2.1 prose deliverables exist and agree with what shipped**: CHANGELOG entry + compare link,
    breaking-windows disposition, as-shipped record entry, the schema-support-window paragraph in
    `precompilation.md`, and the version gate green.
11. **Every gate has a demonstrated red** (mutation, per Risks) recorded in its work item.
12. **The standing rule exists**: the documentation-currency amendment and its ledger entry are
    landed, the README phase row points here, and `docs/spec/records.md` carries this phase's
    entries.
13. **`cd docs && npm run docs:build` passes** on every landing.

## Validation scenarios

- **The anchor trap, executed.** Read deviation 1 as an instruction and delete the
  `IsReferenceish(left) && IsReferenceish(right)` guard locally → tests redden (or, if they do not,
  that is criterion 2's missing pin, found the right way). Restore, correct the sentence, re-run:
  green, and the sentence now describes what the guard does.
- **Undocumented-ID gate.** Delete `HED5019`'s mention from `language-reference.md` → the D4 gate
  reddens naming `HED5019` and the owning document. Add a registry row for a fictional `HED1099`
  with `native-expressions.md` as owner → the gate reddens naming the missing doc row.
- **Registry cross-reference hole (the gate defect).** Add `` `HED7099` `` as a *prose mention* in
  `cross-cutting-decisions.md` with no table row → today the generator-side gate accepts it as
  claimed; after the anchoring fix it does not.
- **Allocated-but-unimplemented ID.** With `HED7025` claimed in the registry and absent from code →
  the gate reports it (today all four gates are silent, which is the current state of `HED7025`).
- **Option default drift.** Change the VS Code extension's `heddle.compile.outputProfile` default
  back to `text` → the D5 gate reddens naming the setting and both expected values. Remove one
  exclusion from `editor-support.md`'s prose list → reddens naming it.
- **Removed API mention.** Add a sentence documenting `Name` item metadata (removed by phase 5) →
  the D6 gate reddens if the name is `Heddle.`-qualified; the MSBuild-metadata case is prose-only and
  is caught by stage 3(c)'s review, which is a deliberate and stated gate limit.
- **Broken citation.** Add `[TemplateOptions.cs:99999](…)` to `precompilation.md` → the D8 gate
  reddens with the file, cited line, and actual length. Point a link at a deleted path → reddens.
- **Version skew.** Bump one shipping csproj to `2.1.1` → the D7b gate reddens naming every
  disagreeing location, including `PINNED_VERSION`.
- **Doc example drift (stage 5).** Edit a corpus template that a document includes → the document's
  rendered example changes in the same build, and the corpus entry's own golden gate reddens if the
  output moved. Pre-phase, a doc example and its transcribed test could disagree with nothing
  noticing.
- **Docs build.** `cd docs && npm run docs:build` after each landing.

## Open questions

Recorded in [open-questions.md](open-questions.md#post-implementation-questions-opened-2026-07-26)
under the post-implementation section; restated here with the default that is operative until ruled.

- **Q8.9 — Where does the narrowed authority convention live, and does it apply retroactively to the
  landed phases' D-items?** D3 places it in
  [cross-cutting-decisions.md](../spec/common/cross-cutting-decisions.md) as a new cross-cutting
  decision, because it outlives this program, and the program README's *Authority convention* bullet
  becomes a pointer. The retroactive half is the real question: several landed phase D-items resolved
  drift *by* citing `native-expressions.md`, and if the convention is narrowed those citations become
  weaker evidence than they were when ratified. **Default:** land it as a cross-cutting decision,
  **non-retroactive** — already-ratified D-items stand as ratified, and the narrowing governs future
  alignments only. Re-auditing seven phases' evidence chains is a bigger effort than the anchor
  defect warrants, and every one of those D-items also carries source evidence.
- **Q8.10 — If phase 7 has not landed when stages 0–4 are done, does D9 ship, slip, or transcribe?**
  **Ruled (user, 2026-07-26): neither — D9 is rejected outright.** Docs stay refined, separate prose;
  byte-identity with a corpus entry is not a property worth buying, because a doc example is chosen
  for what it teaches and a corpus entry for what it exercises. So the question dissolves rather than
  resolving: there is nothing to slip, and hand-transcription remains the anti-pattern it always was —
  `ScopeChannelDocExampleTests`' false coupling is deleted, not formalised. See D9 for the full
  disposition and for the one cost this knowingly accepts.
- **Q8.11 — Should the nine `<Version>` elements be centralised into `Directory.Build.props` as part
  of the 2.1 bump?** Four of the nine are on non-shipping projects; all nine are overridden by CI
  from the git tag (`dotnet.yml`), and `Directory.Build.props` currently excludes Version *by an
  explicit comment*. So the nine are documentation of the release line, hand-maintained, with no
  lockstep test — the duplication class this program spent phase 6 deleting from source.
  **Default: yes** — hoist a single `<VersionPrefix>`, delete the four non-shipping elements, and
  point D7b's gate at the one property. It is a build change, so it lands inside **Q8.2's** work item
  rather than this one; if that work item declines it, D7b's gate simply asserts the nine agree
  instead of asserting one exists.
- **Q8.12 — Who fixes the sample that still passes the removed `Name` item metadata?**
  `samples/codegen-t4-successor/CodegenT4Successor.csproj:45` carries
  `Name="BuildReport"`; `Heddle.Generator.props` no longer reads it, so the sample silently
  registers under its filename key instead of its intended name — and it is golden-checked, so the
  golden encodes the wrong outcome. Not prose, so not this phase's to edit. **Default:** fold into
  the post-audit work-item queue beside Q8.1/Q8.5 as a one-line fix plus a golden re-ratification,
  and have this phase's inventory hand it over. It is small enough that the risk is forgetting it,
  which is what the register entry prevents.

## External grounding

Verified against the tree at `c415943` while planning; every count re-derived, not taken from prior
documents.

| Claim | Source |
| --- | --- |
| Deviation 1 is false: the `object.Equals` fallback is guarded, so `string == int` errors on both tiers | [native-expressions.md](../native-expressions.md) *Deviations from C#* item 1 and the operator table's equality row, against `IsReferenceish(left) && IsReferenceish(right)` at [NativeExpressionCompiler.cs:800](../../src/Heddle/Runtime/Expressions/NativeExpressionCompiler.cs) (predicate at `:1051`) |
| Three further **false** normative claims in the same document: deviation 6 (user-defined operators not honored for `&`/`^`/`\|`, `<<`/`>>`, or any unary); the shift row's `int`-right-operand rule (any integral is accepted and converted); the lifted-operands claim that equality lifts (numeric paths only) | `NativeExpressionCompiler.cs:837` (bitwise `FailBinary`, no operator fallback), `:705-706` (shift `FailBinary`), `:546-584` (unary `FailUnary` before any factory call) vs the honored arms' `try { … } catch (InvalidOperationException)` at `:668-675`, `:738-745`, `:794-808`; `:715-717` (`ConvertTo(right, int)`); `:794-808` + `:817-818` (no lifted equality/bitwise for non-numeric `T`/`T?`) |
| The generator's shared tables already describe those divergences **correctly**, beside the verdicts that encode them — so the most accurate description of native-expression semantics in the tree is code, not the normative document | `src/Heddle/Language/Expressions/NativeOperatorRules.cs:128-132` (shift widening), `:176-181` (`bool` vs `bool?` equality → `HED1008`), `:199-205` (unguarded bitwise pair), `:160-161` (`null == null` folds) |
| Seven further imprecise claims: the `abs`/`min`/`max`/`round`/`floor`/`ceil` numeric coverage; *"never throw at render"*; the overload-ranking paragraph; the registry-freeze trigger; the relational row's `null`-literal gap; the sandbox emit enumeration; the visibility wording | `BuiltInFunctions.cs:205-212` (`floor`/`ceil` are `double`/`decimal` only) with `OverloadRank.cs:124,140-152,245-247` (`int` ties → `Ambiguous` → `HED1013`); `BuiltInFunctions.cs:169-175` (`range`'s sanctioned throw); `NativeExpressionCompiler.cs:56` (the sole `Freeze()` call site — a compile with no native expression never freezes); `:723-724` (`X < null` is `HED1008`, not `false`); `:263-265` (`Expression.ArrayIndex` is not a getter invocation); `Language/Members/MemberFacts.cs:44-48,63-85` |
| The visibility wording is **circularly sourced**: `MemberFacts` cites `native-expressions.md`'s sandbox section as the origin of its "public-or-internal" phrasing, which that document no longer contains, while `breaking-windows.md` lists a wording update to it as a pending window trigger | `MemberFacts.cs:44-48`; `grep 'public.or.internal' docs/*.md` → no hits; [breaking-windows](../spec/common/breaking-windows.md) member-visibility candidate row |
| The authority convention makes that document outrank both implementations | [README — Cross-phase notes, *Authority convention*](README.md) |
| Class M totals across the published set: **2** factually wrong (attributable to this program), **30** stale/incomplete, **4** overstated-and-checkably-false | Per-document audit of `architecture.md`, `precompilation.md`, `language-reference.md`, `csharp-api.md`, `custom-extensions.md`, `editor-support.md`, `built-in-extensions.md`, `patterns.md`, `getting-started.md`, `building.md`, `docs/README.md`, `README.md`, the `coming-from-*` pair and `syntax-highlighting.md` |
| Wrong #1: `TemplateOptions.FullPath` described as *"a plain string concatenation … with no path separator … and is not the resolved read path"* — wrong on both clauses after phase 6's fix, and contradicting `getting-started.md:104` | `docs/csharp-api.md:221-222` vs `src/Heddle/Data/TemplateOptions.cs:166-167` (`Path.Combine`) and `src/Heddle/Runtime/FileReader.cs:35-40` (*"The file this reader opens: `TemplateOptions.FullPath`"*) |
| Wrong #2: directive-line trimming *"You get this for free … there is nothing extra to implement"* — contradicted 450 lines later **in the same file** by the `[ZeroOutput]` row (*"without it, the block is removed dynamically and kept as output when precompiled"*) | `docs/custom-extensions.md:66-68` vs `:512` |
| Two overstatements `precompilation.md` disproves itself: *"an unset property never causes a gauntlet mismatch"* (`:122-123` says a `Native` manifest is refused by the hosted arms' `FullCSharp` fingerprint, and `Native` is what an unset property yields); *"every class above still degrades silently"* (`:193` `CaseMismatch` is never a failure, `:194` duplicate-key **throws**) | `docs/precompilation.md:67-69`, `:196-197` vs `:122-123`, `:193-194`; `src/Heddle/Precompiled/HeddleBuildOptions.cs:34` |
| Two overstatements in `language-reference.md`: *"**exactly** the ones the compiler removes"* (omits the `[ZeroOutput]` requirement on the precompiled tier) and *"Region defaults **and** overridden fills precompile natively"* (a private-region fill is now `HED7024`, a build error) | `docs/language-reference.md:1203-1205`, `:968` |
| The `HED7022`/`HED7023`/`HED7024` cross-references are missing from every document that owns the affected surface | `language-reference.md:252`, `:629-631`, `:962-963`, `:717-722`; `built-in-extensions.md:556-557`, `:667`; `patterns.md:391-392`; `custom-extensions.md` (no `HED7023` mention beside `[Prop]` type resolution) |
| Escalation #1 — a `bool`/`bool?` bitwise pair produces an id-less *"Error while compiling"*, not a diagnostic | `NativeExpressionCompiler.cs:817-818` (unguarded `BitwiseFactory`) escaping to `Runtime/HeddleCompiler.cs:118-126` |
| Escalation #2 — `floor(3)` is `HED1013`; the fix is the already-filed next-window betterness candidate | `BuiltInFunctions.cs:205-212`; `OverloadRank.cs:245-247`; [breaking-windows](../spec/common/breaking-windows.md) betterness row (82 become bindable, 62 stay ambiguous) |
| Escalation #3 — a shipped sample still passes the removed `Name` item metadata | `samples/codegen-t4-successor/CodegenT4Successor.csproj:45`; `src/Heddle.Generator/build/Heddle.Generator.props:14-19` reads only `Key` and `Precompile` |
| `MinSupportedSchemaVersion` is **still 1** — Q8.2's raise to 4 has not landed, so stage 4 is genuinely blocked and any doc written today saying "4" would itself be wrong | `src/Heddle/Precompiled/PrecompiledSchema.cs:18` |
| Only four files were revised by the program's doc touches, which is why the debt distributes as it does | `precompilation.md`, `custom-extensions.md`, `editor-support.md`, `editors/vscode/package.json` |
| The registry designates a per-block **owning document**, and that column is ungated | [cross-cutting-decisions.md](../spec/common/cross-cutting-decisions.md#claimed-diagnostic-ids-registry) rows for `HED1001`–`HED1017` (native-expressions.md), `HED2xxx`/`HED3xxx`/`HED4001`–`HED4002` (built-in-extensions.md), `HED4004`/`HED5001`–`HED5018` (language-reference.md), `HED7xxx` (precompilation.md) |
| 82 shipped IDs; catalog ⇄ ids-class bijection; registry has one extra (`HED9001`, whitelisted) | `src/Heddle/Data/HeddleDiagnosticIds.cs` (82 consts), `src/Heddle/Data/HeddleDiagnosticCatalog.cs` (82 rows), registry (83 expanded) |
| **20 of 82 IDs are named in no published document**; `HED1xxx` is 2-of-17 (`HED1004`, `HED1014` only); `HED1008` is undocumented | Per-ID grep of `docs/*.md`. Undocumented: `HED0001`, `HED0002`, `HED1001`–`HED1003`, `HED1005`–`HED1013`, `HED1015`–`HED1017`, `HED4005`, `HED5007`, `HED5010` |
| Generator-side gate covers `HED70xx` code ⇄ registry ⇄ `precompilation.md`, both directions on the docs leg | `src/Heddle.Generator.Tests/PipelineDiagnosticsTests.cs:250` — filters `id.StartsWith("HED70")` on the code side and `` ^\| `HED(?<id>70\d{2})` `` on the docs side, scoped to `## Build‑time diagnostics` |
| Runtime-side gate covers all 82 constants ⇄ registry, and **no document** | `src/Heddle.Tests/DiagnosticIdTests.cs:99`; `ClaimedIds` is anchored to `## Claimed diagnostic IDs (registry)` and to `` ^\| `HEDaaaa` `` rows; `RegistryOnly = { "HED9001" }` |
| Gate defect (a): the generator-side `ClaimedIds` is unanchored, so a prose cross-reference counts as a claim | `PipelineDiagnosticsTests.cs` `ClaimedIds` — whole-file regex, contrast `DiagnosticIdTests.cs:133-159` |
| Gate defect (b): `DiagnosticIdTests`' doc comment claims `HED0xxx`–`HED5xxx` scope; the code has no block filter | `DiagnosticIdTests.cs:88-96` vs `:102-105` |
| `HED71xx` is gated against no document (the `70\d{2}` predicate excludes it on both sides); it is absent from the build-time table | `PipelineDiagnosticsTests.cs:294-308`; `precompilation.md` table rows end at `HED7024`, `HED7101`–`HED7103` appear only in prose (`:146`, `:155`, `:193`) |
| `HED7025` is allocated in prose with **zero** surfaces and is invisible to all four gates (every gate is code-driven) | `open-questions.md` Q8.1; `phase-6-diagnostics-utilities.md:912`; no constant, catalog row, descriptor or registry row exists |
| `HED7018`–`HED7024` are fully triple-covered — the pattern D4 generalises | Registry rows `:168-174`; `precompilation.md:273-279`; descriptors + raise sites in `src/Heddle.Generator/**` |
| All 830 file:line citations sit in `docs/generator_plan/**` (471) and `docs/research/**` (359); **86 cite past EOF**; published docs, `docs/spec/**` and `docs/plan/**` carry **zero** | Scripted scan of 102 markdown files (excluding `node_modules`). Worst clusters: `DocumentShaper.cs` (119 lines) cited ~30× incl. `:375-396`; `PieceWriter.cs` (24 lines) 8×; `HeddleCompiler.cs` (1690) cited `:1963-1965` |
| "In range" ≠ correct: silent wrong anchors exist that no line check catches | `phase-3-binding-layer.md:645` cites `AqnSansVersion` at `PrecompiledGauntlet.cs:207-212`; the symbol is at `:213` and `207-212` is now a doc comment |
| Dangling symbols: `DocumentsCache` (10 mentions, 0 in source), `ReflectionHelper.CSharpTypes` (6, 0 in source, and `ReflectionHelper.cs:32-49` is now `Reconfigure()`); `AqnSansVersion` is **not** dangling (25 live sites) | Greps over `src/`; `src/Heddle/Helpers/CSharpTypeNames.cs` is the replacement |
| 2 genuinely broken doc links, both to the deleted `src/Heddle.Performance/GoldenCorpus` | `docs/benchmarks/2026-07-25/index.md:109`, `:333` — owned by the benchmark effort |
| Nine `<Version>` elements, no central property, all CI-overridden from the tag; 13 files must change for 2.1.0 and 5 more are coupled | The nine csproj; `Directory.Build.props` (header comment excludes Version deliberately); `.github/workflows/dotnet.yml:97-101`; `editors/vscode/src/extension.ts:13` `PINNED_VERSION`; `.github/workflows/lsp.yml:59`; `PipelineDiagnosticsTests.cs:104` + `HeddleGeneratorTests.cs:30` + 8 `Snapshots/*.verified.txt` asserting `engineVersion: "2.0.0"`; `docs/building.md:38`, `docs/README.md:19`, `coming-from-razor.md:8`, `coming-from-liquid.md:8` |
| CHANGELOG has one entry (`## [2.0.0] - 2026-07-19`) and no `## [Unreleased]`, so 2.1 needs a heading and a compare link | `CHANGELOG.md:8`, `:134` |
| `precompilation.md` states no schema version number anywhere, so `MinSupportedSchemaVersion = 4` has no documented home; the code is `Min = 1`, `Max = Current = 5` | `grep -n schema docs/precompilation.md` → `:154`, `:191` only; `src/Heddle/Precompiled/PrecompiledSchema.cs:18,22,25` |
| The CHANGELOG's own 2.0.0 text still describes the superseded posture — *"anything not yet emittable degrades safely to the dynamic path"* — against phase 5's per-template `HED7020` error | `CHANGELOG.md` *Build-time precompilation* bullet vs `precompilation.md:200-205` |
| `editor-support.md` **is** already correct on the `Text`→`Html` flip (phase 6 updated it), and the VS Code extension's mirror default is `html` — so this surface is a gate opportunity, not a defect | `docs/editor-support.md:84`, `:94-99`; `editors/vscode/package.json` `heddle.compile.outputProfile` default `html` |
| `editor-support.md`'s exclusion prose names **6** options while the LSP parity gate documents **11**, under the claim *"every compile option that affects analysis has a key here"* | `docs/editor-support.md:75-79` vs phase 6's WI9 record (6 wired, 11 documented exclusions) |
| `custom-extensions.md:591` still states the pre-narrowing `HED7006` trigger, while `precompilation.md:262` carries the narrowed one | Compare the two rows |
| Hand-transcription of doc examples into tests already exists and is the anti-pattern D9 (as revised) deletes | `src/Heddle.Tests/ScopeChannelDocExampleTests.cs` — *"--- Verbatim from docs/custom-extensions.md ---"* |
| VitePress `<!--@include: -->` already reads a generated file from disk at build time in this repo | `docs/.vitepress/config.mts` `srcExclude` note on `benchmarks/*/summary-tables.md`; `docs/spec/cross-stack-benchmarks/phase-7-consolidated-report/report-assembly.md` |
| `docs/spec/**` is unpublished, so a "user-facing" claim there is contributor-facing | [cross-cutting-decisions D9](../spec/common/cross-cutting-decisions.md#d9--spec-pages-stay-unpublished); `srcExclude: ['spec/**', …]` |
| The amendments mechanism and its two existing additive precedents (E8 posture, E9 single-sourcing) | [spec-conventions §Amendments](../spec/common/spec-conventions.md#amendments-during-implementation); [records.md ledger](../spec/records.md#cross-spec-amendments-ledger) |
| Docs site builds green at baseline | `cd docs && npm run docs:build` → exit 0 at `c415943` |

---

## Work items

**Stage 0 — class N: the documents that outrank code (D1, D2). One landing per claim group, so a
latent-bug fix is never reviewed in the same diff as a wording improvement.**

- **WI1 — The anchor case.** Correct `native-expressions.md`'s deviation 1 **and** the operator
  table's equality-row note to describe the `IsReferenceish`-guarded behaviour, naming `HED1008` as
  the outcome for a reference/value mix. **Done when** both statements match
  `NativeExpressionCompiler.cs:800`'s predicate; a test pins that `string == int` is `HED1008` on
  **both** tiers (cited if it exists, added if it does not); the deleted-guard rehearsal (validation
  scenarios) has been executed once and recorded; and the landing quotes the deciding source line.
- **WI2 — The other three false claims in `native-expressions.md`.** Correct deviation 6 (name the
  operator classes where user-defined operators *are* honored and the three where they are not), the
  shift row's right-operand rule (any integral, converted to `int` — and note this is a deviation
  *from* C#, so it belongs in the deviations list, which currently omits it), and the
  lifted-operands section (lifting is numeric-path only; a `bool`/`bool?` equality is `HED1008` and
  the bitwise pair has no diagnostic at all, per the escalation above). Add the three missing
  deviations the audit found — the shift widening, no lifted equality/bitwise for non-numeric
  `T`/`T?`, and `null == null` folding to a constant rather than erroring — so the list stops
  claiming to be exhaustive while omitting live divergences. **Done when** each corrected sentence
  cites the deciding source location, and the three "do not fix the code to match this" cases are
  marked in the document as *deliberate, window-gated* rather than left readable as bugs.
- **WI3 — The remaining normative claim set.** The seven imprecise `native-expressions.md` claims
  (the built-in numeric table, *"never throw at render"* against `range`'s sanctioned throw, the
  overload-ranking paragraph, the registry-freeze trigger, the relational row's `null`-literal gap,
  the sandbox emit enumeration's missing array-index arm, the `[Hidden]`/visibility wording that
  `MemberFacts` cites *back* to this document), plus `language-reference.md` and
  `built-in-extensions.md` for their registry-owned diagnostic blocks and `precompilation.md` for the
  identity/staleness/fallback contracts and its two self-contradicted absolutes. Each claim resolves
  under D2's three outcomes. **Done when** every claim is dispositioned (corrected / escalated as a
  `Q8.<n>` / recorded as a known limitation), no disposition is "left as-is with a doubt", the
  overload-ranking paragraph is reconciled against the shared `OverloadRank` core and phase 4's
  betterness measurement (0 of 480 winners change, 82 become bindable, 62 stay ambiguous), and the
  circular citation between `MemberFacts.cs` and the sandbox section is broken in one direction.

**Stage 1 — the convention and the marker (D3, D11).**

- **WI4 — Narrow the authority convention.** New cross-cutting decision in
  `cross-cutting-decisions.md` with a ledger entry in `records.md` carrying WI1–WI2 as its evidence; the
  program README's *Authority convention* bullet becomes a pointer to it. **Done when** the decision
  is landed, the ledger entry names WI1's and WI2's evidence, and criterion 3 holds. *(Q8.9 governs placement
  and retroactivity; the default is operative.)*
- **WI5 — Verification footers.** Add the dated, commit-pinned verification footer to every document
  stage 0 touched, with the ✓ convention for gated claims defined once. **Done when** each footer
  names a commit reachable in history and a date, and the ✓ set is exactly the set some gate covers.

**Stage 2 — the gates. Each lands with a demonstrated red (Risks).**

- **WI6 — Diagnostics gate, all blocks (D4).** Add the registry ⇄ owning-document leg driven off the
  registry's owner column, covering `HED0xxx`–`HED5xxx`, `HED70xx` and `HED71xx`; anchor the
  generator-side `ClaimedIds` helper; fix `DiagnosticIdTests`' doc comment; introduce the
  named-exemption set with a per-entry reason and an asserted size. **Done when** criteria 4 and 5
  hold, the three diagnostics validation scenarios all redden as described, and the exemption set's
  size assertion is in place.
- **WI7 — Options gate (D5).** Extend the LSP options-parity gate to `editor-support.md`'s table and
  prose exclusion list and to the VS Code extension's `contributes.configuration` defaults. **Done
  when** criterion 6 holds and the two option validation scenarios redden.
- **WI8 — Public-API gate (D6).** Doc-mentioned `Heddle.`-qualified names and attribute-bracket forms
  ⊆ the public-API goldens, with the extraction rules documented in the test. **Done when**
  criterion 7 holds and the removed-API scenario reddens; the known gate limit (MSBuild metadata and
  other non-CLR names are review-only) is stated in the test's doc comment.
- **WI9 — Citation gate + advisory report (D8).** Failing gate over published docs +
  `docs/spec/common/**` + `docs/spec/records.md`; report mode over `docs/generator_plan/**` and
  `docs/research/**` emitting the 86 beyond-EOF citations and the dangling-symbol set. **Done when**
  criterion 8 holds, the broken-citation scenario reddens, and the report is handed to the phase-plan
  and research owners as an inventory (not as edits).

**Stage 3 — class M, in behaviour-change batches, each landed green against its stage-2 gate.**

- **WI10 — Batch (a): the build-error and fallback posture.** The narrowed `catch` / per-template
  `HED7020` error (a project that silently degraded now **fails its build** — this is the batch's
  headline and it belongs in the CHANGELOG-adjacent prose too), `HED7006`'s narrowing in
  `custom-extensions.md:591`, `[ZeroOutput]`, the per-carrier `BindDefinition` overload, the
  `PrecompiledSchema` 2→5 movement and the schema-support-window paragraph, and the `TemplateOptions.FullPath`/`RenderPath`
  fixes. **Done when** every listed change is described correctly in every published document that
  describes the area, each verified against source and marked per WI5.
- **WI11 — Batch (b): the diagnostics prose.** Rows/prose for the 20 undocumented IDs (or their
  named exemptions) and for `HED71xx`; `HED7025` only if Q8.1's work item has landed it. **Done
  when** WI6's gate is green with a zero-or-exempted gap and criterion 4 holds.
- **WI12 — Batch (c): options, MSBuild items, and the editor.** `Name` item metadata removal,
  `Precompile` wiring, the LSP option table and exclusion list, the editor default profile's
  documentation (already correct — verified and marked, not rewritten). **Done when** WI7's gate is
  green and criterion 6 holds.

**Stage 4 — the 2.1 documentation. Blocked on Q8.2's work item (D7).**

- **WI13 — 2.1 prose + version gate.** The CHANGELOG `## [2.1.0]` entry and compare link (carrying
  the policy-rule-4 migration note for the `PrecompiledExtensionBinding` break and the
  `MinSupportedSchemaVersion = 4` rejection), the breaking-windows disposition, the as-shipped
  record entry in `records.md`, the schema-support-window paragraph in `precompilation.md`, the six
  docs prose version claims, and the version-consistency gate over the shipping versions, both
  `package.json`s, `PINNED_VERSION` and the CHANGELOG heading. **Done when** criterion 10 holds, the
  version-skew scenario reddens, and the entry describes what Q8.2 actually shipped rather than what
  it planned to.

**Stage 5 — the standing rule and bookkeeping. No external dependency.**

- **WI14 — Delete the false coupling (D9, as revised).** Retire
  `src/Heddle.Tests/ScopeChannelDocExampleTests.cs`' hand transcription — the *"--- Verbatim from
  docs/custom-extensions.md ---"* block — rather than formalising it into an include mechanism. What
  the test asserts about `Scope` channel behaviour is kept where it is genuinely a behaviour test; the
  claim that it is *the doc's bytes* is what goes, because nothing enforced it and the doc is free to
  diverge by design. **Done when** no test claims to be verbatim from a document, and the behavioural
  coverage that block provided is either still asserted elsewhere or explicitly recorded as dropped
  with its reason.
- **WI15 — Documentation-currency rule + bookkeeping (D11).** The additive testing-standards section
  and its ledger entry; this phase's README row and `records.md` entries; the not-delivered items (if
  any) recorded explicitly rather than dropped. **Done when** criterion 12 holds and every criterion
  above is either met or recorded as not-delivered with its reason.
