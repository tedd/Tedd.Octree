```

BenchmarkDotNet v0.16.0-preview.1, Windows 11 (10.0.26200.9168/25H2/2025Update/HudsonValley2)
AMD Ryzen 9 5950X 3.40GHz, 1 CPU, 32 logical and 16 physical cores
Memory: 56.67 GB Total, 34.41 GB Available
.NET SDK 11.0.100-preview.7.26381.103
  [Host]   : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3
  ShortRun : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3

Job=ShortRun  IterationCount=3  LaunchCount=1
WarmupCount=3

```
| Method        | Moving | Mean      | Error      | StdDev   | Ratio | RatioSD | Allocated | Alloc Ratio |
|-------------- |------- |----------:|-----------:|---------:|------:|--------:|----------:|------------:|
| **PointSupport**  | **False**  |  **60.77 ns** |  **12.859 ns** | **0.705 ns** |  **1.00** |    **0.00** |         **-** |          **NA** |
| AreaSupport   | False  |  61.40 ns |  12.530 ns | 0.687 ns |  1.01 |    0.01 |         - |          NA |
| CachedSupport | False  |  20.04 ns |   5.830 ns | 0.320 ns |  0.33 |    0.01 |         - |          NA |
|               |        |           |            |          |       |         |           |             |
| **PointSupport**  | **True**   |  **80.75 ns** |   **3.058 ns** | **0.168 ns** |  **1.00** |    **0.00** |         **-** |          **NA** |
| AreaSupport   | True   |  83.70 ns |  13.670 ns | 0.749 ns |  1.04 |    0.01 |         - |          NA |
| CachedSupport | True   | 837.78 ns | 156.871 ns | 8.599 ns | 10.37 |    0.09 |         - |          NA |
