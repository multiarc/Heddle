# Benchmark run — 2026-07-11

Raw [BenchmarkDotNet](https://benchmarkdotnet.org/) artifacts for the Heddle competitor
head-to-head (D1/D2). Reproduce with `dotnet run -c Release --project src/Heddle.Performance`.

## Environment

```
BenchmarkDotNet v0.15.8 · Windows 11 (10.0.26200.8655/25H2)
AMD Ryzen 9 9950X 4.30GHz, 16 physical / 32 logical cores
.NET SDK 10.0.301 · .NET 10.0.9 runtime, X64 RyuJIT x86-64-v4
Commit 8341bb67
```

The formatted results tables (render + compile) with ratios live in the
[README Performance section](../../../README.md#performance). The parity contract that makes the
comparison apples-to-apples is documented in
[src/Heddle.Performance/Runners](../../../src/Heddle.Performance/Runners/README.md).

## Files

Each suite has a rendered Markdown report (linked) plus `-report.csv` and `-report.html`
siblings in this directory.

- Render — [`Heddle.Performance.TextRenderBenchmarks-report-github.md`](./Heddle.Performance.TextRenderBenchmarks-report-github.md)
  (+ `Heddle.Performance.TextRenderBenchmarks-report.csv`, `Heddle.Performance.TextRenderBenchmarks-report.html`)
- Compile — [`Heddle.Performance.TemplateParseBenchmarks-report-github.md`](./Heddle.Performance.TemplateParseBenchmarks-report-github.md)
  (+ `Heddle.Performance.TemplateParseBenchmarks-report.csv`, `Heddle.Performance.TemplateParseBenchmarks-report.html`)
