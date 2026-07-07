# Phase 9 exit — the roadmap-wide combined end-to-end run (D15)

This is the roadmap's closing artifact: the phase-by-phase traceability table (each phase's success-criteria
surface mapped to the tests/samples that prove it end-to-end) plus the combined-run results. Regenerate the sample
half with `for d in samples/*/; do dotnet run --project "$d" -c Release -- --capture out && bash samples/tools/compare-golden.sh "$d"; done`.

## Phase-by-phase traceability

| Roadmap phase | Success-criteria surface re-verified | Re-verified by | Result |
| --- | --- | --- | --- |
| 1 — native expressions | Sandbox boundary (rejected constructs never execute, positioned diagnostics); native-tier render; the Roslyn-free common path | `samples/sandboxed-user-templates` (rendered/* + `rejected.txt` with HED1001/HED1003; unfired canary) + the demo render with the D9 purity gate proving **no Roslyn in the host** | PASS |
| 2 — safe output | Html profile encodes the unnamed output; `@raw` opt-out; the 2.0 default flip stays rehearsed | `samples/html-safe-output` (Text vs Html vs custom-encoder goldens) + the demo compiling under `OutputProfile.Html` | PASS |
| 3 — branching | `@if`/`@elif`/`@else` semantics; the public channel + branch protocol as extension API | `samples/custom-extensions` (a third-party publisher/reader + branch participant on the public channel) | PASS |
| 4 — ergonomics | `@for` sugar and `TrimDirectiveLines` in real templates | `samples/ssr-aspnetcore` (@for star ratings + TrimDirectiveLines) and `samples/definition-library` goldens | PASS |
| 5 — props & slots | Typed props (defaults/required/override), parameterized slots, composition | `samples/component-props-slots` goldens + demo completion fixture C04 (`complete-c04-props`) | PASS |
| 6 — tooling / LSP | Facade correctness (completion/hover/diagnostics as engine projections); cross-host stability | `DemoContractTests` (C01/C02/C04/C09 + diagnostic + render fixtures on CoreCLR) + the demo typed layer (S2/S3) + the `editors/vscode` walkthrough row | PASS (facade half); S2/S3 CI-only |
| 7 — build-time compilation | Precompiled == dynamic (differential), discovery enumeration, codegen | `samples/precompiled-app` (in-capture differential + `discovery.txt`) + `samples/codegen-t4-successor` (generated-source golden + build-time-only generator) | PASS |
| 8 — streaming & async | Byte-sink parity with string rendering; the `Response.BodyWriter` pattern | `samples/streaming-ssr` (in-capture byte/string/TextWriter parity + response-body goldens) | PASS |

## Combined-run results (this sandbox)

| Gate | Result |
| --- | --- |
| `dotnet build -c Release Heddle.sln` (netstandard2.0;net6.0;net8.0;net10.0) + WASM project | 0 errors |
| `Heddle.Tests` (net6.0/net8.0/net10.0) | 662/662 (657 baseline + 5 `FeatureSwitchTests`) |
| `Heddle.Tests` (net48) | 662/662 (run alone to avoid the known 4-TFM contention flake) |
| `Heddle.LanguageServices.Tests` (net10.0) | 45/45 (39 baseline + 6 `DemoContractTests`) |
| `Heddle.Generator.IntegrationTests` / `Heddle.Generator.Tests` / `Heddle.Tool.Tests` | unchanged (no engine-behavior change on their paths) |
| Ten sample gallery jobs (`--capture` + golden) | 10/10 byte-identical; comparer self-test green |
| Phase-7 differential (`precompiled-app`) + phase-8 parity (`streaming-ssr`) in-capture | green |
| WASM publish: Roslyn purity (`Microsoft.CodeAnalysis*` absent) | PASS — 0 assets |
| WASM publish: bundle size vs 12 MB budget | 8.75 MB raw (staged, incl. .br/.gz) |
| ANTLR `src/Heddle.Language/generated/` diff | none (no grammar change) |
| Existing engine goldens byte-identical | yes (`PublicApiSurfaceTests` unchanged — HED9001 is internal) |

## CI-only steps (cannot run headless in this sandbox)

| Step | Why CI-only | State |
| --- | --- | --- |
| Playwright S1–S5 smoke suite | No chromium here; and `docs:build` (its `webServer`) is separately blocked by the pending D9 spec-page `srcExclude` (`gallery.md` "missing end tag") | Fully scaffolded: `docs/playwright.config.ts`, `docs/demo-smoke/demo.spec.ts`, `demo:smoke` script, chromium install + gate wired in `docs.yml` |
| Seeded-failure "test the tests" runs | Procedural, PR-recorded per harness change | Comparer self-test (WI1) proves the comparer can fail; the sample/golden/bundle break procedures are documented in `samples/README.md` and the docs-workflow gate |

## Roadmap success criteria & validation scenarios

| Criterion / scenario | Verified by | Result |
| --- | --- | --- |
| 1 — typed completion/hover + real compile diagnostics from static hosting, fallback intact | `DemoContractTests` (facade) + demo.html typed layer + S1 fallback | PASS (browser S1–S3 CI-only) |
| 2 — live render in browser, C# tier declined gracefully | demo render pane + S4/S5; `FeatureSwitchTests` (HED9001) | PASS (browser S4/S5 CI-only) |
| 3 — the three current-engine samples green on every PR | `ssr-aspnetcore`, `definition-library`, `dynamic-models` jobs | PASS |
| 4 — smoke test fails on a seeded regression | comparer self-test + the documented bundle-break procedure | PASS (procedure); browser run CI-only |
| 5 — later phases inherit the harness with no new infra | rows 4–10 added by folder + matrix entry only | PASS (structural) |
| 6 — newcomer: `git clone` + `dotnet run`, no undocumented steps | each sample README (README-first) | PASS |
| Demo, WASM blocked → syntax squiggles work | S1 | scaffolded (CI-only) |
| Demo, `@(` inside `@list(Articles)` → Article members | S3 / `complete-c01` | PASS (facade); browser CI-only |
| Demo, member typo → compile diagnostic | S2 / `diagnostic-member-typo` | PASS (facade); browser CI-only |
| Demo + phase 1, valid template → rendered output | S4 / `render-blog` | PASS (facade); browser CI-only |
| Demo + phase 1, C#-tier template → friendly decline, no crash | S5 + `FeatureSwitchTests` | PASS (facade/engine); browser CI-only |
| Sample CI: composed page / override / dynamic == golden | jobs 1–3 goldens | PASS |
| Seeded break in one sample → that job fails, others pass | `fail-fast: false` matrix + comparer | PASS (structural) |
