# JS stability summary — `idiomatic`

Phase 4 D13 verdict computed from **38 consecutive passes** of
`bench/idiomatic.mjs`, each a separate node process. Thresholds are per cell on the
cross-pass RSD of mitata's `avg`: ≤ 5% verified; 5–10% publishable but the report must carry
this table; > 10% blocks publication.

STABILITY: verified

- passes: 38 (run-1.json … run-38.json)
- cells: 16
- cross-pass RSD: median 1.77% · max 4.77% (handlebars, cell 4)
- cells above 5%: 0 · above 10%: 0

| # | Engine | Published (median of passes) | Cross-pass min … max | RSD |
| ---: | --- | ---: | --- | ---: |
| 0 | handlebars | 5322.1 ns | 5270.5 … 5380.3 ns | 0.46% |
| 1 | eta | 4230.6 ns | 4195.1 … 4318.3 ns | 0.74% |
| 2 | handlebars | 629.5 ns | 609.9 … 650.8 ns | 1.38% |
| 3 | eta | 253.6 ns | 244.7 … 276.1 ns | 3.28% |
| 4 | handlebars | 355768.3 ns | 352404.6 … 441995.4 ns | 4.77% |
| 5 | eta | 355803.0 ns | 352378.3 … 361565.8 ns | 0.56% |
| 6 | handlebars | 6470.9 ns | 6267.9 … 7750.4 ns | 3.94% |
| 7 | eta | 4955.6 ns | 4922.5 … 5046.7 ns | 0.51% |
| 8 | handlebars | 98231.1 ns | 90994.1 … 110659.6 ns | 4.67% |
| 9 | eta | 15325.8 ns | 15211.5 … 15772.7 ns | 0.86% |
| 10 | handlebars | 5763.2 ns | 5679.5 … 6391.7 ns | 2.68% |
| 11 | eta | 6508.2 ns | 6428.4 … 6800.2 ns | 1.66% |
| 12 | handlebars | 2472.9 ns | 2443.4 … 2721.4 ns | 1.80% |
| 13 | eta | 1859.3 ns | 1811.8 … 1998.9 ns | 1.74% |
| 14 | handlebars | 4553071.2 ns | 4394283.4 … 4898513.6 ns | 2.09% |
| 15 | eta | 4448684.7 ns | 4297611.3 … 4747080.0 ns | 2.38% |

The published artifact `idiomatic.json` carries this aggregate: `stats.avg` is the median
of the per-pass averages and `stats.min`/`stats.max` span the passes, so its dispersion is
cross-process variation. Per-cell provenance is under `stats.aggregate`.
