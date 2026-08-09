---
paths:
  - "src/*Tests*/**"
  - "src/TestCorpus/**"
  - "samples/**"
---

# Testing

Details: [testing-standards.md](../../docs/spec/common/testing-standards.md) — normative; this is the short form.

- Loop: write the spec's tests failing first → implement to green → run the gate → fix forward. Goldens/baselines/spec tables change only with maintainer review, never to silence a red test.
- Gate (one combined run): `dotnet build -c Release` → `dotnet test --project src/Heddle.Tests/Heddle.Tests.csproj` (all TFMs, serially; both Debug and Release when `#if DEBUG` arms are touched) → no diff under `src/Heddle.Language/generated/` (unless the spec licenses a grammar change) → benchmarks when a hot path is touched → `npm run docs:build` when docs pages changed.
- xUnit v3 on MTP: `dotnet test` needs `--project`/`--solution` (directory form is rejected); filters are MTP syntax after `--` (`--filter-method` etc.); a filter matching nothing exits 8; CI legs run through `.github/scripts/dotnet-test-guarded.sh <project>`, which passes `--fail-skips on`.
- **No test-count floors.** `--minimum-expected-tests` was tried on nine CI legs and withdrawn: it is satisfied by editing a digit, it names nothing when it reddens, it drifted (one commit raised `dotnet.yml` and left `lsp.yml`, and that leg then tolerated a six-test regression for four commits), and it fails a leg for the wrong reason whenever a test is legitimately quarantined. What a suite CONTAINS is gated inside the suite: `src/<Suite>/test-classes.txt`, one line per fact-declaring class, set-equality asserted by that suite's `TestClassInventoryTests` — a red gate names the class. Deliberately the same shape as corpus membership below.
- Fixtures: `.heddle` + sibling golden under `src/Heddle.Tests/TestTemplate`; every new golden/fixture directory is LF-pinned in `.gitattributes` **in the same change**.
- Negative/security tests assert positioned `HED*` diagnostics (ID + position) and prove the construct never executes.
- Descriptive PascalCase sentence test names; assert with context (`Assert.True(result.Success, result.ToString())`); tests are exempt from DRY.
- Concurrency: any state shareable across renders ships a parallel-render isolation test.
- Precompiled tier: fallback to dynamic is byte-identical, so an unpinned test proves nothing about the tier it claims to test. End-to-end tests pin the tier (`PrecompiledMismatchPolicy.Strict` + fallback sentinel); expected fallbacks are declared only via `FallbackGuard.Expect` / `DifferentialHarness.ExpectDegrade`; quarantine guarded fixtures with an owning-item `Explicit = true` (reported *not run*, so it survives `--fail-skips`), never weaken them.
- Test-input single-sourcing: a template shape verified by more than one tier exists once, in the shared corpus, with declared (total) intent — duplicate copies drift silently and break the differential premise. Membership is gated by set equality — never a count or floor (a floor once hid 15 templates silently dropping out).
