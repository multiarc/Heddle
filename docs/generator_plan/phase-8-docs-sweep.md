# Phase 8 — post-implementation documentation sweep

## Header

- **Status:** **implemented (2026-07-26)** — see [Implementation record](#implementation-record-2026-07-26).
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

  **Executed 2026-07-26.** With the guard replaced by `if (true)`, exactly one test reddened:
  `OperatorGuardDifferentialTests.MixedTypeEquality_CompilesTheConsumerProject_AndDegrades` (26 in the
  suite, 25 still green), which is the pin criterion 2 asked for and it already existed — the trap was
  therefore in the prose alone, and a doc-trusting agent would have been stopped by a red build rather
  than shipping the silent `false`. Guard restored, suite green, and the sentence now states the
  predicate plus an explicit "do not widen this" with the reason.
- **Undocumented-ID gate.** Delete `HED5019`'s mention from `language-reference.md` → the D4 gate
  reddens naming `HED5019` and the owning document. Add a registry row for a fictional `HED1099`
  with `native-expressions.md` as owner → the gate reddens naming the missing doc row.

  **Executed 2026-07-26, and the scenario's premise was half wrong.** Removing `HED5019` from
  `language-reference.md` does **not** redden, correctly: the registry row for `HED5019`–`HED5020`
  reads *"this registry row is the live normative home"* and links no owning document, and the id is
  still named in `patterns.md` and `precompilation.md`, so the any-document leg is satisfied. The
  fictional-`HED1099` half is already covered by the sibling `ConstantsAndTheClaimedIdRegistryAgree`,
  which reddens on a registry row with no constant — the doc gate deliberately ranges over shipped
  constants only.
  The gate's falsifiability was demonstrated twice instead. **Organically, on its first run:** it
  reddened naming `HED3005` and `HED4002` as absent from `built-in-extensions.md`, the document the
  registry *does* link as their owner — two real gaps that every existing green gate had missed. And
  **by mutation:** removing `HED1010`'s single mention reddens with *"Shipped diagnostic ids named in
  no published document: HED1010"*.
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
- **WI16 — The 1.x-manifest fallback claim, which describes a case that cannot occur.** Shipped documents
  (the 2.0 as-shipped record, the CHANGELOG) state that 1.x precompiled assemblies fall back because *"the
  engine-version gate rejects 1.x manifests"*. Precompilation shipped **in 2.0.0**, so no 1.x manifest has
  ever existed and no gate has ever fired for one. Q8.24 closed the behavioural half as invalid and named
  the remainder a docs defect with no work item — this is that work item. Note `records.md` is append-only,
  so the 2.0 record is **corrected by an appended note, never rewritten**; the CHANGELOG is editable
  directly. **Done when** no shipped document asserts a rejection path for a manifest version that never
  existed, and the correction says why the claim was wrong rather than quietly deleting it.
- ~~**WI17 — The registration-ordering pattern D11 permits documentation to suggest.**~~
  (**closed 2026-07-26**, landed with Q8.37 rather than in this phase, because it documents the API that
  work added and separating them would have shipped a description of a seam that did not yet exist.)
  [D11](../spec/common/cross-cutting-decisions.md) says the engine must not decide which assemblies are
  loaded, may report a conflict, and may **suggest an architectural pattern — in documentation, and nothing
  more**. The pattern is [precompilation.md's startup-order section](../precompilation.md#startup-order-a-suggestion-not-a-rule),
  placed there rather than in `custom-extensions.md` because the consequence a host actually feels is a
  precompiled template degrading when its extension assembly was not registered first; the registration
  rules themselves live in
  [custom-extensions.md](../custom-extensions.md#registering-your-extensions). It states what the engine
  will not do — load an assembly to satisfy a binding, or defer a render waiting for one — so the
  recommendation cannot be misread as enforcement, and points at `ValidateAll` as the way to turn a
  per-request report into a startup failure.
- **WI15 — Documentation-currency rule + bookkeeping (D11).** The additive testing-standards section
  and its ledger entry; this phase's README row and `records.md` entries; the not-delivered items (if
  any) recorded explicitly rather than dropped. **Done when** criterion 12 holds and every criterion
  above is either met or recorded as not-delivered with its reason.

## Implementation record (2026-07-26)

**Stage 0 — the documents that outrank code.** WI1–WI3 landed. All four false normative claims in
`native-expressions.md` are corrected against source, each with the deciding predicate named and, where
"fixing the code to match" would have widened behaviour, an explicit *do not* with the reason.
The anchor rehearsal was executed rather than argued: with the `IsReferenceish` guard replaced by
`if (true)`, exactly one test reddened, so the cross-tier pin criterion 2 asked for already existed and
the trap was in the prose alone. The seven imprecise claims are dispositioned — the built-in numeric
table (`floor`/`ceil`/`round` are `double`/`decimal` only, and an `int` argument is `HED1013`), the
"never throw" claim against `range`'s sanctioned throw, the overload-ranking paragraph (now carrying
phase 4's measurement: 0 of 480 winners change, 82 become bindable, 62 stay ambiguous), the
registry-freeze trigger (a *native expression* compile, not a template compile), the relational row's
`null`-literal gap, and the sandbox enumeration's missing array-index arm. `precompilation.md`'s two
self-contradicted absolutes are scoped to what its own tables say. The circular citation between
`MemberFacts.cs` and the sandbox section was already broken by the comment sweep.

**Stage 1 — the convention and the marker.** WI4–WI5. D10 already carried the mapping and the
condition; what landed here is the [E10](../spec/records.md#cross-spec-amendments-ledger) ledger entry
with WI1–WI2 as its evidence, the program README bullet reduced to a pointer, and verification footers
on the two stage-0 documents naming commit, date, and which claims each gate covers.

**Stage 2 — the gates. Every one demonstrated red.** WI6–WI9.

| Gate | Test | Found on its first run |
| --- | --- | --- |
| Diagnostics documentation | `DiagnosticIdTests.EveryShippedIdIsNamedInAPublishedDocument` | 16 of 85 ids named in no published page; then `HED3005`/`HED4002` absent from the document the registry links as their owner |
| Options and defaults | `WorkspaceOptionParityTests` (two new legs) | nothing — the surface was already correct, and is now held |
| Public members | `PublicApiDocMentionTests` | `architecture.md` naming `CompileScope.Compile()`, which does not exist (the pass is an internal extension method) |
| Links and citations | `DocumentationLinkTests` | a benchmark report linking a moved `GoldenCorpus`, and — through it — `.gitattributes` pinning the same dead path, so a `-text` pin its own comment calls load-bearing had applied to nothing since the move; plus one beyond-EOF citation |

**Stage 3 — class M.** WI10–WI12. `csharp-api.md`'s `FullPath` description (wrong on both clauses
after the property was fixed) and `custom-extensions.md`'s "nothing extra to implement" about
directive-line trimming (contradicted 450 lines later by `[ZeroOutput]`) are corrected. All 85 shipped
diagnostic ids are now documented, `native-expressions.md` gaining a full table for its
registry-owned block. WI12's option and editor prose needed no rewrite — WI7's gate verified it and
now holds it.

**Stage 4 — the 2.1 documentation.** WI13. The CHANGELOG entry carries the window's two newest
breaks — assembly auto-loading removal and `PrecompiledFallbackEvent.Key`'s removal — each with what
to call instead and what happens if you do not, plus `Register` and `ValidateAll` under *Added*. Its
header points at the as-shipped record, 2.1 being a ratified window rather than a set of individually
dispositioned items. The version gate already covered the shipping versions, both `package.json`s,
`PINNED_VERSION` and the CHANGELOG heading.

**Stage 5 — the standing rule and bookkeeping.** WI14–WI16. `ScopeChannelDocExampleTests` no longer
claims to hold a document's bytes. The 1.x-manifest rejection claim is retracted in both documents
that made it — `records.md` by an appended note, being append-only, the CHANGELOG in place.
Testing-standards gains *Documentation currency*
([E11](../spec/records.md#cross-spec-amendments-ledger)). WI17 landed earlier, with Q8.37, because it
describes that work's API.

### Corrections to this plan, made in place

- **"830 line citations, 86 beyond EOF."** Re-measured at execution time: the tree carries **20**
  `name:line` citations in `docs/generator_plan/**` and none elsewhere, of which **one** pointed past
  EOF. The intervening citation sweep removed the rest. This is why WI9's report-only mode over the
  plan and research trees was **not** built — a report of one item that nobody reads is worse than a
  failing gate, so the gate simply covers every tree.
- **"20 of 82 shipped ids are named in no user-facing document."** 16 of 85 at execution time.
- **The undocumented-ID validation scenario's premise was half wrong**, and is corrected where it is
  written: deleting `HED5019` from `language-reference.md` correctly does *not* redden, because the
  registry links that id no owning document and it is named in two other pages.

### Not delivered

- **`HED7025`'s documentation row** — the id is claimed in the registry and absent from code, so there
  is no shipped behaviour to describe. It documents itself when the owning work item lands it.
- **A named-exemption set for the diagnostics gate.** Deliberately absent: an empty exemption set is a
  concept with no members, and whoever finds an id that genuinely cannot be documented adds the
  mechanism together with the reason. The same reasoning that populated the marker tier rather than
  pinning it empty.

## Independent review (2026-07-27)

Two reviewers ran over the whole program with deliberately opposed briefs — a **verifier** confirming
each claim against source and by execution, and an **adversary** whose only job was to break it. Both
were read-only and independent of each other. They found **17 real defects between them**, and the
implementation record above was wrong in one place. Everything below is fixed and pinned.

### The three that mattered

1. **A reachable data race, reproduced 3/3 runs.** Making `Register` the mandatory, repeatable host
   call — and documenting that registering after rendering has begun is legal — turned a latent race
   into a reachable one. `ReflectionHelper` assigned each name map a fresh empty dictionary and then
   filled it while unlocked readers looked them up; `TemplateFactory` mutated its registry in place with
   no lock. The symptom was a **phantom diagnostic** — `Couldn't resolve type <System.DateTime>` on a
   template that had just compiled, `HED3003` on a valid `@else` — which is worse than a crash, because
   the host sees a template error that is not there. Fixed by atomic publication and copy-on-write;
   pinned by `RegistrationConcurrencyTests`, whose type-map scenario reddens within 28 compiles against
   the pre-fix shape.
2. **Single-file publish observed nothing.** The filter keyed on `Assembly.Location`, which is empty for
   the *entire application* in a single-file or WASM publish — so the engine saw no assemblies at all,
   and `Register(yourAssembly)`, the documented migration step, did not repair it. Reproduced against a
   real `PublishSingleFile` host. The SDK had been emitting `IL3000` on that line throughout.
3. **Load order decided what resolved.** Observation never rebuilt the type maps, so an assembly loaded
   after the first resolution was permanently invisible, and the same host with the same template
   resolved or failed depending on whether an unrelated earlier compile had happened. This falsified,
   in the same words, four documents landed by this phase.

### What that says about the process

Every one of the three was **invisible to a green suite**, and two were invisible to the gates this
phase built. The pattern is the same in each: the tests pinned the *mechanism* a defect had used, not
the *behaviour* the fix promised. `AssemblyRegistrationTests` pinned "no static constructor" and "no
DependencyModel", so restoring the identical discovery walk **lazily** passed all 1,826 tests. That is
the generalisable lesson, and it is the one this program keeps re-learning: a pin on the shape of
yesterday's defect is not a pin on the property you claimed.

Recorded here rather than smoothed over, because the phase's own success criteria were reported met
while three of them were not.

### The rest, fixed

The published `HED1005` row lost its whole description to unescaped pipes. The new startup-order sample
called `RegisterFrom` on `TemplateOptions.Functions`, which defaults to `null`, so it threw. A document
rewritten one commit after a module initializer was added re-asserted that none exists. `HED7104` was
described by two of its three causes. A corpus row promised a byte assertion nothing makes. The
generator-side `ClaimedIds` helper was unanchored, leaving the plan's own "registry cross-reference
hole" scenario open. `DiagnosticIdTests` matched ids by substring. The link gate skipped every
same-directory link — 435 of 803 — and checked only the first number of a range, which is precisely the
defect that prompted it. The public-API gate could not see a documented call with arguments, so this
window's entire new API was invisible to it. `editor-support.md` named 6 of the 11 excluded options
under a claim only checkable if all are listed. Two test names claimed more than they checked.

### Corrections to the record above

- **"all suites and all ten sample goldens are green"** (in the emitted-comment commit) was **false**.
  `samples/codegen-t4-successor`'s generated-source golden still held the stripped citations, and CI runs
  `compare-golden`, so the branch was red. The check that reported green was reading stale bytes: the
  capture path resolves against the sample's own directory, so a repo-relative `--capture samples/x/out`
  writes to `samples/x/samples/x/out` while the comparison reads `samples/x/out`. Golden re-ratified;
  the correct invocation is `--capture out` after deleting the directory.
- **WI6 was reported met and was not**, on both halves of its own done-when. Both are now done.
- **WI7/WI12's "the surface was already correct"** understated a gap D5 had itself named.

## Second independent review (2026-07-27)

A second pair of reviewers ran against `66c5a60` with no knowledge of the first pair's findings, and
a third agent mutation-tested the coverage claims. Fifteen defects were confirmed. The pattern worth
recording is not any single one:

**Three were unfixed halves of defects this record already listed as closed.** Not regressions —
parts of the stated property that were never fixed while the record said they were.

- The single-file fix changed `AssemblyHelper` and wrote a D11 rule covering "type resolution **and
  C#-tier metadata**". The metadata half still gated on `Assembly.Location`, in
  `RoslynReferenceProvider`. Measured on a real `PublishSingleFile` host: 23 observed assemblies, **0**
  metadata references, every C#-tier template failing with "Predefined type 'System.Object' is not
  defined". The language service hit the same gate from the other side, because it byte-loads model
  assemblies — a document whose model resolved fine for `@model` drew 11 errors the moment an
  expression used the C# tier.
- The load-order fix wrote "load order does not decide what resolves". Still false, via an ABA in the
  count gate added for performance: `_observedCount` counted every loaded assembly including the
  excluded collectibles, so an unload plus a load restored the count and observation concluded nothing
  had happened. Demonstrated 3/3, and **sticky** — the type stayed unresolvable until an unrelated
  later load repaired it. The optimisation created it.
- "The engine loads nothing" was pinned only for scanning. Restoring the deleted transitive load walk
  lazily passed all 1,832 tests. Commit `519a30f`'s message claims that reddens; it reddens the scan,
  not the load, and the message did not distinguish them.

**Mutation testing found three of four checked defects entirely unpinned.** Returning a constant
`"DEADBEEF"` as every public key passed 1,832 tests despite 100 invocations. Deleting the `Location`
guard passed. The cached-failure path had no test at all.

New and serious: the generated tier **rejected templates the engine renders** — `@(2147483647+1)`
renders `-2147483648` dynamically and was a `CS0220` build error precompiled, with no Heddle
diagnostic and a manifest still claiming success. `ToHexString` used `"X"` instead of `"X2"`, voiding
every emitted `InternalsVisibleTo` grant. The preparse cache dropped diagnostics, so the same template
reported 1 error then 5 in one process. Import cycles killed the process outright.

### Not fixed, and why

- **Deep nesting is now bounded, after a first attempt that was not.** The first attempt probed remaining
  stack with `EnsureSufficientExecutionStack`. That was wrong in a way worth recording: the answer depends
  on thread stack size and on whether the build is optimised, so the same template passed on one host and
  died on another, and no fixed-depth test was portable. Replaced with a **fixed depth count of 1000**,
  enforced in two places because the failure has two shapes — an ANTLR parse listener for the parser's own
  recursion (prefix and right-associative operators), and an iterative tree-depth check for
  left-associative runs, which ANTLR parses with a *loop* and so never recurses on, while still building a
  tree the walk recurses over. Needs no grammar change, which matters: the parser is committed, not
  generated by the build. Verified against the shapes that produced exit 134 — prefix, conditional,
  coalesce, flat, parenthesis, indexer and block nesting now report `HED4007` at 3000, 20000 and 100000
  levels. The bound is source-linked into the generator, so a build-time parse can no longer take down the
  compiler instead of failing the build.
  <br>**Correction to the sentence above, which was false when written:** prefix runs were listed among the shapes
  reporting `HED4007` "at 3000, 20000 and 100000". They are not — prefix at 20000 stack-overflows, as the residual
  note two sentences later said. Both claims were written in the same commit; the residual one was right.
  <br>**Residual:** a run of several thousand *prefix* operators still exhausts the stack inside ANTLR's
  own lookahead before the bound is reached — 3000 survives, 5000 does not. That is the ATN simulator's
  recursion, upstream (antlr/antlr4#744), and not reachable from a grammar or a listener. Documented in the
  language reference rather than left to be found.
- **The preparse-cache fix has no demonstrated red.** It is correct and saves a redundant Roslyn
  compile per repeated failure, but once the public keys were fixed both paths produced identical text
  and positions, so removing the replay leaves the suite green. Stated in the test rather than implied
  away.
- **The no-load pin does not catch a one-shot startup walk**, which has already run before the probe
  can be built. Verified, not assumed; recorded in the test. Catching it needs a child process.
- **`Heddle.Performance` keeps one change** — the removal of its per-project `<Version>`, from the
  version-centralisation sweep. Restoring it would redden that sweep's gate. A knowing exception to a
  ruling that said "at all", not an oversight. Its three source files are restored.
- **Stale phase-record claims** the verifier catalogued (schema 4→5 where the shipped value is 3; "82
  rows" against 85; "62 templates" against 63; `HED7025` recorded as absent when it ships) are
  corrected here rather than in each section that states them. That is a compromise: a reader lands on
  the delivery section, not this one.
- **`CorpusRenderParityTests` remains a hand-maintained list** of 10 against 32 eligible templates,
  ungated. Partially mitigated by `CorpusResolverSweepTests`, which is set-equality gated but exercises
  the resolver path rather than whole-corpus render.


## Third independent review (2026-07-27)

Two more reviewers, no knowledge of the previous cycles. Twelve confirmed findings. **The pattern held again, and
this time it produced a regression rather than a gap.**

- **The depth bound was bypassed on the error path.** `EnsureTreeWithinLimit` was placed *after* the early
  `return tree.GetText()` that fires when a parse reported errors — and `GetText()` recurses over the whole tree.
  A deep left-associative run plus one stray `@(` still killed the process (exit 134, 52,309 `RuleContext.GetText()`
  frames). Both reviewers found it independently. It mattered most exactly where the bound was sold hardest: an
  editor's document has a syntax error most of the time, and the generator *is* the compiler.
- **The diagnostics cache fix was itself a regression, twice over.** Replaying a stored position stamped the first
  caller's coordinates onto every later one — a one-line document told its error was on line four. Positions were
  correct before that commit. And the cache was never invalidated when the assembly set changed, so a failure cached
  before a host registered an assembly outlived the registration permanently: a fault that used to heal on the next
  compile became load-order-decided forever, which is precisely the property the observation gate one file over
  exists to prevent.
- **`ConstantFolding` was wrong in both directions** because it widened every integer to `long` and lost the operand
  type. Five classes still broke the host build (`(2147483647+0)+(1+0)`, `+2147483647+1`, `uint` overflow, `decimal`
  overflow), and legal C# was refused, taking whole templates off the precompiled tier. Rewritten to track the type
  C# evaluates in, including the constant `int`→`uint` conversion and the `-2147483648` literal rule.
- **Its test class could not fail.** Every case used `@model(){{dynamic}}`, under which constant-only expressions
  degrade anyway — deleting the entire fold left all 876 generator tests green. Typed models now make a degrade
  attributable; deleting the fold reddens 17 of 32.
- **Two unbounded import shapes.** Depth was measured per document, so a chain of 4000 distinct files — no cycle
  anywhere — exhausted the stack. And the cycle key was the raw spelling, so `./a.heddle` and `d/../a.heddle` read as
  different documents: a cycle walked past the guard, and eight spellings produced 863,109 diagnostics at build time.
- **A six-character template threw.** `@(1)}}` underflowed the lexer's mode stack and the `InvalidOperationException`
  escaped the compile. Unrelated to depth, pre-existing, found by a test written for something else.
- **Two fixes from the previous cycle were entirely unpinned** — the `AssemblyNameEqualityComparer` hash and
  `HED7020`'s location both revert green.

### Still not fixed

- **Prefix-operator runs of several thousand** still exhaust ANTLR's own lookahead (antlr/antlr4#744). The published
  numbers are now marked indicative rather than contractual, because the threshold moves with stack size — a 1 MB
  stack, the Windows and thread-pool default, fails earlier than this box's 8 MB.
- **The position re-stamp has no demonstrated red.** Replaying a fixed position leaves the suite green, because two
  documents sharing an expression do not reliably share a cache entry here. The fix is still right; the test says
  plainly that it is a guard.
- **`CorpusRenderParityTests`** remains 10 hand-listed templates against 32 eligible, ungated.
- **`RoslynReferenceProvider` builds each reference twice on a cache miss** — pre-existing, now fixed as part of
  anchoring the assembly, but worth noting it was found by review and not by any test.

## Closing round (2026-07-29)

Everything the review cycles had left confirmed-but-unfixed, plus the items they had marked
suspected. Each fix below was demonstrated red before it was made, except where stated.

### The two that were wrong output, not just wrong shape

- **`?.` abandons the rest of the chain; the engine defaults one hop and keeps walking.** The
  emitter wrote a member path as one null-conditional chain, so `Inner.Maybe.HasValue` over a
  null `Inner` produced nothing at all, while the engine read `HasValue` off `default(int?)` and
  produced `False`. Two tiers, two different renderable answers, no diagnostic on either. The
  `.Value` form of the same path diverged the other way: the engine threw
  `InvalidOperationException` and the generated tier rendered empty. Fixed by ending the chain
  with parentheses before a plain `.` — `(a?.B).C` reads `C` off the default, which is the hop
  the engine performs. Four of four null-hop cases were red; all four now agree, values and
  exceptions alike.
- **A ref-struct hop behind a reference hop did not compile.** The ref-struct form spells its
  receiver twice, and spelling a propagating chain into the read half re-applies `?.` to a type
  with no nullable form: `Inner.Buf.Length` emitted two `CS8978`s into the consumer's build. The
  same parenthesisation fixes it. Found by asking what else the chain-propagation rule touched,
  not by any existing test.

### The rest

- **The preparse cache could serve exactly the entry the drop existed to remove.** Dropping it
  ran *before* the assemblies were unregistered, and a compile already in flight stores its
  result afterwards — into the map that was just emptied. Entries now carry the epoch their
  compile began under and are refused if it has moved; the drop happens after the removal, so an
  epoch that is current implies the removal is complete.
- **`HED4007` had no message pin at either of its two producers**, which are unrelated faults
  with unrelated remedies — expression nesting and `@<<` import nesting. Collapsing both messages
  to one vague string was green. Both are now pinned, and the parse-depth path has a test again:
  a flat left-associative run reaches the reporting path without being able to reach the crash,
  because ANTLR loops left recursion rather than recursing.
- **The corpus render column was a declaration nothing measured.** `ResolveOnly` removes an entry
  from byte-parity coverage on its own say-so, and seven entries declared `WithModel` sat outside
  the gate while rendering identically all along. Everything not declared `ResolveOnly` is now
  rendered and compared, and everything declared `ResolveOnly` is rendered too — to prove it
  cannot be. This closes the item the third review left open.
- **The differential harness was order-dependent.** Handing an assembly to Roslyn equips only the
  precompiled side; the dynamic reference resolves model types over what is actually loaded in
  the process. Corpus templates naming engine test models compiled or failed depending on whether
  an earlier test in the same run had happened to load `Heddle.Tests.dll` — visible as two
  unrelated cases going red under a mutation that only changed scheduling. The harness now loads
  what it references.
- **Dead arithmetic removed.** `Numeric.From` carried rows for `sbyte`/`byte`/`short`/`ushort`,
  which no template literal can produce — the language has no suffix for them, so the smallest a
  written number arrives as is `int`. `Numeric.Shift` masked its count to the operand width, which
  is what the C# `<<` and `>>` it is written in already do.

### Not fixed, and unverifiable here

- **`Path.Combine` rejects `<`, `>` and `|` on .NET Framework** and accepts them on .NET Core.
  The combine was moved inside `ImportIdentity`'s guard so a throw degrades the cache key instead
  of killing the parse — but nothing on this box can make it throw, so that change is reasoning,
  not evidence.
- Everything else platform-shaped is now written down in
  [unverified-platform-surface.md](unverified-platform-surface.md), including a declared `net6.0`
  test container that fails to start while `dotnet test` still exits 0.

## Eighth review cycle (2026-07-29)

Two reviewers, independent. They agreed on the most important finding, which is that the
previous round's fix introduced a defect of its own — the same pattern this record has noted
three times before.

### The fix that broke something else

**A ref-struct hop off a reference receiver read that receiver twice.** The non-nullable-value
hop form has always spelled its receiver twice, because a ref struct has no nullable form to
widen into and the null test has to be written out. That was harmless while the receiver was
always the model local. Parenthesising the chain — the previous round's fix — made the form
*compile* for a deep prefix, and the duplication became reachable: `Inner.Buf.Length` over a
getter that answers differently on its second call renders a value on the engine and throws
`NullReferenceException` on the generated tier. The commit before had turned that same template
into a loud `CS8978`; the fix converted a build failure into silent wrong output.

Fixed by binding the receiver with a type pattern, which names it once. The names come from a
counter owned by the emitter and shared with every expression writer it makes, because a pattern
variable belongs to the block its statement is in and two paths in one block would collide.

### Found alongside it

- **A path ending *on* a ref struct emitted `CS0030`.** Every consumer of a path's value boxes
  it, and a ref struct cannot be boxed. Pre-existing, and a build break in the consumer's project.
  Neither tier can render this — measured, after a first draft of this entry claimed the engine
  could: the engine refuses the template with `HED0005` at compile time. That is the difference
  worth having. An id and a position a host can report beat a raw `CS0030` against a `.heddle`
  file, so the shape degrades and the engine's refusal is the one the reader sees. Reading
  *through* a ref struct to a member of its own is unaffected — what leaves that path is an `int`.
- **Generated arithmetic inherited the consumer's `CheckForOverflowUnderflow`.** The engine's
  arithmetic is built from the unchecked expression-tree factories, so it wraps whatever the host
  sets; bare operators do not. The same template rendered `1410065408` on one tier and threw
  `OverflowException` on the other, decided by an MSBuild property in a project the template
  knows nothing about. Emitted expressions are wrapped in `unchecked` now. Constant overflow is a
  separate question and still degrades.
- **A failed render permanently poisoned a compiled template.** The definition recursion counter
  was incremented and decremented without a `try/finally`, so any throw in between kept the
  increment. Templates are cached and reused, so a hundred bad requests retired the template on
  that thread for good — for healthy requests too, with a recursion error describing nothing that
  happened. Both tiers. Nothing in the repo asserted the recursion guard at all.
- **A C#-tier compilation cache that could never be read from.** Its key was the generated source,
  which names a class after a fresh `Guid` per compile. Measured: eight compiles of three distinct
  expressions, eight adds, zero hits. It retained every generated source string for the life of
  the process. A hit would have been worse than a miss — the entry class is looked up by the
  *current* context's guid, which another context's assembly does not contain.
- **An assembly that lost a name collision was retired for good.** It was marked classified before
  the name was claimed, so the loser was skipped on every later pass and stayed invisible to type
  resolution even after the name was freed.
- **A cached preparse *success* was never invalidated.** The reasoning was that nothing a later
  registration adds can take a type away. It can: a new assembly can make a name ambiguous
  (`CS0104`) or introduce a better overload candidate. Successes are generation-checked now, and
  the cache drops a spent entry rather than merely refusing to read it.
- **The unreachable half of `WriteDynamicPath`** wrote exactly the `?.` chain whose short-circuit
  the typed writer had just been fixed for. Both sides of its gate are constants this assembly
  reads, so it was statically decided. Deleted.

### Gaps the reviewers found in the previous round's tests

Every one of these was a property claimed by a commit message and pinned by nothing: negative
shift-count folding (`if (places < 0) places = 0;` reddened 0 of 587 tests while producing a real
host-build break), shift results never byte-compared anywhere at all, `ImportIdentity`'s catch
unreachable by any test, `ResolveOnly` satisfied by `Assert.ThrowsAny<Exception>` — including the
harness falling over — and the `Assembly.LoadFrom` in the differential harness. All now pinned
except the last two noted below.

### Not fixed

- **An `internal` property on a model type from a *referenced* assembly hard-errors the host
  build.** The engine renders it; Roslyn's default `MetadataImportOptions.Public` hides it, so the
  generator reports `HED7008` at error severity. The two candidate fixes are both bad: degrading
  on any metadata receiver disables `HED7008` for nearly every real model, and leaving it means
  valid templates break builds. This is a policy decision with a large blast radius and it is not
  one to make while clearing a review queue.
- **The epoch ordering is reasoning, not evidence.** Reversing either the drop-after-removal
  ordering or the read-epoch-before-the-assembly-set ordering leaves every suite green, and a test
  that races to observe the difference passes by luck when it is wrong. The argument is written at
  each site; the tests pin only the primitive those sites rely on.
- **`Assembly.LoadFrom` in the differential harness is unpinned**, and one reviewer argues it hides
  a real engine property rather than testing it: a model type can bind at build time and fail at
  run time depending on what has been loaded. Worth its own test; it does not have one.

## The three the eighth cycle left open (2026-07-29)

All three are now closed. Two of them turned out to be worse, or differently shaped, than the
review described — which is the argument for fixing rather than filing.

### The build break was in two halves, and only one was the one reported

The report was that an `internal` **member** on a model type in a referenced assembly draws
`HED7008` at error severity while the engine renders it. True, and confirmed: Roslyn's default
metadata import makes such a member *absent* from the symbol model rather than inaccessible, so
it is indistinguishable from a typo. (It does honour `[InternalsVisibleTo]`, so a consumer with
legitimate access sees it.)

The other half was assumed to behave the same way and does not. Internal **types** *are* imported
from metadata regardless of accessibility, so no diagnostic fired at all — the emitter resolved
the type, wrote its fully-qualified name into a generated cast, and the consumer's build died on
a wall of `CS0122` against `.g.cs`, with no Heddle id and no `.heddle` position. Worse than the
reported symptom and reachable by exactly the same model.

The obvious fix — degrade whenever the receiver came from metadata — was rejected: models
normally live in referenced assemblies, so that is precisely where `HED7008` earns its keep, and
it would have downgraded every genuine typo to a warning. What went in instead is a second view
of the *same references* opened with `MetadataImportOptions.All`, built lazily and only on the
path about to report a failure, held weakly against the compilation it describes. A member the
engine's own visibility policy accepts *there* and this compilation cannot see is hidden, not
missing. So: internal member → degrade under `HED7030` (warning); **private** member → still
`HED7008`, because the engine rejects private too and the tiers agree; typo → still `HED7008`.
Source receivers skip the probe, since Roslyn shows a compilation every member of its own types.

### The epoch was a second counter for something one counter already knew

Introduced in the previous round to close a window; the same round then made staleness
generation-strict, which subsumed it. Removed. The cache now holds one assembly generation at a
time, and everything it must not do follows from that: a result computed against a superseded set
is refused admission, one from a newer set retires the map on the way in, and a reader asking at a
generation the map is not holding gets nothing.

The point of the shape is that the two orderings the reviewers could reverse without reddening
anything no longer exist to be reversed. `GetApplicationReferences` hands out the reference set
and its generation from one lock-held read, so an entry can only be stamped with the set it was
built from; and the unregistration is now a single expression, so the bump cannot happen without
the retarget. One correction on top: a reader whose generation is *older* than the map's must not
retire it, or a straggler arriving after an unregistration throws away every entry the current
set just built.

### The harness suppression is now the subject of a test rather than a side effect

The two tiers do not bind a model type from the same world — the generator resolves over the
compilation's *references*, the engine over the assemblies the process has actually *loaded*. So
`@model(){{X}}` can bind at build time and fail at first render, depending on what else the
process touched. That is a real property with a real consequence for hosts, and the harness's
`Assembly.LoadFrom` was quietly suppressing the only place it was visible. It stays — a
differential test asks whether two tiers emit the same bytes from the same inputs, and "the model
assembly is loaded" is an input — but the property is now asserted head-on against a GUID-named
assembly built at test time, and a second test pins the suppression itself.

## Ninth review cycle (2026-07-29)

Two reviewers, independent. Both led with the same finding, and it is the shape this record keeps
returning to: the previous round fixed a class of defect on one code path and wrote the commit
message as though it had fixed the class.

### The fix that covered one of two paths

**Embedded C# arithmetic was still not `unchecked`.** The previous round wrapped the native
expression writer; the C# tier pastes the author's expression through a different method, and it
was left bare. So the sentence in that commit message — a template that renders a wrapped number
in one project and throws `OverflowException` in the next, decided by an MSBuild property it knows
nothing about — went on being true for anyone using `ExpressionMode.FullCSharp`.

Fixing it needed a decision the native case did not. C# checks a *constant* expression whatever
the compilation is configured to do, so wrapping only the generator would have moved
`@(@100000 * 100000 * 100000)` from "both tiers refuse" to "precompiled renders, engine refuses" —
trading one divergence for another. Both sides are wrapped instead, engine included.

**That changes the engine, and the cost is real:** a constant overflow in embedded C# used to be a
compile error and now wraps. The argument for accepting it is that the engine was already
inconsistent with itself — its native tier builds `Expression.Multiply`, which is unchecked, so
the identical template has always wrapped silently there. Written up in the language reference
with `checked(…)` as the escape hatch rather than left for someone to discover.

### Guards scoped narrower than the defect they were written for

Three separate instances, all found this cycle:

- **`HED7030` missed a `CompilationReference`.** It detected an internal member by its *absence*
  from the symbol model, which is true only of a metadata reference. A Roslyn workspace hands the
  generator a `CompilationReference` for a project-to-project reference, where the member is
  present — so it resolved and was emitted, and the consumer's build died on `CS0122` in `.g.cs`.
  Detection-by-absence and detection-by-accessibility are two halves and only one had been built.
- **The accessibility gate guarded `@model()` alone.** Definition, slot and prop model types each
  resolved a type and wrote its name into a cast without asking whether this assembly may name it.
- **`EndsOnRefStruct` guarded hops, not the model.** `@model(){{System.ReadOnlySpan<char>}}` emitted
  a signature that cannot compile, while the engine accepts the template and reports an ordinary
  catchable error at render.

### The accessibility probe reverted to the bug on ambiguity

`GetTypeByMetadataName` returns null when a name is found in more than one reference, and the
caller kept the answer it already had — `HED7008` at error severity, over a template the engine
renders. The plural `GetTypesByMetadataName` degrades instead. The same probe was also being built
on *every* member miss, including from the estimator, which never reports: in an editor that is one
extra compilation per keystroke through a half-typed member name. It now sits behind the reporting
decision, and path resolution is memoised.

### Six properties that nothing pinned, two of them one day old

Each was demonstrated by mutating the production code and watching every suite stay green:
hop-local name uniqueness (a constant name leaves 567 integration tests green and breaks a real
template with `CS0128`); the `ProcessData` half of the definition recursion guard; both
`PreparseCache` retire rules; and all three `AssemblyHelper` orderings. One of the newest tests was
also retargeting the process-global cache into a band it never left, silently disabling the C# tier
cache for every test that ran after it.

Two of the `AssemblyHelper` orderings **cannot** be pinned — they are claims about what no
concurrent caller can observe, and a racing test passes by luck when the code is wrong. That is
written in the test file in plain words rather than covered by a test that would imply more.

### Declined, deliberately

A hop whose *property type* is internal still emits a name the consumer cannot compile. Checking it
where the member check sits would falsely degrade the ordinary `m?.Inner?.Name` shape, where no
type name is ever written; doing it properly needs a form-aware check at the point of emission.
Recorded rather than half-fixed.

## Tenth review cycle (2026-07-29)

Two reviewers, independent, over the commit above. Four of the eight findings were introduced by
it — the same pattern this record has now noted four times.

### The memo the widening added

Path resolution was memoised last cycle to stop the accessibility probe running per keystroke, and
the key was a **display string**: the receiver's fully-qualified name plus the segments joined by a
dot. Two references each declaring `Dup.Thing` produce one key, so the second walk was answered with
the first's resolved type — which decides the null-safety form, the numeric widening, the formatter
and the member name written into the file. The same key also made `["A.B"]` and `["A","B"]` one
question. The key is now the receiver **symbol** under `SymbolEqualityComparer` and a separator no
identifier can carry. Nothing end-to-end reaches it today, which is why it is pinned at the resolver
rather than through a template, and the test says so.

### A guard whose only test the same commit ate

Widening the accessibility gate from `@model()` to every type the emitter spells left the
`@model()` arm itself unpinned: the one test covering it survives on the *member* rule the same
commit added, because its fixture's member is declared on the internal type. Removing the `@model()`
call now reddens a test whose model declares its member on a public base — six `CS0122`, measured.

### `[Obsolete(…, error: true)]` broke the consumer's build

Reflection ignores `[Obsolete]` entirely, so the engine renders; every generated mention of the name
is a `CS0619` against a `.g.cs` the consumer did not write, attributed to a `.heddle` file and
carrying no Heddle id. Same class as `HED7030` and it takes the same id and the same degrade, on the
type and on the member. The **warning** form deliberately does not degrade: it is a note to the
author, taking every deprecated model in a codebase off the precompiled tier would be a large silent
cost, and the generated file's blanket `#pragma warning disable` already keeps `CS0618` out of the
build. `HED7030`'s message is broadened to say what is true of both causes rather than naming
accessibility alone.

### A static class as `@model()`

`CS0721`: a static type cannot be a parameter, and the entry point takes the model as one. The
direct sibling of last cycle's ref-struct arm, for the other type kind that cannot be a parameter,
and it degrades the same way — the engine declares no such parameter and renders the template.

### `HED5014` was engine-only

The engine type-checks every `@out` value against the declared slot type and refuses the template;
the emitter checked only that it was inside a slot definition, so the same template precompiled and
rendered — or threw `InvalidCastException` from the caller-content cast, depending on whether the
caller's body happened to read a member. The emitter now runs the engine's own conversion table,
with the same `allowBoxToObject: false` the slot caller passes, wherever it can type the value.

Where it cannot, it degrades in one shape and not the other, and the difference is deliberate. A
slot definition declaring `:: dynamic` degrades: the engine compiles such a body per call site off
the value actually passed, the emitter compiles one body for every call site, and there is nothing
to check against. An `@out(this)` inside an `@list` body keeps precompiling: the element type is
information the emitter deliberately does not guess, and refusing every one of them would take the
ordinary per-item slot projection — the canonical use of the feature — off the precompiled tier to
catch a case the caller's cast already throws on. That gap is stated in the code.

### Two invalidations that could not fire

`InvalidateObservation` exists for a loaded assembly that **lost** a name collision, which is
exactly the case where `TryAdd` fails — and two of its three call sites sat inside the success
branch. Every `Configure(assembly)` therefore forced a full re-classification pass that could not
reach a different answer. Both are removed; the one in `UnregisterModelAssemblies`, where a name is
actually freed, is real and pinned.

### Probe assemblies, 58 of them

The registration and C#-tier suites write a GUID-named `.dll` beside the test binaries — they have
to, for "this has never been loaded" to be a fact and for a referenced-but-unloaded probe to be
findable — and deleted none of them. They cannot be deleted when the test that wrote them ends
either: the file is loaded by then, and the C# tier builds its reference set from the locations of
every observed assembly, so removing one mid-run leaves an unrelated suite short of a reference
(measured — it reddens `NoAssemblyIsDroppedFromTheReferenceSet`). They are now deleted at process
exit, best effort. The collision test's `finally` also called the process-global unregister
unconditionally, which would clear another suite's registrations; it now runs only on the paths that
left its own registration in place.

### Measured, and not a defect

`DefinitionBaseExtension` holds one `ThreadLocal<int>` per definition per compiled template and
never disposes it, which was reported as an unbounded per-thread slot table across reloads. It is
not: `ThreadLocal<T>` carries a finalizer that returns the id, so ids recycle without `Dispose`.
Measured on this box — 200,000 definition instances over 10,000 compile-and-render cycles left the
per-thread slot array at **256** entries, flat from the first round; 10,000 cycles of a
single-definition template left it at 1024. The array tracks the peak number of instances awaiting
finalization, not the number ever created. A tight allocation loop that outruns the finalizer does
grow it (100,000 abandoned instances in a loop reached 131,072 slots), which is a shape no template
workload has. Nothing changed.

## Eleventh review cycle (2026-07-29)

Two reviewers, independent, each in its own worktree after the previous pair broke each other's
builds by probing the same tree at once.

### The enumeration was the defect

Three commits in a row had widened the same guard by one type kind, and each time a reviewer found
more: static classes, then ref structs, then error-obsolete types, and this cycle `void`, unbound
generics, pointers, error-obsolete *containing* types and error-obsolete *property* types. Five
separate findings that were one incomplete answer to a single question — *can generated code in the
consumer's assembly name this type, take it as a parameter, and cast `object` to it?*

Answered properly this time, and split in two, which is the part worth keeping. `ClassifyTypeName`
asks whether a name may be written where a value of it lives; `ClassifyModelType` adds ref-struct-ness,
the one restriction that applies only to a value that has to box into `object`. That split is what
lets `Buf.Length` stay precompiled while `Span<char>` as a model degrades — the distinction the
ad-hoc guards kept blurring. Every `TypeKind` now has a recorded verdict, `void` is caught by
`SpecialType` rather than by kind (it is a `Struct`), and `ContainsTypeParameter` walks array and
pointer elements, type arguments *and* containing types.

One of the five was not a type-kind problem at all: an `@model` text that resolves to **no symbol**
was emitted verbatim as the entry point's parameter type, which is why `System.Int32*` never reached
any guard. Fixed where the symbol is resolved rather than where the kinds are listed.

### A degrade is a cost, and last cycle's paid it

The `:: dynamic` slot refusal added last cycle was over-broad: a reusable wrapper whose `@out` values
are all assignable lost the precompiled tier silently, with both tiers producing identical bytes.
Nothing reddened, because only two corpus templates declare a slot and both use typed body models.
The refusal is now per call site, against the static type the caller actually passes — strictly more
precompilation than before, and it still refuses only the case that genuinely cannot be decided.

Fixing it surfaced a second hole: the definition body is cached per definition and fills, so with two
call sites into one `:: dynamic` definition the first one's verdict stood for the second. The cache
key gained the slot-value model — as an ordinal id under `SymbolEqualityComparer`, not a display
string, because two distinct types can share a fully-qualified name. That lesson came from the path-memo
collision two cycles earlier; this is the first time the loop has reused one of its own findings.

### A reproduction that did not reproduce

The property-type case was reported with a repro that does not compile: `public Money Balance` where
`Money` is error-obsolete is itself `CS0619`, so the model could never have been built. The defect is
real, but reaching it needs the property to carry its own *warning*-level `[Obsolete]` — an obsolete
context suppresses the diagnostic on the types it mentions — or a model library not rebuilt since the
type was deprecated. Worth recording because the report was accepted on its reasoning and the
reasoning was checked against the compiler rather than against the reviewer.

### Reverted

An unmeasured micro-optimisation came in with the fixes — marking a registered assembly as already
classified to save a `GetName()` per observation pass. Correctly guarded and honestly flagged as
untested, and removed anyway: nobody measured the cost it addresses, and the file it touches has had
three defects this session. The same rule that left `ThreadLocal` alone after measuring applies to
changing something without measuring.

## Twelfth review cycle (2026-07-29)

Both reviewers found the same newly-introduced defect, and the fix disagreed with both of them.

### The rule the previous cycle guessed at

Making the `:: dynamic` slot refusal per call site rested on an assumption: that the engine compiles
such a body against the value the caller actually passes. It does not. `CompileModelAccessor` takes a
dynamic exit before resolving anything whenever the callee declares `:: dynamic`, so a bare call or a
member-path argument gives the body `ExType.Dynamic` and every `@out(this)` inside it is `HED5014`.
Two of six call forms precompiled templates the engine refuses outright.

The two reviewers proposed different corrections — one a matrix, one a conservative `this`-only rule.
Measuring the engine showed the matrix right and the conservative rule wrong in the other direction:
a *caller's prop read* keeps its static type through a `:: dynamic` definition, because the prop-read
path runs before the dynamic check. The fix mirrors the engine rather than either proposal, and it is
not a refusal of those call forms — `dynamic` is carried in as the body's model, so an `@out` of a
literal or of the definition's own prop stays typed and stays precompiled. Only an `@out` that reads
the model is refused, which is exactly the engine's rule.

### Both tests for that fix passed for the wrong reason

`AssertEngineRefuses` grepped for the id. `HED5014` has two messages — "not assignable" and "must have
a static type" — so the test could not tell which rule it had reproduced. And both tests were
`ExpectDegrade`-only, which cannot fail against the parent commit, where *everything* degraded. They
are rebuilt to name the exact type pair and to carry a precompiling half, and were checked by
re-inserting the parent's blanket refusal: they now fail there, on the positive half.

That is the general lesson worth keeping: **a test that only asserts a degrade cannot distinguish a
rule from a blanket refusal.** Every degrade assertion needs a paired case that must still precompile.

### "Complete" was not complete

The type-nameability verdicts recursed into array elements but not type arguments, so nine spellings —
`Span<char>[]`, `List<Math>`, `Nullable<Span<char>>`, `(Span<char>, int)` and others — still emitted a
`.g.cs` the consumer cannot compile. One `Classify` now serves both entry points, and the only verdict
that varies by position is ref-struct-ness: allowed for a bare hop type, refused as a model, an array
element or a type argument.

And of some nineteen verdict arms, only six were pinned; eight could be deleted with every generator
test green. All nineteen were mutated one at a time. Sixteen went red. **The three that did not were
deleted** — each was a second copy of a walk the recursion already performed.

### A prelude the runtime never had

`@model(){{int?}}` bound a strategy for a template the engine cannot resolve at all: a `?`-suffix
prelude existed in the generator and in no shared grammar. Checking all four positions rather than the
brief's assurance showed the runtime accepts `?` **nowhere** — and that `:: T?` on a definition does
not "work" either, it silently drops the `?` and compiles the body against the unlifted type. The
prelude is deleted and `?` now reaches the same `HED7007` the engine's refusal mirrors. Rows added to
both lockstep corpora, whose stated job was this parity and which had none.

### Smaller

The null literal types as `System.Object` to the engine and as "cannot say" to the emitter, so
`@out(null)` into a typed slot precompiled what the engine refuses. A property whose type is merely
*unusable* — a pointer, `void` — was raising the author-facing `HED7030` reserved for faults an author
can fix; the fault is carried now rather than collapsed into a boolean. And a dotted `@model` whose
last segment happened to name a real type emitted raw text with no diagnostic at all; the existence
check is a dot-bounded suffix match now, generous to a bare name and strict about namespace segments
the author actually wrote.

## Thirteenth review cycle (2026-07-30)

Every finding was settled by measurement before anything was changed. Two of the six turned out to
be something other than what was reported, and one was not a defect at all.

### `:: dynamic` on a definition does not mean "untyped body"

The reported defect was that a `:: dynamic` definition's body was emitted with dynamic binds even
when the call site handed it a static value. The engine was probed over a matrix of (slot / no slot)
× (call form) × (body expression), and the rule it actually follows is unambiguous: it compiles the
body **once per call site, against the static type of the value that call site passes** — `this` gives
the caller's model, a literal gives the literal's type, `null` gives `System.Object`. A bare call and a
member path reach the accessor's dynamic exit and leave the body genuinely untyped; the matrix probed
here contained no other form, and the fourteenth cycle found several — a computed expression, a chained
call, the value inside an `@list` body — every one of which the engine types statically. A body member
the type does not carry is `HED0001`, raised when the template is compiled.

The emitter already computed that model, and used it for one thing only — type-checking the body's
`@out` values. The body itself stayed dynamic. So the tiers diverged in every cell where the model was
static and the member missed: the generated code threw `RuntimeBinderException` at render where the
engine had refused the template, and where the value was `null` the dynamic read yielded empty and the
page **rendered what the engine will not compile at all**. That last shape reached the emitter only
because the previous cycle started typing the null literal.

The fix is not another check. The body is now built in a *typed* context off that same model, so the
existing member-path machinery answers — `HED7008` and a degrade, the mirror of the engine's
`HED0001`. All 33 probed cells now agree. It applies to plain definitions as well as slot-declaring
ones — but only for the call forms in that matrix. A caller value the emitter could not type was
handled by mode: slot mode degraded, and **plain mode kept a dynamically-bound body**, which is the
defect this section says was closed rather than a narrower version of the fix. The fourteenth cycle
closed it. The body cache is keyed on the call-site model,
so two call sites passing different types get different bodies — pinned by a two-call-site file next
to a one-call-site one.

### The deleted walk was real, and the justification for deleting it was the second defect

The previous cycle removed a loop over a containing type's type arguments from `IsObsoleteError`,
calling it a duplicate of the walk `Classify` performs. `Classify` walks a type's **own** arguments;
nothing walked an enclosing type's. Measured at the parent commit: a property of type
`Outer<Legacy>.Inner`, where `Legacy` is error-obsolete, is judged writable, and the consumer's build
dies on two `CS0619` off a property that carries nothing but the warning-level attribute which made it
legal to declare. Not theoretical — one of the two reviewers had reached that verdict having only
tried a ref-struct argument, which C# cannot declare.

The restoration is in `Classify`, not in `IsObsoleteError`. Asking there makes *every* verdict see an
enclosing type's arguments rather than obsolescence alone, and it keeps the "one walk" property the
deleted code was wrongly accused of breaking. It also subsumes the missing array/pointer arms of
`ContainsTypeParameter`: `Outer<T[]>.Inner` is refused again, by the argument walk, and restoring
those arms would only change which sentence the refusal carries. The claim was checked by mutation,
not asserted.

The entry-point route reported alongside it does not exist: `@model(){{List<Outer<Legacy>.Inner>}}`
degrades already, because the shared spelling grammar cannot resolve a nested type of a constructed
generic at all. Measured, and left alone.

### A verdict test that pinned the answer but not the arm

The completeness suite asserted only that each subject was refused and that the reason was non-empty.
That is how the wrong deletion got authorised: with an arm gone, a different arm answered its rows,
the suite stayed green, and mutation testing reported the arm as dead. The rows now carry the words
their reason must contain. Two things fell out immediately: `unbound-generic` is answered by the
**error-type** arm and not the type-parameter one — Roslyn fills an unbound generic's arguments with
error symbols — and the row named `array-of-ref-struct` was built over `Span<T>`, the open definition,
so it was refused for being an open generic and never consulted the ref-struct rule at all. Both are
fixed; the `Classify` doc credited the wrong arm for the first and now says which.

Nineteen mutations, one arm at a time: seventeen redden the completeness suite, and the eighteenth —
the containing-type walk in `IsObsoleteError` — reddens the hostile-model table in the integration
suite instead, which is where it belongs. The nineteenth is below.

### The gate that reported and the emission that did not listen

A `@model` spelling resolving to no symbol was written into the entry point's parameter type verbatim.
`IntegrationTests.Fixtures.Article`, `Fixtures.Article` and `System.Collections.Generic.List` all pass
the name-existence gate — deliberately, since the runtime binds over what is *loaded* — and all three
put four `CS0246` or `CS0305` into the consumer's build. The engine refuses every one of them.

The test added last cycle for the generous half of that gate asserted "no errors" over a body that
degraded for an unrelated reason, so the generated file that could not compile was never built. Its
body is static text now, and it declares the degrade and the near-neighbour that must still precompile.
The gate is unchanged: what it diagnoses and what gets emitted are separate questions, and only the
second one was wrong.

### Measured, and not a defect

`@out(::X)` — a root reference as a slot value — is never type-checked, which was reported as a latent
hole. It cannot become one: `BuildParamExpr` refuses every root-reference call parameter outright, so
such a template degrades before anything is emitted, whatever the type check would have said. Measured
over four shapes, including one the engine renders and the emitter declines. A note records it so the
next reader does not have to measure it again.

### Smaller

The dot-boundary rule in the name-existence gate was pinned by nothing — three separate mutations of it
left every test green. It now carries rows that end mid-identifier, which is the only thing separating
it from a plain suffix test, and one where every segment is real and only the separator is not a dot.

That same gate composed a qualified name per type visited, over the whole reference closure, on a path
that runs per keystroke in an editor. It matches segment by segment from the right instead. Equivalence
was measured rather than argued — 4,097 spellings drawn from the closure, old and new answers compared,
zero disagreements — and the allocation it removes is measured too: three full-closure misses fall from
11,528,976 bytes to 2,206,320, the remainder being Roslyn's own member enumeration.

## Fourteenth review cycle (2026-07-30)

Both reviewers landed on the same sentence in the previous cycle's own code, from different repros: the
emitter was reading "I cannot type this" as "the engine has no type either". Everything below follows
from that one confusion, and from measuring the engine instead of reading the generator.

### "Cannot say" is not "dynamic"

`TryTypeCallSiteBody` asked for the model a `:: dynamic` definition's body should be compiled against,
and on `null` — the answer meaning *the emitter has no static type for this call-site value* — it
returned success for a plain definition and left the body dynamically bound. The engine signals a
genuinely untyped model separately and definitely, as `dynamic`. So every call form outside the
previous cycle's matrix diverged: a computed native expression, a chained call, and `this` inside an
`@list` body all carry a real static type in the engine, and the generated tier bound them dynamically.
Measured at the parent commit, with a definition body reading a member the type does not have: the
engine refuses at compile time in every one of them, naming the type it compiled against
(`[Int32]`, `[String]`, `[Boolean]`, `[MenuOption]`, `[Object]`), and the generated tier precompiled
with no diagnostic and either threw `RuntimeBinderException` at render or — where the value was `null`
or the element was `null` — **rendered a page the engine will not compile at all**.

`null` now degrades, in plain mode as in slot mode. That closes the divergence unconditionally, and it
costs precompilation, so the second half of the work was getting the cost back rather than paying it.

**Typing what the engine types.** Two rules were mirrored, both from tables that already exist:

* A computed expression's type is the shared operator tables' own promotion arithmetic —
  `NativeOperatorRules.BinaryResult` / `UnaryResult` / `ClassifyTernary`, the same tables the expression
  writer consults before it emits anything. That gate is exactly the right one: a shape the tables call
  `Supported` is a shape the two tiers already agree about, and a shape they do not the writer would
  refuse to emit anyway, so nothing is lost that the writer had not already lost. The descriptor names
  only the numeric primitives, `bool` and `string`, so a reference or enum result stays "cannot say" —
  except `x ?? null`, whose type is `x`'s own and is carried through by symbol.
* The model inside an `@list` body is the element type, which the host resolves as the collection's
  `IEnumerable<T>` and falls back to `dynamic` for a collection implementing no generic form. A type
  reaching `IEnumerable<T>` more than once is "cannot say": the host picks one by reflection order.

Measured over a sweep of twenty call-site value forms handed to a definition whose body reads nothing
(`<frame>{{[k]}} :: dynamic`), comparing this commit against its parent: the parent degrades **one** of
the twenty (a chained call, whose render type the emitter does not track) and this commit degrades
**two** (that one and a function call). An earlier draft of this section reported "eight of thirteen
before, one after"; that figure is not a parent-versus-child count and no commit reproduces it — it can
only have been measured against an intermediate working tree, after the `null`-degrades change and
before the typing rules below were added back on top of it. Over the same shapes with a body reading a missing member, the type named in the
generator's `HED7008` and the type named in the engine's `HED0001` agree in all fourteen typed shapes,
including the lifted one (`int?` / `Nullable<Int32>`) and the promotions (`Price * 2` is `decimal`, not
`int`). That pairing is the test: a degrade assertion alone would pass whatever type the emitter picked,
and a wrong type is a cast the generated body throws on.

The degrade cost of the whole change, measured on the instruments: the integration suite and all ten
samples' goldens are unchanged.

`@out(this)` inside an `@list` body inside a slot definition's own body was the reverse of a declared
cost: at the parent it was an **unchecked precompile**. Swept over seven slot parameter types, the
parent precompiled all seven and rendered `[[c]]` — and five of those seven are templates the engine
refuses outright (`string`, `int`, `bool`, `decimal`, and an unrelated class, each `HED5014`: the
element is not assignable to the declared slot type). This commit degrades exactly those five and keeps
the two the engine accepts (the element type itself, and `object`), which is the shape a rule has and a
blanket refusal does not.

### A prop the `@list` body was not supposed to lose

The engine saves and restores the active prop layout around **definition** bodies only. An `@list` body
nested inside a definition therefore still resolves its first path segment as a prop, before it looks
at the element at all. The emitter built the item context with no layout, so the element's member of
that name won instead: `<host(Name: string)>{{@list(Products){{@(Name)}}}}` rendered `[PROP]` on the
engine and `[ELEMENT]` on the generated tier. Both precompiled, no diagnostic, different bytes — and
the same template with the `@list` removed, or with `@if` in its place, agreed. The layout propagates
now. The element row next to it (a name the layout does *not* carry still reads off the element) is
what keeps this prop-first resolution rather than the layout swallowing the body.

### Rows that could not fail

The `null` row of the `:: dynamic` body table asserted a degrade and an empty error list, which any
refusal satisfies. Demonstrated: refusing the null literal outright left all nine tests green, and
refusing **every** literal call site outright failed only two rows and passed the other 672 integration
tests. Nothing anywhere asserted that a `:: dynamic` definition called with a literal ever precompiles.
It does now — four rows, each rendering the engine's bytes, one of them reading `Length` off a `string`
literal so it fails both if the body is left dynamic and if it is typed as anything else. Both
mutations redden.

### Two arms nothing could reach

`Classify`'s `TypeKind` switch grouped `Unknown` with `Error` and `Module` with `Submission`, and no C#
compilation produces a symbol carrying either of the first two — so through symbols alone both labels
could be deleted with every suite green. The previous cycle's record named one of them. The kind table
is a function of `TypeKind` now, asked of every kind the enum declares plus a row asserting the rows
cover the enum, so a kind Roslyn adds later arrives with no verdict and the theory does not compile
past it. Deleting either label reddens.

The same "pinned by nothing" verdict applied to the name-existence gate's global-namespace stop, which
the previous cycle's record claimed to have covered: the rows it added end mid-identifier, and the stop
is about running out of namespaces with spelling left over. Removing it turns every leading-dot
spelling — `.System.Object` and its kind — from refused into accepted; the review that reported it
counted 241 such flips over 6,857 sampled spellings. `.Probe.Only.UniqueProbe` is the row that reddens
when the stop goes.

### The durable lesson

A resolver that answers `null` is answering two different questions at once, and the caller has to be
told which. "I could not compute this" and "there is nothing to compute" lead to opposite correct
actions — degrade versus proceed — and the cheap direction is always the wrong one, because proceeding
is what silently produces different bytes. Where two tiers must agree, every "cannot say" belongs on
the refusing side of the branch, and the way to keep that from costing the precompiled tier is to
shrink the set of things that cannot be said, not to widen what "cannot say" is allowed to mean.

## Fifteenth review cycle (2026-07-30)

One root cause with four faces, found independently by both reviewers, and it is the plainest one yet:
**the engine resolves a native expression's path prop-first**, and two of the readers this cycle knew
about had never been told.

> **Corrected by the sixteenth cycle.** "Four paths" was a count of what this cycle had looked at, not
> of what exists. There are at least six places that resolve a path's first segment and at least six
> that build the context the layout travels in; the sixteenth cycle enumerates both lists. Two more
> readers were still resolving off the model after this cycle, and one whole *context* — the content a
> call site hands a definition — never received the layout at all.

### A prop is a prop in every reader, and there are more readers than this section counted

`NativeExpressionCompiler` tries the active prop layout on a path's first segment before it looks at
the scope type at all, so inside a definition body a prop shadows a model member of the same name —
documented, supported, and the engine's only complaint about it is a shadowing warning. The emitter's
member-path reader mirrored that. Two other paths did not:

* The expression writer was constructed with the model and **no layout**, so every prop name inside an
  arithmetic, comparison, concatenation, ternary or function argument was emitted as a member read off
  the model.
* The routine that types a computed call-site value resolved off the model too — it did not even take
  the body context the layout lives on.

Both faces of the first one are severe and neither needed a shadowed name to appear. A prop the model
has **no** member of — the ordinary case, `<host(n: int = 5)>{{[@(n + 1)]}}` — resolved to nothing and
reported `HED7008` at **Error**, so a template the engine compiles and renders broke the consumer's
build. A prop that *did* shadow a member compiled quietly and rendered the model's value: `[8]` where
the engine renders `[P1]`, `[t]` where the engine renders `[a][b]`, `[False]` where the engine renders
`[True]`. Different bytes, no diagnostic, both tiers rendering.

The second one was the same mistake one level up, and the previous cycle's new typing made it visible:
a shadowed name got the shadowed *member's* type, so `Cols + 1` over a `string` prop was typed `Int32`,
and the body built off that type reported a member the `string` it really receives has — `HED7008` at
Error again, over a template the engine renders — or refused a `string` slot the engine fills.

The fix is one sentence, and this cycle applied it twice: give both paths the layout the member-path
reader already had, and resolve the first segment prop-first, hopping the rest off the slot's declared
type. A path whose first segment names a prop never falls back to the member it shadows.

> **Corrected by the sixteenth cycle.** Twice was not enough, and the sweep below could not have shown
> it: every shape in it puts the read directly in a definition body, which is the one context that did
> receive the layout. The prop-argument checker three lines above the writer this cycle fixed was still
> typing its value off the model, so check and emission then disagreed about which value the argument
> even was; and caller content never received the layout in the first place, so the same reads
> reproduced there in full — silent wrong bytes, an `HED7008` build error, and an `@list` iterating the
> wrong string.

Measured over forty prop shapes, before and after, each against the engine: **fourteen matched, five
rendered different bytes and twenty-one degraded** before; **thirty-nine match and one degrades** after,
and the one is a template the engine itself refuses. Zero shapes newly degrade. Two templates that
previously precompiled now degrade, both of them templates the engine refuses to compile: a `string`
slot handed `int` arithmetic over a shadowed name, and a body reading `Length` off an `Int32`. The
corpus classification is unchanged and all ten samples' goldens are unchanged.

### `@list` over something that is not a list

An extension declares the type it accepts, and the engine checks the call's value against it before it
compiles anything: `@list` accepts `IEnumerable`, so `@list(Obj)` over an `object`-typed member is a
template the engine refuses (`HED0004`). The emitter mirrored no such check, so it precompiled and
rendered — walking a string's characters where the value happened to be a string, rendering nothing
where it was an `int`. The check is on the value's *static* type, so a `dynamic` value and a value the
emitter could not type are both exempt, and every enumerable shape is untouched: a `List<T>`, an array
(which reaches `IEnumerable` through `System.Array`), a `string`, and a `List<object>` whose elements
carry no members of their own all still precompile and still match.

### Rows that were one assertion wearing eight hats

The type-kind table's nine `null` rows all asked `UnnameableKind` for a null answer, which is the same
`default:` arm nine times: they passed and failed together, and none was about the kind it named. They
are asked of a real symbol of that kind now, through the classifier the emitter actually calls, so each
row stands alone. The mutation that settles it: making the classifier refuse every enum type leaves the
old rows entirely green and reddens exactly the new `Enum` row.

> **Corrected by the sixteenth cycle.** "Each row stands alone" held for six of the seven. The
> classifier short-circuited an array into a recursion on its element *before* consulting the kind
> table, so no answer the table gave about `Array` was reachable and the `Array` row was a second copy
> of its element's row. Measured: an `Array` case left all thirty-seven green, while a `Struct` case
> reddened `Array` as well as `Struct`. The classifier consults the table first now, so an `Array` case
> reddens the `Array` row — which still also reddens under its element's case, because an array is
> answered by recursing onto its element and no element type escapes that.

Two names could not be given an honest row and are recorded instead of pretended. `Structure` is Visual
Basic's spelling of `Struct` and parses to the same value, so the `Struct` row already answers for it.
That is asserted — but the assertion is about **Roslyn's** enum, not about anything here: no change in
this repository can redden it, and only a Roslyn upgrade splitting the two names ever would. Calling it
something that "would stop being true loudly" overstated what it covers. `Extension` is declared only by newer Roslyn; the
generator compiles against Microsoft.CodeAnalysis.CSharp 4.4.0, which does not declare the name at all,
so a case for it is a compile error and no production change could make a row about it fail. It is
covered by the completeness gate and by nothing else, and there is nothing further to pin until the
generator's own Roslyn moves. Measured, not assumed: adding the case is `CS0117`.

### The durable lesson

A rule that resolves names is not implemented where it is written down; it is implemented at every
place that resolves a name. Prop-first resolution was correct, tested and documented in the one reader
that reads a bare member path, and simply absent from the two that read the same names inside an
expression — so the feature worked in the shape its tests were written in and silently produced other
bytes in every other shape. When a lookup rule is added, the question to answer is not "does the reader
implement it" but "how many readers are there", and the answer is found by looking for every
construction of the thing that does the reading, not by trusting that there is one.

## Sixteenth review cycle (2026-07-30)

Three cycles running, a fix landed correctly and the same defect turned up one path over. This cycle
started by enumerating every place the two rules live rather than by fixing the reported repros, and
the two tables below are the durable part of it. Both were built by reading the emitter, and every row
that should mirror the engine and did not was fixed.

### Table A — every `BodyContext` in `TemplateEmitter.cs`, and whether the prop layout travels with it

The engine saves and restores `ActivePropLayout` around **definition bodies only**
(`HeddleCompiler.cs`, the block that sets it from the resolved layout and restores `savedLayout`).
Everything else compiled under an active layout still sees it — including the content a call site hands
a definition, which the engine compiles *before* the save/restore runs.

| # | Construction site | Layout it carries | Engine's answer | Verdict |
|---|---|---|---|---|
| 1 | document root | none | no layout is active at the root | correct |
| 2 | nested `@list` body (`TryNestedBodyContext`, `ElementOfData`) | the enclosing `Props` | enclosing layout still active — no restore around a `@list` body | correct (fifteenth cycle) |
| 3 | branch / `@for` body (`TryNestedBodyContext`, `Parent`) | the whole enclosing context unchanged | same | correct |
| 4 | untyped bare region body (`TryRegionBodyContext`) | `RegionHostProps` | region body keeps `savedLayout`, the enclosing one | correct |
| 5 | typed region body (`TryRegionBodyContext`) | `RegionHostProps` | same | correct |
| 6 | definition body (`DefinitionBodyContext` + `WithProps(layout)`) | the definition's **own** layout | the layout the save/restore installs | correct |
| 7 | `:: dynamic` definition body (`DefinitionBodyContext`, then `TypedAs` / `WithDynamicBodyModel`) | own layout, preserved through both | same | correct |
| 8 | slot-mode definition body (`AsSlot`) | own layout, preserved | same | correct |
| 9 | **caller content** (`BuildDefinitionCall`, `SlotBodyContext`/`DefinitionBodyContext` + `WithFills`) | **none** | the **caller's** layout is still active: caller content is compiled before the save | **fixed this cycle** |
| 10 | `SlotBodyContext` used to read the slot *type* | n/a — only `ModelSymbol` is taken | n/a | not a body |

`RegionHostProps` is a second copy of `Props` at every site that sets either, so rows 4 and 5 agree
with row 6 by construction; that is worth knowing before anyone "fixes" one of them alone.

Row 9 also lost the enclosing **slot mode**, for exactly the same reason — the engine installs the
callee's `SlotParameterType` after caller content is compiled too — and that was a second, separate
divergence in the same line. Both travel now.

### Table B — every reader of a path's first segment, and whether it tries the layout first

The engine's `NativeExpressionCompiler.TryVisitPropRoot` runs **before** the scope-type check, so a
prop-rooted path resolves with no model at all.

| # | Reader | Prop-first? | Verdict |
|---|---|---|---|
| 1 | `BuildParamExpr`, member-path branch | yes | correct (long-standing) |
| 2 | `BuildParamExpr`, native-expression branch → `NativeExpressionWriter` | yes, but refused outright when the tier was dynamic, before the layout was consulted | **fixed this cycle** |
| 3 | `BuildCall`, top-level function call → `NativeExpressionWriter` | yes, and never had the dynamic-tier guard | correct |
| 4 | `BuildChainItemExpr`, function item → `NativeExpressionWriter` | yes, no guard | correct |
| 5 | `TryBuildDynamicSetter`, prop-argument **check** (`_resolver.ResolvePath`) | **no** — typed off the model, three lines above the writer that reads the prop | **fixed this cycle** |
| 6 | `TryBuildDynamicSetter`, prop-argument **emission** → `NativeExpressionWriter` | yes | correct (fifteenth cycle) |
| 7 | `NativeExpressionWriter.WritePath` | yes (`PropRoot`) | correct (fifteenth cycle) |
| 8 | `NativeExpressionWriter.EstimatePath` | yes (`PropRoot`) | correct (fifteenth cycle) |
| 9 | `CallSiteValueType`, member-path branch | yes | correct (fifteenth cycle) |
| 10 | `ComputedValueType`, `PathNode` (`PropRootType` / `IsPropName`) | yes | correct (fifteenth cycle) |
| 11 | `DynamicDefinitionBodyModel` | yes | correct (fifteenth cycle) |
| 12 | `ResolvedTypeOf` | n/a — a helper called with a start type already chosen | not a first-segment reader |

`ResolveModelType` appears in four places and reads none of them: it resolves *type names* from the
`::` and `@model` grammar, which are never prop names.

### Caller content is compiled under the caller's layout (Table A row 9)

Both reviewers found this independently. The content a call site hands a definition was built from the
**callee's** model with no layout and no slot mode, and every read in it diverged in its own way.
Measured at the parent commit against the engine, model `GridModel { Name = "model", Cols = 7 }`:

* `<outer(Cols: string = "PP")>{{@inner(this){{[@(Cols)]}}}}` — engine `([PP])`, generated `([7])`.
  Silent, both tiers rendering, different bytes. The native form `@(Cols + "!")` is `([PP!])` against
  `([7!])`.
* `<outer(label: string = "PP")>` with `@(label)` — engine renders `([PP])`; the build **fails** with
  `HED7008: 'GridModel' does not contain an accessible member 'label'`. That is the symptom the
  previous commit's message claims to have eliminated, alive one context over.
* `@list(Name)` in caller content, prop `"PP"` against member `"ab"` — engine `([P][P])`, generated
  `([a][b])`: the enumerability gate had judged the *model member's* type.
* A valued `@out` in caller content lexically inside a slot definition's body — engine renders, the
  emitter dropped the whole template. The bare `@out()` in the same place is the reverse: the engine
  refuses with `HED5013` and the emitter precompiled and rendered.

All of them match now, and the near neighbour is in the table: a name the caller's layout does *not*
carry still reads off the callee's model.

### A prop argument is checked against the prop it reads (Table B row 5)

`TryBuildDynamicSetter` typed the argument off the caller's model while the writer three lines below it
emitted the caller's *prop*. That is worse than either half alone: the check and the emission
disagreed about which value the argument even is. `<inner(q: int = 0)>` called as `q: Cols` with
`<outer(Cols: string = "PP")>` rendered `[PP]` — a `string` boxed into an `int`-declared slot — where
the engine refuses the template with `HED5003`. Reversed (`q: string`, `Cols: int`) it rendered `[5]`
against the same refusal. It is prop-specific: a model member or a literal in the same position always
precompiled. This was pre-existing, but the previous commit is what made the two halves disagree — at
its parent the emission rendered the model member, which is what the check had approved.

Two over-degrades fell out of the same change and were kept: a non-shadowing prop argument (`q: p`
where the model has no `p`) now precompiles and renders, and a prop-rooted argument at a call site on
the dynamic tier no longer needs a typed caller model.

### A native expression over a prop needs no model (Table B row 2)

`BuildParamExpr` refused every native expression when the context was dynamic — an `@list` body's tier
— before it looked at the layout, so `<host(n: int = 5)>{{@list(Tags){{[@(n + 1)]}}}}` dropped the
whole template where the engine renders `[6][6]`. The guard is gone rather than narrowed: the writer,
given no model type, already refuses any path that reads one, which is the engine's own order and the
engine's own answer. A constant expression that needs no model at all precompiles for the same reason,
and that is what moved `at-escape-comment-adjacent.heddle` from `FallsBackSafely` to `Precompiles` —
its `@if(true)` had been refused for needing a model it does not read.

Still degrading, and reported rather than hidden: a native expression reading the **element's** own
member inside an `@list` body (`@(Name + "!")` over `List<Product>`). The engine compiles it against
the element type; the emitter has that type in `DynamicBodyModel` but emits the body's reads on the
dynamic tier, so typing the writer off it is a change of a different shape and was left for a later
cycle. The plain path `@(Name)` and the function form `@upper(Name)` are unaffected.

### What a chain hands the next link is text, not the producer's value

The flattening of a one-item chain call-parameter carried a comment claiming it was "byte-identical".
It is not, and the comment is corrected in place. A chain call-parameter is compiled as a chain and the
engine hands the extension `callParameter.RenderType` — the last item's `InitStart` return, which for
every chain the emitter can flatten is `AbstractExtension`'s default `typeof(string)`. So the value is
the carrier's **rendered text**. Flattened to the producer's own expression it was invisible wherever
the consumer merely printed it, and wrong wherever the consumer was type-sensitive. Measured at the
parent commit:

* `@list(len(Name))` with `Name = "abcd"` — engine `<4>` (it iterates the characters of `"4"`),
  generated **empty**. Silent, both tiers rendering.
* `@list((Cols))` with `Cols = 7` — engine `<7>`, generated empty. Neither reviewer reported this one;
  it is the same defect one syntax over, which is why the enumeration came first.
* `<probe>{{[@(Length)]}} :: System.String` called as `@probe(len(Name))` — engine `[1]`, generated
  **`InvalidCastException` at render**.
* `@list(upper(Name))` matched all along, because `upper` already returns a string. That is the whole
  reason this survived so long.

The emitter reproduces the carrier now, through `PrecompiledRuntime.CarrierValue`, which is
`EmptyExtension`'s own pass-through: `null` becomes the empty string, a string is itself, anything else
is `ToString()`. The type follows: `CallSiteValueType` answers `System.String` for a chain, so the
`@list` gate and the slot-value check judge the value the extension actually receives, and a
`:: dynamic` definition called with a chain is typed by it instead of degrading.

The sibling is a different syntax with a different answer. A multi-argument call is a *native
expression*, not a chain — no carrier renders it — so the engine keeps the function's return type and
refuses `@list(min(1, 2))` outright, naming `System.Int32`. `ComputedValueType` had no `CallNode` arm,
so the gate exempted every call as "cannot say". It asks the shared ranker for the return descriptor
now. The near neighbour that keeps this from being a refusal of all calls: `@list(substr(Title, 0, 2))`
still precompiles and still matches.

### An extension prop layout with no demonstrated red

`ResolveExtensionPropLayout` filled its slot types from `[Prop]` metadata without the `CanWriteTypeName`
gate the definition layout applies, so a slot type this assembly may not name would have been spelled
into the cast a prop read emits. No call path makes an extension layout the *active* layout, so there
is nothing to demonstrate and no test pretends otherwise; the gate is there so that the day one does,
the spelling is checked before it is written. This is a guard, not a fix.

### The `Array` verdict row could not fail on the arm it named

`Classify` short-circuited an array into a recursion on its element before consulting `UnnameableKind`,
so `UnnameableKind(TypeKind.Array)` was unreachable and the row testing it was a second copy of the row
for its element's kind. Measured at the parent: an `Array` case left all thirty-seven rows green while
each of the other six kinds reddened its own; the `Array` row did redden under the `Struct` case,
because `int[]`'s element is `int`. The table is consulted first now — it returns null for `Array`, so
no verdict changes — and the mutation matrix re-measured: an `Array` case reddens the `Array` row, and
the `Class`, `Enum`, `Interface`, `Delegate` and `Dynamic` cases each redden exactly one row. `Struct`
reddens two, its own and `Array`, and that is not removable: an array is answered by recursing onto its
element and every element has a kind with a row. The docstring says so now instead of claiming
independence it does not have.

### Degrade sweep

Every template that newly degrades is one the engine refuses, checked one by one: a prop argument whose
prop type does not fit the slot (`HED5003`, both directions), `@list` over a native call returning a
non-enumerable (`HED0004`), an `@out` of a chain value into a slot that is not `string` and an `@out` of
an `int`-returning call into a `string` slot (`HED5014` each), a caller-content prop path whose
remaining segments do not resolve off the prop's type (`HED0001`), and a bare `@out()` in caller content
under an enclosing slot definition (`HED5013`). Nothing else newly degrades: the corpus intent table
moved in the opposite direction only — one entry from `FallsBackSafely` to `Precompiles` — and no
`ExpectPrecompiled` in any suite had to be relaxed.

### The durable lesson

The previous cycle wrote the right lesson — look for every construction of the thing that does the
reading — and then did not carry it out, which is how the same class survived a third cycle. The
missing step is that a lookup rule has **two** populations, not one: the readers that apply it, and the
contexts that carry the state it reads. Fixing every reader while one context never receives the state
leaves the rule exactly as broken as before, and a sweep written around the contexts that do carry it
will report full coverage. Both lists belong in the record, written out in full, before the first fix.


## Seventeenth review cycle (2026-07-30)

The previous cycle's two enumerations — every `BodyContext` construction and whether it carries the prop
layout, every reader of a path's first segment and whether it tries props first — were re-derived
independently by two reviewers, and both came back complete with no wrong verdict. That is the first
completeness claim in this series to survive two independent re-derivations, and it is worth recording
that the enumeration approach *worked*: three cycles of "fixed it, and the same defect turned up one
path over" ended when the lists were written out in full before the first fix. Nothing in those two
tables moved this cycle.

What did move came from the opposite direction. The gates the previous cycle added were correct about
the shapes they were derived from and blind to a whole class next to them, and the reason is worth
stating precisely.

### A kind funnel cannot say what a name says

The check that types a call-site value routes a function call through the shared operand *descriptor*:
the ranker picks an overload, `ReturnKind` turns that overload's return type into an `OperandKind`, and
the emitter turns the `OperandKind` back into a type symbol. The descriptor names the numeric
primitives, `bool` and `string`, and nothing else — every other return type arrives as "some other value
type" or "a reference", which the emitter reads as *cannot say*, which every gate exempts.

`range` is the only row in the built-in table whose return type is none of those three, and it is
exactly the built-in a reader reaches for when they want to iterate. So:

* `@list(range(1, 3)){{<@()>}}` — the engine refuses the template outright (`HED0004`, naming
  `Heddle.Models.Range` against `System.Collections.IEnumerable`); the generated tier precompiled and
  rendered **empty**, having handed `ListExtension` a `Range` to iterate.
* `<s(out:: object)>{{[@out(range(1, 3))]}}` — the engine refuses (`HED5014`, `Range` into
  `System.Object`, boxing switched off for this check); the generated tier precompiled and rendered.

Both were already true at the parent commit, so this is not a regression — but the commit's degrade
sweep reads as complete and was not.

The suspicion that follows by construction was measured rather than reasoned about, over a purpose-built
export container in an assembly of the suite's own so no shared fixture moved: a host `[ExportFunctions]`
function returning a `DateTime` or a class of its own slips **both** gates in exactly the same way, in
`@list` position and in `@out` position. Four rows, all four confirmed.

The fix is to stop funnelling: the chosen overload carries its declared return type — a metadata name in
the built-in table, an `ITypeSymbol` for an export — and typing the call from *that* closes `range` and
the export class by construction rather than by enumeration. `dynamic` is the one return type that must
not be taken at face value, because there is no such thing in metadata: the engine reads a `MethodInfo`
whose return type is `System.Object`, and that is what it checks, so the emitter maps it there too.
Measured both ways — an export returning `dynamic` into an `object` slot precompiles and matches; into a
`string` slot both tiers refuse — and the mapping is pinned: without it, the `object` row degrades.

### `[DataType]` is a rule, not a list of extensions

An extension declares the types it accepts with `[DataType]`, and the engine checks the call value
against every one of them before it compiles the call (`HeddleCompiler.CheckTypes`, reading the
attribute with `inherit: true` and asking `Type.IsAssignableFrom`). The emitter had that check written
out for `@list`'s `IEnumerable` and for nothing else, which left `@for` — the only other `[DataType]`
built-in the emitter still binds, the rest degrading on their own — unchecked. `@for` accepts a `Range`
or an `int`; measured against the engine with `Cols = 7`, `Name = "ab"`:

* `@for((Cols)){{<@()>}}` and `@for(len(Name)){{<@()>}}` — the engine refuses both (`HED0004`, naming
  `System.String` against `[Heddle.Models.Range, System.Int32]`), because a chain call-parameter reaches
  the extension as the carrier's rendered **text**. The parent commit rendered 7 and 2 iterations; this
  commit's parent rendered **0**, the previous commit having taught the emitter to hand `@for` the
  string. Both are wrong, in different ways, and neither degraded.
* `@for(Name)` over a `string` member and `@for(Price)` over a `decimal` are the same refusal without a
  chain in sight.

Generalising costs nothing measurable: the check reads the attribute off the bound extension now and
applies to every extension the emitter binds, `@list` included, and the whole corpus is byte-identical
with it — same classification, same generated sources, same manifest, same diagnostics. The near
neighbours are pinned in full: the literal, an `int` member, `range(...)`, `Count + 1` and a nullable
`int` (which the engine unwraps before it checks) all still precompile and still render the engine's
bytes. `[DataType]` gates for built-ins other than `@list` is closed as a known-open item; there is no
`[DataType]`-declaring built-in the emitter binds that is not covered.

Reflection's assignability is reproduced, not C#'s: identity, a base class, an implemented interface, or
the boxing every value has to `object`. There is deliberately **no** numeric widening, because
`Type.IsAssignableFrom` has none — which is why `@for` over a `long` is a template the engine refuses.

### Caller content under a `:: dynamic` callee was still untyped

The sixteenth cycle taught the emitter that `:: dynamic` does not declare an untyped body — the engine
compiles it once per call site, off the value that call site passes — and applied that to the definition
body only. The content the **call site** hands the definition kept the untyped context, and it is
compiled against the same model. Swept over twenty-one shapes against the engine — a member read, a
missing member, `this`, a native expression, a function call, `@list`, `@for`, `@if`, two adjacent
carriers, and the same again under a `:: T` callee, under a slot-declaring callee, and over a call site
passing a literal, a member path or `this`:

* `@frame("ab"){{(@(Title))}}` and `@frame(this){{(@(Nope))}}` — the engine refuses at compile time
  (`HED0001`, naming `String` and the caller's model respectively); the generated tier **precompiled**.
* `@frame(Name){{(@(Length))}}` and `@frame(Name){{(@(Zzz))}}` — a member path is the engine's own
  dynamic exit, so it compiles the template and binds at render; both tiers matched, throwing the same
  `RuntimeBinderException` on the second. That half must keep matching, and it does: the same rule that
  types the body types this, so a call form the engine itself types `dynamic` still gets untyped caller
  content.

Sixteen of the twenty-one matched before the fix and eighteen match after it. Three shapes degrade where
the engine renders, unchanged by this cycle and reported rather than hidden: a native expression, a
function call and an `@if` in caller content under a `:: dynamic` callee.

### The sweep category that did not exist

The sixteenth cycle's degrade sweep enumerates templates that newly **degrade**. That category cannot
hold `@for` over a chain, which kept precompiling across the commit and rendered *different bytes* —
seven iterations at the parent, none after. A template whose precompiled output moves while it goes on
precompiling is invisible to a degrade sweep, and invisible to a differential test as well whenever the
engine refuses the template, because then there is no dynamic render to compare against.

So the sweep has two counts now, and this cycle reports both: **newly degrading**, and **bytes moved
while still precompiling**. The instrument for the second is the same corpus run captured twice —
classification, every generated `.g.cs`, the manifest and the diagnostic list — and diffed. This cycle:
**thirteen** template shapes newly degrade and **zero** moved bytes. Every one of the thirteen is a
template the engine refuses, checked one by one — `HED0004` for `@list(range(1, 3))`, for `@list` over a
host export returning a class or a `DateTime`, and for `@for` over a chain value, a `string` member or a
`decimal` member (six); `HED5014` for a `Range`, a host class, a host `DateTime` and a host `dynamic` as
a slot value the slot cannot take (five); `HED0001` for a caller-content member read under a
`:: dynamic` callee whose call site passes a literal or `this` (two). Across the 63-template corpus and
the ten samples nothing moved at all, in either direction: same classification, same generated sources,
same manifest, same diagnostics, all ten goldens unchanged.

### Two smaller things, both examined rather than assumed

`bctx.IsDynamic ? null : bctx.ModelSymbol` was a no-op at both sites it appeared: every construction
site upholds `IsDynamic ⇒ ModelSymbol is null`, verified again here by reading all eleven of them. The
guard read as though the two could disagree, so it is gone and the invariant is stated on the field
instead, next to the pointer to `DynamicBodyModel`, which is the field that answers the *different*
question of what the engine typed a dynamically-emitted body against.

The writer the call-typing pass builds is thrown away undrained, so a refusal it proves is discarded.
That costs nothing, and the reason is structural rather than a survey: a call the ranker refuses has no
return type either, so the value stays "cannot say", no gate can refuse the template on account of it,
and the writer that *emits* the same expression is always reached and reports what this one saw. Pinned
in both gated positions — an ambiguous call as an `@list` value and as an `@out` slot value each still
report `HED7025` at Error.

### The extension prop-layout guard, and what measuring it found

The gate the sixteenth cycle added — a prop whose type this assembly cannot name fails the layout —
stopped part-way through building the layout, leaving a **truncated** one in the cache that only the
`Failed` flag stood between and a caller. The whole declaration list is checked before a single slot is
built now, so a failed layout carries no slots at all; the flag is unchanged and so is every observable
behaviour.

Measuring the guard for a red found the opposite of one. Built out — a host extension declaring
`[Prop("box", typeof(InternalModel), Optional = true)]` over an `internal` type, called as
`@hiddenProp()` — the engine renders `[box=<null>]`, and so does the generated tier **once the gate is
removed**: identical bytes, and the generated code compiles, because no consumer of an extension prop
layout ever spells the slot type. A required prop of that type is refused earlier for having no default;
a dynamic setter boxes to `object` and emits no cast; an optional one writes the literal `null`. So the
gate as written costs a working precompiled template and emits an `HED7030` telling the consumer to make
an internal type public for no reason. It is left in place — it is outside this cycle's brief and
removing it re-opens the case it was added for — and recorded here with the repro so the decision is
made deliberately rather than by nobody.

### The durable lesson

Two rules met the same wall from opposite sides. The `@list` gate knew one extension's accepted type
because someone wrote it down; the call-site typing knew one *shape* of return type because the
descriptor it went through could only spell that shape. Both were correct about everything they could
see and silent about everything else, and in both cases the fix was to read the authority instead of a
projection of it — the `[DataType]` attribute, the overload's declared return type — after which the
cases nobody enumerated are covered by construction.

The general form: when a check has to answer "what type is this", ask what the *engine* asks and read
the same source it reads. A shared descriptor built for a different question — here, deciding whether an
operator may be emitted — is a lossy projection, and a lossy projection used as a type answer turns
"I know exactly what this is" into "I cannot say" for every case outside its range. That failure is
silent by construction, because "cannot say" is always the exempting answer.

## Eighteenth review cycle (2026-07-30)

Two reviewers, working independently, found the same two regressions in the previous commit and the same
structural gap underneath one of them. The corpus was byte-identical across that commit and could not
have seen any of it.

### Reflection's assignability was claimed, not reproduced

The accepted-type gate the previous cycle generalised compares the value's type against each declared
`[DataType]` with `SymbolEqualityComparer` over the base chain and the interface set. That is nominal
identity. `Type.IsAssignableFrom` is not: it has generic variance, array covariance and the CLR's own
`Nullable<T>` treatment in it. The doc comment enumerating "identity, a base class, an implemented
interface, or the boxing to `object`" was itself the defect — the enumeration read as complete and the
relation it described is a strict subset of the one the engine asks.

Measured against the engine, six shapes the engine renders and the parent commit precompiled had started
degrading: `int` and `int?` into `[DataType(typeof(int?))]`, `List<string>`, `IList<string>` and
`string[]` into `IEnumerable<object>`, and `string[]` into `object[]`. The nullable rows are the sharpest
and are not a variance subtlety: the gate unwraps the *value's* nullable and compares against the
un-unwrapped declared one, so `[DataType(typeof(int?))]` accepted nothing whatever — not even an `int?`.
That declaration was entirely non-functional.

The fix is not a longer enumeration. The generator already carries one adapter whose whole job is to
answer the CLR relation over symbols, with its verified Roslyn-vs-CLR corrections and a shared
conformance corpus driven from both tiers; the gate now asks it.

> Corrected in the nineteenth cycle. This paragraph originally claimed that "variance, array covariance
> and the nullable domain come with it, by construction rather than by list". Generic variance and the
> nullable domain did; array covariance did not. Roslyn classifies `uint[] → int[]` as no conversion at
> all, so the adapter refused every array pair whose element types the CLR reduces to one — twenty such
> disagreements survived this commit, and the corpus had no row in that family to catch it. The claim was
> the same failure mode this entry's own durable lesson names: a completeness claim nothing tests.

Why no fixture caught it: no built-in is affected — `@list`'s `IEnumerable` and `@for`'s `Range`/`int`
are exact matches at every call site the corpus has — and neither the corpus nor the samples contain a
single host extension declaring `[DataType]`. "The corpus is byte-identical" was true and closed nothing.
Four such extensions now exist, covering the nullable lift, generic covariance through a class and
through an interface, array covariance, and the inherited-declaration walk; rows in both directions, and
two mutations that redden disjoint halves of them — an acceptance that answers yes to everything reddens
every refusal assertion, and one back to nominal identity reddens every acceptance row that is not an
exact match.

> Corrected in the nineteenth cycle. This originally read "thirteen rows in both directions, and a
> mutation that accepts everything reddens five of them while a mutation back to nominal identity reddens
> six". Neither count reproduces: both mutations redden the same number of tests, and the two figures
> were rows counted one way and tests the other.

### `:: object` is the engine's predicate; `dynamic` is a spelling

The engine's test for "compile this body against the value the call site passes" is
`acceptType == typeof(object)` in `HeddleCompiler.CreateExtension`. `dynamic`, `object` and an
undeclared model all resolve there — the parser fills in `object` when no `::` is written, and the alias
table maps `dynamic` to `object`. Every rule in this area had been keyed on the *word* `dynamic`, so
`:: object` still carried the whole class of divergence `:: dynamic` had, in both directions:

* `<s(out:: object)>{{[@out(this)]}} :: object` called `@s(5)` — the engine refuses (`HED5014`, `Int32`
  into `System.Object`, boxing switched off for this check); the generated tier precompiled and rendered
  `[|5|]`. The `:: dynamic` twin degraded correctly. The emitter typed that body `object`, so `@out(this)`
  looked like the identity `object → object`.
* `<frame>{{@for(this)}} :: object` + `@frame(2)`, `<frame>{{@list(this)}} :: object` + `@frame("ab")`,
  `<frame>{{[@(Length)]}} :: object` + `@frame("ab")`, the same with caller content, and the undeclared
  form of each — the engine renders all of them and the generated tier degraded, because an `object`
  model is a supertype of the one the engine typed and every gate over it can only over-refuse.

The rule is re-keyed on the engine's predicate. `:: object` and `:: dynamic` do *not* collapse into one
answer, though, and the difference is load-bearing: only `dynamic` sends the engine's model accessor
down its dynamic exit, so under `:: object` a member path at the call site resolves statically and
`@frame(Name)` hands the body a `string` — a member the `string` lacks is then a compile-time refusal
where the `:: dynamic` twin is a render-time throw that both tiers share. Routing the two together would
have emitted a dynamically bound read for a template the engine will not compile. Both halves are pinned.

One more shape needs the engine's answer rather than the emitter's caution: a caller whose own scope has
no static type. The accessor resolves nothing against a dynamic scope and the bare-call arm reads that
same dynamic scope type, so the body's model is `dynamic` — not "cannot say". A model-less document is
nothing but this shape, and answering "cannot say" took every one of them off the precompiled tier.

### An `[Obsolete(…, error: true)]` export broke the consumer's build

The highest-severity finding of the cycle, pre-existing and on no known-open list. Reflection ignores
`[Obsolete]` entirely, so the engine calls such a function and renders; the generated file writes the
call out as C# into the *consumer's* assembly, where the error form is CS0619. Measured four ways —
expression position and `@out` slot position, with a `string` return and a class return — the consumer's
build stopped, at both commits, on a `.g.cs` no one can edit, over a template that is not at fault.

> Corrected in the nineteenth cycle. Three of those four cells were committed as test rows; the
> slot-position-with-a-class-return cell was measured and then not written down. It is a row now, and it
> reddens with the other three when the method arm of the guard is removed.

The emitter already had the doctrine: a type carrying `[Obsolete(error: true)]` is one this assembly may
not name, and the template degrades with `HED7030` rather than pre-compiling a name the build will
reject. The rule had simply never been asked about the *method* a call is written to. It is now, over
both names the emitted call spells — the container type and the method. Three near neighbours keep it
from being a refusal of exports, of `[Obsolete]`, or of the container: warning-level `[Obsolete]` raises
nothing (the generated file opens with a blanket `#pragma warning disable`) and must keep precompiling;
an export whose *return* type the consumer may not name compiles and renders identically, because the
call site spells the method and not what it hands back; and the same container's undecorated export is
unaffected. Removing the check turns all three degrading rows back into CS0619 in the generated code.

### The extension prop-layout guard, reversed

The previous cycle added this gate on a suspicion and recorded the suspicion honestly. Two reviewers
have now measured it and it is a net negative, so it is gone.

The verification, re-done here rather than taken on trust: the extension layout has exactly three
consumers — the frozen props prototype, the parameter-name field and the binding row's fingerprint — and
none of them writes a type name. The prototype stores boxed values and the only type-writing branch in
the value formatter spells a numeric C# keyword; the parameter-name field holds strings; the fingerprint
is a manifest string the runtime recomputes from the live type. The cast-emitting prop reader is
unreachable from an extension layout, because every layout that becomes a body's active props is a
*definition* layout. A dynamic setter refuses outright unless the argument type matches the slot exactly
or widens numerically, so it too writes a keyword or nothing.

Measured with a host extension declaring `[Prop("badge", typeof(InternalBadge), Optional = true)]` over
an `internal` type: gate on, every call shape degrades with `HED7030@Warning` while the engine renders;
gate off, all of them precompile, the generated code compiles, and the bytes match the engine — through
the default, a literal argument and a dynamic setter alike, and through the resolver gauntlet under
`PrecompiledMismatchPolicy.Strict`, where the fingerprint round-trips. The definition layout's identical
gate is untouched and is not the same case: a definition layout really does become a body's active one.

### A third sweep category

The sweep had two counts — newly degrading, and bytes moved while still precompiling. Both reviewers
found the same missing third: **newly precompiling**. A template that starts precompiling is new
generated code reaching consumers, and neither existing count can hold it.

This cycle, over the 58-template corpus captured twice and diffed on classification, generated source
and rendered bytes, plus the ten samples' goldens and their generated files: **zero** newly degrading,
**zero** moved bytes, **one** newly precompiling — a model-less template whose definition declares no
model and whose body branches around an `@out` projection. It refused because that body was typed
`object`; typed the engine's way its branches and projection are ordinary emissions, and it now renders
the engine's bytes through the corpus parity gate. Its intent row moved with it.

### Two tests that could not fail

An assertion read `slotDyn` where `objectDyn` was meant, which left the third scenario of its own test —
the reference conversion the engine's table *does* allow — with no byte assertion at all, only
tier-versus-tier equality. Proven by changing that scenario's caller content: the render moved and the
test still passed. Fixed, and the mutation now reddens it.

The inherited-`[DataType]` walk had no test naming it: reading only the extension type's own attributes
left every suite green. The behaviour is right — the runtime reads the attribute with `inherit: true` —
and an extension declaring `int` over a base declaring `string` now pins it from both sides, with a third
type refused on both tiers.

### The durable lesson

Both regressions have the same shape, and it is not the previous cycle's shape. There the fix was to
read the authority instead of a lossy projection of it; here the authority was read and then
*re-implemented* — `Type.IsAssignableFrom` written out as a four-case enumeration, the engine's
`acceptType == typeof(object)` written out as a string comparison against `"dynamic"`. Both
re-implementations were accompanied by a comment asserting fidelity, and in both cases the comment is
what a reader would have believed instead of checking.

So: when a rule mirrors an engine predicate, prefer calling the one adapter that already answers it over
restating it, and when restating is unavoidable, write down the predicate's *source expression* rather
than a prose enumeration of the cases someone thought of. An enumeration is a claim of completeness that
nothing tests; a pointer to the source is a claim a reader can check in one step.

## Nineteenth review cycle (2026-07-31)

One regression, introduced by the previous commit — and introduced by the *correction* it added alongside
a fix, not by the fix. Three pre-existing divergences, two of them in the adapter the previous cycle had
just made load-bearing. Three inaccuracies in the eighteenth-cycle entry above, corrected in place.

### A region body forgot its host's model

The previous commit added an arm to the `:: object` body-model rule for a caller whose own scope has no
static type: such a body's model is `dynamic`, because the engine's accessor resolves nothing against a
dynamic scope. That is right for the shape it was written for — a model-less document — and wrong one
level down, because the engine tries a *body prop read* before it consults whether the scope is dynamic.
A prop's own type stands whatever the scope is.

The arm was reachable only through one context, and only because that context was under-informed: a
region body was rebuilt from its host's model cast, model symbol and dynamic flag while the host's
`DynamicBodyModel` — the type the engine has in hand behind a dynamically-emitted body — was dropped.
The region kept the host's *prop layout* and lost the host's *model*, which is the one combination that
reaches the new arm.

Measured over a model-less document whose component declares `p: string`, fills a region, and calls
`@d(p)` from that region body, with nothing spelling `dynamic` anywhere but the `@model` directive:

* `<d>{{@for(this)}}` — the engine refuses (`HED0004`, `String` against `Range`/`int`); the commit
  precompiled it and rendered a bare newline, with no diagnostic at build or at run. The body model was
  `dynamic`, so the accepted-type gate took its dynamic exemption and `@for`'s `[DataType]` never ran.
* `<d>{{[@(Nope)]}}` — the engine refuses (`HED0001`, on `String`); the commit precompiled it and threw
  `RuntimeBinderException` at render. The same through the caller's content, which is typed by the same
  rule.
* The parent commit degraded all three.

Which of the two is the root was decided by measurement, not by argument. Adding the missing prop-read
guard to the arm closes all three faces — and also degrades `@d(p)` with a body the engine renders
(`[@(Length)]` over the string prop, `[4]` on both tiers), because with no host model in hand the value
question has no answer and the guard routes to "cannot say". Carrying the host's `DynamicBodyModel` into
the region body closes the same three faces and keeps that template precompiling. The arm was right; the
context was under-informed. **Degrade cost: zero** — no template that precompiled correctly stopped.

### `Nullable<TEnum>` reached `System.Enum`

The adapter's Roslyn-boxing correction excluded exactly one target kind: an interface. `int? → IComparable`
classifies as boxing because the boxed `int` implements it, and the CLR says false because `Nullable<T>`
implements nothing — so the correction was written as "boxing, except a nullable into an interface".
`System.Enum` is a class, so `DayOfWeek? → Enum` survived it, and the CLR says false there for the same
reason: `Nullable<T>`'s base chain is `ValueType` and `object` and stops.

Reachable through the shared prop-layout rule, which asks the adapter whether a re-declared prop type is
assignable to the inherited one. An extension re-declaring an inherited `Enum` prop as `DayOfWeek?` is
`HED5008` on the engine and refuses the template; the generated tier accepted the layout and rendered.
The correction is now phrased as the CLR's own question — a boxing conversion out of a `Nullable<T>` is
assignable only where the target is on `Nullable<T>`'s own hierarchy — which subsumes the interface case
rather than sitting beside it. The neighbour that keeps it from being a refusal of nullable
re-declarations is the same `DayOfWeek?` against a `ValueType` base, which is on that hierarchy, accepts,
and renders the engine's bytes.

### Array covariance over the CLR's reduced element types

The same adapter, the other direction, and the claim the eighteenth-cycle entry got wrong. The CLR
compares array element types after reducing an enum to its underlying primitive and each signed/unsigned
integer pair to one representative, so `uint[] → int[]`, `byte[] ↔ sbyte[]`, `long[] ↔ ulong[]` and
`DayOfWeek[] → int[]` are all assignable, and it propagates — through the array's own generic interfaces
(`uint[] → IList<int>`) and through a jagged array, whose element type is itself an array. Roslyn
classifies none of these as a conversion at all, so the adapter refused every one: twenty rows in one
sweep, thirteen in another, all the same family. The engine renders them; the generated tier degraded.

Not a regression — the nominal comparison the adapter replaced refused the same rows — but it made the
previous cycle's "by construction rather than by list" false as written.

*Two scope claims in the two paragraphs above were corrected the following cycle.* "Each signed/unsigned
integer pair" named three pairs and omitted the pointer-width one — `nint`/`nuint` are not in the C#
numeric-conversion table the reduction reads from, so `IntPtr[] ↔ UIntPtr[]` stayed refused. And
"reducing both sides answers the array interfaces" is half of that sentence's claim: reducing the arrays
answers `uint[] → IList<int>`, where the *source* is what reduces, and leaves `int[] → IList<uint>`
refused, where the interface's own type argument is. Both are written up under "Where the reduction
stopped short" below.

The rule is one clause: reduce both sides' element types and re-ask. The re-ask is what answers the
propagated forms without naming them, and it terminates because the reduction is idempotent. Direction
safety is pinned from the other side too: `int[] → object[]` is false at runtime for value-type elements,
and a mutation that accepts any two arrays of equal rank reddens that row along with `int[] → ValueType[]`,
`DayOfWeek[] → Enum[]`, `char[] → ushort[]` and `bool[] → byte[]` — every same-width pair the CLR does
*not* reduce.

`AssignabilityCorpus` had four array rows, none in the disagreeing family, and no `Nullable<enum>` row.
It has both now, with their refusing neighbours, and the reflection-side driver re-derives every
expectation from the live CLR relation on each run, so the committed values are generated data.

### A declared `:: T` was never checked against what the call site passed

`<frame>{{[@()]}} :: System.String` called `@frame(Nested)` — the engine refuses (`HED0004`); the
generated tier precompiled and rendered the `Nested`. With `:: System.Int32` and `@frame(Name)` it
rendered the string; with a body reading `Length` it threw `InvalidCastException` at render. The
declaration was being read as the body's model and never as a constraint on the value, so generated code
cast the value to `T` and carried on.

The engine's check is asymmetric, and mirroring the asymmetry is the whole of the fix. `CheckTypes` runs
against the *input model type* its accessor produced, which exists only for a value the accessor resolved
statically — a body prop read or a member path. A literal, `this`, a computed expression, a chain and a
path ending in a dynamic hop leave it with none, and the engine then compares the declared type with
itself and passes. So `@frame(5)`, `@frame(this)` and `@frame(len(Name))` against a `:: System.String`
are templates the engine compiles and renders, and a stricter rule than the engine's would take all three
off the precompiled tier. All three are pinned as must-precompile rows beside the three refusals, and a
mutation that drops the exemptions reddens exactly those three.

### The sweep

Two populations, each captured at `HEAD` and in the working tree and diffed on classification, generated
source and rendered bytes: the corpus (63 templates, through the classification and render-parity gates)
and a 773-row grid of definition, region and accepted-type shapes — declared model × call-site value ×
body, each in a plain document and inside a filled region body, plus every accepted-type call the fixtures
support.

* **Newly degrading: 30.** The engine refuses all thirty, and at `HEAD` all thirty were divergences —
  twenty-four rendering a page the engine will not compile, six throwing at render. No template that both
  tiers agreed on stopped precompiling.

  *Corrected the following cycle.* That result is this grid's, and it does not generalise: an independent
  572-row population run afterwards found **135** newly degrading, of which **4 had been matching** — the
  array-reduction under-acceptances written up under "Where the reduction stopped short" below. "No
  template that both tiers agreed on stopped" was true of the population that was measured and was read as
  a claim about the change. A degrade count is only ever a fact about the grid that produced it, so the
  breakdown that matters — how many of the newly degrading were *previously matching*, not how many the
  engine refuses — has to be stated per population.
* **Bytes moved while still precompiling: 0.**
* **Newly precompiling: 22.** All twenty-two are region bodies that can now be typed because the region
  carries its host's model, and every one renders the engine's bytes exactly.
* Divergences remaining in the grid: **zero**, down from thirty. The corpus sweep is byte-identical, and
  the ten samples' goldens are unchanged.

### One arm kept, marked unreachable

The export guard checks both names an emitted call spells, the method and its container. The container
arm has no reachable path: naming an obsolete-error type in `[ExportFunctions]` is CS0619 in the assembly
that declares the export, and no pragma there suppresses it. It stays, so the two names are guarded
alike, with a clause saying so — an arm that looks live and is not is a worse trap than one that is
labelled.

### The durable lesson

The regression came in through a correction added *alongside* the fix it was correcting. The fix — re-key
the body-model rule on the engine's predicate instead of the word `dynamic` — was right, and the arm added
next to it to handle a scope with no static type was right for the shape it was written for. What made it
a regression is that a correction is written against the case in front of you and inherits the fix's
credibility, while carrying none of the fix's measurement.

So: a correction bolted onto a fix needs its own reachability question, asked out loud — *which contexts
satisfy this condition?* Here the answer was one, and that one context reached it only because it had
been built with half its information thrown away. The condition was not the bug; it was the place the
bug became visible. When a new arm turns out to have exactly one caller, suspect the caller.

## Twentieth review cycle (2026-07-31)

One regression, introduced by the previous commit and introduced by the *fix* this time, not by a
correction beside it. Three pre-existing divergences, one of them silently wrong output. Three lines
shipped last cycle that no test held. One clause presented as load-bearing that was not.

### One region body, two call sites, two bodies

The engine compiles a definition body **once per parsed body**, not once per call site. Every item it
compiles is memoized for the whole compile in `CompileContext.CompiledItems`, so a second call site into
one definition re-uses the code the first one produced and its own value is simply cast to the model that
first call site typed it against. Two call sites reach two compiles only where the parser isolated the
definition tree between them — a document-scope output chain isolates, and so does a subtemplate outside
a definition body; inside a definition body nothing does, and one body there serves every call however
many models the calls hand it.

The emitter's body cache had a call-site model in its key. Before the previous commit that model was
always absent for a region body, because `TryRegionBodyContext` dropped the host's `DynamicBodyModel`, so
every region body keyed the same and the emitter *accidentally* reproduced the engine's single compile.
Carrying the model — which is what closed the previous cycle's regression, and is right — made two call
sites key differently and built two differently-typed bodies.

Measured, with a component whose element shadows the host's `Tag` with `new`:

* `@r()|@list(Items){{@r()}}` — engine `[host]|[host]`, both calls through the host-typed body it
  compiled first. The commit rendered `[host]|[element]`. Both tiers compiled, both rendered, the bytes
  differed and nothing was reported.
* The same with an element that is not the host's type at all — no shadowing needed — engine
  `InvalidCastException` at render, the commit rendered both.
* Mirror order, `@list(Items){{@r()}}|@r()` — the engine's one body is the element's, and the direct
  call's host is cast to it and fails. The commit rendered `[element]|[host]`. This face diverged at
  `HEAD` *and* with the carry removed, so it was not the commit's.

Reverting the carry restores the accident and reinstates the previous cycle's regression. The fix is to
mirror the memoisation instead: the emitter shares a body by the measure the engine shares one — the
identity of the `ParseContext` the definition was reached through, which is exactly what isolation
creates a second of — and re-types the call site that arrives second to the body that already exists.
Where that body is typed, its `(T)scope.ModelData` **is** the engine's cast and reproduces it exactly,
failure included; the second and third faces close on bytes, not on a degrade. Where the body is on the
dynamic tier there is no cast to reproduce, so a later call site of another model degrades. That closes
the mirror-order face too, by degrade rather than by bytes.

The parse-context identity is what keeps this from over-sharing: two document-scope chains calling one
definition with different models are two isolated copies, two contexts, two bodies — the engine's answer,
and two existing tests already pin it.

**Degrade cost: 2 rows of 997.** Both are a region reached first from a `@list` body and then from a
differently-typed call site, with a body that reads nothing from its model at all — the case where reuse
would in fact have been harmless. The rule cannot tell, because it decides before the body exists, and
`NeedsModelLocal` is not a sound proxy: a nested branch body reads the enclosing model without the
enclosing body's flag ever being set. Four rows that were *divergences* stopped precompiling in the same
family.

### An `object` prop with an enum default

`[Prop("e", typeof(object), Default = DayOfWeek.Tuesday)]`, called `@ext(this)`: the engine renders
`e=Tuesday/DayOfWeek` and the generated tier rendered `e=2/Int32`. Roslyn's `TypedConstant` for an enum
constant carries the **underlying primitive**, and the `object` arm of the prop-value writer passed the
literal straight through on the reasoning that it "matches `ConvertValue`'s pass-through" — which it does,
except that what the runtime passes through is a boxed `DayOfWeek` read by reflection and what the
emitter wrote was a boxed `int`. Same digits for some values, different type for all of them, and every
read that formats or types the value diverges.

The value alone cannot say which of the two it is, so the declaration's own type now travels with it. An
enum default reaches a layout only by identity, by lift, or boxed into `object` — no conversion the
runtime performs turns it into anything else — so the box it stores is always the enum, and the emitter
writes the enum's name. Which means the name goes into generated source: an enum this assembly cannot
spell degrades here rather than emitting a file the consumer's build rejects (CS0122, demonstrated).

### Prop defaults with no literal form

Wider than the row above and the same root. A `[Prop]` default with no case in the literal writer degraded
the **whole template, silently** — no diagnostic, no manifest entry. Eight measured shapes the engine
renders: an enum-typed prop with an enum default (zero and non-zero), a `byte`-backed enum, a
`Nullable<enum>`, and plain `byte`, `sbyte`, `short` and `ushort` props with same-typed defaults.

Two mechanisms. The literal writer has no case for the four integral types narrower than `int`, because
C# gives them no literal suffix and that writer's own contract is that what it writes round-trips through
the expression parser, which has no such form to parse — so those are written as a cast here instead of
there. And for an enum target the underlying `SpecialType` is `None`, so neither the identity nor the
widening arm could fire; the enum rule above answers those.

The contrast that keeps this about the enum *type* rather than the default machinery:
`[Prop("e", typeof(DayOfWeek), Default = 2)]` is `HED5009` on the engine and `HED7017` on the generator,
and stays refused by both.

### Where the reduction stopped short

The array-element reduction the previous cycle added was right in the dangerous direction — two
independent sweeps, 99,856 and 3,364 pairs, found zero over-acceptances — and short in two places, both of
which cost templates the engine renders:

* It reduced the element of a type that **is** an array, and never a constructed array-**interface**
  target's type argument. So `uint[] → IList<int>` worked, because the source reduces, and
  `int[] → IList<uint>` did not, because the interface's argument is what has to. The argument now
  reduces too — against an array source and nowhere else, because `IList<int> → IList<uint>` is not a
  conversion the CLR makes, and the re-ask still has to find the interface on the array before anything is
  admitted.
* It omitted the pointer-width pair. `nint`/`nuint` are not in the C# numeric-conversion table the
  reduction otherwise reads from, so `IntPtr[] ↔ UIntPtr[]` was refused.

Six declared-model rows over these two families precompile and render the engine's bytes now, plus four
more that the reduction already handled and no test held. The guard rows are unchanged and still false:
`int[] → object[]`, `int[] → ValueType[]`, `DayOfWeek[] → Enum[]`, `char[] → ushort[]`,
`bool[] → byte[]`, `uint?[] → int?[]`, and every rank mismatch.

### Three lines shipped unpinned

Each of these could be deleted with all five suites staying green, and each changes a template's tier:

* The 16-bit case of the element reduction. The corpus had `Byte`/`SByte`, `UInt32`/`Int32` and
  `Int64`/`UInt64` array rows and no 16-bit row at all.
* The rank argument in the reduction's array rebuild. Rebuilding at rank 1 leaves every suite green and
  makes the adapter **over-accept** more than twenty pairs against live reflection (`int[] → uint[,]`,
  `DayOfWeek[,] → int[]`, `int[][] → uint[][,]`). The corpus had no multi-dimensional row at all.
* The nullable unwrap in the declared-model check. Nothing in the previous cycle's new rows passed a
  `Nullable<T>` value at all.

All three are now corpus rows or declared-model rows, and each reddens under its own deletion. The
corpus's reflection-side driver re-derives every expectation from the live CLR relation on each run, so
the added values are generated data, not belief.

### A clause that only looked load-bearing

The declared-model check opened with `cp.NativeExpression != null || (cp.ChainParameter != null &&
cp.ChainParameter.Count != 0)`, which is strictly subsumed by the guard beneath it —
`IsModelTypeParameter` is *defined* as those three fields being null. Deleting it left all 817
integration tests green and every grid row unchanged. The previous cycle's entry describes a mutation
that reddens the three must-precompile rows, and that description is true — of the intended mutation,
which removes the member-path test entirely. It is not true of the clause the commit message pointed at.
The clause is gone, and what remains is one test with a comment saying plainly that its two halves agree
on every input the grammar produces, so neither carries it alone. The `RootReference` disjunct beside it
went the same way: the value-type lookup already answers `null` for a root reference, so the guard was
inert and read as though it were not.

### The sweep

997 rows, captured at `HEAD` and in the working tree and diffed on classification and rendered bytes:
a declared-model grid (39 declared spellings × 24 call-site arguments over a model carrying every array
element type the CLR reduces, a jagged array, a lifted scalar and a shadowing pair), every
parameter-declaring extension fixture called bare and with an argument, and a region grid (3 bodies × 9
call-site orders). Plus the 63-template corpus through its classification and render-parity gates, and
the ten samples' goldens.

* **Newly degrading: 6.** Four had been **divergences** — the generated tier rendering where the engine
  throws `InvalidCastException`. **Two had been matching**: the degrade cost above.
* **Bytes moved while still precompiling: 3.** All three moved *to* the engine's bytes — the `object`
  prop with an enum default, and the two region orders where the host-typed body now serves both calls.
* **Newly precompiling: 15.** All fifteen render the engine's bytes exactly: ten declared-model rows over
  the two reduction families, and five prop-default shapes.
* Corpus classification and render parity unchanged; sample goldens unchanged.

### The durable lesson

A fix can be correct in isolation and wrong in combination, because the thing it corrects was
compensating for something else. Dropping the region body's host model was a bug — it is what sent a
prop-typed body down the model-less arm — and it was also, by accident, the only thing reproducing the
engine's single compile. Restoring the model was right and split a body the engine keeps whole. Neither
the fix nor the revert is the answer; the answer was to find the invariant the accident had been
maintaining and maintain it deliberately.

So: when a fix removes a *lossy* step — a field dropped, a type widened, a name flattened — ask what
else was standing on that loss. Anything downstream that keys on the lossy value has been getting a
coarser key than it asked for, and the fix is a sharpening it never consented to. The place to look is
every cache, every dedup key and every equality comparison the corrected value flows into; here it was one
dictionary key three hundred lines away, and the engine had a name for the invariant it broke.
## Twenty-first review cycle (2026-07-31)

Nine findings. One regression introduced by the previous commit — a build-time refusal turned into a
shipped template that throws at render. Four pre-existing divergences, **three of them silently wrong
output**, and two of those in code the register had already predicted was uninspected. Two terms of the
previous commit's new rule that nothing held. One unreachable arm, decided.

The reviewers worked to a new protocol: **enumerate the whole class before reporting the instance.**
Recent cycles produced two findings each. This one produced nine — including the severity-1
`System.Object` half that nobody would have found from the `dynamic` repro, because that half only
appears once you ask what the declaration *resolves to* rather than which words it is spelled with.

### The order two texts are built in

A definition call has two texts: the definition's body, and the content the caller wrote inside the
call. The engine compiles the caller content first and the body second. The emitter did the reverse.

That was inert for as long as it was, and stopped being inert the moment the previous commit made
**first arrival** decide the typing of a body two call sites share. After that the order settles which
of the two call sites types the shared body, and the emitter was picking the wrong one:

```
@model(){{…RegionArticle}}@%
<frame>{{[@(Title)]}} :: dynamic
<outer>{{@frame(this){{@frame(5)}}}} :: …RegionArticle
%@
@outer()
```

The engine refuses at compile — `HED0001: Property Title not found in Type [Int32]`. The commit under
review **precompiled** it, and threw `InvalidCastException: 'System.Int32' to '…RegionArticle'` at
render; the parent commit degraded it. Seven order-sensitive constructs were built and measured, and
exactly one site is inverted: the caller content of the *same* call. The mirror, an unrelated call,
`@if`/`@else`, `@list`-then-direct and chains in both directions all matched already. All three
spellings that resolve to `System.Object` reproduce it.

### A spelling where the engine has a resolved answer

The largest finding of the cycle, and the one the protocol earned.

A region's body is typed either by a model it declares or by the value its call site passes. The engine
decides which by resolving the declaration and asking whether the answer is `System.Object` — so
`dynamic`, `object`, `System.Object` and declaring nothing at all are one case, and every one of them
is typed from the call site. The emitter decided by looking at the *text*: empty or the word `object`
went one way, everything else the other. Two of the four spellings therefore came out with no model at
all, and **no region path called the call-site typing at all**.

Both faces, on a host whose element shadows one member, with a single call site and no sharing:

* `@%<row>{{[@(Tag)] [@(Missing)]}} :: dynamic%@@list(Items){{@row(this)}}` — the engine refuses at
  compile (`HED0001`, naming the element type); the generated tier precompiled, the consumer's build
  was green, and it threw `RuntimeBinderException` at render.
* `@%<row>{{[@(Tag)]}} :: dynamic%@@row(this)|@list(Items){{@row(this)}}` — the engine prints
  `[host]|[host]`, the generated tier `[host]|[element]`. Both render. Nothing is reported by either
  tier. That is the silent-wrong-output face.

Measured over 24 cells per spelling: nothing and `:: object` correct; `:: dynamic` eleven
generator-renders-where-engine-refuses plus two different-bytes; **`:: System.Object` fifteen and
three**. Widening the string test to include the word `dynamic` was measured and is *not* the fix — it
closes thirteen of thirty-one cells and leaves every `System.Object` cell open, which is precisely the
mistake this defect class is made of. The fix re-keys on the resolved answer and routes that arm
through the same call-site typing a non-region definition takes.

**Reach is wide, not exotic:** a definition is a region exactly when it is nested inside a component
body, so every such definition is one.

### A body identity with a term the engine does not have

The engine memoizes each item of a definition body by the parsed item it came from, and saves and
restores the region fill scope around the body compile **without putting it in that memo**. So two call
sites inside one component body that fill the same region differently still run one body — the first
site's, fills included.

```
<panel>{{@%<:head>{{[d]}}%@@head()}} :: …RegionFeed
<outer>{{@panel(){{@%<head:head>{{[A]}}%@}}|@panel(){{@%<head:head>{{[B]}}%@}}}} :: …RegionFeed
```

Engine `[A]|[A]`; generated `[A]|[B]`. Both render, nothing reported. Eleven of seventeen enclosing-body
shapes diverge — every shape that is not a parser isolation boundary. Document scope, `@if` at document
scope and a document with no definitions block match, because the parser hands each call site its own
copy of the definition tree there.

A one-line fix does not work: dropping the fill scope from the sharing key alone leaves the equality
check succeeding without transplanting the first context, so the emitted-body cache key still splits.
It had to come out of **both** keys — and once it had, the two keys were the same string, which is what
they should always have been. They are now computed once, and the identity they carry is the engine's:
the definition, and the parse context it was reached through.

### An answer the engine has and this side cannot name

A collection reaching `IEnumerable<T>` at two different `T` is a shape the emitter cannot resolve,
because the host picks one by reflection order. The emitter said "cannot say" — and "cannot say" is the
answer every gate downstream exempts. `@list(Multi){{@list(this){{y}}}}` precompiled and rendered empty
where the engine refuses (`HED0004`, `System.Int32` against `IEnumerable`); `@list(Multi){{[@(Nope)]}}`
precompiled and threw where the engine refuses (`HED0001`).

The engine *does* pick one, so it has a definite element type and compiles the whole body against it.
There are three answers here, not two: a type, "I cannot say", and **"the engine has one and I cannot
name it"** — and only the second is exempt. Not being able to reproduce the order the runtime chose in
is a reason to leave the body to the dynamic tier, not to emit one against no type at all.

### A slot the region declared and never entered

The engine swaps the slot parameter type around every definition body it compiles and does **not**
exempt a region — in contrast to the prop layout on the line immediately above it, which it does. The
emitter never set it for a region, so both `@out` gates took the wrong branch: a valueless `@out()`
inside such a region rendered `[]` where the engine refuses with `HED5013`, and the same region with
`@out(this)` degraded where the engine renders. One line, two faces, opposite directions.

### Two terms that decided nothing

Of the four terms in the previous commit's new sharing rule, two were load-bearing (collapsing the
parse-context identity reddens 2; deleting the degrade arm reddens 1) and two reddened nothing at all
out of 847. Both were removed rather than pinned. The fill-scope term is the defect above. The
`DynamicBodyModel` equality term is **redundant by construction, not merely untested**: which arm gives
a body its model is a property of the definition rather than of the call site, and both arms leave the
model symbol and the dynamic body model in step, so agreement on the first two already implies
agreement on the third. Re-adding it changes nothing over 879 tests, which is what that argument
predicts.

The rule this leaves: **when a term cannot be reddened, say which of the two it is — untested, or
unable to decide anything — and delete it if it is the second.** A term that cannot decide is a claim,
and the reader will believe it.

### The register's own predicted residual was live

Cycle 16's `BodyContext` tables are the series' one enumeration success, and the register recorded, in
plain words, that they answered only one of the seven columns and that a future cycle should write the
rest. Two of the uninspected columns each held a defect: the slot column at both region sites, and the
model columns at the typed-region site for the two spellings that resolve to `System.Object`. The grid
is now written out in the register, along with the question that produced the fill-scope finding —
*what is the emitter's body identity, term by term, and does the engine key on the same terms?*

### The unreachable arm

`IntegralText`'s `char` case cannot be reached: its two callers are the enum branch — C# admits no enum
over `char` — and the narrow-integral writer, whose switch has no `char` arm. **Deleted**, and the
method's contract written down. That is the opposite decision from the export guard's container arm,
and for the reason given there: that arm is symmetric with a live one and reads as a pair, so labelling
it costs nothing. This one is symmetric with nothing — it is a case in a list of cases, and a reader
counting the list would conclude the callers accept `char`.

### The sweep

214 rows, captured with the committed emitter and with the emitter at `HEAD` and diffed on
classification and on rendered bytes: the region-spelling grid (5 declared spellings × 18 body-and-call
shapes × both region declaration syntaxes), the emission-order set, the enclosing-body set for fills,
the ambiguous-element set and the region-slot set. Plus the 63-template corpus through its
classification and render-parity gates, and the ten samples' goldens.

* **Newly degrading: 27.** Twenty-four had been **divergences** — the generated tier rendering or
  throwing where the engine refuses at compile. **Three had been matching**: two are a `:: dynamic`
  region reached bare and then from a `@list` body, which the sharing rule's existing degrade arm now
  sees for the first time, and one is an `@list` body over an ambiguous element that both tiers
  happened to render.
* **Bytes moved while still precompiling: 21.** All twenty-one moved *to* the engine's behaviour —
  fifteen from differing bytes to identical bytes, six from one tier throwing to both throwing the same
  exception.
* **Newly precompiling: 25.** Seventeen render the engine's bytes exactly; eight reproduce the engine's
  `InvalidCastException`, message included.
* Corpus classification and render parity unchanged; all ten sample goldens unchanged.

### The durable lesson

Two, and they are the same one seen from either end.

**A class is not enumerated until it is enumerated over the thing the engine keys on.** The reviewers
did not report `:: dynamic` and stop; they resolved every spelling a region can carry and measured the
grid, and that is the only reason the `System.Object` half exists in this record at all. It is a
severity-1 silent wrong output, it is not exotic, and a repro-driven cycle would have closed thirteen
cells of thirty-one and written "fixed" beside it.

**And a residual written down is a finding waiting to be collected.** The register said, three cycles
ago, that only one of seven `BodyContext` columns had ever been checked, and named the other six. Two
of them held defects. The cheapest work in this cycle was reading the register's own list of what it
had not looked at.

## Twenty-second review cycle (2026-07-31)

Two reviewers, both working from the register's defect-class section rather than from probes. Both
picked a class the register said was **not enumerated**, enumerated it, and each enumeration
immediately produced a severity-2 defect — generated code that stops the consumer's build — that no
prior cycle's probing had reached. That is the cycle's result, and it is worth more than the three
fixes.

### Class C, enumerated by position: the extension type nobody asked about

Defect class C is *can generated code in the consumer's assembly name this type?* The register said it
was enumerated over `TypeKind` and **not** over positions, and told the next cycle to write the
position list. Written, it closes on sinks rather than call sites: everything that reaches a `.g.cs`
goes through `CodeWriter.Line`/`Raw`, the `_fieldDecls` buffer, the `_methodDecls` buffer or the
manifest builder, and that grep reaches two files.

The position that had never been asked is the **bound host extension's own type**. `ExtensionBinder`
contains no accessibility or `[Obsolete]` check at all, and the emitter spells the type twice per call
site — the field's declared type and the `new` that fills it. The engine's discovery filters a type on
the extension interface and the name attribute alone (`TemplateFactory.cs:245-262`) and
`Activator.CreateInstance` instantiates a non-public type with a public constructor and ignores
`[Obsolete]` outright. So against a referenced assembly carrying the parameterless
`[assembly: ExportExtensions]` — which needs no cooperation from the extension's author:

| template | engine | generated |
| --- | --- | --- |
| `@secret(Name)` — `internal sealed class SecretExtension` | renders `<ab>` | `CS0122` ×2 |
| `@boxed(Name, width: 7)` — `internal` + `[Prop]` | renders `{7:ab}` | `CS0122` ×1 |
| `@legacy(Name)` — `[Obsolete("gone", true)]` | renders `[ab]` | `CS0619` ×2 |

No diagnostic and no degrade: the manifest said precompiled and the build stopped on a file the
consumer cannot edit. The question is now asked once, at the choke point in `BuildCall`, before any of
the three writers allocates a field — which also covers the fourth write, reachable only through a host
assembly whose simple name is literally `Heddle`, without needing a measurement nobody can construct.
`ClassifyTypeName` and not `ClassifyModelType`: no extension is boxed into a model and none could be a
ref struct. Four neighbours must keep pre-compiling, and one of them is deliberate: an extension whose
declared `[Prop]` **type** is unnameable, a gate a prior cycle measured and removed.

### And class C is not a class of types

The second member of the same enumeration is `@using`. Its body was copied into the generated file
verbatim as a C# `using` directive, with nothing between the parse and the emission:

| template | engine | generated |
| --- | --- | --- |
| `@using(){{Zork.Nope}}@\` | renders | `CS0246` |
| `@using(){{1 + 2}}@\` | renders | `CS1001`+`CS1002`+`CS8805`+`CS0201` — **the file no longer parses** |
| `@using(){{System.Linq}}@\` | renders | precompiles, clean |

The second row is worse than one bad name: an unparseable compilation unit takes every other template
in the same compilation with it. To the engine a `@using` body is advice about resolving a model type
name — `UsingExtension.InitStart` only calls `CSharpContext.ImportNamespace` — so a body naming nothing
is simply never consulted. The directive is now omitted when the text names no namespace this
compilation can see. The **collected list is untouched**, because that is what a model type name is
resolved through; filtering the list instead was measured equivalent and reddens nothing, and the
emission site was chosen as the narrower change rather than because they differ.

**A position list built by asking "which types does the emitter spell" would have missed this
entirely.** It is a name that is never resolved to a symbol at all.

### Class A, enumerated: the import identity

Defect class A is *the emitter keys on a spelling where the engine keys on a resolved answer* — nine
members, none enumerated. Enumerated, the row that diverged is the `@<<` import identity. The generator
installs `TemplateKey.TryNormalize` as both the cycle-guard identity and the import-map lookup; the
engine resolves through `Path.GetFullPath(Path.Combine(RootPath, path))`. Thirteen spellings of one
file:

| spelling | engine | generator |
| --- | --- | --- |
| `lib.heddle`, `./lib.heddle`, `.//lib.heddle` | renders | precompiled, matches |
| `x\lib.heddle`, `LIB.heddle`, `./../outside/lib.heddle` | refuses | `HED7011` + degrade (both refuse) |
| `x/../lib.heddle`, `sub/../lib.heddle`, `sub/./../lib.heddle` | **renders** | **`error HED7011` — breaks the build** |
| `/lib.heddle`, `~/lib.heddle`, `lib` | **refuses** | **precompiled, renders** |

`TryNormalize` rejects `..` outright and falls back to the raw spelling, so the map misses. **The `..`
direction is fixed**: a `..` is applied before the key is derived, the way `GetFullPath` applies it,
with `.` and repeated separators dropped on the way, and a `..` with nothing left to cancel against
**kept**, so a spelling reaching above the root still refuses as the engine does. `TryNormalize` itself
is untouched — a template key genuinely may not contain a `..`, and widening it would reach the
resolver and the registry. `HED7011`'s message was wrong for every row here ("Add it as a
`<HeddleTemplate>` item" — the file *is* an item) and now says what the spelling is matched against.

**The other direction is recorded, not closed.** Closing it means refusing `lib`, `~/lib.heddle` and
`/lib.heddle`, and the first two are spellings the documentation teaches. Taking working precompiled
templates off the tier to match a refusal is the wrong trade at severity 3, and the register carries it
with its measurement.

**The LSP needed no treatment, and that is a source fact rather than a hope.** `grep -rn
"ImportIdentifier"` gives five hits; the LanguageServices facade sets neither `ImportReader` nor
`ImportIdentifier` and parses through `DocumentParser.Runtime`, so it already had the engine's rule
exactly.

### The trap the enumeration disarmed

Enumerating class A also walked into something that looks exactly like a defect and is not. The
emitter compares `def.ModelType == "dynamic"` — a raw spelling test, in the same shape as the finding
the previous cycle fixed one path over. It is **correct**, because `HeddleCompiler.cs:585` compares the
same text: the engine keys on the spelling in that position too, and resolving it would be the
divergence. This is now written down in the register with the citation, so the next cycle spends
nothing on it.

### A term that decides nothing, resolved the way the register says to

The previous cycle wrote the rule — *when a term cannot be reddened, say which of the two it is:
untested, or unable to decide anything* — and then, in its very next key, wrote two terms of the second
kind. The folded body key was `Name@Position@ParseContextId`; dropping the name reddened 0 of 1431,
dropping the span reddened 0 of 1431, and the context id alone as the whole key reddened 0 of 1431.

They are redundant **by construction**, not merely untested, and the argument is enumerable: a
`DefinitionItem` gets a fresh `ParseContext` in its constructor, and every path that hands two live
items one context — the copy constructor, `OverrideWith`, and the region-fill materializer, which takes
its name and its span from the same candidate — carries the name and the span across with it. The one
path that does not, `ParseContext.IsolateContext`, gives the copy a *new* context, which only splits
further. Both terms are deleted and the argument is in the method's contract. The surviving term
reddens thirty-odd tests.

### The ambiguity refusal's price, measured rather than assumed

The register recorded the `@list`-over-an-ambiguous-element refusal as costing "one measured cell". It
costs two, because the refusal is taken **before the body is inspected** and so declines bodies that
could not have needed the element type — including one that reads nothing at all, which the engine
renders. A third shape the adversary reported, a slot projection, refuses on both tiers in the spelling
constructed for it and is not a cost cell.

It is **recorded rather than narrowed**, and the reason is in the finding it came from: narrowing means
building the body first against no type and asking afterwards whether it consulted one, which is
precisely the state that refusal exists to prevent. The cost now has rows of its own, so a cycle that
narrows the rule reddens them and has to say what it did.

### The sweep

Three grids captured against the emitter at `HEAD` and against the committed one, plus the corpus:
the 13-spelling import grid, the 7-row extension-nameability grid, the 5-row `@using` grid, and the
63-template corpus through its classification, its `.g.cs` byte hashes and its full diagnostic list.

* **Newly degrading: 3.** All three are the unnameable-extension rows. **None had been matching** —
  each previously *broke the consumer's build* with `CS0122`/`CS0619` against a generated file, so the
  degrade is not a loss of a working precompiled template but the replacement of a build failure by a
  warning and a render.
* **Bytes moved while still precompiling: 0.** The corpus dump — classification, per-file source hash
  and diagnostics — is byte-identical to `HEAD`'s across all 63 templates, and that is the sweep that
  would see the body-key change if it moved anything.
* **Newly precompiling: 5.** Two `@using` rows and three import spellings. All five previously produced
  a **build error**, and all five now render the engine's bytes exactly.

Corpus classification and render parity unchanged; all ten sample goldens unchanged.

### The durable lesson

**Two defect classes were enumerated to closure this cycle, and each one immediately yielded a
severity-2 defect that no prior cycle's probing had reached** — the extension type and the `@using`
text for class C, the import identity for class A. Neither is exotic. An `internal` extension is what a
host writes when it does not intend the type to be public API; `x/../lib.heddle` is what a path
concatenation produces. Both had been sitting in front of twenty-one cycles of probes.

And the enumeration pays a second way: it **disarms traps**. The `"dynamic"` string test looks exactly
like the defect the previous cycle fixed, and is correct, because the engine compares the same text.
Only a cycle that walked the whole class could tell them apart, and now the register says which is
which so the next one does not have to.
