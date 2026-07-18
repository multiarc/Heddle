# Phase 1 — file-based-api-correctness

## Header

- **Status:** ready — Plan DoR
- **Goal (one line):** Restore working recompile-on-change for a `HeddleTemplate` compiled from a file with `EnableFileChangeCheck = true`, correct across repeated edits.
- **Depends on:** nothing hard — but it **coordinates with [Phase 4](phase-4-engine-robustness.md)** on the shared `HeddleTemplate` dispose/lifecycle surface (`_runners` / `_disposeAfterComplete` / `_runtimeDocument`). Phase 1's safe swap-and-release of a *superseded* runtime document builds on the disposal synchronization Phase 4 hardens; neither blocks the other, but their diffs to those fields must be merged with each other in view (see Risks and [standing-rulings — cross-phase sequencing](common/standing-rulings.md#notes-on-cross-phase-sequencing)).
- **Changes an externally-visible contract:** No new contract — this restores the already-documented `EnableFileChangeCheck` / `OnFileChanged` / `OnFileDeleted` / `OnFileRenamed` behavior that is currently inoperative. The observable behavior of the opt-in flag changes from a no-op to functional.

## Goal

When a host compiles a template from a file and sets `EnableFileChangeCheck = true`, editing that file on disk should raise the file events and transparently recompile the template, so the next render reflects the new content. Today this feature is wired but non-functional: the [C# API reference](../csharp-api.md#file-watching) documents it as a "Current limitation" and the [engine assessment](../language-assessment.md) records the "inoperative file-change watcher" among the engine's known sharp edges (§4.6, §8). This phase makes the documented behavior real.

The user-visible outcome is a development-time live-reload loop: compile once from a file, edit the file, and subsequent renders pick up the change with no host intervention — including on the second, third, and further edits, and including for templates that use the embedded C# (`FullCSharp`) tier. The three defects that block this are compounding — each one alone would suffice to break the feature — so the phase delivers value only if all three are fixed together.

## Non-goals / scope boundary

This phase is a targeted correctness fix for one opt-in feature, not a hot-reload framework. Explicitly out of scope:

- **Async recompile.** Recompilation stays synchronous on the watcher callback, consistent with the engine's [no-async-render stance](../csharp-api.md#streaming-generate-into-a-sink).
- **A general hot-reload / file-provider abstraction.** No new pluggable file-source or reload-strategy surface.
- **Watching the transitive `@<<` import closure.** This phase watches the root template file only; a change to an imported dependency does not trigger recompile. The import closure is marked here as a **future seam** (see Open questions Q1), not delivered.
- **Precompiled templates.** The build-time precompiled backend (`Heddle.Precompiled`) is untouched; it has no file watcher and is not a live-reload target.

The boundary this phase marks for later work: dependency-aware watching (imports/partials), debounced/coalesced reloads, and any richer reload policy build on the corrected single-file mechanism this phase establishes.

## Design direction

The chosen approach is a **minimal additive fix of the three root causes**, converging recompile-on-change onto the engine's existing from-scratch compile behavior rather than adding new machinery:

1. **Correct the watcher filter to the real read path.** The watcher must filter on the same file name the [`FileReader`](../../src/Heddle/FileReader.cs) actually reads — the template name **plus** `FileNamePostfix` (`home.heddle`) — not the bare stem (`home`).
2. **Arm the watcher.** A `FileSystemWatcher` raises no events until it is enabled; enabling it is what makes any edit observable.
3. **Make each recompile equivalent to a fresh compile of the new content.** A recompile must run the same finalization the first compile runs — delayed-extension completion and, for `FullCSharp` templates, the embedded-C# (Roslyn) pass — so the second and later reloads produce a fully finalized document, not a partially-initialized one that reused already-"compiled" state.

Why this direction beats the alternatives: the engine already has a correct, well-exercised path that produces a fully finalized document — the constructor/`Compile` path used for the first compile. The cheapest and safest correctness story is for recompile-on-change to reach that **same finalized outcome** for the new content, rather than a reduced "second pass" that skips finalization. Weighed and rejected: (a) building a general reload/file-provider abstraction — over-engineered for a defect fix and a non-goal; (b) leaving the events as a no-op and only documenting the limitation more loudly — fails the phase goal; (c) fixing only the filter and arming, leaving the stale-recompile defect — explicitly disallowed, because it would expose a latent bug (a hot reload that silently serves an unfinalized/stale document) that is worse than today's honest no-op.

The direction is stated at the WHAT level: *a recompile after an on-disk change must yield a document equivalent to compiling the new file content from scratch, honoring the original options.* The mechanism (resetting finalize state versus compiling into a fresh context) is a spec concern.

## Dependencies & ordering

Nothing must land first; this is the first phase and it depends only on the existing dynamic compile path. It unblocks any later work on dependency-aware watching (the `@<<` import closure), reload coalescing/debounce, and richer reload policies, all of which assume a correct single-file reload as their foundation.

## Back-compat / impact

- **Opt-in only.** Everything here is gated behind `EnableFileChangeCheck = true`, which [defaults to `false`](../csharp-api.md#templateoptions). Hosts that do not set it see no change.
- **No rendered-byte change (R5).** No template's rendered output changes. Inline-string compiles, precompiled templates, and file compiles with the flag off are byte-identical to today. The fix only affects *when and how* a file-watched template is recompiled, never the bytes any given source produces.
- **Restores a documented contract.** The `OnFile*` events and `EnableFileChangeCheck` are already public and documented to work; this phase makes them behave as documented. The only observable change for a host that already set the flag is that reload now happens (previously it silently did not). The [csharp-api.md "Current limitation"](../csharp-api.md#file-watching) note and the [assessment's §4.6 / §8](../language-assessment.md) "inoperative file-change watcher" references should be updated to reflect the fix as part of this phase's delivery.
- **New runtime behavior on the opt-in path.** With the flag on, the watcher now runs a background callback that recompiles and swaps the live document. This introduces the concurrency and resource-release considerations captured under Risks and Open questions.

## Risks & mitigations

| Risk | Mitigation | Size (S/M/L) |
|---|---|---|
| A recompile on the watcher thread swaps the live document while a render is in flight on another thread, tearing state. | Publish the new document so an in-flight render sees a coherent old-or-new document, and release the superseded document through the deferred-dispose bookkeeping **Phase 4 hardens** — today recompile simply overwrites `_runtimeDocument` (orphaning the previous one), so this per-superseded release is *new* work built on Phase 4's disposal synchronization (see Open questions Q4). Do not attempt full multi-field atomicity beyond today's volatile-field visibility this phase. | M |
| Atomic-save editors (write-temp then rename onto the target) emit a rename/create, not a `Changed`, so a filter-and-arm-only fix still never recompiles for common editors — a partial fix that *looks* broken (violates R6). | Broaden the recompile trigger to cover the watcher events that resolve to the target file existing (create / rename-to-target), not only `Changed` (see Open questions Q3). | M |
| The superseded runtime document (and, over many reloads, watcher resources) leak if never released — today recompile overwrites `_runtimeDocument` without disposing the old one. | Add a per-superseded-document release that reuses the same deferred-dispose discipline `Dispose` applies under active renders (hardened in Phase 4), replacing the current silent overwrite. | S |
| A recompile of edited-but-broken source fails and leaves the template unusable or throws on a background thread with no host handler. | Keep serving the last-good document and surface the failure through the existing `CompileResult` / error channel (see Open questions Q2). | S |
| `FileSystemWatcher` fires duplicate/rapid `Changed` events for a single save, causing redundant recompiles. | After the stale-recompile fix, repeated recompiles are idempotent (each converges to the same finalized document), so redundancy is wasteful, not incorrect; debounce is deferred (see Open questions Q5). | S |
| Platform/environment quirks of `FileSystemWatcher` (network drives, case sensitivity, internal-buffer overflow) misbehave outside the engine's control. | Scope the feature to local development-time reload and document the platform caveats; do not attempt to paper over OS-level watcher limits. | S |

## Success criteria

Measurable, checkable statements a spec could turn into a test.

- [ ] With `EnableFileChangeCheck = true` compiling `home` + `.heddle`, the installed watcher's filter matches the actual on-disk file name **including** the postfix (`home.heddle`), not the bare stem (`home`).
- [ ] After a file compile with the flag on, an edit to that file on disk raises `OnFileChanged` and triggers a recompile with no further host call (the watcher is armed).
- [ ] After the first on-disk change, a subsequent render reflects the **new** file content, not the previously compiled content.
- [ ] A **second** and a **third** successive on-disk change each produce a correctly finalized document: a render after each change equals rendering a from-scratch compile of that same content — including for a `FullCSharp` template whose embedded C# expression changed (the Roslyn pass and delayed-extension finalization re-run on every recompile).
- [ ] A recompiled document honors the **original** `TemplateOptions` — model type, `ExpressionMode`, `OutputProfile`, `Encoder`, and `RenderBudget` — so behavior other than the changed content is unchanged.
- [ ] Over a scripted sequence of edit / delete / rename operations on the watched file, each documented event is raised for its corresponding on-disk operation — `OnFileChanged` for an in-place edit, `OnFileDeleted` for a deletion, `OnFileRenamed` for a rename — and no event is raised when no watched-file operation occurred.
- [ ] A failed recompile (edited source that no longer compiles) keeps the last-good document renderable and surfaces the error through the existing `CompileResult` / error channel, rather than corrupting state or throwing to a threadpool with no handler.
- [ ] No rendered-byte change for any template not using `EnableFileChangeCheck` (R5): inline compiles, precompiled templates, and file compiles with the flag off are byte-identical to before.
- [ ] The docs no longer describe recompile-on-change as inoperative: the [csharp-api.md "Current limitation"](../csharp-api.md#file-watching) note and the [assessment §4.6 / §8](../language-assessment.md) references are updated.

## Validation scenarios

| Input | Expected outcome |
|---|---|
| Compile `home.heddle` with `EnableFileChangeCheck = true`; inspect the watcher filter. | Filter equals `home.heddle` (name + postfix), so it can match the real file. |
| After that compile, edit `home.heddle`'s content and wait for the watcher. | `OnFileChanged` is raised; the next `Generate` reflects the new content. |
| Edit the same file three times in succession (a `FullCSharp` template whose embedded C# expression changes each time). | Each `Generate` after each edit reflects the latest content and equals a fresh from-scratch compile of that content; no stale or unfinalized output on the 2nd/3rd edit. |
| Edit content that does not change the declared model type, then render with the original model. | Render succeeds and honors the original model type / expression mode / profile / encoder / budget. |
| Introduce a syntax error into the file. | Recompile fails; the last-good document still renders; the error is visible on `CompileResult`. *(per Q2 lean)* |
| Delete the watched file. | `OnFileDeleted` fires; the last-good document still renders. |
| Save via atomic replace (editor writes a temp file then renames it onto `home.heddle`). | A recompile still occurs. *(per Q3 lean)* |
| Compile the same source inline (no file) or from a file with the flag off. | No watcher is installed; behavior and output are unchanged. |

## Open questions

None — resolved (see [open-questions.md](open-questions.md)). Every P1 question (Q1–Q5) is folded into the design direction, risks, and success criteria above.

## External grounding

| Claim | Source |
|---|---|
| The watcher filter is built from `RootPath` + `TemplateName` without `FileNamePostfix`, so it never matches the real file. | [HeddleTemplate.cs:263-275](../../src/Heddle/HeddleTemplate.cs) |
| The real read path resolves the file as `TemplateName` + `FileNamePostfix` (e.g. `home.heddle`). | [FileReader.cs:39](../../src/Heddle/FileReader.cs) |
| The filter defect is a documented "Current limitation": the events do not fire and recompile-on-change does not occur. | [csharp-api.md — File watching](../csharp-api.md#file-watching) |
| `FileSystemWatcher.EnableRaisingEvents` is never set to `true` anywhere in the repository, so no event fires even with a correct filter. | Repo-wide search for `EnableRaisingEvents` returns zero matches; handlers are wired but the watcher is left unarmed at [HeddleTemplate.cs:267-275](../../src/Heddle/HeddleTemplate.cs) |
| On a file change the same `CompileScope` (`_context`) instance is reused for recompile. | [HeddleTemplate.cs:404-424](../../src/Heddle/HeddleTemplate.cs) (field declared line 21, set line 347) |
| Recompile overwrites `_runtimeDocument` with the new document **without disposing the superseded one**, orphaning it — so the per-superseded-document release is new work (built on Phase 4's disposal synchronization), not an existing path. | [HeddleTemplate.cs:349](../../src/Heddle/HeddleTemplate.cs) |
| Delayed-extension finalization (`CompleteInit`) is guarded by `!context.Compiled` and sets `Compiled = true`, so a reused, already-compiled context skips finalization on the second recompile. | [ContextCompilation.cs:78-89](../../src/Heddle/Runtime/ContextCompilation.cs) |
| The embedded-C# (Roslyn) pass is guarded by `!CSharpContext.Compiled` and sets it `true`, so a `FullCSharp` template's recompile skips the Roslyn pass on a reused context. | [ContextCompilation.cs:97-98, 225](../../src/Heddle/Runtime/ContextCompilation.cs) |
| The inoperative file-change watcher is a recognized, documented engine sharp edge. | [language-assessment.md §4.6 and §8](../language-assessment.md) |
| `EnableFileChangeCheck` is opt-in and defaults to `false`. | [csharp-api.md — TemplateOptions](../csharp-api.md#templateoptions) |
| Disposal is best-effort deferred while a render is in progress via a not-fully-synchronized runner counter — the mechanism relevant to releasing a superseded document (Q4). | [csharp-api.md — Disposal](../csharp-api.md#disposal) |
