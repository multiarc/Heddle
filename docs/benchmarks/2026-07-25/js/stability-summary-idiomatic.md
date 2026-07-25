# JS stability summary — `idiomatic`

Phase 4 D13 verdict computed from **54 consecutive passes** of
`bench/idiomatic.mjs`, each a separate node process. Thresholds are per cell on the
cross-pass RSD of mitata's `avg`: ≤ 5% verified; 5–10% publishable but the report must carry
this table; > 10% blocks publication.

STABILITY: verified

- passes: 54 (run-1.json … run-54.json)
- cells: 16
- cross-pass RSD: median 1.76% · max 3.02% (eta, cell 11)
- cells above 5%: 0 · above 10%: 0

| # | Engine | Published (median of passes) | Cross-pass min … max | RSD |
| ---: | --- | ---: | --- | ---: |
| 0 | handlebars | 5713.5 ns | 5682.0 … 5844.9 ns | 0.68% |
| 1 | eta | 4302.5 ns | 4281.2 … 4397.6 ns | 0.58% |
| 2 | handlebars | 709.7 ns | 684.3 … 737.0 ns | 1.70% |
| 3 | eta | 277.2 ns | 270.3 … 301.2 ns | 2.10% |
| 4 | handlebars | 460825.1 ns | 446191.0 … 524310.1 ns | 2.39% |
| 5 | eta | 430571.6 ns | 419512.3 … 468306.9 ns | 1.82% |
| 6 | handlebars | 8217.3 ns | 7927.5 … 9576.6 ns | 3.00% |
| 7 | eta | 6094.3 ns | 6016.1 … 6358.9 ns | 1.10% |
| 8 | handlebars | 119396.4 ns | 115683.9 … 128479.8 ns | 2.25% |
| 9 | eta | 19574.3 ns | 19313.0 … 20492.5 ns | 1.46% |
| 10 | handlebars | 7289.5 ns | 7204.2 … 7963.9 ns | 1.62% |
| 11 | eta | 8065.2 ns | 7897.8 … 9071.9 ns | 3.02% |
| 12 | handlebars | 2829.6 ns | 2770.4 … 2953.3 ns | 1.32% |
| 13 | eta | 1915.2 ns | 1860.8 … 1974.8 ns | 1.26% |
| 14 | handlebars | 5046491.0 ns | 4739828.9 … 5276455.3 ns | 2.28% |
| 15 | eta | 4770335.2 ns | 4586932.0 … 5092412.0 ns | 2.04% |

The published artifact `idiomatic.json` carries this aggregate: `stats.avg` is the median
of the per-pass averages and `stats.min`/`stats.max` span the passes, so its dispersion is
cross-process variation. Per-cell provenance is under `stats.aggregate`.
