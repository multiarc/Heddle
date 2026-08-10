# JVM benchmark harness

The JVM entry of the cross-stack benchmark program: JTE 3.2.4 and
Thymeleaf 3.1.5.RELEASE rendering the eight golden-corpus workloads in both fairness
tracks under JMH 1.37, gated by
[parity contract v2](../docs/parity-contract-v2.md).

See [jvm-harness-and-jmh.md](../docs/jvm-harness-and-jmh.md) for the full layout, build,
gate, and run procedure; the workloads are defined in [workloads.md](../docs/workloads.md),
the corpus in [golden-corpus.md](../docs/golden-corpus.md), the per-engine construct
choices in [jvm-construct-mapping.md](../docs/jvm-construct-mapping.md), the measurement
protocol in [metrics-protocol.md](../docs/metrics-protocol.md), and the Thymeleaf
controlled-track evidence in [jvm-thymeleaf-feasibility.md](../docs/jvm-thymeleaf-feasibility.md).

## Quick start

Requires Eclipse Temurin 25 (LTS) — the measurement pin. From this directory:

```
./mvnw -q clean verify          # build target/benchmarks.jar + run the gate calibration
java -cp target/benchmarks.jar heddle.benchmarks.jvm.gate.GateCli probe       # escaper probe
java -cp target/benchmarks.jar heddle.benchmarks.jvm.gate.GateCli calibrate   # verifier calibration
java -cp target/benchmarks.jar heddle.benchmarks.jvm.gate.GateCli gate        # all 32 parity cells
```

The corpus is read from `../../benchmarks/dotnet/GoldenCorpus/` (override with
`-Dheddle.corpus=<path>`); every entry is SHA-256-verified against `manifest.json` at load
(exit 2 on mismatch or absence).

Measurement run (protocol machine only — see the run procedure in
[jvm-harness-and-jmh.md](../docs/jvm-harness-and-jmh.md)):

```
java -jar target/benchmarks.jar -prof gc -rf json -rff jmh-result.json
```
