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

`docs/generator_plan/findings-register.md` is the durable record of every
finding this review series has produced: what it was, how it was measured,
what fixed it, which test pins it, and whether it is closed, known-open, or
judged not-a-defect.

Before you probe anything:

1. Read it end to end.
2. **Regression duty.** For every finding marked FIXED whose area your review
   touches, re-run its repro and confirm it is still fixed. Report these as a
   table — this is part of your coverage ledger, not optional. A defect that
   returns after being fixed is the most expensive kind, and only you can
   catch it.
3. **Do not re-report** anything marked KNOWN-OPEN or NOT-A-DEFECT — but
   **"do not re-report" does NOT mean "do not re-measure".** These are the
   two most expensive mistakes this instruction has caused, and both have
   now happened:

   - A known-open was recorded in cycle 17, fixed incidentally by unrelated
     work two cycles later, and sat in the register for eight more cycles
     because every reviewer read "do not re-report" and skipped it. It took
     233 measured cells to establish it had been closed all along.
   - Four known-opens were parked on a *stated reason* that measurement
     later showed to be false — a cure that would not have worked, a
     documentation contract that governed a different feature on a
     different code path, and a fingerprint that did not contain the fields
     it was said to contain.

   **A known-open with no regression check is a claim with an expiry date.**
   Every entry now carries one; run the ones in your area, exactly as you
   run a FIXED entry's. If a known-open's check passes, the defect is gone —
   report that. If its stated *reason* is something you can test, test it.
   Overturning an entry, in either direction, is a legitimate and valuable
   finding.
4. Use it to aim: an area where several findings clustered is where the next
   one lives.

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

## Anti-stop rule

Do not stop when you find something interesting. Finding a severity-1 defect
in the first twenty minutes means the remaining budget goes to enumerating
its class and continuing the sweep — not to writing it up at length. Depth on
one finding is worth less than the complete catalogue.

Budget guidance: roughly a third of your effort on Phase 1, a third on Phases
2–3, a third on everything else. If you are writing prose about a defect and
have not finished the inventories, you are in the wrong phase.

## What a good report looks like

1. Register regression table (Phase 0) — every FIXED finding in your area,
   re-run, still-fixed or not
2. Inventories (Phase 1 tables, with the exhaustiveness method stated)
3. Sweep matrices and tallies (Phase 2)
4. Findings, each WITH its class expansion, ranked by severity (Phase 3)
5. Coverage ledger + not-checked + could-not-close (Phase 4)
6. Claim-by-claim verdicts on the commit (verifier) or attack list with
   outcomes (adversary)
7. **Register deltas you propose**: new entries for what you found, and any
   existing entry you believe should change status, each with the measurement

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
