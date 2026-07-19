```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8875/25H2/2025Update/HudsonValley2)
AMD Ryzen 9 9950X 4.30GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.302
  [Host]     : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v4
  DefaultJob : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v4


```
| Method           | Mean       | Error    | StdDev    | Ratio | RatioSD | Gen0   | Gen1   | Allocated | Alloc Ratio |
|----------------- |-----------:|---------:|----------:|------:|--------:|-------:|-------:|----------:|------------:|
| RenderHeddle     |   118.7 ns |  2.26 ns |   2.60 ns |  1.00 |    0.03 | 0.0961 |      - |    1608 B |        1.00 |
| RenderFluid      |   538.0 ns | 10.12 ns |   9.94 ns |  4.53 |    0.13 | 0.1392 |      - |    2344 B |        1.46 |
| RenderScriban    | 4,414.0 ns | 88.04 ns | 158.75 ns | 37.20 |    1.54 | 2.0752 | 0.1831 |   35200 B |       21.89 |
| RenderDotLiquid  | 2,037.9 ns | 33.40 ns |  37.13 ns | 17.18 |    0.47 | 0.6199 | 0.0038 |   10384 B |        6.46 |
| RenderHandlebars |   398.3 ns |  4.94 ns |   4.38 ns |  3.36 |    0.08 | 0.0439 |      - |     736 B |        0.46 |
