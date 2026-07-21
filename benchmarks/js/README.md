# Heddle cross-stack benchmarks — JS/Node harness

Phase 4 harness of the cross-stack benchmark program. The normative specification is
[docs/spec/cross-stack-benchmarks/phase-4-js/](../../docs/spec/cross-stack-benchmarks/phase-4-js/README.md)
(with [templates-and-models.md](../../docs/spec/cross-stack-benchmarks/phase-4-js/templates-and-models.md)
and [harness-and-run.md](../../docs/spec/cross-stack-benchmarks/phase-4-js/harness-and-run.md));
the parity contract and golden corpus it implements are Phase 1's
[parity-contract-v2.md](../../docs/spec/cross-stack-benchmarks/phase-1-cross-stack-foundation/parity-contract-v2.md)
and [golden-corpus.md](../../docs/spec/cross-stack-benchmarks/phase-1-cross-stack-foundation/golden-corpus.md).
The golden corpus is consumed read-only from `src/Heddle.Performance/GoldenCorpus/`.

## Pins

- Node **24.18.0** (exact; `engines` + `engine-strict=true` — any other Node version is refused)
- handlebars **4.7.9**, eta **4.6.0**, mitata **1.0.34** (exact pins, committed lockfile)
- The only sanctioned install command is `npm ci`.

## Reproduce

```
cd benchmarks/js
npm ci
npm run gate                        # both gates, all 32 cells
./run.ps1 bench/controlled.mjs     # published runs go through the launcher (High priority)
./run.ps1 bench/idiomatic.mjs
./run.ps1 bench/cold-compile.mjs
./run.ps1 bench/controlled.mjs -Repeat 5   # D13 Windows stability verification
```

Development checks: `npm run selftest` (normalization fixtures + verifier calibration re-run),
`node test/gate-selftest.mjs` directly.
