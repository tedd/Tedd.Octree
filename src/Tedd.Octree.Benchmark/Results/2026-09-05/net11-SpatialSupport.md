```

BenchmarkDotNet v0.16.0-preview.1, Windows 11 (10.0.26200.9168/25H2/2025Update/HudsonValley2)
AMD Ryzen 9 5950X 3.40GHz, 1 CPU, 32 logical and 16 physical cores
Memory: 56.67 GB Total, 35.47 GB Available
.NET SDK 11.0.100-preview.7.26381.103
  [Host]   : .NET 11.0.0 (11.0.0-preview.7.26381.103, 11.0.26.38203), X64 RyuJIT x86-64-v3
  ShortRun : .NET 11.0.0 (11.0.0-preview.7.26381.103, 11.0.26.38203), X64 RyuJIT x86-64-v3

Job=ShortRun  IterationCount=3  LaunchCount=1
WarmupCount=3

```
| Method        | Moving | Mean      | Error      | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|-------------- |------- |----------:|-----------:|----------:|------:|--------:|----------:|------------:|
| **PointSupport**  | **False**  |  **57.41 ns** |   **9.395 ns** |  **0.515 ns** |  **1.00** |    **0.00** |         **-** |          **NA** |
| AreaSupport   | False  |  61.54 ns |   3.644 ns |  0.200 ns |  1.07 |    0.01 |         - |          NA |
| CachedSupport | False  |  34.09 ns |  16.429 ns |  0.901 ns |  0.59 |    0.01 |         - |          NA |
|               |        |           |            |           |       |         |           |             |
| **PointSupport**  | **True**   |  **84.23 ns** |  **76.804 ns** |  **4.210 ns** |  **1.00** |    **0.00** |         **-** |          **NA** |
| AreaSupport   | True   |  82.63 ns |  11.720 ns |  0.642 ns |  0.98 |    0.04 |         - |          NA |
| CachedSupport | True   | 871.09 ns | 269.994 ns | 14.799 ns | 10.36 |    0.49 |         - |          NA |
