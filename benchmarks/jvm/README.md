# JVM benchmark harness

The Phase 3 (JVM) entry of the cross-stack benchmark program: JTE 3.2.4 and
Thymeleaf 3.1.5.RELEASE rendering the eight Phase 1 corpus workloads in both fairness
tracks under JMH 1.37, gated by parity contract v2.

Spec (normative): [docs/spec/cross-stack-benchmarks/phase-3-jvm/](../../docs/spec/cross-stack-benchmarks/phase-3-jvm/README.md)
— see [harness-and-jmh.md](../../docs/spec/cross-stack-benchmarks/phase-3-jvm/harness-and-jmh.md)
for the full layout, build, gate, and run procedure.

## Quick start

Requires Eclipse Temurin 25 (LTS) — the measurement pin (spec D10). From this directory:

```
./mvnw -q clean verify          # build target/benchmarks.jar + run the gate calibration
java -cp target/benchmarks.jar heddle.benchmarks.jvm.gate.GateCli probe       # D5 escaper probe
java -cp target/benchmarks.jar heddle.benchmarks.jvm.gate.GateCli calibrate   # verifier calibration
java -cp target/benchmarks.jar heddle.benchmarks.jvm.gate.GateCli gate        # all 32 parity cells
```

The corpus is read from `../../src/Heddle.Performance/GoldenCorpus/` (override with
`-Dheddle.corpus=<path>`); every entry is SHA-256-verified against `manifest.json` at load
(exit 2 on mismatch or absence).

Measurement run (protocol machine only — spec D9/D10 and the run procedure):

```
java -jar target/benchmarks.jar -prof gc -rf json -rff jmh-result.json
```
