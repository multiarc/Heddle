# Specification index

The master index of Heddle's specification material — every spec document is reachable
from here ([conventions → index maintenance](common/spec-conventions.md#index-maintenance)).
Specs are contributor material, **not published** to the docs site
([D9](common/cross-cutting-decisions.md#d9--spec-pages-stay-unpublished)).

## Common specs (initiative-independent)

| Document | Purpose |
| --- | --- |
| [spec-conventions.md](common/spec-conventions.md) | How specs are structured, written, and linked; document shapes; the no-open-questions discipline; the amendment mechanism. |
| [coding-standards.md](common/coding-standards.md) | Repository style, the comment rules (C1–C8), error/diagnostic rules, hot-path performance rules, API compatibility, balanced-mode SOLID/DRY/YAGNI. |
| [testing-standards.md](common/testing-standards.md) | The canonical spec→implement→gate loop, suite homes, fixture/golden conventions, regression gates, precompiled-tier posture, test-input single-sourcing, documentation currency. |
| [cross-cutting-decisions.md](common/cross-cutting-decisions.md) | Shared decisions `D1`–`D11`, the condensed release records and collapsed program records of the three completed initiatives, the claimed diagnostic-ID registry, and the (closed) amendments-ledger mechanism. |
| [breaking-windows.md](common/breaking-windows.md) | Breaking-change policy, the as-implemented record of the 3.0 window (pending the tag; it absorbs the 2.1 window, which was retired without a release), and the next-window candidate register. |
| [shared-source-architecture.md](common/shared-source-architecture.md) | What still governs after the 2.x generator's removal (reduced 2026-09-06): one language front end in `Heddle.dll` serving the engine, the `heddle compile` host and the language service; the diagnostic catalog and projection as single sources; no production project links `src/Heddle` source (`CompiledFormLinkedSourceTests`). |
| [review-protocol.md](common/review-protocol.md) | The mandatory reviewer protocol: exhaustive catalogue over first-found; inventories, class expansion, coverage ledger; findings land as tests. |
| [findings-register.md](common/findings-register.md) | Recurring mistake classes, code that looks wrong but is correct, open items, and the unverified platform surface. Code and tests never cite it. |

## Initiatives

| Initiative | Status | Entry document | Supplementary documents | Most consequential decisions |
| --- | --- | --- | --- | --- |
| Precompilation v2 — a serialized compiled form, not a second compiler (the v3 breaking window, closed with 3.0.0) | Implemented on the 3.0.0 line (records ratified 2026-09-17; as-shipped reconciliation at the `v3.0.0` tag). **Retired 2026-09-18** — collapsed into the [program record](common/cross-cutting-decisions.md#program-record--precompilation-v2-closed) | `docs/spec/precompilation-v2/README.md` (retired) | `artifact-contract.md`, `phase-1-compiled-form.md`, `phase-2-build-integration.md`, `phase-3-generated-sites.md`, `phase-4-removal-and-release-tail.md` (retired) | The engine compiles at build in the out-of-process `heddle compile` host and serializes its post-compile graph into one embedded artifact (`Heddle.CompiledForm`, schema 3) that the loader re-materializes into the engine's own objects; `Heddle.Build` replaces `Heddle.Generator`; bodiless calls the build cannot bind are late-bound data; generated code is limited to member accessors, native expressions and embedded C# by site id with a strict no-load-time-compilation mode; v3 rejects 2.x manifests and removes every `Heddle.dll` member that existed only for generated 2.x code. |

All three completed initiatives — the cross-stack benchmark program, the generator ↔ engine
code-sharing program, and precompilation v2 — are collapsed into
[cross-cutting-decisions.md](common/cross-cutting-decisions.md)'s program records; the
benchmark harnesses' operational contract lives in
[benchmarks/docs/](../../benchmarks/docs/README.md). Full retired documents:
`git show c4691266:docs/spec/` and `git show c4691266:docs/generator_plan/` for the first two;
`git show f8a9497c:docs/spec/precompilation-v2/` and `git show f8a9497c:docs/plan/precompilation-v2/` for precompilation v2.
