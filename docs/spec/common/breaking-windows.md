# Breaking windows — policy

The executable side of [D2](cross-cutting-decisions.md#d2--breaking-changes-land-only-in-ratified-breaking-windows):
how breaking changes are batched, executed, and recorded. This document is
instruction-only — policy plus the register of candidates for the **next** window.
Completed windows are recorded in the [historical records](../records.md), never here.

## Policy (applies to every window)

1. **One window per major, one migration per window.** All ratified breaking changes for
   a major land together; users touch their options/templates once. Between windows,
   everything ships additively (new options default to current behavior).
2. **Contents are ratified, not accumulated.** Each window item needs a maintainer-ratified
   decision record in its owning spec; the window's consolidated table, execution order,
   and whatever no single spec owns are assembled in that window's own planning document
   when the window opens — never retrofitted into this policy.
3. **Execution order discipline.** Default flips and byte-changing swaps execute together
   in the release tail, followed by exactly **one** golden re-ratification commit reviewed
   against pre-measured churn tables — goldens change once, attributably (the
   [testing standards](testing-standards.md#regression-gates)' fix-forward rule).
4. **The migration note is a deliverable.** One page shipped with the release notes:
   what changed / how to keep old behavior, per item — nothing else. Folding it into the
   release's CHANGELOG entry satisfies this.
5. **As-shipped verification.** After the release, the window is reconciled against the
   shipped source and the result recorded in the [historical records](../records.md);
   items that slipped are dispositioned through the
   [amendments ledger](cross-cutting-decisions.md#cross-spec-amendments-ledger) — a
   window item is never silently assumed shipped.
6. **The next window has a register, not a plan.** Breaking candidates discovered between
   windows accumulate in the register below, each with the trigger/precondition that
   would let a ratified window adopt it. The register becomes a window only by an
   explicit maintainer decision to open one; adding a row requires a decision record in
   an owning spec.

## Next-window candidate register

Nothing here is ratified. Rows are appended as owning specs record them and removed only
when a ratified window adopts them (recording the adoption) or an owning spec withdraws
them (recording the withdrawal in the ledger).

| Candidate | Precondition / trigger | Owning record |
| --- | --- | --- |
| Default encoder swap (`WebUtility` → `HtmlEncoder.Create(UnicodeRanges.All)` or a successor choice) | `TemplateOptions.Encoder` shipped in 2.0.0; soak it, so hosts that need byte-exact output have a pin available before the default moves | The [2.0 record](../records.md#the-20-breaking-window--as-shipped-record) (item 2) |
| `AllowCSharp` property removal | `[Obsolete]` shipped in 2.0.0; soak it, and confirm `ExpressionMode` usage dominant in first-party surface and docs | The [2.0 record](../records.md#the-20-breaking-window--as-shipped-record) (item 4) |
| `[NotEncode]` type deletion | Revisit only inside a ratified window | The [2.0 record](../records.md#the-20-breaking-window--as-shipped-record) (exclusions) |
