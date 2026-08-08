# JS stability summary — `controlled`

Phase 4 D13 verdict computed from **38 consecutive passes** of
`bench/controlled.mjs`, each a separate node process. Thresholds are per cell on the
cross-pass RSD of mitata's `avg`: ≤ 5% verified; 5–10% publishable but the report must carry
this table; > 10% blocks publication.

STABILITY: verified-with-disclosure

- passes: 38 (run-1.json … run-38.json)
- cells: 16
- cross-pass RSD: median 1.41% · max 5.11% (handlebars, cell 6)
- cells above 5%: 1 · above 10%: 0

| # | Engine | Published (median of passes) | Cross-pass min … max | RSD |
| ---: | --- | ---: | --- | ---: |
| 0 | handlebars | 5414.3 ns | 5270.0 … 5505.8 ns | 0.91% |
| 1 | eta | 4235.4 ns | 4180.9 … 4309.9 ns | 0.67% |
| 2 | handlebars | 742.5 ns | 719.3 … 767.6 ns | 1.43% |
| 3 | eta | 135.4 ns | 130.7 … 147.3 ns | 2.37% |
| 4 | handlebars | 407297.3 ns | 401000.2 … 420329.5 ns | 0.92% |
| 5 | eta | 250076.0 ns | 247647.6 … 256711.5 ns | 0.86% |
| 6 | handlebars | 7667.3 ns | 7295.7 … 9421.2 ns | 5.11% |
| 7 | eta | 3004.4 ns | 2937.9 … 3034.5 ns | 0.83% |
| 8 | handlebars | 101570.7 ns | 93572.9 … 110068.9 ns | 4.33% |
| 9 | eta | 10537.6 ns | 10419.7 … 10679.0 ns | 0.51% |
| 10 | handlebars | 6650.6 ns | 6571.0 … 7295.3 ns | 2.61% |
| 11 | eta | 4809.3 ns | 4754.6 … 5062.3 ns | 1.60% |
| 12 | handlebars | 2644.9 ns | 2593.6 … 2735.4 ns | 1.04% |
| 13 | eta | 1825.6 ns | 1757.0 … 1879.7 ns | 1.70% |
| 14 | handlebars | 4712629.5 ns | 4569566.7 … 4866639.0 ns | 1.58% |
| 15 | eta | 4436398.7 ns | 4301750.6 … 4578670.9 ns | 1.40% |

The published artifact `controlled.json` carries this aggregate: `stats.avg` is the median
of the per-pass averages and `stats.min`/`stats.max` span the passes, so its dispersion is
cross-process variation. Per-cell provenance is under `stats.aggregate`.
