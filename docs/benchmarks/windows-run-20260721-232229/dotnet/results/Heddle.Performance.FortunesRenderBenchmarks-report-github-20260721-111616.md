```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8894/25H2/2025Update/HudsonValley2)
AMD Ryzen 9 9950X 4.30GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.302
  [Host] : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v4
  Dry    : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v4

Job=Dry  IterationCount=1  LaunchCount=1  
RunStrategy=ColdStart  UnrollFactor=1  WarmupCount=1  

```
| Method           | Mean | Error | Ratio | RatioSD | Alloc Ratio |
|----------------- |-----:|------:|------:|--------:|------------:|
| RenderHeddle     |   NA |    NA |     ? |       ? |           ? |
| RenderFluid      |   NA |    NA |     ? |       ? |           ? |
| RenderScriban    |   NA |    NA |     ? |       ? |           ? |
| RenderDotLiquid  |   NA |    NA |     ? |       ? |           ? |
| RenderHandlebars |   NA |    NA |     ? |       ? |           ? |

Benchmarks with issues:
  FortunesRenderBenchmarks.RenderHeddle: Dry(IterationCount=1, LaunchCount=1, RunStrategy=ColdStart, UnrollFactor=1, WarmupCount=1)
  FortunesRenderBenchmarks.RenderFluid: Dry(IterationCount=1, LaunchCount=1, RunStrategy=ColdStart, UnrollFactor=1, WarmupCount=1)
  FortunesRenderBenchmarks.RenderScriban: Dry(IterationCount=1, LaunchCount=1, RunStrategy=ColdStart, UnrollFactor=1, WarmupCount=1)
  FortunesRenderBenchmarks.RenderDotLiquid: Dry(IterationCount=1, LaunchCount=1, RunStrategy=ColdStart, UnrollFactor=1, WarmupCount=1)
  FortunesRenderBenchmarks.RenderHandlebars: Dry(IterationCount=1, LaunchCount=1, RunStrategy=ColdStart, UnrollFactor=1, WarmupCount=1)
