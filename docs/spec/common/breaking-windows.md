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

## Explicit not-window-gated rulings

Recorded so a *narrowing* change that touched runtime behaviour is not mistaken later for an
unrecorded breaking change.

- **Short-name type resolution: the order-dependent silent pick became the ambiguity error**
  (phase 3, Q3.5 / OQ5's escape clause; `ReflectionHelper.ResolveSimpleType`, 2026-07-26).
  A bare type name matched by several types used to be resolved with
  `types.FirstOrDefault(t => imports.Contains(t.Namespace))` — a first-match pick over an
  assembly-scan-ordered list. Where **two or more** imported namespaces both declared the name,
  which type won depended on the order `Assembly.GetTypes()` happened to yield them. That arm now
  raises the same `"the type name is ambigous"` error the dotted/full-name arms have always
  raised. One import matching still resolves; no import matching still fails as before.
  **Judgement: defect repair, not window-gated.** (a) The behaviour it replaces is
  non-deterministic across runs and deployments, so no user could *correctly* depend on it —
  the [policy](#policy-applies-to-every-window)'s test for a breaking change. (b) The file's own
  `RegisterType` comment already documented the intended contract as "surfaces as the existing
  *ambiguous* error rather than a silent pick"; the fix makes the code match its stated contract
  on the arm where it did not. (c) It **narrows** — it converts a silent wrong-type bind into a
  positioned error — and the register's widening test does not apply. (d) OQ5 required the two
  tiers to stay matched, and the build tier cannot reproduce an assembly-load-order pick by
  construction; reproducing it would bake load order into build output. The generator raises
  `HED7023` for the same input, and the two-driver corpus
  (`TypeSpellingLockstepTests` / `TypeSpellingSymbolLockstepTests`) pins the agreement.

## Next-window candidate register

Nothing here is ratified. Rows are appended as owning specs record them and removed only
when a ratified window adopts them (recording the adoption) or an owning spec withdraws
them (recording the withdrawal in the ledger).

| Candidate | Precondition / trigger | Owning record |
| --- | --- | --- |
| Default encoder swap (`WebUtility` → `HtmlEncoder.Create(UnicodeRanges.All)` or a successor choice) | `TemplateOptions.Encoder` shipped in 2.0.0; soak it, so hosts that need byte-exact output have a pin available before the default moves | The [2.0 record](../records.md#the-20-breaking-window--as-shipped-record) (item 2) |
| `AllowCSharp` property removal | `[Obsolete]` shipped in 2.0.0; soak it, and confirm `ExpressionMode` usage dominant in first-party surface and docs | The [2.0 record](../records.md#the-20-breaking-window--as-shipped-record) (item 4) |
| `[NotEncode]` type deletion | Revisit only inside a ratified window | The [2.0 record](../records.md#the-20-breaking-window--as-shipped-record) (exclusions) |
| **C# betterness in the runtime overload binder** — replace the flat Pareto rank with C#'s better-conversion-target rule (plus validations preserving the documented deviation set), landed **jointly** on both tiers so the generator matches by construction and the cast-pin emit guard mostly retires | Quantified and ready (phase 4 WI10): over the shipped built-in table, **0 of 480** argument combinations change their winning overload, **82** become bindable that are ambiguity errors today, and **62** stay ambiguous (`double`/`decimal` are mutually non-convertible, so neither target is closer) — so it is a pure widening with no rendered-byte change for any template that compiles today, but it *is* a widening: templates that error today would start rendering, and that acceptance cannot be withdrawn later. Trigger: adoption inside a ratified window, together with the extra validations for deviations 4–7 (whose enforcement the flat rank currently gets for free from ambiguity) and a decision on the residual `double`/`decimal` ambiguity's diagnostic | [phase 4 — expression-writers](../../generator_plan/phase-4-expression-writers.md) (D10 amendment / WI10), from the Q4.2(b) ruling; measured by `OverloadBetternessEvaluationTests` |
| **Member-visibility widening** — accept a `protected internal` getter (the plain-English reading of the sandbox's "public-or-internal" contract), surface inherited non-public and base-interface members | Phase 4 WI6 pinned today's narrow runtime behavior as normative (OQ1/Q3.1) and encoded it in one function (`MemberVisibility.IsAccessible`), so any of these becomes a one-line policy change plus a conformance-corpus row flip. Trigger: a ratified window, landed jointly on both tiers with a spec-wording update to `native-expressions.md`'s sandbox section | [phase 4 — expression-writers](../../generator_plan/phase-4-expression-writers.md) (D7 / OQ1) |
| **Dynamic-vs-typed visibility asymmetry** — the typed member tier accepts an `internal` getter regardless of assembly while the dynamic tier (binding in `Heddle`'s context) cannot see a foreign `internal` member | Phase 4 WI9 made the dynamic-tier binder context exist exactly once (`PrecompiledRuntime.DynamicMember`), so harmonizing the two tiers is now a single decision rather than two. Trigger: a spec clarification deciding *which* tier is right, then a window | [phase 4 — expression-writers](../../generator_plan/phase-4-expression-writers.md) (D11 / OQ3) |
| **Extension-name collision between unrelated types** — the runtime's `TemplateFactory.AddExtensions` throws `TemplateOverrideException`; the build tier degrades the call to dynamic with a recorded reason instead of erroring | Phase 3 landed the full runtime precedence in one shared rule-core (`ExtensionRegistrationRules`), so making the build tier error too is a one-line verdict change. Deliberately *not* done now: it is a host wiring error the build has no business failing on before the host is even assembled, and the runtime already surfaces it at first render | [phase 3 — binding-layer](../../generator_plan/phase-3-binding-layer.md) (Q3.3 / OQ3) |
| Must-surface precompiled mismatches stop degrading silently under the **default** policy (`ExtensionBindingMismatch`, `FunctionBindingMismatch` per-request; `SchemaVersionUnsupported`, `EngineVersionIncompatible` at registration), with an explicit opt-out policy value preserving today's blanket degrade | The fallback taxonomy ships first (done — [precompilation.md](../../precompilation.md#which-fallbacks-are-legitimate)); the two binding classes are provisional until phase 3 finishes its binding work, since a residual mismatch is only conclusive evidence of skew once the generator binds through the full runtime replacement precedence | [phase 5 — pipeline-config](../../generator_plan/phase-5-pipeline-config.md) (D12b), from the Q2.2 fallback-legitimacy ruling |
