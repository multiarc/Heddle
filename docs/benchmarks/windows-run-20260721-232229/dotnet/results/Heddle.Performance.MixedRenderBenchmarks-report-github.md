```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8894/25H2/2025Update/HudsonValley2)
AMD Ryzen 9 9950X 4.30GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.302
  [Host]     : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v4
  DefaultJob : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v4


```
| Method           | Mean      | Error     | StdDev    | Ratio | RatioSD | Gen0    | Gen1   | Allocated | Alloc Ratio |
|----------------- |----------:|----------:|----------:|------:|--------:|--------:|-------:|----------:|------------:|
| RenderHeddle     |  2.601 μs | 0.0515 μs | 0.0670 μs |  1.00 |    0.04 |  2.6779 | 0.1907 |  43.88 KB |        1.00 |
| RenderFluid      |  8.680 μs | 0.0502 μs | 0.0469 μs |  3.34 |    0.09 |  1.7700 |      - |  28.99 KB |        0.66 |
| RenderScriban    | 19.539 μs | 0.1151 μs | 0.1076 μs |  7.52 |    0.19 |  7.4463 | 1.1597 |  121.7 KB |        2.77 |
| RenderDotLiquid  | 54.698 μs | 0.1096 μs | 0.0915 μs | 21.04 |    0.53 | 17.3340 | 1.3428 | 283.67 KB |        6.47 |
| RenderHandlebars |  7.285 μs | 0.0425 μs | 0.0398 μs |  2.80 |    0.07 |  2.6779 | 0.2060 |  43.83 KB |        1.00 |
