# Phase 4 — engine-robustness

## Header

- **Status:** ready — Plan DoR
- **Goal (one line):** Close three documented engine sharp edges — the concurrent-dispose race, the `TryCompilation` false-confidence gap, and Release-mode model-type validation — without changing the rendered bytes of any valid template or flipping an existing default.
- **Depends on:** nothing (the three fixes here are mutually independent, and none consumes Phase 1's deliverable). It does, however, **coordinate with [Phase 1](phase-1-file-based-api-correctness.md)** on the shared `HeddleTemplate` dispose/lifecycle surface (`_runners` / `_disposeAfterComplete` / `_runtimeDocument`): this phase's disposal-synchronization fix is what Phase 1's superseded-document release builds on, so their diffs to those fields are merged in view of each other (see [standing-rulings — cross-phase sequencing](common/standing-rulings.md#notes-on-cross-phase-sequencing)).
- **Changes an externally-visible contract:** yes, additively only — (a) a strengthened `TryCompilation` success contract (a green dry run comes to predict `Compile` success), and (b) a new opt-in [`TemplateOptions`](../../src/Heddle/Data/TemplateOptions.cs) flag that enables the model-type guard in Release. No existing default flips; no valid template's output changes.

## Goal

Heddle's own documentation names four "documented sharp edges" as a credibility signal ([language-assessment.md §4.6](../language-assessment.md), and again in [§8 item 4](../language-assessment.md)). This phase resolves the three that are pure engine correctness/robustness issues, leaving the file-watcher (Phase 1) aside. The user-visible outcome is that three behaviours the docs currently warn against become safe or predictable: an application may dispose a template instance while other threads are still rendering it; a CI/lint `TryCompilation` pass that returns success actually means a later `Compile` of the same template will succeed; and a production (Release) host can opt in to the same wrong-model-type guard that today only fires in `DEBUG`.

Each fix is grounded in a warning the docs already print to users. [`csharp-api.md`](../csharp-api.md) states the in-flight counter "is not fully synchronized, so avoid disposing an instance that is still being rendered concurrently"; that `TryCompilation` "does **not** run the deferred-finalization or embedded-C# (Roslyn) steps, so a `FullCSharp` template … can pass the dry run and still fail a real `Compile`"; and that model-type validation runs only in `DEBUG` — "Release builds skip this check". This phase turns each of those cautions into a guarantee (disposal, dry-run parity) or an opt-in guarantee (Release validation). The point is to make the "documented defect" list shorter without asking any existing caller to change code or accept different output.

## Non-goals / scope boundary

- **The file-change watcher** — its filter bug and inoperative recompile-on-change are Phase 1, not here.
- **Redesigning the render pipeline** or making rendering async — out of scope; there are no async render methods by design ([csharp-api.md](../csharp-api.md)), and this phase keeps it that way.
- **Changing the default of any existing option** — the Release model-type guard ships default-off (today's behaviour); nothing that alters bytes of a successful render is touched.
- **Render budgets** (`RenderBudget`) — a separate availability concern, already shipped; not revisited here.
- **Blocking / waiting semantics for `Dispose`** are a non-goal for the disposal fix: the target is a race-free guarantee (no torn counter, no dispose-under-active-render), not a barrier that stalls the disposer while renders drain (recorded as Q3).

## Design direction

Three bounded fixes, each following an approach already established elsewhere in the codebase so the change is additive and low-surprise.

**1. Disposal — guarantee safety, not blocking.** Today `_runners` is a `volatile int` mutated with non-atomic `++`/`--` and read by `Dispose()` as a plain `if (_runners == 0)` before disposing `_runtimeDocument` — a read-then-act (TOCTOU) window in which a concurrent `Render` can pass its own dispose guard and increment the counter after the disposer decided the instance was idle ([HeddleTemplate.cs](../../src/Heddle/HeddleTemplate.cs)). The chosen direction keeps the existing *intent* — best-effort deferred disposal via the `_disposeAfterComplete` flag — but makes the in-flight bookkeeping and the "may I dispose now?" decision observe a single consistent state, so the runtime document is never disposed while a render is active and the counter can never tear under concurrent renders. This is a pure correctness fix: correct single-threaded and read-many-render use is byte-for-byte unchanged. The direction deliberately does **not** introduce a blocking wait in `Dispose` (Q3) — the assessment's own framing is "avoid disposing an instance still being rendered", and the minimal correctness guarantee (no use-after-dispose, no torn counter, idempotent disposal) meets that without changing the shape of `Dispose` or adding a stall.

**2. `TryCompilation` — pursue full parity.** The dry-run path runs the parse plus object-graph compile (`HeddleCompiler.Compile`) but, in `simulate` mode, skips the finalization pass `compileScope.Compile()` that a real compile runs — and that pass is exactly where deferred subtemplate finalization (`CompleteInit` over `DelayedTemplates`) and the Roslyn parse/emit for `FullCSharp` embedded C# happen ([ContextCompilation.cs](../../src/Heddle/Runtime/ContextCompilation.cs); [HeddleTemplate.cs](../../src/Heddle/HeddleTemplate.cs)). So a `FullCSharp` template with a Roslyn-level error, or a template whose delayed subtemplate fails finalization, passes the dry run and still fails `Compile`. The chosen direction is to run those same passes during the dry run and collect their errors, then discard the compiled artifact as `TryCompilation` already does — so a green dry run means the same template would compile. This beats the cheaper alternative (leave the dry run partial but make its partial contract explicit and queryable) because prediction *is* the feature's entire purpose: a lint/CI gate that returns success while a real compile would fail is worse than no gate. The added cost (a dry run now pays finalization + Roslyn emit for `FullCSharp`) is the accepted trade-off and is recorded as Q1, including the assembly-cache side effect that a real Roslyn emit carries.

**3. Release model-type validation — an additive opt-in.** The wrong-model-type guard that throws `TemplateProcessingException` lives inside `#if DEBUG` in `Render`, so Release builds skip it silently ([HeddleTemplate.cs](../../src/Heddle/HeddleTemplate.cs)). The chosen direction adds an opt-in [`TemplateOptions`](../../src/Heddle/Data/TemplateOptions.cs) flag, default off, that enables the *identical* check in Release. This mirrors the codebase's own precedent for additive, default-null/off options that never touch a successful render's bytes (`RenderBudget`, `Encoder`) — off by default preserves today's exact behaviour and zero-cost hot path; on, it reproduces the DEBUG guard's exception and message. It is deliberately **not** on-by-default (Q2): flipping it would be a behaviour and hot-path change, which R5 forbids.

## Dependencies & ordering

Nothing must land before this phase. It does not depend on Phase 1 (file-watcher) and shares no code path with it. Internally the three fixes are independent and may land in any order or separately; none unblocks another. This phase unblocks the assessment's goal of shrinking the "documented sharp edges" list ([§8 item 4](../language-assessment.md)) from four to one (the watcher).

## Back-compat / impact

Governed by standing ruling R5 (additive or pure-correctness only; no change to rendered bytes of a valid template; no broken valid public API; no flipped defaults).

- **Disposal:** pure correctness. Single-threaded and concurrent-render-without-dispose callers see identical behaviour. The only behavioural change is at the previously-warned-against case (dispose during active render), which moves from undefined/racy to safe. `Dispose` remains idempotent and non-blocking.
- **`TryCompilation`:** the *success* contract tightens — templates that previously returned a (misleading) success and then failed `Compile` will now surface those errors in the dry run. This reports pre-existing breakage earlier; it cannot make a genuinely-compilable template fail. Cost rises for `FullCSharp` dry runs (Roslyn now runs); the doc caveat that currently warns of the gap is removed.
- **Release model-type check:** default-off means Release behaviour is byte-identical to today. Only a host that explicitly opts in observes the new throw. The new flag must not participate in `TemplateOptions` value-equality / `GetHashCode` (it changes no bytes of a successful render — same rule the docs state for `MaxRecursionCount` and `RenderBudget`), so it cannot perturb the template cache key (Q2).
- **Docs:** [`csharp-api.md`](../csharp-api.md)'s Disposal note, `TryCompilation` caveat, and `Generate` DEBUG-only note are updated to describe the new guarantees/opt-in. (Doc edits are downstream of the code change, not part of this WHAT.)

## Risks & mitigations

| Risk | Mitigation | Size (S/M/L) |
|---|---|---|
| Disposal fix adds synchronization to the render hot path, regressing the best-in-class render numbers. | Keep synchronization confined to the enter/exit bookkeeping and the dispose decision; leave the render core untouched; verify with the existing benchmark parity harness that render throughput/allocation is unchanged. | M |
| Full-parity `TryCompilation` makes dry runs materially slower for `FullCSharp` (Roslyn emit now runs), surprising CI users who used it as a fast lint. | Record the cost trade-off as Q1; scope the extra work to the passes a real compile already runs (no new work); document the new cost in `csharp-api.md`. | M |
| Running Roslyn during a dry run populates the shared assembly cache as a side effect / loads assemblies into the process. | Flag as part of Q1; the cache is keyed by generated code and idempotent, so a later real `Compile` reuses it rather than double-emitting — treat as benign but state it explicitly. | S |
| The new opt-in flag is mistaken for a bytes-affecting option and wired into `TemplateOptions` equality, changing cache keys. | Q2 records that it must be excluded from `Equals`/`GetHashCode`, matching the existing rule for non-byte-affecting options. | S |
| Precompiled-adapter instances carry no `CompileContext` model type, so the guard cannot check them even when opted in — a partial guarantee. | Q2 records that the opt-in mirrors the DEBUG guard exactly, including that the precompiled path is skipped; documented as a known limit rather than silently divergent. | S |

## Success criteria

Measurable, checkable statements a spec could turn into a test.

- [ ] Over a stress run of N concurrent `Generate` calls interleaved with `Dispose()` on the same instance, no call ever throws `ObjectDisposedException` from a render that had already begun, no render produces corrupted/truncated output, and the runtime document is disposed exactly once after the last in-flight render returns.
- [ ] Under concurrent `Generate`/`Generate` load with no dispose, the in-flight counter returns to its starting value every time (no torn increment/decrement) and no render observes a disposed runtime document.
- [ ] `Dispose()` is idempotent: calling it twice (including the internal deferred re-entry from a render's completion) disposes owned resources once and never throws.
- [ ] A `FullCSharp` template whose embedded C# has a Roslyn-level error reported by `Compile` is also reported by `TryCompilation` (same error surfaced on the result), where today `TryCompilation` returns success.
- [ ] A template whose delayed subtemplate fails finalization (`CompleteInit`) and is reported by `Compile` is also reported by `TryCompilation`.
- [ ] For every template that `TryCompilation` returns success on, a fresh `Compile` of the same source + context also succeeds (no green-dry-run-then-red-compile pair exists in the test corpus).
- [ ] With the new opt-in enabled in a Release build, `Generate` with a wrong-typed `data` throws `TemplateProcessingException` carrying the same "Type mismatch. Need X but got Y" shape as the DEBUG guard.
- [ ] With the opt-in off (default), Release `Generate` behaviour and output are byte-identical to today for both correct and wrong-typed data (wrong-typed data is not validated).
- [ ] Enabling/disabling the new flag does not change `TemplateOptions` equality or hash code, and does not change the rendered bytes of any successful render.

## Validation scenarios

| Input | Expected outcome |
|---|---|
| 64 threads calling `Generate(model)` in a tight loop while one thread calls `Dispose()` at a random point, repeated over many iterations. | No `ObjectDisposedException` from a started render, no corrupted output; runtime document disposed once after the last render completes. |
| `Dispose()` called on an instance with zero in-flight renders, then called again. | Resources disposed once; second call is a no-op; neither throws. |
| A `FullCSharp` template with a compile-error in its inner-`@` C# (e.g. references an undefined symbol) passed to `TryCompilation`. | Result `Success == false` with the Roslyn error(s) in `ErrorList`, matching what `Compile` reports. |
| A template with a delayed subtemplate that fails `CompleteInit`, passed to `TryCompilation`. | Result `Success == false` with the finalization error(s), matching `Compile`. |
| A valid `FullCSharp` template passed to `TryCompilation`. | Result `Success == true`; a subsequent real `Compile` of the same source also succeeds. |
| Release build, opt-in flag **on**, `Generate(wrongTypedData)`. | Throws `TemplateProcessingException` (same message shape as DEBUG). |
| Release build, opt-in flag **off**, `Generate(wrongTypedData)`. | No type-mismatch throw; identical to today's Release behaviour. |
| Two `TemplateOptions` differing only by the new flag. | Compare `Equals`; hash codes equal (flag excluded from value identity). |

## Open questions

None — resolved (see [open-questions.md](open-questions.md)). Every P4 question (Q1–Q3) is folded into the design direction, risks, and success criteria above.

## External grounding

| Claim | Source |
|---|---|
| The in-flight disposal counter is not fully synchronized; callers are warned to avoid disposing an instance still being rendered concurrently. | [csharp-api.md — Disposal](../csharp-api.md) |
| `Dispose()` reads `if (_runners == 0)` then disposes `_runtimeDocument`, while `Render` guards on `_disposeAfterComplete` and does `_runners++`/`_runners--` non-atomically on a `volatile int` — the TOCTOU window. | [HeddleTemplate.cs](../../src/Heddle/HeddleTemplate.cs) |
| `TryCompilation` runs parse + extension/type-check but not deferred-finalization or embedded-C# (Roslyn), so a `FullCSharp` template (or one whose delayed subtemplate fails finalization) can pass the dry run and still fail `Compile`. | [csharp-api.md — Compilation methods](../csharp-api.md) |
| The skipped finalization pass (`compileScope.Compile()` under `!simulate`) is where `CompleteInit` over `DelayedTemplates` and the `FullCSharp` Roslyn parse/emit run. | [HeddleTemplate.cs](../../src/Heddle/HeddleTemplate.cs), [ContextCompilation.cs](../../src/Heddle/Runtime/ContextCompilation.cs) |
| In `DEBUG` builds `Generate` validates `data`'s type against the compiled model type and throws `TemplateProcessingException` on mismatch; Release builds skip this check. | [csharp-api.md — Generate](../csharp-api.md), [HeddleTemplate.cs `#if DEBUG` guard](../../src/Heddle/HeddleTemplate.cs) |
| Precedent for additive, default-off/null options that change no successful-render bytes and are excluded from `TemplateOptions` value-equality (`RenderBudget`, `MaxRecursionCount`). | [csharp-api.md — TemplateOptions](../csharp-api.md) |
| These are three of the four "documented sharp edges" the assessment lists as an engineering-culture signal / item holding the engine back. | [language-assessment.md §4.6](../language-assessment.md), [§8 item 4](../language-assessment.md) |
