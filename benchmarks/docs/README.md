# Benchmark contract documentation

The operational contract the cross-stack benchmark harnesses implement. The program that
produced it is complete; its collapsed decision record lives in
[cross-cutting-decisions.md § Program record — cross-stack benchmarks](../../docs/spec/common/cross-cutting-decisions.md#program-record--cross-stack-benchmarks-closed).
The reproduce book is [benchmarks/README.md](../README.md).

| Document | What it pins |
| --- | --- |
| [workloads.md](workloads.md) | The eight workloads: template shapes per engine, model data, expected output characteristics |
| [parity-contract-v2.md](parity-contract-v2.md) | The parity contract: normalization pipeline N1–N5 (+N3b), entity canonicalization, untrusted-data alphabet, controlled/idiomatic gates, exclusion policy |
| [golden-corpus.md](golden-corpus.md) | Golden oracle corpus: on-disk format, manifest, export tool, verifier definitions, regeneration policy |
| [metrics-protocol.md](metrics-protocol.md) | Statistic mapping, presentation rules, machine record, report format, honest-reporting rules |
| [report-assembly.md](report-assembly.md) | Consolidated-report assembly: verbatim-excerpt rule, permitted arithmetic, publication layout |
| [rust-workload-ports.md](rust-workload-ports.md) | Rust: Askama + Tera ports, the 32 normative cell texts, budget and measurement rules |
| [jvm-construct-mapping.md](jvm-construct-mapping.md) | JVM: JTE + Thymeleaf construct mapping |
| [jvm-harness-and-jmh.md](jvm-harness-and-jmh.md) | JVM: JMH harness settings and run procedure |
| [jvm-thymeleaf-feasibility.md](jvm-thymeleaf-feasibility.md) | JVM: Thymeleaf controlled-track feasibility probe and exclusion-evidence procedure |
| [js-harness-and-run.md](js-harness-and-run.md) | JS: mitata harness, materialisation checks, run procedure |
| [js-templates-and-models.md](js-templates-and-models.md) | JS: Handlebars + Eta templates and models |
| [python-harness.md](python-harness.md) | Python: pyperf harness, in-worker gate, tracemalloc memory pass |
| [python-templates.md](python-templates.md) | Python: Jinja2 + Mako templates |
| [go-port-mapping.md](go-port-mapping.md) | Go: html/template + templ port mapping |
| [go-harness-and-measurement.md](go-harness-and-measurement.md) | Go: testing/benchstat measurement, layout and toolchain rules |
| [go-templ-feasibility.md](go-templ-feasibility.md) | Go: templ feasibility spike |
| [linux-environment-and-toolchains.md](linux-environment-and-toolchains.md) | Linux cross-check: environment capture and toolchain install |
| [linux-harness-settings-and-validation.md](linux-harness-settings-and-validation.md) | Linux cross-check: harness settings, validation tool, material-divergence thresholds |
