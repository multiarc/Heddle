```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8894/25H2/2025Update/HudsonValley2)
AMD Ryzen 9 9950X 4.30GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.302
  [Host]     : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v4
  DefaultJob : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v4


```
| Method           | Mean        | Error     | StdDev    | Ratio | RatioSD | Gen0   | Gen1   | Allocated | Alloc Ratio |
|----------------- |------------:|----------:|----------:|------:|--------:|-------:|-------:|----------:|------------:|
| RenderHeddle     |    669.0 ns |   4.24 ns |   3.31 ns |  1.00 |    0.01 | 0.4072 | 0.0038 |   6.66 KB |        1.00 |
| RenderFluid      |  2,163.7 ns |  10.06 ns |   9.41 ns |  3.23 |    0.02 | 0.3433 |      - |   5.61 KB |        0.84 |
| RenderScriban    |  8,528.9 ns |  60.60 ns |  56.69 ns | 12.75 |    0.10 | 2.8687 | 0.2441 |  47.13 KB |        7.07 |
| RenderDotLiquid  | 24,952.9 ns | 156.08 ns | 146.00 ns | 37.30 |    0.28 | 4.4556 | 0.2747 |  73.22 KB |       10.99 |
| RenderHandlebars |  1,733.8 ns |  14.15 ns |  13.24 ns |  2.59 |    0.02 | 0.1354 |      - |   2.22 KB |        0.33 |
