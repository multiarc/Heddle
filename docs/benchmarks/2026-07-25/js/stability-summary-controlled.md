# JS stability summary — `controlled`

Phase 4 D13 verdict computed from **54 consecutive passes** of
`bench/controlled.mjs`, each a separate node process. Thresholds are per cell on the
cross-pass RSD of mitata's `avg`: ≤ 5% verified; 5–10% publishable but the report must carry
this table; > 10% blocks publication.

STABILITY: verified

- passes: 54 (run-1.json … run-54.json)
- cells: 16
- cross-pass RSD: median 1.76% · max 3.45% (handlebars, cell 6)
- cells above 5%: 0 · above 10%: 0

| # | Engine | Published (median of passes) | Cross-pass min … max | RSD |
| ---: | --- | ---: | --- | ---: |
| 0 | handlebars | 5726.5 ns | 5685.1 … 5882.9 ns | 0.57% |
| 1 | eta | 4273.3 ns | 4255.8 … 4356.3 ns | 0.44% |
| 2 | handlebars | 853.0 ns | 824.8 … 888.9 ns | 1.36% |
| 3 | eta | 141.4 ns | 135.5 … 151.2 ns | 2.79% |
| 4 | handlebars | 525036.0 ns | 508407.5 … 534988.8 ns | 1.18% |
| 5 | eta | 295628.3 ns | 291009.4 … 305666.3 ns | 1.29% |
| 6 | handlebars | 9646.8 ns | 9289.2 … 11069.1 ns | 3.45% |
| 7 | eta | 3709.9 ns | 3627.1 … 3881.5 ns | 1.38% |
| 8 | handlebars | 123514.6 ns | 118474.7 … 132576.9 ns | 2.43% |
| 9 | eta | 14244.0 ns | 13829.6 … 15099.1 ns | 2.24% |
| 10 | handlebars | 8415.3 ns | 8286.2 … 9122.9 ns | 2.04% |
| 11 | eta | 5769.6 ns | 5715.5 … 6175.3 ns | 1.64% |
| 12 | handlebars | 3016.0 ns | 2949.1 … 3394.1 ns | 2.40% |
| 13 | eta | 1909.5 ns | 1858.5 … 2030.1 ns | 1.59% |
| 14 | handlebars | 5243033.6 ns | 5031571.8 … 5436085.4 ns | 2.09% |
| 15 | eta | 4774380.9 ns | 4557464.0 … 4964556.5 ns | 1.87% |

The published artifact `controlled.json` carries this aggregate: `stats.avg` is the median
of the per-pass averages and `stats.min`/`stats.max` span the passes, so its dispersion is
cross-process variation. Per-cell provenance is under `stats.aggregate`.
