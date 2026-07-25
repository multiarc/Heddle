```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8894/25H2/2025Update/HudsonValley2)
AMD Ryzen 9 9950X 4.30GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.302
  [Host]     : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v4
  DefaultJob : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v4


```
| Method           | Mean      | Error    | StdDev   | Ratio | RatioSD | Gen0    | Gen1   | Allocated  | Alloc Ratio |
|----------------- |----------:|---------:|---------:|------:|--------:|--------:|-------:|-----------:|------------:|
| RenderHeddle     |  13.97 μs | 0.039 μs | 0.030 μs |  1.00 |    0.00 |  6.0120 | 0.6561 |   98.33 KB |        1.00 |
| RenderFluid      |  40.12 μs | 0.234 μs | 0.219 μs |  2.87 |    0.02 |  3.2959 | 0.1221 |   54.94 KB |        0.56 |
| RenderScriban    |  93.55 μs | 0.569 μs | 0.532 μs |  6.69 |    0.04 | 16.6016 | 3.2959 |   271.7 KB |        2.76 |
| RenderDotLiquid  | 341.66 μs | 2.782 μs | 2.603 μs | 24.45 |    0.19 | 90.8203 |      - | 1485.26 KB |       15.11 |
| RenderHandlebars |  34.06 μs | 0.387 μs | 0.323 μs |  2.44 |    0.02 |  4.0894 | 0.3662 |   67.08 KB |        0.68 |
