# Phase 4 — engine-robustness (spec)

## Header

- **Status:** Specified — ready for implementation
- **Source plan:** [phase-4-engine-robustness.md](../../plan/phase-4-engine-robustness.md) (Plan DoR; P4-Q1..Q3 resolved in [open-questions.md](../../plan/open-questions.md))
- **Assumes merged:** nothing. The three fixes here are mutually independent and none consumes another phase's deliverable. This spec **coordinates** with [Phase 1](../../plan/phase-1-file-based-api-correctness.md) on the shared `HeddleTemplate` dispose/lifecycle surface — it establishes the disposal-synchronization primitive Phase 1's superseded-document release will build on (see [SD-A forward note](#sd-a--forward-note-non-normative-for-phase-1)) — but it does not depend on Phase 1 code and Phase 1 is not assumed merged.

| Document | Purpose |
|---|---|
| this document | Specifies all three Phase 4 fixes: the concurrent-dispose synchronization, `TryCompilation` full-parity dry run, and the opt-in Release model-type guard. |

This spec is self-contained given the [common specs](../common/spec-conventions.md); it restates the minimal context it needs. Source citations give `path` + member name; load-bearing line numbers carry their anchor symbol and are marked *(verify at implementation — line numbers drift)*.

## Scope and goal

Close three documented engine sharp edges without changing the rendered bytes of any valid template or flipping an existing default (standing ruling [R5](../../plan/common/standing-rulings.md#standing-rulings-user-set), additive/pure-correctness):

1. **Concurrent-dispose race** — make `HeddleTemplate.Dispose()` and the in-flight render bookkeeping observe a single consistent state so the runtime document is never disposed while a render is executing, the runner counter never tears under concurrent renders, and `Dispose` stays idempotent and non-blocking (no drain/stall).
2. **`TryCompilation` false-confidence gap** — run the finalization + Roslyn passes a real `Compile` runs during the dry run and collect their errors, then discard the artifact as today, so a green dry run predicts `Compile` success.
3. **Release-mode model-type validation** — add an opt-in `TemplateOptions` flag, default off, that enables in Release the identical wrong-model-type guard that today fires only in `DEBUG`.

**Out of scope** (per the plan's non-goals): the file-change watcher (Phase 1), async rendering, redesigning the render pipeline, changing any existing option default, and `RenderBudget`. This spec adds **no** new `HED*` diagnostic (the Release guard throws the existing `TemplateProcessingException`, not a compile diagnostic — see [Diagnostics](#diagnostics)).

## Assumed state

Re-verified against current source on branch `feature/lang_asmt`. Line anchors are *(verify at implementation)*.

| Seam | Verified state |
|---|---|
| Shared lifecycle fields | [`HeddleTemplate`](../../../src/Heddle/HeddleTemplate.cs): `_context` is `CompileScope` (≈ line 21, **plain field, not volatile**); `_runtimeDocument` is `volatile RuntimeDocument` (≈25); `_processStrategy` is `volatile IProcessStrategy` (≈26); `_disposeAfterComplete` is `volatile bool` (≈28); `_runners` is `volatile int` (≈29). |
| Render enter/exit bookkeeping | `HeddleTemplate.Render` does **non-atomic** `_runners++` (≈223) then, in `finally`, `_runners--` (≈232) followed by `if (_disposeAfterComplete && _runners == 0) Dispose();` (≈233-236). Before the increment it throws `ObjectDisposedException` when `_disposeAfterComplete` is set (≈219-222). |
| Dispose / finalizer | `HeddleTemplate.Dispose()` (≈99-111): `if (_runners == 0) { _watcher?.Dispose(); _runtimeDocument?.Dispose(); GC.SuppressFinalize(this); } else { _disposeAfterComplete = true; }`. The `_runners == 0` read (≈101) and `_runtimeDocument?.Dispose()` (≈104) are **unsynchronized** — a concurrent `Render`'s `_runners++` (≈223) can land between them (the TOCTOU window). The finalizer `~HeddleTemplate()` (≈113-117) duplicates `_watcher?.Dispose(); _runtimeDocument?.Dispose();` with no idempotency guard. |
| DEBUG model-type guard | `HeddleTemplate.Render` (≈209-218) under `#if DEBUG`: `if (!_precompiled && data != null && !_context.ScopeType.Type.IsType(data)) throw new TemplateProcessingException(string.Format(CultureInfo.InvariantCulture, "Type mismatch. Need {0} but got {1}", _context.ScopeType.Type?.FullName ?? _context.ScopeType.ToString(), data.GetType().FullName));`. Release compiles it out. `_precompiled` is a `#if DEBUG`-only `readonly bool` (≈75), assigned `true` in the precompiled-adapter ctor (≈87); its **sole reader** is the guard at line 210 (verified by grep — no other reference in the tree). `_context` is `null` for the precompiled adapter (never assigned) and non-null for every compiled template (set at ≈347), so `_context != null` ⟺ `!_precompiled` at the guard point. |
| `IsType` semantics | [`TypeExtension.IsType(this Type, object)`](../../../src/Heddle/Helpers/TypeExtension.cs) returns `typeToCheck.IsInstanceOfType(data)` and **throws `ArgumentNullException` if `typeToCheck` is null**; the guard reaches it only with `data != null`. `ExType.Type` ([`ExType`](../../../src/Heddle/Data/ExType.cs)) is the wrapped `System.Type` (non-null for typed and untyped-`object` contexts). |
| Dry-run skip | `HeddleTemplate.Compile(CompileScope, string, bool simulate)` (≈319-386): when `simulate` is true the branch at ≈354-358 does `compileScope.Dispose(); rtdoc.Dispose();` and **skips** `compileScope.Compile()` (≈338, the only call to it). The non-simulate branch stores the artifact and resolves `_encoder`/`_renderBudget` from options (≈347-352). |
| Finalization + Roslyn pass | `compileScope.Compile()` is [`ContextCompilation.Compile(this CompileScope)`](../../../src/Heddle/Runtime/ContextCompilation.cs) (≈94-121): it runs `Compile(this CompileContext)` (≈78-89, `CompleteInit` over `DelayedTemplates`, guarded by `context.Compiled`) and, for `ExpressionMode.FullCSharp` with methods, `CompileCSharp` (≈123-227). `CompileCSharp` parses/emits via Roslyn, populates the process-global `Cache` keyed by generated code (≈132/141/187) with `Assembly.Load`, adds errors to `CompileContext.CompileErrors` on failure (≈165-167/178-180), can throw `TemplateCompileException` (≈127), and sets `CSharpContext.Compiled = true` (≈225). The static `Cache` is a `ConcurrentDictionary<string, Assembly>` (≈26), idempotent by generated code. |
| `TemplateOptions` shape | [`TemplateOptions`](../../../src/Heddle/Data/TemplateOptions.cs): copy ctor (≈127-143) copies every option incl. `RenderBudget` with the comment "not part of Equals/GetHashCode or the fingerprint"; `Equals(TemplateOptions)` (≈147-149) compares `FileNamePostfix`, `TemplateName`, `RootPath`, `OutputProfile`, `TrimDirectiveLines`, and `Encoder` (by reference), while `GetHashCode()` (≈171-179) hashes a **narrower** set — `TemplateName`, `OutputProfile`, `TrimDirectiveLines`, `Encoder` (it does **not** hash `FileNamePostfix` or `RootPath`). Either way, `RenderBudget`/`MaxRecursionCount`/`PrecompiledMismatchPolicy` are copied but excluded from both — the precedent `ValidateModelType` follows. |
| Completeness test | [`TemplateOptionsCompletenessTests`](../../../src/Heddle.Tests/TemplateOptionsCompletenessTests.cs) reflects over every public settable property and round-trips it through the copy ctor; `Synthesize` already handles `bool` (returns `true`), so a new `bool` option is auto-covered with **no test edit**. |
| Concurrency-test model | [`BranchConcurrencyTests`](../../../src/Heddle.Tests/BranchConcurrencyTests.cs) uses `Parallel.For(0, 20000, …)` over one compiled template asserting per-iteration correctness — the model this spec's dispose-stress test follows. |
| Guarding benchmarks | [`TextRenderBenchmarks.RenderHeddle`](../../../src/Heddle.Performance/TextRenderBenchmarks.cs) (string render path, `[MemoryDiagnoser]`, `Baseline = true`) and [`SinkRenderBenchmarks`](../../../src/Heddle.Performance/SinkRenderBenchmarks.cs) (`RenderString`/`RenderTextWriter`/`RenderUtf8Buffer`) all route through `HeddleTemplate.Render` — the methods this spec's disposal change touches. |
| Target frameworks | Engine code must compile and behave on `netstandard2.0;net6.0;net8.0;net10.0` ([coding standards](../common/coding-standards.md#language-and-target-frameworks)). `System.Threading.Interlocked` and `System.Threading.Volatile` are present on all of these. |

**Plan corrections found.** None material. The plan's field descriptions all match source; the plan's line ranges were confirmed and are cited here with anchor symbols rather than trusted verbatim.

## Design decisions

Every P4 plan question, lean, and pin appears here as a closed decision.

### D1 — Disposal synchronization: Interlocked runner-count + CAS-guarded single teardown (closes P4-Q3, SD-A)

- **Decision.** Replace the non-atomic, TOCTOU-prone bookkeeping with a lock-free, allocation-free state discipline over three `int` fields on `HeddleTemplate`:
  - `_runners` (`int`) — active render count, mutated **only** via `Interlocked.Increment`/`Interlocked.Decrement`, read via `Volatile.Read`.
  - `_disposeAfterComplete` (**changed from `volatile bool` to `int`**, same name kept for the SD-A shared surface; `0` = live, `1` = disposal requested) — set via `Interlocked.Exchange`, read via `Volatile.Read`. Interlocked has no `bool` overload on `netstandard2.0`, so the flag becomes an `int`.
  - `_teardownDone` (`int`, new) — `0`/`1` one-shot guard; the actual resource teardown runs exactly once, chosen by `Interlocked.CompareExchange(ref _teardownDone, 1, 0)`.

  The primitive is three private helpers implementing a coherent **enter / exit / "may I dispose now?"** decision over one consistent state (exact bodies in [WI1](#wi1--disposal-synchronization-heddletemplatecs)):
  - **Enter** (render start): read `_disposeAfterComplete`; if set, throw `ObjectDisposedException`. Else `Interlocked.Increment(_runners)`, then **re-read** `_disposeAfterComplete`; if now set, back the increment out via *Exit* and throw `ObjectDisposedException`. Only a render that observes the flag clear at **both** points proceeds to `_processStrategy.Render`.
  - **Exit** (render end, in `finally`): `Interlocked.Decrement(_runners)`; if the result is `0` **and** `_disposeAfterComplete` is set, run *Teardown*.
  - **Dispose decision**: `Interlocked.Exchange(_disposeAfterComplete, 1)` (fences new renders out and is idempotent), then if `Volatile.Read(_runners) == 0` run *Teardown*; otherwise the last Exit runs it. No spin, no wait, no drain.
  - **Teardown**: `if (Interlocked.CompareExchange(ref _teardownDone, 1, 0) != 0) return;` then `_watcher?.Dispose(); _runtimeDocument?.Dispose(); GC.SuppressFinalize(this);` (unchanged order). The finalizer routes through Teardown too, so an explicit `Dispose` and a later finalization never double-dispose.

- **Rationale.** The correctness proof rests on ordering `Dispose`'s *set-flag-then-read-runners* against enter's *increment-then-re-read-flag*: if any render is executing (it passed the re-read with the flag clear), that clear read precedes `Dispose`'s flag write in the location's total order, so the render's earlier increment also precedes `Dispose`'s runners read — `Dispose` therefore sees `_runners ≥ 1` and defers. Teardown via Exit only fires when the decrement reaches `0`, which cannot happen while another render holds a slot. Hence **no dispose under active render** and **no use-after-dispose**. `Interlocked` on the counter removes the torn `++`/`--`. The CAS on `_teardownDone` makes teardown run **once** across the explicit-Dispose, deferred-Exit, and finalizer paths — **idempotent**. Nothing blocks, satisfying P4-Q3 ("safety-only, non-blocking; no drain/stall"). All operations are `int` atomics with **zero heap allocation** — no lock object, no `Monitor`, no wait handle — so the render hot path gains only a couple of atomic ops per whole render call (not per character), guarded by the benchmarks in [Performance](#performance-considerations). `netstandard2.0`-compatible: `Interlocked`/`Volatile` ship in that surface.

- **Alternatives rejected.**
  - *`lock`/`Monitor` around enter/exit and the dispose decision* — a monitor taken on every render serializes concurrent renders and regresses the best-in-class throughput the benchmarks protect; rejected for measured hot-path cost (precedence rule 4, [coding standards](../common/coding-standards.md#precedence-when-principles-conflict)).
  - *`ReaderWriterLockSlim` / `SemaphoreSlim` drain in `Dispose`* — introduces a blocking wait, directly violating P4-Q3, and allocates.
  - *`CountdownEvent` / `ManualResetEventSlim` barrier* — a blocking barrier (P4-Q3) plus an allocation on the lifecycle path.
  - *Keep `volatile bool _disposeAfterComplete` and only atomize `_runners`* — leaves the flag/counter decision non-atomic across two locations (the original TOCTOU); a single fenced flag read/write ordering is required for the proof.

- **Grounding.** [`HeddleTemplate.Dispose`/`Render`](../../../src/Heddle/HeddleTemplate.cs); [Interlocked](https://learn.microsoft.com/en-us/dotnet/api/system.threading.interlocked) and [Volatile](https://learn.microsoft.com/en-us/dotnet/api/system.threading.volatile) (both `netstandard2.0`); model harness [`BranchConcurrencyTests`](../../../src/Heddle.Tests/BranchConcurrencyTests.cs).

#### SD-A — forward note (non-normative, for Phase 1)

Phase 1's watcher recompile must release the **superseded** `_runtimeDocument` when it swaps a freshly compiled document in, without disposing a document an in-flight render still reads. That release reuses **this** deferred-dispose discipline: the superseded document is torn down under the same "is any render active?" decision (the `_runners == 0` / last-Exit gate) rather than being orphaned as the current code does. Phase 4 owns and ships the primitive (the enter/exit helpers and the atomic state); Phase 1 layers a per-document release on top and reconciles the shared fields with this diff in view ([standing-rulings — cross-phase sequencing](../../plan/common/standing-rulings.md#notes-on-cross-phase-sequencing)). Phase 4 specifies **no** watcher work.

### D2 — `TryCompilation` full parity: run finalization + Roslyn during the dry run, then discard (closes P4-Q1)

- **Decision.** In `HeddleTemplate.Compile(CompileScope, string, bool simulate)`, hoist `compileScope.Compile()` (finalization of `DelayedTemplates` via `CompleteInit`, plus the `FullCSharp` Roslyn parse/emit) so it runs in **both** modes, immediately after the object-graph compile's error check. Its errors are collected onto the result exactly as the non-simulate path already does. Only the **store-the-artifact** step (`_context`/`_document`/`_runtimeDocument`/`_processStrategy`/`_encoder`/`_renderBudget`/`_validateModelType` assignment) stays guarded by `!simulate`; the `simulate` branch disposes `compileScope` and `rtdoc` and returns the (now finalization-checked) result.

  Two correctness constraints the dry run must satisfy (both are load-bearing for the "green dry run predicts a real `Compile` of the same source + context" contract):

  1. **The dry run must not mutate the caller's `CompileContext`.** `compileScope.Compile()` runs `ContextCompilation.Compile(CompileContext)` (≈78-89), which sets `context.Compiled = true`, clears `context.DelayedTemplates`, and accumulates errors onto `context.CompileErrors` (`CompileScope.CompileErrors` delegates to `CompileContext.CompileErrors`, [`CompileScope.cs`](../../../src/Heddle/Runtime/CompileScope.cs):15). Today `TryCompilation(CompileContext context)` wraps the caller's **own** context instance (`new CompileScope(context)`, ≈302-309), so a green dry run would leave that instance `Compiled == true` with `DelayedTemplates` emptied and prior errors on its list — and a subsequent real `Compile` of the **same** context instance would then **skip** `CompleteInit` (the `!context.Compiled` guard) and serve an unfinalized document (or spuriously fail on the leftover errors). Therefore `TryCompilation(CompileContext context)` compiles the probe into a **fresh** context reconstructed from the caller's `Options` + `RootScopeType` + current `ScopeType` + `OutputProfile` (+ `ControllerName`) — never the caller's instance — so the caller's context stays pristine and a real `Compile` of it finalizes from scratch. The probe mirrors the caller's current `ScopeType`/`OutputProfile` (both public-settable and independently mutable — [`CompileContext.cs`](../../../src/Heddle/Runtime/CompileContext.cs):218/203) so the dry run predicts the real compile even for a caller that mutated `ScopeType` (away from `RootScopeType`) or flipped `OutputProfile` after construction, not only for a freshly-constructed root context. (The `TryCompilation(string, TemplateOptions, ExType)` overload already builds a fresh throwaway `new CompileContext(options, modelType)`, ≈315-316, so it is already isolated — no change.)
  2. **The dry-run artifact must be disposed on every non-store exit, including exceptions.** `compileScope.Compile()` can **throw** (`CompileCSharp` throws `TemplateCompileException` when `InitErrors` is unsuccessful, [`ContextCompilation.cs`](../../../src/Heddle/Runtime/ContextCompilation.cs):126-128), which the existing `catch (TemplateCompileException)`/`catch (Exception)` (≈361-372) turn into an error result **without** disposing `compileScope`/`rtdoc` — a leak of the freshly built runtime document and its `CompileScope`. The dry run therefore guards artifact disposal with a `finally` keyed on a `stored` flag, so the artifact is disposed on the error-return, the simulate-success, **and** the exception paths, and retained **only** when it was actually published to the template fields. Exact diff in [WI2](#wi2--trycompilation-full-parity-heddletemplatecs).

- **Rationale.** Prediction is the whole point of a lint/CI dry run: a pass that returns success while a real `Compile` would fail is worse than no gate. Running the identical passes a real compile runs makes a green dry run mean "this template compiles." Isolating the probe onto a fresh reconstructed context (constraint 1) makes the contract **literally** true — a green dry run does not perturb the caller's context, so the real `Compile` of that same source + context runs the identical passes from a clean state. The dry run does **no new kind of work** — it runs exactly the passes `Compile` runs — so it cannot make a genuinely compilable template fail; it only surfaces pre-existing breakage earlier. **Added cost:** a `FullCSharp` dry run now pays Roslyn parse/emit **and permanently loads a new assembly.** The generated class name embeds a per-`CSharpContext` `ClassGuid` (`Guid.NewGuid()`, [`CSharpContext.cs`](../../../src/Heddle/Runtime/CSharpContext.cs):94; emitted into the code as `class CSE_<guid>`, [`CSharpClassTemplate.tcs`](../../../src/Heddle/LanguageTemplates/CSharpClassTemplate.tcs):3), and `ContextCompilation.Cache` is keyed by the **generated code text** ([`ContextCompilation.cs`](../../../src/Heddle/Runtime/ContextCompilation.cs):132). Because the isolated probe carries a **different** `CSharpContext` (hence a different `ClassGuid`, hence different code text) than the caller's real compile, the real compile is a cache **miss** and re-emits + `Assembly.Load`s a **second** assembly. Loaded assemblies are never unloaded (`CompileContext.Dispose` is a documented no-op TODO), so a `FullCSharp` `TryCompilation` costs one permanently-loaded assembly, and the real compile costs another — the accepted, documented cost of P4-Q1's full parity (the earlier "idempotent cache hit avoids the re-emit" framing was **incorrect**: distinct `ClassGuid`s never share a cache entry).

- **Decision status — maintainer-confirmed (2026-07-15).** Full parity is a **maintainer-confirmed decision, not an open lean**: keep the full-parity `TryCompilation`, **accepting and documenting** the per-call cost this section quantifies — one **permanently-loaded, un-reclaimable assembly per `FullCSharp` dry run**, on the **dry-run (CI / host-validation) path only, never the render path**. The user was shown that true cost (a **guaranteed** cache miss via the distinct per-`CSharpContext` `ClassGuid`, and assemblies that are never unloaded because `CompileContext.Dispose` is a no-op TODO — i.e. an unbounded leak on a dry-run-loop workflow, where today's `simulate` path leaks zero) and **chose to keep full parity** over the cheaper explicit-partial contract P4-Q1 reserved — so that reserved downgrade is **closed as not taken** (retained only as a [Deferred item](#deferred-items) should the cost prove unacceptable in practice). **Revisit trigger:** assembly-unloading support (`AssemblyLoadContext` unload wired into `CompileContext.Dispose`, today a no-op TODO — see [Deferred items](#deferred-items)), which converts the permanent leak into a reclaimable transient and is the designated mitigation for this accepted cost.

- **Alternatives rejected.** *Explicit-partial contract* (leave the dry run partial but document/expose that it does not cover finalization/Roslyn, and add a queryable "partial" marker) — the cheaper option the plan flags as the user's possible downgrade; rejected because it preserves the false-confidence footgun the fix exists to remove, and a queryable partial flag pushes the gap onto every CI author instead of closing it. *Run finalization but skip Roslyn* — would still let a `FullCSharp` Roslyn error pass the dry run, i.e. not parity. *Run the dry run on the caller's own context (today's wiring)* — poisons the context for a subsequent real compile (constraint 1), the exact false-confidence-in-reverse the fix must avoid.

- **Grounding.** [`HeddleTemplate.Compile(CompileScope,string,bool)`](../../../src/Heddle/HeddleTemplate.cs) simulate branch; [`ContextCompilation.Compile`/`CompileCSharp`](../../../src/Heddle/Runtime/ContextCompilation.cs).

### D3 — Release model-type guard: opt-in `TemplateOptions.ValidateModelType`, default off, excluded from identity (closes P4-Q2)

- **Decision.** Add `public bool ValidateModelType { get; set; }` to [`TemplateOptions`](../../../src/Heddle/Data/TemplateOptions.cs), **default `false`** (today's Release behavior). Resolve it once at compile into a new `HeddleTemplate` field `private bool _validateModelType;` (alongside `_encoder`/`_renderBudget`, only on the `!simulate` store path). Rewrite the model-type guard in `Render` so it fires when validation is active — **always** in `DEBUG` (unchanged historical behavior), and in Release **only when the opt-in is set** — reproducing the guard body and message verbatim. To make the same condition token work in Release, promote `_precompiled` out of `#if DEBUG` (it is now read in Release by the opt-in guard, so no `CS0414`) and gate on `!_precompiled`, which keeps the **precompiled path skipped** exactly as the DEBUG guard does. Copy-ctor completeness: add `ValidateModelType = value.ValidateModelType;` to the copy constructor. **Exclude** it from `Equals`/`GetHashCode` (it changes no bytes of a successful render, so it must never perturb the template cache key) — the `RenderBudget`/`MaxRecursionCount` precedent. Exact shapes in [WI3](#wi3--release-model-type-guard-templateoptionscs--heddletemplatecs) and [Public API contract](#public-api-contract).

- **Rationale.** Off-by-default preserves today's exact Release behavior and its zero-cost hot path (R5: no flipped default, no rendered-byte change); on, it reproduces the DEBUG guard's `TemplateProcessingException` and message so DEBUG and opted-in-Release behave identically. Mirroring the DEBUG guard exactly — including that the precompiled adapter (which carries no `CompileContext` `ScopeType`) is skipped — keeps the opt-in a faithful extension rather than a subtly divergent second check; the precompiled skip is a **documented known limit**, not silent divergence. Excluding the flag from identity matches the repo rule for options that change failure handling but not successful-render bytes.

- **Alternatives rejected.** *On-by-default in the 2.0 window* — the plan notes the revised R5 window would permit it later, but it adds hot-path cost and changes Release behavior for every host; not taken now (P4-Q2). *Include the flag in `Equals`/`GetHashCode`* — would fragment the template cache and trip the precompiled options fingerprint for a flag that changes no output bytes; rejected (the exact mistake the [risks table](../../plan/phase-4-engine-robustness.md) warns against). *A separate `IValidatingTemplate` seam or a distinct exception type* — over-abstraction for a one-line opt-in; YAGNI ([coding standards](../common/coding-standards.md#yagni-applied)).

- **Grounding.** [`HeddleTemplate.Render` DEBUG guard](../../../src/Heddle/HeddleTemplate.cs); [`TemplateOptions.RenderBudget`](../../../src/Heddle/Data/TemplateOptions.cs) exclusion precedent; [`TemplateProcessingException`](../../../src/Heddle/Exceptions/TemplateProcessingException.cs).

### D4 — The three fixes are independent and land in any order

- **Decision.** WI1/WI2/WI3 touch disjoint code (dispose/render bookkeeping; the `simulate` compile branch; the guard + options), share no state, and unblock nothing between them. The [Implementation plan](#implementation-plan) orders them by touch-surface risk (disposal first, as it is the SD-A foundation Phase 1 consumes) but they may be reviewed/merged separately. Rationale: the plan's own "Dependencies & ordering" says the three are independent; ordering here is editorial, not technical.

## Implementation plan

Ordered work items an implementer works straight down. Each is independently completable (D4).

### WI1 — Disposal synchronization ([`HeddleTemplate.cs`](../../../src/Heddle/HeddleTemplate.cs))

- **Files.** `src/Heddle/HeddleTemplate.cs`.
- **Change.**
  1. Fields: change `_disposeAfterComplete` from `volatile bool` to `private int _disposeAfterComplete;`; change `_runners` from `volatile int` to a plain `private int _runners;` — **drop `volatile`**, because passing a `volatile` field by `ref` to `Interlocked.Increment`/`Interlocked.Decrement`/`Volatile.Read` raises **CS0420** ("a reference to a volatile field will not be treated as volatile"); the `Interlocked`/`Volatile` calls provide the fences, so `volatile` is both redundant and warning-inducing. `_disposeAfterComplete` and the new `private int _teardownDone;` are likewise plain `int`s accessed only via `Interlocked`/`Volatile` (same CS0420 reason). Never mix a plain `++`/`--` or a direct read with these fields.
  2. Add three private helpers:

     ```csharp
     private void EnterRender()
     {
         if (Volatile.Read(ref _disposeAfterComplete) != 0)
             throw new ObjectDisposedException($"{GetType()} Disposed");
         Interlocked.Increment(ref _runners);
         // Dispose() may have set the flag between the read and the increment; if so, back out.
         if (Volatile.Read(ref _disposeAfterComplete) != 0)
         {
             ExitRender();
             throw new ObjectDisposedException($"{GetType()} Disposed");
         }
     }

     private void ExitRender()
     {
         if (Interlocked.Decrement(ref _runners) == 0 && Volatile.Read(ref _disposeAfterComplete) != 0)
             Teardown();
     }

     private void Teardown()
     {
         if (Interlocked.CompareExchange(ref _teardownDone, 1, 0) != 0)
             return; // already torn down — idempotent across Dispose / deferred exit / finalizer
         _watcher?.Dispose();
         _runtimeDocument?.Dispose();
         GC.SuppressFinalize(this);
     }
     ```
  3. `Render`: replace the `if (_disposeAfterComplete) throw …; _runners++;` prologue (≈219-223) with `EnterRender();`, and replace the `finally { _runners--; if (_disposeAfterComplete && _runners == 0) Dispose(); }` (≈230-237) with `finally { ExitRender(); }`. The `try { var scope = …; _processStrategy.Render(scope); }` body is unchanged.
  4. `Dispose()` (≈99-111): replace body with

     ```csharp
     Interlocked.Exchange(ref _disposeAfterComplete, 1); // fence out new renders; idempotent
     if (Volatile.Read(ref _runners) == 0)
         Teardown();
     ```
  5. `~HeddleTemplate()` (≈113-117): replace body with `Teardown();` (the `GC.SuppressFinalize` inside is a no-op during finalization; the CAS makes a post-explicit-Dispose finalizer a no-op).
- **Done when.** Solution builds on all TFMs; the new [disposal tests](#testing-plan) (stress, idempotency, deferred-teardown, counter-balance) pass on every TFM; the existing suite is green and goldens byte-identical; `TextRenderBenchmarks.RenderHeddle` and `SinkRenderBenchmarks` show no allocation increase and no mean-time regression beyond BenchmarkDotNet's reported error.

### WI2 — `TryCompilation` full parity ([`HeddleTemplate.cs`](../../../src/Heddle/HeddleTemplate.cs))

- **Files.** `src/Heddle/HeddleTemplate.cs` (`Compile(CompileScope, string, bool simulate)`, ≈319-386; `TryCompilation(CompileContext)`, ≈302-309).
- **Change.** (a) Rewrite the artifact-lifetime region so `compileScope.Compile()` runs in **both** modes and the artifact is disposed on every non-store exit — including exceptions — via a `finally` keyed on `stored` (constraint 2 of [D2](#d2--trycompilation-full-parity-run-finalization--roslyn-during-the-dry-run-then-discard-closes-p4-q1)):

  ```csharp
  RuntimeDocument rtdoc = HeddleCompiler.Compile(optimizedDocument, compileScope, parseContext, null);
  bool stored = false;   // true once the artifact is owned elsewhere (published to fields, or disposed by a dispose-race branch) → the finally must not dispose it
  try
  {
      if (compileScope.CompileErrors.Count > 0)
      {
          var result = new HeddleCompileResult(false, document, parseContext);
          result.Errors.AddRange(compileScope.CompileErrors);
          return result;                       // finally disposes compileScope + rtdoc
      }
      compileScope.Compile();                  // P4-Q1 parity: CompleteInit + FullCSharp Roslyn, in dry runs too — MAY THROW
      if (compileScope.CompileErrors.Count > 0)
      {
          var result = new HeddleCompileResult(false, document, parseContext);
          result.Errors.AddRange(compileScope.CompileErrors);
          return result;                       // finally disposes
      }
      if (!simulate)
      {
          _context = compileScope;
          _document = optimizedDocument;
          _runtimeDocument = rtdoc;
          _processStrategy = rtdoc?.Strategy;
          _encoder = compileScope.CompileContext.Options.Encoder;
          _renderBudget = compileScope.CompileContext.Options.RenderBudget;
          _validateModelType = compileScope.CompileContext.Options.ValidateModelType; // WI3
          stored = true;                       // retained by the template — do NOT dispose in finally
      }
      return new HeddleCompileResult(true, document, parseContext);
  }
  finally
  {
      if (!stored)
      {
          compileScope.Dispose();
          rtdoc?.Dispose();
      }
  }
  ```

  Delete the old inner `if (!simulate) { compileScope.Compile(); if (…) {…} … } else { compileScope.Dispose(); rtdoc.Dispose(); }` and the old explicit disposes on the error-return paths, so `compileScope.Compile()` is called **exactly once** and disposal lives in the one `finally`. The `catch (TemplateCompileException)`/`catch (Exception)` blocks (≈361-372) are unchanged — the `finally` runs as the exception propagates through them, so a throwing `compileScope.Compile()` no longer leaks. The `_validateModelType` store line is WI3's; if WI2 lands first, omit it and add it with WI3. **Phase-1 note:** Phase 1 wraps the `!simulate` store block in `lock (_publishGate)` and adds a dispose-race branch that disposes the artifact and sets `stored = true`; the `stored`-guarded `finally` is the same flag both phases key on (see [Phase 1 D5](phase-1-file-watcher.md#d5--safe-live-swap-publish-a-coherent-old-or-new-document-release-the-superseded-one-via-phase-4s-deferred-dispose-discipline-closes-p1-q4)).

  (b) In `TryCompilation(CompileContext context)`, compile the probe into a **fresh** reconstructed context instead of the caller's instance (constraint 1 of D2):

  ```csharp
  public HeddleCompileResult TryCompilation(CompileContext context)
  {
      if (_runtimeDocument != null)
          throw new TemplateInitException("Template already compiled.");
      var reader = new FileReader(context.Options);
      var document = reader.ReadEntireFile();
      // Isolate the dry run: reconstruct from the caller's options + root model type (+ ControllerName), and mirror
      // the caller's *current* ScopeType and OutputProfile so the probe compiles the same context state a real
      // Compile(context) would — even if the caller mutated ScopeType (so it diverges from RootScopeType) or flipped
      // OutputProfile after construction. The caller's own CompileContext is never mutated (Compiled /
      // DelayedTemplates / CompileErrors), so a subsequent real Compile of that context finalizes from scratch.
      var probe = new CompileContext(context.Options, context.RootScopeType)
      {
          ScopeType = context.ScopeType,          // mirror a caller-mutated model type; setter leaves the already-non-null RootScopeType intact
          OutputProfile = context.OutputProfile,  // mirror a caller-flipped @profile default
          ControllerName = context.ControllerName,
      };
      return Compile(new CompileScope(probe), document, true);
  }
  ```
- **Done when.** The [parity tests](#testing-plan) pass: a `FullCSharp` Roslyn error and a delayed-subtemplate finalization failure each now surface on `TryCompilation`; a valid `FullCSharp` template returns success **and** a subsequent real `Compile` of the **same source + context instance** also succeeds (no green-dry-run-then-poisoned-compile); a `TryCompilation` whose finalization throws leaks no artifact (no undisposed `RuntimeDocument`/`CompileScope`); the existing `TryCompilation` tests (e.g. `HeddleTemplateTests.WierdWhiteSpace`) stay green; full suite green.

### WI3 — Release model-type guard ([`TemplateOptions.cs`](../../../src/Heddle/Data/TemplateOptions.cs) + [`HeddleTemplate.cs`](../../../src/Heddle/HeddleTemplate.cs))

- **Files.** `src/Heddle/Data/TemplateOptions.cs`, `src/Heddle/HeddleTemplate.cs`.
- **Change.**
  1. `TemplateOptions`: add the property (final signature/XML-doc in [Public API contract](#public-api-contract)); add `ValidateModelType = value.ValidateModelType;` to the copy ctor (≈127-143); **do not** touch `Equals`/`GetHashCode`. The parameterless/string ctors need no change (default `false` matches the other opt-in options).
  2. `HeddleTemplate`: add `private bool _validateModelType;`. Set it on the `!simulate` store path (the WI2 diff line). Promote `_precompiled` out of `#if DEBUG` (remove the `#if DEBUG`/`#endif` around both the field ≈72-76 and the `_precompiled = true;` assignment ≈86-88; update its comment to note it now also gates the Release opt-in guard). Replace the guard (≈209-218) with:

     ```csharp
     var validateModelType = _validateModelType;
 #if DEBUG
     validateModelType = true; // DEBUG always validates (historical guard); Release honors the opt-in
 #endif
     if (validateModelType && !_precompiled && data != null && !_context.ScopeType.Type.IsType(data))
     {
         throw new TemplateProcessingException
             (string.Format
                 (CultureInfo.InvariantCulture, "Type mismatch. Need {0} but got {1}",
                     _context.ScopeType.Type?.FullName ?? _context.ScopeType.ToString(),
                     data.GetType().FullName));
     }
     ```

     `_validateModelType` is read (assigned to the local) in all configurations, so no `CS0414` in `DEBUG`. `!_precompiled` keeps the precompiled adapter skipped in both configs.
- **Done when.** The [guard tests](#testing-plan) pass (opt-in on → `TemplateProcessingException` with the "Type mismatch. Need X but got Y" shape; opt-in off in Release → no type-mismatch throw; DEBUG-always-on preserved); the equality-exclusion test passes; `TemplateOptionsCompletenessTests` stays green (auto-covers the copy); full suite green, goldens byte-identical.

## Public API contract

**New — [`TemplateOptions.ValidateModelType`](../../../src/Heddle/Data/TemplateOptions.cs)** (WI3):

```csharp
/// <summary>
/// <para>When <c>true</c>, a render (<c>Generate</c>) validates the supplied <c>data</c> against the
/// template's compiled model type and throws <see cref="Heddle.Exceptions.TemplateProcessingException"/>
/// on a mismatch — the same guard that fires unconditionally in <c>DEBUG</c> builds, now available in
/// Release. Default: <c>false</c> (Release behavior is unchanged — wrong-typed data is not validated).</para>
/// <para>The check is skipped for precompiled-adapter templates (a precompiled root carries no
/// compile-time model type to check against) — a known limit mirrored from the <c>DEBUG</c> guard.
/// A <c>null</c> model is always legal and never validated. In <c>DEBUG</c> the guard fires regardless
/// of this flag.</para>
/// <para>Copied by the copy constructor. Deliberately absent from <see cref="Equals(TemplateOptions)"/>/
/// <see cref="GetHashCode"/> and any template/precompiled cache key (precedent
/// <see cref="RenderBudget"/>, <see cref="MaxRecursionCount"/>): it changes failure handling, never the
/// bytes of a successful render, so it must not fragment caches.</para>
/// </summary>
public bool ValidateModelType { get; set; }
```

- **Semantics.** Resolved once at compile onto the template; read on the render path only. When active and `data != null` and `data`'s runtime type is not assignable to the compiled `ScopeType`, `Generate` throws `TemplateProcessingException("Type mismatch. Need {compiledType} but got {actualType}")` before rendering. Off (default) = today's Release behavior, byte-identical.
- **Thread-safety.** `TemplateOptions` is a caller-owned configuration object read at compile time; the resolved `_validateModelType` field is written once during compile and read (never written) on concurrent renders — safe. No per-render state added.

**Behavioral contract change (no signature change) — `HeddleTemplate.Dispose()` / `Render` bookkeeping** (WI1): `Dispose()` is now safe to call concurrently with active renders — it never disposes the runtime document while a render executes, is idempotent (including the internal deferred re-entry and the finalizer), and remains non-blocking (it does not wait for renders to drain; a render that has not yet started after `Dispose()` throws `ObjectDisposedException`, as today). `Render` remains `internal`.

**Strengthened contract (no signature change) — `HeddleTemplate.TryCompilation(...)`** (WI2): a `Success == true` result now predicts that a real `Compile` of the same source + context succeeds, including `FullCSharp` Roslyn and delayed-subtemplate finalization. The dry run is **side-effect-free on the caller's `CompileContext`**: `TryCompilation(CompileContext)` runs the probe on a fresh reconstructed context (mirroring the caller's `Options`, `RootScopeType`, current `ScopeType`, `OutputProfile`, and `ControllerName`, so a caller that mutated `ScopeType`/`OutputProfile` after construction is still predicted faithfully), so the passed instance keeps `Compiled == false`, its `DelayedTemplates`, and an unpolluted `CompileErrors` list, and remains usable for a real `Compile` that finalizes from scratch. A `FullCSharp` dry run permanently loads one emitted assembly, and a later real compile of the same source loads its own (distinct `ClassGuid` ⇒ no cache reuse) — the documented P4-Q1 cost. Both overloads (`TryCompilation(CompileContext)` and `TryCompilation(string, TemplateOptions, ExType)`) are unchanged in signature.

## Diagnostics

**n/a — no new compile diagnostic; no `HED*` id claimed this phase.** The only new failure surface is the Release model-type guard (D3), which throws the existing `TemplateProcessingException` (a **host-programming/render-path exception**, matching the DEBUG guard), not a positioned `HeddleCompileError`/`HeddleCompileWarning` on a compile result. Per [D1](../common/cross-cutting-decisions.md#d1--stable-diagnostic-ids-hedxxxx) the diagnostic-ID scheme covers compile diagnostics; a wrong runtime model type is a caller bug surfaced as an exception ([coding standards — host-programming errors throw](../common/coding-standards.md#error-handling-and-diagnostics)). WI2 surfaces **pre-existing** compile diagnostics (Roslyn `CS####` mapped by `FormatErrors`, and finalization errors) earlier on `TryCompilation`; it claims no new id. The [claimed-ID registry](../common/cross-cutting-decisions.md#claimed-diagnostic-ids-registry) is **not** modified by this phase.

## Testing plan

**TDD verdict.** Test-first (deterministic) for WI2 parity, WI3 guard on/off, and the equality-exclusion test — each is written failing before its fix. Test-with (written first, used as the regression witness, but inherently probabilistic) for the WI1 dispose race: a high-iteration stress harness modeled on [`BranchConcurrencyTests`](../../../src/Heddle.Tests/BranchConcurrencyTests.cs) — a green run is corroboration, not proof, so the correctness argument in [D1](#d1--disposal-synchronization-interlocked-runner-count--cas-guarded-single-teardown-closes-p4-q3-sd-a) carries the guarantee. New tests pass on **all** test TFMs (`net6.0;net8.0;net10.0` + `net48` on Windows).

New xUnit fixtures under [`src/Heddle.Tests`](../../../src/Heddle.Tests):

- **`HeddleTemplateDisposalTests.cs`** (WI1):
  - `ConcurrentDisposeDuringActiveRendersNeverTearsOrDisposesUnderRender` — `Parallel.For` (≥ 64 degree, many outer iterations) of `Generate(model)` on one compiled instance while one task calls `Dispose()` at a random point; assert no render that had begun observes a disposed document or corrupted/truncated output, and that any `ObjectDisposedException` only comes from renders that had **not** begun. Repeat over fresh instances to exercise the interleaving.
  - `DisposeIsIdempotent` — `Dispose()` twice on an idle instance disposes once (a counting/observing `RuntimeDocument`-owning fixture, or a watcher-dispose count) and neither call throws.
  - `DeferredDisposeRunsExactlyOnceAfterLastRender` — start N renders on a template whose strategy blocks on a gate, call `Dispose()` (must not block, must not tear down yet), release the gate; assert teardown happens exactly once after the last render returns and no in-flight render threw.
  - `ConcurrentRendersWithoutDisposeLeaveCounterBalanced` — high-iteration `Parallel.For` of `Generate` with no dispose; assert every render succeeds and (via a test hook or by a subsequent clean `Dispose`+re-check) the runner count returns to zero with no observed disposed document.
- **`TryCompilationParityTests.cs`** (WI2):
  - `TryCompilationSurfacesFullCSharpRoslynError` — a `FullCSharp` template whose embedded C# references an undefined symbol: `TryCompilation(...).Success == false` with the Roslyn error(s) in `ErrorList`, matching what a real `Compile` reports.
  - `TryCompilationSurfacesDelayedSubtemplateFinalizationError` — a template with a delayed subtemplate that fails `CompleteInit`: `Success == false` with the finalization error(s).
  - `TryCompilationSuccessPredictsCompileSuccess` — a valid `FullCSharp` template: `TryCompilation` succeeds **and** a fresh `Compile` of the same source + context also succeeds (no green-dry-run-then-red-compile pair).
  - `TryCompilationDoesNotMutateCallerContext` — build one `CompileContext` with a delayed subtemplate (or `FullCSharp` methods); call `TryCompilation(ctx)` (green), then `new HeddleTemplate(ctx).Generate(model)` (a **real** compile of the same instance) and assert it renders the fully-finalized output — i.e. `CompleteInit`/Roslyn ran on the real compile. Also assert `ctx.Compiled == false` and `ctx.CompileErrors` empty immediately after the green dry run. Fails on today's shared-context wiring (real compile skips `CompleteInit`).
  - `TryCompilationMirrorsMutatedScopeTypeAndProfile` — build a `CompileContext`, then mutate `ctx.ScopeType` (so it diverges from `RootScopeType`) and/or `ctx.OutputProfile` before the dry run; assert `TryCompilation(ctx)`'s result matches a real `Compile` of the same mutated context (both green, or the same errors) — the probe reconstructs the caller's current `ScopeType`/`OutputProfile`, not only the root state.
  - `TryCompilationOnThrowingFinalizationLeaksNoArtifact` — a `FullCSharp` template constructed so `compileScope.Compile()` throws (e.g. force `InitErrors` unavailable, or a delayed subtemplate whose `CompleteInit` throws): `TryCompilation` returns `Success == false`, and a disposal witness (a counting `RuntimeDocument`/`CompileScope`, as in the WI1 idempotency fixture) confirms the probe artifact was disposed (the `finally` ran).
- **Model-type guard tests** (WI3), in `HeddleTemplateModelTypeGuardTests.cs`:
  - `OptInValidateModelTypeThrowsOnMismatch` — `new TemplateOptions { ValidateModelType = true }`, a typed template (e.g. model `Flag`, static body), `Generate(new object())` throws `TemplateProcessingException` whose message starts `"Type mismatch. Need "` and matches the DEBUG shape. Valid in both configs.
  - `MismatchWithoutOptInIsNotValidatedInRelease` — wrapped in `#if !DEBUG`: `ValidateModelType = false`, same wrong-typed `Generate` does **not** throw the type-mismatch (a static-body typed template renders its text). (In `DEBUG` the guard fires regardless — asserted by the next test.)
  - `DebugGuardFiresRegardlessOfOptIn` — wrapped in `#if DEBUG`: `ValidateModelType = false`, wrong-typed `Generate` still throws (documents the preserved unconditional DEBUG behavior).
  - `PrecompiledPathIsNotValidatedEvenWhenOptedIn` — a precompiled-adapter instance with `ValidateModelType` requested renders wrong-typed data without the guard throwing (documents the known limit).
- **Equality exclusion** (WI3), in `TemplateOptionsValidateModelTypeTests.cs`:
  - `EqualityAndHashIgnoreValidateModelType` — two `TemplateOptions` differing only by `ValidateModelType` compare `Equals == true` and have equal `GetHashCode()`. (`TemplateOptionsCompletenessTests` already covers the copy-ctor round-trip for free.)

Fixtures for the `FullCSharp`/delayed-subtemplate cases follow the `.heddle` sibling-pair convention under [`TestTemplate`](../../../src/Heddle.Tests/TestTemplate) only where a file is clearer than an inline string; inline template strings (the `HeddleTemplateTests` style) are acceptable for the small negative cases. Tests assert with context (`Assert.True(result.Success, result.ToString())`) and carry an XML-doc comment naming the pinned scenario ([testing standards](../common/testing-standards.md#test-authoring-conventions)).

**Regression gate** ([testing standards](../common/testing-standards.md#regression-gates)), one combined run:
1. `dotnet build -c Release` — whole solution, all TFMs.
2. `dotnet test src/Heddle.Tests` — full suite, all TFMs, zero failures; goldens byte-identical (this phase renders nothing new — no golden changes).
3. Grammar-stability: `src/Heddle.Language/generated/` has no diff (this phase declares no grammar change).
4. **Benchmarks (hot path touched by WI1):** run [`TextRenderBenchmarks.RenderHeddle`](../../../src/Heddle.Performance/TextRenderBenchmarks.cs) and [`SinkRenderBenchmarks`](../../../src/Heddle.Performance/SinkRenderBenchmarks.cs) before/after on the same machine; acceptance: **allocated bytes unchanged**, mean time not regressed beyond BenchmarkDotNet's reported error.
5. No docs build required for the code change itself; the downstream `csharp-api.md` doc edits (Disposal note, `TryCompilation` caveat, `Generate` DEBUG-only note → opt-in) are the plan's noted downstream work, run `docs:build` if taken in the same change.

No samples-gallery item: the three changes are correctness/robustness with no new author-facing template surface (the opt-in is a host-code option); [testing standards](../common/testing-standards.md#the-canonical-loop) require a gallery item only for user-visible template changes.

## Back-compat and migration

Governed by [R5](../../plan/common/standing-rulings.md#standing-rulings-user-set) (additive/pure-correctness; no rendered-byte change to a valid template; no flipped default). **No breaking-window entry** — nothing here is a breaking change, so [breaking-windows.md](../common/breaking-windows.md) is untouched.

- **Disposal (WI1):** pure correctness. Single-threaded and concurrent-render-without-dispose callers see identical behavior (proven by the existing suite staying green + `ConcurrentRendersWithoutDisposeLeaveCounterBalanced`). The only behavioral change is at the previously undefined/racy case (dispose during active render), which becomes safe. `Dispose` stays idempotent and non-blocking. **Proven by:** byte-identical goldens (no render output change) + the disposal tests.
- **`TryCompilation` (WI2):** the *success* contract tightens — templates that returned a misleading success and then failed `Compile` now surface those errors in the dry run. This reports pre-existing breakage earlier; it **cannot** make a genuinely-compilable template fail (it runs only passes a real compile already runs). The dry run no longer mutates the caller's `CompileContext`, so callers that reuse a context after a dry run (today an undefined-but-observable footgun: skipped finalization / leaked errors) now get a clean real compile — a strict improvement with no signature change. No rendered output exists to change. **Proven by:** `TryCompilationSuccessPredictsCompileSuccess`, `TryCompilationDoesNotMutateCallerContext`, `TryCompilationOnThrowingFinalizationLeaksNoArtifact` + the existing `TryCompilation` tests staying green.
- **Release model-type guard (WI3):** default-off ⇒ Release behavior byte-identical to today for both correct and wrong-typed data. Only a host that explicitly sets `ValidateModelType = true` observes the new throw. The flag is **excluded** from `Equals`/`GetHashCode`, so it cannot perturb the template cache key. **Proven by:** `EqualityAndHashIgnoreValidateModelType`, `MismatchWithoutOptInIsNotValidatedInRelease` (Release), `TemplateOptionsCompletenessTests` (copy round-trip), and byte-identical goldens.

## Performance considerations

- **Hot path touched:** `HeddleTemplate.Render` enter/exit bookkeeping (WI1). Per whole render call it now executes a few `int` atomics (`Interlocked.Increment` + two `Volatile.Read` on enter; `Interlocked.Decrement` + a conditional `Volatile.Read` on exit) in place of the non-atomic `_runners++`/`_runners--` and one `volatile bool` read. This is **per render call, not per character/element**, and adds **zero heap allocation** (no lock object, no wait handle). The `Scope` construction and `_processStrategy.Render` core are unchanged.
- **Guarding benchmarks:** `TextRenderBenchmarks.RenderHeddle` (string path) and `SinkRenderBenchmarks.RenderString`/`RenderTextWriter`/`RenderUtf8Buffer` (sink paths) — all route through `Render`. Acceptance per the [regression gate](#testing-plan): allocated bytes unchanged; mean time within BenchmarkDotNet's reported error.
- **WI2 (compile-path):** a `FullCSharp` `TryCompilation` now pays Roslyn parse/emit **and permanently loads one emitted assembly** — a compile-path cost ("compile once"), not a render cost; acceptable per [coding standards](../common/coding-standards.md#performance-rules-the-render-path-is-hot). It is **not** amortized by the assembly cache against a later real compile: `ContextCompilation.Cache` is keyed by generated code text, which embeds the per-`CSharpContext` `ClassGuid` (`Guid.NewGuid()`), so the isolated probe and the real compile carry distinct guids and never share a cache entry — each emits + loads its own assembly, and neither is ever unloaded (`CompileContext.Dispose` no-op TODO). This is the accepted, documented cost of full parity (deferred item: [assembly unloading](#deferred-items)). No render-path or startup-precompilation benchmark is affected.
- **WI3:** off (default) adds nothing to the hot path; on, the guard is one bool + one `IsInstanceOfType` per render call before rendering, only for opted-in hosts.

## Standards compliance

- **YAGNI / simplicity (precedence rule 3).** The disposal fix uses the smallest primitive that meets the success criteria — three `int` atomics and two helpers — rather than a lock, drain, or a new synchronization abstraction. The model-type opt-in is a single `bool`, not a new interface or exception type.
- **Measured performance (precedence rule 4) beats the "obvious" lock.** A `Monitor`/`ReaderWriterLockSlim` would be simpler to reason about but serializes concurrent renders and/or allocates; the lock-free design is justified by the hot-path benchmarks that protect Heddle's throughput story (D1 alternatives-rejected).
- **DRY at the right seam.** WI2 makes the dry run reuse the *same* `compileScope.Compile()` pass the real compile runs — one representation of "what finalization+Roslyn means" — rather than a parallel partial check. WI1's `Teardown` consolidates the teardown body that was duplicated between `Dispose` and the finalizer into one CAS-guarded place.
- **Additive-by-default API.** `ValidateModelType` is a new opt-in property defaulting to current behavior, added to the copy ctor and excluded from identity — the exact `TemplateOptions` completeness discipline ([coding standards](../common/coding-standards.md#api-design-and-compatibility)), auto-guarded by the reflection completeness test.
- **Thread-safety contract stated.** No mutable per-render state is added to any shared instance; `_validateModelType` is compile-time-written/render-time-read, and the disposal state is manipulated only through atomics.

## Deferred items

| Item | Trigger |
|---|---|
| Blocking/draining `Dispose` (wait for in-flight renders) | A concrete need for a "dispose blocks until quiescent" contract; explicitly a non-goal now (P4-Q3). |
| Validating the model type on the **precompiled** path | A precompiled adapter that carries a checkable compile-time model type (would need the precompiled surface, [D8](../common/cross-cutting-decisions.md#d8--template-identity--naming-policy-has-one-owner), to thread one through); documented known limit today. |
| `ValidateModelType` default-on (or an equivalent stricter default) | A maintainer decision under the R5 2.0 window accepting the hot-path cost and Release behavior change (P4-Q2 notes it is available but not taken). |
| Explicit-partial / queryable `TryCompilation` contract | Only if the full-parity Roslyn cost proves unacceptable for CI use in practice (the rejected P4-Q1 alternative); revisit trigger = a measured CI-time complaint. |
| Unloading assemblies emitted during a dry run | `AssemblyLoadContext` unload support wired into `CompileContext.Dispose` (currently a TODO no-op) — process-wide concern beyond this phase. |
| Phase 1's per-superseded-document release | Owned by [Phase 1](../../plan/phase-1-file-based-api-correctness.md); it consumes this phase's disposal primitive (SD-A). |

## External references

- [`HeddleTemplate.cs`](../../../src/Heddle/HeddleTemplate.cs), [`ContextCompilation.cs`](../../../src/Heddle/Runtime/ContextCompilation.cs), [`TemplateOptions.cs`](../../../src/Heddle/Data/TemplateOptions.cs), [`ExType.cs`](../../../src/Heddle/Data/ExType.cs), [`TypeExtension.cs`](../../../src/Heddle/Helpers/TypeExtension.cs), [`RuntimeDocument.cs`](../../../src/Heddle/Runtime/RuntimeDocument.cs), [`TemplateProcessingException.cs`](../../../src/Heddle/Exceptions/TemplateProcessingException.cs) — the seams this spec verified and edits.
- [`TemplateOptionsCompletenessTests.cs`](../../../src/Heddle.Tests/TemplateOptionsCompletenessTests.cs), [`BranchConcurrencyTests.cs`](../../../src/Heddle.Tests/BranchConcurrencyTests.cs), [`HeddleTemplateTests.cs`](../../../src/Heddle.Tests/HeddleTemplateTests.cs) — test models this spec extends.
- [`TextRenderBenchmarks.cs`](../../../src/Heddle.Performance/TextRenderBenchmarks.cs), [`SinkRenderBenchmarks.cs`](../../../src/Heddle.Performance/SinkRenderBenchmarks.cs) — the guarding benchmarks.
- [System.Threading.Interlocked](https://learn.microsoft.com/en-us/dotnet/api/system.threading.interlocked) and [System.Threading.Volatile](https://learn.microsoft.com/en-us/dotnet/api/system.threading.volatile) — `netstandard2.0` atomics used by WI1.
- [.NET library breaking-change rules](https://learn.microsoft.com/en-us/dotnet/standard/library-guidance/breaking-changes), [.NET Framework Design Guidelines](https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/) — the additive-API discipline WI3 follows.
- Plan & governance: [phase-4-engine-robustness.md](../../plan/phase-4-engine-robustness.md), [standing-rulings.md](../../plan/common/standing-rulings.md), [open-questions.md](../../plan/open-questions.md), [coding-standards.md](../common/coding-standards.md), [testing-standards.md](../common/testing-standards.md), [cross-cutting-decisions.md](../common/cross-cutting-decisions.md).
