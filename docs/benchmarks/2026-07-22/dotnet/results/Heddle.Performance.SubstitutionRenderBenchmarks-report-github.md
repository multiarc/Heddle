```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8894/25H2/2025Update/HudsonValley2)
AMD Ryzen 9 9950X 4.30GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.302
  [Host]     : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v4
  DefaultJob : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v4


```
| Method           | Mean       | Error    | StdDev   | Ratio | RatioSD | Gen0   | Gen1   | Allocated | Alloc Ratio |
|----------------- |-----------:|---------:|---------:|------:|--------:|-------:|-------:|----------:|------------:|
| RenderHeddle     |   113.6 ns |  0.67 ns |  0.52 ns |  1.00 |    0.01 | 0.0961 | 0.0001 |    1608 B |        1.00 |
| RenderFluid      |   467.4 ns |  1.49 ns |  1.32 ns |  4.12 |    0.02 | 0.1397 |      - |    2344 B |        1.46 |
| RenderScriban    | 4,027.0 ns | 21.78 ns | 20.38 ns | 35.45 |    0.23 | 2.0981 | 0.1907 |   35202 B |       21.89 |
| RenderDotLiquid  | 1,944.3 ns | 10.59 ns |  9.38 ns | 17.12 |    0.11 | 0.6180 | 0.0038 |   10384 B |        6.46 |
| RenderHandlebars |   380.0 ns |  2.56 ns |  2.27 ns |  3.35 |    0.02 | 0.0439 |      - |     736 B |        0.46 |
