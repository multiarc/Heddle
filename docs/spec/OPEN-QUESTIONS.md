# Open-questions register

The single sanctioned home for **maintainer-level** unresolved items (per the
[conventions](common/spec-conventions.md#no-open-questions)). Phase specs themselves
contain no open questions: every entry here carries a **provisional default that the
specs implement**, so no work blocks on this register. Resolving an entry — ratifying
the default or overriding it — lands as a
[ledger](common/cross-cutting-decisions.md#cross-spec-amendments-ledger) entry, and the
row moves to the *Resolved* section below.

Origin note: the July 2026 cross-phase gap analysis surfaced seven gaps (G1–G7). Five
were resolvable from already-ratified direction and are closed directly in the owning
specs; the two that introduced new product-scope surface were registered here and have
since been **resolved by the maintainer (July 2026)**.

## Open

*(none)*

## Resolved

### OQ1 — Scope of function support in precompiled templates (from gap G1) — RESOLVED July 2026

- **Question.** Phase 1's `FunctionRegistry` accepts host registrations that exist only
  after host startup code runs — which the phase 7 source generator never does. What
  function surface do precompiled templates support in v1?
- **Maintainer resolution (overrides the provisional default).** Build-time binding is
  an **integration problem**, solved the same way the runtime host solves it: the
  consuming project references the assemblies carrying its external extensions and
  functions, the generator **searches those references first, then compiles, binding
  directly to the discovered types** in the generated compilation. Concretely:
  - Discovery runs over the compilation's metadata references (extensions via the
    existing assembly-declarative markers; functions via the declarative function
    export introduced for the one-shot scan — see OQ2's resolution), before emission.
  - Generated code **binds directly** to the discovered public static methods and
    extension types — no registry dispatch for anything discoverable at build time;
    built-ins bind through the public shim (they are `internal` in `Heddle.dll`).
  - Build/runtime parity is the **integration's responsibility**: the build must see
    the assemblies the host registers from. The manifest function/extension bindings
    and the registration gauntlet remain the safety net that turns a parity violation
    into a detected `Fallback`/`Strict` divergence, never silence.
  - Only registrations that are *not representable in an assembly* (opaque runtime
    delegates) remain un-precompilable: `HED7014` + runtime fallback, unchanged.
- **Implemented by.** Phase 7 D21 (revised to the direct-binding model) with the
  discovery/binding work items; the declarative function export lives with phase 6's
  scan machinery (sequential order: 6 ships before 7).
- **Ledger.** See the OQ1 rows in the
  [amendments ledger](common/cross-cutting-decisions.md#cross-spec-amendments-ledger).

### OQ2 — How the LSP workspace learns about host registrations (from gap G4) — RESOLVED July 2026

- **Question.** The LSP server process never runs the host's startup code, so
  host-registered functions and runtime-registered extensions are invisible to it. What
  channel closes that gap in v1?
- **Maintainer resolution (ratifies the scan, replaces the config list).** Two
  knowledge sources only:
  1. the **one-shot assembly scan** of the workspace's configured assemblies at load
     (ratified as provisionally specified: default ALC, no rescan — restart to pick up
     changes), which also yields **declaratively exported functions** (the same
     assembly-declarative model OQ1's resolution binds against, keeping editor
     knowledge and precompilability the same set); and
  2. **declarations within the template itself** — definitions declare names
     first-class, exactly as the phase 1 resolution order (definition → extension →
     function) already reads them.
  The provisional `functions` workspace-configuration array is **dropped** — no
  hand-declared signature lists. Purely runtime (delegate-only) registrations remain
  invisible to the editor and are documented as such.
  **Extension rescanning / dynamic extension development is explicitly a separate
  future effort**: how development looks when extensions are being developed and
  searched for dynamically is not designed here; it is recorded as a deferred item with
  that effort as its trigger.
- **Implemented by.** Phase 6 D23 (scan, ratified) and D24 (revised: declarative
  function export + template-definition knowledge replace the config list), plus the
  new deferred item for the dynamic-extension development workflow.
- **Ledger.** See the OQ2 rows in the
  [amendments ledger](common/cross-cutting-decisions.md#cross-spec-amendments-ledger).
