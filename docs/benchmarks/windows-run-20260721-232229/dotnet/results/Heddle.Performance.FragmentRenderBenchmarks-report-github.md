```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8894/25H2/2025Update/HudsonValley2)
AMD Ryzen 9 9950X 4.30GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.302
  [Host]     : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v4
  DefaultJob : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v4


```
| Method           | Mean       | Error     | StdDev    | Ratio | RatioSD | Gen0    | Gen1   | Allocated | Alloc Ratio |
|----------------- |-----------:|----------:|----------:|------:|--------:|--------:|-------:|----------:|------------:|
| RenderHeddle     |   2.880 μs | 0.0556 μs | 0.0682 μs |  1.00 |    0.03 |  1.3733 | 0.0496 |  22.45 KB |        1.00 |
| RenderFluid      |  10.897 μs | 0.0605 μs | 0.0566 μs |  3.79 |    0.09 |  2.0905 | 0.0153 |  34.37 KB |        1.53 |
| RenderScriban    |  31.204 μs | 0.2183 μs | 0.2042 μs | 10.84 |    0.25 |  9.7656 |      - | 160.12 KB |        7.13 |
| RenderDotLiquid  | 156.771 μs | 0.7511 μs | 0.7026 μs | 54.46 |    1.25 | 54.1992 | 3.9063 | 887.27 KB |       39.53 |
| RenderHandlebars |  19.897 μs | 0.1455 μs | 0.1361 μs |  6.91 |    0.16 | 29.1443 | 2.4719 |  476.2 KB |       21.22 |
