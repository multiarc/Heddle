# Heddle cross-stack benchmarks — JS/Node harness

JS/Node harness of the cross-stack benchmark program. The normative contract documents are
[js-harness-and-run.md](../docs/js-harness-and-run.md) and
[js-templates-and-models.md](../docs/js-templates-and-models.md);
the parity contract and golden corpus they implement are
[parity-contract-v2.md](../docs/parity-contract-v2.md)
and [golden-corpus.md](../docs/golden-corpus.md), with the workload set defined in
[workloads.md](../docs/workloads.md).
The golden corpus is consumed read-only from `benchmarks/dotnet/GoldenCorpus/`.

## Pins

- Node **24.x** (major pin; `engines` + `engine-strict=true` — any non-24 Node is refused, and the run records the exact version it used)
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
./run.ps1 bench/controlled.mjs -Repeat 5   # Windows repeat-run stability verification
```

Development checks: `npm run selftest` (normalization fixtures + verifier calibration re-run),
`node test/gate-selftest.mjs` directly.
