# Historical records

The append-only record of completed spec work. The [common specs](common/spec-conventions.md)
are instruction-only — anything that documents *finished* work (per-window as-shipped
records, the accumulated amendments ledger, registry corrections) lives here instead.
Entries are never edited or deleted; later facts are recorded as new entries.

## The 2.0 breaking window — as-shipped record

The first window under the [breaking-windows policy](common/breaking-windows.md).
**Window status: closed** — `v2.0.0` released 2026-07-19; contents reconciled against the
shipped source per policy rule 5. The migration note shipped as the release's
[CHANGELOG entry](../../CHANGELOG.md) (policy rule 4).

**Window contents as shipped in `v2.0.0`:**

| # | Change | 1.x state | As shipped in 2.0.0 |
| --- | --- | --- | --- |
| 1 | `OutputProfile` default flip | `Text` | ✅ `Html` — the unnamed `@(...)` encodes by default ([TemplateOptions.cs](../../src/Heddle/Data/TemplateOptions.cs) ctor) |
| 2 | Encoder pluggability | `WebUtility.HtmlEncode` at the pinned call sites; no seam | ✅ `TemplateOptions.Encoder` (`System.Text.Encodings.Web.TextEncoder` seam with span/UTF-8 paths, [HtmlEncodedRenderer.cs](../../src/Heddle/Data/HtmlEncodedRenderer.cs)); the **default (`null`) keeps the 1.x `WebUtility.HtmlEncode` bytes**. The byte-changing *default swap* to `HtmlEncoder.Create(UnicodeRanges.All)` is **not** in this window — deferred to the [next-window candidate register](common/breaking-windows.md#next-window-candidate-register) until the `Encoder` pin has soaked |
| 3 | `TrimDirectiveLines` default flip | `false` | ✅ `true` |
| 4 | `[Obsolete]` on `AllowCSharp` | Un-attributed bridge over `ExpressionMode` | ✅ `[Obsolete]`, behavior unchanged (compatibility bridge keeps working) |
| 5 | Generator MSBuild defaults follow the engine | `HeddleOutputProfile=Text`, `HeddleTrimDirectiveLines=false` | ✅ `Html` / `true` ([Heddle.Generator.props](../../src/Heddle.Generator/build/Heddle.Generator.props)) |
| 6 | Legacy `@import()` removal | Functional include | ✅ Removed — a call site fails with a single positioned `HED4003` **error** naming both replacements (`@<<{{ path }}` / `@partial(){{ name }}`); the `import` name is kept as a registered no-op tombstone so the diagnostic is a targeted migration signpost. See [language-reference.md](../language-reference.md#imports---) |
| 7 | `ForModel` → `Range` remodel | `Heddle.Models.ForModel` (mutable class, nullable members) | ✅ `Heddle.Models.ForModel` deleted; `range(...)`, `PrecompiledFunctions.Range`, and `@for` remodeled onto the immutable `Heddle.Models.Range` value type (`public readonly struct`). Only rendered-byte effect: range stringification now renders the readable call form (`range(2, 10, 2)`) instead of the type name |

**Also part of the window — precompiled assemblies from 1.x fall back under a 2.0
engine.** No compatibility shim: the engine-version gate rejects 1.x manifests; outcome
follows `PrecompiledMismatchPolicy` (`Fallback` recompiles with `HED7101`, `Strict`
throws). Documented in the [CHANGELOG](../../CHANGELOG.md) and
[precompilation.md](../precompilation.md). Rationale preserved: the gate exists so a
byte-changing window can never be masked by stale generated code.

**Explicitly excluded from the 2.0 window:** the default-encoder *swap* (item 2 — a
[next-window candidate](common/breaking-windows.md#next-window-candidate-register) gated
on the shipped `Encoder` pin soaking); `[NotEncode]` type deletion (needs its own
ratified window — a next-window candidate); `TypeForwardedTo` hygiene for
`Heddle.LanguageServices` (churn without benefit); async render APIs (sync-sinks decision
stands); any grammar change beyond the `@import` tombstone (everything else shipped
additively at the grammar level).

**Verification gate (as executed):** the standard regression gate plus
`samples/html-safe-output` running under both profile defaults, and the precompiled ==
dynamic differential against the 2.0 defaults proving engine and generator flipped
together.

## Diagnostic registry corrections

- **2026-07:** `HED3005` (branch continuation/terminal missing `[ScopeChannel]` drift
  warning) and `HED7016` (its build-time generator twin) shipped in 2.0.0 with the
  `[BranchRole]` work ([CHANGELOG](../../CHANGELOG.md)) but were never recorded in the
  [registry](common/cross-cutting-decisions.md#claimed-diagnostic-ids-registry); both
  verified in source and recorded now.

## Cross-spec amendments ledger

The accumulated, append-only entries of the
[ledger mechanism](common/cross-cutting-decisions.md#cross-spec-amendments-ledger).
Entries recorded after the `v2.0.0` release start here.

| Amendment | Made by | Amends |
| --- | --- | --- |
| **E1 (clarification requested; no Phase 1 text change).** Confirm that the mandated Heddle reference row may appear in the wall-time tables of *either* track (presentation rules 1–2 force it, since rule 2's ratio column must anchor to it) and that rule 5's "numbers from different tracks are never mixed" governs *engine result rows*, not the track-agnostic reference excerpt. | Phase 2 (rust) spec | metrics-protocol presentation rules 1–5 (clarification only — the *Amends* target is deliberately a request for a courtesy wording note; no normative rule is altered, so this cell names a clause to clarify, not a decision to overturn) |
| **E2 (stale example; non-normative).** Parity-contract-v2 N5 rule 3's parenthetical "Askama's fixed `&#x27;`" and the Phase 1 README's external-reference line describe the legacy `djc/askama` escaper; askama-rs 0.16.0 emits the all-decimal family (`&#34; &#38; &#39; &#60; &#62;`). Every 0.16.0 spelling is already in N5's recognized set, so **no normative change** — the examples should name the decimal family (or both, dated). | Phase 2 (rust) spec | parity-contract-v2 N5 rule 3 example + Phase 1 README external-reference line |
| **E3 (stale expectation; non-normative).** Phase 5 WI6's done-when phrase 'encoded-loop retained mean ≈ corpus byteLength' assumes 1-byte/char string storage. Measured evidence (implementation pass, 2026-07-21): CPython stores the BMP-Japanese-bearing output at 2 bytes/char (PEP 393), so the correct anchor is the string's in-memory size — retained mean matches `sys.getsizeof(out)` within 0.1% (1,523,428 B vs corpus byteLength 831,685 B, ≈1.83×). The tracemalloc measurement and D11 procedure are correct; the phrase should anchor to `sys.getsizeof`, not byteLength. | Phase 5 (python) implementation review | phase-5-python README WI6 done-when phrase (expectation text only — no normative rule altered) |
