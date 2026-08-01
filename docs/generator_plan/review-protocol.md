# Reviewer protocol — exhaustive catalogue, not first-found

This protocol is MANDATORY. A report that finds two real defects but does not
follow it is a FAILED review, because the defects it did not enumerate become
next cycle's work, and a cycle costs more than the extra breadth does.

## Why this exists

Four review cycles in a row found the FIRST instance of a defect class and
stopped. The fix landed, and the next cycle found the same class one path
over — prop-first resolution missing from two readers, then a third, then a
fourth; a body-typing rule keyed on `IsDynamic` when the engine's predicate
was `acceptType == typeof(object)`, so `:: object` carried the whole class
its twin had just had fixed. Each of those was discoverable in the cycle
that found its sibling. None was found, because nobody enumerated the class.

**The unit of a finding is a CLASS, not an instance.**

## Phase 0 — Read the findings register FIRST

`docs/generator_plan/findings-register.md` is short and is kept short. It
holds four things: the recurring mistake classes, the code that looks wrong
but is correct, the open items, and the fixes whose tests would not catch a
regression. Fixed one-off defects are NOT in it — the test suite is their
record, and full history is in git (the register says where).

Before you probe anything:

1. Read it in full. It is a few pages; there is no excuse to skip any part.
2. **Regression duty.** The suites are the regression record: run the ones
   your area touches, serially, and include a Release leg for the generator
   suites. The register's *weak pins* section lists the fixes the suites
   cannot defend — if your area touches one, re-check that fix by hand.
   (That section trends to empty; anything added to it comes with the test
   that empties it, or an open row saying why none can exist.)
3. **Do not re-report** anything in the *open* or *do not "fix"* sections —
   but **"do not re-report" does NOT mean "do not re-measure".** Run the
   checks for your area. If a check passes or a stated reason tests false,
   the entry is wrong — report that; overturning an entry in either
   direction is a legitimate and valuable finding. Six former known-opens
   died exactly that way.
4. Use the mistake classes to aim: a new finding is usually another member
   of a listed class. Check membership before writing it up as new.

**The register is documentation and may cite code, tests and commits. Code
and tests must NEVER cite the register** — no finding ids in comments or test
names. That rule is absolute here.

## Phase 1 — Surface enumeration (do this BEFORE probing anything)

Derive complete inventories FROM SOURCE, not from the commit message and not
from what you happen to probe. For the area under review, produce every
inventory that applies:

- every construction site of the relevant context/state object
- every reader of the relevant field/rule
- every emission point that writes the relevant construct
- every gate/guard that can refuse
- every call site of the function you are reviewing

Each inventory is a TABLE with, per row: `file:line`, what it does, the
verdict (correct / wrong / n-a), and how you established the verdict. State
the method you used to be exhaustive — the grep or symbol search that
guarantees no row is missing, with the exact pattern — and say explicitly
whether the set is closed (e.g. "the type is private to this file, so the
population is closed").

An inventory you cannot close is itself a finding. Say so.

## Phase 2 — Systematic sweep

Build a matrix over the enumerated surface, not a handful of probes. State
its dimensions and its cell count. Run it against BOTH tiers. Report the
tally by outcome class:

  match / generator-renders-where-engine-refuses / generator-throws-where-
  engine-refuses / different-bytes / degrade-where-engine-renders /
  degrade-where-engine-refuses

Where the commit under review claims a count, re-derive it independently over
YOUR OWN population and say whether it generalises. Do not reuse the author's
sweep set.

## Phase 3 — Class expansion (the rule that would have saved four cycles)

For EVERY defect you find, before reporting it:

1. Name the class. Not "`@list` misses X" but "every consumer that asks Y".
2. Enumerate every member of that class from source, with citations.
3. Test EVERY member. Report all of them — the ones that diverge and the
   ones that do not.
4. State the predicate the code keys on and the predicate the ENGINE keys
   on, side by side. A mismatch between them IS the finding; the instance
   you started from is one symptom of it.

A finding reported without its class expansion is incomplete. If you cannot
expand it, say why in one sentence.

## Phase 4 — Coverage ledger

Report EVERY check you performed, including the ones that passed. A passing
check is evidence and prevents the next reviewer repeating it. Format:

| # | check | method | citation | result |

Then, explicitly:

- **Not checked**, with the reason for each (out of scope / could not
  construct / unreachable-and-here-is-why / ran out of budget).
- **Could not close**: any inventory or class you could not enumerate
  exhaustively.

"I did not check X" is a valuable line. A silent gap is not.

## Citation requirements

- Every claim about code: `file:line`, and quote the line if it is the crux.
- Every claim about behaviour: the actual template, the actual model, the
  actual output of BOTH tiers, verbatim. Not a paraphrase.
- Every claim about the engine's rule: the engine source location that
  establishes it, plus a measurement confirming you read it correctly.
- Every mutation: what you changed, which tests reddened, and the failure
  message. A mutation that reddens NOTHING is a finding — report it.

## Findings land as tests

Every confirmed finding leaves review as a test, not as a register entry.
Decide by this tree, in order:

1. **Fixed** → a green regression test whose reversion was rehearsed red (the
   mutation bullet above already applies). No register entry — the suite is
   the record.
1b. **Overturned** — you wrote the test and it is green, because the reported
   defect is not real. **Keep the test, open and running.** There is nothing
   to fix, so nothing to skip: it is now the pin against a regression and the
   record that the claim was measured rather than waved away. Its doc comment
   says what was claimed and why it does not hold, which is what stops the
   same report returning next cycle. If you are green but unsure the current
   behaviour is the *intended* one, ASK THE MAINTAINER before asserting it —
   a test pinning the wrong contract makes the wrong behaviour permanent.
2. **Real, unfixed, deterministically reproducible in-process** → write the
   red test NOW and check it in skipped:
   `[Fact(Skip = "known defect — <owner>: <defect>; un-skip with that fix")]`.
   Rehearse it red locally first, record what failed, then skip. The suite's
   skip list IS the pending-work list — gates report the count, and un-skipping
   is the fixer's acceptance evidence. Mechanics are written once each:
   skip-string form and un-skip-as-acceptance in
   `phase-0-test-fallback-guardrails.md` (D6); "quarantined, never weakened —
   an unexplained or orphaned skip is a review failure" in
   `docs/spec/common/testing-standards.md` (precompiled-tier posture).
3. **Not testable in-process** — needs a child process, is an ordering the
   register's class J forbids racing for, or needs a platform absent from
   CI → an open-table row in the findings register with the reason and a
   re-measure check. This is the ONLY shape that may add a register row.
4. **A documented limitation, not a defect** — an upstream library bug, a
   platform or API absence, a stated performance bound, or an edge case no
   realistic use of this project reaches → record it with its reason (a
   degrade-pinning test where one is possible) and do NOT demand a fix.

**Be reasonable in what you require.** A required change must be realistic
and proportionate to its severity. If you are unsure whether a fix is
feasible, or whether anyone would ever hit the defect, ASK THE MAINTAINER
instead of requiring the work — building what may never be used is exactly
the waste YAGNI names. The repo's stance is already written down: see
`docs/spec/common/coding-standards.md` § "SOLID, DRY, and YAGNI in balanced
mode" (precedence: correctness → sandbox security → simplicity → measured
performance → abstraction last; a seam is added when a second consumer
exists, not because one might appear). Precedent for recorded limitations:
`unverified-platform-surface.md`.

**The register may not grow with instances.** A new row is one of: a new
recurring class (two or more independent members), a correction or status
change to an existing line, or an untestable open item per branches 3–4. An
instance of anything else gets a test.

**Weak pins are the fixer's debt.** A fix whose test survives the fix's
reversion is unfinished: land the honest pin, or move the item to the open
table with the reason no pin can exist. The register's weak-pins section
exists only to shrink.

## Anti-stop rule

Do not stop when you find something interesting. Finding a severity-1 defect
in the first twenty minutes means the remaining budget goes to enumerating
its class and continuing the sweep — not to writing it up at length. Depth on
one finding is worth less than the complete catalogue.

Budget guidance: roughly a third of your effort on Phase 1, a third on Phases
2–3, a third on everything else. If you are writing prose about a defect and
have not finished the inventories, you are in the wrong phase.

## What a good report looks like

1. Phase 0 results — suites run (with configuration), weak pins re-checked,
   open-item checks run, each with its outcome
2. Inventories (Phase 1 tables, with the exhaustiveness method stated)
3. Sweep matrices and tallies (Phase 2)
4. Findings, each WITH its class expansion, ranked by severity (Phase 3)
5. Coverage ledger + not-checked + could-not-close (Phase 4)
6. Claim-by-claim verdicts on the commit (verifier) or attack list with
   outcomes (adversary)
7. **Tests you land or propose**, per *Findings land as tests*: the green
   pins for what was fixed, the skipped red tests for what was not, and —
   only for branches 3–4 of the tree — register rows with the measurement.
   Keep proposed rows as short as the register's existing ones

"No new defects" remains a valid and valuable result — but only when it
arrives with the inventories and the ledger that make it credible. An empty
findings list with no coverage ledger is not a clean review; it is an
unaudited one.

## Honesty

Report only what you MEASURED. Label unmeasured suspicions as such and keep
them in their own section. Do not manufacture a finding to fill a table — a
fabricated defect costs more than a clean report, because it sends a fixer
after nothing. If you contradict the other reviewer or the commit message,
say so plainly and give the measurement that decides it.
